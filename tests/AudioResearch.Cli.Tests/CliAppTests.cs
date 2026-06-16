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

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "audioresearch-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
