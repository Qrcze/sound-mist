using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SoundMist.Models
{
    public class LikedTracksCollection
    {
        [JsonPropertyName("collection")]
        public List<LikedTrack> Collection { get; set; } = [];

        [JsonPropertyName("next_href")]
        public string? NextHref { get; set; }

        [JsonPropertyName("query_urn")]
        public string? QueryUrn { get; set; }
    }

    public class TracksIdsCollection
    {
        [JsonPropertyName("collection")]
        public List<int> Collection { get; set; } = [];

        [JsonPropertyName("next_href")]
        public string? NextHref { get; set; }

        [JsonPropertyName("query_urn")]
        public string? QueryUrn { get; set; }
    }

    public class Track
    {
        public static Track CreatePlaceholderTrack(string author = "Empty", string title = "Track", string imageUrl = "https://upload.wikimedia.org/wikipedia/commons/3/3f/Placeholder_view_vector.svg")
        {
            return new Track()
            {
                User = new() { Username = author },
                Title = title,
                ArtworkUrl = imageUrl,
            };
        }

        public override string ToString() => FullLabel;

        [JsonIgnore] public string FullLabel => $"{PublisherMetadata?.Artist ?? User.Username} - {Title}";
        [JsonIgnore] public string ArtistName => PublisherMetadata?.Artist ?? User.Username;
        [JsonIgnore] public string LocalFilePath => $"{Globals.LocalDownloadsPath}/{FullLabel}.mp3"; //todo check for invalid characters in label

        [JsonIgnore]
        public string DurationFormatted
        {
            get
            {
                var time = TimeSpan.FromMilliseconds(FullDuration);
                if (time.Hours > 0)
                    return time.ToString(@"hh\:mm\:ss");
                return time.ToString(@"mm\:ss");
            }
        }

        [JsonIgnore] public string? ArtworkOrAvatarUrl => ArtworkUrl ?? User?.AvatarUrl;

        [JsonIgnore] public string? ArtworkUrlSmall => ArtworkOrAvatarUrl?.Replace("large", "small");
        [JsonIgnore] public string? ArtworkUrlOriginal => ArtworkOrAvatarUrl?.Replace("large", "original");
        [JsonIgnore] public string? ArtworkUrlLarge => ArtworkOrAvatarUrl;

        [JsonIgnore]
        public string CreatedAgo
        {
            get
            {
                DateTime diff = new(DateTime.Now.Ticks - CreatedLocalTime.Ticks);
                if (diff.Year - 1 > 0)
                    return $"{diff.Year - 1} years ago";
                if (diff.Month - 1 > 0)
                    return $"{diff.Month - 1} months ago";
                if (diff.Day - 1 > 0)
                    return $"{diff.Day - 1} days ago";
                if (diff.Hour - 1 > 0)
                    return $"{diff.Hour - 1} hours ago";
                return $"{diff.Minute - 1} minutes ago";
            }
        }

        [JsonIgnore] public DateTime CreatedLocalTime => CreatedAt.ToLocalTime();

        [JsonIgnore] public string LikesFormatted => LikesCount.HasValue ? ShortenedNumber(LikesCount.Value) : string.Empty;
        [JsonIgnore] public string PlaybacksFormatted => PlaybackCount.HasValue ? ShortenedNumber(PlaybackCount.Value) : string.Empty;

        string ShortenedNumber(int num)
        {
            if (num > 1_000_000)
                return $"{(float)num / 1_000_000:0.##}M";
            if (num > 10_000)
                return $"{(float)num / 1_000:0.##}K";
            return num.ToString();
        }

        [JsonIgnore] public bool HasGenre => !string.IsNullOrEmpty(Genre);

        public string? BackgroundVisualUrl
        {
            get
            {
                if (Visuals is not null && Visuals.Items.Count > 0)
                    return Visuals.Items.FirstOrDefault(x => x.VisualUrl is not null && x.VisualUrl.Contains("/bg/"))?.VisualUrl;
                return null;
            }
        }

        public bool HasBackgroundVisuals => !string.IsNullOrEmpty(BackgroundVisualUrl);

        [JsonPropertyName("artwork_url")]
        public string? ArtworkUrl { get; set; }

        [JsonPropertyName("caption")]
        public string? Caption { get; set; }

        [JsonPropertyName("comment_count")]
        public int? CommentCount { get; set; }

        [JsonPropertyName("commentable")]
        public bool? Commentable { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("display_date")]
        public DateTime? DisplayDate { get; set; }

        [JsonPropertyName("download_count")]
        public int? DownloadCount { get; set; }

        [JsonPropertyName("downloadable")]
        public bool? Downloadable { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("embeddable_by")]
        public string? EmbeddableBy { get; set; }

        [JsonPropertyName("full_duration")]
        public int FullDuration { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("has_downloads_left")]
        public bool? HasDownloadsLeft { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("label_name")]
        public string? LabelName { get; set; }

        [JsonPropertyName("last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("license")]
        public string? License { get; set; }

        [JsonPropertyName("likes_count")]
        public int? LikesCount { get; set; }

        [JsonPropertyName("media")]
        public Media Media { get; set; } = null!;

        [JsonPropertyName("monetization_model")]
        public string? MonetizationModel { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("permalink_url")]
        public string? PermalinkUrl { get; set; }

        [JsonPropertyName("playback_count")]
        public int? PlaybackCount { get; set; }

        /// <summary>
        /// possible policies: "ALLOW", "SNIP"
        /// </summary>
        [JsonPropertyName("policy")]
        public string? Policy { get; set; }

        [JsonPropertyName("public")]
        public bool? Public { get; set; }

        [JsonPropertyName("publisher_metadata")]
        public PublisherMetadata? PublisherMetadata { get; set; } = null!;

        [JsonPropertyName("purchase_title")]
        public string? PurchaseTitle { get; set; }

        [JsonPropertyName("purchase_url")]
        public string? PurchaseUrl { get; set; }

        [JsonPropertyName("release_date")]
        public DateTime? ReleaseDate { get; set; }

        [JsonPropertyName("reposts_count")]
        public int? RepostsCount { get; set; }

        [JsonPropertyName("secret_token")]
        public string? SecretToken { get; set; }

        [JsonPropertyName("sharing")]
        public string? Sharing { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("station_permalink")]
        public string? StationPermalink { get; set; }

        [JsonPropertyName("station_urn")]
        public string? StationUrn { get; set; }

        [JsonPropertyName("streamable")]
        public bool? Streamable { get; set; }

        [JsonPropertyName("tag_list")]
        public string? TagList { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("track_authorization")]
        public string? TrackAuthorization { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("urn")]
        public string? Urn { get; set; }

        [JsonPropertyName("user")]
        public User? User { get; set; } = null!;

        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonPropertyName("visuals")]
        public Visuals? Visuals { get; set; }

        [JsonPropertyName("waveform_url")]
        public string? WaveformUrl { get; set; }
    }

    public class Visuals
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("tracking")]
        public object? Tracking { get; set; }

        [JsonPropertyName("urn")]
        public string Urn { get; set; } = string.Empty;

        [JsonPropertyName("visuals")]
        public List<VisualItem> Items { get; set; } = [];
    }

    public class VisualItem
    {
        [JsonPropertyName("entry_time")]
        public int EntryTime { get; set; }

        [JsonPropertyName("urn")]
        public string Urn { get; set; } = string.Empty;

        [JsonPropertyName("visual_url")]
        public string VisualUrl { get; set; } = string.Empty;
    }

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

    public class TrackCollection
    {
        [JsonPropertyName("collection")]
        public List<Track> Collection { get; set; } = [];
    }

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

    public class Format
    {
        [JsonPropertyName("mime_type")]
        public string? MimeType { get; set; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }
    }

    public class Media
    {
        [JsonPropertyName("transcodings")]
        public List<Transcoding> Transcodings { get; set; } = null!;
    }

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
        public int? Id { get; set; }

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
}