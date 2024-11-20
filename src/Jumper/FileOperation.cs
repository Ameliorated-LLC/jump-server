using System.Text;

namespace JumpServer;

public class FileOperation : IDisposable
{
    public Mutex Mutex { get; private set; }
    public string FilePath { get; private set; }


    public FileOperation(string filePath)
    {
        FilePath = filePath;
        Mutex = new Mutex(false, $"jumper-{Convert.ToBase64String(Encoding.UTF8.GetBytes(filePath))}");
        Mutex.WaitOne();
    }
    
    public void Dispose()
    {
        Mutex.ReleaseMutex();  
        Mutex.Dispose();
    }
}