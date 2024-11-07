using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PostRemoveScript;

class Program
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);
    
    static void Main(string[] args)
    {
        if (!Directory.Exists("/etc/jumper"))
            return;
        
        var action = args.FirstOrDefault();
        if (action != "remove" && action != "purge")
            return;
            
        foreach (var file in Directory.GetFiles("/etc/jumper"))
        {
            var username = Path.GetFileName(file).Split('.').FirstOrDefault();
            if (username == null || !File.Exists($"/home/{username}/chroot/bin/jumper"))
                continue;

            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    if (process.MainModule == null)
                        continue;
                    if (process.MainModule.FileName.Equals($"/home/{username}/chroot/bin/jumper", StringComparison.OrdinalIgnoreCase) ||
                        process.MainModule.FileName.Equals($"/home/{username}/chroot/bin/ssh", StringComparison.OrdinalIgnoreCase))
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

            try
            {
                File.Copy("/usr/bin/sh", $"/home/{username}/chroot/bin/jumper", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: could not update user jumper executable: {e.Message}");
            }
            
            Console.Write($"Delete jumper chroot user {username.ToColored(AnsiColor.Fuchsia)} with key? [Y/N] ");
            bool delete;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Y)
                {
                    Console.WriteLine("Y");
                    delete = true;
                    break;
                } else if (key.Key == ConsoleKey.N)
                {
                    Console.WriteLine("N");
                    delete = false;
                    break;
                }
            }
            if (!delete)
                continue;

            try
            {
                if (ExecuteCommand("id", [username]).ExitCode == 0)
                {
                    if (ExecuteCommand("userdel", [username]).ExitCode != 0)
                        Console.WriteLine($"Error: userdel exited with a non-zero exit code.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: could not delete user: {e.Message}");
            }


            try
            {
                File.Delete(file);
                if (Directory.GetFileSystemEntries("/etc/jumper").Length == 0)
                    Directory.Delete("/etc/jumper");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: failed to delete {username}.config.yml.");
            }

            try
            {
                Directory.Delete($"/home/{username}", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: could not delete user directory: {e.Message}");
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
            catch (Exception e)
            {
                Console.WriteLine($"Warning: could not remove sshd user config entry: {e.Message}");
            }
        }

    }
        
        
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