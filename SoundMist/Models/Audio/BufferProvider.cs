using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundMist.Models.Audio
{
    public sealed class BufferProvider(int estimatedSize) : IDisposable
    {
        public byte[] RawBuffer => _buffer;
        public int BufferExpectedSize { get; private set; } = estimatedSize;

        private byte[] _buffer = ArrayPool<byte>.Shared.Rent(estimatedSize);

        public volatile int offset;

        public int LoadedBytes { get; private set; }
        public bool FinishedLoading { get; set; }

        public void AppendBuffer(Span<byte> bytes)
        {
            if (LoadedBytes + bytes.Length > _buffer.Length)
            {
                Debug.Print($"had to increase the track buffer size ({_buffer.Length} + {bytes.Length}bytes needed)");

                BufferExpectedSize = LoadedBytes + bytes.Length;
                var newBuffer = ArrayPool<byte>.Shared.Rent(BufferExpectedSize);
                Array.Copy(_buffer, newBuffer, _buffer.Length);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = newBuffer;
            }

            bytes.CopyTo(_buffer.AsSpan(LoadedBytes, bytes.Length));
            LoadedBytes += bytes.Length;
        }

        /// <summary>
        /// Fills up the buffer up to the requested length with available bytes. If stalled, will return -1, if stream reached the end, will simply return 0.
        /// </summary>
        /// <param name="buffer">pointer to buffer to copy to</param>
        /// <param name="requestedLen">max bytes to copy into the buffer</param>
        /// <returns></returns>
        public int ReadBuffer(nint buffer, int requestedLen)
        {
            if (offset + requestedLen > LoadedBytes)
            {
                if (!FinishedLoading)
                    return -1;

                requestedLen = LoadedBytes - offset;
            }

            Marshal.Copy(_buffer, offset, buffer, requestedLen);
            offset += requestedLen;

            return requestedLen;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}