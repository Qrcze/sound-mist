using SoundMist;
using SoundMist.Models;
using SoundMist.ViewModels;
using RichardSzalay.MockHttp;
using System.Text.Json;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;

namespace SCPlayerTests
{
    public class UnitTests
    {
        private static readonly QueryResponse<Track> Tracks = JsonSerializer.Deserialize<QueryResponse<Track>>(File.ReadAllText("Resources/TracksCollection.json"))!;

        [Fact]
        public void MainViewCanOpenSettings()
        {
            var settings = new ProgramSettings();
            var vm = new MainViewModel(settings);
            var sv = new SettingsViewModel(null!, settings);

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
            var database = new DummyDatabase();
            settings.StartingTabIndex = MainViewTab.LikedTracks;

            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);

            var lv = new LikedLibraryViewModel(null, settings, database, musicPlayer, logger);
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
            var database = new DummyDatabase();
            settings.StartingTabIndex = MainViewTab.LikedTracks;

            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);

            var lv = new LikedLibraryViewModel(null!, settings, database, musicPlayer, logger);
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
            var history = new History(settings);
            var musicPlayer = new MockMusicPlayer();
            var logger = new DummyLogger();

            var mv = new MainViewModel(settings);
            var tv = new TrackInfoViewModel(null!, settings, musicPlayer, logger, history);

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
    }
}