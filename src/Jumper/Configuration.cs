using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JumpServer;

[YamlSerializable]
public class Configuration
{
    public static Configuration Current { get; set; } = new Configuration();
    
    [YamlMember(Alias = "server_name")]
    public string ServerName { get; set; } = "Jump Server Setup";
    [YamlMember(Alias = "admin_password")]
    public string? AdminPassword { get; set; }
    [YamlMember(Alias = "restrict_admin_access")]
    public bool RestrictAdminAccess { get; set; }
    
    public List<Location> Locations { get; set; } = [];

    public string Serialize()
    {
        var serializer = new StaticSerializerBuilder(new JumpContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return serializer.Serialize(this);
    }

    public static Configuration Deserialize(string yaml)
    {
        var deserializer = new StaticDeserializerBuilder(new JumpContext())
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<Configuration>(yaml);
    }
}

[YamlSerializable]
public class Location : IDisposable, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [YamlMember(Alias = "name")] public string Name { get => _name; set => SetProperty(ref _name, value); }
    private string _name = null!;
    [YamlMember(Alias = "username")] public string Username { get => _username; set => SetProperty(ref _username, value); }
    private string _username = null!;

    [YamlMember(Alias = "ip_addr")]
    public string IP
    {
        get => _ip;
        set
        {
            SetProperty(ref _ip, value);
            try
            {
                _connectionPropertyChanged.Release();
            }
            catch
            {
            }
        }
    }
    private string _ip = null!;

    [YamlMember(Alias = "ssh_port")]
    public int Port
    {
        get => _port;
        set
        {
            SetProperty(ref _port, value);
            try
            {
                _connectionPropertyChanged.Release();
            }
            catch
            {
            }
        }
    }
    private int _port = 22;


    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
        
    [YamlIgnore]
    public bool? Connected { get => _connected; set => SetProperty(ref _connected, value); }
    private bool? _connected = null;

    [YamlIgnore] public int Ping { get => _ping; set => SetProperty(ref _ping, value); }
    private int _ping = 0;

    private SemaphoreSlim _connectionPropertyChanged = new SemaphoreSlim(0, 1);
    private CancellationTokenSource? _cts = null;
    public void StartPinging()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Run(() =>
        {
            try
            {
                _connectionPropertyChanged.Wait(0);
                
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    
                    var result = false;
                    try
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        using (TcpClient client = new TcpClient())
                        {
                            stopwatch.Start();
                            client.ConnectAsync(IP, Port, token).AsTask().Wait(Connected == null ? 1500 : 2500);
                            stopwatch.Stop();

                            result = client.Connected;
                        }
                        
                        if (result)
                        {
                            try
                            {
                                using var ping1 = new Ping();
                                var reply = ping1.SendPingAsync(IP, TimeSpan.FromMilliseconds(Connected == null ? 1500 : 2500), cancellationToken: token).GetAwaiter().GetResult();
                                if (reply.Status == IPStatus.Success)
                                    Ping = (int)reply.RoundtripTime;
                                else
                                    throw new Exception();
                            }
                            catch
                            {
                                token.ThrowIfCancellationRequested();
                                Ping = (int)stopwatch.ElapsedMilliseconds;
                            }
                        }
                    }
                    catch
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    Connected = result;

                    if (Connected != true)
                    {
                        _connectionPropertyChanged.Wait(10000, token);
                        continue;
                    }
                    
                    Thread.Sleep(3000);

                    using var ping2 = new Ping();
                    while (!_connectionPropertyChanged.Wait(0))
                    {
                        try
                        {
                            token.ThrowIfCancellationRequested();

                            var reply = ping2.SendPingAsync(IP, TimeSpan.FromMilliseconds(2500), cancellationToken: token).GetAwaiter().GetResult();
                            if (reply.Status == IPStatus.Success)
                                Ping = (int)reply.RoundtripTime;
                            else
                                throw new Exception();

                            token.ThrowIfCancellationRequested();
                            Thread.Sleep(3000);
                        }
                        catch
                        {
                            token.ThrowIfCancellationRequested();
                            Thread.Sleep(3000);
                            break;
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        });
    }
    
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _connectionPropertyChanged.Dispose();
    }
}

[YamlStaticContext]
public partial class JumpContext : StaticContext { }