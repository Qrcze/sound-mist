using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Format
{
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }
}