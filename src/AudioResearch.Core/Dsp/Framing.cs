namespace AudioResearch.Core.Dsp;

/// <summary>Splits a signal into overlapping frames.</summary>
public static class Framing
{
    /// <summary>
    /// Number of complete frames produced for the given signal length. Trailing
    /// samples that do not fill a whole frame are dropped (no partial frames).
    /// </summary>
    public static int FrameCount(int signalLength, int frameSize, int hop)
    {
        Validate(frameSize, hop);
        if (signalLength < frameSize)
        {
            return 0;
        }

        return 1 + (signalLength - frameSize) / hop;
    }

    /// <summary>
    /// Splits <paramref name="signal"/> into overlapping frames of length
    /// <paramref name="frameSize"/> advancing by <paramref name="hop"/> samples.
    /// Returns a jagged array of copied frames.
    /// </summary>
    public static double[][] Split(IReadOnlyList<float> signal, int frameSize, int hop)
    {
        ArgumentNullException.ThrowIfNull(signal);
        int count = FrameCount(signal.Count, frameSize, hop);
        var frames = new double[count][];
        for (int f = 0; f < count; f++)
        {
            var frame = new double[frameSize];
            int start = f * hop;
            for (int i = 0; i < frameSize; i++)
            {
                frame[i] = signal[start + i];
            }

            frames[f] = frame;
        }

        return frames;
    }

    /// <summary>Applies a window in place to a frame (element-wise multiply).</summary>
    public static double[] ApplyWindow(double[] frame, double[] window)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(window);
        if (frame.Length != window.Length)
        {
            throw new ArgumentException("Frame and window lengths must match.");
        }

        var result = new double[frame.Length];
        for (int i = 0; i < frame.Length; i++)
        {
            result[i] = frame[i] * window[i];
        }

        return result;
    }

    private static void Validate(int frameSize, int hop)
    {
        if (frameSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameSize), "Frame size must be positive.");
        }

        if (hop <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hop), "Hop must be positive.");
        }
    }
}
