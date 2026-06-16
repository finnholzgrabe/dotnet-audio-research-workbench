using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AudioResearch.Core.Audio;
using AudioResearch.Core.Features;

namespace AudioResearch.Cli;

/// <summary>Handles <c>features bands|summary &lt;wav&gt; --out &lt;path&gt;</c>.</summary>
public static class FeaturesCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        if (args.Count == 0)
        {
            error.WriteLine("Usage: features <bands|summary> <wav> [--out <path>]");
            return 2;
        }

        string sub = args[0];
        Options opts = Options.Parse(args.Skip(1).ToArray());
        if (opts.Positionals.Count == 0)
        {
            error.WriteLine($"Usage: features {sub} <wav> [--out <path>]");
            return 2;
        }

        string path = opts.Positionals[0];
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("WAV file not found.", path);
        }

        AudioBuffer audio = WavFile.Read(path);
        var options = new FeatureOptions(
            FrameSize: opts.GetInt("frame", FeatureOptions.Default.FrameSize),
            Hop: opts.GetInt("hop", FeatureOptions.Default.Hop),
            FilterCount: opts.GetInt("filters", FeatureOptions.Default.FilterCount),
            FMin: opts.GetDouble("fmin", FeatureOptions.Default.FMin));

        return sub switch
        {
            "bands" => WriteBands(audio, options, opts.GetString("out") ?? "features.csv", @out),
            "summary" => WriteSummary(audio, options, path, opts.GetString("out") ?? "summary.json", @out),
            _ => UnknownSub(sub, error),
        };
    }

    private static int UnknownSub(string sub, TextWriter error)
    {
        error.WriteLine($"Unknown features subcommand '{sub}'. Expected 'bands' or 'summary'.");
        return 2;
    }

    private static int WriteBands(AudioBuffer audio, FeatureOptions options, string outPath, TextWriter @out)
    {
        BandFeatures bands = FeatureExtractor.ExtractBands(audio, options);
        var sb = new StringBuilder();

        sb.Append("frame_index,time_s");
        for (int b = 0; b < bands.FilterCount; b++)
        {
            sb.Append(CultureInfo.InvariantCulture, $",band_{bands.CenterFrequencies[b]:0}hz");
        }

        sb.Append('\n');

        for (int f = 0; f < bands.FrameCount; f++)
        {
            double time = (double)(f * bands.Hop) / bands.SampleRate;
            sb.Append(CultureInfo.InvariantCulture, $"{f},{time:0.#####}");
            double[] row = bands.FrameEnergies[f];
            for (int b = 0; b < row.Length; b++)
            {
                sb.Append(CultureInfo.InvariantCulture, $",{row[b]:0.######}");
            }

            sb.Append('\n');
        }

        WriteAllText(outPath, sb.ToString());
        @out.WriteLine($"Wrote {bands.FrameCount} frames x {bands.FilterCount} bands -> {outPath}");
        return 0;
    }

    private static int WriteSummary(AudioBuffer audio, FeatureOptions options, string sourcePath, string outPath, TextWriter @out)
    {
        FeatureSummary summary = FeatureExtractor.Summarize(audio, options);

        var features = new JsonObject();
        foreach (KeyValuePair<string, double> pair in summary.Pairs())
        {
            features[pair.Key] = JsonValue.Create(Round(pair.Value));
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["source"] = sourcePath,
            ["sampleRate"] = audio.SampleRate,
            ["channels"] = audio.Channels,
            ["durationSeconds"] = Round(audio.Duration),
            ["frameSize"] = options.FrameSize,
            ["hop"] = options.Hop,
            ["filterCount"] = options.FilterCount,
            ["featureCount"] = summary.Vector.Length,
            ["features"] = features,
        };

        WriteAllText(outPath, Json.Write(root) + "\n");
        @out.WriteLine($"Wrote {summary.Vector.Length} features -> {outPath}");
        return 0;
    }

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static void WriteAllText(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
    }
}
