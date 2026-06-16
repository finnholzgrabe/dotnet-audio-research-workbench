using AudioResearch.Core.Audio;

namespace AudioResearch.Core.Experiments;

/// <summary>A labeled synthetic audio sample.</summary>
public sealed record LabeledAudio(string Label, AudioBuffer Audio);

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
}
