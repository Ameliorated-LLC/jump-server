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
public class Location
{
    [YamlMember(Alias = "name")] public string Name { get; set; } = null!;
    [YamlMember(Alias = "username")] public string Username { get; set; } = null!;
    [YamlMember(Alias = "ip_addr")] public string IP { get; set; } = null!;
    [YamlMember(Alias = "ssh_port")]
    public int Port { get; set; }
        
    [YamlIgnore]
    public bool? Connected { get; set; }

    [YamlIgnore] public string Ping { get; set; } = "   0";
}

[YamlStaticContext]
public partial class JumpContext : StaticContext { }