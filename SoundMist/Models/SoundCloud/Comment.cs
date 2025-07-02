using SoundMist.Helpers;
using System;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
    public class Comment
    {
        [JsonIgnore] public string? TimestampFormatted => StringHelpers.DurationFormatted(Timestamp);
        [JsonIgnore] public string? TimeAgo => StringHelpers.TimeAgo(CreatedAt);
        [JsonIgnore] public Avalonia.Thickness ItemUiMargin => InThread ? new(60, 10, 10, 10) : new(10);

        public bool InThread { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("timestamp")]
        public int Timestamp { get; set; }

        [JsonPropertyName("track_id")]
        public int TrackId { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("self")]
        public CommentSelf Self { get; set; }

        [JsonPropertyName("user")]
        public User User { get; set; }
    }

    public class CommentSelf
    {
        [JsonPropertyName("urn")]
        public string Urn { get; set; }
    }
}