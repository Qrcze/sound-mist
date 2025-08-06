using System;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud;

public class HistoryTrack
{
    [JsonPropertyName("played_at")]
    public long? PlayedAtEpochMs { get; set; }

    public DateTime? PlayedAt => PlayedAtEpochMs.HasValue ? new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(PlayedAtEpochMs.Value) : null;

    [JsonPropertyName("track_id")]
    public long TrackId { get; set; }

    [JsonPropertyName("track")]
    public Track? Track { get; set; }
}