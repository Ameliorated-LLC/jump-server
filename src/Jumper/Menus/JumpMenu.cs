using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Net.Sockets;

// ReSharper disable InconsistentlySynchronizedField

namespace JumpServer.Menus;

public class JumpMenu
{
    private List<Option> _options = [];

    public JumpMenu()
    {
        Configuration.Current.Locations.ForEach(x => _options.Add(new Option() { Location = x }));
    }

    public void Show(bool admin)
    {
        Canvas.Set(new Frame(Configuration.Current.ServerName, _options.Count + 4, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            admin || Program.CommandLineOptions.RestrictAdminAccess ? null : new DynamicBar() { Center = new Text("Press Ctrl + X to unlock admin options", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() },
            !admin ? null : new BottomBar() { Items = [
                " ^A ".ToColored(null, AnsiColor.Grey35) + " Add Entry ".ToColored(null, AnsiColor.Grey19),
                " ^D ".ToColored(null, AnsiColor.Grey35) + " Delete Entry ".ToColored(null, AnsiColor.Grey19),
                " ^E ".ToColored(null, AnsiColor.Grey35) + " Edit Entry ".ToColored(null, AnsiColor.Grey19),
                " ^X ".ToColored(null, AnsiColor.Grey35) + " Lock ".ToColored(null, AnsiColor.Grey19),
                ] }));

        if (_options.Count < 1)
            throw new ArgumentException();

        lock (_writeLock)
        {
            for (var i = 0; i < _options.Count; i++)
            {
                _options[i].Location.PropertyChanged += LocationOnPropertyChanged!;
                Canvas.WriteFrameLine(i, 0, $"   {_options[i].Location.Name} ({_options[i].Location.Username})", AnsiColor.Cornsilk1);
                Canvas.WriteFrame(i, -10,
                    (_options[i].Location.Connected == true ? (PingTo4CharText(_options[i].Location.Ping) + "ms ").ToColored(AnsiColor.Grey93) : "   0ms ".ToColored(AnsiColor.Grey23)) +
                    "• ".ToColored(_options[i].Location.Connected == null ? AnsiColor.Grey23 : _options[i].Location.Connected!.Value ? AnsiColor.Green : AnsiColor.Red));
            }
        }

        try
        {
            _lock.Release();
        }
        catch { }

        var index = 0;
        Select(ref index, 0);

        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && !Program.CommandLineOptions.RestrictAdminAccess)
            {
                if (Program.Authenticated)
                {
                    Program.Authenticated = false;
                    index = -2;
                    break;
                }

                index = -1;
                break;
            }
            if (keyInfo.Key == ConsoleKey.E && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && Program.Authenticated)
            {
                EntryMenu.Edit(Configuration.Current.Locations[index]);
                File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                return;
            }
            if (keyInfo.Key == ConsoleKey.D && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && Program.Authenticated)
            {
                if (DeleteConfirmMenu.Show(Configuration.Current.Locations[index].Name))
                {
                    Configuration.Current.Locations[index].Dispose();
                    Configuration.Current.Locations.Remove(Configuration.Current.Locations[index]);
                    File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                }
                return;
            }
            if (keyInfo.Key == ConsoleKey.A && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && Program.Authenticated)
            {
                var result = AddEntryMenu.Show();
                if (result.Location != null)
                {
                    LocationSetupMenu.Show(result.Location, result.ImportKey, result.DisablePasswordAuth, result.RandomizeSSHPort, result.RequireTOTP);
                }
                return;
            }

            if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S || keyInfo.Key == ConsoleKey.Tab)
                Select(ref index, index + 1);
            if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                Select(ref index, index - 1);
        }

        _options.ForEach(x => x.Location.PropertyChanged -= LocationOnPropertyChanged!);
        _lock.Wait();

        if (index > -1)
        {
            Program.Exit(null, null);
            Console.WriteLine();
            RunSSHCommand(_options[index].Location.IP, _options[index].Location.Port, _options[index].Location.Username);
            Environment.Exit(0);
            Thread.Sleep(Timeout.Infinite);
        }

        if (index == -1)
        {
            if (!Program.Authenticated && !string.IsNullOrEmpty(Configuration.Current.AdminPassword))
            {
                Program.Authenticated = AuthenticateMenu.Show();
                //if (!Program.Authenticated)
                //    return;
            }

            return;
        }
    }

    private void LocationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Location.Connected) && e.PropertyName != nameof(Location.Ping))
            return;

        try
        {
            var location = (Location)sender;
            
            lock (_writeLock)
            {
                if (_lock.Wait(0))
                {
                    try
                    {
                        var option = _options.First(x => x.Location == location);
                        Canvas.WriteFrame(_options.IndexOf(option), -10, (location.Connected == true ? (PingTo4CharText(location.Ping) + "ms ").ToColored(option.Selected ? AnsiColor.Black : AnsiColor.Grey93)  : "   0ms ".ToColored(AnsiColor.Grey23)) + "• ".ToColored(location.Connected == true ? AnsiColor.Green : AnsiColor.Red), null, option.Selected ? AnsiColor.Grey93 : null);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }
        }
        catch
        {
            return;
        }
    }

    static void RunSSHCommand(string hostname, int port, string username)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ssh",
                Arguments = $"-p \"{port}\" \"{username}@{hostname}\"",
            }
        };

        process.Start();
        process.WaitForExit();
    }

    private void Select(ref int index, int newIndex)
    {
        if (newIndex > _options.Count - 1 || newIndex < 0)
            return;

        lock (_writeLock)
        {
            _options[index].Selected = false;
            Canvas.WriteFrameLine(index, 0, $"   {_options[index].Location.Name} ({_options[index].Location.Username})", AnsiColor.Cornsilk1);
            Canvas.WriteFrame(index, -10, 
                (_options[index].Location.Connected == true ? (PingTo4CharText(_options[index].Location.Ping) + "ms ").ToColored(AnsiColor.Grey93)  : "   0ms ".ToColored(AnsiColor.Grey23)) + "• ".ToColored(_options[index].Location.Connected == null ? AnsiColor.Grey23 : _options[index].Location.Connected!.Value ? AnsiColor.Green : AnsiColor.Red));
            index = newIndex;
            _options[index].Selected = true;
            Canvas.WriteFrameLine(index, 0, $" > {_options[index].Location.Name} ({_options[index].Location.Username})", AnsiColor.Black, AnsiColor.Grey93);
            Canvas.WriteFrame(index, -10, 
                (_options[index].Location.Connected == true ? (PingTo4CharText(_options[index].Location.Ping) + "ms ").ToColored(AnsiColor.Black)  : "   0ms ".ToColored(AnsiColor.Grey23)) + "• ".ToColored(_options[index].Location.Connected == null ? AnsiColor.Grey23 : _options[index].Location.Connected!.Value ? AnsiColor.Green : AnsiColor.Red),
                null, AnsiColor.Grey93);
        }
    }

    private static string PingTo4CharText(int ping) => new string(' ', -(Math.Min(ping.ToString().Length, 4) - 4)) + (ping.ToString().Length > 4 ? ping.ToString().Substring(0, 4) :  ping.ToString());


    private static SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private static object _writeLock = new object();

    private class Option
    {
        public Location Location { get; set; }
        
        public bool Selected = false;
    }
}