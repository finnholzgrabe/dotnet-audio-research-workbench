using System.Globalization;
using System.Text.Json.Nodes;
using AudioResearch.Core.Audio;
using AudioResearch.Core.Experiments;
using AudioResearch.Core.Features;
using AudioResearch.ML;

namespace AudioResearch.Cli;

/// <summary>Handles <c>ml baseline</c>.</summary>
public static class MlCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        if (args.Count == 0 || args[0] != "baseline")
        {
            error.WriteLine("Usage: ml baseline [--dataset <dir>] [--out <path>] [--per-class N] [--k N] [--test-fraction F] [--seed N]");
            return 2;
        }

        Options opts = Options.Parse(args.Skip(1).ToArray());
        int perClass = opts.GetInt("per-class", 12);
        int k = opts.GetInt("k", 3);
        double testFraction = opts.GetDouble("test-fraction", 0.3);
        int seed = opts.GetInt("seed", 42);
        double seconds = opts.GetDouble("seconds", 0.5);
        int rate = opts.GetInt("rate", 16000);
        string outPath = opts.GetString("out") ?? Path.Combine("artifacts", "baseline-report.json");
        string? datasetDir = opts.GetString("dataset");

        // Source the labeled audio: from a directory of WAVs if given and usable,
        // otherwise from the deterministic in-memory synthetic dataset.
        string datasetSource;
        IReadOnlyList<LabeledAudio> dataset;
        if (datasetDir is not null && TryLoadFromDirectory(datasetDir, out var loaded) && loaded.Count > 0)
        {
            dataset = loaded;
            datasetSource = $"directory:{datasetDir}";
        }
        else
        {
            dataset = DatasetBuilder.Build(perClass, seconds, rate);
            datasetSource = "synthetic (deterministic)";
        }

        // Extract a summary feature vector per sample.
        var vectors = new List<LabeledVector>(dataset.Count);
        IReadOnlyList<string>? featureNames = null;
        foreach (LabeledAudio item in dataset)
        {
            FeatureSummary summary = FeatureExtractor.Summarize(item.Audio);
            featureNames ??= summary.Names;
            vectors.Add(new LabeledVector(item.Label, summary.Vector));
        }

        featureNames ??= Array.Empty<string>();
        BaselineReport report = BaselineRunner.Run(vectors, featureNames, k, testFraction, seed);

        WriteReport(report, datasetSource, outPath);

        @out.WriteLine($"Dataset:   {datasetSource}, {vectors.Count} samples, {report.Classes.Count} classes");
        @out.WriteLine($"Model:     {report.ModelType}, k={report.K}");
        @out.WriteLine($"Split:     {report.TrainCount} train / {report.TestCount} test (seed {report.Seed})");
        if (report.TestCount == 0)
        {
            error.WriteLine("Warning: too few samples per class to form a held-out test set; no accuracy reported.");
            error.WriteLine("         Provide more WAVs per class, or omit --dataset to use the synthetic dataset.");
        }
        else
        {
            @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Accuracy:  {report.Accuracy:0.000} on the held-out test set"));
        }

        @out.WriteLine($"Report:    {outPath}");
        @out.WriteLine("Note: a toy baseline on synthetic audio for engineering evidence, not a medical or production model.");
        return 0;
    }

    private static bool TryLoadFromDirectory(string dir, out List<LabeledAudio> samples)
    {
        samples = new List<LabeledAudio>();
        if (!Directory.Exists(dir))
        {
            return false;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.wav").OrderBy(f => f, StringComparer.Ordinal))
        {
            string? label = InferLabel(Path.GetFileNameWithoutExtension(file));
            if (label is null)
            {
                continue;
            }

            samples.Add(new LabeledAudio(label, WavFile.Read(file)));
        }

        return true;
    }

    private static string? InferLabel(string name)
    {
        string lower = name.ToLowerInvariant();
        if (lower.Contains("tone") || lower.Contains("sine")) return "tone";
        if (lower.Contains("sweep") || lower.Contains("chirp")) return "sweep";
        if (lower.Contains("noise")) return "noise";
        if (lower.Contains("modul") || lower.Contains("_am") || lower.StartsWith("am")) return "modulated";
        return null;
    }

    private static void WriteReport(BaselineReport report, string datasetSource, string outPath)
    {
        var confusion = new JsonObject();
        for (int i = 0; i < report.Classes.Count; i++)
        {
            var row = new JsonObject();
            for (int j = 0; j < report.Classes.Count; j++)
            {
                row[report.Classes[j]] = report.Confusion[i][j];
            }

            confusion[report.Classes[i]] = row;
        }

        var classCounts = new JsonObject();
        foreach (KeyValuePair<string, int> pair in report.ClassCounts)
        {
            classCounts[pair.Key] = pair.Value;
        }

        var featureNames = new JsonArray();
        foreach (string n in report.FeatureNames)
        {
            featureNames.Add(n);
        }

        var classes = new JsonArray();
        foreach (string c in report.Classes)
        {
            classes.Add(c);
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["datasetSource"] = datasetSource,
            ["classes"] = classes,
            ["classCounts"] = classCounts,
            ["modelType"] = report.ModelType,
            ["k"] = report.K,
            ["featureCount"] = report.FeatureCount,
            ["featureNames"] = featureNames,
            ["trainCount"] = report.TrainCount,
            ["testCount"] = report.TestCount,
            ["testFraction"] = report.TestFraction,
            ["seed"] = report.Seed,
            ["accuracy"] = Math.Round(report.Accuracy, 6, MidpointRounding.AwayFromZero),
            ["confusionMatrix"] = confusion,
            ["disclaimer"] = "Toy baseline on synthetic audio for engineering evidence only. Not a medical or production model.",
        };

        string? dirName = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        File.WriteAllText(outPath, Json.Write(root) + "\n");
    }
}
