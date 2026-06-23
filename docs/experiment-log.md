# Experiment Log

A reproducible record of runs. Every result here is regenerable from a clean
checkout because all randomness is seeded (the FSDD runs additionally require the
one-off `dataset fetch fsdd` download).

## 2026-06-16 — speaker-independent (leave-speakers-out) split

Added a group-aware split (`ml baseline --group-by speaker`) so whole speakers are
held out of training. This is the honest protocol for measuring whether digit
recognition generalizes to unheard voices.

| FSDD digit task | Split | Held-out speakers | Accuracy |
| --- | --- | --- | --- |
| `--labels digit` | random (1750/750) | — | 0.941 |
| `--labels digit --group-by speaker` | group-holdout (1500/1000) | jackson, nicolas | **0.603** |

Sanity check — `--labels speaker --group-by speaker` gives **0.000**: the held-out
speakers are classes the model never saw, so it cannot possibly predict them. This
confirms the group split isolates speakers with no leakage.

Takeaway: the 0.941 random-split digit number was optimistic; 0.603 is the
defensible generalization figure. The mechanism (random vs. grouped split), not a
model change, accounts for the gap.

## 2026-06-16 — harder datasets and real audio (FSDD)

Same environment as below. All runs use `seed 42` and default options unless noted.

| Run | Dataset | Split | Accuracy |
| --- | --- | --- | --- |
| `ml baseline --difficulty easy` | separable synthetic | stratified-random | 1.000 |
| `ml baseline` (default) | noisy synthetic (overlap + SNR 0–20 dB) | stratified-random | 0.708 |
| `ml baseline --split regime` | noisy synthetic | low→high freq holdout | 0.825 |
| `ml baseline --dataset data/fsdd/recordings --labels speaker` | FSDD (real, 5 speakers, 2500 clips) | stratified-random (1750/750) | 0.976 |
| `ml baseline --dataset data/fsdd/recordings --labels digit` | FSDD (real, 10 digits) | stratified-random | 0.941 |

Interpretation:

- The drop from 1.000 (easy) to **0.708** (noisy) and **0.825** (regime holdout)
  is the point: overlapping classes, added noise, and a train/test split across
  *different frequency bands* make the task genuinely non-trivial, so the number
  is informative rather than decorative.
- **FSDD** is real recorded speech (CC BY-SA 4.0, pinned release `v1.0.9`, 8 kHz
  mono). Speaker identity is well captured by the spectral-envelope features
  (0.976); digit identity less so relative to dedicated speech features, but a
  simple k-NN over 53 generic features still reaches 0.941 on this clean corpus.
- FSDD uses a random split, so speakers appear in both train and test; this is a
  standard split, not a speaker-independent protocol. See
  [datasets.md](datasets.md) for license and handling.

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
