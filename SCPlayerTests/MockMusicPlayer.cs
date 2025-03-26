using SoundMist.Models;

namespace SCPlayerTests
{
    internal class MockMusicPlayer : IMusicPlayer
    {
        public Track? CurrentTrack { get; }
        public float DesiredVolume { get; set; }
        public bool PlayerReady { get; }
        public bool Playing { get; }
        public TracksPlaylist TracksPlaylist { get; }
        public event Action<string>? ErrorCallback;
        public event Action<PlayState, string>? PlayStateUpdated;
        public event Action<Track>? TrackChanging;
        public event Action<Track>? TrackChanged;
        public event Action<double>? TrackTimeUpdated;

        public void SetPosition(double value)
        {
            throw new NotImplementedException();
        }

        public Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            throw new NotImplementedException();
        }

        public Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null)
        {
            throw new NotImplementedException();
        }

        public void ClearQueue()
        {
            throw new NotImplementedException();
        }

        public void ContinueWithAutoplay()
        {
            throw new NotImplementedException();
        }

        public Task LoadNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null,
            bool startPlaying = true)
        {
            throw new NotImplementedException();
        }

        public Task PlayNext()
        {
            throw new NotImplementedException();
        }

        public Task PlayPause(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task PlayPrev()
        {
            throw new NotImplementedException();
        }

        public Task SkipUser(int id)
        {
            throw new NotImplementedException();
        }

        public Task SkipTrack(int id)
        {
            throw new NotImplementedException();
        }
    }
}