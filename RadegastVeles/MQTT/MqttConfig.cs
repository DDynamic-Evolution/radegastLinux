using System.Text.Json.Serialization;

namespace Radegast.Veles.MQTT;

public class MqttConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 1883;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("root_topic")]
    public string RootTopic { get; set; } = "radegast";

    [JsonPropertyName("qos")]
    public int Qos { get; set; } = 1;

    [JsonPropertyName("use_tls")]
    public bool UseTls { get; set; }

    [JsonPropertyName("publish_chat_send")]
    public bool PublishChatSend { get; set; } = true;

    [JsonPropertyName("publish_chat_receive")]
    public bool PublishChatReceive { get; set; } = true;

    [JsonPropertyName("publish_im_send")]
    public bool PublishImSend { get; set; } = true;

    [JsonPropertyName("publish_im_receive")]
    public bool PublishImReceive { get; set; } = true;

    [JsonPropertyName("publish_location")]
    public bool PublishLocation { get; set; }

    [JsonPropertyName("subscribe_commands")]
    public bool SubscribeCommands { get; set; } = true;

    [JsonPropertyName("auto_connect")]
    public bool AutoConnect { get; set; }
}
