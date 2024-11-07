using System.Drawing;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

namespace JumpServer;

public class AuthenticateMenu
{
    public static bool Show()
    {
        Canvas.Set(new Frame("Authenticate", 6, 52,
            new DynamicBar() { Center = new Text(Configuration.Current.ServerName, AnsiColor.Grey93, (AnsiColor?)null).Compile() },
            new DynamicBar() { Center = new Text("Press Ctrl + X to return to menu", AnsiColor.Cornsilk1, (AnsiColor?)null).Compile()
            }));

        int tries = 0;
        while (true)
        {
            Canvas.WriteFrameLine(0, 0, "");
            Canvas.WriteFrame(0, 0, " Enter password: ");
            var password = ReadPassword(tries);
            TerminalCommands.Execute(TerminalCommand.HideCursor);
            
            if (password == null)
                return false;
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes("jumper-salt" + password));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        
            if (hash == Configuration.Current.AdminPassword)
                return true;

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
                        Canvas.WriteFrame(0, 0, " Enter password: " + new string('*', password.Length));
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
                        Canvas.WriteFrame(0, " Enter password: ".Length + password.Length - 1, " ");
                        password = password.Substring(0, password.Length - 1);
                        Canvas.WriteFrame(0, " Enter password: ".Length + password.Length - 1, password.Length > 0 ? "*" : " ");
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

                    Canvas.WriteFrame(0, " Enter password: ".Length + password.Length, "*");
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
}