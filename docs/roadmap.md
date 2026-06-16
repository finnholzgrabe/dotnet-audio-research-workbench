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

### 🔜 1. Speaker-independent split for the FSDD digit task
**Why:** the current FSDD digit run uses a random split, so the same speaker
appears in train and test — accuracy (0.941) is optimistic. A speaker-independent
(leave-speakers-out) protocol is the honest measure of digit generalization.
**How:** add a group-aware split keyed on the parsed speaker; in the CLI expose
`--group-by speaker`. Reuse `BaselineRunner.RunWithSplit`.
**Acceptance:** report records `splitStrategy: "group-holdout(speaker)"`; expect a
visibly lower, honest digit accuracy; documented in the experiment log.

### ✅ 2. Visualization / reporting
Done: `experiments/python/visualize.py` (matplotlib) renders one figure with the
pipeline (waveform → STFT spectrogram → cochlear band-energy heatmap) and the ML
results (confusion matrix + accuracy comparison) from the CLI's CSV/JSON
artifacts. Showcased in the README (`docs/images/overview.png`).
**Remaining:** publish it via GitHub Pages (see item 10), and optionally add a
.NET-native SVG export so no Python is required.

### 🔜 3. Per-class metrics in the report
**Why:** accuracy alone hides class imbalance/confusions.
**How:** add precision/recall/F1 per class to `BaselineReport` and the JSON.
**Acceptance:** report includes per-class metrics; covered by a test.

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
