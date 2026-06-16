namespace AudioResearch.Cli;

/// <summary>
/// Entry point and command dispatch for the <c>audio-research</c> CLI. Returns a
/// process exit code: 0 on success, 1 on a handled user error, 2 on bad usage.
/// All output goes through the injected writers so behaviour is testable.
/// </summary>
public static class CliApp
{
    public const string Version = "0.1.0";
    public const string Name = "audio-research";

    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(@out);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count == 0 || IsHelp(args[0]))
        {
            PrintHelp(@out);
            return 0;
        }

        string command = args[0];
        string[] rest = args.Skip(1).ToArray();

        try
        {
            switch (command)
            {
                case "version":
                    @out.WriteLine($"{Name} {Version}");
                    return 0;
                case "dataset":
                    return DatasetCommand.Run(rest, @out, error);
                case "generate":
                    return GenerateCommand.Run(rest, @out, error);
                case "inspect":
                    return InspectCommand.Run(rest, @out, error);
                case "features":
                    return FeaturesCommand.Run(rest, @out, error);
                case "ml":
                    return MlCommand.Run(rest, @out, error);
                default:
                    error.WriteLine($"Unknown command '{command}'. Run '{Name} --help' for usage.");
                    return 2;
            }
        }
        catch (FileNotFoundException ex)
        {
            error.WriteLine($"Error: file not found: {ex.FileName ?? ex.Message}");
            return 1;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or DirectoryNotFoundException or IOException)
        {
            error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string arg) =>
        arg is "--help" or "-h" or "help";

    private static void PrintHelp(TextWriter @out)
    {
        @out.WriteLine($"{Name} {Version} - cochlear audio research workbench");
        @out.WriteLine();
        @out.WriteLine("USAGE:");
        @out.WriteLine($"  {Name} <command> [options]");
        @out.WriteLine();
        @out.WriteLine("COMMANDS:");
        @out.WriteLine("  version                       Print the tool version.");
        @out.WriteLine("  generate tone                 Generate a sine-tone WAV.");
        @out.WriteLine("  generate noise                Generate a white-noise WAV.");
        @out.WriteLine("  generate chirp                Generate a linear-sweep WAV.");
        @out.WriteLine("  generate am                   Generate an amplitude-modulated WAV.");
        @out.WriteLine("  dataset fetch fsdd            Download the Free Spoken Digit Dataset (CC BY-SA 4.0).");
        @out.WriteLine("  inspect <wav>                 Report sample rate, channels, duration, peak.");
        @out.WriteLine("  features bands <wav>          Write per-frame cochlear band energies (CSV).");
        @out.WriteLine("  features summary <wav>        Write a feature summary (JSON).");
        @out.WriteLine("  ml baseline                   Train/evaluate the toy classifier; write a report.");
        @out.WriteLine();
        @out.WriteLine("COMMON OPTIONS:");
        @out.WriteLine("  --out <path>                  Output file path.");
        @out.WriteLine("  --seconds <n>                 Duration in seconds (generators).");
        @out.WriteLine("  --rate <hz>                   Sample rate (generators, default 16000).");
        @out.WriteLine("  --freq <hz>                   Tone frequency (generate tone).");
        @out.WriteLine();
        @out.WriteLine("EXAMPLES:");
        @out.WriteLine($"  {Name} generate tone --freq 440 --seconds 1 --out samples/generated/tone.wav");
        @out.WriteLine($"  {Name} inspect samples/generated/tone.wav");
        @out.WriteLine($"  {Name} features summary samples/generated/tone.wav --out summary.json");
        @out.WriteLine($"  {Name} ml baseline --out artifacts/baseline-report.json");
        @out.WriteLine();
        @out.WriteLine("Not a medical device. See docs/medical-disclaimer.md.");
    }
}
