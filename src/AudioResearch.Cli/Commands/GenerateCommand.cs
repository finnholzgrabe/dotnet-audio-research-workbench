using System.Globalization;
using AudioResearch.Core.Audio;

namespace AudioResearch.Cli;

/// <summary>Handles <c>generate tone|noise|chirp|am</c>.</summary>
public static class GenerateCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        if (args.Count == 0)
        {
            error.WriteLine("Usage: generate <tone|noise|chirp|am> [options]");
            return 2;
        }

        string kind = args[0];
        Options opts = Options.Parse(args.Skip(1).ToArray());

        double seconds = opts.GetDouble("seconds", 1.0);
        int rate = opts.GetInt("rate", 16000);
        double amplitude = opts.GetDouble("amplitude", 0.8);

        AudioBuffer buffer;
        string defaultName;
        switch (kind)
        {
            case "tone":
                double freq = opts.GetDouble("freq", 440.0);
                buffer = SignalGenerator.Sine(freq, seconds, rate, amplitude);
                defaultName = $"tone_{(int)freq}hz.wav";
                break;
            case "noise":
                int seed = opts.GetInt("seed", 1234);
                buffer = SignalGenerator.WhiteNoise(seconds, rate, amplitude, seed);
                defaultName = "noise.wav";
                break;
            case "chirp":
                double start = opts.GetDouble("start", 200.0);
                double end = opts.GetDouble("end", 4000.0);
                buffer = SignalGenerator.Chirp(start, end, seconds, rate, amplitude);
                defaultName = "chirp.wav";
                break;
            case "am":
                double carrier = opts.GetDouble("carrier", 1000.0);
                double mod = opts.GetDouble("mod", 8.0);
                buffer = SignalGenerator.AmplitudeModulated(carrier, mod, seconds, rate, amplitude);
                defaultName = "am.wav";
                break;
            default:
                error.WriteLine($"Unknown generate kind '{kind}'. Expected tone, noise, chirp, or am.");
                return 2;
        }

        string outPath = opts.GetString("out") ?? Path.Combine("samples", "generated", defaultName);
        WavFile.Write(outPath, buffer);

        @out.WriteLine($"Wrote {kind} -> {outPath}");
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {buffer.SampleRate} Hz, {buffer.Channels} ch, {buffer.Duration:0.###} s, {buffer.FrameCount} frames"));
        return 0;
    }
}
