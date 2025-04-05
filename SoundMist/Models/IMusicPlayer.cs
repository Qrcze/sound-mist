﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMist.Models
{
    public enum PlayState
    {
        Playing,
        Paused,
        Loading,
        Loaded,
        Error,
    }

    public interface IMusicPlayer
    {
        Track? CurrentTrack { get; }
        float DesiredVolume { get; set; }
        bool PlayerReady { get; }
        bool Playing { get; }
        TracksPlaylist TracksPlaylist { get; }

        event Action<string>? ErrorCallback;
        event Action<PlayState, string>? PlayStateUpdated;
        event Action<Track>? TrackChanging;
        event Action<Track>? TrackChanged;
        event Action<double>? TrackTimeUpdated;

        void SetPosition(double value);
        Task AddToQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null);
        Task AddToQueue(Track track, Func<Task<IEnumerable<Track>>>? downloadMore = null);
        void ClearQueue();
        void ContinueWithAutoplay();
        Task LoadNewQueue(IEnumerable<Track> tracks, Func<Task<IEnumerable<Track>>>? downloadMore = null, bool startPlaying = true);
        void PlayPause();
        Task PlayNext();
        Task PlayPrev();
        Task ReloadCurrentTrack();
    }
}