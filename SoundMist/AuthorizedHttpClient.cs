using System.Net.Http;
using System.Net.Http.Headers;

namespace SoundMist;

public class AuthorizedHttpClient : HttpClient
{
    public bool IsAuthorized => DefaultRequestHeaders.Authorization is not null;

    public AuthenticationHeaderValue? Authorization
    {
        get => DefaultRequestHeaders.Authorization;
        set => DefaultRequestHeaders.Authorization = value;
    }
}