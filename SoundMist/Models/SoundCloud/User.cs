using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SoundMist.Models.SoundCloud
{
    public class User
    {
        [JsonIgnore] public string? BackgroundVisual => Visuals?.Items.FirstOrDefault()?.VisualUrl;

        //CountryCode comes in a form like "GB" instead of "United Kingdom" and such, so not very verbose, but it'll have to do for now
        [JsonIgnore] public string? CityAndCountry
        {
            get
            {
                if (string.IsNullOrEmpty(CountryCode))
                    return City;
                if (string.IsNullOrEmpty(City))
                    return CountryCode;
                return $"{City}, {CountryCodes.GetCountryName(CountryCode)}";
            }
        }

        [JsonIgnore] public bool HasCityOrCountry => !(string.IsNullOrEmpty(City) && string.IsNullOrEmpty(CountryCode));

        [JsonIgnore] public bool HasFullName => !string.IsNullOrEmpty(FullName);

        [JsonIgnore] public string? AvatarUrlSmall => AvatarUrl?.Replace("large", "small");
        [JsonIgnore] public string? AvatarUrlLarge => AvatarUrl;
        [JsonIgnore] public string? AvatarUrlOriginal => AvatarUrl?.Replace("large", "original");


        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = null!;

        [JsonPropertyName("badges")]
        public Badges? Badges { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("followers_count")]
        public int? FollowersCount { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("last_modified")]
        public DateTime? LastModified { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("permalink")]
        public string? Permalink { get; set; }

        [JsonPropertyName("permalink_url")]
        public string? PermalinkUrl { get; set; }

        [JsonPropertyName("station_permalink")]
        public string? StationPermalink { get; set; }

        [JsonPropertyName("station_urn")]
        public string? StationUrn { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("urn")]
        public string? Urn { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; } = null!;

        [JsonPropertyName("verified")]
        public bool? Verified { get; set; }

        //properties below are retrieved from the user query

        [JsonPropertyName("comments_count")]
        public int? CommentsCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("creator_subscription")]
        public CreatorSubscription? CreatorSubscription { get; set; }

        [JsonPropertyName("creator_subscriptions")]
        public List<CreatorSubscription> CreatorSubscriptions { get; set; } = [];

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("followings_count")]
        public int? FollowingsCount { get; set; }

        [JsonPropertyName("groups_count")]
        public int? GroupsCount { get; set; }

        [JsonPropertyName("likes_count")]
        public int? LikesCount { get; set; }

        [JsonPropertyName("playlist_count")]
        public int? PlaylistCount { get; set; }

        [JsonPropertyName("playlist_likes_count")]
        public int? PlaylistLikesCount { get; set; }

        [JsonPropertyName("reposts_count")]
        public object? RepostsCount { get; set; }

        [JsonPropertyName("track_count")]
        public int? TrackCount { get; set; }

        [JsonPropertyName("visuals")]
        public Visuals? Visuals { get; set; }

        //properties below are extras from the official api documentation for /me; high probability they can be null or outdated
        //also not all of them are publicly available information, some of them only show for the authorized user

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("discogs_name")]
        public string? DiscogsName { get; set; }

        [JsonPropertyName("locale")]
        public string? Locale { get; set; }

        [JsonPropertyName("online")]
        public bool? Online { get; set; }

        [JsonPropertyName("plan")]
        public string? Plan { get; set; }

        [JsonPropertyName("primary_email_confirmed")]
        public bool? PrimaryEmailConfirmed { get; set; }

        [JsonPropertyName("private_playlists_count")]
        public int? PrivatePlaylistsCount { get; set; }

        [JsonPropertyName("private_tracks_count")]
        public int? PrivateTracksCount { get; set; }

        [JsonPropertyName("public_favorites_count")]
        public int? PublicFavoritesCount { get; set; }

        [JsonPropertyName("quota")]
        public Quota? Quota { get; set; }

        [JsonPropertyName("subscriptions")]
        public List<Subscription>? Subscriptions { get; set; }

        [JsonPropertyName("upload_seconds_left")]
        public int? UploadSecondsLeft { get; set; }

        [JsonPropertyName("website")]
        public string? Website { get; set; }

        [JsonPropertyName("website_title")]
        public string? WebsiteTitle { get; set; }

        internal static User CreateDeletedUser(long id)
        {
            return new()
            {
                Id = id,
                Username = $"Missing User {id}",
                AvatarUrl = "",
            };
        }
    }

    public class CreatorSubscription
    {
        [JsonPropertyName("product")]
        public Product Product { get; set; }
    }

    public class Product
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class Quota
    {
        [JsonPropertyName("unlimited_upload_quota")]
        public bool UnlimitedUploadQuota { get; set; }

        [JsonPropertyName("upload_seconds_used")]
        public int UploadSecondsUsed { get; set; }

        [JsonPropertyName("upload_seconds_left")]
        public int UploadSecondsLeft { get; set; }
    }

    public class Subscription
    {
        [JsonPropertyName("product")]
        public Product? Product { get; set; }

        [JsonPropertyName("recurring")]
        public bool Recurring { get; set; }
    }
}