using SoundMist.Models;
using System;
using System.Net;
using System.Net.Http;

namespace SoundMist;

public class HttpManager : IHttpManager
{
    private readonly ProgramSettings _programSettings;
    private readonly string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 OPR/124.0.0.0";

    private HttpClient _httpClient;
    private HttpClient _proxiedClient;
    private WebProxy _webProxy;
    private readonly CookieContainer _authorizedCookies = new();
    private readonly CookieContainer _proxiedAuthorizedCookies = new();

    private WebProxy _authorizedProxy;
    private AuthorizedHttpClient _authorizedClient;
    private AuthorizedHttpClient _proxiedAuthorizedClient;

    public HttpManager(ProgramSettings settings)
    {
        _programSettings = settings;

        _webProxy = new WebProxy();
        _authorizedProxy = new WebProxy();

        var handler = new HttpClientHandler() { Proxy = _webProxy, AutomaticDecompression = DecompressionMethods.All };
        var authorizedHandler = new HttpClientHandler()
        {
            Proxy = _webProxy,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
            CookieContainer = _authorizedCookies,
        };
        var proxiedAuthorizedHandler = new HttpClientHandler()
        {
            Proxy = _authorizedProxy,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
            CookieContainer = _proxiedAuthorizedCookies,
        };

        _httpClient = new HttpClient();
        _authorizedClient = new AuthorizedHttpClient(authorizedHandler);
        _proxiedClient = new HttpClient(handler);
        _proxiedAuthorizedClient = new AuthorizedHttpClient(proxiedAuthorizedHandler);

        ConfigureHttpClient(_httpClient);
        ConfigureHttpClient(_authorizedClient);
        ConfigureHttpClient(_proxiedClient);
        ConfigureHttpClient(_proxiedAuthorizedClient);

        SetDataDomeCookie(_programSettings.DataDomeToken);
    }

    void ConfigureHttpClient(HttpClient client)
    {
        client.BaseAddress = new Uri(Globals.SoundCloudBaseUrl);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
    }

    public HttpClient DefaultClient
    {
        get
        {
            if (_programSettings.ProxyMode == ViewModels.ProxyMode.Always
                && !string.IsNullOrEmpty(_programSettings.ProxyHost)
                && _programSettings.ProxyPort != 0)
            {
                _webProxy.Address = ProxyUri();
                return _proxiedClient;
            }
            else
            {
                return _httpClient;
            }
        }
    }

    public AuthorizedHttpClient AuthorizedClient
    {
        get
        {
            if (_programSettings.ProxyMode == ViewModels.ProxyMode.Always
                && !string.IsNullOrEmpty(_programSettings.ProxyHost)
                && _programSettings.ProxyPort != 0)
            {
                _authorizedProxy.Address = ProxyUri();
                return _proxiedAuthorizedClient;
            }
            else
            {
                return _authorizedClient;
            }
        }
    }

    Uri ProxyUri() => new Uri($"{_programSettings.ProxyProtocol}://{_programSettings.ProxyHost}:{_programSettings.ProxyPort}");

    public HttpClient GetProxiedClient()
    {
        _webProxy.Address = ProxyUri();
        return _proxiedClient;
    }

    public HttpClient GetClient()
    {
        return _httpClient;
    }

    public void SetDataDomeCookie(string? value)
    {
        SetDataDomeCookie(_authorizedCookies, value);
        SetDataDomeCookie(_proxiedAuthorizedCookies, value);
    }

    private static void SetDataDomeCookie(CookieContainer container, string? value)
    {
        var cookieUri = new Uri("https://api-v2.soundcloud.com/");

        // Replacing the same name/path/domain updates the existing clearance token.
        // An expired cookie removes a previously entered token when the field is cleared.
        var cookie = new Cookie("datadome", value ?? string.Empty, "/", ".soundcloud.com")
        {
            Secure = true,
            Expires = string.IsNullOrWhiteSpace(value) ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddYears(1),
        };
        container.Add(cookieUri, cookie);
    }
}
