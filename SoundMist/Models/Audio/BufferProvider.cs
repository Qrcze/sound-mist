using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundMist.Models.Audio
{
    internal class BufferProvider(int estimatedSize)
    {
        public byte[] RawBuffer => _buffer;

        private byte[] _buffer = new byte[estimatedSize];

        public volatile int offset;
        public int LoadedBytes { get; private set; }
        public bool FinishedLoading { get; set; }

        public void AppendBuffer(byte[] bytes)
        {
            if (LoadedBytes + bytes.Length > _buffer.Length)
            {
                Debug.Print($"had to increase the track buffer size ({_buffer.Length} + {bytes.Length}bytes needed)");

                var newBuffer = new byte[_buffer.Length + bytes.Length];
                Array.Copy(_buffer, newBuffer, _buffer.Length);
                _buffer = newBuffer;
            }

            Array.Copy(bytes, 0, _buffer, LoadedBytes, bytes.Length);
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
    }
}