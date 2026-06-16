using AudioResearch.Core.Audio;
using AudioResearch.Core.Experiments;
using Xunit;

namespace AudioResearch.Core.Tests;

public class AugmentTests
{
    [Theory]
    [InlineData(0.0, NoiseColor.White)]
    [InlineData(10.0, NoiseColor.White)]
    [InlineData(20.0, NoiseColor.Pink)]
    public void AddNoiseAtSnr_ProducesRequestedSnr(double snrDb, NoiseColor color)
    {
        AudioBuffer clean = SignalGenerator.Sine(440.0, 0.2, 16000);
        AudioBuffer noisy = AudioAugment.AddNoiseAtSnr(clean, snrDb, color, seed: 5);

        // Recover the added noise and measure the actual SNR.
        var noise = new float[clean.Samples.Length];
        for (int i = 0; i < noise.Length; i++)
        {
            noise[i] = noisy.Samples[i] - clean.Samples[i];
        }

        double signalPower = AudioAugment.Power(clean.Samples);
        double noisePower = AudioAugment.Power(noise);
        double measuredSnr = 10.0 * Math.Log10(signalPower / noisePower);

        Assert.Equal(snrDb, measuredSnr, 1); // within ~0.1 dB
    }

    [Fact]
    public void AddNoiseAtSnr_IsDeterministicForSameSeed()
    {
        AudioBuffer clean = SignalGenerator.Sine(440.0, 0.1, 16000);
        AudioBuffer a = AudioAugment.AddNoiseAtSnr(clean, 6.0, NoiseColor.White, seed: 42);
        AudioBuffer b = AudioAugment.AddNoiseAtSnr(clean, 6.0, NoiseColor.White, seed: 42);
        Assert.Equal(a.Samples, b.Samples);
    }

    [Fact]
    public void ApplyGain_ScalesRms()
    {
        AudioBuffer clean = SignalGenerator.Sine(440.0, 0.1, 16000);
        AudioBuffer loud = AudioAugment.ApplyGain(clean, 2.0);
        Assert.Equal(4.0 * AudioAugment.Power(clean.Samples), AudioAugment.Power(loud.Samples), 6);
    }

    [Fact]
    public void Clip_BoundsAmplitude()
    {
        AudioBuffer clean = SignalGenerator.Sine(440.0, 0.05, 16000, amplitude: 0.9);
        AudioBuffer clipped = AudioAugment.Clip(clean, 0.3);
        Assert.True(clipped.Peak() <= 0.3f + 1e-6f);
    }

    [Fact]
    public void BuildVaried_IsDeterministicAndBalanced()
    {
        IReadOnlyList<LabeledAudio> a = DatasetBuilder.BuildVaried(perClass: 6, seconds: 0.2, seed: 7);
        IReadOnlyList<LabeledAudio> b = DatasetBuilder.BuildVaried(perClass: 6, seconds: 0.2, seed: 7);

        Assert.Equal(24, a.Count); // 4 classes * 6
        Assert.Equal(a[0].Audio.Samples, b[0].Audio.Samples); // deterministic
        Assert.Equal(4, a.Select(x => x.Label).Distinct().Count());
        foreach (var group in a.GroupBy(x => x.Label))
        {
            Assert.Equal(6, group.Count()); // balanced
        }
    }

    [Fact]
    public void GeneralizationSplit_HasDisjointSizesAndAllClasses()
    {
        DatasetSplit split = DatasetBuilder.BuildGeneralizationSplit(perClassTrain: 8, perClassTest: 4, seconds: 0.2);

        Assert.Equal(32, split.Train.Count); // 4 * 8
        Assert.Equal(16, split.Test.Count);  // 4 * 4
        Assert.Equal(4, split.Train.Select(x => x.Label).Distinct().Count());
        Assert.Equal(4, split.Test.Select(x => x.Label).Distinct().Count());
    }
}
