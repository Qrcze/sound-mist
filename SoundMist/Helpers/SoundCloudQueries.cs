using SoundMist.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SoundMist.Helpers
{
    public static class SoundCloudQueries
    {
        /// <summary> hard defined by SoundCloud api </summary>
        private const int TracksQueryLimit = 50;

        /// <inheritdoc cref="GetTracksById(HttpClient, string, int, IEnumerable{int}, CancellationToken)"/>
        public static async Task<List<Track>> GetTracksById(HttpClient httpClient, string clientId, int appVersion, IEnumerable<int> tracksIds)
            => await GetTracksById(httpClient, clientId, appVersion, tracksIds, CancellationToken.None);

        /// <summary>
        /// Will return a list of tracks with their full info. It divides each query into chunks of 50 tracks per request, so SC doesn't complain.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public static async Task<List<Track>> GetTracksById(HttpClient httpClient, string clientId, int appVersion, IEnumerable<int> tracksIds, CancellationToken token)
        {
            int skip = 0;
            var fullTracks = new List<Track>();
            while (true)
            {
                token.ThrowIfCancellationRequested();

                string Ids = string.Join(',', tracksIds.Skip(skip).Take(TracksQueryLimit));
                if (string.IsNullOrEmpty(Ids))
                    break;

                skip += 50;

                string url = $"https://api-v2.soundcloud.com/tracks?ids={HttpUtility.UrlEncode(Ids)}&client_id={clientId}&app_version={appVersion}&app_locale=en";
                using var response = await httpClient.GetAsync(url, token);
                response.EnsureSuccessStatusCode();

                var list = await response.Content.ReadFromJsonAsync<List<Track>>(token);
                fullTracks.AddRange(list!);
            }

            return fullTracks;
        }

        public static async Task<WaveformData?> GetTrackWaveform(HttpClient httpClient, string waveformUrl, CancellationToken token)
        {
            using var response = await httpClient.GetAsync(waveformUrl, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<WaveformData>(token);
        }

        internal static async Task<User?> GetUserInfo(HttpClient httpClient, string clientId, int appVersion, int userId, CancellationToken token)
        {
            using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/users/{userId}?client_id={clientId}&app_version={appVersion}&app_locale=en", token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<User>(token);
        }

        internal static async Task<Playlist?> GetPlaylistInfo(HttpClient httpClient, string clientId, int appVersion, int playlistId, CancellationToken token)
        {
            using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/playlists/{playlistId}?client_id={clientId}&app_version={appVersion}&app_locale=en", token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Playlist>(token);
        }

        /// <exception cref="TaskCanceledException" />
        public static async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedTracksIds(AuthorizedHttpClient httpClient, string clientId, int appVersion, CancellationToken token)
        {
            if (!httpClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/me/track_likes/ids?limit=200&client_id={clientId}&app_version={appVersion}&app_locale=en", token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<int>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed get liked tracks request: {ex.Message}");
            }
        }

        /// <exception cref="TaskCanceledException" />
        public static async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedUsersIds(AuthorizedHttpClient httpClient, string clientId, int appVersion, CancellationToken token)
        {
            if (!httpClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/me/user_likes/ids?limit=5000&client_id={clientId}&app_version={appVersion}&app_locale=en", token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<int>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed get liked users request: {ex.Message}");
            }
        }

        /// <exception cref="TaskCanceledException" />
        public static async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedPlaylistsIds(AuthorizedHttpClient httpClient, string clientId, int appVersion, CancellationToken token)
        {
            if (!httpClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/me/playlist_likes/ids?limit=5000&client_id={clientId}&app_version={appVersion}&app_locale=en", token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<int>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed get liked playlists request: {ex.Message}");
            }
        }

        /// <exception cref="TaskCanceledException" />
        internal static async Task<(QueryResponse<HistoryTrack>? tracks, string? errorMessage)> GetPlayHistory(AuthorizedHttpClient httpClient, string? href, string clientId, int appVersion, int offset, CancellationToken token)
        {
            if (!httpClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                if (string.IsNullOrEmpty(href))
                    href = $"https://api-v2.soundcloud.com/me/play-history/tracks?client_id={clientId}&limit=25&offset={offset}&linked_partitioning=1&app_version={appVersion}&app_locale=en";
                else
                    href += $"&client_id={clientId}&app_version={appVersion}&app_locale=en";

                using var response = await httpClient.GetAsync(href, token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<HistoryTrack>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed play history request: {ex.Message}");
            }
        }
    }
}