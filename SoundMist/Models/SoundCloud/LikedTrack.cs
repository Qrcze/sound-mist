using System;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class LikedTrack
{
    public override string ToString() => Track.FullLabel;

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "like";

    [JsonPropertyName("track")]
    public Track Track { get; set; } = null!;
}