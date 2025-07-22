using SoundMist.Models;
using System;
using System.Net;
using System.Net.Http;

namespace SoundMist;

public class HttpManager : IHttpManager
{
    private readonly ProgramSettings _programSettings;

    private HttpClient _httpClient;
    private HttpClient _proxiedClient;
    private WebProxy _webProxy;

    private WebProxy _authorizedProxy;
    private AuthorizedHttpClient _authorizedClient;
    private AuthorizedHttpClient _proxiedAuthorizedClient;

    public HttpManager(ProgramSettings settings)
    {
        _programSettings = settings;

        _httpClient = new HttpClient() { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };
        _authorizedClient = new AuthorizedHttpClient() { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };

        _webProxy = new WebProxy();
        var handler = new HttpClientHandler() { Proxy = _webProxy, AutomaticDecompression = DecompressionMethods.All };
        _proxiedClient = new HttpClient(handler) { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };

        _authorizedProxy = new WebProxy();
        var authorizedHandler = new HttpClientHandler() { Proxy = _webProxy, AutomaticDecompression = DecompressionMethods.All };
        _proxiedAuthorizedClient = new AuthorizedHttpClient(authorizedHandler) { BaseAddress = new Uri(Globals.SoundCloudBaseUrl) };
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