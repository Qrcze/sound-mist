using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.Models
{
    public interface IMusicPlayer
    {
        Track? CurrentTrack { get; }
        float DesiredVolume { get; set; }
        bool PlayerReady { get; }
        bool Playing { get; }
        TracksPlaylist TracksPlaylist { get; }

        event Action<string>? LoadingStatusChanged;
        event Action<PlayState>? PlayStateChanged;
        event Action<Track>? TrackChanged;
        event Action<Track>? TrackChanging;
        event Action<double>? TrackTimeUpdated;

        Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false);
        Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool preloadTrack = false);
        void ClearQueue();
        void ContinueWithAutoplay();
        Task SaveTrackLocally(Track track);
        Task PlayPause(CancellationToken token);
        Task PlayNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null);
        Task PlayNext();
        Task PlayPrev();
        void SetPosition(double value);
        void ShufflePlaylist(bool value);
        Task SkipTrack(int id);
        Task SkipUser(int id);
    }
}