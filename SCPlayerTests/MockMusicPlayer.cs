using SoundMist.Models;
using SoundMist.Models.Audio;
using SoundMist.Models.SoundCloud;

namespace SCPlayerTests
{
    internal class MockMusicPlayer : IMusicPlayer
    {
        public Track? CurrentTrack { get; }
        public float DesiredVolume { get; set; }
        public bool PlayerReady { get; }
        public bool IsPlaying { get; }
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

        public void PlayPause()
        {
            throw new NotImplementedException();
        }

        public Task PlayPrev()
        {
            throw new NotImplementedException();
        }

        public Task SkipUser(long id)
        {
            throw new NotImplementedException();
        }

        public Task SkipTrack(long id)
        {
            throw new NotImplementedException();
        }

        public Task ReloadCurrentTrack()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public bool Mute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}