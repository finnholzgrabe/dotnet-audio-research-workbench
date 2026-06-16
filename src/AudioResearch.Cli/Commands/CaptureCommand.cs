using System.Globalization;
using AudioResearch.Core.Audio;

namespace AudioResearch.Cli;

/// <summary>
/// Handles <c>capture</c>: acquires audio through an <see cref="IAudioCaptureSource"/>
/// and writes it to a WAV. Bundled sources are offline and safe (synthetic signal
/// or an existing file). Real microphone capture is opt-in and not bundled.
/// </summary>
public static class CaptureCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        Options opts = Options.Parse(args);
        string source = (opts.GetString("source") ?? "synthetic").ToLowerInvariant();
        double seconds = opts.GetDouble("seconds", 1.0);
        int rate = opts.GetInt("rate", 16000);

        IAudioCaptureSource capture;
        string defaultName;
        switch (source)
        {
            case "synthetic":
                string kind = (opts.GetString("kind") ?? "tone").ToLowerInvariant();
                double freq = opts.GetDouble("freq", 440.0);
                capture = new SyntheticCaptureSource(kind, freq);
                defaultName = $"capture_{kind}.wav";
                break;
            case "file":
                string? path = opts.GetString("file");
                if (path is null)
                {
                    error.WriteLine("capture --source file requires --file <wav>");
                    return 2;
                }

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException("Capture source file not found.", path);
                }

                capture = new WavFileCaptureSource(path);
                defaultName = "capture_file.wav";
                break;
            case "mic":
            case "microphone":
                error.WriteLine("Live microphone capture is not bundled (platform-specific).");
                error.WriteLine("Implement IAudioCaptureSource in an optional project; see docs/architecture.md.");
                return 1;
            default:
                error.WriteLine($"Unknown capture source '{source}'. Expected synthetic, file, or mic.");
                return 2;
        }

        AudioBuffer buffer = capture.Capture(seconds, rate);
        string outPath = opts.GetString("out") ?? Path.Combine("samples", "generated", defaultName);
        WavFile.Write(outPath, buffer);

        @out.WriteLine($"Captured [{capture.Description}] -> {outPath}");
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"  {buffer.SampleRate} Hz, {buffer.Channels} ch, {buffer.Duration:0.###} s, {buffer.FrameCount} frames"));
        return 0;
    }
}
