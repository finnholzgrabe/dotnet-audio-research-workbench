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
