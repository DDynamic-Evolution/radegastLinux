using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Radegast.Veles.VPN;

public class VpnProfile
{
    public string Name { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<VpnPeer> Peers { get; set; } = [];
    public bool AutoConnect { get; set; }

    public VpnProfile Clone() => new()
    {
        Name = Name,
        PrivateKey = PrivateKey,
        Address = Address,
        Peers = Peers.ConvertAll(p => new VpnPeer
        {
            PublicKey = p.PublicKey,
            Endpoint = p.Endpoint,
            AllowedIPs = [.. p.AllowedIPs],
            PersistentKeepalive = p.PersistentKeepalive,
        }),
        AutoConnect = AutoConnect,
    };
}

public class VpnPeer
{
    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("allowed_ips")]
    public List<string> AllowedIPs { get; set; } = ["0.0.0.0/0"];

    [JsonPropertyName("persistent_keepalive")]
    public int PersistentKeepalive { get; set; } = 25;
}
