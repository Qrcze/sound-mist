using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class PublisherMetadata
{
    [JsonPropertyName("album_title")]
    public string? AlbumTitle { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("contains_music")]
    public bool? ContainsMusic { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("isrc")]
    public string? Isrc { get; set; }

    [JsonPropertyName("p_line")]
    public string? PLine { get; set; }

    [JsonPropertyName("p_line_for_display")]
    public string? PLineForDisplay { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("release_title")]
    public string? ReleaseTitle { get; set; }

    [JsonPropertyName("urn")]
    public string? Urn { get; set; }

    [JsonPropertyName("writer_composer")]
    public string? WriterComposer { get; set; }
}