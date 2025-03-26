using SoundMist.Models;
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
        private const int TracksQueryLimit = 50;

        public static async Task<List<Track>> DownloadTracksDataById(HttpClient httpClient, string clientId, int appVersion, IEnumerable<int> tracksIds)
        {
            int skip = 0;
            var fullTracks = new List<Track>();
            while (true)
            {
                string Ids = string.Join(',', tracksIds.Skip(skip).Take(TracksQueryLimit));
                if (string.IsNullOrEmpty(Ids))
                    break;

                skip += 50;

                string url = $"https://api-v2.soundcloud.com/tracks?ids={HttpUtility.UrlEncode(Ids)}&client_id={clientId}&app_version={appVersion}&app_locale=en";
                using var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var list = await response.Content.ReadFromJsonAsync<List<Track>>();
                fullTracks.AddRange(list!);
            }

            return fullTracks;
        }

        public static async Task<WaveformData?> GetTrackWaveform(HttpClient httpClient, string waveformUrl)
        {
            using var response = await httpClient.GetAsync(waveformUrl);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<WaveformData>();
        }

        internal static async Task<User?> GetUserInfo(HttpClient httpClient, ProgramSettings settings, int userId, CancellationToken token)
        {
            using var response = await httpClient.GetAsync($"https://api-v2.soundcloud.com/users/{userId}?client_id={settings.ClientId}&app_version={settings.AppVersion}&app_locale=en", token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<User>(token);
        }
    }
}