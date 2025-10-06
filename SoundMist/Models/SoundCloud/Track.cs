using SoundMist.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
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

        internal static Track CreateRemovedTrack(long id)
        {
            return new()
            {
                Id = id,
                Title = $"Deleted Track {id}",
                ArtworkUrl = "",
                User = User.CreateDeletedUser(-1)
            };
        }

        [JsonIgnore] public string FullLabel => $"{PublisherMetadata?.Artist ?? User.Username} - {Title}";
        [JsonIgnore] public string ArtistName => PublisherMetadata?.Artist ?? User.Username;
        [JsonIgnore] public string LocalFilePath => $"{Globals.LocalDownloadsPath}/{FullLabel}.mp3"; //todo check for invalid characters in label

        [JsonIgnore] public string DurationFormatted => StringHelpers.DurationFormatted(FullDuration);

        [JsonIgnore] public string? LikedThumbnail => ArtworkUrl?.Replace("large", "t200x200") ?? User?.AvatarUrl.Replace("large", "t200x200");
        [JsonIgnore] public string? ArtworkOrAvatarUrl => ArtworkUrl ?? User?.AvatarUrl;
        [JsonIgnore] public string? ArtworkOrAvatarUrlOriginal => ArtworkUrl?.Replace("large", "original") ?? User?.AvatarUrl?.Replace("large", "original");

        [JsonIgnore] public string? ArtworkUrlSmall => ArtworkOrAvatarUrl?.Replace("large", "small");
        [JsonIgnore] public string? ArtworkUrlLarge => ArtworkOrAvatarUrl;
        [JsonIgnore] public string? ArtworkUrlOriginal => ArtworkOrAvatarUrl?.Replace("large", "original");

        [JsonIgnore] public string DisplayDateAgo => StringHelpers.TimeAgo(DisplayDate);
        [JsonIgnore] public bool ShowDisplayDate => DisplayDate != CreatedAt && DisplayDate != LastModified;
        [JsonIgnore] public string CreatedAgo => StringHelpers.TimeAgo(CreatedAt);
        [JsonIgnore] public string ModifiedAgo => StringHelpers.TimeAgo(LastModified);
        [JsonIgnore] public bool WasModified => CreatedAt != LastModified;

        [JsonIgnore] public DateTime CreatedLocalTime => CreatedAt.ToLocalTime();
        [JsonIgnore] public DateTime ModifiedLocalTime => LastModified.ToLocalTime();

        [JsonIgnore] public string LikesFormatted => LikesCount.HasValue ? StringHelpers.ShortenedNumber(LikesCount.Value) : "0";
        [JsonIgnore] public string PlaybackFormatted => PlaybackCount.HasValue ? StringHelpers.ShortenedNumber(PlaybackCount.Value) : "0";
        [JsonIgnore] public string RepostsFormatted => RepostsCount.HasValue ? StringHelpers.ShortenedNumber(RepostsCount.Value) : "0";
        [JsonIgnore] public string CommentFormatted => CommentCount.HasValue ? StringHelpers.ShortenedNumber(CommentCount.Value) : "0";

        [JsonIgnore] public string PlaybackTooltip => PlaybackCount.HasValue ? $"{PlaybackCount.Value:n0} plays" : "0 plays";
        [JsonIgnore] public string LikesTooltip => LikesCount.HasValue ? $"{LikesCount.Value:n0} likes" : "0 likes";
        [JsonIgnore] public string RepostsTooltip => RepostsCount.HasValue ? $"{RepostsCount.Value:n0} reposts" : "0 reposts";
        [JsonIgnore] public string CommentTooltip => CommentCount.HasValue ? $"{CommentCount.Value:n0} comments" : "0 reposts";

        [JsonIgnore] public bool HasPlaybacks => PlaybackCount.HasValue && PlaybackCount.Value > 0;
        [JsonIgnore] public bool HasLikes => LikesCount.HasValue && LikesCount.Value > 0;
        [JsonIgnore] public bool HasReposts => RepostsCount.HasValue && RepostsCount.Value > 0;
        [JsonIgnore] public bool HasComment => CommentCount.HasValue && CommentCount.Value > 0;

        [JsonIgnore] public bool HasGenre => !string.IsNullOrEmpty(Genre);

        [JsonIgnore]
        public string? BackgroundVisualUrl
        {
            get
            {
                if (Visuals is not null && Visuals.Items.Count > 0)
                    return Visuals.Items.FirstOrDefault(x => x.VisualUrl is not null && x.VisualUrl.Contains("/bg/"))?.VisualUrl;
                return null;
            }
        }

        [JsonIgnore] public bool HasBackgroundVisuals => !string.IsNullOrEmpty(BackgroundVisualUrl);

        [JsonIgnore] public bool RegionBlocked => Policy == "BLOCK";
        [JsonIgnore] public bool Snipped => Policy == "SNIP";
        [JsonIgnore] public bool FromAutoplay { get; set; }

        [JsonIgnore]
        public List<string> TagListArray
        {
            get
            {
                if (string.IsNullOrEmpty(TagList))
                    return [];

                //TagList?.Split(' ') ?? []
                List<string> tags = [];
                int start = 0;
                bool compoundTag = false;
                for (int i = 0; i < TagList.Length; i++)
                {
                    var c = TagList[i];
                    if (c == '"')
                    {
                        if (!compoundTag)
                        {
                            compoundTag = true;
                            start = i + 1;
                        }
                        else
                        {
                            compoundTag = false;
                            tags.Add(TagList[start..i]);
                            start = i + 2;
                            i++;
                        }
                    }
                    else if (!compoundTag && char.IsWhiteSpace(c))
                    {
                        tags.Add(TagList[start..i]);
                        start = i + 1;
                    }
                }

                return tags;
            }
        }

        [JsonIgnore] public bool HasTags => !string.IsNullOrEmpty(TagList);

        [JsonIgnore] public bool IsRepost => RepostingUser is not null;
        [JsonIgnore] public User? RepostingUser { get; set; }
        [JsonIgnore] public string RepostingUserUsername => RepostingUser?.Username ?? string.Empty;

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
        public DateTime DisplayDate { get; set; }

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
        public long Id { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("label_name")]
        public string? LabelName { get; set; }

        [JsonPropertyName("last_modified")]
        public DateTime LastModified { get; set; }

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
        public long? UserId { get; set; }

        [JsonPropertyName("visuals")]
        public Visuals? Visuals { get; set; }

        [JsonPropertyName("waveform_url")]
        public string? WaveformUrl { get; set; }
    }
}