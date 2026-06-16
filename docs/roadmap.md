# Roadmap & Open Items

A living plan. The README has a short roadmap for readers; this file is the
detailed working list. Status: ✅ done · 🔜 next · 💡 later.

## Done (v0.1 + data work)

- ✅ .NET solution skeleton, CI, deterministic build (warnings-as-errors).
- ✅ WAV IO (16-bit PCM), synthetic generators, DSP primitives (framing, Hann,
  radix-2 FFT, STFT), cochlear-inspired ERB filter bank, feature export (CSV/JSON).
- ✅ k-NN baseline with stratified split + JSON report.
- ✅ Harder synthetic datasets: `BuildVaried` (overlap + SNR), regime holdout.
- ✅ Real-data workflow: `dataset fetch fsdd` (pinned, SHA-256-verified, CC BY-SA 4.0),
  speaker/digit labels.
- ✅ DSP golden tests (Parseval, sine `A·N/2`, filter-bank partition-of-unity).
- ✅ 48 deterministic tests.

## Next

### ✅ 1. Speaker-independent split for the FSDD digit task
Done: `BaselineRunner.RunGrouped` (leave-group-out) + CLI `--group-by speaker`.
The report records `splitStrategy: "group-holdout(speaker)"` plus the held-out
groups. Honest result: FSDD digit accuracy drops from **0.941** (random, speakers
leak) to **0.603** (speaker-independent). Sanity check: classifying *speakers*
leave-speaker-out gives 0.000, confirming no leakage.

### ✅ 2. Visualization / reporting
Done: `experiments/python/visualize.py` (matplotlib) renders one figure with the
pipeline (waveform → STFT spectrogram → cochlear band-energy heatmap) and the ML
results (confusion matrix + accuracy comparison) from the CLI's CSV/JSON
artifacts. Showcased in the README (`docs/images/overview.png`).
**Remaining:** publish it via GitHub Pages (see item 10), and optionally add a
.NET-native SVG export so no Python is required.

### ✅ 3. Per-class metrics in the report
Done: `BaselineReport` carries per-class precision/recall/F1/support and macro-F1,
shown in the console and JSON. Covered by tests (perfect-separation and a forced
misclassification case).

## Later

- 💡 4. Richer features (MFCC + deltas) to lift digit accuracy *honestly*, compared
  side-by-side against the current generic features.
- 💡 5. k-fold cross-validation option (`--folds`) instead of a single split, with
  mean ± std accuracy.
- 💡 6. `benchmark` command: timing of FFT/STFT/feature extraction at sizes, stable
  output for regression tracking.
- 💡 7. ONNX export/inference path (train elsewhere, run in .NET) — keep optional.
- 💡 8. Microphone capture abstraction (strictly opt-in, no upload, documented).
- 💡 9. More datasets via the documented fetch pattern (e.g. ESC-10, CC BY) with
  resampling to a common rate.
- 💡 10. GitHub Pages demo publishing the generated visualization(s).

## Principles to keep

- Keep it publishable and honest at every step; no medical/clinical claims.
- Deterministic outputs; never commit datasets, binaries, or model weights.
- CI stays offline (no dataset downloads); network features are opt-in commands.
