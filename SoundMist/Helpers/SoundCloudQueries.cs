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

        public async Task<(WaveformData? waveform, string? errorMessage)> GetTrackWaveform(string waveformUrl, CancellationToken token)
        {
            try
            {
                using var response = await _httpManager.DefaultClient.GetAsync(waveformUrl, token);
                response.EnsureSuccessStatusCode();

                return (await response.Content.ReadFromJsonAsync<WaveformData>(token), null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving waveform data: {ex.Message}");
            }
        }

        internal async Task<(User? user, string? errorMessage)> GetUserInfo(int userId, CancellationToken token)
        {
            try
            {
                using var response = await _httpManager.DefaultClient.GetAsync($"https://api-v2.soundcloud.com/users/{userId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
                response.EnsureSuccessStatusCode();

                return (await response.Content.ReadFromJsonAsync<User>(token), null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving user data: {ex.Message}");
            }
        }

        internal async Task<(Playlist? playlist, string? errorMessage)> GetPlaylistInfo(int playlistId, CancellationToken token)
        {
            try
            {
                using var response = await _httpManager.DefaultClient.GetAsync($"https://api-v2.soundcloud.com/playlists/{playlistId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", token);
                response.EnsureSuccessStatusCode();

                return (await response.Content.ReadFromJsonAsync<Playlist>(token), null);
            }
            catch (HttpRequestException ex)
            {
                return (null, $"Failed retrieving playlist data: {ex.Message}");
            }
        }

        /// <inheritdoc cref="SimpleGet{T}(HttpClient, string, string, int, CancellationToken)"/>
        async Task<(QueryResponse<T>? items, string? errorMessage)> SimpleGet<T>(HttpClient httpClient, string href, string errorMessageTemplate, CancellationToken token)
            => await SimpleGet<T>(httpClient, href, errorMessageTemplate, int.MinValue, token);

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="httpClient"></param>
        /// <param name="href"></param>
        /// <param name="errorMessageTemplate"> will be formatted, {0} - exception message, {1} additional parameter </param>
        /// <param name="extraParameter"> if equal <see cref="int.MinValue"/>; will be ignored </param>
        /// <param name="token"></param>
        /// <returns></returns>
        async Task<(QueryResponse<T>? items, string? errorMessage)> SimpleGet<T>(HttpClient httpClient, string href, string errorMessageTemplate, int extraParameter, CancellationToken token)
        {
            try
            {
                var response = await httpClient.GetAsync(href, token);

                response.EnsureSuccessStatusCode();

                var c = await response.Content.ReadFromJsonAsync<QueryResponse<T>>(token);

                return (c, null);
            }
            catch (HttpRequestException ex)
            {
                if (extraParameter == int.MinValue)
                    return (null, string.Format(errorMessageTemplate, ex.Message));
                return (null, string.Format(errorMessageTemplate, ex.Message, extraParameter));
            }
            catch (TaskCanceledException)
            {
                return (null, null);
            }
        }

        /// <exception cref="TaskCanceledException" />
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedTracksIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            string href = $"https://api-v2.soundcloud.com/me/track_likes/ids?limit=200&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

            return await SimpleGet<int>(_httpManager.AuthorizedClient, href, "Failed get liked tracks request: {0}", token);
        }

        /// <exception cref="TaskCanceledException" />
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedUsersIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            string href = $"https://api-v2.soundcloud.com/me/user_likes/ids?limit=5000&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";
            return await SimpleGet<int>(_httpManager.AuthorizedClient, href, "Failed get liked users request: {0}", token);
        }

        /// <exception cref="TaskCanceledException" />
        public async Task<(QueryResponse<int>? ids, string? errorMessage)> GetUsersLikedPlaylistsIds(CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            string href = $"https://api-v2.soundcloud.com/me/playlist_likes/ids?limit=5000&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";
            return await SimpleGet<int>(_httpManager.AuthorizedClient, href, "Failed get liked playlists request: {0}", token);
        }

        /// <exception cref="TaskCanceledException" />
        internal async Task<(QueryResponse<HistoryTrack>? tracks, string? errorMessage)> GetPlayHistory(string? href, int offset, CancellationToken token)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (null, "User not logged-in");

            if (string.IsNullOrEmpty(href))
                href = $"https://api-v2.soundcloud.com/me/play-history/tracks?client_id={_settings.ClientId}&limit=25&offset={offset}&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
            else
                href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

            return await SimpleGet<HistoryTrack>(_httpManager.AuthorizedClient, href, "Failed play history request: {0}", token);
        }

        /// <summary>
        /// Grabs a much larger chunk of comments, but doesn't include the comments posted as replies
        /// </summary>
        public async Task<(QueryResponse<Comment>? comments, string? errorMessage)> GetTrackCommentsAhead(string? href, int trackId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(href))
                href = $"tracks/{trackId}/comments?threaded=0&client_id={_settings.ClientId}&limit=200&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
            else
                href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

            return await SimpleGet<Comment>(_httpManager.DefaultClient, href, "Failed retrieving non-threaded comments for track id <{1}>: {0}", trackId, token);
        }

        /// <summary>
        /// Gets all of the comments, and will mark the comments as InThread if they're not int the <paramref name="nonThreadedComments"/>.
        /// </summary>
        /// <param name="href"></param>
        /// <param name="nonThreadedComments"></param>
        /// <param name="trackId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<(QueryResponse<Comment>? comments, string? errorMessage)> GetTrackComments(string? href, HashSet<long> nonThreadedComments, int trackId, CancellationToken token)
        {
            if (string.IsNullOrEmpty(href))
                href = $"tracks/{trackId}/comments?sort=newest&threaded=1&client_id={_settings.ClientId}&limit=20&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
            else
                href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

            var result = await SimpleGet<Comment>(_httpManager.DefaultClient, href, "Failed retrieving threaded comments for track id <{1}>: {0}", trackId, token);

            //if the comment doesn't exist in the non-threaded comments, it's a reply comment
            result.items?.Collection.ForEach(x => x.InThread = !nonThreadedComments.Contains(x.Id));

            return result;
        }

        public async Task<(QueryResponse<Track>? tracks, string? errorMessage)> GetUserTracks(int userId, string? href, CancellationToken token)
        {
            if (string.IsNullOrEmpty(href))
                href = $"https://api-v2.soundcloud.com/users/{userId}/tracks?representation=&client_id={_settings.ClientId}&limit=20&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";
            else
                href += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

            return await SimpleGet<Track>(_httpManager.DefaultClient, href, "Failed retrieving tracks from user <{1}>: {0}", userId, token);
        }
    }
}