using AudioResearch.Core.Audio;
using AudioResearch.Core.Dsp;
using AudioResearch.Core.Features;
using Xunit;

namespace AudioResearch.Core.Tests;

public class MfccTests
{
    [Fact]
    public void Dct_OfConstant_ConcentratesInDcCoefficient()
    {
        var x = new[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 };
        double[] y = Dct.TransformII(x);

        Assert.Equal(4.0, y[0], 9); // sum of inputs
        for (int k = 1; k < y.Length; k++)
        {
            Assert.Equal(0.0, y[k], 9);
        }
    }

    [Fact]
    public void MelConversion_RoundTrips()
    {
        foreach (double hz in new[] { 100.0, 500.0, 1000.0, 4000.0 })
        {
            Assert.Equal(hz, MelFilterBank.MelToHz(MelFilterBank.HzToMel(hz)), 3);
        }
    }

    [Fact]
    public void MelFilterBank_IsPartitionOfUnity_InInterior()
    {
        const int fftSize = 512;
        const int sampleRate = 8000;
        var bank = MelFilterBank.Create(fftSize, sampleRate, filterCount: 26, fMin: 0.0);
        int bins = fftSize / 2 + 1;
        double first = bank.CenterFrequencies[0];
        double last = bank.CenterFrequencies[^1];

        int checkedBins = 0;
        for (int b = 0; b < bins; b++)
        {
            double freq = Fourier.BinFrequency(b, fftSize, sampleRate);
            if (freq <= first + 1.0 || freq >= last - 1.0)
            {
                continue;
            }

            var oneHot = new double[bins];
            oneHot[b] = 1.0;
            Assert.Equal(1.0, bank.Apply(oneHot).Sum(), 6);
            checkedBins++;
        }

        Assert.True(checkedBins > 10);
    }

    [Fact]
    public void MfccSummary_HasStableLengthSchemaAndIsDeterministic()
    {
        AudioBuffer tone = SignalGenerator.Sine(440.0, 0.4, 16000);
        var opts = new MfccOptions(CoefficientCount: 13);

        FeatureSummary a = MfccExtractor.Summarize(tone, opts);
        FeatureSummary b = MfccExtractor.Summarize(tone, opts);

        Assert.Equal(4 * 13, a.Vector.Length); // mean+std for mfcc and delta
        Assert.Equal(a.Vector.Length, a.Names.Count);
        Assert.Contains("mfcc00_mean", a.Names);
        Assert.Contains("dmfcc00_mean", a.Names);
        Assert.Equal(a.Vector, b.Vector); // deterministic
        Assert.All(a.Vector, v => Assert.False(double.IsNaN(v)));
    }
}
