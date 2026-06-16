namespace AudioResearch.Core.Dsp;

/// <summary>Discrete cosine transform (type II), the standard transform for MFCCs.</summary>
public static class Dct
{
    /// <summary>
    /// Computes the DCT-II of <paramref name="input"/>:
    /// X[k] = Σ_{n} x[n] · cos(π/N · (n + ½) · k). O(N²); N is small for MFCCs.
    /// </summary>
    public static double[] TransformII(double[] input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int n = input.Length;
        var output = new double[n];
        for (int k = 0; k < n; k++)
        {
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                sum += input[i] * Math.Cos(Math.PI / n * (i + 0.5) * k);
            }

            output[k] = sum;
        }

        return output;
    }
}
