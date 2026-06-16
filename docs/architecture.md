# Architecture Notes

The project keeps domain logic separate from command-line and experiment
concerns. The design bias is boring, explicit, testable code over framework
machinery.

## Projects

| Project | Responsibility | Depends on |
| --- | --- | --- |
| `AudioResearch.Core` | WAV IO, synthetic audio generation, DSP primitives, feature extraction, dataset building. No console IO. | — |
| `AudioResearch.ML` | Feature-vector abstractions, k-NN classifier, baseline train/eval runner. | Core |
| `AudioResearch.Cli` | Argument parsing, filesystem IO, user-facing output, exit codes. | Core, ML |
| `AudioResearch.Core.Tests` | Deterministic DSP / feature / WAV / ML tests. | Core, ML |
| `AudioResearch.Cli.Tests` | CLI command-contract and exit-code tests. | Cli, Core, ML |

## Core namespaces

- `AudioResearch.Core.Audio`
  - `AudioBuffer` — interleaved float samples + sample rate + channel count;
    `ToMono`, `Peak`, `Duration`, `FrameCount`.
  - `SignalGenerator` — deterministic sine, chirp, white noise (seeded),
    amplitude-modulated, and speech-like envelope generators.
  - `WavFile` — strict 16-bit PCM reader/writer (mono and multi-channel),
    stream-based with `leaveOpen` so callers own stream lifetime.
  - `AudioAugment` — deterministic controlled-SNR additive noise (white/pink),
    gain, DC offset, and clipping, used to build harder datasets.
- `AudioResearch.Core.Dsp`
  - `Windows.Hann` (periodic), `Framing` (overlapping frames + window apply),
    `Fourier` (self-contained radix-2 FFT, magnitude spectrum, bin frequency),
    `Stft` (windowed short-time magnitude transform).
- `AudioResearch.Core.Features`
  - `BandEnergy` (linear bands), `CochlearFilterBank` (ERB-spaced triangular
    filters), `FeatureExtractor` (per-frame band energies + a fixed-length
    summary vector with stable, named fields).
- `AudioResearch.Core.Experiments`
  - `DatasetBuilder` — deterministic labeled fixtures: `Build` (separable),
    `BuildVaried` (overlapping + noisy), and `BuildGeneralizationSplit`
    (frequency-regime holdout).

The CLI adds `dataset fetch fsdd`, which downloads a pinned, SHA-256-verified
release of the Free Spoken Digit Dataset into a git-ignored folder (see
[datasets.md](datasets.md)); the core library itself never performs network IO.

## Design decisions

- **No third-party runtime dependencies.** The FFT and k-NN are implemented in
  ~150 lines each so the build is offline-friendly and every result is
  deterministic across machines. A mature FFT package would be a reasonable
  swap later; the surface (`Fourier`) is small and isolated.
- **Hand-rolled CLI** (`Options` + `CliApp`) instead of a parser dependency,
  per the v0.1 brief. Commands return explicit exit codes and write through
  injected `TextWriter`s, which makes them unit-testable without spawning a
  process.
- **Stable output schemas.** JSON outputs carry a `schemaVersion`; the feature
  summary vector has fixed length and names so it can feed both the JSON export
  and the ML pipeline.

## Pipeline

```text
WAV / generator -> AudioBuffer -> normalize -> frame + Hann window -> FFT magnitude (STFT)
   -> cochlear filter bank (ERB) -> log energies -> per-frame bands (CSV)
                                                  -> summary vector (JSON, ML features)
                                                       -> k-NN baseline -> report (JSON)
```

## Exit codes

- `0` success
- `1` handled user error (missing/invalid file, bad data)
- `2` usage error (unknown command, missing argument)

## Limitations

The value of this project is careful engineering in a research-adjacent audio
domain, not solving hearing science. The cochlear filter bank is an
approximation (see [signal-processing-notes.md](signal-processing-notes.md))
and the ML task is intentionally easy.
