namespace AudioResearch.Core.Audio;

/// <summary>
/// An in-memory block of audio with interleaved floating-point samples in the
/// nominal range [-1, 1]. This is the common currency passed between the WAV IO,
/// signal generation, and DSP layers.
/// </summary>
public sealed class AudioBuffer
{
    /// <summary>Interleaved samples. For stereo the layout is L,R,L,R,...</summary>
    public float[] Samples { get; }

    /// <summary>Samples per second (e.g. 16000, 44100).</summary>
    public int SampleRate { get; }

    /// <summary>Number of interleaved channels (1 = mono, 2 = stereo).</summary>
    public int Channels { get; }

    public AudioBuffer(float[] samples, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), "Channel count must be positive.");
        }

        if (samples.Length % channels != 0)
        {
            throw new ArgumentException("Sample count must be a multiple of the channel count.", nameof(samples));
        }

        Samples = samples;
        SampleRate = sampleRate;
        Channels = channels;
    }

    /// <summary>Number of sample frames (samples per channel).</summary>
    public int FrameCount => Samples.Length / Channels;

    /// <summary>Duration in seconds.</summary>
    public double Duration => (double)FrameCount / SampleRate;

    /// <summary>Absolute peak sample value across all channels.</summary>
    public float Peak()
    {
        float peak = 0f;
        foreach (float s in Samples)
        {
            float a = Math.Abs(s);
            if (a > peak)
            {
                peak = a;
            }
        }

        return peak;
    }

    /// <summary>
    /// Returns a mono view of the audio. Multi-channel audio is down-mixed by
    /// averaging channels; mono audio is returned as a copy.
    /// </summary>
    public float[] ToMono()
    {
        if (Channels == 1)
        {
            return (float[])Samples.Clone();
        }

        int frames = FrameCount;
        var mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            int baseIndex = i * Channels;
            for (int c = 0; c < Channels; c++)
            {
                sum += Samples[baseIndex + c];
            }

            mono[i] = sum / Channels;
        }

        return mono;
    }
}
