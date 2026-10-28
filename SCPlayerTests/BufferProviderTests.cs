using SoundMist.Models.Audio;

namespace SCPlayerTests
{
    public class BufferProviderTests
    {
        [Fact]
        public void AppendsBytes()
        {
            using var bp = new BufferProvider(100);

            Assert.Equal(0, bp.LoadedBytes);

            bp.AppendBuffer(new byte[10]);

            Assert.Equal(10, bp.LoadedBytes);

            bp.AppendBuffer(new byte[15]);

            Assert.Equal(25, bp.LoadedBytes);
        }

        [Fact]
        public void IncreasesArrayIfNeeded()
        {
            using var bp = new BufferProvider(100);

            Assert.True(bp.RawBuffer.Length >= 100); //ArrayPool can assign more bytes than needed
            Assert.Equal(0, bp.LoadedBytes);

            bp.AppendBuffer(new byte[50]);

            Assert.True(bp.RawBuffer.Length >= 100); //ArrayPool can assign more bytes than needed
            Assert.Equal(50, bp.LoadedBytes);

            bp.AppendBuffer(new byte[155]);

            Assert.True(bp.RawBuffer.Length >= 205); //ArrayPool can assign more bytes than needed
            Assert.Equal(205, bp.LoadedBytes);
        }

        [Fact]
        public void ReadsCorrectBytes()
        {
            using var bp = new BufferProvider(100);

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
            using var bp = new BufferProvider(100);

            byte[] bytesToAppend = Enumerable.Range(0, 100).Select(x => (byte)x).ToArray();

            byte[] readBuffer = new byte[100];

            bp.AppendBuffer(bytesToAppend[0..20]);

            int offset = 0;
            int readBytes = 0;
            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(20, readBytes); //changed - previously it stalled, now it just returns whatever it can

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(-1, readBytes); //now it should be stalled because there's no thing left to get

            //add 30 more bytes
            bp.AppendBuffer(bytesToAppend[20..50]);

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(50, bp.LoadedBytes);
            Assert.Equal(30, readBytes);

            bp.AppendBuffer(bytesToAppend[50..100]);
            bp.FinishedLoading = true;

            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(50, readBytes);
            Assert.Equal(100, bp.LoadedBytes);
            
            //stream ended and nothing left to read - return 0
            readBytes = ReadNext(bp, readBuffer, 50, ref offset);
            Assert.Equal(0, readBytes);
            Assert.Equal(100, bp.LoadedBytes);

        }
    }
}