using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JumpServer.Commands;

public class Push
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);

    public static int Execute(Program.PushOptions options, bool sudo)
    {
        if (!sudo)
        {
            Console.WriteLine("sudo is required to run this command.");
            return 1;
        }
        
        if (!File.Exists("/usr/bin/jumper"))
        {
            Console.WriteLine("Warning: jumper not found in bin directory.");
            return 0;
        }

        if (!Directory.Exists("/etc/jumper"))
        {
            Console.WriteLine("Warning: jumper etc configuration directory not found.");
            return 0;
        }

        foreach (var file in Directory.GetFiles("/etc/jumper"))
        {
            var username = Path.GetFileName(file).Split('.').FirstOrDefault();
            if (username == null)
                continue;

            if (File.Exists($"/home/{username}/chroot/bin/jumper"))
            {
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
            }

            try
            {
                File.Copy("/usr/bin/jumper", $"/home/{username}/chroot/bin/jumper", true);
                File.Copy("/usr/bin/ssh", $"/home/{username}/chroot/bin/ssh", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: could not update user jumper executable in chroot user {username}: {e.Message}");
            }
        }

        return 0;
    }
}