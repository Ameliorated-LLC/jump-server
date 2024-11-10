using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using CommandLine;
using CommandLine.Text;
using JumpServer.Menus;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JumpServer;

public class Program
{
    public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version!.Major + "." + Assembly.GetExecutingAssembly().GetName().Version!.Minor + "." + Assembly.GetExecutingAssembly().GetName().Version!.Build;
    public static bool Authenticated = false;

    public class Options
    {
        [Option("restrict-admin", Required = false, HelpText = "Prevents access to admin menu even with password.")]
        public bool RestrictAdminAccess { get; set; } = false;
    }

    public static Options CommandLineOptions;

    [DllImport("libc")]
    private static extern uint geteuid();

    public static bool IsRunningWithSudo()
    {
        return geteuid() == 0;
    }
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Options))]
    private static void HandleArguments(string[] args)
    {
        var parser = new Parser(with => with.HelpWriter = null);
        var parserResult = parser.ParseArguments<Options>(args);
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
                    Console.WriteLine(parserResultError);
                }
                Console.WriteLine();
            }
            Console.WriteLine(helpText.ToString());
            
            return;
        }
        Start(parserResult.Value);
    }
    
    static void Start(Options options)
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
}