using System.Buffers.Binary;

namespace AudioResearch.Core.Audio;

/// <summary>
/// Minimal, explicit reader/writer for canonical PCM WAV files.
/// Supports 16-bit signed PCM, mono or multi-channel. This is intentionally
/// small and strict: unsupported formats raise a clear exception rather than
/// guessing.
/// </summary>
public static class WavFile
{
    private const int BitsPerSample = 16;
    private const float MaxSample = 32768f; // 2^15, used for symmetric scaling

    /// <summary>Reads a 16-bit PCM WAV file from disk.</summary>
    public static AudioBuffer Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads a 16-bit PCM WAV stream into normalized float samples.</summary>
    public static AudioBuffer Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        if (ReadFourCc(reader) != "RIFF")
        {
            throw new InvalidDataException("Not a RIFF file (missing 'RIFF' tag).");
        }

        reader.ReadInt32(); // overall chunk size, not needed
        if (ReadFourCc(reader) != "WAVE")
        {
            throw new InvalidDataException("Not a WAVE file (missing 'WAVE' tag).");
        }

        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        bool haveFormat = false;
        byte[]? data = null;

        while (stream.Position < stream.Length)
        {
            string chunkId = ReadFourCc(reader);
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                short audioFormat = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();
                haveFormat = true;

                if (audioFormat != 1)
                {
                    throw new InvalidDataException($"Unsupported WAV format tag {audioFormat}; only PCM (1) is supported.");
                }

                if (bitsPerSample != BitsPerSample)
                {
                    throw new InvalidDataException($"Unsupported bit depth {bitsPerSample}; only 16-bit PCM is supported.");
                }

                // Skip any extra fmt bytes beyond the 16 we read.
                int extra = chunkSize - 16;
                if (extra > 0)
                {
                    reader.ReadBytes(extra);
                }
            }
            else if (chunkId == "data")
            {
                data = reader.ReadBytes(chunkSize);
            }
            else
            {
                // Skip unknown chunks (e.g. LIST, fact).
                reader.ReadBytes(chunkSize);
            }

            // Chunks are word-aligned; skip a pad byte for odd sizes.
            if (chunkSize % 2 != 0 && stream.Position < stream.Length)
            {
                reader.ReadByte();
            }
        }

        if (!haveFormat)
        {
            throw new InvalidDataException("WAV file is missing a 'fmt ' chunk.");
        }

        if (data is null)
        {
            throw new InvalidDataException("WAV file is missing a 'data' chunk.");
        }

        int sampleCount = data.Length / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short raw = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i * 2, 2));
            samples[i] = raw / MaxSample;
        }

        return new AudioBuffer(samples, sampleRate, channels);
    }

    /// <summary>Writes an <see cref="AudioBuffer"/> as a 16-bit PCM WAV file.</summary>
    public static void Write(string path, AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using FileStream stream = File.Create(path);
        Write(stream, buffer);
    }

    /// <summary>Writes an <see cref="AudioBuffer"/> as 16-bit PCM into a stream.</summary>
    public static void Write(Stream stream, AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(buffer);

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        int channels = buffer.Channels;
        int sampleRate = buffer.SampleRate;
        int blockAlign = channels * (BitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;
        int dataBytes = buffer.Samples.Length * (BitsPerSample / 8);

        WriteFourCc(writer, "RIFF");
        writer.Write(36 + dataBytes);
        WriteFourCc(writer, "WAVE");

        WriteFourCc(writer, "fmt ");
        writer.Write(16);                       // PCM fmt chunk size
        writer.Write((short)1);                 // audio format = PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)BitsPerSample);

        WriteFourCc(writer, "data");
        writer.Write(dataBytes);

        Span<byte> two = stackalloc byte[2];
        foreach (float sample in buffer.Samples)
        {
            float clamped = Math.Clamp(sample, -1f, 1f);
            // Symmetric rounding to nearest 16-bit code, clamped to valid range.
            int value = (int)MathF.Round(clamped * (MaxSample - 1));
            value = Math.Clamp(value, short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(two, (short)value);
            writer.Write(two);
        }
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        byte[] b = reader.ReadBytes(4);
        if (b.Length < 4)
        {
            throw new InvalidDataException("Unexpected end of file while reading a chunk tag.");
        }

        return System.Text.Encoding.ASCII.GetString(b);
    }

    private static void WriteFourCc(BinaryWriter writer, string fourCc)
    {
        writer.Write(System.Text.Encoding.ASCII.GetBytes(fourCc));
    }
}
