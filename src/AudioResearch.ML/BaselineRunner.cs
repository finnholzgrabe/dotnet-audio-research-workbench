namespace AudioResearch.ML;

/// <summary>Precision, recall, F1, and support (test-set count) for one class.</summary>
public sealed record ClassMetrics(string Label, double Precision, double Recall, double F1, int Support);

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

    /// <summary>Unweighted mean of per-class F1 scores.</summary>
    public double MacroF1 { get; init; }

    /// <summary>Per-class precision/recall/F1/support, ordered like <see cref="Classes"/>.</summary>
    public IReadOnlyList<ClassMetrics> PerClass { get; init; } = Array.Empty<ClassMetrics>();

    public string SplitStrategy { get; init; } = "stratified-random";

    public IReadOnlyList<string> TrainGroups { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TestGroups { get; init; } = Array.Empty<string>();

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

    /// <summary>
    /// Group-aware (leave-group-out) evaluation: whole groups are assigned to
    /// either train or test, so no group (e.g. speaker) appears in both. This is
    /// the honest way to measure generalization to unseen groups.
    /// </summary>
    public static BaselineReport RunGrouped(
        IReadOnlyList<LabeledVector> samples,
        IReadOnlyList<string> featureNames,
        string groupName,
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

        if (samples.Any(s => s.Group is null))
        {
            throw new ArgumentException("Every sample must have a Group for grouped evaluation.", nameof(samples));
        }

        if (testFraction is <= 0.0 or >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(testFraction), "Test fraction must be in (0, 1).");
        }

        var groupSizes = samples
            .GroupBy(s => s.Group!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        if (groupSizes.Count < 2)
        {
            throw new ArgumentException("Grouped evaluation needs at least two distinct groups.", nameof(samples));
        }

        // Assign whole groups to the test set (seeded) until ~testFraction of the
        // samples are held out, always leaving at least one group to train on.
        var keys = groupSizes.Keys.OrderBy(g => g, StringComparer.Ordinal).ToList();
        Shuffle(keys, new Random(seed));

        var testGroups = new HashSet<string>(StringComparer.Ordinal);
        int target = (int)Math.Round(samples.Count * testFraction);
        int held = 0;
        foreach (string key in keys)
        {
            if (testGroups.Count == keys.Count - 1)
            {
                break; // keep at least one group for training
            }

            testGroups.Add(key);
            held += groupSizes[key];
            if (held >= target)
            {
                break;
            }
        }

        var train = samples.Where(s => !testGroups.Contains(s.Group!)).ToList();
        var test = samples.Where(s => testGroups.Contains(s.Group!)).ToList();
        var testGroupList = testGroups.OrderBy(g => g, StringComparer.Ordinal).ToList();
        var trainGroupList = keys.Where(g => !testGroups.Contains(g)).OrderBy(g => g, StringComparer.Ordinal).ToList();

        return Evaluate(
            train, test, samples, featureNames, k,
            (double)test.Count / samples.Count, seed, $"group-holdout({groupName})",
            trainGroupList, testGroupList);
    }

    private static BaselineReport Evaluate(
        IReadOnlyList<LabeledVector> train,
        IReadOnlyList<LabeledVector> test,
        IReadOnlyList<LabeledVector> all,
        IReadOnlyList<string> featureNames,
        int k,
        double testFraction,
        int seed,
        string splitStrategy,
        IReadOnlyList<string>? trainGroups = null,
        IReadOnlyList<string>? testGroups = null)
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

        // Per-class precision/recall/F1 derived from the confusion matrix.
        var perClass = new ClassMetrics[classes.Count];
        double f1Sum = 0.0;
        for (int i = 0; i < classes.Count; i++)
        {
            int tp = confusion[i][i];
            int rowSum = confusion[i].Sum();              // actual class i
            int colSum = 0;                               // predicted class i
            for (int r = 0; r < classes.Count; r++)
            {
                colSum += confusion[r][i];
            }

            double precision = colSum > 0 ? (double)tp / colSum : 0.0;
            double recall = rowSum > 0 ? (double)tp / rowSum : 0.0;
            double f1 = (precision + recall) > 0 ? 2 * precision * recall / (precision + recall) : 0.0;
            perClass[i] = new ClassMetrics(classes[i], precision, recall, f1, rowSum);
            f1Sum += f1;
        }

        double macroF1 = classes.Count > 0 ? f1Sum / classes.Count : 0.0;

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
            MacroF1 = macroF1,
            PerClass = perClass,
            SplitStrategy = splitStrategy,
            TrainGroups = trainGroups ?? Array.Empty<string>(),
            TestGroups = testGroups ?? Array.Empty<string>(),
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
