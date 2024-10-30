using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace JumpServer.Menus;

public class LocationSetupMenu
{
    public static void Show(Location location)
    {
        Canvas.Initialize(new Frame("Entry Setup", 20, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, Color.GhostWhite, (Color?)null) },
            new DynamicBar() { Center = new Text("Press Ctrl + X to cancel setup", Color.LightGoldenrodYellow, (Color?)null) }
           ));

        string? password = null;
        
        Canvas.WriteFrame(0, 1, $"Connecting to '{location.Username}@{location.IP}'...", Color.LightGoldenrodYellow);
        try
        {
            while (true)
            {
                Canvas.WriteFrameLine(1, 0, "");
                Canvas.WriteFrameLine(2, 0, "");
                var connectionInfo = new ConnectionInfo(location.IP, location.Port, location.Username, [
                    new PasswordAuthenticationMethod(location.Username, password ?? ""),
                    //new PrivateKeyAuthenticationMethod(location.Username, new PrivateKeyFile[] 
                    //   { new PrivateKeyFile("", "") }),
                ]);
                using (var client = new SshClient(connectionInfo))
                {
                    client.HostKeyReceived += (sender, e) =>
                    {
                        if (e.FingerPrint != null && e.HostKey != null)
                        {
                            string knownHostEntry = FormatKnownHostEntry(location.IP, e.HostKeyName, e.HostKey);
                            try
                            {
                                File.AppendAllText("/etc/jump-server/knownhosts", Environment.NewLine + knownHostEntry);
                            }
                            catch (Exception exception)
                            {
                                Program.Exit(null, null);
                                Console.WriteLine(exception);
                                Environment.Exit(1);
                                Thread.Sleep(Timeout.Infinite);
                            }
                        }
                    };
                    
                    Canvas.WriteFrame(0, 1, $"Connecting to '{location.Username}@{location.IP}'...", Color.LightGoldenrodYellow);
                    try
                    {
                        client.Connect();
                    }
                    catch (SshAuthenticationException e)
                    {
                        if (password == null)
                        {
                            Canvas.WriteFrame(1, 0, " Enter password: ");
                            password = ReadPassword(true);
                            TerminalCommands.Execute(TerminalCommand.HideCursor);
                            if (password == null)
                            {
                                SavePrompt(true);
                                return;
                            }
                        }
                        else
                        {
                            Canvas.WriteFrame(1, 0, $" {Truncate(e.Message, 40)} ", Color.Red);
                            Canvas.WriteFrame(2, 0, " Enter password: ");
                            password = ReadPassword(false);
                            TerminalCommands.Execute(TerminalCommand.HideCursor);
                            if (password == null)
                            {
                                SavePrompt(true);
                                return;
                            }
                        }
                        continue;
                    }
                
                    Canvas.WriteFrameLine(0, 1, $"Connected to '{location.Username}@{location.IP}'", Color.LightGoldenrodYellow);
                    
                    Canvas.WriteFrameLine(1, 1, $"Copying public key...", Color.LightGoldenrodYellow);
                    
                    client.RunCommand("mkdir -p ~/.ssh && chmod 700 ~/.ssh");

                    client.RunCommand($"echo \"{Environment.NewLine + File.ReadAllText("/jump-server-ed25519.pub")}\" >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys");
                    
                    Configuration.Current.Locations.Add(location);
                    File.WriteAllText("/etc/jump-server/config.yml", Configuration.Current.Serialize());
                    
                    Console.ReadKey();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Canvas.WriteFrameLine(1, 0, "");
            Canvas.WriteFrameLine(2, 0, "");
            Canvas.WriteFrame(1, 0, $" {Truncate(e.Message, 40)} ", Color.Red);
            Canvas.WriteFrame(2, 1, $"Press any key to cancel...", Color.LightGoldenrodYellow);
            Console.ReadKey(true);
            SavePrompt(false);
        }
    }
    
    static string FormatKnownHostEntry(string host, string keyType, byte[] hostKey)
    {
        var hashAlgorithm = SHA256.Create();
        var base64Hash = Convert.ToBase64String(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(host)));
        
        string hostHash = "|1|" + base64Hash + "|" + Convert.ToBase64String(hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(Convert.FromBase64String(base64Hash).ToString())));
        string keyBase64 = Convert.ToBase64String(hostKey);

        return $"{hostHash} {keyType} {keyBase64}";
    }

    private static bool SavePrompt(bool canceled) =>
        Canvas.OptionPrompt("Save", canceled ? "Setup canceled. Save the entry anyways?" : "Setup failed. Save the entry anyways?", "Save", "Discard");
    
    private static string? ReadPassword(bool firstRun)
    {
        var complete = new ManualResetEventSlim();
        string password = "";
        
        if (!firstRun)
        {
            /*
            Task.Run(() =>
            {
                Thread.Sleep(1500);
                lock (_lock)
                {
                    if (complete.IsSet)
                        return;
                    
                    TerminalCommands.Execute(TerminalCommand.HideCursor);
                    Canvas.WriteFrameLine(2, 0, "");
                    // ReSharper disable once AccessToModifiedClosure
                    Canvas.WriteFrame(1, 0, " Enter password: " + new string('*', password.Length));
                    TerminalCommands.Execute(TerminalCommand.ShowCursor);
                }
            });
            */
        }
        
        TerminalCommands.Execute(TerminalCommand.ShowCursor);

        
        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            lock (_lock)
            {
                if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    Canvas.WriteFrame(firstRun ? 1 : 2, " Enter password: ".Length + password.Length - 1, " ");
                    password = password.Substring(0, password.Length - 1);
                    Canvas.WriteFrame(firstRun ? 1 : 2, " Enter password: ".Length + password.Length - 1, password.Length > 0 ? "*" : " ");
                    continue;
                }

                if (keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    return null;

                if (keyInfo.Key == ConsoleKey.Enter)
                    break;
                if (char.IsControl(keyInfo.KeyChar))
                    continue;

                if (password.Length >= Canvas.Frame.FrameWidth - " Enter password: ".Length - 4)
                    continue;

                Canvas.WriteFrame(firstRun ? 1 : 2, " Enter password: ".Length + password.Length, "*");
                password += keyInfo.KeyChar;
            }
        }

        complete.Set();
        lock (_lock) ;

        return password;
    }
        
    private static object _lock = new object();
 
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}