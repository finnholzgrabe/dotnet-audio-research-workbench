# Experiment Log

A reproducible record of runs. Every result here is regenerable from a clean
checkout because all randomness is seeded.

## 2026-06-16 — v0.1 baseline on synthetic audio

**Environment:** .NET SDK 8.0.127, macOS (darwin). The pipeline has no
third-party runtime dependencies, so results are platform-independent.

### Commands

```sh
dotnet build AudioResearch.sln -c Release
DLL=src/AudioResearch.Cli/bin/Release/net8.0/AudioResearch.Cli.dll

dotnet "$DLL" generate tone --freq 440 --seconds 1 --out samples/generated/tone_440hz.wav
dotnet "$DLL" features summary samples/generated/tone_440hz.wav --out artifacts/tone-summary.json
dotnet "$DLL" ml baseline --out artifacts/baseline-report.json
```

### Dataset

`DatasetBuilder.Build(perClass: 12, seconds: 0.5, sampleRate: 16000)` produces
**48 samples** across four balanced classes — `tone`, `sweep`, `noise`,
`modulated` — with parameters spread deterministically and seeded noise.

### Features

Each sample is reduced to a **53-dimensional** summary vector (24 band means,
24 band temporal stds, plus spectral centroid mean/std, spectral flatness, RMS,
and zero-crossing rate). See `FeatureExtractor.Summarize`.

Sanity check — a 440 Hz tone's per-frame cochlear energies peak in the bands
centered near 400–490 Hz, as expected (from `artifacts/tone-bands.csv`).

### Model and split

- Model: k-nearest-neighbours (`k = 3`), z-score standardized features,
  squared-Euclidean distance, ordinal tie-breaking (fully deterministic).
- Split: stratified, seeded (`seed = 42`), `testFraction = 0.3` →
  **32 train / 16 test**.

### Result

```text
Accuracy:  1.000 on the held-out test set
```

Confusion matrix (rows = actual, cols = predicted), 4 per class in the test set:

| actual \ pred | modulated | noise | sweep | tone |
| --- | --- | --- | --- | --- |
| modulated | 4 | 0 | 0 | 0 |
| noise     | 0 | 4 | 0 | 0 |
| sweep     | 0 | 0 | 4 | 0 |
| tone      | 0 | 0 | 0 | 4 |

### Interpretation

The classifier perfectly separates the four classes. This is **expected and not
impressive on its own**: the synthetic classes differ along obvious, engineered
axes (spectral concentration, flatness, centroid motion, temporal modulation),
and the features were chosen to expose exactly those axes. The value of the run
is that it demonstrates an end-to-end, tested, reproducible pipeline from audio
to features to a scored model — not that the task is hard or medically
meaningful.

### Reproducibility notes

- Re-running `ml baseline` with the same flags yields identical accuracy and an
  identical report (asserted by `BaselineTests.Baseline_OnSyntheticDataset_IsAccurateAndDeterministic`).
- Generated WAVs and `artifacts/` JSON/CSV are git-ignored; they are
  regenerable from the commands above.
