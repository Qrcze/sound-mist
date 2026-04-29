using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

[DebuggerDisplay("{MimeType} {Protocol}")]
public class Format
{
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; set; }
}