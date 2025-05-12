using SoundMist.Models.Audio;

namespace SCPlayerTests
{
    internal class DummyAudioController : IAudioController
    {
        public bool ChannelInitialized { get; private set; }

        public double TimeInSeconds { get; set; }
        public double Volume { get; set; }

        public bool IsPlaying { get; private set; }

        public event Action? OnTrackEnded;

        public void Play()
        {
            if (ChannelInitialized)
                IsPlaying = true;
        }

        public void Pause()
        {
            if (ChannelInitialized)
                IsPlaying = false;
        }

        public void Stop()
        {
            IsPlaying = false;
            TimeInSeconds = 0;
            ChannelInitialized = false;
        }

        public void AppendBytes(byte[] bytes)
        {
        }

        public void InitBufferedChannel(byte[] initialBytes, int trackDurationMs)
        {
            ChannelInitialized = true;
        }

        public void LoadFromFile(string filePath)
        {
            ChannelInitialized = true;
        }

        public void StreamCompleted()
        {
        }
    }
}