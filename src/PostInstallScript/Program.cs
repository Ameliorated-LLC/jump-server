using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PostInstallScript;

class Program
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);
    
    static void Main(string[] args)
    {
        if (!File.Exists("/usr/bin/jumper"))
            return;

        if (Directory.Exists("/etc/jumper"))
        {
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
                    Console.WriteLine($"Error: could not update user jumper executable: {e.Message}");
                }
            }
        }
    }
}