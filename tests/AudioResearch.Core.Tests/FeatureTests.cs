using AudioResearch.Core.Audio;
using AudioResearch.Core.Features;
using Xunit;

namespace AudioResearch.Core.Tests;

public class FeatureTests
{
    [Fact]
    public void ErbConversion_RoundTrips()
    {
        foreach (double hz in new[] { 100.0, 500.0, 1000.0, 4000.0 })
        {
            double erb = CochlearFilterBank.HzToErbRate(hz);
            double back = CochlearFilterBank.ErbRateToHz(erb);
            Assert.Equal(hz, back, 3);
        }
    }

    [Fact]
    public void FilterBank_CentersAreMonotonicAndWithinRange()
    {
        var bank = CochlearFilterBank.Create(fftSize: 512, sampleRate: 16000, filterCount: 24);

        Assert.Equal(24, bank.FilterCount);
        for (int i = 1; i < bank.CenterFrequencies.Length; i++)
        {
            Assert.True(bank.CenterFrequencies[i] > bank.CenterFrequencies[i - 1]);
        }

        Assert.All(bank.CenterFrequencies, f => Assert.InRange(f, 0.0, 8000.0));
    }

    [Fact]
    public void LowSineTone_ConcentratesEnergyInLowBands()
    {
        AudioBuffer tone = SignalGenerator.Sine(300.0, 0.5, 16000);
        BandFeatures bands = FeatureExtractor.ExtractBands(tone, new FeatureOptions(FilterCount: 16));

        // Average log-energy per band across all frames.
        var avg = new double[bands.FilterCount];
        for (int b = 0; b < bands.FilterCount; b++)
        {
            double sum = 0;
            for (int f = 0; f < bands.FrameCount; f++)
            {
                sum += bands.FrameEnergies[f][b];
            }

            avg[b] = sum / bands.FrameCount;
        }

        int peakBand = Array.IndexOf(avg, avg.Max());
        double peakFreq = bands.CenterFrequencies[peakBand];

        // A 300 Hz tone should peak well below 1 kHz.
        Assert.True(peakFreq < 1000.0, $"peak band center was {peakFreq} Hz");
    }

    [Fact]
    public void Summary_HasStableSchemaAndLength()
    {
        AudioBuffer tone = SignalGenerator.Sine(440.0, 0.5, 16000);
        FeatureSummary summary = FeatureExtractor.Summarize(tone, new FeatureOptions(FilterCount: 24));

        // 24 means + 24 stds + 5 global descriptors = 53.
        Assert.Equal(53, summary.Vector.Length);
        Assert.Equal(summary.Vector.Length, summary.Names.Count);
        Assert.Contains("spectral_centroid_mean_hz", summary.Names);
        Assert.Contains("band00_mean", summary.Names);
        Assert.All(summary.Vector, v => Assert.False(double.IsNaN(v)));
    }

    [Fact]
    public void Summary_IsDeterministic()
    {
        AudioBuffer tone = SignalGenerator.Sine(440.0, 0.3, 16000);
        double[] a = FeatureExtractor.Summarize(tone).Vector;
        double[] b = FeatureExtractor.Summarize(tone).Vector;
        Assert.Equal(a, b);
    }

    [Fact]
    public void ChirpHasHigherCentroidSpreadThanTone()
    {
        AudioBuffer tone = SignalGenerator.Sine(1000.0, 0.5, 16000);
        AudioBuffer chirp = SignalGenerator.Chirp(200.0, 4000.0, 0.5, 16000);

        FeatureSummary toneS = FeatureExtractor.Summarize(tone);
        FeatureSummary chirpS = FeatureExtractor.Summarize(chirp);

        int idx = toneS.Names.ToList().IndexOf("spectral_centroid_std_hz");
        Assert.True(chirpS.Vector[idx] > toneS.Vector[idx]);
    }
}
