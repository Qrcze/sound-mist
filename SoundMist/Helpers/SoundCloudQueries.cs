using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SoundMist.Helpers
{
    public class SoundCloudQueries(IHttpManager httpManager, ProgramSettings settings)
    {
        /// <summary> hard defined by SoundCloud api </summary>
        private const int TracksQueryLimit = 50;

        private readonly IHttpManager _httpManager = httpManager;
        private readonly ProgramSettings _settings = settings;

        /// <inheritdoc cref="GetTracksById(IEnumerable{int}, bool, CancellationToken)"/>
        public async Task<List<Track>> GetTracksById(IEnumerable<int> tracksIds, bool forceProxy = false)
            => await GetTracksById(tracksIds, forceProxy, CancellationToken.None);

        /// <summary>
        /// Will return a list of tracks with their full info. It divides each query into chunks of 50 tracks per request, so SC doesn't complain.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="HttpRequestException" />
        /// <exception cref="TaskCanceledException" />
        public async Task<List<Track>> GetTracksById(IEnumerable<int> tracksIds, bool forceProxy, CancellationToken token)
        {
            int skip = 0;
            var fullTracks = new List<Track>();
            var client = forceProxy ? _httpManager.GetProxiedClient() : _httpManager.DefaultClient;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                string Ids = string.Join(',', tracksIds.Skip(skip).Take(TracksQueryLimit));
                if (string.IsNullOrEmpty(Ids))
                    break;

                skip += 50;

                string url = $"https://api-v2.soundcloud.com/tracks?ids={HttpUtility.UrlEncode(Ids)}&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

                using var response = await client.GetAsync(url, token);
                response.EnsureSuccessStatusCode();

                var list = await response.Content.ReadFromJsonAsync<List<Track>>(token);
                fullTracks.AddRange(list!);
            }

            return fullTracks;
        }

        public async Task<WaveformData?> GetTrackWaveform(string waveformUrl, CancellationToken token)
        {
            using var response = await _httpManager.DefaultClient.GetAsync(waveformUrl, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<WaveformData>(token);
        }

        internal async Task<User?> GetUserInfo(int userId, CancellationToken token)
        {
            using var response = await _httpManager.DefaultClient.GetAsync($"https://api-v2.soundcloud.com/users/{userId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<User>(token);
        }

        internal async Task<Playlist?> GetPlaylistInfo(int playlistId, CancellationToken token)
        {
            using var response = await _httpManager.DefaultClient.GetAsync($"https://api-v2.soundcloud.com/playlists/{playlistId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Playlist>(token);
        }

        /// <exception cref="TaskCanceledException" />
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedTracksIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await _httpManager.AuthorizedClient.GetAsync($"https://api-v2.soundcloud.com/me/track_likes/ids?limit=200&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
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
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedUsersIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await _httpManager.AuthorizedClient.GetAsync($"https://api-v2.soundcloud.com/me/user_likes/ids?limit=5000&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
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
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedPlaylistsIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                using var response = await _httpManager.AuthorizedClient.GetAsync($"https://api-v2.soundcloud.com/me/playlist_likes/ids?limit=5000&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
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
        internal async Task<(QueryResponse<HistoryTrack>? tracks, string? errorMessage)> GetPlayHistory(string? href, int offset, CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            try
            {
                if (string.IsNullOrEmpty(href))
                    href = $"https://api-v2.soundcloud.com/me/play-history/tracks?client_id={_settings.ClientId}&limit=25&offset={offset}&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
                else
                    href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

                using var response = await _httpManager.AuthorizedClient.GetAsync(href, token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<HistoryTrack>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed play history request: {ex.Message}");
            }
        }

        /// <summary>
        /// Grabs a much larger chunk of comments, but doesn't include the comments posted as replies
        /// </summary>
        public async Task<(QueryResponse<Comment>? comments, string? errorMessage)> GetTrackCommentsAhead(string? href, int trackId, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(href))
                {
                    href = $"tracks/{trackId}/comments?threaded=0&client_id={_settings.ClientId}&limit=200&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
                }
                else
                    href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

                var response = await _httpManager.DefaultClient.GetAsync(href, token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<Comment>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving non-threaded comments for track id {trackId}: {ex}");
            }
            catch (TaskCanceledException ex)
            {
                return (null, null);
            }
        }

        public async Task<(QueryResponse<Comment>? comments, string? errorMessage)> GetTrackComments(string? href, HashSet<long> nonThreadedComments, int trackId, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrEmpty(href))
                {
                    href = $"tracks/{trackId}/comments?sort=newest&threaded=1&client_id={_settings.ClientId}&limit=20&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
                }
                else
                    href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

                var response = await _httpManager.DefaultClient.GetAsync(href, token);
                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<Comment>>(token);

                //if the comment doesn't exist in the non-threaded comments, it's a reply comment
                c!.Collection.ForEach(x =>
                {
                    x.InThread = !nonThreadedComments.Contains(x.Id);
                });

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving threaded comments for track id {trackId}: {ex}");
            }
            catch (TaskCanceledException ex)
            {
                return (null, null);
            }
        }
    }
}