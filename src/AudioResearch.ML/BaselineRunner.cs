namespace AudioResearch.ML;

/// <summary>Per-class precision/recall counts and the overall accuracy of a run.</summary>
public sealed class BaselineReport
{
    public required string ModelType { get; init; }

    public required int K { get; init; }

    public required int FeatureCount { get; init; }

    public required IReadOnlyList<string> FeatureNames { get; init; }

    public required int TrainCount { get; init; }

    public required int TestCount { get; init; }

    public required double TestFraction { get; init; }

    public required int Seed { get; init; }

    public required double Accuracy { get; init; }

    public string SplitStrategy { get; init; } = "stratified-random";

    public required IReadOnlyList<string> Classes { get; init; }

    /// <summary>Confusion matrix indexed [actualClass][predictedClass].</summary>
    public required int[][] Confusion { get; init; }

    public required IReadOnlyDictionary<string, int> ClassCounts { get; init; }
}

/// <summary>
/// Trains and evaluates the k-NN baseline on labeled feature vectors using a
/// deterministic, seeded, stratified train/test split.
/// </summary>
public static class BaselineRunner
{
    public static BaselineReport Run(
        IReadOnlyList<LabeledVector> samples,
        IReadOnlyList<string> featureNames,
        int k = 3,
        double testFraction = 0.3,
        int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(featureNames);
        if (samples.Count == 0)
        {
            throw new ArgumentException("Dataset must not be empty.", nameof(samples));
        }

        if (testFraction is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(testFraction), "Test fraction must be in (0, 1).");
        }

        // Stratified split: shuffle within each class with a seeded RNG so the
        // same proportion of every class lands in the test set, reproducibly.
        var rng = new Random(seed);
        var train = new List<LabeledVector>();
        var test = new List<LabeledVector>();

        foreach (var group in samples.GroupBy(s => s.Label, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            List<LabeledVector> items = group.ToList();
            Shuffle(items, rng);
            int testSize = Math.Max(1, (int)Math.Round(items.Count * testFraction));
            testSize = Math.Min(testSize, items.Count - 1); // always keep at least one to train on
            for (int i = 0; i < items.Count; i++)
            {
                (i < testSize ? test : train).Add(items[i]);
            }
        }

        return Evaluate(train, test, samples, featureNames, k, testFraction, seed, "stratified-random");
    }

    /// <summary>
    /// Evaluates the baseline on an explicit, predefined train/test split (e.g. a
    /// frequency-regime holdout for measuring generalization). No internal split.
    /// </summary>
    public static BaselineReport RunWithSplit(
        IReadOnlyList<LabeledVector> train,
        IReadOnlyList<LabeledVector> test,
        IReadOnlyList<string> featureNames,
        int k = 3,
        int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(train);
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(featureNames);
        if (train.Count == 0)
        {
            throw new ArgumentException("Training set must not be empty.", nameof(train));
        }

        var all = train.Concat(test).ToList();
        double frac = all.Count > 0 ? (double)test.Count / all.Count : 0.0;
        return Evaluate(train, test, all, featureNames, k, frac, seed, "regime-holdout");
    }

    private static BaselineReport Evaluate(
        IReadOnlyList<LabeledVector> train,
        IReadOnlyList<LabeledVector> test,
        IReadOnlyList<LabeledVector> all,
        IReadOnlyList<string> featureNames,
        int k,
        double testFraction,
        int seed,
        string splitStrategy)
    {
        var classes = all
            .Select(s => s.Label)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var index = classes.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i, StringComparer.Ordinal);

        var model = new KnnClassifier(k);
        model.Fit(train);

        var confusion = new int[classes.Count][];
        for (int i = 0; i < classes.Count; i++)
        {
            confusion[i] = new int[classes.Count];
        }

        int correct = 0;
        foreach (LabeledVector sample in test)
        {
            string predicted = model.Predict(sample.Features);
            confusion[index[sample.Label]][index[predicted]]++;
            if (string.Equals(predicted, sample.Label, StringComparison.Ordinal))
            {
                correct++;
            }
        }

        var classCounts = all
            .GroupBy(s => s.Label, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return new BaselineReport
        {
            ModelType = "k-nearest-neighbours (z-score standardized, Euclidean)",
            K = k,
            FeatureCount = featureNames.Count,
            FeatureNames = featureNames,
            TrainCount = train.Count,
            TestCount = test.Count,
            TestFraction = testFraction,
            Seed = seed,
            Accuracy = test.Count > 0 ? (double)correct / test.Count : 0.0,
            SplitStrategy = splitStrategy,
            Classes = classes,
            Confusion = confusion,
            ClassCounts = classCounts,
        };
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
