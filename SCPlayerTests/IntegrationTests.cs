using SoundMist;
using SoundMist.Helpers;
using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;
using SoundMist.ViewModels;

namespace SCPlayerTests
{
    public class SoundcloudConnections
    {
        public IDatabase Database { get; } = new DummyDatabase();
        public ILogger Logger { get; } = new DummyLogger();
        public ProgramSettings Settings { get; } = new();
        public HttpManager HttpManager { get; }
        public IMusicPlayer MusicPlayer { get; } = new MockMusicPlayer();
        public SoundCloudQueries Queries { get; }
        public SoundCloudDownloader Downloader { get; }

        public SoundcloudConnections()
        {
            HttpManager = new HttpManager(Settings);
            Queries = new SoundCloudQueries(HttpManager, Settings);
            Downloader = new SoundCloudDownloader(HttpManager, Settings, Queries);
            var initializer = new SoundcloudDataInitializer(Settings, HttpManager, Queries, Logger, null!);

            Settings.AppVersion = initializer.GetAppVersion().Result;
            (Settings.ClientId, Settings.AnonymousUserId) = initializer.GetClientAndAnonymousUserIds().Result;
        }
    }

    public class IntegrationTests(SoundcloudConnections connections) : IClassFixture<SoundcloudConnections>
    {
        SoundcloudConnections Connections { get; } = connections;

        [Fact]
        public void ClientIdIsValid()
        {
            Assert.False(string.IsNullOrEmpty(Connections.Settings.ClientId), "Failed getting client id");
        }

        [Fact]
        public void AnonymousUserIdIsValid()
        {
            Assert.False(string.IsNullOrEmpty(Connections.Settings.AnonymousUserId), "Failed getting anonymous user id");
        }

        [Fact]
        public async Task Search_All()
        {
            //all of the queries are going to be moved to a separate static helper class with querioes only
            var vm = new SearchViewModel(Connections.HttpManager, Connections.Queries, Connections.Database, Connections.Settings, Connections.MusicPlayer, Connections.Logger);
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
            var vm = new SearchViewModel(Connections.HttpManager, Connections.Queries, Connections.Database, Connections.Settings, Connections.MusicPlayer, Connections.Logger);
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
            var vm = new SearchViewModel(Connections.HttpManager, Connections.Queries, Connections.Database, Connections.Settings, Connections.MusicPlayer, Connections.Logger);
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
            var vm = new SearchViewModel(Connections.HttpManager, Connections.Queries, Connections.Database, Connections.Settings, Connections.MusicPlayer, Connections.Logger);
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
            var tracks = await Connections.Queries.GetTracksById([2, 17]);

            Assert.True(tracks.Count == 2, $"Failed downloading all of the tracks; got: {tracks.Count}");

            Assert.True(tracks[0].Title == "Electro 1", $"First track has unexpected title: {tracks[0].Title} (expected: \"Electro 1\")");
            Assert.True(tracks[1].Title == "Chaos", $"Second track has unexpected title: {tracks[1].Title} (expected: \"Chaos\")");
        }

        [Fact]
        public async Task Get_Waveform()
        {
            var tracks = await Connections.Queries.GetTracksById([2]);
            Assert.True(tracks.Count == 1, "Tracks download malfunction");

            (var wave, var error) = await Connections.Queries.GetTrackWaveform(tracks[0].WaveformUrl!, CancellationToken.None);

            Assert.True(wave != null, $"Retrieved waveform is null: {error}");
        }

        [Fact]
        public async Task Get_Comments()
        {
            var (response, error) = await Connections.Queries.GetTrackComments(null, [], 2, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error));
            Assert.True(response != null && response.Collection.Count > 0);
            Assert.False(string.IsNullOrEmpty(response.NextHref));

            var (response2, error2) = await Connections.Queries.GetTrackComments(response.NextHref, [], 2, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error2));
            Assert.True(response2 != null && response2.Collection.Count > 0);
            Assert.True(response.Collection[0].Id != response2.Collection[0].Id, "Comments NextHref returned the same set of comments");
            Assert.False(string.IsNullOrEmpty(response2.NextHref));
        }

        [Fact]
        public async Task Get_Autoplay()
        {
            var track = (await Connections.Queries.GetTracksById([2])).Single();
            var response = await Connections.Downloader.GetRelatedTracks(track);

            Assert.True(response.tracks != null, $"failed getting related tracks: {response.error}");
            Assert.NotEmpty(response.tracks.Collection);
        }
    }
}