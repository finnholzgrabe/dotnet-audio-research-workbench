namespace AudioResearch.Core.Audio;

/// <summary>
/// Abstraction over a source of captured audio. This is the opt-in seam for audio
/// acquisition. The bundled implementations are offline and safe (synthetic
/// signal, or an existing WAV file). Real microphone capture is intentionally NOT
/// implemented here: it is platform-specific and would add a heavy dependency, so
/// it belongs in a separate optional project that implements this interface.
/// </summary>
public interface IAudioCaptureSource
{
    /// <summary>Human-readable description of the source (for logs/UX).</summary>
    string Description { get; }

    /// <summary>
    /// Produces up to <paramref name="seconds"/> of audio. Sources that own their
    /// rate (e.g. a file) may ignore <paramref name="sampleRate"/>.
    /// </summary>
    AudioBuffer Capture(double seconds, int sampleRate);
}

/// <summary>A deterministic synthetic "capture" — useful for tests and demos.</summary>
public sealed class SyntheticCaptureSource : IAudioCaptureSource
{
    private readonly string _kind;
    private readonly double _frequency;
    private readonly int _seed;

    public SyntheticCaptureSource(string kind = "tone", double frequency = 440.0, int seed = 1234)
    {
        _kind = kind.ToLowerInvariant();
        _frequency = frequency;
        _seed = seed;
    }

    public string Description => $"synthetic:{_kind}";

    public AudioBuffer Capture(double seconds, int sampleRate) => _kind switch
    {
        "noise" => SignalGenerator.WhiteNoise(seconds, sampleRate, 0.8, _seed),
        _ => SignalGenerator.Sine(_frequency, seconds, sampleRate),
    };
}

/// <summary>
/// "Captures" by reading an existing WAV file, optionally trimming to the first
/// <c>seconds</c>. The file dictates the sample rate (the requested rate is
/// ignored). This lets the rest of the pipeline treat recorded files and live
/// capture uniformly.
/// </summary>
public sealed class WavFileCaptureSource : IAudioCaptureSource
{
    private readonly string _path;

    public WavFileCaptureSource(string path)
    {
        _path = path;
    }

    public string Description => $"file:{_path}";

    public AudioBuffer Capture(double seconds, int sampleRate)
    {
        AudioBuffer buffer = WavFile.Read(_path);
        if (seconds <= 0)
        {
            return buffer;
        }

        int maxFrames = (int)Math.Round(seconds * buffer.SampleRate);
        if (maxFrames >= buffer.FrameCount)
        {
            return buffer;
        }

        var trimmed = new float[maxFrames * buffer.Channels];
        Array.Copy(buffer.Samples, trimmed, trimmed.Length);
        return new AudioBuffer(trimmed, buffer.SampleRate, buffer.Channels);
    }
}
