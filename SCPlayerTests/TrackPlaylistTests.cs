using SoundMist.Models;
using SoundMist.Models.SoundCloud;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xRetry;

namespace SCPlayerTests
{
    public class TrackPlaylistTests
    {
        [Fact]
        public void ListChangedCallback()
        {
            var playlist = new TracksPlaylist();

            bool added = false;
            bool removed = false;
            bool cleared = false;
            bool shuffled = false;

            playlist.ListChanged += (changeType, tracks) =>
            {
                switch (changeType)
                {
                    case TracksPlaylist.Changetype.Added:
                        added = true;
                        Assert.NotEmpty(tracks);
                        break;

                    case TracksPlaylist.Changetype.Removed:
                        removed = true;
                        Assert.NotEmpty(tracks);
                        break;

                    case TracksPlaylist.Changetype.Cleared:
                        cleared = true;
                        Assert.Empty(tracks);
                        break;

                    case TracksPlaylist.Changetype.Shuffled:
                        shuffled = true;
                        Assert.NotEmpty(tracks);
                        break;

                    default:
                        break;
                }
            };

            Assert.False(added);
            playlist.Add(new Track() { Id = 1 });
            Assert.True(added);

            Assert.False(removed);
            playlist.RemoveAll(x => x.Id == 1);
            Assert.True(removed);

            playlist.Add(new Track() { Id = 2 });
            playlist.Add(new Track() { Id = 3 });
            Assert.False(cleared);
            playlist.Clear();
            Assert.True(cleared);

            playlist.Add(new Track() { Id = 1 });
            playlist.Add(new Track() { Id = 2 });
            Assert.False(shuffled);
            playlist.ChangeShuffle(true);
            playlist.ChangeShuffle(false);
            Assert.True(shuffled);
        }

        [Fact]
        public void AddingToQueue()
        {
            var playlist = new TracksPlaylist();

            playlist.Add(new Track() { Id = 1 });
            playlist.Add(new Track() { Id = 2 });
            playlist.Add(new Track() { Id = 4 });

            Assert.Equal(3, playlist.Count);
            Assert.True(playlist.TryGetCurrent(out var currentTrack));
            Assert.True(currentTrack.Id == 1);
            Assert.Equal(4, playlist.GetLastTrack()?.Id);

            playlist.AddRange([new Track() { Id = 5 }, new Track() { Id = 6 }, new Track() { Id = 7 },]);
            Assert.Equal(6, playlist.Count);
            Assert.True(playlist.TryGetCurrent(out currentTrack));
            Assert.True(currentTrack.Id == 1);
            Assert.Equal(7, playlist.GetLastTrack()?.Id);
        }

        [Fact]
        public void TraversingQueue()
        {
            var playlist = new TracksPlaylist();
            playlist.AddRange([new Track() { Id = 1 }, new Track() { Id = 2 }, new Track() { Id = 3 },]);

            Track? currentTrack = null;

            Assert.True(playlist.TryGetCurrent(out currentTrack));
            Assert.Equal(1, currentTrack.Id);

            Assert.True(playlist.TryMoveForward(out currentTrack));
            Assert.True(playlist.TryMoveForward(out currentTrack));
            Assert.Equal(3, currentTrack.Id);

            Assert.True(playlist.TryMoveBack(out currentTrack));
            Assert.Equal(2, currentTrack.Id);

            var track = new Track() { Id = 4 };
            playlist.Add(track);

            Assert.True(playlist.TryMovePositionToTrack(track));
            Assert.True(playlist.TryGetCurrent(out currentTrack));
            Assert.Equal(4, currentTrack.Id);
        }

        //could need a retry because of randomness nature
        [RetryFact(5)]
        public void QueueShuffle()
        {
            var playlist = new TracksPlaylist();
            var firstTrack = new Track() { Id = 1 };
            playlist.AddRange([firstTrack, new Track() { Id = 2 }, new Track() { Id = 3 }, new Track() { Id = 4 },]);

            //sanity check for original queue
            int expectedId = 1;
            do
            {
                playlist.TryGetCurrent(out var track);
                Assert.Equal(expectedId, track.Id);
                expectedId++;
            } while (playlist.TryMoveForward(out _));

            playlist.TryMovePositionToTrack(firstTrack);

            //shuffle the list (because it's random, it's technically possible that the 
            playlist.ChangeShuffle(true);

            expectedId = 1;
            bool orderChanged = false;
            do
            {
                playlist.TryGetCurrent(out var track);
                if (track.Id != expectedId)
                {
                    orderChanged = true;
                    break;
                }
                expectedId++;
            } while (playlist.TryMoveForward(out _));

            Assert.True(orderChanged);

            //
            playlist.ChangeShuffle(false);
            playlist.TryMovePositionToTrack(firstTrack);

            expectedId = 1;
            do
            {
                playlist.TryGetCurrent(out var track);
                Assert.Equal(expectedId, track.Id);
                expectedId++;
            } while (playlist.TryMoveForward(out _));


        }
    }
}