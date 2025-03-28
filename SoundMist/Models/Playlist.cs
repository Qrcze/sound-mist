using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SoundMist.Models
{
    //public class PlaylistCollection
    //{
    //    [JsonPropertyName("collection")]
    //    public List<Playlist> Collection { get; set; }
    //}

    public class Playlist
    {
        [JsonIgnore] public string Author => User.Username;
        [JsonIgnore] public string? ReleaseYear => ReleaseDate.HasValue ? ReleaseDate.Value.Year.ToString() : string.Empty;

        //sometimes it doesn't send all 5 of the tracks in full data, possibly happens when there's a lot of tracks
        //potential todo: have it send a request to get the tracks
        [JsonIgnore] public IEnumerable<Track> FirstFiveTracks => Tracks.Where(x => x.User is not null).Take(5);

        [JsonIgnore] public bool HasMoreTracks => Tracks.Count > 5;

        [JsonIgnore] public bool HasGenre => !string.IsNullOrEmpty(Genre);

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

        [JsonIgnore] public string? ArtworkOrFirstTrackArtwork => ArtworkUrl ?? (Tracks.Count > 0 ? Tracks[0].ArtworkOrAvatarUrl : User.AvatarUrl);

        [JsonPropertyName("artwork_url")]
        public string? ArtworkUrl { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("display_date")]
        public DateTime? DisplayDate { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }

        [JsonPropertyName("embeddable_by")]
        public string? EmbeddableBy { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("is_album")]
        public bool IsAlbum { get; set; }

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

        [JsonPropertyName("managed_by_feeds")]
        public bool ManagedByFeeds { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("permalink_url")]
        public string? PermalinkUrl { get; set; }

        [JsonPropertyName("public")]
        public bool Public { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; set; }

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

        [JsonPropertyName("set_type")]
        public string? SetType { get; set; }

        [JsonPropertyName("sharing")]
        public string? Sharing { get; set; }

        [JsonPropertyName("tag_list")]
        public string? TagList { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("track_count")]
        public int? TrackCount { get; set; }

        [JsonPropertyName("tracks")]
        public List<Track> Tracks { get; set; } = new();

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("user")]
        public User User { get; set; }

        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
    }

    public class TrackSimple
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("monetization_model")]
        public string? MonetizationModel { get; set; }

        [JsonPropertyName("policy")]
        public string? Policy { get; set; }
    }
}