using AudioResearch.Core.Audio;
using AudioResearch.Core.Dsp;

namespace AudioResearch.Core.Features;

/// <summary>Options for the MFCC front-end.</summary>
public sealed record MfccOptions(int FrameSize = 512, int Hop = 256, int MelFilters = 26, int CoefficientCount = 13)
{
    public static MfccOptions Default { get; } = new();
}

/// <summary>
/// Computes Mel-Frequency Cepstral Coefficients (MFCCs): mel filter bank → log →
/// DCT-II → keep the first coefficients. This is the classic speech front-end and
/// is a fairer feature set for tasks like spoken-digit recognition than the
/// generic spectral summary.
/// </summary>
public static class MfccExtractor
{
    /// <summary>Per-frame MFCC vectors (length = CoefficientCount each).</summary>
    public static double[][] ExtractFrames(AudioBuffer audio, MfccOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(audio);
        options ??= MfccOptions.Default;

        float[] mono = FeatureExtractor.Normalize(audio.ToMono());
        StftResult stft = Stft.Compute(mono, options.FrameSize, options.Hop, audio.SampleRate);
        var bank = MelFilterBank.Create(stft.FftSize, audio.SampleRate, options.MelFilters);

        var frames = new double[stft.FrameCount][];
        for (int f = 0; f < stft.FrameCount; f++)
        {
            double[] mel = bank.Apply(stft.Magnitudes[f]);
            for (int m = 0; m < mel.Length; m++)
            {
                mel[m] = Math.Log10(mel[m] + 1e-9);
            }

            double[] cepstrum = Dct.TransformII(mel);
            var coeffs = new double[options.CoefficientCount];
            Array.Copy(cepstrum, coeffs, Math.Min(options.CoefficientCount, cepstrum.Length));
            frames[f] = coeffs;
        }

        return frames;
    }

    /// <summary>
    /// Fixed-length MFCC summary: per-coefficient mean and std, plus the same for
    /// the first temporal difference (delta) coefficients. Length = 4 · CoefficientCount.
    /// </summary>
    public static FeatureSummary Summarize(AudioBuffer audio, MfccOptions? options = null)
    {
        options ??= MfccOptions.Default;
        double[][] frames = ExtractFrames(audio, options);
        int c = options.CoefficientCount;

        // First-difference (delta) frames; first delta is zero.
        var deltas = new double[frames.Length][];
        for (int f = 0; f < frames.Length; f++)
        {
            var d = new double[c];
            if (f > 0)
            {
                for (int i = 0; i < c; i++)
                {
                    d[i] = frames[f][i] - frames[f - 1][i];
                }
            }

            deltas[f] = d;
        }

        var names = new List<string>();
        var values = new List<double>();

        AddStats(names, values, frames, c, "mfcc{0:D2}_mean", "mfcc{0:D2}_std");
        AddStats(names, values, deltas, c, "dmfcc{0:D2}_mean", "dmfcc{0:D2}_std");

        return new FeatureSummary(names, values.ToArray());
    }

    private static void AddStats(List<string> names, List<double> values, double[][] frames, int c, string meanFmt, string stdFmt)
    {
        var mean = new double[c];
        var std = new double[c];
        if (frames.Length > 0)
        {
            for (int i = 0; i < c; i++)
            {
                double sum = 0.0;
                for (int f = 0; f < frames.Length; f++)
                {
                    sum += frames[f][i];
                }

                double mu = sum / frames.Length;
                double varSum = 0.0;
                for (int f = 0; f < frames.Length; f++)
                {
                    double diff = frames[f][i] - mu;
                    varSum += diff * diff;
                }

                mean[i] = mu;
                std[i] = Math.Sqrt(varSum / frames.Length);
            }
        }

        for (int i = 0; i < c; i++)
        {
            names.Add(string.Format(meanFmt, i));
            values.Add(mean[i]);
        }

        for (int i = 0; i < c; i++)
        {
            names.Add(string.Format(stdFmt, i));
            values.Add(std[i]);
        }
    }
}
