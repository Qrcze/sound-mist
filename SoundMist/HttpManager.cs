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

    private WebProxy _authorizedProxy;
    private AuthorizedHttpClient _authorizedClient;
    private AuthorizedHttpClient _proxiedAuthorizedClient;

    public HttpManager(ProgramSettings settings)
    {
        _programSettings = settings;

        _webProxy = new WebProxy();
        _authorizedProxy = new WebProxy();

        var handler = new HttpClientHandler() { Proxy = _webProxy, AutomaticDecompression = DecompressionMethods.All };
        var authorizedHandler = new HttpClientHandler() { Proxy = _webProxy, AutomaticDecompression = DecompressionMethods.All };

        _httpClient = new HttpClient();
        _authorizedClient = new AuthorizedHttpClient();
        _proxiedClient = new HttpClient(handler);
        _proxiedAuthorizedClient = new AuthorizedHttpClient(authorizedHandler);

        ConfigureHttpClient(_httpClient);
        ConfigureHttpClient(_authorizedClient);
        ConfigureHttpClient(_proxiedClient);
        ConfigureHttpClient(_proxiedAuthorizedClient);
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
}