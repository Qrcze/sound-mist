using SoundMist.Helpers;
using System;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class UserEntry
{
    [JsonIgnore] public string FormattedCreatedAt => StringHelpers.TimeAgo(CreatedAt);

    [JsonPropertyName("caption")]
    public object? Caption { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("track")]
    public Track? Track { get; set; }

    [JsonPropertyName("playlist")]
    public Playlist? Playlist { get; set; }

    [JsonPropertyName("user")]
    public User User { get; set; } = null!;

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;
}