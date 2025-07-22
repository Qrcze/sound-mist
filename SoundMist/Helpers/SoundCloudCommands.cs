using SoundMist.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace SoundMist.Helpers
{
    public class SoundCloudCommands(IHttpManager httpManager, ProgramSettings settings)
    {
        private readonly IHttpManager _httpManager = httpManager;
        private readonly ProgramSettings _settings = settings;

        public async Task<(bool success, string message)> ToggleLikedDisliked(bool liked, int trackId)
        {
            if (!_httpManager.AuthorizedClient.IsAuthorized)
                return (false, "User not logged-in");

            try
            {
                HttpResponseMessage response;
                if (liked)
                {
                    //using var message = new HttpRequestMessage(HttpMethod.Options, $"https://api-v2.soundcloud.com/users/{userId}?client_id={clientId}&app_version={appVersion}&app_locale=en");
                    //using var opt = await httpClient.SendAsync(message);
                    //using var mee = await httpClient.PostAsync("https://api-v2.soundcloud.com/me?client_id=0hzUVOl6v8aC6mJ7TmWjHRlD0Zp6Gf8a");
                    response = await _httpManager.AuthorizedClient.PutAsync($"https://api-v2.soundcloud.com/users/{_settings.UserId}/track_likes/{trackId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en", null);
                }
                else
                    response = await _httpManager.AuthorizedClient.DeleteAsync($"https://api-v2.soundcloud.com/users/{_settings.UserId}/track_likes/{trackId}?client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (true, $"Already {(liked ? "liked" : "removed")}");

                if (!response.IsSuccessStatusCode)
                {
                    var r = await response.Content.ReadAsStringAsync();
                }
                response.EnsureSuccessStatusCode();

                response.Dispose();

                return (true, "OK");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Failed sending out a like request: {ex.Message}");
            }
        }
    }
}