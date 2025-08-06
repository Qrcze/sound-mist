using SoundMist.Models;

namespace SCPlayerTests
{
    public class HistoryTests
    {
        [Fact]
        public void HistoryKeepsLimit()
        {
            var settings = new ProgramSettings() { HistoryLimit = 10 };
            var history = new History(settings);

            for (int i = 0; i < 15; i++)
                history.AddPlayedHistory(new() { Id = i });

            Assert.True(history.PlayHistory.Count() == settings.HistoryLimit);
        }

        [Fact]
        public void HistoryItemsAreInCorrectOrder()
        {
            var settings = new ProgramSettings() { HistoryLimit = 3 };
            var history = new History(settings);

            history.AddPlayedHistory(new() { Id = 1 });
            history.AddPlayedHistory(new() { Id = 2 });
            history.AddPlayedHistory(new() { Id = 3 });
            history.AddPlayedHistory(new() { Id = 4 });

            var items = history.PlayHistory.ToList();
            Assert.Equal(4, items[0]);
            Assert.Equal(3, items[1]);
            Assert.Equal(2, items[2]);
        }

        [Fact]
        public void HistoryForwardsEventWithProperNewId()
        {
            var settings = new ProgramSettings() { HistoryLimit = 3 };
            var history = new History(settings);

            long? newId = null;

            history.HistoryChanged += (s, e) =>
            {
                newId = e.NewId;
            };

            history.AddPlayedHistory(new() { Id = 1 });
            Assert.Equal(1, newId);
            history.AddPlayedHistory(new() { Id = 2 });
            Assert.Equal(2, newId);
            history.AddPlayedHistory(new() { Id = 3 });
            Assert.Equal(3, newId);
            history.AddPlayedHistory(new() { Id = 4 });
            Assert.Equal(4, newId);

            history.AddTrackInfoHistory(new() { Id = 1 });
            Assert.Equal(1, newId);
            history.AddTrackInfoHistory(new() { Id = 2 });
            Assert.Equal(2, newId);
            history.AddTrackInfoHistory(new() { Id = 3 });
            Assert.Equal(3, newId);
            history.AddTrackInfoHistory(new() { Id = 4 });
            Assert.Equal(4, newId);

            history.AddUserInfoHistory(new() { Id = 1 });
            Assert.Equal(1, newId);
            history.AddUserInfoHistory(new() { Id = 2 });
            Assert.Equal(2, newId);
            history.AddUserInfoHistory(new() { Id = 3 });
            Assert.Equal(3, newId);
            history.AddUserInfoHistory(new() { Id = 4 });
            Assert.Equal(4, newId);

            history.AddPlaylistInfoHistory(new() { Id = 1 });
            Assert.Equal(1, newId);
            history.AddPlaylistInfoHistory(new() { Id = 2 });
            Assert.Equal(2, newId);
            history.AddPlaylistInfoHistory(new() { Id = 3 });
            Assert.Equal(3, newId);
            history.AddPlaylistInfoHistory(new() { Id = 4 });
            Assert.Equal(4, newId);
        }

        [Fact]
        public void HistoryPushesRepeatsToTheTop()
        {
            var settings = new ProgramSettings() { HistoryLimit = 10 };
            var history = new History(settings);

            history.AddPlayedHistory(new() { Id = 1 });
            history.AddPlayedHistory(new() { Id = 2 });
            history.AddPlayedHistory(new() { Id = 3 });
            history.AddPlayedHistory(new() { Id = 2 });

            List<long> items = history.PlayHistory.ToList();

            Assert.True(items.Count == 3, $"Playlist didn't have correct number of items: {items.Count}");

            Assert.Equal(2, items[0]);
            Assert.Equal(3, items[1]);
            Assert.Equal(1, items[2]);
        }

        [Fact]
        public void HistoryRemovesOldEntriesWhenExceedingLimit()
        {
            var settings = new ProgramSettings() { HistoryLimit = 3 };
            var history = new History(settings);

            long? removedId = null;

            history.HistoryChanged += (s, e) =>
            {
                removedId = e.RemovedId;
            };

            history.AddPlayedHistory(new() { Id = 1 });
            history.AddPlayedHistory(new() { Id = 2 });
            history.AddPlayedHistory(new() { Id = 3 });
            history.AddPlayedHistory(new() { Id = 4 });

            Assert.Equal(1, removedId);
        }

        [Fact]
        public void HistoryFiresEventsForEachListType()
        {
            var settings = new ProgramSettings() { HistoryLimit = 10 };
            var history = new History(settings);

            bool playedFired = false;
            bool tracksFired = false;
            bool usersFired = false;
            bool playlistsFired = false;

            history.HistoryChanged += (s, e) =>
            {
                switch (e.List)
                {
                    case History.List.PlayHistory:
                        playedFired = true;
                        break;

                    case History.List.TracksHistory:
                        tracksFired = true;
                        break;

                    case History.List.UsersHistory:
                        usersFired = true;
                        break;

                    case History.List.PlaylistsHistory:
                        playlistsFired = true;
                        break;

                    default:
                        Assert.Fail($"unexpected list type fired: {e.List}");
                        break;
                }
            };

            history.AddPlayedHistory(new() { Id = 10 });
            history.AddTrackInfoHistory(new() { Id = 11 });
            history.AddUserInfoHistory(new() { Id = 12 });
            history.AddPlaylistInfoHistory(new() { Id = 13 });

            Assert.True(playedFired, "played list didn't fire their respective event");
            Assert.True(tracksFired, "tracks list didn't fire their respective event");
            Assert.True(usersFired, "users list didn't fire their respective event");
            Assert.True(playlistsFired, "playlists list didn't fire their respective event");
        }
    }
}