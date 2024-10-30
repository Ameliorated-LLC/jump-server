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

    private bool _authenticated = false;

    public void Show()
    {
        Canvas.Initialize(new Frame("Jump", _options.Count + 4, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, Color.GhostWhite, (Color?)null) },
            new DynamicBar() { Center = new Text("Press Ctrl + X to access admin menu", Color.LightGoldenrodYellow, (Color?)null) }));

        if (_options.Count < 1)
            throw new ArgumentException();

        using var pingCancel = new CancellationTokenSource();
        for (var i = 0; i < _options.Count; i++)
        {
            Canvas.WriteFrameLine(i, 0, $"   {_options[i].Location.Name} ({_options[i].Location.Username})", Color.LightGoldenrodYellow);
            Canvas.WriteFrame(i, -6 -_options[i].Location.Ping.Length, (_options[i].Location.Connected == true ?  (_options[i].Location.Ping + "ms ").ToColored(Color.GhostWhite)  : "   0ms ".ToColored(Color.DimGray)) + "💠 ".ToColored(_options[i].Location.Connected == null ? Color.DimGray : _options[i].Location.Connected!.Value ? Color.Green : Color.Red));
            if (_options[i].Location.Connected == null)
            {
                int testIndex = i;
                var token = pingCancel.Token;
                Task.Run(() => TestConnection(_options[testIndex], new Random().Next(1000, 1500), token));
            }
            else if (_options[i].Location.Connected == true)
            {
                int testIndex = i;
                var token = pingCancel.Token;
                Task.Run(() => LoopPing(_options[testIndex], token));
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
            if (keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                index = -1;
                break;
            }

            if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.S || keyInfo.Key == ConsoleKey.Tab)
                Select(ref index, index + 1);
            if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.W)
                Select(ref index, index - 1);
        }

        pingCancel.Cancel();
        _lock.Wait();

        if (index != -1)
        {
            Program.Exit(null, null);
            Console.WriteLine();
            RunSSHCommand(_options[index].Location.IP, _options[index].Location.Port, _options[index].Location.Username);
            Environment.Exit(0);
            Thread.Sleep(Timeout.Infinite);
        }

        if (index == -1)
        {
            if (!Program.Authenticated && Configuration.Current.AdminPassword != null)
            {
                Program.Authenticated = AuthenticateMenu.Show();
                if (!Program.Authenticated)
                    return;
            }

            AdminMenu.Show();

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

        lock (_selectLock)
        {
            _options[index].Selected = false;
            Canvas.WriteFrameLine(index, 0, $"   {_options[index].Location.Name} ({_options[index].Location.Username})", Color.LightGoldenrodYellow);
            Canvas.WriteFrame(index, -6 -_options[index].Location.Ping.Length, 
                (_options[index].Location.Connected == true ?  (_options[index].Location.Ping + "ms ").ToColored(Color.GhostWhite)  : "   0ms ".ToColored(Color.DimGray)) + "💠 ".ToColored(_options[index].Location.Connected == null ? Color.DimGray : _options[index].Location.Connected!.Value ? Color.Green : Color.Red));
            index = newIndex;
            _options[index].Selected = true;
            Canvas.WriteFrameLine(index, 0, $" > {_options[index].Location.Name} ({_options[index].Location.Username})", Color.Black, Color.GhostWhite);
            Canvas.WriteFrame(index, -6 -_options[index].Location.Ping.Length, 
                (_options[index].Location.Connected == true ?  (_options[index].Location.Ping + "ms ").ToColored(Color.Black)  : "   0ms ".ToColored(Color.DimGray)) + "💠 ".ToColored(_options[index].Location.Connected == null ? Color.DimGray : _options[index].Location.Connected!.Value ? Color.Green : Color.Red),
                null, Color.GhostWhite);
        }
    }

    private void TestConnection(Option option, int timeout, CancellationToken cancellationToken)
    {
        bool result;
        try
        {
            using (TcpClient client = new TcpClient())
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                client.ConnectAsync(option.Location.IP, option.Location.Port).Wait(timeout);
                stopwatch.Stop();

                result = client.Connected;

                if (result)
                {
                    try
                    {
                        using var ping = new Ping();
                        var reply = ping.SendPingAsync(option.Location.IP, TimeSpan.FromMilliseconds(1500), cancellationToken: cancellationToken).GetAwaiter().GetResult();
                        if (reply.Status == IPStatus.Success)
                            option.Location.Ping = new string(' ', -(Math.Min((int)reply.RoundtripTime.ToString().Length, 4) - 4)) + (int)reply.RoundtripTime; 
                        else
                            throw new Exception();
                    }
                    catch (Exception e)
                    {
                        option.Location.Ping = new string(' ', -(Math.Min((int)stopwatch.ElapsedMilliseconds.ToString().Length, 4) - 4)) + (int)stopwatch.ElapsedMilliseconds; 
                    }
                }
                    
            }
        }
        catch (Exception ex)
        {
            result = false;
        }

        option.Location.Connected = result;

        lock (_selectLock)
        {
            if (_lock.Wait(0))
            {
                try
                {
                    Canvas.WriteFrame(_options.IndexOf(option), -6 + -option.Location.Ping.Length, (result ?  (option.Location.Ping + "ms ").ToColored(option.Selected ? Color.Black : Color.GhostWhite)  : "   0ms ".ToColored(Color.DimGray)) + "💠 ".ToColored(result ? Color.Green : Color.Red), null, option.Selected ? Color.GhostWhite : null);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        if (!result)
            return;

        Task.Run(() => LoopPing(option, cancellationToken));
    }

    private void LoopPing(Option option, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        while (true)
        {
            try
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                
                    var reply = ping.SendPingAsync(option.Location.IP, TimeSpan.FromMilliseconds(1500), cancellationToken: cancellationToken).GetAwaiter().GetResult();
                    if (reply.Status == IPStatus.Success)
                    {
                        option.Location.Ping = new string(' ', -(Math.Min((int)reply.RoundtripTime.ToString().Length, 4) - 4)) + (int)reply.RoundtripTime; 
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(3000);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (Exception e)
            {
                Thread.Sleep(3000);
                continue;
            }
            
            if (cancellationToken.IsCancellationRequested)
                return;
            lock (_selectLock)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                if (_lock.Wait(0))
                {
                    try
                    {
                        Canvas.WriteFrame(_options.IndexOf(option), -6 + -option.Location.Ping.Length, (option.Location.Ping + "ms ").ToColored(option.Selected ? Color.Black : Color.GhostWhite) + "💠 ".ToColored(Color.Green), null, option.Selected ? Color.GhostWhite : null);
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }
        }
    }

    private static SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private static object _selectLock = new object();

    private class Option
    {
        public Location Location { get; set; }
        
        public bool Selected = false;
    }
}