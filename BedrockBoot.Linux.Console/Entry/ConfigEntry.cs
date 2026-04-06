using System.Text.Json.Serialization;

namespace BedrockBoot.Linux.Console.Entry;

public class ConfigEntry
{
    [JsonPropertyName("enableX11Detector")]
    public bool EnableX11Detector { get; set; } = true;
}