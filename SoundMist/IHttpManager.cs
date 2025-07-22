using System.Net.Http;

namespace SoundMist
{
    public interface IHttpManager
    {
        AuthorizedHttpClient AuthorizedClient { get; }
        HttpClient DefaultClient { get; }

        HttpClient GetClient();
        HttpClient GetProxiedClient();
    }
}