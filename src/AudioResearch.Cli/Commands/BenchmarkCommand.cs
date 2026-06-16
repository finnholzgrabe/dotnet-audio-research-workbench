using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using AudioResearch.Core.Audio;
using AudioResearch.Core.Dsp;
using AudioResearch.Core.Features;

namespace AudioResearch.Cli;

/// <summary>
/// Handles <c>benchmark</c>: times the core DSP / feature operations and writes a
/// stable-schema JSON report for tracking performance regressions over time.
/// Timings are wall-clock and therefore non-deterministic; the schema is stable.
/// </summary>
public static class BenchmarkCommand
{
    private sealed record Result(string Operation, string Size, int Iterations, double MeanMs, double MedianMs);

    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        Options opts = Options.Parse(args);
        int iterations = Math.Max(1, opts.GetInt("iterations", 50));
        int sampleRate = opts.GetInt("rate", 16000);
        double seconds = opts.GetDouble("seconds", 1.0);
        string? outPath = opts.GetString("out");

        var results = new List<Result>();

        // 1) FFT across power-of-two sizes.
        foreach (int n in new[] { 256, 512, 1024, 2048, 4096 })
        {
            var template = new Complex[n];
            var rng = new Random(1);
            for (int i = 0; i < n; i++)
            {
                template[i] = new Complex(rng.NextDouble() * 2 - 1, 0.0);
            }

            results.Add(Measure("fft", n.ToString(CultureInfo.InvariantCulture), iterations, () =>
            {
                var buffer = (Complex[])template.Clone();
                Fourier.Transform(buffer);
            }));
        }

        // 2) STFT, cochlear features, and MFCC features on a generated signal.
        AudioBuffer signal = SignalGenerator.Chirp(200, 6000, seconds, sampleRate);
        string sizeLabel = string.Create(CultureInfo.InvariantCulture, $"{seconds:0.##}s@{sampleRate}Hz");

        results.Add(Measure("stft", sizeLabel, iterations, () =>
            Stft.Compute(signal.ToMono(), 512, 256, sampleRate)));
        results.Add(Measure("features.cochlear", sizeLabel, iterations, () =>
            FeatureExtractor.Summarize(signal)));
        results.Add(Measure("features.mfcc", sizeLabel, iterations, () =>
            MfccExtractor.Summarize(signal)));

        foreach (Result r in results)
        {
            @out.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{r.Operation,-20} {r.Size,-14} x{r.Iterations,-4}  mean {r.MeanMs,8:0.000} ms  median {r.MedianMs,8:0.000} ms"));
        }

        if (outPath is not null)
        {
            WriteReport(results, iterations, sampleRate, seconds, outPath);
            @out.WriteLine($"Report: {outPath}");
        }

        @out.WriteLine("Note: wall-clock timings are machine-dependent and non-deterministic.");
        return 0;
    }

    private static Result Measure(string operation, string size, int iterations, Action action)
    {
        int warmup = Math.Min(5, iterations);
        for (int i = 0; i < warmup; i++)
        {
            action();
        }

        var samples = new double[iterations];
        var sw = new Stopwatch();
        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        double mean = samples.Average();
        Array.Sort(samples);
        double median = iterations % 2 == 1
            ? samples[iterations / 2]
            : (samples[iterations / 2 - 1] + samples[iterations / 2]) / 2.0;

        return new Result(operation, size, iterations, mean, median);
    }

    private static void WriteReport(IReadOnlyList<Result> results, int iterations, int sampleRate, double seconds, string outPath)
    {
        var items = new JsonArray();
        foreach (Result r in results)
        {
            items.Add(new JsonObject
            {
                ["operation"] = r.Operation,
                ["size"] = r.Size,
                ["iterations"] = r.Iterations,
                ["meanMs"] = Math.Round(r.MeanMs, 4, MidpointRounding.AwayFromZero),
                ["medianMs"] = Math.Round(r.MedianMs, 4, MidpointRounding.AwayFromZero),
            });
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["iterations"] = iterations,
            ["sampleRate"] = sampleRate,
            ["seconds"] = seconds,
            ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["results"] = items,
        };

        string? dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outPath, Json.Write(root) + "\n");
    }
}
