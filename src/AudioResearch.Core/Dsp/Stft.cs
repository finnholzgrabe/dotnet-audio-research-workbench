namespace AudioResearch.Core.Dsp;

/// <summary>Result of a short-time Fourier transform.</summary>
public sealed class StftResult
{
    /// <summary>Magnitude spectra, one row per frame, each of length BinCount.</summary>
    public double[][] Magnitudes { get; }

    /// <summary>Number of FFT bins per frame (fftSize/2 + 1).</summary>
    public int BinCount { get; }

    /// <summary>FFT size used (power of two &gt;= frame size).</summary>
    public int FftSize { get; }

    /// <summary>Sample rate of the source audio.</summary>
    public int SampleRate { get; }

    public StftResult(double[][] magnitudes, int binCount, int fftSize, int sampleRate)
    {
        Magnitudes = magnitudes;
        BinCount = binCount;
        FftSize = fftSize;
        SampleRate = sampleRate;
    }

    public int FrameCount => Magnitudes.Length;

    /// <summary>Center frequency in Hz of a given FFT bin.</summary>
    public double BinFrequency(int bin) => Fourier.BinFrequency(bin, FftSize, SampleRate);
}

/// <summary>Computes a windowed short-time Fourier transform (magnitude only).</summary>
public static class Stft
{
    /// <summary>
    /// Frames the signal, applies a Hann window, and computes the magnitude
    /// spectrum of each frame.
    /// </summary>
    public static StftResult Compute(IReadOnlyList<float> signal, int frameSize, int hop, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(signal);
        double[][] frames = Framing.Split(signal, frameSize, hop);
        double[] window = Windows.Hann(frameSize);

        int fftSize = Fourier.NextPowerOfTwo(frameSize);
        int bins = fftSize / 2 + 1;
        var magnitudes = new double[frames.Length][];
        for (int f = 0; f < frames.Length; f++)
        {
            double[] windowed = Framing.ApplyWindow(frames[f], window);
            magnitudes[f] = Fourier.MagnitudeSpectrum(windowed);
        }

        return new StftResult(magnitudes, bins, fftSize, sampleRate);
    }
}
