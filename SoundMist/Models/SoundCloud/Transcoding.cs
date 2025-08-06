using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Transcoding
{
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("format")]
    public Format Format { get; set; } = null!;

    [JsonPropertyName("is_legacy_transcoding")]
    public bool? IsLegacyTranscoding { get; set; }

    [JsonPropertyName("preset")]
    public string? Preset { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("snipped")]
    public bool? Snipped { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}