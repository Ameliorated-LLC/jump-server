using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CommandLine;
using CommandLine.Text;
using JumpServer.Menus;
using Konscious.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JumpServer;

public class Program
{
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.Major + "." + Assembly.GetExecutingAssembly().GetName().Version!.Minor + "." + Assembly.GetExecutingAssembly().GetName().Version!.Build;
    public static bool Authenticated = false;

    [Verb("change-password", HelpText = "Change jumper admin password for an already setup jumper chroot user.")]
    public class ChangePasswordOptions
    {
        [Value(0, MetaName = "username", HelpText = "Username of jumper chroot user. (Note: the default jumper username is 'jump')", Required = true)]
        public string Username { get; set; } = string.Empty;
    }
    
    [Verb("run", HelpText = "Run jumper normally.")]
    public class RunOptions
    {
        [Option("--restrict-admin", Required = false, Default = false, HelpText = "Prevents access to admin menu even with password.")]
        public bool RestrictAdminAccess { get; set; } = false;
    }

    public static RunOptions CommandLineOptions;

    [DllImport("libc")]
    private static extern uint geteuid();

    public static bool IsRunningWithSudo()
    {
        return geteuid() == 0;
    }
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RunOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ChangePasswordOptions))]
    private static void HandleArguments(string[] args)
    {
        if (args.Length == 0)
        {
            Start(new RunOptions());
            return;
        }
        
        var parser = new Parser();
        var parserResult = parser.ParseArguments<RunOptions, ChangePasswordOptions>(args);
        var helpText = HelpText.AutoBuild(parserResult, h =>
        {
            h.Heading = "jumper v" + Version;
            return h;
        }, e => e);
        if (parserResult.Tag == ParserResultType.NotParsed)
        {
            if (parserResult.Errors.Any() && !parserResult.Errors.Any(x =>
                    x.Tag == ErrorType.HelpRequestedError || x.Tag == ErrorType.HelpVerbRequestedError || x.Tag == ErrorType.VersionRequestedError))
            {
                foreach (var parserResultError in parserResult.Errors)
                {
                    Console.WriteLine(parserResultError.ToString());
                }
                Console.WriteLine();
            }
            if (parserResult.Errors.Any(x => x.Tag == ErrorType.VersionRequestedError))
                Console.WriteLine("jumper v" + Version + Environment.NewLine + "MIT License");
            else
                Console.WriteLine(helpText.ToString());
            
            return;
        }

        if (parserResult.Value is ChangePasswordOptions changePasswordOptions)
        {
            try
            {
                if (!Directory.Exists("/etc/jumper") || !Directory.GetFiles("/etc/jumper").Any())
                {
                    Console.WriteLine("Jumper has not been configured with a user yet.");
                    Environment.Exit(1);
                    return;
                }

                var file = Directory.GetFiles("/etc/jumper").FirstOrDefault(file => Path.GetFileName(file).Split('.').FirstOrDefault()?.StartsWith(changePasswordOptions.Username) ?? false);
                if (file == null) {
                    Console.WriteLine($"No jumper configuration found for {changePasswordOptions.Username}.");
                    Environment.Exit(1);
                    return;
                }
                
                var yaml = File.ReadAllText(file);
                var configuration = Configuration.Deserialize(yaml);

                string? base64;
                
                Console.Write("Enter new password: ");
                var password = ReadPassword();
                Console.WriteLine();
                if (string.IsNullOrWhiteSpace(password))
                    base64 = null;
                else
                {
                    using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
                    {
                        argon2.Salt = Encoding.UTF8.GetBytes("jumper-salt");
                        argon2.DegreeOfParallelism = 8;
                        argon2.MemorySize = 65536;
                        argon2.Iterations = 20;

                        byte[] hashBytes = argon2.GetBytes(32);
                        base64 = Convert.ToBase64String(hashBytes);
                    }
                }
                
                configuration.AdminPassword = base64;
                
                File.WriteAllText(file, Configuration.Current.Serialize());
                Console.WriteLine(Environment.NewLine + "Password changed successfully.".ToColored(AnsiColor.Green3));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        } else if (parserResult.Value is RunOptions options)
        {
            Start(options);
        }
    }
    
    static void Start(RunOptions options)
    {
        CommandLineOptions = options;
        
        if (!File.Exists("/etc/jumper/config.yml") && !IsRunningWithSudo())
        {
            Console.WriteLine("sudo privileges are required for first time setup.");
            Environment.Exit(1);
            return;
        }

        PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => { Exit(null, null); });

        TerminalCommands.Execute(TerminalCommand.SaveCursorPos);
        TerminalCommands.Execute(TerminalCommand.SaveScreen);
        TerminalCommands.Execute(TerminalCommand.HideCursor);
        Console.CancelKeyPress += Exit;

        try
        {

            if (!File.Exists("/etc/jumper/config.yml"))
            {
                if (Directory.Exists("/etc/jumper"))
                {
                    List<string> usernames = new List<string>();
                    foreach (var file in Directory.GetFiles("/etc/jumper"))
                    {
                        var username = Path.GetFileName(file).Split('.').FirstOrDefault();
                        if (username == null)
                            continue;
                        usernames.Add(username);
                    }

                    if (usernames.Count == 1 && !Canvas.OptionPrompt("Setup", $"A jump user already exists. Add another?", "Yes", "No", true))
                    {
                        Exit(null, null);
                        Console.WriteLine("Run " + $"ssh {usernames.First()}@localhost".ToColored(AnsiColor.Green3) + " to use existing configuration.");
                        return;
                    }
                    else if (usernames.Count > 1 && !Canvas.OptionPrompt("Setup", $"Multiple jump users already exists. Add another?", "Yes", "No", true))
                    {
                        Exit(null, null);
                        Console.WriteLine("Existing jump users:");
                        foreach (string username in usernames)
                        {
                            Console.WriteLine(username);
                        }
                        Console.WriteLine("Run " + $"ssh [username]@localhost".ToColored(AnsiColor.Green3) + " to use existing configuration.");
                        return;
                    }
                }

                Menus.Setup.SetupMain.Start();
                Exit(null, null);
                return;
            }

            try
            {
                var yaml = File.ReadAllText("/etc/jumper/config.yml");
                Configuration.Current = Configuration.Deserialize(yaml);
                Configuration.Current.Locations ??= [];
            }
            catch (Exception e)
            {
                Exit(null, null);
                Console.WriteLine("Error reading config.yml: " + e.Message);
                Environment.Exit(1);
                return;
            }
            
            Configuration.Current.Locations.ForEach(x => x.StartPinging());

            while (true)
            {
                if (Configuration.Current.Locations.Count == 0)
                {
                    NoLocationsMenu.Show();
                    continue;
                }
                
                var menu = new JumpMenu();
                menu.Show(Program.Authenticated);
            }
        }
        catch (Exception e)
        {
            Exit(null, null);
            Console.WriteLine(e);
            Environment.Exit(1);
            return;
        }

        Exit(null, null);
    }

    static void Main(string[] args)
    {
        HandleArguments(args);
    }

    public static void Exit(object? sender, EventArgs? e)
    {
        lock (_exitLock)
        {
            if (_exitTriggered)
                return;
            _exitTriggered = true;
            Canvas.SizeCheckTimer?.Dispose();
            TerminalCommands.Execute(TerminalCommand.RestoreScreen);
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
            TerminalCommands.Execute(TerminalCommand.ResetCursorShapeAndBlinking);
            TerminalCommands.Execute(TerminalCommand.RestoreCursorPos);
        }
    }

    private static object _exitLock = new object();
    private static volatile bool _exitTriggered = false;
    
    private static string ReadPassword()
    {
        string password = "";

        ConsoleKeyInfo keyInfo;
        while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            if (keyInfo.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                Console.Write("\b \b");
                password = password.Substring(0, password.Length - 1);
                continue;
            }

            if (keyInfo.Key == ConsoleKey.Enter)
                break;
            if (char.IsControl(keyInfo.KeyChar))
                continue;

            Console.Write("*");
            password += keyInfo.KeyChar;
        }

        return password;
    }
}