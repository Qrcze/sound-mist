using System;

namespace SoundMist.Models.Audio
{
    public interface IAudioController
    {
        bool ChannelInitialized { get; }
        double TimeInSeconds { get; set; }
        double Volume { get; set; }
        bool IsPlaying { get; }
        bool Mute { get; set; }

        event Action? OnTrackEnded;

        void Play();

        void Pause();

        void Stop();

        void AppendBytes(Span<byte> bytes);

        void InitBufferedChannel(byte[] initialBytes, int trackDurationMs);

        void LoadFromFile(string filePath);

        void StreamCompleted();
    }
}