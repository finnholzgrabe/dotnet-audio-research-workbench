using System.Numerics;

namespace AudioResearch.Core.Dsp;

/// <summary>
/// A small, self-contained radix-2 Cooley-Tukey FFT and helpers for computing
/// magnitude spectra. Kept dependency-free so fixtures and tests are fully
/// deterministic and offline-buildable.
/// </summary>
public static class Fourier
{
    /// <summary>
    /// In-place iterative radix-2 FFT. <paramref name="buffer"/> length must be a
    /// power of two. Set <paramref name="inverse"/> for the inverse transform
    /// (this implementation does not scale the inverse by 1/N).
    /// </summary>
    public static void Transform(Complex[] buffer, bool inverse = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        int n = buffer.Length;
        if (n == 0)
        {
            return;
        }

        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException("FFT length must be a power of two.", nameof(buffer));
        }

        // Bit-reversal permutation.
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }
        }

        // Butterfly stages.
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = 2.0 * Math.PI / len * (inverse ? 1 : -1);
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                Complex w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    Complex u = buffer[i + k];
                    Complex v = buffer[i + k + len / 2] * w;
                    buffer[i + k] = u + v;
                    buffer[i + k + len / 2] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    /// <summary>Smallest power of two that is greater than or equal to value.</summary>
    public static int NextPowerOfTwo(int value)
    {
        if (value <= 1)
        {
            return 1;
        }

        int p = 1;
        while (p < value)
        {
            p <<= 1;
        }

        return p;
    }

    /// <summary>
    /// Computes the magnitude spectrum of a real frame. The frame is zero-padded
    /// up to the next power of two. The returned array contains the non-negative
    /// frequency bins (length = fftSize/2 + 1).
    /// </summary>
    public static double[] MagnitudeSpectrum(double[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        int fftSize = NextPowerOfTwo(frame.Length);
        var buffer = new Complex[fftSize];
        for (int i = 0; i < frame.Length; i++)
        {
            buffer[i] = new Complex(frame[i], 0.0);
        }

        Transform(buffer);

        int bins = fftSize / 2 + 1;
        var magnitude = new double[bins];
        for (int i = 0; i < bins; i++)
        {
            magnitude[i] = buffer[i].Magnitude;
        }

        return magnitude;
    }

    /// <summary>
    /// Center frequency in Hz of magnitude-spectrum bin <paramref name="bin"/>
    /// for the given FFT size and sample rate.
    /// </summary>
    public static double BinFrequency(int bin, int fftSize, int sampleRate)
    {
        return (double)bin * sampleRate / fftSize;
    }
}
