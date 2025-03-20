using SoundMist;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.ViewModels;

namespace SCPlayerTests
{
    public class IntegrationTests
    {
        private static readonly HttpClient _httpClient = new() { BaseAddress = new(Globals.SoundCloudBaseUrl) };
        private static readonly ILogger _logger = new DummyLogger();
        private static readonly ProgramSettings _settings = new();
        private static readonly IMusicPlayer _musicPlayer = new MockMusicPlayer();

        public IntegrationTests()
        {
            var initializer = new SoundcloudDataInitializer(_settings, _httpClient, _logger, null!);

            _settings.AppVersion = initializer.GetAppVersion().Result;
            (_settings.ClientId, _settings.AnonymousUserId) = initializer.GetClientAndAnonymousUserIds().Result;
        }

        [Fact]
        public void ClientIdIsValid()
        {
            Assert.False(string.IsNullOrEmpty(_settings.ClientId), "Failed getting client id");
        }

        [Fact]
        public void AnonymousUserIdIsValid()
        {
            Assert.False(string.IsNullOrEmpty(_settings.AnonymousUserId), "Failed getting anonymous user id");
        }

        [Fact]
        public async Task Search_All()
        {
            //all of the queries are going to be moved to a separate static helper class with querioes only
            var vm = new SearchViewModel(_httpClient, _settings, _musicPlayer, _logger);
            vm.SelectedFilter = "All";
            vm.SearchFilter = "Love";
            var results = await vm.GetSearchResults();

            Assert.NotNull(results);

            //technically not 100% guaranteed, but with a filter that vague, it's pretty much certain
            Assert.True(results.Collection.Count > 0, $"search query for tracks with the word \"{vm.SearchFilter}\" is empty");
        }

        [Fact]
        public async Task Search_Tracks()
        {
            var vm = new SearchViewModel(_httpClient, _settings, _musicPlayer, _logger);
            vm.SelectedFilter = "Tracks";
            vm.SearchFilter = "Love";
            var results = await vm.GetSearchResults();

            Assert.NotNull(results);

            //technically not 100% guaranteed, but with a filter that vague, it's pretty much certain
            Assert.True(results.Collection.Count > 0, $"search query for tracks with the word \"{vm.SearchFilter}\" is empty");
            Assert.True(results.Collection.All(x => x is Track), "Not all search results for tracks are actually of type Track");
        }

        [Fact]
        public async Task Search_People()
        {
            var vm = new SearchViewModel(_httpClient, _settings, _musicPlayer, _logger);
            vm.SelectedFilter = "People";
            vm.SearchFilter = "Love";
            var results = await vm.GetSearchResults();

            Assert.NotNull(results);

            //technically not 100% guaranteed, but with a filter that vague, it's pretty much certain
            Assert.True(results.Collection.Count > 0, $"search query for users with the word \"{vm.SearchFilter}\" is empty");
            Assert.True(results.Collection.All(x => x is User), "Not all search results for tracks are actually of type User");
        }

        [Fact]
        public async Task Search_Albums()
        {
            var vm = new SearchViewModel(_httpClient, _settings, _musicPlayer, _logger);
            vm.SelectedFilter = "Albums";
            vm.SearchFilter = "Love";
            var results = await vm.GetSearchResults();

            Assert.NotNull(results);

            //technically not 100% guaranteed, but with a filter that vague, it's pretty much certain
            Assert.True(results.Collection.Count > 0, $"search query for albums with the word \"{vm.SearchFilter}\" is empty");
            Assert.True(results.Collection.All(x => x is Playlist), "Not all search results for albums are actually of type Album");
        }

        [Fact]
        public async Task Get_TracksById()
        {
            //tracks with id 2 and 17 are the oldest available tracks i could find, rather unlikely they'll get deleted lol
            var tracks = await SoundCloudQueries.DownloadTracksDataById(_httpClient, _settings, [2, 17]);

            Assert.True(tracks.Count == 2, $"Failed downloading all of the tracks; got: {tracks.Count}");

            Assert.True(tracks[0].Title == "Electro 1", $"First track has unexpected title: {tracks[0].Title} (expected: \"Electro 1\")");
            Assert.True(tracks[1].Title == "Chaos", $"Second track has unexpected title: {tracks[1].Title} (expected: \"Chaos\")");
        }

        [Fact]
        public async Task Get_Waveform()
        {
            var tracks = await SoundCloudQueries.DownloadTracksDataById(_httpClient, _settings, [2]);
            Assert.True(tracks.Count == 1, "Tracks download malfunction");

            var wave = await SoundCloudQueries.GetTrackWaveform(_httpClient, tracks[0].WaveformUrl!);

            Assert.True(wave is not null, "Retrieved waveform is null");
        }
    }
}