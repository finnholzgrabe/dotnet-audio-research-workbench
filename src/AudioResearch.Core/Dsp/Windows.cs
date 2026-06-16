namespace AudioResearch.Core.Dsp;

/// <summary>Analysis window functions.</summary>
public static class Windows
{
    /// <summary>
    /// Returns a periodic Hann window of length <paramref name="length"/>.
    /// The periodic form (divisor N rather than N-1) is the convention used for
    /// spectral analysis and STFT overlap-add.
    /// </summary>
    public static double[] Hann(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Window length must be positive.");
        }

        var window = new double[length];
        if (length == 1)
        {
            window[0] = 1.0;
            return window;
        }

        for (int i = 0; i < length; i++)
        {
            window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / length));
        }

        return window;
    }
}
