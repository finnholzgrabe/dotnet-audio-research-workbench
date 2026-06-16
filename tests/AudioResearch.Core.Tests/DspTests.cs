using System.Numerics;
using AudioResearch.Core.Audio;
using AudioResearch.Core.Dsp;
using Xunit;

namespace AudioResearch.Core.Tests;

public class DspTests
{
    [Fact]
    public void Hann_EndpointsAreZero_AndMidpointIsOne()
    {
        double[] w = Windows.Hann(8);

        Assert.Equal(0.0, w[0], 10);
        Assert.Equal(1.0, w[4], 10); // periodic Hann peaks at N/2
        Assert.All(w, v => Assert.InRange(v, 0.0, 1.0));
    }

    [Theory]
    [InlineData(1024, 256, 256, 4)] // (1024-256)/256 + 1 = 4
    [InlineData(1000, 256, 256, 3)]
    [InlineData(100, 256, 256, 0)]  // signal shorter than frame
    public void FrameCount_MatchesFormula(int signalLength, int frameSize, int hop, int expected)
    {
        Assert.Equal(expected, Framing.FrameCount(signalLength, frameSize, hop));
    }

    [Fact]
    public void Split_ProducesContiguousOverlappingFrames()
    {
        var signal = new float[8];
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = i;
        }

        double[][] frames = Framing.Split(signal, 4, 2);

        Assert.Equal(3, frames.Length);
        Assert.Equal(new double[] { 0, 1, 2, 3 }, frames[0]);
        Assert.Equal(new double[] { 2, 3, 4, 5 }, frames[1]);
        Assert.Equal(new double[] { 4, 5, 6, 7 }, frames[2]);
    }

    [Fact]
    public void Fft_OfImpulse_IsFlat()
    {
        var buffer = new Complex[8];
        buffer[0] = Complex.One;
        Fourier.Transform(buffer);

        foreach (Complex c in buffer)
        {
            Assert.Equal(1.0, c.Magnitude, 9);
        }
    }

    [Fact]
    public void Fft_NonPowerOfTwo_Throws()
    {
        var buffer = new Complex[6];
        Assert.Throws<ArgumentException>(() => Fourier.Transform(buffer));
    }

    [Fact]
    public void MagnitudeSpectrum_OfSine_PeaksAtToneBin()
    {
        const int sampleRate = 16000;
        const double freq = 1000.0;
        AudioBuffer tone = SignalGenerator.Sine(freq, 0.064, sampleRate); // 1024 samples
        double[] frame = new double[1024];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = tone.Samples[i];
        }

        double[] mag = Fourier.MagnitudeSpectrum(frame);

        int peakBin = 0;
        double peak = 0;
        for (int i = 0; i < mag.Length; i++)
        {
            if (mag[i] > peak)
            {
                peak = mag[i];
                peakBin = i;
            }
        }

        double peakFreq = Fourier.BinFrequency(peakBin, 1024, sampleRate);
        Assert.Equal(freq, peakFreq, 0); // within one bin (15.6 Hz) rounds to 1000
    }

    [Fact]
    public void Stft_ProducesExpectedFrameAndBinCounts()
    {
        AudioBuffer tone = SignalGenerator.Sine(440.0, 0.5, 16000);
        StftResult stft = Stft.Compute(tone.ToMono(), 512, 256, 16000);

        Assert.Equal(Framing.FrameCount(8000, 512, 256), stft.FrameCount);
        Assert.Equal(512 / 2 + 1, stft.BinCount);
    }
}
