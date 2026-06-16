using AudioResearch.Core.Audio;

namespace AudioResearch.Core.Experiments;

/// <summary>
/// A labeled audio sample. <paramref name="Group"/> is an optional grouping key
/// (e.g. the speaker) used for group-aware, leave-group-out evaluation.
/// </summary>
public sealed record LabeledAudio(string Label, AudioBuffer Audio, string? Group = null);

/// <summary>A predefined train/test partition of a labeled dataset.</summary>
public sealed record DatasetSplit(IReadOnlyList<LabeledAudio> Train, IReadOnlyList<LabeledAudio> Test);

/// <summary>
/// Builds a small, fully deterministic labeled dataset of synthetic fixtures for
/// the ML baseline. Classes: tone, sweep (chirp), noise, modulated (AM tone).
/// Variation comes from deterministic parameter sweeps and seeded noise.
/// </summary>
public static class DatasetBuilder
{
    public static readonly IReadOnlyList<string> Labels = new[] { "tone", "sweep", "noise", "modulated" };

    /// <summary>
    /// Builds <paramref name="perClass"/> examples for each of the four classes.
    /// All randomness is seeded, so the dataset is identical on every run.
    /// </summary>
    public static IReadOnlyList<LabeledAudio> Build(int perClass = 12, double seconds = 0.5, int sampleRate = 16000)
    {
        if (perClass <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(perClass), "perClass must be positive.");
        }

        var samples = new List<LabeledAudio>();
        for (int i = 0; i < perClass; i++)
        {
            // Spread parameters deterministically across each class.
            double toneFreq = 220.0 + i * 90.0;                       // 220 Hz upward
            double sweepStart = 200.0 + i * 30.0;
            double sweepEnd = 3000.0 - i * 50.0;
            double carrier = 800.0 + i * 60.0;
            double modRate = 6.0 + i * 1.5;

            samples.Add(new LabeledAudio("tone", SignalGenerator.Sine(toneFreq, seconds, sampleRate)));
            samples.Add(new LabeledAudio("sweep", SignalGenerator.Chirp(sweepStart, sweepEnd, seconds, sampleRate)));
            samples.Add(new LabeledAudio("noise", SignalGenerator.WhiteNoise(seconds, sampleRate, 0.8, seed: 1000 + i)));
            samples.Add(new LabeledAudio("modulated", SignalGenerator.AmplitudeModulated(carrier, modRate, seconds, sampleRate)));
        }

        return samples;
    }

    /// <summary>
    /// Builds a deliberately harder dataset: class parameter ranges overlap (slow
    /// narrow sweeps resemble tones, shallow AM resembles tones, low-SNR tones
    /// resemble noise) and every sample gets seeded additive noise at an SNR drawn
    /// from <paramref name="minSnrDb"/>..<paramref name="maxSnrDb"/>, plus light
    /// gain / DC-offset augmentation. Fully deterministic for a given seed.
    /// </summary>
    public static IReadOnlyList<LabeledAudio> BuildVaried(
        int perClass = 20,
        double seconds = 0.5,
        int sampleRate = 16000,
        double minSnrDb = 0.0,
        double maxSnrDb = 20.0,
        int seed = 7)
    {
        if (perClass <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(perClass), "perClass must be positive.");
        }

        var rng = new Random(seed);
        double nyquist = sampleRate / 2.0;
        var samples = new List<LabeledAudio>();

        for (int i = 0; i < perClass; i++)
        {
            double snr = Lerp(minSnrDb, maxSnrDb, rng.NextDouble());

            // tone: anywhere in a wide band that overlaps the AM carrier band.
            double toneFreq = Lerp(300.0, 1600.0, rng.NextDouble());
            samples.Add(Noisy("tone", SignalGenerator.Sine(toneFreq, seconds, sampleRate), snr, rng));

            // sweep: span is sometimes narrow + slow, so it looks almost like a tone.
            double sweepStart = Lerp(300.0, 1200.0, rng.NextDouble());
            double span = Lerp(60.0, 2500.0, rng.NextDouble());
            double sweepEnd = Math.Min(nyquist - 100.0, sweepStart + span);
            samples.Add(Noisy("sweep", SignalGenerator.Chirp(sweepStart, sweepEnd, seconds, sampleRate), snr, rng));

            // noise: alternate white / pink (pink shifts energy low, like voiced sounds).
            NoiseColor color = (i % 2 == 0) ? NoiseColor.White : NoiseColor.Pink;
            AudioBuffer noiseBase = color == NoiseColor.Pink
                ? AudioAugment.AddNoiseAtSnr(SignalGenerator.Sine(1.0, seconds, sampleRate, 0.0), 0.0, NoiseColor.Pink, seed: 5000 + i)
                : SignalGenerator.WhiteNoise(seconds, sampleRate, 0.8, seed: 5000 + i);
            samples.Add(Augment(new LabeledAudio("noise", noiseBase), rng));

            // modulated: carrier overlaps the tone band; shallow depth resembles a tone.
            double carrier = Lerp(300.0, 1600.0, rng.NextDouble());
            double modRate = Lerp(3.0, 16.0, rng.NextDouble());
            double depth = Lerp(0.1, 0.9, rng.NextDouble());
            AudioBuffer am = SignalGenerator.AmplitudeModulated(carrier, modRate, seconds, sampleRate, 0.8, depth);
            samples.Add(Noisy("modulated", am, snr, rng));
        }

        return samples;
    }

    /// <summary>
    /// Builds a generalization split: training tones/carriers/sweeps live in a LOW
    /// frequency regime and the test set in a disjoint HIGH regime, at a fixed
    /// moderate SNR. This measures whether the features generalize across frequency
    /// rather than memorizing the training band.
    /// </summary>
    public static DatasetSplit BuildGeneralizationSplit(
        int perClassTrain = 16,
        int perClassTest = 8,
        double seconds = 0.5,
        int sampleRate = 16000,
        double snrDb = 12.0,
        int seed = 21)
    {
        var rng = new Random(seed);
        var train = BuildRegime(perClassTrain, lowHz: 300.0, highHz: 850.0, seconds, sampleRate, snrDb, rng, noiseSeedBase: 8000);
        var test = BuildRegime(perClassTest, lowHz: 1150.0, highHz: 1900.0, seconds, sampleRate, snrDb, rng, noiseSeedBase: 9000);
        return new DatasetSplit(train, test);
    }

    private static List<LabeledAudio> BuildRegime(
        int perClass, double lowHz, double highHz, double seconds, int sampleRate, double snrDb, Random rng, int noiseSeedBase)
    {
        double nyquist = sampleRate / 2.0;
        var samples = new List<LabeledAudio>();
        for (int i = 0; i < perClass; i++)
        {
            double tone = Lerp(lowHz, highHz, rng.NextDouble());
            samples.Add(Noisy("tone", SignalGenerator.Sine(tone, seconds, sampleRate), snrDb, rng));

            double start = Lerp(lowHz, highHz, rng.NextDouble());
            double end = Math.Min(nyquist - 100.0, start + Lerp(200.0, 1500.0, rng.NextDouble()));
            samples.Add(Noisy("sweep", SignalGenerator.Chirp(start, end, seconds, sampleRate), snrDb, rng));

            samples.Add(Augment(new LabeledAudio("noise", SignalGenerator.WhiteNoise(seconds, sampleRate, 0.8, seed: noiseSeedBase + i)), rng));

            double carrier = Lerp(lowHz, highHz, rng.NextDouble());
            double depth = Lerp(0.3, 0.9, rng.NextDouble());
            samples.Add(Noisy("modulated", SignalGenerator.AmplitudeModulated(carrier, Lerp(4.0, 14.0, rng.NextDouble()), seconds, sampleRate, 0.8, depth), snrDb, rng));
        }

        return samples;
    }

    private static LabeledAudio Noisy(string label, AudioBuffer clean, double snrDb, Random rng)
    {
        NoiseColor color = rng.NextDouble() < 0.5 ? NoiseColor.White : NoiseColor.Pink;
        AudioBuffer noisy = AudioAugment.AddNoiseAtSnr(clean, snrDb, color, seed: rng.Next());
        return Augment(new LabeledAudio(label, noisy), rng);
    }

    private static LabeledAudio Augment(LabeledAudio sample, Random rng)
    {
        AudioBuffer audio = sample.Audio;
        // Light, label-preserving augmentation: gain and a small DC offset.
        double gain = Lerp(0.6, 1.0, rng.NextDouble());
        audio = AudioAugment.ApplyGain(audio, gain);
        if (rng.NextDouble() < 0.3)
        {
            audio = AudioAugment.AddDcOffset(audio, Lerp(-0.02, 0.02, rng.NextDouble()));
        }

        return sample with { Audio = audio };
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
