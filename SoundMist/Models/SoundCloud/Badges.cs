using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class Badges
{
    [JsonPropertyName("creator_mid_tier")]
    public bool? CreatorMidTier { get; set; }

    [JsonPropertyName("pro")]
    public bool? Pro { get; set; }

    [JsonPropertyName("pro_unlimited")]
    public bool? ProUnlimited { get; set; }

    [JsonPropertyName("verified")]
    public bool? Verified { get; set; }
}