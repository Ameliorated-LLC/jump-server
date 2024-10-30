using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PostInstallScript;

class Program
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);
    
    static void Main(string[] args)
    {
        if (!Directory.Exists("/etc/jump-server") || !File.Exists("/usr/bin/jump-server"))
            return;

        foreach (var file in Directory.GetFiles("/etc/jump-server"))
        {
            var username = Path.GetFileName(file).Split('.').FirstOrDefault();
            if (username == null || !File.Exists($"/home/{username}/chroot/bin/jump-server"))
                continue;

            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    if (process.MainModule == null)
                        continue;
                    if (process.MainModule.FileName.Equals($"/home/{username}/chroot/bin/jump-server", StringComparison.OrdinalIgnoreCase) ||
                        process.MainModule.FileName.Equals($"/home/{username}/chroot/bin/ssh", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Killing {(process.MainModule.FileName.EndsWith("ssh") ? "jump-server ssh" : "jump-server")} process (PID: {process.Id})");
                        
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
                Console.WriteLine($"Warning: error killing jump-server process: {ex.Message}");
            }

            try
            {
                File.Copy("/usr/bin/jump-server", $"/home/{username}/chroot/bin/jump-server", true);
                File.Copy("/usr/bin/ssh", $"/home/{username}/chroot/bin/ssh", true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: could not update user jump-server executable: {e.Message}");
            }
        }
    }
}