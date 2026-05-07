using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using SoundMist.Models.Audio;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundMistBenchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<BufferProviderBenchmarks>();
    }
}

[MemoryDiagnoser]
[Config(typeof(DontForceGcCollectionsConfig))]
public class BufferProviderBenchmarks
{
    [Benchmark]
    public void CreatePooled()
    {
        var b = new BufferProvider(5_000_000);
        b.Dispose();
    }

    [Benchmark]
    public void CreateDefault()
    {
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(new BufferProviderOld(5_000_000));
    }
}

public class DontForceGcCollectionsConfig : ManualConfig
{
    public DontForceGcCollectionsConfig()
    {
        AddJob(Job.Default.WithGcMode(new GcMode() { Force = false }));
    }
}

public class BufferProviderOld(int estimatedSize)
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

            var newBuffer = new byte[LoadedBytes + bytes.Length];
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