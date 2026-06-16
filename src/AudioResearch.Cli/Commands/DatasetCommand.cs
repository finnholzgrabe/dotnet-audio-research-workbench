using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AudioResearch.Cli;

/// <summary>
/// Handles <c>dataset fetch fsdd</c>: downloads a pinned, verified release of the
/// Free Spoken Digit Dataset into a local (git-ignored) directory.
///
/// FSDD is licensed CC BY-SA 4.0. The data is never committed to this repository;
/// it is fetched on demand only when the user runs this command.
/// </summary>
public static class DatasetCommand
{
    // Pinned release for reproducibility. v1.0.9: 2500 recordings, 5 speakers,
    // 8 kHz mono 16-bit PCM WAV.
    private const string FsddTag = "v1.0.9";
    private const string FsddUrl = "https://github.com/Jakobovski/free-spoken-digit-dataset/archive/refs/tags/v1.0.9.tar.gz";
    private const string FsddSha256 = "8bc44cde129d505cbbb6b365c09f80c663f2aa77578afc634e6141a2f87100c0";
    private const int FsddExpectedCount = 2500;
    private const string FsddLicense = "CC BY-SA 4.0";
    private const string FsddCitation = "Free Spoken Digit Dataset (FSDD), Jakobovski et al., https://github.com/Jakobovski/free-spoken-digit-dataset";

    public static int Run(IReadOnlyList<string> args, TextWriter @out, TextWriter error)
    {
        if (args.Count == 0 || args[0] != "fetch")
        {
            error.WriteLine("Usage: dataset fetch fsdd [--out <dir>] [--force]");
            return 2;
        }

        if (args.Count < 2 || args[1] != "fsdd")
        {
            error.WriteLine("Unknown dataset. Supported: fsdd");
            return 2;
        }

        Options opts = Options.Parse(args.Skip(2).ToArray());
        string outDir = opts.GetString("out") ?? Path.Combine("data", "fsdd");
        string recordingsDir = Path.Combine(outDir, "recordings");
        bool force = opts.HasFlag("force");

        if (!force && Directory.Exists(recordingsDir) && Directory.EnumerateFiles(recordingsDir, "*.wav").Any())
        {
            int existing = Directory.EnumerateFiles(recordingsDir, "*.wav").Count();
            @out.WriteLine($"FSDD already present at {recordingsDir} ({existing} files). Use --force to re-download.");
            return 0;
        }

        @out.WriteLine($"Downloading FSDD {FsddTag} ({FsddLicense})...");
        @out.WriteLine($"  {FsddUrl}");

        byte[] archive;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            archive = http.GetByteArrayAsync(FsddUrl).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            error.WriteLine($"Error: download failed: {ex.Message}");
            return 1;
        }

        string actualSha = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        if (!string.Equals(actualSha, FsddSha256, StringComparison.Ordinal))
        {
            error.WriteLine("Error: checksum mismatch for downloaded archive (refusing to use it).");
            error.WriteLine($"  expected {FsddSha256}");
            error.WriteLine($"  actual   {actualSha}");
            return 1;
        }

        Directory.CreateDirectory(recordingsDir);
        int extracted = ExtractRecordings(archive, recordingsDir);

        WriteAttribution(outDir);

        @out.WriteLine($"Extracted {extracted} WAV files -> {recordingsDir}");
        if (extracted != FsddExpectedCount)
        {
            error.WriteLine($"Warning: expected {FsddExpectedCount} files but extracted {extracted}.");
        }

        @out.WriteLine($"License: {FsddLicense}. Attribution written to {Path.Combine(outDir, "ATTRIBUTION.txt")}.");
        @out.WriteLine("This data is git-ignored and must not be committed.");
        @out.WriteLine($"Next: dotnet \"$DLL\" ml baseline --dataset {recordingsDir} --labels speaker");
        return 0;
    }

    private static int ExtractRecordings(byte[] archive, string recordingsDir)
    {
        int count = 0;
        using var gz = new GZipStream(new MemoryStream(archive), CompressionMode.Decompress);
        using var tar = new TarReader(gz);
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) is not null)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            string name = entry.Name.Replace('\\', '/');
            if (!name.Contains("/recordings/", StringComparison.Ordinal) || !name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fileName = Path.GetFileName(name);
            string destination = Path.Combine(recordingsDir, fileName);
            using FileStream fs = File.Create(destination);
            entry.DataStream?.CopyTo(fs);
            count++;
        }

        return count;
    }

    private static void WriteAttribution(string outDir)
    {
        string text =
            $"Free Spoken Digit Dataset (FSDD)\n" +
            $"Release: {FsddTag}\n" +
            $"Source:  {FsddUrl}\n" +
            $"License: {FsddLicense} (https://creativecommons.org/licenses/by-sa/4.0/)\n" +
            $"Cite:    {FsddCitation}\n" +
            $"\nThis data is fetched on demand and is not part of this repository.\n";
        File.WriteAllText(Path.Combine(outDir, "ATTRIBUTION.txt"), text);
    }
}
