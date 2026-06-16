using AudioResearch.Core.Dsp;

namespace AudioResearch.Core.Features;

/// <summary>
/// A cochlear-INSPIRED triangular filter bank.
///
/// The basilar membrane resolves frequency on an approximately logarithmic
/// (tonotopic) axis, with finer resolution at low frequencies. This class
/// approximates that behaviour by spacing triangular band-pass filters evenly on
/// the ERB-rate scale (Glasberg &amp; Moore, 1990). It is a coarse engineering
/// approximation for feature extraction only -- NOT a validated auditory model
/// and not suitable for any clinical or perceptual claim.
/// </summary>
public sealed class CochlearFilterBank
{
    private readonly double[][] _weights; // [filter][bin]

    public int FilterCount { get; }

    public int FftSize { get; }

    public int SampleRate { get; }

    /// <summary>Approximate center frequency (Hz) of each filter.</summary>
    public double[] CenterFrequencies { get; }

    private CochlearFilterBank(double[][] weights, double[] centers, int fftSize, int sampleRate)
    {
        _weights = weights;
        CenterFrequencies = centers;
        FilterCount = weights.Length;
        FftSize = fftSize;
        SampleRate = sampleRate;
    }

    /// <summary>Converts a frequency in Hz to its ERB-rate (number of ERBs).</summary>
    public static double HzToErbRate(double hz) => 21.4 * Math.Log10(1.0 + 0.00437 * hz);

    /// <summary>Converts an ERB-rate back to frequency in Hz.</summary>
    public static double ErbRateToHz(double erb) => (Math.Pow(10.0, erb / 21.4) - 1.0) / 0.00437;

    /// <summary>
    /// Builds a filter bank with <paramref name="filterCount"/> triangular filters
    /// spaced on the ERB-rate scale between <paramref name="fMin"/> and
    /// <paramref name="fMax"/> (defaults to Nyquist).
    /// </summary>
    public static CochlearFilterBank Create(int fftSize, int sampleRate, int filterCount, double fMin = 50.0, double fMax = 0.0)
    {
        if (filterCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(filterCount), "Filter count must be positive.");
        }

        if (fMax <= 0.0)
        {
            fMax = sampleRate / 2.0;
        }

        int bins = fftSize / 2 + 1;

        // filterCount filters need filterCount + 2 ERB-spaced edge points.
        double erbMin = HzToErbRate(fMin);
        double erbMax = HzToErbRate(fMax);
        var edgesHz = new double[filterCount + 2];
        for (int i = 0; i < edgesHz.Length; i++)
        {
            double erb = erbMin + (erbMax - erbMin) * i / (filterCount + 1);
            edgesHz[i] = ErbRateToHz(erb);
        }

        var centers = new double[filterCount];
        var weights = new double[filterCount][];
        for (int f = 0; f < filterCount; f++)
        {
            double lower = edgesHz[f];
            double center = edgesHz[f + 1];
            double upper = edgesHz[f + 2];
            centers[f] = center;

            var row = new double[bins];
            for (int bin = 0; bin < bins; bin++)
            {
                double freq = Fourier.BinFrequency(bin, fftSize, sampleRate);
                double w = 0.0;
                if (freq >= lower && freq <= center && center > lower)
                {
                    w = (freq - lower) / (center - lower);
                }
                else if (freq > center && freq <= upper && upper > center)
                {
                    w = (upper - freq) / (upper - center);
                }

                row[bin] = w;
            }

            weights[f] = row;
        }

        return new CochlearFilterBank(weights, centers, fftSize, sampleRate);
    }

    /// <summary>
    /// Applies the filter bank to a single magnitude spectrum, returning the
    /// weighted energy (sum of weight * magnitude^2) in each filter.
    /// </summary>
    public double[] Apply(double[] magnitude)
    {
        ArgumentNullException.ThrowIfNull(magnitude);
        var output = new double[FilterCount];
        for (int f = 0; f < FilterCount; f++)
        {
            double[] row = _weights[f];
            int limit = Math.Min(row.Length, magnitude.Length);
            double sum = 0.0;
            for (int bin = 0; bin < limit; bin++)
            {
                double m = magnitude[bin];
                sum += row[bin] * m * m;
            }

            output[f] = sum;
        }

        return output;
    }
}
