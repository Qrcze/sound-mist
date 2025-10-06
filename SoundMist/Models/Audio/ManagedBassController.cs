using ManagedBass;
using System;
using System.Diagnostics;
using System.Threading;

namespace SoundMist.Models.Audio
{
    public class ManagedBassController : IAudioController
    {
        public event Action<string>? Message;

        public event Action<int, int>? ChunksLoadingStateChanged;

        private readonly FileProcedures _procedures;
        private BufferProvider? _bufferProvider;
        private int _musicChannel;
        private bool _mute;
        private double _muteVolume;

        public event Action? OnTrackEnded;

        //private CancellationTokenSource? _downloadTokenSource;

        public ManagedBassController()
        {
            _procedures = new FileProcedures() { Length = ProcLength, Read = ProcRead, Close = ProcClose, Seek = ProcSeek };
            Bass.Init();
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
            _bufferProvider = null;
        }

        public void SetVolume(double volume)
        {
            if (_musicChannel == 0)
                return;

            Bass.ChannelSetAttribute(_musicChannel, ChannelAttribute.Volume, Math.Clamp(volume, 0, 1));
        }

        public void AppendBytes(byte[] bytes)
        {
            _bufferProvider!.AppendBuffer(bytes);
        }

        public void StreamCompleted()
        {
            _bufferProvider!.FinishedLoading = true;
        }

        public void InitBufferedChannel(byte[] initialBytes, int trackDurationMs)
        {
            Stop();

            var decodeChannel = Bass.CreateStream(initialBytes, 0, initialBytes.Length, BassFlags.Decode);
            ThrowOnBassError();

            Bass.ChannelGetAttribute(decodeChannel, ChannelAttribute.Bitrate, out float bitrate);
            Debug.Print($"received bitrate: {bitrate}");
            ThrowOnBassError();

            Bass.StreamFree(decodeChannel);

            int estimatedSize = trackDurationMs * (int)bitrate / 8;
            _bufferProvider = new(estimatedSize);
            _bufferProvider.AppendBuffer(initialBytes);

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
            //return 0;
            return _bufferProvider!.RawBuffer.Length;
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