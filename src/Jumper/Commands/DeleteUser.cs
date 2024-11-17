using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace JumpServer.Commands;

public class DeleteUser
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);

    public static int Execute(Program.DeleteUserOptions options, bool sudo)
    {
        if (!sudo)
        {
            Console.WriteLine("sudo is required to run this command.");
            return 1;
        }
        if (!Directory.Exists("/etc/jumper"))
        {
            Console.WriteLine("Configuration directory for jumper doesn't exist.");
            return 1;
        }

        var file = Directory.GetFiles("/etc/jumper").FirstOrDefault(x => Path.GetFileName(x).Split('.').FirstOrDefault()?.StartsWith(options.Username) ?? false);
        if (file == null)
            Console.WriteLine($"Warning: No jumper configuration found for {options.Username}.");
        if (!File.Exists($"/home/{options.Username}/chroot/bin/jumper"))
            Console.WriteLine($"Warning: No home directory found for {options.Username}.");
        else
        {
            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    if (process.MainModule == null)
                        continue;
                    if (process.MainModule.FileName.Equals($"/home/{options.Username}/chroot/bin/jumper", StringComparison.OrdinalIgnoreCase) ||
                        process.MainModule.FileName.Equals($"/home/{options.Username}/chroot/bin/ssh", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Killing {(process.MainModule.FileName.EndsWith("ssh") ? "jumper ssh" : "jumper")} process (PID: {process.Id})");

                        // SIGTERM
                        kill(process.Id, 15);
                        if (!process.WaitForExit(2500))
                            process.Kill();
                        if (!process.WaitForExit(2500))
                            throw new Exception("Process did not exit.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: error killing jumper process: {ex.Message}");
            }

            if (!options.Confirm)
            {
                Console.Write($"Delete jumper chroot user {options.Username.ToColored(AnsiColor.Fuchsia)} with key? [Y/N] ");
                bool delete;
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Y)
                    {
                        Console.WriteLine("Y");
                        delete = true;
                        break;
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        Console.WriteLine("N");
                        delete = false;
                        break;
                    }
                }

                if (!delete)
                    return 5;
            }
            
            try
            {
                File.Copy("/usr/bin/sh", $"/home/{options.Username}/chroot/bin/jumper", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: Could not update user jumper executable: {e.Message}");
            }
            
            try
            {
                if (Utilities.ExecuteCommand("id", [options.Username]).ExitCode == 0)
                {
                    if (Utilities.ExecuteCommand("userdel", [options.Username]).ExitCode != 0)
                        Console.WriteLine($"Warning: userdel exited with a non-zero exit code.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: could not delete user: {e.Message}");
            }

            try
            {
                Directory.Delete($"/home/{options.Username}", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: could not delete user directory: {e.Message}");
            }

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
                        if (Regex.IsMatch(line, $@"^Match User {Regex.Escape(options.Username!)}\s*$"))
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
            catch (Exception e)
            {
                Console.WriteLine($"Warning: could not remove sshd user config entry: {e.Message}");
            }
        }

        if (file != null)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Warning: failed to delete {options.Username}.config.yml.");
            }
        }

        return 0;
    }
}