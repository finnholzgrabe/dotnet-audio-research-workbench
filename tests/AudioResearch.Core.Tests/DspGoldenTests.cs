using System.Numerics;
using AudioResearch.Core.Dsp;
using AudioResearch.Core.Features;
using Xunit;

namespace AudioResearch.Core.Tests;

/// <summary>
/// "Golden" tests pinned to analytic ground truth, not just "the code runs".
/// These assert mathematical properties the DSP must satisfy.
/// </summary>
public class DspGoldenTests
{
    [Fact]
    public void Parseval_TimeEnergyEqualsFrequencyEnergy()
    {
        // Σ|x[n]|² == (1/N) Σ|X[k]|²  (Parseval's theorem for the DFT).
        const int n = 256;
        var rng = new Random(123);
        var time = new Complex[n];
        double timeEnergy = 0.0;
        for (int i = 0; i < n; i++)
        {
            double v = rng.NextDouble() * 2.0 - 1.0;
            time[i] = new Complex(v, 0.0);
            timeEnergy += v * v;
        }

        Fourier.Transform(time);
        double freqEnergy = 0.0;
        foreach (Complex c in time)
        {
            freqEnergy += c.Magnitude * c.Magnitude;
        }

        freqEnergy /= n;
        Assert.Equal(timeEnergy, freqEnergy, 6);
    }

    [Fact]
    public void BinAlignedSine_HasMagnitudeAmplitudeTimesNOverTwo()
    {
        // For a non-windowed, bin-aligned real sine of amplitude A and length N,
        // the magnitude at its bin equals A*N/2.
        const int n = 1024;
        const int m = 64;          // integer bin -> exactly bin-aligned
        const double amplitude = 0.5;
        var frame = new double[n];
        for (int i = 0; i < n; i++)
        {
            frame[i] = amplitude * Math.Sin(2.0 * Math.PI * m * i / n);
        }

        double[] mag = Fourier.MagnitudeSpectrum(frame);
        double expected = amplitude * n / 2.0;
        Assert.Equal(expected, mag[m], 3);

        // Energy must be concentrated at that bin: neighbours are ~zero.
        Assert.True(mag[m - 1] < expected * 1e-6);
        Assert.True(mag[m + 1] < expected * 1e-6);
    }

    [Fact]
    public void FilterBank_IsPartitionOfUnity_InInterior()
    {
        // The ERB triangular filters should sum to 1 at every frequency strictly
        // between the first and last filter centers. Probe with one-hot spectra:
        // Apply squares the magnitude, so a unit impulse yields exactly the
        // per-bin weight, and summing across filters gives the partition value.
        const int fftSize = 512;
        const int sampleRate = 16000;
        var bank = CochlearFilterBank.Create(fftSize, sampleRate, filterCount: 24);
        int bins = fftSize / 2 + 1;
        double firstCenter = bank.CenterFrequencies[0];
        double lastCenter = bank.CenterFrequencies[^1];

        int checkedBins = 0;
        for (int b = 0; b < bins; b++)
        {
            double freq = Fourier.BinFrequency(b, fftSize, sampleRate);
            if (freq <= firstCenter + 1.0 || freq >= lastCenter - 1.0)
            {
                continue; // outside the partitioned interior
            }

            var oneHot = new double[bins];
            oneHot[b] = 1.0;
            double sum = bank.Apply(oneHot).Sum();
            Assert.Equal(1.0, sum, 6);
            checkedBins++;
        }

        Assert.True(checkedBins > 10, "expected several interior bins to verify");
    }

    [Fact]
    public void Fft_RoundTripsThroughInverse()
    {
        const int n = 64;
        var rng = new Random(7);
        var original = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            original[i] = new Complex(rng.NextDouble(), rng.NextDouble());
        }

        var work = (Complex[])original.Clone();
        Fourier.Transform(work, inverse: false);
        Fourier.Transform(work, inverse: true);
        for (int i = 0; i < n; i++)
        {
            // Inverse is unscaled, so divide by N to recover the original.
            Assert.Equal(original[i].Real, work[i].Real / n, 9);
            Assert.Equal(original[i].Imaginary, work[i].Imaginary / n, 9);
        }
    }
}
