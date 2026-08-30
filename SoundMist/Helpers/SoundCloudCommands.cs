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

            string trackReference = track.Urn ?? $"soundcloud:tracks:{track.Id}";
            string publicUrl = $"https://api.soundcloud.com/likes/tracks/{Uri.EscapeDataString(trackReference)}";
            var publicResult = await SendLikeRequest(liked, publicUrl, includeWebHeaders: false, internalEndpoint: false);
            if (publicResult.success)
            {
                TrackLikeChanged?.Invoke(track, liked);
                return (true, "OK");
            }

            // The public API may reject a web-session token. Keep the internal web
            // endpoint as a fallback for sessions that are accepted by api-v2.
            string internalUrl = $"users/{_settings.UserId.Value}/track_likes/{track.Id}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";
            var internalResult = await SendLikeRequest(liked, internalUrl, includeWebHeaders: true, internalEndpoint: true);
            if (internalResult.success)
            {
                TrackLikeChanged?.Invoke(track, liked);
                return (true, "OK");
            }

            return (false, $"Public API: {publicResult.message} Internal API: {internalResult.message}");
        }

        private async Task<(bool success, string message)> SendLikeRequest(bool liked, string url, bool includeWebHeaders, bool internalEndpoint)
        {
            try
            {
                HttpMethod method = internalEndpoint
                    ? (liked ? HttpMethod.Put : HttpMethod.Delete)
                    : (liked ? HttpMethod.Post : HttpMethod.Delete);
                using var request = new HttpRequestMessage(method, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (includeWebHeaders)
                {
                    request.Headers.Referrer = new Uri("https://soundcloud.com/");
                    request.Headers.TryAddWithoutValidation("Origin", "https://soundcloud.com");
                    request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                }

                using var response = await _httpManager.AuthorizedClient.SendAsync(request);

                // These responses mean the requested state was already reached.
                bool alreadyInRequestedState = (liked && response.StatusCode == HttpStatusCode.Conflict)
                    || (!liked && response.StatusCode == HttpStatusCode.NotFound);
                if (response.IsSuccessStatusCode || alreadyInRequestedState)
                    return (true, "OK");

                string details = (await response.Content.ReadAsStringAsync()).Trim();
                if (details.Length > 300)
                    details = details[..300] + "...";
                string suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    suffix += " SoundCloud may require a fresh datadome cookie or browser verification.";
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    suffix += " Please sign in again.";
                return (false, $"SoundCloud returned {(int)response.StatusCode} ({response.ReasonPhrase}).{suffix}");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Failed sending a like request: {ex.Message}");
            }
        }
    }
}
