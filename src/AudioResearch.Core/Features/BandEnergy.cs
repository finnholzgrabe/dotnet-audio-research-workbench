using AudioResearch.Core.Dsp;

namespace AudioResearch.Core.Features;

/// <summary>Linear (equal-width) frequency-band energy extraction.</summary>
public static class BandEnergy
{
    /// <summary>
    /// Integrates magnitude-spectrum energy into <paramref name="bandCount"/>
    /// equal-width frequency bands spanning 0..Nyquist. Energy is the sum of
    /// squared magnitudes of the bins falling in each band.
    /// </summary>
    public static double[] FromMagnitude(double[] magnitude, int fftSize, int sampleRate, int bandCount)
    {
        ArgumentNullException.ThrowIfNull(magnitude);
        if (bandCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandCount), "Band count must be positive.");
        }

        double nyquist = sampleRate / 2.0;
        double bandWidth = nyquist / bandCount;
        var energies = new double[bandCount];
        for (int bin = 0; bin < magnitude.Length; bin++)
        {
            double freq = Fourier.BinFrequency(bin, fftSize, sampleRate);
            int band = (int)(freq / bandWidth);
            if (band >= bandCount)
            {
                band = bandCount - 1;
            }

            energies[band] += magnitude[bin] * magnitude[bin];
        }

        return energies;
    }
}
