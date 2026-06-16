using AudioResearch.Core.Audio;
using AudioResearch.Core.Dsp;

namespace AudioResearch.Core.Features;

/// <summary>Configuration for the feature pipeline. Defaults are reasonable for 16 kHz fixtures.</summary>
public sealed record FeatureOptions(int FrameSize = 512, int Hop = 256, int FilterCount = 24, double FMin = 50.0)
{
    public static FeatureOptions Default { get; } = new();
}

/// <summary>Per-frame cochlear-filter-bank energies plus the frequency axis.</summary>
public sealed class BandFeatures
{
    public double[][] FrameEnergies { get; }   // [frame][filter], log-compressed

    public double[] CenterFrequencies { get; }

    public int FrameSize { get; }

    public int Hop { get; }

    public int SampleRate { get; }

    public BandFeatures(double[][] frameEnergies, double[] centerFrequencies, int frameSize, int hop, int sampleRate)
    {
        FrameEnergies = frameEnergies;
        CenterFrequencies = centerFrequencies;
        FrameSize = frameSize;
        Hop = hop;
        SampleRate = sampleRate;
    }

    public int FrameCount => FrameEnergies.Length;

    public int FilterCount => CenterFrequencies.Length;
}

/// <summary>
/// A fixed-length, named feature vector summarising an audio buffer. The order of
/// <see cref="Vector"/> matches <see cref="Names"/> and is stable across runs so
/// it can feed both the JSON summary and the ML baseline.
/// </summary>
public sealed class FeatureSummary
{
    public IReadOnlyList<string> Names { get; }

    public double[] Vector { get; }

    public FeatureSummary(IReadOnlyList<string> names, double[] vector)
    {
        if (names.Count != vector.Length)
        {
            throw new ArgumentException("Names and vector must have equal length.");
        }

        Names = names;
        Vector = vector;
    }

    public IEnumerable<KeyValuePair<string, double>> Pairs()
    {
        for (int i = 0; i < Vector.Length; i++)
        {
            yield return new KeyValuePair<string, double>(Names[i], Vector[i]);
        }
    }
}

/// <summary>
/// Turns audio into cochlear-inspired band features and a compact summary vector.
/// Pure: no IO. The CLI is responsible for serializing the results.
/// </summary>
public static class FeatureExtractor
{
    /// <summary>Computes per-frame, log-compressed cochlear-filter-bank energies.</summary>
    public static BandFeatures ExtractBands(AudioBuffer audio, FeatureOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(audio);
        options ??= FeatureOptions.Default;

        float[] mono = Normalize(audio.ToMono());
        StftResult stft = Stft.Compute(mono, options.FrameSize, options.Hop, audio.SampleRate);
        var bank = CochlearFilterBank.Create(stft.FftSize, audio.SampleRate, options.FilterCount, options.FMin);

        var frames = new double[stft.FrameCount][];
        for (int f = 0; f < stft.FrameCount; f++)
        {
            double[] energies = bank.Apply(stft.Magnitudes[f]);
            for (int i = 0; i < energies.Length; i++)
            {
                // Log compression mirrors the ear's roughly logarithmic loudness response.
                energies[i] = Math.Log10(energies[i] + 1e-9);
            }

            frames[f] = energies;
        }

        return new BandFeatures(frames, bank.CenterFrequencies, options.FrameSize, options.Hop, audio.SampleRate);
    }

    /// <summary>
    /// Computes a fixed-length summary vector: per-band mean and temporal standard
    /// deviation of the cochlear energies, plus global spectral-shape descriptors.
    /// </summary>
    public static FeatureSummary Summarize(AudioBuffer audio, FeatureOptions? options = null)
    {
        options ??= FeatureOptions.Default;
        BandFeatures bands = ExtractBands(audio, options);

        int filters = bands.FilterCount;
        var names = new List<string>();
        var values = new List<double>();

        // Per-band statistics over time.
        var bandMean = new double[filters];
        var bandStd = new double[filters];
        if (bands.FrameCount > 0)
        {
            for (int b = 0; b < filters; b++)
            {
                double sum = 0.0;
                for (int f = 0; f < bands.FrameCount; f++)
                {
                    sum += bands.FrameEnergies[f][b];
                }

                double mean = sum / bands.FrameCount;
                double varSum = 0.0;
                for (int f = 0; f < bands.FrameCount; f++)
                {
                    double d = bands.FrameEnergies[f][b] - mean;
                    varSum += d * d;
                }

                bandMean[b] = mean;
                bandStd[b] = Math.Sqrt(varSum / bands.FrameCount);
            }
        }

        for (int b = 0; b < filters; b++)
        {
            names.Add($"band{b:D2}_mean");
            values.Add(bandMean[b]);
        }

        for (int b = 0; b < filters; b++)
        {
            names.Add($"band{b:D2}_std");
            values.Add(bandStd[b]);
        }

        // Global descriptors derived from the magnitude spectra.
        float[] mono = Normalize(audio.ToMono());
        StftResult stft = Stft.Compute(mono, options.FrameSize, options.Hop, audio.SampleRate);
        (double centroidMean, double centroidStd, double flatnessMean) = SpectralShape(stft);

        names.Add("spectral_centroid_mean_hz");
        values.Add(centroidMean);
        names.Add("spectral_centroid_std_hz");
        values.Add(centroidStd);
        names.Add("spectral_flatness_mean");
        values.Add(flatnessMean);
        names.Add("rms");
        values.Add(Rms(mono));
        names.Add("zero_crossing_rate");
        values.Add(ZeroCrossingRate(mono));

        return new FeatureSummary(names, values.ToArray());
    }

    /// <summary>Peak-normalizes a signal to unit amplitude (no-op for silence).</summary>
    public static float[] Normalize(float[] signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        float peak = 0f;
        foreach (float s in signal)
        {
            float a = Math.Abs(s);
            if (a > peak)
            {
                peak = a;
            }
        }

        if (peak <= 0f)
        {
            return (float[])signal.Clone();
        }

        var result = new float[signal.Length];
        for (int i = 0; i < signal.Length; i++)
        {
            result[i] = signal[i] / peak;
        }

        return result;
    }

    private static (double centroidMean, double centroidStd, double flatnessMean) SpectralShape(StftResult stft)
    {
        if (stft.FrameCount == 0)
        {
            return (0.0, 0.0, 0.0);
        }

        var centroids = new double[stft.FrameCount];
        double flatnessSum = 0.0;
        for (int f = 0; f < stft.FrameCount; f++)
        {
            double[] mag = stft.Magnitudes[f];
            double weighted = 0.0;
            double total = 0.0;
            double logSum = 0.0;
            double arithSum = 0.0;
            int n = mag.Length;
            for (int bin = 0; bin < n; bin++)
            {
                double power = mag[bin] * mag[bin];
                weighted += stft.BinFrequency(bin) * power;
                total += power;
                logSum += Math.Log(power + 1e-12);
                arithSum += power + 1e-12;
            }

            centroids[f] = total > 0 ? weighted / total : 0.0;
            double geoMean = Math.Exp(logSum / n);
            double arithMean = arithSum / n;
            flatnessSum += arithMean > 0 ? geoMean / arithMean : 0.0;
        }

        double meanC = 0.0;
        foreach (double c in centroids)
        {
            meanC += c;
        }

        meanC /= centroids.Length;
        double varC = 0.0;
        foreach (double c in centroids)
        {
            double d = c - meanC;
            varC += d * d;
        }

        double stdC = Math.Sqrt(varC / centroids.Length);
        return (meanC, stdC, flatnessSum / stft.FrameCount);
    }

    private static double Rms(float[] signal)
    {
        if (signal.Length == 0)
        {
            return 0.0;
        }

        double sum = 0.0;
        foreach (float s in signal)
        {
            sum += (double)s * s;
        }

        return Math.Sqrt(sum / signal.Length);
    }

    private static double ZeroCrossingRate(float[] signal)
    {
        if (signal.Length < 2)
        {
            return 0.0;
        }

        int crossings = 0;
        for (int i = 1; i < signal.Length; i++)
        {
            if ((signal[i - 1] >= 0f) != (signal[i] >= 0f))
            {
                crossings++;
            }
        }

        return (double)crossings / (signal.Length - 1);
    }
}
