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
            error.WriteLine("Usage: ml baseline [--difficulty easy|noisy] [--split random|regime] [--dataset <dir> --labels speaker|digit|auto [--group-by speaker|digit]]");
            error.WriteLine("                  [--snr-min dB] [--snr-max dB] [--out <path>] [--per-class N] [--k N] [--test-fraction F] [--seed N]");
            return 2;
        }

        Options opts = Options.Parse(args.Skip(1).ToArray());
        int perClass = opts.GetInt("per-class", 20);
        int k = opts.GetInt("k", 3);
        double testFraction = opts.GetDouble("test-fraction", 0.3);
        int seed = opts.GetInt("seed", 42);
        double seconds = opts.GetDouble("seconds", 0.5);
        int rate = opts.GetInt("rate", 16000);
        double snrMin = opts.GetDouble("snr-min", 0.0);
        double snrMax = opts.GetDouble("snr-max", 20.0);
        string difficulty = (opts.GetString("difficulty") ?? "noisy").ToLowerInvariant();
        string split = (opts.GetString("split") ?? "random").ToLowerInvariant();
        string outPath = opts.GetString("out") ?? Path.Combine("artifacts", "baseline-report.json");
        string? datasetDir = opts.GetString("dataset");

        string datasetSource;
        BaselineReport report;
        IReadOnlyList<string> featureNames;
        int sampleCount;

        string labelScheme = (opts.GetString("labels") ?? "auto").ToLowerInvariant();
        string groupScheme = (opts.GetString("group-by") ?? "none").ToLowerInvariant();
        if (datasetDir is not null && TryLoadFromDirectory(datasetDir, labelScheme, groupScheme, out var loaded) && loaded.Count > 0)
        {
            var vectors = ToVectors(loaded, out featureNames);
            sampleCount = vectors.Count;
            if (groupScheme != "none")
            {
                // Leave-group-out: no group (e.g. speaker) appears in both splits.
                datasetSource = $"directory:{datasetDir} (labels={labelScheme}, group-by={groupScheme})";
                report = BaselineRunner.RunGrouped(vectors, featureNames, groupScheme, k, testFraction, seed);
            }
            else
            {
                datasetSource = $"directory:{datasetDir} (labels={labelScheme})";
                report = BaselineRunner.Run(vectors, featureNames, k, testFraction, seed);
            }
        }
        else if (difficulty == "noisy" && split == "regime")
        {
            // Generalization: train on a low-frequency regime, test on a disjoint high one.
            DatasetSplit ds = DatasetBuilder.BuildGeneralizationSplit(
                perClassTrain: perClass, perClassTest: Math.Max(4, perClass / 2),
                seconds: seconds, sampleRate: rate, seed: seed);
            var train = ToVectors(ds.Train, out featureNames);
            var test = ToVectors(ds.Test, out _);
            sampleCount = train.Count + test.Count;
            datasetSource = $"synthetic noisy, regime-holdout (low->high freq)";
            report = BaselineRunner.RunWithSplit(train, test, featureNames, k, seed);
        }
        else
        {
            // easy = trivially separable; noisy = overlapping params + SNR variation.
            IReadOnlyList<LabeledAudio> dataset = difficulty == "easy"
                ? DatasetBuilder.Build(perClass, seconds, rate)
                : DatasetBuilder.BuildVaried(perClass, seconds, rate, snrMin, snrMax, seed);
            var vectors = ToVectors(dataset, out featureNames);
            sampleCount = vectors.Count;
            datasetSource = difficulty == "easy"
                ? "synthetic easy (separable)"
                : string.Create(CultureInfo.InvariantCulture, $"synthetic noisy, SNR {snrMin:0}..{snrMax:0} dB");
            report = BaselineRunner.Run(vectors, featureNames, k, testFraction, seed);
        }

        WriteReport(report, datasetSource, outPath);

        @out.WriteLine($"Dataset:   {datasetSource}, {sampleCount} samples, {report.Classes.Count} classes");
        @out.WriteLine($"Model:     {report.ModelType}, k={report.K}");
        @out.WriteLine($"Split:     {report.TrainCount} train / {report.TestCount} test ({report.SplitStrategy}, seed {report.Seed})");
        if (report.TestGroups.Count > 0)
        {
            @out.WriteLine($"Held-out:  {string.Join(", ", report.TestGroups)} (train: {string.Join(", ", report.TrainGroups)})");
        }
        if (report.TestCount == 0)
        {
            error.WriteLine("Warning: too few samples per class to form a held-out test set; no accuracy reported.");
            error.WriteLine("         Provide more WAVs per class, or omit --dataset to use the synthetic dataset.");
        }
        else
        {
            @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Accuracy:  {report.Accuracy:0.000} (macro-F1 {report.MacroF1:0.000}) on the held-out test set"));
            @out.WriteLine("Per class: " + string.Join("  ", report.PerClass.Select(m =>
                string.Create(CultureInfo.InvariantCulture, $"{m.Label}(P{m.Precision:0.00}/R{m.Recall:0.00}/F{m.F1:0.00},n={m.Support})"))));
        }

        @out.WriteLine($"Report:    {outPath}");
        @out.WriteLine("Note: a small k-NN baseline with simple spectral features; engineering evidence, not a medical or production model.");
        return 0;
    }

    private static List<LabeledVector> ToVectors(IReadOnlyList<LabeledAudio> dataset, out IReadOnlyList<string> featureNames)
    {
        var vectors = new List<LabeledVector>(dataset.Count);
        IReadOnlyList<string>? names = null;
        foreach (LabeledAudio item in dataset)
        {
            FeatureSummary summary = FeatureExtractor.Summarize(item.Audio);
            names ??= summary.Names;
            vectors.Add(new LabeledVector(item.Label, summary.Vector, item.Group));
        }

        featureNames = names ?? Array.Empty<string>();
        return vectors;
    }

    private static bool TryLoadFromDirectory(string dir, string labelScheme, string groupScheme, out List<LabeledAudio> samples)
    {
        samples = new List<LabeledAudio>();
        if (!Directory.Exists(dir))
        {
            return false;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.wav").OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string? label = InferLabel(name, labelScheme);
            if (label is null)
            {
                continue;
            }

            string? group = groupScheme == "none" ? null : InferLabel(name, groupScheme);
            samples.Add(new LabeledAudio(label, WavFile.Read(file), group));
        }

        return true;
    }

    /// <summary>
    /// Infers a class label from a WAV filename. FSDD files are named
    /// <c>{digit}_{speaker}_{index}.wav</c> (e.g. <c>7_jackson_32.wav</c>); the
    /// "speaker" / "digit" schemes parse those. "auto" uses signal-type keywords.
    /// </summary>
    internal static string? InferLabel(string name, string labelScheme)
    {
        switch (labelScheme)
        {
            case "speaker":
            case "digit":
                string[] parts = name.Split('_');
                if (parts.Length < 3)
                {
                    return null;
                }

                return labelScheme == "digit" ? parts[0] : parts[1];
            default:
                string lower = name.ToLowerInvariant();
                if (lower.Contains("tone") || lower.Contains("sine")) return "tone";
                if (lower.Contains("sweep") || lower.Contains("chirp")) return "sweep";
                if (lower.Contains("noise")) return "noise";
                if (lower.Contains("modul") || lower.Contains("_am") || lower.StartsWith("am")) return "modulated";
                return null;
        }
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

        var testGroups = new JsonArray();
        foreach (string g in report.TestGroups)
        {
            testGroups.Add(g);
        }

        var trainGroups = new JsonArray();
        foreach (string g in report.TrainGroups)
        {
            trainGroups.Add(g);
        }

        var perClass = new JsonObject();
        foreach (ClassMetrics m in report.PerClass)
        {
            perClass[m.Label] = new JsonObject
            {
                ["precision"] = Math.Round(m.Precision, 6, MidpointRounding.AwayFromZero),
                ["recall"] = Math.Round(m.Recall, 6, MidpointRounding.AwayFromZero),
                ["f1"] = Math.Round(m.F1, 6, MidpointRounding.AwayFromZero),
                ["support"] = m.Support,
            };
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["datasetSource"] = datasetSource,
            ["splitStrategy"] = report.SplitStrategy,
            ["trainGroups"] = trainGroups,
            ["testGroups"] = testGroups,
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
            ["macroF1"] = Math.Round(report.MacroF1, 6, MidpointRounding.AwayFromZero),
            ["perClass"] = perClass,
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
