namespace AudioResearch.ML;

/// <summary>A feature vector paired with its class label.</summary>
public sealed record LabeledVector(string Label, double[] Features);

/// <summary>
/// A small, dependency-free k-nearest-neighbours classifier with z-score feature
/// standardization. Deterministic: ties in the neighbour vote are broken by label
/// ordinal so predictions never depend on iteration order.
/// </summary>
public sealed class KnnClassifier
{
    private readonly int _k;
    private LabeledVector[] _train = Array.Empty<LabeledVector>();
    private double[] _mean = Array.Empty<double>();
    private double[] _std = Array.Empty<double>();

    public KnnClassifier(int k = 3)
    {
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "k must be positive.");
        }

        _k = k;
    }

    /// <summary>Fits the classifier: stores standardized training vectors.</summary>
    public void Fit(IReadOnlyList<LabeledVector> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            throw new ArgumentException("Training set must not be empty.", nameof(samples));
        }

        int dim = samples[0].Features.Length;
        _mean = new double[dim];
        _std = new double[dim];

        foreach (LabeledVector s in samples)
        {
            if (s.Features.Length != dim)
            {
                throw new ArgumentException("All feature vectors must have equal length.");
            }

            for (int d = 0; d < dim; d++)
            {
                _mean[d] += s.Features[d];
            }
        }

        for (int d = 0; d < dim; d++)
        {
            _mean[d] /= samples.Count;
        }

        foreach (LabeledVector s in samples)
        {
            for (int d = 0; d < dim; d++)
            {
                double diff = s.Features[d] - _mean[d];
                _std[d] += diff * diff;
            }
        }

        for (int d = 0; d < dim; d++)
        {
            _std[d] = Math.Sqrt(_std[d] / samples.Count);
            if (_std[d] < 1e-9)
            {
                _std[d] = 1.0; // avoid divide-by-zero for constant features
            }
        }

        _train = samples.Select(s => new LabeledVector(s.Label, Standardize(s.Features))).ToArray();
    }

    /// <summary>Predicts the label of a single feature vector.</summary>
    public string Predict(double[] features)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (_train.Length == 0)
        {
            throw new InvalidOperationException("Classifier must be fit before prediction.");
        }

        double[] q = Standardize(features);
        int k = Math.Min(_k, _train.Length);

        // Find the k nearest by squared Euclidean distance.
        var neighbours = _train
            .Select(t => (t.Label, Dist: SquaredDistance(q, t.Features)))
            .OrderBy(x => x.Dist)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .Take(k);

        var votes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string label, double _) in neighbours)
        {
            votes[label] = votes.GetValueOrDefault(label) + 1;
        }

        return votes
            .OrderByDescending(v => v.Value)
            .ThenBy(v => v.Key, StringComparer.Ordinal)
            .First()
            .Key;
    }

    private double[] Standardize(double[] features)
    {
        var result = new double[features.Length];
        for (int d = 0; d < features.Length; d++)
        {
            result[d] = (features[d] - _mean[d]) / _std[d];
        }

        return result;
    }

    private static double SquaredDistance(double[] a, double[] b)
    {
        double sum = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            double d = a[i] - b[i];
            sum += d * d;
        }

        return sum;
    }
}
