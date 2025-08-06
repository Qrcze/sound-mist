using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class VisualItem
{
    [JsonPropertyName("entry_time")]
    public int EntryTime { get; set; }

    [JsonPropertyName("urn")]
    public string Urn { get; set; } = string.Empty;

    [JsonPropertyName("visual_url")]
    public string VisualUrl { get; set; } = string.Empty;
}