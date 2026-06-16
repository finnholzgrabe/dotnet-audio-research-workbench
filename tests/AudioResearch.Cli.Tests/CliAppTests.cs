using System.Text.Json;
using AudioResearch.Cli;
using Xunit;

namespace AudioResearch.Cli.Tests;

public class CliAppTests
{
    private static (int code, string @out, string err) Run(params string[] args)
    {
        var outW = new StringWriter();
        var errW = new StringWriter();
        int code = CliApp.Run(args, outW, errW);
        return (code, outW.ToString(), errW.ToString());
    }

    [Fact]
    public void Version_PrintsVersionAndSucceeds()
    {
        (int code, string @out, _) = Run("version");
        Assert.Equal(0, code);
        Assert.Contains(CliApp.Version, @out);
    }

    [Fact]
    public void NoArgs_PrintsHelp()
    {
        (int code, string @out, _) = Run();
        Assert.Equal(0, code);
        Assert.Contains("USAGE:", @out);
        Assert.Contains("medical", @out, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HelpFlag_Succeeds()
    {
        (int code, string @out, _) = Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("COMMANDS:", @out);
    }

    [Fact]
    public void UnknownCommand_ReturnsUsageError()
    {
        (int code, _, string err) = Run("frobnicate");
        Assert.Equal(2, code);
        Assert.Contains("Unknown command", err);
    }

    [Fact]
    public void Inspect_MissingFile_ReturnsHandledError()
    {
        (int code, _, string err) = Run("inspect", "does-not-exist.wav");
        Assert.Equal(1, code);
        Assert.Contains("not found", err);
    }

    [Fact]
    public void GenerateThenInspect_RoundTrips()
    {
        string dir = NewTempDir();
        try
        {
            string wav = Path.Combine(dir, "tone.wav");
            (int genCode, string genOut, _) = Run("generate", "tone", "--freq", "440", "--seconds", "0.25", "--out", wav);
            Assert.Equal(0, genCode);
            Assert.True(File.Exists(wav));
            Assert.Contains("Wrote tone", genOut);

            (int inspCode, string inspOut, _) = Run("inspect", wav);
            Assert.Equal(0, inspCode);
            Assert.Contains("16000 Hz", inspOut);
            Assert.Contains("Channels:     1", inspOut);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FeaturesSummary_WritesValidJson()
    {
        string dir = NewTempDir();
        try
        {
            string wav = Path.Combine(dir, "tone.wav");
            string json = Path.Combine(dir, "summary.json");
            Run("generate", "tone", "--freq", "440", "--seconds", "0.3", "--out", wav);

            (int code, _, _) = Run("features", "summary", wav, "--out", json);
            Assert.Equal(0, code);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(json));
            JsonElement root = doc.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(16000, root.GetProperty("sampleRate").GetInt32());
            Assert.True(root.GetProperty("features").GetProperty("spectral_centroid_mean_hz").GetDouble() > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FeaturesBands_WritesCsvWithHeader()
    {
        string dir = NewTempDir();
        try
        {
            string wav = Path.Combine(dir, "tone.wav");
            string csv = Path.Combine(dir, "bands.csv");
            Run("generate", "tone", "--freq", "440", "--seconds", "0.3", "--out", wav);

            (int code, _, _) = Run("features", "bands", wav, "--out", csv, "--filters", "16");
            Assert.Equal(0, code);

            string[] lines = File.ReadAllLines(csv);
            Assert.StartsWith("frame_index,time_s", lines[0]);
            Assert.True(lines.Length > 1);
            // header has 2 fixed columns + 16 band columns
            Assert.Equal(18, lines[0].Split(',').Length);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void MlBaseline_WritesReportWithAccuracy()
    {
        string dir = NewTempDir();
        try
        {
            string report = Path.Combine(dir, "report.json");
            (int code, string @out, _) = Run("ml", "baseline", "--per-class", "8", "--out", report);
            Assert.Equal(0, code);
            Assert.Contains("Accuracy:", @out);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(report));
            JsonElement root = doc.RootElement;
            Assert.Equal(4, root.GetProperty("classes").GetArrayLength());
            double acc = root.GetProperty("accuracy").GetDouble();
            Assert.InRange(acc, 0.0, 1.0);
            Assert.True(root.GetProperty("featureCount").GetInt32() > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void MlBaseline_DirectorySpeakerLabels_ParsesFsddNaming()
    {
        // Offline stand-in for FSDD: files named {digit}_{speaker}_{index}.wav,
        // where each "speaker" has a distinct tone so the task is separable.
        string dir = NewTempDir();
        try
        {
            string[] speakers = { "alice", "bob", "carol" };
            double[] freqs = { 300, 900, 1500 };
            for (int s = 0; s < speakers.Length; s++)
            {
                for (int idx = 0; idx < 8; idx++)
                {
                    int digit = idx % 10;
                    string wav = Path.Combine(dir, $"{digit}_{speakers[s]}_{idx}.wav");
                    Run("generate", "tone", "--freq", freqs[s].ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "--seconds", "0.2", "--out", wav);
                }
            }

            string report = Path.Combine(dir, "fsdd-speaker.json");
            (int code, string @out, _) = Run("ml", "baseline", "--dataset", dir, "--labels", "speaker", "--out", report);
            Assert.Equal(0, code);
            Assert.Contains("labels=speaker", @out);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(report));
            JsonElement root = doc.RootElement;
            Assert.Equal(3, root.GetProperty("classes").GetArrayLength()); // 3 speakers
            // Distinct tones per speaker -> the split should be highly accurate.
            Assert.True(root.GetProperty("accuracy").GetDouble() > 0.8);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void MlBaseline_GroupBySpeaker_HoldsSpeakersOut()
    {
        string dir = NewTempDir();
        try
        {
            string[] speakers = { "alice", "bob", "carol" };
            for (int s = 0; s < speakers.Length; s++)
            {
                for (int idx = 0; idx < 6; idx++)
                {
                    int digit = idx % 3;
                    string wav = Path.Combine(dir, $"{digit}_{speakers[s]}_{idx}.wav");
                    Run("generate", "tone", "--freq", (300 + 100 * digit).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "--seconds", "0.2", "--out", wav);
                }
            }

            string report = Path.Combine(dir, "grouped.json");
            (int code, string @out, _) = Run("ml", "baseline", "--dataset", dir,
                "--labels", "digit", "--group-by", "speaker", "--out", report);
            Assert.Equal(0, code);
            Assert.Contains("Held-out:", @out);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(report));
            JsonElement root = doc.RootElement;
            Assert.StartsWith("group-holdout(speaker)", root.GetProperty("splitStrategy").GetString());

            var test = root.GetProperty("testGroups").EnumerateArray().Select(e => e.GetString()).ToHashSet();
            var train = root.GetProperty("trainGroups").EnumerateArray().Select(e => e.GetString()).ToHashSet();
            Assert.NotEmpty(test);
            Assert.NotEmpty(train);
            Assert.Empty(test.Intersect(train)); // no speaker in both splits
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Capture_Synthetic_WritesWav()
    {
        string dir = NewTempDir();
        try
        {
            string wav = Path.Combine(dir, "cap.wav");
            (int code, string @out, _) = Run("capture", "--source", "synthetic", "--kind", "tone",
                "--seconds", "0.3", "--out", wav);
            Assert.Equal(0, code);
            Assert.Contains("synthetic:tone", @out);
            Assert.True(File.Exists(wav));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Capture_Mic_IsNotBundled()
    {
        (int code, _, string err) = Run("capture", "--source", "mic");
        Assert.Equal(1, code);
        Assert.Contains("not bundled", err);
    }

    [Fact]
    public void Benchmark_WritesReportWithStableSchema()
    {
        string dir = NewTempDir();
        try
        {
            string report = Path.Combine(dir, "bench.json");
            (int code, string @out, _) = Run("benchmark", "--iterations", "2", "--seconds", "0.1", "--out", report);
            Assert.Equal(0, code);
            Assert.Contains("fft", @out);

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(report));
            JsonElement root = doc.RootElement;
            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            JsonElement results = root.GetProperty("results");
            Assert.True(results.GetArrayLength() > 0);
            JsonElement first = results[0];
            Assert.True(first.GetProperty("meanMs").GetDouble() >= 0);
            Assert.False(string.IsNullOrEmpty(first.GetProperty("operation").GetString()));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "audioresearch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
