# Cochlear Audio Research Workbench

A small **.NET-first research-software workbench** for experimenting with audio
feature extraction, cochlear-inspired filter-bank features, and a reproducible
toy ML baseline.

## What this is

- A cross-platform C#/.NET command-line tool (`audio-research`) and a clean,
  tested core library.
- A transparent DSP pipeline: synthetic audio generation, WAV IO, framing,
  Hann windowing, a dependency-free radix-2 FFT/STFT, band energies, and a
  cochlear-inspired (ERB-spaced) triangular filter bank.
- A tiny, reproducible machine-learning baseline that classifies generated
  fixtures (tone / sweep / noise / modulated) from extracted features.
- Deterministic outputs (CSV features, JSON summaries, JSON experiment reports)
  suitable for examples, tests, and CI.

## What this is **not**

- Not a medical device, cochlear implant simulator, or clinical tool.
- Not a validated auditory model. The "cochlear-inspired" filter bank is a
  coarse engineering approximation, clearly documented as such.
- Not production-grade ML. The baseline is a learning/portfolio artifact.

See [docs/medical-disclaimer.md](docs/medical-disclaimer.md).

## Quick start

Requires the .NET SDK (developed and tested against **.NET 8.0 LTS**;
`dotnet --version` reported `8.0.127`).

```sh
# Build and test
dotnet build AudioResearch.sln -c Release
dotnet test AudioResearch.sln

# Run the CLI (via dotnet run)
dotnet run --project src/AudioResearch.Cli -c Release -- --help
dotnet run --project src/AudioResearch.Cli -c Release -- version
```

A typical end-to-end session:

```sh
DLL=src/AudioResearch.Cli/bin/Release/net8.0/AudioResearch.Cli.dll

# 1. Generate deterministic fixtures
dotnet "$DLL" generate tone  --freq 440 --seconds 1 --out samples/generated/tone_440hz.wav
dotnet "$DLL" generate noise --seconds 1            --out samples/generated/noise.wav
dotnet "$DLL" generate chirp --start 200 --end 4000 --seconds 1 --out samples/generated/chirp.wav

# 2. Inspect a file
dotnet "$DLL" inspect samples/generated/tone_440hz.wav

# 3. Extract features
dotnet "$DLL" features bands   samples/generated/tone_440hz.wav --out artifacts/tone-bands.csv
dotnet "$DLL" features summary samples/generated/tone_440hz.wav --out artifacts/tone-summary.json

# 4. Run the toy ML baseline (uses a deterministic in-memory dataset)
dotnet "$DLL" ml baseline --out artifacts/baseline-report.json
```

Example `inspect` output:

```text
File:         samples/generated/tone_440hz.wav
Sample rate:  16000 Hz
Channels:     1
Frames:       16000
Duration:     1 s
Peak:         0.8 (-1.9 dBFS)
```

Example `ml baseline` output:

```text
Dataset:   synthetic (deterministic), 48 samples, 4 classes
Model:     k-nearest-neighbours (z-score standardized, Euclidean), k=3
Split:     32 train / 16 test (seed 42)
Accuracy:  1.000 on the held-out test set
```

> The baseline reaches high accuracy because the four synthetic classes are
> deliberately well-separated. This demonstrates a working, tested
> feature-to-classifier pipeline — **not** a hard or medically meaningful task.

## Architecture

```text
src/
  AudioResearch.Core   pure domain logic: Audio, Dsp, Features, Experiments (no console IO)
  AudioResearch.ML     feature-vector abstractions + k-NN baseline (depends on Core)
  AudioResearch.Cli    command parsing, file IO, exit codes (depends on Core + ML)
tests/
  AudioResearch.Core.Tests   DSP / feature / WAV / ML algorithm tests (24)
  AudioResearch.Cli.Tests    CLI contract + exit-code tests (9)
```

`AudioResearch.Core` performs no console IO and only touches the filesystem via
explicitly passed streams/paths. The CLI owns all user-facing IO and exit codes
(`0` success, `1` handled error, `2` usage error). See
[docs/architecture.md](docs/architecture.md) and
[docs/signal-processing-notes.md](docs/signal-processing-notes.md).

## Tests

```sh
dotnet test AudioResearch.sln
```

33 deterministic tests cover WAV round-tripping, generator determinism, framing
and window endpoints, FFT correctness (impulse → flat spectrum, sine → expected
bin), band-energy concentration, feature-schema stability, and the ML baseline's
accuracy and determinism.

## Roadmap

Implemented in **v0.1** (this milestone): synthetic generators, WAV IO, DSP
primitives, cochlear-inspired features, CSV/JSON export, and the k-NN baseline.

Candidate **v0.2** work: microphone capture abstraction (opt-in), optional real
dataset workflow, plots, ONNX inference, a benchmark command, and a GitHub Pages
demo. These are not implemented yet and are not claimed.

## Medical disclaimer

This project is for software-engineering, signal-processing, and machine-learning
learning purposes only. It is not a medical device, cochlear implant simulator,
clinical tool, or validated auditory model. Any cochlear-inspired processing here
is an approximation for experimentation only. Full text:
[docs/medical-disclaimer.md](docs/medical-disclaimer.md).

## License

[MIT](LICENSE).
