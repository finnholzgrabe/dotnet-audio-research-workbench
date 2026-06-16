using AudioResearch.Core.Audio;
using Xunit;

namespace AudioResearch.Core.Tests;

public class AudioIoTests
{
    [Fact]
    public void Sine_HasExpectedLengthAndRate()
    {
        AudioBuffer tone = SignalGenerator.Sine(440.0, 0.5, 16000);

        Assert.Equal(16000, tone.SampleRate);
        Assert.Equal(1, tone.Channels);
        Assert.Equal(8000, tone.FrameCount);
        Assert.Equal(0.5, tone.Duration, 6);
        Assert.True(tone.Peak() > 0.5f);
    }

    [Fact]
    public void WhiteNoise_IsDeterministicForSameSeed()
    {
        AudioBuffer a = SignalGenerator.WhiteNoise(0.1, 16000, 0.8, seed: 7);
        AudioBuffer b = SignalGenerator.WhiteNoise(0.1, 16000, 0.8, seed: 7);
        AudioBuffer c = SignalGenerator.WhiteNoise(0.1, 16000, 0.8, seed: 8);

        Assert.Equal(a.Samples, b.Samples);
        Assert.NotEqual(a.Samples, c.Samples);
    }

    [Fact]
    public void WavRoundTrip_PreservesSamplesWithinQuantization()
    {
        AudioBuffer original = SignalGenerator.Sine(440.0, 0.05, 16000);
        using var ms = new MemoryStream();
        WavFile.Write(ms, original);
        ms.Position = 0;
        AudioBuffer loaded = WavFile.Read(ms);

        Assert.Equal(original.SampleRate, loaded.SampleRate);
        Assert.Equal(original.Channels, loaded.Channels);
        Assert.Equal(original.FrameCount, loaded.FrameCount);
        for (int i = 0; i < original.Samples.Length; i++)
        {
            // 16-bit quantization step is ~3e-5; allow a small tolerance.
            Assert.True(Math.Abs(original.Samples[i] - loaded.Samples[i]) < 1e-3);
        }
    }

    [Fact]
    public void WavRoundTrip_PreservesStereoLayout()
    {
        // Build a 2-channel buffer with distinct L/R values.
        var samples = new float[] { 0.5f, -0.5f, 0.25f, -0.25f };
        var stereo = new AudioBuffer(samples, 8000, 2);

        using var ms = new MemoryStream();
        WavFile.Write(ms, stereo);
        ms.Position = 0;
        AudioBuffer loaded = WavFile.Read(ms);

        Assert.Equal(2, loaded.Channels);
        Assert.Equal(2, loaded.FrameCount);
        Assert.True(loaded.Samples[0] > loaded.Samples[1]); // L > R preserved
    }

    [Fact]
    public void Read_OnNonRiffData_Throws()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        Assert.Throws<InvalidDataException>(() => WavFile.Read(ms));
    }

    [Fact]
    public void ToMono_AveragesChannels()
    {
        var stereo = new AudioBuffer(new float[] { 1.0f, 0.0f, 0.5f, 0.5f }, 8000, 2);
        float[] mono = stereo.ToMono();

        Assert.Equal(2, mono.Length);
        Assert.Equal(0.5f, mono[0], 0.0001f);
        Assert.Equal(0.5f, mono[1], 0.0001f);
    }
}
