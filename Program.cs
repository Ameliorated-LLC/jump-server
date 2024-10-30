using System.Drawing;
using System.Runtime.InteropServices;
using JumpServer.Menus;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JumpServer;

public class Program
{
    public static bool Authenticated = false;

    [DllImport("libc")]
    private static extern uint geteuid();

    public static bool IsRunningWithSudo()
    {
        return geteuid() == 0;
    }

    static void Main(string[] args)
    {
        if (!File.Exists("/etc/jump-server/config.yml") && !IsRunningWithSudo())
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

            if (!File.Exists("/etc/jump-server/config.yml"))
            {
                Menus.Setup.Main.Start();
                Exit(null, null);
                return;
            }

            try
            {
                var yaml = File.ReadAllText("/etc/jump-server/config.yml");
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

            while (true)
            {
                if (Configuration.Current.Locations.Count == 0)
                {
                    NoLocationsMenu.Show();
                    continue;
                }
                
                var menu = new JumpMenu();
                menu.Show();
                Canvas.Frame.FrameTitle = "Jump";
                Canvas.Frame.FrameHeight = Configuration.Current.Locations.Count + 4;
                Canvas.Frame.StatusBar = new DynamicBar() { Center = new Text("Press Ctrl + X to access admin menu", Color.LightGoldenrodYellow, (Color?)null) };
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

    public static void Exit(object? sender, EventArgs? e)
    {
        lock (_exitLock)
        {
            if (_exitTriggered)
                return;
            _exitTriggered = true;
            TerminalCommands.Execute(TerminalCommand.RestoreScreen);
            TerminalCommands.Execute(TerminalCommand.ShowCursor);
            TerminalCommands.Execute(TerminalCommand.RestoreCursorPos);
        }
    }

    private static object _exitLock = new object();
    private static volatile bool _exitTriggered = false;
}