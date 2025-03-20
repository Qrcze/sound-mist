using CommunityToolkit.Mvvm.ComponentModel;
using SoundMist.Helpers;
using SoundMist.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Web;

namespace SoundMist.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    [ObservableProperty] private string _searchFilter = string.Empty;
    [ObservableProperty] private string _selectedFilter = "All";
    [ObservableProperty] private bool _showQueryResults;
    [ObservableProperty] private object? _selectedItem;
    [ObservableProperty] private string _resultsMessage;

    private string? _nextHref;

    public string[] Filters { get; } = ["All", "Tracks", "People", "Albums"];

    /// <summary> The full list of results when finalized search </summary>
    public ObservableCollection<object> SearchResults { get; } = [];

    /// <summary> The popup that shows when typing in the search filter </summary>
    public ObservableCollection<SearchQuery> QueryResults { get; } = [];

    private readonly Timer _querySearchDelay;
    private readonly HttpClient _httpClient;
    private readonly ProgramSettings _settings;
    private readonly IMusicPlayer _musicPlayer;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _convertJsonScObjects = new() { Converters = { new ScObjectConverter() } };

    public SearchViewModel(HttpClient httpClient, ProgramSettings settings, IMusicPlayer musicPlayer, ILogger logger)
    {
        _querySearchDelay = new(800) { AutoReset = false };
        _querySearchDelay.Elapsed += GetSearchResults;

        _httpClient = httpClient;
        _settings = settings;
        _musicPlayer = musicPlayer;
        _logger = logger;
    }

    partial void OnSelectedFilterChanged(string? oldValue, string newValue)
    {
        _nextHref = null;
        Task.Run(() => RunSearch());
    }

    partial void OnSearchFilterChanged(string? oldValue, string newValue)
    {
        _querySearchDelay.Stop();
        _querySearchDelay.Start();
    }

    private void GetSearchResults(object? sender, ElapsedEventArgs e)
    {
        Debug.Print("getting search results");
        Task.Run(GetSearchQuery);
    }

    async Task GetSearchQuery()
    {
        QueryResults.Clear();
        ShowQueryResults = false;
        if (string.IsNullOrEmpty(SearchFilter))
            return;

        string url = $"https://api-v2.soundcloud.com/search/queries?q={SearchFilter}&client_id={_settings.ClientId}&limit=10&offset=0&linked_partitioning=1&app_version={_settings.AppVersion}&app_locale=en";

        SearchQueryCollection? q;
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            q = await response.Content.ReadFromJsonAsync<SearchQueryCollection>();
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Failed getting the response for search query: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unhandled exception while getting search query: {ex.Message}");
            throw;
        }

        if (q is null)
            return;

        Debug.Print($"retrieved {q.Collection.Count} items from query");
        foreach (var query in q.Collection)
        {
            QueryResults.Add(query);
        }
        ShowQueryResults = true;
    }

    internal async Task RunSearch(bool newSearch = true)
    {
        _querySearchDelay.Stop();
        ShowQueryResults = false;
        if (string.IsNullOrEmpty(SearchFilter))
            return;

        if (newSearch)
        {
            ResultsMessage = string.Empty;
            SearchResults.Clear();
        }

        SearchCollection? result;
        try
        {
            result = await GetSearchResults(newSearch);
        }
        catch (HttpRequestException ex)
        {
            string message = $"Failed getting the response for search query: {ex.Message}";
            SearchResults.Add(message);
            _logger.Error(message);
            return;
        }
        catch (Exception ex)
        {
            string message = $"Unhandled exception while getting search query: {ex.Message}";
            SearchResults.Add(message);
            _logger.Error(message);
            return;
        }

        if (result != null && result.Collection.Count > 0)
        {
            ResultsMessage = $"Found: {result.TotalResults} results.";
            foreach (var item in result.Collection)
                SearchResults.Add(item);
        }
        else
        {
            ResultsMessage = "0 results found.";
        }
    }

    public async Task<SearchCollection?> GetSearchResults(bool newSearch = true)
    {
        string? url;

        if (!newSearch)
            url = _nextHref;
        else
        {
            url = SelectedFilter switch
            {
                "All" => "search?facet=model",
                "Tracks" => "search/tracks?facet=genre",
                "People" => "search/users?facet=place",
                "Albums" => "search/albums?faced=genre",
                "Playlists" => "playlists_without_albums?facet=genre",
                _ => throw new UnreachableException($"Tried searching with an unknown filter: {SelectedFilter}"),
            };

            url = $"{url}&q={HttpUtility.UrlEncode(SearchFilter)}&limit=20&offset=0&linked_partitioning=1";
        }

        if (string.IsNullOrEmpty(url))
            return null;

        url += $"&client_id={_settings.ClientId}&app_version={_settings.AppVersion}&app_locale=en";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchCollection>(_convertJsonScObjects);

        _nextHref = result?.NextHref;

        return result;
    }

    public void OpenAboutPage(object? specificItem = null)
    {
        var item = specificItem ?? SelectedItem;
        if (item is null)
            return;

        if (item is User user)
            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);
        else if (item is Track track)
            Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, track);
        else if (item is Playlist playlist)
            Mediator.Default.Invoke(MediatorEvent.OpenPlaylistInfo, playlist);
    }

    internal async Task RunSelectedItem()
    {
        if (SelectedItem is null)
            return;

        if (SelectedItem is User user)
            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);
        else if (SelectedItem is Track track)
            await _musicPlayer.PlayNewQueue([track]);
        else if (SelectedItem is Playlist playlist)
            await PlayFromPlaylist(playlist, 0);
    }

    internal async Task PlayFromPlaylist(Playlist playlist, int selectedIndex)
    {
        IEnumerable<Track> tracks = playlist.FirstFiveTracks;
        if (playlist.Tracks.Count > 5)
        {
            try
            {
                var trackIds = playlist.Tracks.Except(tracks).Select(x => x.Id);
                var restOfTracks = await SoundCloudQueries.DownloadTracksDataById(_httpClient, _settings, trackIds);
                if (restOfTracks != null && restOfTracks.Count != 0)
                    tracks = tracks.Concat(restOfTracks);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed retrieving the tracks from IDs, {ex.Message}");
            }
        }

        if (tracks.Any())
            await _musicPlayer.PlayNewQueue(tracks.Skip(selectedIndex));
    }

    public class ScObjectConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            string raw = root.GetRawText();
            if (root.TryGetProperty("kind", out JsonElement typeProperty))
            {
                string type = typeProperty.GetString()!;
                return type switch
                {
                    "track" => JsonSerializer.Deserialize<Track>(root.GetRawText(), options),
                    "user" => JsonSerializer.Deserialize<User>(root.GetRawText(), options),
                    "playlist" => JsonSerializer.Deserialize<Playlist>(root.GetRawText(), options),
                    _ => $"unhandled type: {type}...",
                };
            }
            else
            {
                throw new JsonException($"Missing 'kind' property while reading json object: {raw}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(value, value.GetType(), options);
        }
    }

    public class SearchCollection
    {
        [JsonPropertyName("collection")]
        public List<object> Collection { get; set; } = [];

        [JsonPropertyName("next_href")]
        public string? NextHref { get; set; } = null!;

        [JsonPropertyName("query_urn")]
        public string QueryUrn { get; set; } = null!;

        [JsonPropertyName("total_results")]
        public int TotalResults { get; set; }

        [JsonPropertyName("facets")]
        public List<Facet> Facets { get; set; } = [];
    }

    public class Facet
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("facets")]
        public List<Facet> Facets { get; set; } = [];

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("filter")]
        public string Filter { get; set; } = string.Empty;
    }

    private class SearchQueryCollection
    {
        [JsonPropertyName("collection")]
        public List<SearchQuery> Collection { get; set; } = [];

        [JsonPropertyName("next_href")]
        public string? NextHref { get; set; } = null!;

        [JsonPropertyName("query_urn")]
        public string QueryUrn { get; set; } = null!;
    }

    public class SearchQuery
    {
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
    }
}