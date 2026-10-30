using ManagedBass;
using NLog;
using System;
using System.Diagnostics;
using System.Threading;

namespace SoundMist.Models.Audio
{
    public class ManagedBassController : IAudioController
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public event Action<string>? Message;

        public event Action<int, int>? ChunksLoadingStateChanged;

        private readonly FileProcedures _procedures;
        private BufferProvider? _bufferProvider;
        private int _musicChannel;
        private bool _mute;
        private double _muteVolume;

        public event Action? OnTrackEnded;

        public ManagedBassController()
        {
            _procedures = new FileProcedures() { Length = ProcLength, Read = ProcRead, Close = ProcClose, Seek = ProcSeek };
            Bass.Init();

            #if OS_LINUX
            int result = Bass.PluginLoad("libbass_aac.so");
            if (result == 0)
                _logger.Error("Failed loading bass aac plugin: {err}", Bass.LastError);
            #endif
        }

        ~ManagedBassController() => Stop();

        public bool ChannelInitialized => _musicChannel != 0;

        public double TimeInSeconds
        {
            get
            {
                if (_musicChannel == 0)
                    return 0;

                var pos = Bass.ChannelGetPosition(_musicChannel);
                return Bass.ChannelBytes2Seconds(_musicChannel, pos);
            }
            set
            {
                if (_musicChannel == 0)
                    return;

                var b = Bass.ChannelSeconds2Bytes(_musicChannel, value);
                if (b == 0)
                    _logger.Error("Couldn't convert seconds to bytes; {e}", Bass.LastError);
                else
                    Bass.ChannelSetPosition(_musicChannel, b);
            }
        }

        public double Volume
        {
            get
            {
                return _musicChannel != 0 ? Bass.ChannelGetAttribute(_musicChannel, ChannelAttribute.Volume) : 0;
            }
            set
            {
                _mute = false;
                Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, Math.Clamp(value, 0, 1));
            }
        }

        public bool Mute
        {
            get => _mute;
            set
            {
                if (_musicChannel == 0)
                    return;

                _mute = value;
                if (_mute)
                {
                    _muteVolume = Bass.ChannelGetAttribute(_musicChannel, ChannelAttribute.Volume);
                    Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, 0);
                }
                else
                {
                    Volume = _muteVolume;
                }
            }
        }

        public bool IsPlaying { get; private set; }

        public void Play()
        {
            if (_musicChannel == 0)
                return;

            IsPlaying = true;
            bool g = Bass.ChannelPlay(_musicChannel);
        }

        public void Pause()
        {
            if (_musicChannel == 0)
                return;

            IsPlaying = false;
            bool g = Bass.ChannelPause(_musicChannel);
        }

        public void Stop()
        {
            if (_musicChannel == 0)
                return;

            IsPlaying = false;

            Bass.ChannelStop(_musicChannel);
            Bass.StreamFree(_musicChannel);

            _musicChannel = 0;
            _bufferProvider?.Dispose();
            _bufferProvider = null;
        }

        public void SetVolume(double volume)
        {
            if (_musicChannel == 0)
                return;

            Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, Math.Clamp(volume, 0, 1));
        }

        public void AppendBytes(Span<byte> bytes)
        {
            _bufferProvider!.AppendBuffer(bytes);
        }

        public void StreamCompleted()
        {
            if (_bufferProvider is not null)
                _bufferProvider.FinishedLoading = true;
        }

        public void InitStream(byte[] buffer, int bufferLength, int trackDurationMs, int bitrate)
        {
            Stop();

            int estimatedSize = (trackDurationMs / 1_000) * (bitrate / 8);
            _bufferProvider = new(estimatedSize);
            _bufferProvider.AppendBuffer(buffer.AsSpan(0, bufferLength));

            _musicChannel = Bass.CreateStream(StreamSystem.Buffer, BassFlags.Default, _procedures);
            ThrowOnBassError();

            Bass.ChannelSetSync(_musicChannel, SyncFlags.End, 0, TrackEnded);
        }

        public void LoadFromFile(string filePath)
        {
            _musicChannel = Bass.CreateStream(filePath, 0, 0, BassFlags.Default);
            ThrowOnBassError();

            Bass.ChannelSetSync(_musicChannel, SyncFlags.End, 0, TrackEnded);
        }

        private void TrackEnded(int Handle, int Channel, int Data, nint User)
        {
            OnTrackEnded?.Invoke();
        }

        static void ThrowOnBassError()
        {
            if (Bass.LastError != Errors.OK)
                throw new AudioControllerException($"Bass library threw exception: {Bass.LastError}");
        }

        protected long ProcLength(nint User)
        {
            //make an overestimation so bass doesn't cut the audio stream short
            return (int)(_bufferProvider!.BufferExpectedSize * 1.5) + 1_000_000;
        }

        protected int ProcRead(nint Buffer, int Length, nint User)
        {
            int l = _bufferProvider!.ReadBuffer(Buffer, Length);

            //stalled
            while (l == -1)
            {
                Debug.Print("//waiting for download...");
                Thread.Sleep(1000);
                if (_bufferProvider is null)
                    return 0;

                l = _bufferProvider.ReadBuffer(Buffer, Length);
            }

            return l;
        }

        protected void ProcClose(nint User)
        {
            Debug.Print("! Stream closed");
            _bufferProvider?.Dispose();
            _bufferProvider = null;
            GC.Collect();
        }

        protected bool ProcSeek(long Offset, nint User)
        {
            //not used by StreamSystem.Buffer
            throw new NotImplementedException();
        }
    }
}