using SoundMist.Models.Audio;

namespace SCPlayerTests
{
    public class BufferProviderTests
    {
        [Fact]
        public void AppendsBytes()
        {
            var bp = new BufferProvider(100);

            Assert.Equal(0, bp.LoadedBytes);

            bp.AppendBuffer(new byte[10]);

            Assert.Equal(10, bp.LoadedBytes);

            bp.AppendBuffer(new byte[15]);

            Assert.Equal(25, bp.LoadedBytes);
        }

        [Fact]
        public void IncreasesArrayIfNeeded()
        {
            var bp = new BufferProvider(100);

            Assert.Equal(100, bp.RawBuffer.Length);
            Assert.Equal(0, bp.LoadedBytes);

            bp.AppendBuffer(new byte[50]);

            Assert.Equal(100, bp.RawBuffer.Length);
            Assert.Equal(50, bp.LoadedBytes);

            bp.AppendBuffer(new byte[55]);

            Assert.Equal(105, bp.RawBuffer.Length);
            Assert.Equal(105, bp.LoadedBytes);
        }

        [Fact]
        public void ReadsCorrectBytes()
        {
            var bp = new BufferProvider(100);

            byte[] bytesToAppend = Enumerable.Range(0, 80).Select(x => (byte)x).ToArray();

            bp.AppendBuffer(bytesToAppend);
            bp.FinishedLoading = true;

            byte[] readBuffer = new byte[100];

            int offset = 0;
            int readBytes = 0;
            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(50, readBytes);
            readBytes = ReadNext(bp, readBuffer, 20, ref offset);
            Assert.Equal(20, readBytes);

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.True(readBytes == 10, $"Last read should only return 10 even if asking for more, but it returned {readBytes}");
        }

        unsafe int ReadNext(BufferProvider bp, byte[] readBuffer, int count, ref int offset)
        {
            int readBytes = 0;
            fixed (byte* bPtr = readBuffer)
            {
                readBytes = bp.ReadBuffer((nint)bPtr, count);
            }

            if (readBytes == -1)
                return -1;

            //read next count bytes, rest shouldn't be touched
            for (int i = 0; i < readBytes; i++)
            {
                if (i < count)
                    Assert.Equal(i + offset, readBuffer[i]);
                else
                    Assert.Equal(0, readBuffer[i]);

                readBuffer[i] = 0;
            }

            offset += readBytes;

            return readBytes;
        }

        [Fact]
        public void FillsBufferOnlyIfNotStalled()
        {
            var bp = new BufferProvider(100);

            byte[] bytesToAppend = Enumerable.Range(0, 100).Select(x => (byte)x).ToArray();

            byte[] readBuffer = new byte[100];

            bp.AppendBuffer(bytesToAppend[0..20]);

            int offset = 0;
            int readBytes = 0;
            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.True(readBytes == -1, "Asking for more bytes than loaded should be stalled");

            offset = 0;

            bp.AppendBuffer(bytesToAppend[20..50]);

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(50, bp.LoadedBytes);
            Assert.Equal(50, readBytes);

            bp.AppendBuffer(bytesToAppend[50..60]);
            bp.FinishedLoading = true;

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(10, readBytes);
            Assert.Equal(60, bp.LoadedBytes);
        }
    }
}