using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CommandLine;
using CommandLine.Text;
using JumpServer.Commands;
using JumpServer.Menus;
using Konscious.Security.Cryptography;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JumpServer;

public class Program
{
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.Major + "." + Assembly.GetExecutingAssembly().GetName().Version!.Minor + "." + Assembly.GetExecutingAssembly().GetName().Version!.Build;
    public static bool Authenticated = false;

    [Verb("run", HelpText = "Run jumper normally.")]
    public class RunOptions
    {
        [Option("--restrict-admin", Required = false, Default = false, HelpText = "Prevents access to admin menu even with password.")]
        public bool RestrictAdminAccess { get; set; } = false;
    }
    
    [Verb("passwd", HelpText = "Change jumper admin password for an already setup jumper chroot user.")]
    public class ChangePasswordOptions
    {
        [Value(0, MetaName = "username", HelpText = "Username of jumper chroot user. (Note: the default jumper username is 'jump')", Required = true)]
        public string Username { get; set; } = string.Empty;
    }
    
    [Verb("deluser", HelpText = "Delete a jumper chroot user along with the users configuration and jumper SSH key.")]
    public class DeleteUserOptions
    {
        [Value(0, MetaName = "username", HelpText = "Username of jumper chroot user. (Note: the default jumper username is 'jump')", Required = true)]
        public string Username { get; set; } = string.Empty;
        
        [Option('y', "--confirm", Required = false, Default = false, HelpText = "Confirms deletion of jumper chroot user.")]
        public bool Confirm { get; set; } = false;
    }
    
    [Verb("push", HelpText = "Push jumper executable to any and all jumper chroot users.")]
    public class PushOptions { }

    public static RunOptions CommandLineOptions;

    [DllImport("libc")]
    private static extern uint geteuid();

    public static bool IsRunningWithSudo()
    {
        return geteuid() == 0;
    }
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RunOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ChangePasswordOptions))]
    private static int HandleArguments(string[] args)
    {
        if (args.Length == 0)
            return Start(new RunOptions());
        
        var parser = new Parser(with => with.HelpWriter = null);
        var parserResult = parser.ParseArguments<RunOptions, ChangePasswordOptions, DeleteUserOptions, PushOptions>(args);
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
                Console.WriteLine(@$"
jumper v{Version}
MIT License

Usage:
  jumper [command] [options]

Commands:

  run                          Run jumper normally.
    --restrict-admin           Prevents access to admin menu even with password. (Default: false)

  passwd <username>   Change jumper admin password for an already setup jumper chroot user.

  deluser <username>   Delete a jumper chroot user along with the users configuration and jumper SSH key.
    --confirm, -y           Confirms deletion of jumper chroot user. (Default: false)

Options:

  --help                       Display this help screen.
  --version                    Display version information.
");
            
            return 1;
        }

        switch (parserResult.Value)
        {
            case RunOptions runOptions:
                return Start(runOptions);
            case ChangePasswordOptions changePasswordOptions:
                return ChangePassword.Execute(changePasswordOptions);
            case DeleteUserOptions deleteUserOptions:
                return DeleteUser.Execute(deleteUserOptions);
            case PushOptions pushOptions:
                return Push.Execute(pushOptions);
            default:
                return 1;
        }
    }
    
    static int Start(RunOptions options)
    {
        CommandLineOptions = options;
        
        if (!File.Exists("/etc/jumper/config.yml") && !IsRunningWithSudo())
        {
            Console.WriteLine("sudo privileges are required for first time setup.");
            return 1;
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

                    if (usernames.Count == 1 && !Canvas.OptionPrompt("Setup", $"A jump user already exists. Add another?", "Yes", "No", false))
                    {
                        Exit(null, null);
                        Console.WriteLine("Run " + $"ssh {usernames.First()}@localhost".ToColored(AnsiColor.Green3) + " to use existing configuration.");
                        return 0;
                    }
                    else if (usernames.Count > 1 && !Canvas.OptionPrompt("Setup", $"Multiple jump users already exists. Add another?", "Yes", "No", false))
                    {
                        Exit(null, null);
                        Console.WriteLine("Existing jump users:");
                        foreach (string username in usernames)
                        {
                            Console.WriteLine(username);
                        }
                        Console.WriteLine("Run " + $"ssh [username]@localhost".ToColored(AnsiColor.Green3) + " to use existing configuration.");
                        return 0 ;
                    }
                }

                Menus.Setup.SetupMain.Start();
                Exit(null, null);
                return 0;
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
                return 1;
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
            return 1;
        }

        Exit(null, null);
    }

    static void Main(string[] args)
    {
        Environment.Exit(HandleArguments(args));
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
}