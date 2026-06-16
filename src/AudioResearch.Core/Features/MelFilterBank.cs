using AudioResearch.Core.Dsp;

namespace AudioResearch.Core.Features;

/// <summary>
/// Triangular mel-spaced filter bank, the standard front-end for MFCCs. Filters
/// are evenly spaced on the mel scale (O'Shaughnessy):
/// mel(f) = 2595 · log10(1 + f/700).
/// </summary>
public sealed class MelFilterBank
{
    private readonly double[][] _weights;

    public int FilterCount { get; }

    public double[] CenterFrequencies { get; }

    private MelFilterBank(double[][] weights, double[] centers)
    {
        _weights = weights;
        CenterFrequencies = centers;
        FilterCount = weights.Length;
    }

    public static double HzToMel(double hz) => 2595.0 * Math.Log10(1.0 + hz / 700.0);

    public static double MelToHz(double mel) => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

    public static MelFilterBank Create(int fftSize, int sampleRate, int filterCount, double fMin = 0.0, double fMax = 0.0)
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
        double melMin = HzToMel(fMin);
        double melMax = HzToMel(fMax);
        var edges = new double[filterCount + 2];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i] = MelToHz(melMin + (melMax - melMin) * i / (filterCount + 1));
        }

        var centers = new double[filterCount];
        var weights = new double[filterCount][];
        for (int f = 0; f < filterCount; f++)
        {
            double lower = edges[f];
            double center = edges[f + 1];
            double upper = edges[f + 2];
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

        return new MelFilterBank(weights, centers);
    }

    /// <summary>Returns the weighted power (Σ weight · magnitude²) in each filter.</summary>
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
