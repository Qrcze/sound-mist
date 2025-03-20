using SoundMist;
using SoundMist.Models;
using SoundMist.ViewModels;
using RichardSzalay.MockHttp;
using System.Text.Json;

namespace SCPlayerTests
{
    public class UnitTests
    {
        private static readonly TrackCollection Tracks = JsonSerializer.Deserialize<TrackCollection>(File.ReadAllText("Resources/TracksCollection.json"))!;

        [Fact]
        public void MainViewCanOpenSettings()
        {
            var settings = new ProgramSettings();
            var vm = new MainViewModel(settings);
            var sv = new SettingsViewModel(settings);

            bool mediatorTriggered = false;
            Mediator.Default.Register(MediatorEvent.OpenSettings, _ => mediatorTriggered = true);

            Assert.False(sv.IsVisible, "SettingsViewModel shouldn't be visible on launch");

            vm.OpenSettings();

            Assert.True(mediatorTriggered, "Mediator event for opening settings never got triggered");
            Assert.True(sv.IsVisible, "Failed opening the SettingsViewModel from MainViewModel");
        }

        [Fact]
        public void LikedLibraryOpensTrackInfoPage()
        {
            //setup
            var settings = new ProgramSettings();
            settings.StartingTabIndex = MainViewTab.LikedTracks;

            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);

            var lv = new LikedLibraryViewModel(null, settings, musicPlayer, logger);
            lv.SelectedTrack = new Track() { Id = 5 };

            object? track = null;
            bool eventTriggered = false;
            Mediator.Default.Register(MediatorEvent.OpenTrackInfo, t => { track = t; eventTriggered = true; });

            //tests
            Assert.True(mv.OpenedTabIndex == settings.StartingTabIndex, "Main view doesn't set default tab specified in settings");

            lv.OpenTrackPage();

            Assert.True(eventTriggered, "Mediator event for opening track info never got triggered");
            Assert.True(track is Track, "Event triggered by mediator didn't send a Track object");
            Assert.True(mv.OpenedTabIndex == MainViewTab.TrackInfo, "Failed opening the track info tab");
        }

        [Fact]
        public void LikedLibraryOpensUserInfoPage()
        {
            //setup
            var settings = new ProgramSettings();
            settings.StartingTabIndex = MainViewTab.LikedTracks;

            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);

            var lv = new LikedLibraryViewModel(null, settings, musicPlayer, logger);
            lv.SelectedTrack = new Track() { Id = 1, User = new User() { Id = 5 } };

            object? user = null;
            bool eventTriggered = false;
            Mediator.Default.Register(MediatorEvent.OpenUserInfo, u => { user = u; eventTriggered = true; });

            //tests
            Assert.True(mv.OpenedTabIndex == settings.StartingTabIndex, "Main view doesn't set default tab specified in settings");

            lv.OpenUserPage();

            Assert.True(eventTriggered, "Mediator event for opening user info never got triggered");
            Assert.True(user is User, "Event triggered by mediator didn't send a User object");
            Assert.True(mv.OpenedTabIndex == MainViewTab.UserInfo, "Failed opening the user info tab");
        }

        [Fact]
        public void OpeningTrackInfoGetsHandled()
        {
            //setup
            var settings = new ProgramSettings { StartingTabIndex = MainViewTab.LikedTracks };
            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);
            var tv = new TrackInfoViewModel(null, musicPlayer, logger);

            var track = new Track() { Id = 5 };

            //tests
            Assert.True(mv.OpenedTabIndex == settings.StartingTabIndex, "Main view doesn't set default tab specified in settings");

            Mediator.Default.Invoke(MediatorEvent.OpenTrackInfo, track);

            Assert.True(mv.OpenedTabIndex == MainViewTab.TrackInfo, "Failed opening the track info tab");
            Assert.True(tv.Track is not null, "Track info still has null Track property");
            Assert.True(tv.Track == track, "Track info tab displays incorrect track");
        }

        [Fact]
        public void OpeningUserInfoGetsHandled()
        {
            //setup
            var settings = new ProgramSettings { StartingTabIndex = MainViewTab.LikedTracks };
            //var musicPlayer = new MockMusicPlayer();
            //var logger = new DummyLogger();

            var mv = new MainViewModel(settings);

            //it is now grabbing extra data from an http query, so test is not fully valid here
            //var uv = new UserInfoViewModel(null, settings, logger);

            var user = new User() { Id = 5 };

            //tests
            Assert.True(mv.OpenedTabIndex == settings.StartingTabIndex, "Main view doesn't set default tab specified in settings");

            Mediator.Default.Invoke(MediatorEvent.OpenUserInfo, user);

            Assert.True(mv.OpenedTabIndex == MainViewTab.UserInfo, "Failed opening the user info tab");

            //Assert.True(uv.User is not null, "User info still has null User property");
            //Assert.True(uv.User == user, "User info tab displays incorrect user");
        }

        [Fact]
        public async Task MusicPlayer_Queue_And_Autoplay_Works()
        {
            var mockHttp = new MockHttpMessageHandler();

            mockHttp.When("https://api-v2.soundcloud.com/media/*")
                .Respond("application/json", """
                { "url": "https://cf-hls-media.sndcdn.com/playlist/link" }
                """);

            mockHttp.When("https://cf-hls-media.sndcdn.com/playlist/link")
                .Respond("application/text", """
                #EXTM3U
                #EXT-X-VERSION:6
                #EXT-X-PLAYLIST-TYPE:VOD
                #EXT-X-TARGETDURATION:10
                #EXT-X-MEDIA-SEQUENCE:0
                #EXTINF:1.985272,
                https://cf-hls-media.sndcdn.com/media/chunk/1/f4GUt0zDOLFI.128.mp3?Policy=xxx&Signature=zzz
                #EXTINF:2.977908,
                https://cf-hls-media.sndcdn.com/media/chunk/2/f4GUt0zDOLFI.128.mp3?Policy=xxx&Signature=zzz
                #EXTINF:4.989302,
                https://cf-hls-media.sndcdn.com/media/chunk/3/f4GUt0zDOLFI.128.mp3?Policy=xxx&Signature=zzz
                #EXT-X-ENDLIST
                """);

            using var fs = File.OpenRead("Resources/music.mp3");

            mockHttp.When("https://cf-hls-media.sndcdn.com/media/chunk/*")
                .Respond("audio/mpeg", fs);

            mockHttp.When("https://cf-hls-media.sndcdn.com/media/")
                .Respond("audio/mpeg", fs);

            mockHttp.When("https://api-v2.soundcloud.com/tracks/*")
                .Respond("application/json", File.ReadAllText("Resources/TracksCollection.json"));

            var client = mockHttp.ToHttpClient();
            client.BaseAddress = new("https://api-v2.soundcloud.com/");

            var mockTrack1 = Tracks.Collection[0];
            var mockTrack2 = Tracks.Collection[1];
            var mockTrack3 = Tracks.Collection[2];
            Assert.False(mockTrack1 is null);
            Assert.False(mockTrack2 is null);

            ProgramSettings settings = new();
            var logger = new DummyLogger();

            MusicPlayer mp = new(client, settings, logger);
            mp.PlayStateChanged += (s) =>
            {
                Assert.False(s == PlayState.Error, $"Player triggered an error. Last message: {logger.LastMessage}");
            };

            await mp.AddToQueue(mockTrack1);
            Assert.True(mp.CurrentTrack == null, "Queue shouldn't have changed the track from simply adding to it");

            await mp.AddToQueue(mockTrack2, null, true);
            Assert.True(mp.CurrentTrack == mockTrack1, "Player didn't change changed track after appending to the queue with preload on");
            await mp.AddToQueue(mockTrack3);

            Assert.True(mp.CurrentTrack == mockTrack1, "Player changed track after appending to the queue");
            Assert.True(mp.TracksPlaylist.Count == 3, $"Queue has incorrect size: {mp.TracksPlaylist.Count} (should be 3)");

            await mp.PlayNext();
            Assert.True(mp.CurrentTrack == mockTrack2, "Track didn't change to the second track");
            Assert.True(mp.TracksPlaylist.Count == 3, $"Queue unexpectedly increased after playing next: {mp.TracksPlaylist.Count} (should be 3)");

            await mp.PlayNext();
            Assert.True(mp.CurrentTrack == mockTrack3, "Track didn't change to the third track");
            Assert.True(mp.TracksPlaylist.Count == 3, $"Queue unexpectedly increased after playing next: {mp.TracksPlaylist.Count} (should be 3)");

            await mp.PlayNext();
            Assert.True(mp.CurrentTrack.Id == mockTrack1.Id, "The next in queue was not correct after autoplay download, it should have been the first track");
            Assert.True(mp.TracksPlaylist.Count == 3 + Tracks.Collection.Count, $"Queue got to unpredicted size: {mp.TracksPlaylist.Count} (should be {3 + Tracks.Collection.Count})");
        }
    }
}