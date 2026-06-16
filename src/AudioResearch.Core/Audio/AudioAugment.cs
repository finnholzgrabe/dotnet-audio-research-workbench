namespace AudioResearch.Core.Audio;

/// <summary>Noise color for additive-noise augmentation.</summary>
public enum NoiseColor
{
    White,
    Pink,
}

/// <summary>
/// Deterministic audio augmentation: controlled-SNR additive noise, gain, DC
/// offset, and clipping. These exist to turn trivially-separable fixtures into a
/// realistic dataset, so the ML baseline reports an honest accuracy. Every method
/// is a pure function of its arguments (randomness is seeded).
/// </summary>
public static class AudioAugment
{
    /// <summary>
    /// Returns a copy of <paramref name="signal"/> with added noise scaled to the
    /// requested signal-to-noise ratio in decibels. Lower dB = noisier.
    /// </summary>
    public static AudioBuffer AddNoiseAtSnr(AudioBuffer signal, double snrDb, NoiseColor color = NoiseColor.White, int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(signal);
        float[] noise = color == NoiseColor.Pink
            ? PinkNoise(signal.Samples.Length, seed)
            : WhiteNoise(signal.Samples.Length, seed);

        double signalPower = Power(signal.Samples);
        double noisePower = Power(noise);
        if (signalPower <= 0 || noisePower <= 0)
        {
            return Clone(signal);
        }

        // Scale noise so that 10*log10(signalPower / scaledNoisePower) == snrDb.
        double targetNoisePower = signalPower / Math.Pow(10.0, snrDb / 10.0);
        double scale = Math.Sqrt(targetNoisePower / noisePower);

        var mixed = new float[signal.Samples.Length];
        for (int i = 0; i < mixed.Length; i++)
        {
            mixed[i] = (float)(signal.Samples[i] + noise[i] * scale);
        }

        return new AudioBuffer(mixed, signal.SampleRate, signal.Channels);
    }

    /// <summary>Returns a gain-scaled copy (linear factor, e.g. 0.5 = -6 dB).</summary>
    public static AudioBuffer ApplyGain(AudioBuffer signal, double gain)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var result = new float[signal.Samples.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (float)(signal.Samples[i] * gain);
        }

        return new AudioBuffer(result, signal.SampleRate, signal.Channels);
    }

    /// <summary>Returns a copy with a constant DC offset added.</summary>
    public static AudioBuffer AddDcOffset(AudioBuffer signal, double offset)
    {
        ArgumentNullException.ThrowIfNull(signal);
        var result = new float[signal.Samples.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (float)(signal.Samples[i] + offset);
        }

        return new AudioBuffer(result, signal.SampleRate, signal.Channels);
    }

    /// <summary>Returns a hard-clipped copy at the given symmetric threshold.</summary>
    public static AudioBuffer Clip(AudioBuffer signal, double threshold)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be positive.");
        }

        float t = (float)threshold;
        var result = new float[signal.Samples.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Math.Clamp(signal.Samples[i], -t, t);
        }

        return new AudioBuffer(result, signal.SampleRate, signal.Channels);
    }

    /// <summary>Mean power (mean of squared samples) of a signal.</summary>
    public static double Power(IReadOnlyList<float> samples)
    {
        if (samples.Count == 0)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = 0; i < samples.Count; i++)
        {
            sum += (double)samples[i] * samples[i];
        }

        return sum / samples.Count;
    }

    private static float[] WhiteNoise(int n, int seed)
    {
        var rng = new Random(seed);
        var noise = new float[n];
        for (int i = 0; i < n; i++)
        {
            noise[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        return noise;
    }

    /// <summary>
    /// Pink (1/f) noise via Paul Kellet's economical filtering of white noise.
    /// Deterministic for a given seed.
    /// </summary>
    private static float[] PinkNoise(int n, int seed)
    {
        var rng = new Random(seed);
        var noise = new float[n];
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (int i = 0; i < n; i++)
        {
            double white = rng.NextDouble() * 2.0 - 1.0;
            b0 = 0.99886 * b0 + white * 0.0555179;
            b1 = 0.99332 * b1 + white * 0.0750759;
            b2 = 0.96900 * b2 + white * 0.1538520;
            b3 = 0.86650 * b3 + white * 0.3104856;
            b4 = 0.55000 * b4 + white * 0.5329522;
            b5 = -0.7616 * b5 - white * 0.0168980;
            double pink = b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362;
            b6 = white * 0.115926;
            noise[i] = (float)(pink * 0.11); // approximate normalization to ~[-1, 1]
        }

        return noise;
    }

    private static AudioBuffer Clone(AudioBuffer signal) =>
        new((float[])signal.Samples.Clone(), signal.SampleRate, signal.Channels);
}
