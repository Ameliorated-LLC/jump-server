using System.Text;
using Konscious.Security.Cryptography;

namespace JumpServer.Commands;

public class ChangePassword
{
    public static int Execute(Program.ChangePasswordOptions options, bool sudo)
    {
        if (!Directory.Exists("/etc/jumper") || !Directory.GetFiles("/etc/jumper").Any())
        {
            Console.WriteLine("Jumper has not been configured with a user yet.");
            return 1;
        }

        var file = Directory.GetFiles("/etc/jumper")
            .FirstOrDefault(file => Path.GetFileName(file).Split('.').FirstOrDefault()?.StartsWith(options.Username) ?? false);
        if (file == null)
        {
            Console.WriteLine($"No jumper configuration found for {options.Username}.");
            return 1;
        }

        string yaml;
        using (var _ = new FileOperation(file))
            yaml = File.ReadAllText(file);
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
                argon2.Iterations = 10;

                byte[] hashBytes = argon2.GetBytes(32);
                base64 = Convert.ToBase64String(hashBytes);
            }
        }

        configuration.AdminPassword = base64;

        using (var _ = new FileOperation(file))
            File.WriteAllText(file, configuration.Serialize());
        Console.WriteLine(Environment.NewLine + "Password changed successfully.".ToColored(AnsiColor.Green3));
        return 0;
    }

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