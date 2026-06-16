using System.Globalization;
using AudioResearch.Core.Audio;

namespace AudioResearch.Cli;

/// <summary>Handles <c>inspect &lt;wav&gt;</c>.</summary>
public static class InspectCommand
{
    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        Options opts = Options.Parse(args);
        if (opts.Positionals.Count == 0)
        {
            error.WriteLine("Usage: inspect <wav>");
            return 2;
        }

        string path = opts.Positionals[0];
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("WAV file not found.", path);
        }

        AudioBuffer audio = WavFile.Read(path);
        float peak = audio.Peak();
        double peakDb = peak > 0 ? 20.0 * Math.Log10(peak) : double.NegativeInfinity;

        @out.WriteLine($"File:         {path}");
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Sample rate:  {audio.SampleRate} Hz"));
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Channels:     {audio.Channels}"));
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Frames:       {audio.FrameCount}"));
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Duration:     {audio.Duration:0.###} s"));
        @out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Peak:         {peak:0.####} ({peakDb:0.0} dBFS)"));
        return 0;
    }
}
