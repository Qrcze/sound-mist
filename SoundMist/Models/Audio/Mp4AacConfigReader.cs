using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SoundMist.Models.Audio;

public readonly record struct AacConfig(int SampleRate, int Channels, int AudioObjectType, byte[] AudioSpecificConfig);

public static class Mp4AacConfigReader
{
    private static readonly int[] _sampleRateTable =
    {
        96000, 88200, 64000, 48000, 44100, 32000, 24000,
        22050, 16000, 12000, 11025, 8000, 7350
    };

    // Boxes that contain other boxes and may lead to stsd/mp4a
    private static readonly HashSet<string> _containerBoxes = ["moov", "trak", "mdia", "minf", "stbl"];

    private readonly record struct Box(string Type, int Start, int PayloadStart, int End);

    /// <summary>
    /// Reads config data from init.mp4 file
    /// </summary>
    /// <param name="init"></param>
    /// <returns></returns>
    /// <exception cref="InvalidDataException"></exception>
    public static AacConfig ReadAacConfigFromInit(byte[] init)
    {
        return FindAac(init, 0, init.Length) ?? throw new InvalidDataException("No mp4a/esds AAC configuration found in init segment.");
    }

    private static AacConfig? FindAac(byte[] d, int start, int end)
    {
        foreach (var box in ReadBoxes(d, start, end))
        {
            AacConfig? result = null;

            if (_containerBoxes.Contains(box.Type))
            {
                result = FindAac(d, box.PayloadStart, box.End);
            }
            else if (box.Type == "stsd")
            {
                // FullBox: version/flags (4) + entry_count (4), then sample entries
                result = FindAac(d, box.PayloadStart + 8, box.End);
            }
            else if (box.Type == "mp4a")
            {
                result = ParseMp4a(d, box.PayloadStart, box.End);
            }

            if (result is not null) return result;
        }
        return null;
    }

    private static IEnumerable<Box> ReadBoxes(byte[] d, int start, int end)
    {
        int pos = start;
        while (pos + 8 <= end)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos));
            string type = Encoding.ASCII.GetString(d, pos + 4, 4);
            int header = 8;

            if (size == 0) // extends to end of container
                size = end - pos;
            else if (size == 1) // 64-bit largesize
            {
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(d.AsSpan(pos + 8));
                header = 16;
            }

            if (size < header || pos + size > end) yield break;

            yield return new Box(type, pos, pos + header, pos + (int)size);
            pos += (int)size;
        }
    }

    private static AacConfig? ParseMp4a(byte[] d, int payload, int end)
    {
        // AudioSampleEntry layout (payload-relative):
        //  0-5   reserved, 6-7 data_reference_index
        //  8-15  reserved (8 bytes)
        //  16-17 channelcount
        //  18-19 samplesize, 20-21 pre_defined, 22-23 reserved
        //  24-27 samplerate (16.16 fixed point)
        //  28+   child boxes (esds)
        int channelCount = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(payload + 16));
        int sampleRate = (int)(BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(payload + 24)) >> 16);

        foreach (var box in ReadBoxes(d, payload + 28, end))
        {
            if (box.Type == "esds")
                return ParseEsds(d, box.PayloadStart, box.End, sampleRate, channelCount);
        }
        return null;
    }

    private static AacConfig? ParseEsds(byte[] d, int start, int end, int mp4aRate, int mp4aChannels)
    {
        int pos = start + 4; // skip version/flags (esds is a FullBox)

        // ES_Descriptor (tag 0x03)
        ReadDescriptorHeader(d, ref pos, 0x03);
        pos += 2;                                   // ES_ID
        byte flags = d[pos++];
        if ((flags & 0x80) != 0) pos += 2;          // dependsOn_ES_ID
        if ((flags & 0x40) != 0) pos += 1 + d[pos]; // URL (length-prefixed)
        if ((flags & 0x20) != 0) pos += 2;          // OCR_ES_ID

        // DecoderConfigDescriptor (tag 0x04)
        ReadDescriptorHeader(d, ref pos, 0x04);
        if (d[pos] != 0x40) return null;            // 0x40 = MPEG-4 Audio (AAC)
        pos += 13;                                  // objectTypeIndication, streamType,

        // bufferSizeDB, maxBitrate, avgBitrate

        // DecoderSpecificInfo (tag 0x05) = AudioSpecificConfig
        int ascLength = ReadDescriptorHeader(d, ref pos, 0x05);
        byte[] asc = d.AsSpan(pos, ascLength).ToArray();

        return ParseAudioSpecificConfig(asc, mp4aRate, mp4aChannels);
    }

    // MPEG-4 descriptors: 1 tag byte, then a length encoded in 1-4 bytes (7 bits each, high bit = continue)
    private static int ReadDescriptorHeader(byte[] d, ref int pos, byte expectedTag)
    {
        if (d[pos++] != expectedTag)
            throw new InvalidDataException($"Expected descriptor tag 0x{expectedTag:X2}.");

        int length = 0;
        for (int i = 0; i < 4; i++)
        {
            byte b = d[pos++];
            length = (length << 7) | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return length;
    }

    private static AacConfig ParseAudioSpecificConfig(byte[] asc, int fallbackRate, int fallbackChannels)
    {
        int bit = 0;

        int aot = ReadBits(asc, ref bit, 5);
        if (aot == 31) aot = 32 + ReadBits(asc, ref bit, 6); // escaped object type

        int freqIndex = ReadBits(asc, ref bit, 4);
        int sampleRate;
        if (freqIndex == 15)
        {
            sampleRate = ReadBits(asc, ref bit, 24);         // explicit rate follows
        }
        else if (freqIndex < _sampleRateTable.Length)
        {
            sampleRate = _sampleRateTable[freqIndex];
        }
        else
        {
            throw new InvalidDataException($"Reserved sampling frequency index {freqIndex}.");
        }

        int chanCfg = ReadBits(asc, ref bit, 4);
        int channels = chanCfg switch
        {
            0 => fallbackChannels,   // channels defined in a program_config_element
            7 => 8,                  // 7 = 7.1 surround (8 channels)
            _ => chanCfg
        };

        if (sampleRate == 0) sampleRate = fallbackRate;

        return new AacConfig(sampleRate, channels, aot, asc);
    }

    private static int ReadBits(byte[] data, ref int bitPos, int count)
    {
        int value = 0;
        for (int i = 0; i < count; i++, bitPos++)
            value = (value << 1) | ((data[bitPos >> 3] >> (7 - (bitPos & 7))) & 1);
        return value;
    }

    /// <summary>
    /// Returns the raw AAC access units (no ADTS header) for one media segment (.m4s/.mp4)
    /// </summary>
    /// <param name="segment"></param>
    /// <returns></returns>
    public static List<byte[]> ExtractAacSamples(byte[] segment)
    {
        var samples = new List<byte[]>();
        var top = new List<Box>(ReadBoxes(segment, 0, segment.Length));

        for (int i = 0; i < top.Count; i++)
        {
            if (top[i].Type != "moof") continue;
            var moof = top[i];

            // Without data_offset, samples start at the payload of the mdat that follows this moof
            int cursor = -1;
            for (int j = i + 1; j < top.Count; j++)
            {
                if (top[j].Type == "mdat")
                {
                    cursor = top[j].PayloadStart;
                    break;
                }
            }

            foreach (var traf in ReadBoxes(segment, moof.PayloadStart, moof.End))
            {
                if (traf.Type == "traf")
                    ParseTraf(segment, moof, traf, ref cursor, samples);
            }
        }

        return samples;
    }

    private static void ParseTraf(byte[] d, Box moof, Box traf, ref int cursor, List<byte[]> samples)
    {
        long? baseDataOffset = null;
        uint defaultSampleSize = 0;

        // tfhd: track fragment header, holds the defaults for this traf
        foreach (var box in ReadBoxes(d, traf.PayloadStart, traf.End))
        {
            if (box.Type != "tfhd") continue;

            int pos = box.PayloadStart;
            uint flags = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos)) & 0x00FFFFFF;
            pos += 8; // version/flags + track_ID

            if ((flags & 0x000001) != 0) { baseDataOffset = (long)BinaryPrimitives.ReadUInt64BigEndian(d.AsSpan(pos)); pos += 8; }
            if ((flags & 0x000002) != 0) pos += 4; // sample_description_index
            if ((flags & 0x000008) != 0) pos += 4; // default_sample_duration
            if ((flags & 0x000010) != 0) defaultSampleSize = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos)); // default_sample_size
        }

        // trun: one or more runs describing sample sizes and data offsets
        foreach (var box in ReadBoxes(d, traf.PayloadStart, traf.End))
        {
            if (box.Type != "trun") continue;

            int pos = box.PayloadStart;
            uint flags = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos)) & 0x00FFFFFF;
            pos += 4;
            uint sampleCount = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos));
            pos += 4;

            bool hasDataOffset = (flags & 0x000001) != 0;
            long sampleStart;

            if (hasDataOffset)
            {
                int dataOffset = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(pos)); // signed
                pos += 4;
                long baseAbs = baseDataOffset ?? moof.Start; // default: relative to moof start
                sampleStart = baseAbs + dataOffset;
            }
            else
            {
                sampleStart = cursor; // continue after the previous run's data
            }

            if ((flags & 0x000004) != 0) pos += 4; // first_sample_flags

            for (uint s = 0; s < sampleCount; s++)
            {
                if ((flags & 0x000100) != 0) pos += 4; // sample_duration

                uint size;
                if ((flags & 0x000200) != 0)
                {
                    size = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(pos));
                    pos += 4;
                }
                else
                {
                    size = defaultSampleSize;
                }

                if ((flags & 0x000400) != 0) pos += 4; // sample_flags
                if ((flags & 0x000800) != 0) pos += 4; // composition time offset

                if (size == 0) throw new InvalidDataException("Sample size not defined in tfhd or trun.");
                if (sampleStart < 0 || sampleStart + size > d.Length)
                    throw new InvalidDataException("Sample data lies outside the segment.");

                samples.Add(d.AsSpan((int)sampleStart, (int)size).ToArray());
                sampleStart += size;
            }

            cursor = (int)sampleStart;
        }
    }

    // Wraps one raw AAC access unit in a 7-byte ADTS header (MPEG-4, no CRC)
    public static byte[] WrapAdts(ReadOnlySpan<byte> aacFrame, AacConfig config)
    {
        // ADTS stores profile as (object type - 1), which only covers AAC-LC and its
        // predecessors (AOT 1 to 4). Higher object types such as HE-AAC need explicit signalling.
        int profile = config.AudioObjectType - 1;
        if (profile < 0 || profile > 3)
            throw new NotSupportedException($"ADTS cannot represent audio object type {config.AudioObjectType}.");

        // ADTS needs the table index. Explicit 24-bit rates (index 15) can't be expressed.
        int freqIndex = Array.IndexOf(_sampleRateTable, config.SampleRate);
        if (freqIndex < 0)
            throw new NotSupportedException($"Sample rate {config.SampleRate} has no ADTS frequency index.");

        // channel_configuration 7 means 8 channels (7.1); the value 8 is not valid in ADTS
        int channelCfg = config.Channels == 8 ? 7 : config.Channels;
        if (channelCfg < 1 || channelCfg > 7)
            throw new NotSupportedException($"Channel count {config.Channels} cannot be expressed in ADTS.");

        int frameLength = aacFrame.Length + 7; // ADTS frame_length includes the header
        if (frameLength > 0x1FFF)
            throw new InvalidDataException("AAC frame too large for an ADTS header.");

        var frame = new byte[frameLength];

        frame[0] = 0xFF;                                   // syncword (high 8 bits)
        frame[1] = 0xF1;                                   // syncword low 4 bits, ID=0 (MPEG-4), layer=00, protection_absent=1
        frame[2] = (byte)((profile << 6) | (freqIndex << 2) | ((channelCfg >> 2) & 0x01));
        frame[3] = (byte)(((channelCfg & 0x03) << 6) | ((frameLength >> 11) & 0x03));
        frame[4] = (byte)((frameLength >> 3) & 0xFF);
        frame[5] = (byte)(((frameLength & 0x07) << 5) | 0x1F); // frame_length low 3 bits, buffer_fullness high 5 bits (0x7FF = VBR)
        frame[6] = 0xFC;                                   // buffer_fullness low 6 bits, 0 extra raw blocks (one block per frame)

        aacFrame.CopyTo(frame.AsSpan(7));
        return frame;
    }
}