using System;

namespace SoundMist.Models.Audio
{
    public interface IAudioController
    {
        bool ChannelInitialized { get; }
        double TimeInSeconds { get; set; }
        double Volume { get; set; }
        bool IsPlaying { get; }

        event Action? OnTrackEnded;

        void Play();

        void Pause();

        void Stop();

        void AppendBytes(byte[] bytes);

        void InitBufferedChannel(byte[] initialBytes, int trackDurationMs);

        void LoadFromFile(string filePath);

        void StreamCompleted();
    }
}