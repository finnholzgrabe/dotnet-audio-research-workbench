namespace AudioResearch.Core.Audio;

/// <summary>
/// Deterministic synthetic-audio generators. Every method is a pure function of
/// its arguments (noise is driven by an explicit seed), so fixtures are
/// reproducible across machines and runs.
/// </summary>
public static class SignalGenerator
{
    /// <summary>Generates a mono sine tone.</summary>
    public static AudioBuffer Sine(double frequencyHz, double seconds, int sampleRate = 16000, double amplitude = 0.8)
    {
        int n = SampleCount(seconds, sampleRate);
        var samples = new float[n];
        double step = 2.0 * Math.PI * frequencyHz / sampleRate;
        for (int i = 0; i < n; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(step * i));
        }

        return new AudioBuffer(samples, sampleRate, 1);
    }

    /// <summary>Generates a linear frequency sweep (chirp) from start to end Hz.</summary>
    public static AudioBuffer Chirp(double startHz, double endHz, double seconds, int sampleRate = 16000, double amplitude = 0.8)
    {
        int n = SampleCount(seconds, sampleRate);
        var samples = new float[n];
        double duration = (double)n / sampleRate;
        double rate = duration > 0 ? (endHz - startHz) / duration : 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / sampleRate;
            // Instantaneous phase of a linear chirp: 2*pi*(f0*t + 0.5*k*t^2).
            double phase = 2.0 * Math.PI * (startHz * t + 0.5 * rate * t * t);
            samples[i] = (float)(amplitude * Math.Sin(phase));
        }

        return new AudioBuffer(samples, sampleRate, 1);
    }

    /// <summary>Generates seeded white noise (uniform in [-amplitude, amplitude]).</summary>
    public static AudioBuffer WhiteNoise(double seconds, int sampleRate = 16000, double amplitude = 0.8, int seed = 1234)
    {
        int n = SampleCount(seconds, sampleRate);
        var samples = new float[n];
        var rng = new Random(seed);
        for (int i = 0; i < n; i++)
        {
            samples[i] = (float)(amplitude * (rng.NextDouble() * 2.0 - 1.0));
        }

        return new AudioBuffer(samples, sampleRate, 1);
    }

    /// <summary>
    /// Generates an amplitude-modulated tone: a carrier sine whose envelope is a
    /// raised cosine at the modulation frequency.
    /// </summary>
    public static AudioBuffer AmplitudeModulated(
        double carrierHz,
        double modulationHz,
        double seconds,
        int sampleRate = 16000,
        double amplitude = 0.8,
        double modulationDepth = 0.8)
    {
        int n = SampleCount(seconds, sampleRate);
        var samples = new float[n];
        double carrierStep = 2.0 * Math.PI * carrierHz / sampleRate;
        double modStep = 2.0 * Math.PI * modulationHz / sampleRate;
        for (int i = 0; i < n; i++)
        {
            double envelope = 1.0 + modulationDepth * Math.Sin(modStep * i);
            double carrier = Math.Sin(carrierStep * i);
            samples[i] = (float)(amplitude / (1.0 + modulationDepth) * envelope * carrier);
        }

        return new AudioBuffer(samples, sampleRate, 1);
    }

    /// <summary>
    /// Generates a crude speech-like fixture: a low carrier shaped by a slow
    /// syllable-rate envelope plus light seeded noise. This is a caricature for
    /// engineering tests, not a model of speech.
    /// </summary>
    public static AudioBuffer SpeechLikeEnvelope(double seconds, int sampleRate = 16000, double amplitude = 0.8, int seed = 4321)
    {
        int n = SampleCount(seconds, sampleRate);
        var samples = new float[n];
        var rng = new Random(seed);
        double carrierStep = 2.0 * Math.PI * 220.0 / sampleRate;   // ~voiced pitch
        double syllableStep = 2.0 * Math.PI * 4.0 / sampleRate;    // ~4 Hz syllable rate
        for (int i = 0; i < n; i++)
        {
            double env = 0.5 * (1.0 + Math.Sin(syllableStep * i));
            double voiced = Math.Sin(carrierStep * i);
            double noise = (rng.NextDouble() * 2.0 - 1.0) * 0.15;
            samples[i] = (float)(amplitude * env * (voiced * 0.85 + noise));
        }

        return new AudioBuffer(samples, sampleRate, 1);
    }

    private static int SampleCount(double seconds, int sampleRate)
    {
        if (seconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "Duration must be non-negative.");
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        }

        return (int)Math.Round(seconds * sampleRate);
    }
}
