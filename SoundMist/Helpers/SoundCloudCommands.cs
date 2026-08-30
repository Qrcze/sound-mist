using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SoundMist.Helpers
{
    public class SoundCloudCommands(IHttpManager httpManager, ProgramSettings settings)
    {
        private readonly IHttpManager _httpManager = httpManager;
        private readonly ProgramSettings _settings = settings;

        public event Action<Track, bool>? TrackLikeChanged;

        public Task<(bool success, string message)> SetTrackLiked(bool liked, long trackId)
            => SetTrackLiked(liked, new Track { Id = trackId });

        public async Task<(bool success, string message)> SetTrackLiked(bool liked, Track track)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized || !_settings.UserId.HasValue)
                return (false, "User not logged-in");

            try
            {
                string url = $"users/{_settings.UserId.Value}/track_likes/{track.Id}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";
                using var request = new HttpRequestMessage(liked ? HttpMethod.Put : HttpMethod.Delete, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await _httpManager.AuthorizedClient.SendAsync(request);

                // Both operations are safe to retry. SoundCloud reports an already-liked
                // track as a conflict and an already-removed like as not found.
                bool alreadyInRequestedState = (liked && response.StatusCode == HttpStatusCode.Conflict)
                    || (!liked && response.StatusCode == HttpStatusCode.NotFound);
                if (!response.IsSuccessStatusCode && !alreadyInRequestedState)
                {
                    string details = await response.Content.ReadAsStringAsync();
                    string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
                    return (false, $"SoundCloud returned {(int)response.StatusCode} ({response.ReasonPhrase}).{suffix}");
                }

                TrackLikeChanged?.Invoke(track, liked);
                return (true, "OK");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Failed sending a like request: {ex.Message}");
            }
        }

        [Obsolete("Use SetTrackLiked.")]
        public Task<(bool success, string message)> ToggleLikedDisliked(bool liked, long trackId)
            => SetTrackLiked(liked, trackId);
    }
}
