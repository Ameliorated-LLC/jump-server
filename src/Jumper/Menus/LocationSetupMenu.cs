using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace JumpServer.Menus;

public class LocationSetupMenu
{
    public static void Show(Location location, bool importKey, bool disablePasswordAuth, bool randomizeSSHPort, bool requireTOTP)
    {
        var height = 8;


        //importKey = Canvas.OptionPrompt("Entry Setup", "Authorize jump server key on remote server?", "Yes", "No");
        //if (importKey)
        //    disablePasswordAuth = Canvas.OptionPrompt("Entry Setup", "Disable password authentication on server?", "Yes", "No");

        if (importKey) height++;
        if (disablePasswordAuth) height++;
        if (randomizeSSHPort) height++;
        if (requireTOTP) height++;

        Canvas.Set(new Frame("Entry Setup", height, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Press Ctrl + X to cancel setup", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() }
        ));

        string? password = null;

        int y = 1;
        Canvas.WriteFrame(0, 1, $"Connecting to '{location.Username}@{location.IP}'...", AnsiColor.Cornsilk1);

        bool sudoFailed = false;
        
        try
        {
            while (true)
            {
                Canvas.WriteFrameLine(1, 0, "");
                Canvas.WriteFrameLine(2, 0, "");
                var connectionInfo = new ConnectionInfo(location.IP, location.Port, location.Username, [
                    new PasswordAuthenticationMethod(location.Username, password ?? ""),
                    new PrivateKeyAuthenticationMethod(location.Username, [new PrivateKeyFile("/jumper-ed25519.key")]),
                ]);
                using (var client = new SshClient(connectionInfo))
                {
                    var keyReceived = new ManualResetEventSlim();
                    client.HostKeyReceived += (sender, e) =>
                    {
                        try
                        {
                            if (e.FingerPrint != null && e.HostKey != null)
                            {
                                keyReceived.Set();
                                var info = FormatKnownHostEntry(location.IP, e.HostKey);

                                using var writer = new StringWriter();
                                if (File.Exists("/etc/jumper/known_hosts"))
                                {
                                    foreach (var line in File.ReadAllLines("/etc/jumper/known_hosts"))
                                    {
                                        try
                                        {
                                            var elements = line.Split('|');
                                            if (line.StartsWith("|1|") && elements.Length == 4)
                                            {
                                                var saltBase64 = elements[2];
                                                var hostHashBase64 = elements[3].Split(' ').First();
                                                var keyBase64 = elements[3].Split(' ').Last();

                                                byte[] saltBytes = Convert.FromBase64String(saltBase64);
                                                byte[] hostBytes = Encoding.UTF8.GetBytes(location.IP);

                                                byte[] hostHashBytes;
                                                using (HMACSHA1 sha1 = new HMACSHA1(saltBytes))
                                                {
                                                    hostHashBytes = sha1.ComputeHash(hostBytes);
                                                }

                                                var result = Convert.ToBase64String(hostHashBytes);
                                                if (result != hostHashBase64)
                                                    writer.WriteLine(line);
                                            }
                                        }
                                        catch
                                        {
                                            writer.WriteLine(line);
                                        }
                                    }
                                }

                                writer.WriteLine($"|1|{info.Salt}|{info.HostHash} {e.HostKeyName} {info.Key}");

                                File.WriteAllText("/etc/jumper/known_hosts", writer.ToString());
                            }
                        }
                        catch
                        {
                            return;
                        }
                    };

                    Canvas.WriteFrame(0, 1, $"Connecting to '{location.Username}@{location.IP}'...", AnsiColor.Cornsilk1);
                    try
                    {
                        client.Connect();

                        Canvas.WriteFrameLine(0, 1, $"Connected to '{location.Username}@{location.IP}'", AnsiColor.Cornsilk1);

                        if (!disablePasswordAuth && !requireTOTP && !randomizeSSHPort)
                            break;
                        
                        var sudoResult = client.RunCommand(@$"echo ""{password}"" | sudo -S bash -c ""echo jumper-sudo-auth-test""");
                        
                        if (password == null || !sudoResult.Result.Contains("jumper-sudo-auth-test"))
                        {
                            var c = 0;
                            string errorMessage = "";
                            while (true)
                            {
                                Canvas.WriteFrameLine(1, 0, "");
                                Canvas.WriteFrameLine(2, 0, "");
                                
                                if (c == 0)
                                {
                                    Canvas.WriteFrame(1, 0, " Sudo password: ");
                                }
                                else
                                {
                                    Canvas.WriteFrameLine(1, 0, $" {Truncate(errorMessage, 40)} ", AnsiColor.Red);
                                    Canvas.WriteFrame(2, 0, " Sudo password: ");
                                }

                                if (c >= 3)
                                    Thread.Sleep(5000);
                                
                                c++;

                                password = ReadPassword(" Sudo password: ", c == 0);
                                TerminalCommands.Execute(TerminalCommand.HideCursor);
                                if (password == null)
                                {
                                    if (SavePrompt(true))
                                    {
                                        try
                                        {
                                            Configuration.Current.Locations.Add(location);
                                            File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                                        }
                                        catch
                                        {
                                            return;
                                        }
                                    }

                                    return;
                                }
                                
                                var result = client.RunCommand(@$"echo ""{password}"" | sudo -S bash -c ""echo jumper-sudo-auth-test""");
                                if (result.Result.Contains("jumper-sudo-auth-test"))
                                    break;
                                errorMessage = string.IsNullOrWhiteSpace(result.Error)
                                    ? string.IsNullOrWhiteSpace(result.Result)
                                        ? $"Invalid password ({(result.ExitStatus?.ToString() ?? result.ExitSignal)})"
                                        : result.Result.Split('\n').LastOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Invalid password"
                                    : result.Error.Split('\n').LastOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Invalid password";
                            }
                        }
                    }
                    catch (SshAuthenticationException e)
                    {
                        if (password == null)
                        {
                            Canvas.WriteFrame(1, 0, " Enter password: ");
                            password = ReadPassword(" Enter password: ", true);
                            TerminalCommands.Execute(TerminalCommand.HideCursor);
                            if (password == null)
                            {
                                if (SavePrompt(true))
                                {
                                    try
                                    {
                                        Configuration.Current.Locations.Add(location);
                                        File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                }

                                return;
                            }
                        }
                        else
                        {
                            Canvas.WriteFrame(1, 0, $" {Truncate(e.Message, 40)} ", AnsiColor.Red);
                            Canvas.WriteFrame(2, 0, " Enter password: ");
                            password = ReadPassword(" Enter password: ", false);
                            TerminalCommands.Execute(TerminalCommand.HideCursor);
                            if (password == null)
                            {
                                if (SavePrompt(true))
                                {
                                    try
                                    {
                                        Configuration.Current.Locations.Add(location);
                                        File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                }

                                return;
                            }
                        }

                        continue;
                    }

                    Canvas.WriteFrameLine(1, 0, "");
                    Canvas.WriteFrameLine(2, 0, "");
                    
                    var random = new Random();

                    var hostnameResult = client.RunCommand("hostname");
                    if (hostnameResult.ExitStatus == 0 && Regex.IsMatch(hostnameResult.Result, @"^(?!.*\.\.)[A-Za-z0-9.-]+$") &&
                        !(hostnameResult.Result.Trim().Contains('\n') || hostnameResult.Result.Length > 22))
                        location.Name = hostnameResult.Result.Trim();
                    Thread.Sleep(random.Next(250, 500));



                    if (importKey)
                    {
                        y++;
                        Canvas.WriteFrameLine(1, 1, $"Copying public key...", AnsiColor.Cornsilk1);
                        Thread.Sleep(random.Next(750, 1500));

                        client.RunCommand("mkdir -p ~/.ssh && chmod 700 ~/.ssh");

                        var keyText = File.ReadAllText("/jumper-ed25519.pub");
                        var result = client.RunCommand(
                            $"grep -qxF \"{keyText.Trim()}\" ~/.ssh/authorized_keys || echo \"{Environment.NewLine + keyText}\" >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys");
                        if (result.ExitStatus != 0)
                            Canvas.WriteFrameLine(1, 1, $"Failed to copy public key", AnsiColor.Yellow);
                        else
                            Canvas.WriteFrameLine(1, 1, $"Copied public key", AnsiColor.Cornsilk1);


                        Thread.Sleep(random.Next(250, 500));
                    }

                    bool restartSsh = false;

                    if (disablePasswordAuth)
                    {
                        y++;
                        Canvas.WriteFrameLine(y - 1, 1, $"Disabling password auth...", AnsiColor.Cornsilk1);
                        Thread.Sleep(random.Next(750, 1500));

                        if (!sudoFailed)
                        {
                            var result = client.RunCommand(
                                @$"grep -q '^PasswordAuthentication no' /etc/ssh/sshd_config || echo ""{password}"" | sudo -S bash -c ""((grep -q '^PasswordAuthentication .*' /etc/ssh/sshd_config && sed -i 's/^PasswordAuthentication .*$/\# PasswordAuthentication no/' /etc/ssh/sshd_config) & sed -i '1i PasswordAuthentication no\n' /etc/ssh/sshd_config) && exit 117""");
                            if (result.ExitStatus != 0 && result.ExitStatus != 117)
                            {
                                Canvas.WriteFrameLine(y - 1, 1, $"Failed to disable password auth", AnsiColor.Yellow);
                                if (result.Error.Contains("\nsudo:"))
                                    sudoFailed = true;
                            }
                            else
                                Canvas.WriteFrameLine(y - 1, 1, $"Disabled password auth", AnsiColor.Cornsilk1);

                            if (result.ExitStatus == 117)
                                restartSsh = true;
                        }
                        else
                            Canvas.WriteFrameLine(y - 1, 1, $"Failed to disable password auth", AnsiColor.Yellow);

                        Thread.Sleep(random.Next(250, 500));
                    }

                    if (randomizeSSHPort)
                    {
                        y++;
                        Canvas.WriteFrameLine(y - 1, 1, $"Randomizing SSH port...", AnsiColor.Cornsilk1);
                        Thread.Sleep(random.Next(250, 750));
                        int port = RandomNumberGenerator.GetInt32(11001, 49151);

                        for (int i = 0; i < 50; i++)
                        {
                            port = RandomNumberGenerator.GetInt32(11001, 49151);
                            var result = client.RunCommand(@$"echo ""{password}"" | sudo -S bash -c ""lsof -nP -i:{port}""");
                            if (result.ExitStatus != 0)
                            {
                                if (result.Error.Contains("\nsudo:"))
                                    sudoFailed = true;
                                break;
                            }
                        }

                        if (!sudoFailed)
                        {
                            var result = client.RunCommand(
                                @$"grep -q '^Port {port}' /etc/ssh/sshd_config || echo ""{password}"" | sudo -S bash -c ""((grep -q '^Port .*' /etc/ssh/sshd_config && sed -i 's/^Port .*$/\# Port 22/' /etc/ssh/sshd_config) & sed -i '1i Port {port}\n' /etc/ssh/sshd_config) && exit 117""");
                            if (result.ExitStatus != 0 && result.ExitStatus != 117)
                                Canvas.WriteFrameLine(y - 1, 1, $"Failed randomize SSH port", AnsiColor.Yellow);
                            else
                            {
                                location.Port = port;
                                Canvas.WriteFrameLine(y - 1, 1, $"Randomized SSH port", AnsiColor.Cornsilk1);
                            }

                            if (result.ExitStatus == 117)
                                restartSsh = true;
                        }
                        else
                            Canvas.WriteFrameLine(y - 1, 1, $"Failed randomize SSH port", AnsiColor.Yellow);

                        Thread.Sleep(random.Next(250, 500));
                    }

                    if (restartSsh)
                    {
                        client.RunCommand($@"echo ""{password}"" | sudo -S bash -c ""sudo systemctl restart ssh || sudo systemctl restart sshd""");
                    }

                    y++;
                    Canvas.WriteFrameLine(y - 1, 1, $"Saving fingerprint...", AnsiColor.Cornsilk1);
                    Thread.Sleep(random.Next(750, 1500));

                    if (keyReceived.IsSet)
                        Canvas.WriteFrameLine(y - 1, 1, $"Saved fingerprint", AnsiColor.Cornsilk1);
                    else
                        Canvas.WriteFrameLine(y - 1, 1, $"Failed to save fingerprint", AnsiColor.Yellow);

                    Thread.Sleep(random.Next(250, 500));

                    try
                    {
                        location.StartPinging();
                        Configuration.Current.Locations.Add(location);
                        File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                    }
                    catch (Exception e)
                    {
                        Canvas.WriteFrame(y, 0, $" {Truncate(e.Message, 40)} ", AnsiColor.Red);
                        Canvas.WriteFrame(height - 4, 1, $"Press any key to cancel setup...", AnsiColor.Grey93);
                        Console.ReadKey(true);
                    }

                    Canvas.WriteFrameLine(y, 1, $"Entry setup complete", AnsiColor.Green3);

                    Canvas.WriteFrameLine(y + 2, 1, $"Press any key to return to menu...", AnsiColor.Grey93);
                    Console.ReadKey(true);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Canvas.WriteFrame(y, 0, $" {Truncate(e.Message, 40)} ", AnsiColor.Red);
            Canvas.WriteFrame(height - 4, 1, $"Press any key to cancel setup...", AnsiColor.Grey93);
            Console.ReadKey(true);
            if (SavePrompt(false))
            {
                try
                {
                    location.StartPinging();
                    Configuration.Current.Locations.Add(location);
                    File.WriteAllText("/etc/jumper/config.yml", Configuration.Current.Serialize());
                }
                catch
                {
                    return;
                }
            }
        }
    }

    static (string Salt, string HostHash, string Key) FormatKnownHostEntry(string host, byte[] hostKey)
    {
        byte[] saltBytes = new byte[20];
        RandomNumberGenerator.Fill(saltBytes);

        string saltBase64 = Convert.ToBase64String(saltBytes);

        byte[] hostBytes = Encoding.UTF8.GetBytes(host);
        byte[] hostHashBytes;
        using (HMACSHA1 sha1 = new HMACSHA1(saltBytes))
        {
            hostHashBytes = sha1.ComputeHash(hostBytes);
        }

        string hostHashBase64 = Convert.ToBase64String(hostHashBytes);


        //string hostHash = "|1|" + saltBase64 + "|" + hashBase64;
        string keyBase64 = Convert.ToBase64String(hostKey);

        return (saltBase64, hostHashBase64, keyBase64);
//        return $"{hostHash} {keyType} {keyBase64}";
    }

    private static bool SavePrompt(bool canceled) =>
        Canvas.OptionPrompt("Save", canceled ? "Setup canceled. Save the entry anyways?" : "Setup failed. Save the entry anyways?", "Save", "Discard");

    private static string? ReadPassword(string passwordString, bool firstRun)
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
                    Canvas.WriteFrame(firstRun ? 1 : 2, passwordString.Length + password.Length - 1, " ");
                    password = password.Substring(0, password.Length - 1);
                    Canvas.WriteFrame(firstRun ? 1 : 2, passwordString.Length + password.Length - 1, password.Length > 0 ? "*" : " ");
                    continue;
                }

                if (keyInfo.Key == ConsoleKey.X && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    return null;

                if (keyInfo.Key == ConsoleKey.Enter)
                    break;
                if (char.IsControl(keyInfo.KeyChar))
                    continue;

                if (password.Length >= Canvas.Frame.FrameWidth - passwordString.Length - 4)
                    continue;

                Canvas.WriteFrame(firstRun ? 1 : 2, passwordString.Length + password.Length, "*");
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