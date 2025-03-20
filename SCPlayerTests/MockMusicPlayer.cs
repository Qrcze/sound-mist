using SoundMist.Models;

namespace SCPlayerTests
{
    internal class MockMusicPlayer : IMusicPlayer
    {
        public Track? CurrentTrack { get; set; }

        public float DesiredVolume { get; set; } = 1;

        public bool PlayerReady { get; set; }
        public bool Playing => throw new NotImplementedException();

        public TracksPlaylist TracksPlaylist { get; } = new();

        public event Action<string>? LoadingStatusChanged;

        public event Action<PlayState>? PlayStateChanged;

        public event Action<Track>? TrackChanged;

        public event Action<Track>? TrackChanging;

        public event Action<double>? TrackTimeUpdated;

        public Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false)
        {
            TracksPlaylist.AddRange(tracks);
            return Task.CompletedTask;
        }

        public Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false)
        {
            return Task.CompletedTask;
        }

        public void ClearQueue()
        {
            TracksPlaylist.Clear();
        }

        public void ContinueWithAutoplay()
        {
        }

        public Task SaveTrackLocally(Track track)
        {
            return Task.CompletedTask;
        }

        public Task PlayPause(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public Task PlayNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            TracksPlaylist.Clear();
            TracksPlaylist.AddRange(tracks);

            return Task.CompletedTask;
        }

        public Task PlayNext()
        {
            TracksPlaylist.TryMoveForward(out var _);
            return Task.CompletedTask;
        }

        public Task PlayPrev()
        {
            TracksPlaylist.TryMoveBack(out var _);
            return Task.CompletedTask;
        }

        public void SetPosition(double value)
        {
        }

        public void ShufflePlaylist(bool value)
        {
            TracksPlaylist.ChangeShuffle(value);
        }

        public Task SkipTrack(int id)
        {
            throw new NotImplementedException();
        }

        public Task SkipUser(int id)
        {
            throw new NotImplementedException();
        }

    }
}