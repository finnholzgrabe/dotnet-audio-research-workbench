using AudioResearch.Core.Audio;
using AudioResearch.Core.Experiments;
using AudioResearch.Core.Features;
using AudioResearch.ML;
using Xunit;

namespace AudioResearch.Core.Tests;

public class BaselineTests
{
    [Fact]
    public void Knn_SeparatesObviousClusters()
    {
        var train = new List<LabeledVector>
        {
            new("a", new[] { 0.0, 0.0 }),
            new("a", new[] { 0.1, 0.1 }),
            new("b", new[] { 9.0, 9.0 }),
            new("b", new[] { 9.1, 8.9 }),
        };

        var knn = new KnnClassifier(k: 1);
        knn.Fit(train);

        Assert.Equal("a", knn.Predict(new[] { 0.2, 0.0 }));
        Assert.Equal("b", knn.Predict(new[] { 8.5, 9.2 }));
    }

    [Fact]
    public void Baseline_OnSyntheticDataset_IsAccurateAndDeterministic()
    {
        IReadOnlyList<LabeledAudio> dataset = DatasetBuilder.Build(perClass: 12, seconds: 0.5);
        var vectors = new List<LabeledVector>();
        IReadOnlyList<string> names = Array.Empty<string>();
        foreach (LabeledAudio item in dataset)
        {
            FeatureSummary summary = FeatureExtractor.Summarize(item.Audio);
            names = summary.Names;
            vectors.Add(new LabeledVector(item.Label, summary.Vector));
        }

        BaselineReport r1 = BaselineRunner.Run(vectors, names, k: 3, testFraction: 0.3, seed: 42);
        BaselineReport r2 = BaselineRunner.Run(vectors, names, k: 3, testFraction: 0.3, seed: 42);

        Assert.Equal(r1.Accuracy, r2.Accuracy, 9);          // deterministic
        Assert.Equal(4, r1.Classes.Count);
        Assert.True(r1.TrainCount > r1.TestCount);
        Assert.True(r1.Accuracy >= 0.75, $"accuracy was {r1.Accuracy}");
    }

    [Fact]
    public void NoisyBaseline_IsHarderThanEasy_AndAboveChance()
    {
        var noisy = DatasetBuilder.BuildVaried(perClass: 20, seconds: 0.4, minSnrDb: 0, maxSnrDb: 12, seed: 7)
            .Select(d => new LabeledVector(d.Label, FeatureExtractor.Summarize(d.Audio).Vector))
            .ToList();
        IReadOnlyList<string> names = FeatureExtractor.Summarize(SignalGenerator.Sine(440, 0.4, 16000)).Names;

        BaselineReport report = BaselineRunner.Run(noisy, names, k: 3, testFraction: 0.3, seed: 42);

        // Well above the 0.25 chance level, but the overlapping/noisy classes make
        // it genuinely harder than the trivially-separable easy dataset.
        Assert.True(report.Accuracy > 0.4, $"accuracy was {report.Accuracy}");
        Assert.True(report.Accuracy < 1.0, $"noisy task should not be perfectly separable, was {report.Accuracy}");
    }

    [Fact]
    public void RegimeSplit_RunsAndReportsHoldoutStrategy()
    {
        DatasetSplit ds = DatasetBuilder.BuildGeneralizationSplit(perClassTrain: 10, perClassTest: 5, seconds: 0.3);
        IReadOnlyList<string> names = FeatureExtractor.Summarize(ds.Train[0].Audio).Names;
        var train = ds.Train.Select(d => new LabeledVector(d.Label, FeatureExtractor.Summarize(d.Audio).Vector)).ToList();
        var test = ds.Test.Select(d => new LabeledVector(d.Label, FeatureExtractor.Summarize(d.Audio).Vector)).ToList();

        BaselineReport report = BaselineRunner.RunWithSplit(train, test, names, k: 3);

        Assert.Equal("regime-holdout", report.SplitStrategy);
        Assert.Equal(train.Count, report.TrainCount);
        Assert.Equal(test.Count, report.TestCount);
        Assert.InRange(report.Accuracy, 0.0, 1.0);
    }

    [Fact]
    public void RunGrouped_KeepsGroupsDisjointAcrossSplit()
    {
        var samples = new List<LabeledVector>();
        foreach (string g in new[] { "A", "B", "C", "D" })
        {
            samples.Add(new LabeledVector("x", new[] { 0.0 }, g));
            samples.Add(new LabeledVector("y", new[] { 1.0 }, g));
        }

        BaselineReport report = BaselineRunner.RunGrouped(samples, new[] { "f" }, "speaker", k: 1, testFraction: 0.3, seed: 1);

        Assert.StartsWith("group-holdout(speaker)", report.SplitStrategy);
        Assert.NotEmpty(report.TestGroups);
        Assert.NotEmpty(report.TrainGroups);
        Assert.Empty(report.TestGroups.Intersect(report.TrainGroups)); // disjoint
        Assert.Equal(samples.Count, report.TrainCount + report.TestCount);
    }

    [Fact]
    public void RunGrouped_WhenLabelEqualsGroup_CannotGeneralize()
    {
        // If the class IS the group (like classifying speakers leave-speaker-out),
        // held-out classes are never seen in training -> accuracy must be 0.
        var samples = new List<LabeledVector>();
        foreach (string g in new[] { "alice", "bob", "carol" })
        {
            for (int i = 0; i < 4; i++)
            {
                samples.Add(new LabeledVector(g, new[] { (double)i }, g));
            }
        }

        BaselineReport report = BaselineRunner.RunGrouped(samples, new[] { "f" }, "speaker", k: 1, testFraction: 0.34, seed: 3);
        Assert.Equal(0.0, report.Accuracy, 9);
    }

    [Fact]
    public void RunGrouped_ThrowsWhenGroupsMissing()
    {
        var samples = new List<LabeledVector> { new("x", new[] { 0.0 }), new("y", new[] { 1.0 }) };
        Assert.Throws<ArgumentException>(() => BaselineRunner.RunGrouped(samples, new[] { "f" }, "speaker"));
    }

    [Fact]
    public void Baseline_ConfusionMatrixSumsToTestCount()
    {
        IReadOnlyList<LabeledAudio> dataset = DatasetBuilder.Build(perClass: 8, seconds: 0.4);
        var vectors = dataset
            .Select(d => new LabeledVector(d.Label, FeatureExtractor.Summarize(d.Audio).Vector))
            .ToList();

        BaselineReport report = BaselineRunner.Run(vectors, FeatureExtractor.Summarize(dataset[0].Audio).Names);

        int total = report.Confusion.Sum(row => row.Sum());
        Assert.Equal(report.TestCount, total);
    }
}
