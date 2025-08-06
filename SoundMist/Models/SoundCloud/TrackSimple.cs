using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class TrackSimple
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("monetization_model")]
    public string? MonetizationModel { get; set; }

    [JsonPropertyName("policy")]
    public string? Policy { get; set; }
}