using System.Net.Http;
using System.Threading.Tasks;

namespace SoundMist.Helpers
{
    public static class SoundCloudCommands
    {
        public static async Task<(bool success, string message)> ToggleLikedDisliked(bool liked, AuthorizedHttpClient httpClient, int trackId, int userId, string clientId, int appVersion)
        {
            if (!httpClient.IsAuthorized)
                return (false, "User not logged-in");

            try
            {
                HttpResponseMessage response;
                if (liked)
                {
                    //using var message = new HttpRequestMessage(HttpMethod.Options, $"https://api-v2.soundcloud.com/users/{userId}?client_id={clientId}&app_version={appVersion}&app_locale=en");
                    //using var opt = await httpClient.SendAsync(message);
                    //using var mee = await httpClient.PostAsync("https://api-v2.soundcloud.com/me?client_id=0hzUVOl6v8aC6mJ7TmWjHRlD0Zp6Gf8a");
                    response = await httpClient.PutAsync($"https://api-v2.soundcloud.com/users/{userId}/track_likes/{trackId}?client_id={clientId}&app_version={appVersion}&app_locale=en", null);
                }
                else
                    response = await httpClient.DeleteAsync($"https://api-v2.soundcloud.com/users/{userId}/track_likes/{trackId}?client_id={clientId}&app_version={appVersion}&app_locale=en");

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