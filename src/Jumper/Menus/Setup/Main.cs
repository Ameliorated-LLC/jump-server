using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Konscious.Security.Cryptography;
using Renci.SshNet.Security;
using SshNet.Keygen;
using SshNet.Keygen.Extensions;

namespace JumpServer.Menus.Setup;

public static class SetupMain
{
    public static void Start()
    {
        while (true)
        {
            bool express = Canvas.OptionPrompt("Setup", "Select installation type", "Express", "Custom", true);
            bool result;
            if (express)
                result = SetupExpress();
            else
                result = SetupCustom();
            
            if (result)
                break;
        }
    }

    private static bool SetupExpress()
    {
        string? name = null;
        string? adminPassword = null;
        string? password = null;
        string? username = "jump";
        while (true)
        {
            name = ServerNameMenu.Show(name);
            if (name == null)
                return false;
            
            while (true)
            {
                adminPassword = AdminPasswordMenu.Show(null);
                if (adminPassword == null)
                    break;

                bool userExists = ExecuteCommand("id", "jump").ExitCode == 0;

                if (!userExists)
                {
                    password = UserPasswordMenu.Show(null);
                    if (password == null)
                        continue;
                }
                else
                {
                    while (true)
                    {
                        username = UserUsernameMenu.Show(null);
                        if (username == null)
                            break;

                        if (ExecuteCommand("id", [username]).ExitCode == 0)
                            continue;

                        password = UserPasswordMenu.Show(null);
                        if (password != null)
                            break;
                    }

                    if (username == null)
                        continue;
                }

                break;
            }
            if (adminPassword != null)
                break;
        }
        
        Canvas.Set(new Frame("Setup", 10, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() }
            //new DynamicBar() { Center = new Text("Press Ctrl + X to cancel setup", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile() }
        ));
        
        try
        {
            var random = new Random();
            
            Canvas.WriteFrameLine(0, 1, "Creating jump user...", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(750, 1500));
            
            bool userCreated = ExecuteCommand("useradd", [ username!, "-m", "-s", "/bin/sh" ]).ExitCode == 0;
            if (!userCreated)
                throw new Exception("Failed to create user.");
            bool userPassword = ExecuteCommand("chpasswd", Array.Empty<string>(), $"{username!}:{password}").ExitCode == 0;
            if (!userPassword)
                throw new Exception("Failed to set password.");
            
            Canvas.WriteFrameLine(0, 1, "Created jump user", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(250, 500));
            
            Canvas.WriteFrameLine(1, 1, "Creating chroot environment...", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(750, 1500));

            Directory.CreateDirectory($"/home/{username}/chroot");
            
            // Permissions required for SSH chroot to work
            
            if (ExecuteCommand("chown", [ "root:root", "/home/" + username! ]).ExitCode != 0)
                throw new Exception("Failed to set ownership.");
            if (ExecuteCommand("chmod", [ "0755", "/home/" + username! ]).ExitCode != 0)
                throw new Exception("Failed to set ownership.");
            if (ExecuteCommand("chown", [ "root:root", "/home/" + username! + "/chroot" ]).ExitCode != 0)
                throw new Exception("Failed to set ownership.");
            if (ExecuteCommand("chmod", [ "0755", "/home/" + username! + "/chroot" ]).ExitCode != 0)
                throw new Exception("Failed to set ownership.");
            
            // Config file
            
            Directory.CreateDirectory($"/home/{username}/chroot/etc/jumper");

            if (adminPassword != "")
            {
                using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(adminPassword)))
                {
                    argon2.Salt = Encoding.UTF8.GetBytes("jumper-salt");
                    argon2.DegreeOfParallelism = 8;
                    argon2.MemorySize = 65536;
                    argon2.Iterations = 16;

                    byte[] hashBytes = argon2.GetBytes(32);
                    adminPassword = Convert.ToBase64String(hashBytes);
                }
            }
            
            File.WriteAllText($"/home/{username}/chroot/etc/jumper/config.yml", new Configuration()
            {
                ServerName = name,
                AdminPassword = string.IsNullOrWhiteSpace(adminPassword) ? null : adminPassword,
                RestrictAdminAccess = Program.CommandLineOptions.RestrictAdminAccess,
            }.Serialize());
            if (ExecuteCommand("chmod", [ "-R", "0777", $"/home/{username}/chroot/etc/jumper" ]).ExitCode != 0)
                throw new Exception("Failed to set ownership.");
            
            if (!Directory.Exists($"/etc/jumper"))
                Directory.CreateDirectory($"/etc/jumper");
            if (File.Exists($"/etc/jumper/{username}.config.yml"))
                File.Delete($"/etc/jumper/{username}.config.yml");
            File.CreateSymbolicLink($"/etc/jumper/{username}.config.yml", $"/home/{username}/chroot/etc/jumper/config.yml");

            // Copy required chroot files
            
            foreach (var mount in new List<(string SourcePath, string DestinationPath, bool IsFile, string? ParentFolder, bool Required)> {
                         ("/usr/lib64", $"/home/{username}/chroot/lib64", false, null, true),
                         ("/usr/lib/x86_64-linux-gnu", $"/home/{username}/chroot/lib/x86_64-linux-gnu", false, null, true),
                         ("/usr/bin/ssh", $"/home/{username}/chroot/bin/ssh", true, $"/home/{username}/chroot/bin", true),
                         ("/usr/bin/jumper", $"/home/{username}/chroot/bin/jumper", true, $"/home/{username}/chroot/bin", true),
                         ("/usr/bin/sh", $"/home/{username}/chroot/bin/sh", true, $"/home/{username}/chroot/bin", true),
                         ("/usr/bin/ping", $"/home/{username}/chroot/bin/ping", true, $"/home/{username}/chroot/bin", false),
                         ("/usr/lib/terminfo", $"/home/{username}/chroot/lib/terminfo", false, null, false),
                         ("/usr/share/terminfo", $"/home/{username}/chroot/usr/share/terminfo", false, null, false),
                         ("/etc/terminfo", $"/home/{username}/chroot/etc/terminfo", false, null, false),
                     })
            {
                if ((mount.IsFile && !File.Exists(mount.SourcePath)) || (!mount.IsFile && !Directory.Exists(mount.SourcePath)))
                {
                    if (mount.Required)
                        throw new Exception($"Missing required {(mount.IsFile ? "file" : "directory")}: {mount.SourcePath}");
                    
                    continue;
                }
                if (!Directory.Exists(mount.IsFile ? mount.ParentFolder! : mount.DestinationPath))
                    Directory.CreateDirectory(mount.IsFile ? mount.ParentFolder! : mount.DestinationPath);


                if (mount.IsFile)
                    File.Copy(mount.SourcePath, mount.DestinationPath, true);
                else
                {
                    Directory.CreateDirectory(mount.DestinationPath);
                    Directory.GetFiles(mount.SourcePath, "*", SearchOption.AllDirectories).ToList().ForEach(x =>
                    {
                        if (Path.GetDirectoryName(x.Replace(mount.SourcePath, mount.DestinationPath)) != null && !Directory.Exists(Path.GetDirectoryName(x.Replace(mount.SourcePath, mount.DestinationPath))))
                            Directory.CreateDirectory(Path.GetDirectoryName(x.Replace(mount.SourcePath, mount.DestinationPath))!);
                        File.Copy(x, x.Replace(mount.SourcePath, mount.DestinationPath), true);
                    });
                }
            }
            
            File.WriteAllText($"/home/{username}/chroot/etc/resolv.conf", $"nameserver 4.2.2.2\nnameserver 8.8.8.8");
            
            Directory.CreateDirectory($"/home/{username}/chroot/etc");
            
            var passwd = File.ReadAllText("/etc/passwd").Split(Environment.NewLine).First(x => x.StartsWith($"{username}:"));
            var group = File.ReadAllText("/etc/passwd").Split(Environment.NewLine).First(x => x.StartsWith($"{username}:"));
            File.WriteAllText($"/home/{username}/chroot/etc/passwd", passwd);
            File.WriteAllText($"/home/{username}/chroot/etc/group", group);

            if (!Directory.Exists($"/home/{username}/chroot/dev"))
                Directory.CreateDirectory($"/home/{username}/chroot/dev");
            foreach (var dev in new List<(string Path, int Major, int Minor)>
                     {
                         ($"/home/{username}/chroot/dev/null", 1, 3),
                         ($"/home/{username}/chroot/dev/tty", 5, 0),
                         ($"/home/{username}/chroot/dev/zero", 1, 5),
                         ($"/home/{username}/chroot/dev/random", 1, 8),
                     })
            {
                if (File.Exists(dev.Path))
                    continue;

                if (ExecuteCommand("mknod", ["-m", "666", dev.Path, "c", dev.Major.ToString(), dev.Minor.ToString()]).ExitCode != 0)
                    throw new Exception("Failed to mknod: " + dev.Path);
            }
            
            Canvas.WriteFrameLine(1, 1, "Created chroot environment", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(250, 500));

            Canvas.WriteFrameLine(2, 1, "Adding SSH config...", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(750, 1500));
            
            var config = File.ReadAllText("/etc/ssh/sshd_config");
            using (var reader = new StringReader(config))
            {
                using var writer = new StringWriter();

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var savedLines = new List<string>();
                    if (Regex.IsMatch(line, $@"^Match User {Regex.Escape(username!)}\s*$"))
                    {
                        while ((line = reader.ReadLine()) != null && (Regex.IsMatch(line, @"^[\s#]+.*$") || Regex.IsMatch(line, @"^\s*$")))
                        {
                            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                                savedLines.Add(line);
                            else
                                savedLines.Clear();
                        }
                    }

                    if (line != null)
                        writer.WriteLine(line);
                    else
                        savedLines.ForEach(writer.WriteLine);
                }
                writer.WriteLine();
                writer.WriteLine($"Match User {username!}");
                writer.WriteLine($"    ForceCommand /bin/jumper");
                writer.WriteLine($"    AllowUsers {username!}");
                writer.WriteLine($"    PasswordAuthentication yes");
                writer.WriteLine($"    ChrootDirectory /home/{username!}/chroot");
                
                File.WriteAllText("/etc/ssh/sshd_config", writer.GetStringBuilder().ToString());
            }
            
            if (ExecuteCommand("systemctl", "restart ssh").ExitCode != 0)
                throw new Exception("Failed to restart ssh.");
            
            Canvas.WriteFrameLine(2, 1, "Added SSH config", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(250, 500));
            
            Canvas.WriteFrameLine(3, 1, "Generating SSH key...", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(750, 1500));

            var key = SshKey.Generate(new SshKeyGenerateInfo(SshKeyType.ED25519) {Comment = ""});
            File.WriteAllText($"/home/{username}/chroot/jumper-ed25519.key", key.ToOpenSshFormat());
            File.WriteAllText($"/home/{username}/chroot/jumper-ed25519.pub", key.ToOpenSshPublicFormat().Trim());
            
            if (!Directory.Exists($"/home/{username}/chroot/etc/ssh"))
                Directory.CreateDirectory($"/home/{username}/chroot/etc/ssh");
            File.WriteAllText($"/home/{username}/chroot/etc/ssh/ssh_config", $"    UserKnownHostsFile /etc/jumper/known_hosts\n    IdentityFile /jumper-ed25519.key\n    SendEnv LANG LC_*\n    HashKnownHosts yes\n    GSSAPIAuthentication yes");

            Canvas.WriteFrameLine(3, 1, "Generated SSH key", AnsiColor.Cornsilk1);
            Thread.Sleep(random.Next(250, 500));
            
            Canvas.WriteFrameLine(4, 1, "Setup complete", AnsiColor.Green3);
            Thread.Sleep(500);
            
            Canvas.WriteFrameLine(6, 1, "Press any key to complete setup...", AnsiColor.Grey93);

            while (Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadKey(true);
            
            Program.Exit(null, null);
            Console.WriteLine("Setup completed. Run " + $"ssh {username}@localhost".ToColored(AnsiColor.Green3) + " to use jumper.");
        }
        catch (Exception e)
        {
            Program.Exit(null, null);
            Console.WriteLine("Cleaning up...");

            try
            {
                var config = File.ReadAllText("/etc/ssh/sshd_config");
                using (var reader = new StringReader(config))
                {
                    using var writer = new StringWriter();

                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var savedLines = new List<string>();
                        if (Regex.IsMatch(line, $@"^Match User {Regex.Escape(username!)}\s*$"))
                        {
                            while ((line = reader.ReadLine()) != null && (Regex.IsMatch(line, @"^[\s#]+.*$") || Regex.IsMatch(line, @"^\s*$")))
                            {
                                if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                                    savedLines.Add(line);
                                else
                                    savedLines.Clear();
                            }
                        }

                        if (line != null)
                            writer.WriteLine(line);
                        else
                            savedLines.ForEach(writer.WriteLine);
                    }

                    File.WriteAllText("/etc/ssh/sshd_config", writer.GetStringBuilder().ToString());
                }
            }
            catch
            {
                // Ignore. Worst case is we have a unused Match User block.
            }

            try
            {
                Directory.Delete($"/home/{username}", true);
            }
            catch
            {
                Console.WriteLine("Failed to delete user directory: " + e.Message);
            }

            try
            {
                if (ExecuteCommand("userdel", ["-rf", username!]).ExitCode != 0)
                    throw new Exception("userdel failed.");
                
                if (File.Exists($"/etc/jumper/{username}.config.yml"))
                    File.Delete($"/etc/jumper/{username}.config.yml");
            }
            catch
            {
                Console.WriteLine("Failed to delete user: " + e.Message);
            }
            
            Console.WriteLine(Environment.NewLine + "Setup failed: ".ToColored(AnsiColor.Red) + Environment.NewLine + e);
            Environment.Exit(1);
            Thread.Sleep(Timeout.Infinite);
        }
        
        return true;
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
    
    private static bool SetupCustom()
    {
        bool userExists = ExecuteCommand("id", "jump").ExitCode == 0;
        bool setupUser = Canvas.OptionPrompt("Setup", "Create jailed jump ssh user?", "Yes", "No");
        return true;
    }

    private static string PromptPassword()
    {
        Canvas.Set(new Frame("Setup", 6, 52,
            new DynamicBar() { Center = new Text("jumper v" + Program.Version, AnsiColor.Grey93, (AnsiColor?)null).Compile() }));
        
        int tries = 0;
        while (true)
        {
            Canvas.WriteFrameLine(0, 0, "");
            Canvas.WriteFrame(0, 0, " Set jump user password: ");
            var password = ReadPassword(tries);
            TerminalCommands.Execute(TerminalCommand.HideCursor);

            tries++;
            
            Canvas.WriteFrame(1, 0, " Incorrect password. ", AnsiColor.Red);
        }
    }
    
    private static string? ReadPassword(int tries)
    {
        using var _cancel = new CancellationTokenSource();
        string password = "";

        try
        {
            if (tries > 0)
            {
                var token = _cancel.Token;
                Task.Run(() =>
                {
                    Thread.Sleep(1500);
                    lock (_lock)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        TerminalCommands.Execute(TerminalCommand.HideCursor);
                        Canvas.WriteFrameLine(1, 0, "");
                        // ReSharper disable once AccessToModifiedClosure
                        Canvas.WriteFrame(0, 0, "  Set jump user password: " + new string('*', password.Length));
                        TerminalCommands.Execute(TerminalCommand.ShowCursor);
                    }
                });
            }

            TerminalCommands.Execute(TerminalCommand.ShowCursor);

            ConsoleKeyInfo keyInfo;
            while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                lock (_lock)
                {
                    if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        Canvas.WriteFrame(0, " Set jump user password: ".Length + password.Length - 1, " ");
                        password = password.Substring(0, password.Length - 1);
                        Canvas.WriteFrame(0, " Set jump user password: ".Length + password.Length - 1, password.Length > 0 ? "*" : " ");
                        continue;
                    }

                    if (keyInfo.Key == ConsoleKey.Enter)
                        break;
                    if (char.IsControl(keyInfo.KeyChar))
                        continue;

                    if (password.Length >= Canvas.Frame.FrameWidth - " Set jump user password: ".Length - 4)
                        continue;

                    Canvas.WriteFrame(0, " Set jump user password: ".Length + password.Length, "*");
                    password += keyInfo.KeyChar;
                }
            }
            
            return password;
        }
        finally
        {
            _cancel.Cancel();
            lock (_lock) ;
        }
    }
    private static object _lock = new object();
    
    public static (string Output, string Error, int ExitCode) ExecuteCommand(string command, string arguments, string? input = null) => ExecuteCommand(command, arguments.Split(' '), input);
    public static (string Output, string Error, int ExitCode) ExecuteCommand(string command, string[] arguments, string? input = null)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using (Process process = new Process())
        {
            process.StartInfo.FileName = command;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            
            using var outputCompleted = new SemaphoreSlim(0, 1);
            using var errorCompleted = new SemaphoreSlim(0, 1);

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outputBuilder.AppendLine(args.Data);
                else
                    // ReSharper disable once AccessToDisposedClosure
                    outputCompleted.Release();
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)

                    errorBuilder.AppendLine(args.Data);
                else
                    // ReSharper disable once AccessToDisposedClosure
                    errorCompleted.Release();
            };

            try
            {
                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (input != null)
                {
                    using (var stdin = process.StandardInput)
                    {
                        stdin.WriteLine(input);
                        stdin.Flush();
                    }
                }
                
                process.WaitForExit();
                
                outputCompleted.Wait();
                errorCompleted.Wait();

                int exitCode = process.ExitCode;

                return (outputBuilder.ToString(), errorBuilder.ToString(), exitCode);
            }
            catch (Exception ex)
            {
                return ($"Exception: {ex.Message}", ex.Message, -1);
            }
        }
    }
}
