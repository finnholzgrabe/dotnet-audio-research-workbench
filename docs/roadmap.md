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

- ✅ 4. Richer features (MFCC + deltas), selectable via `--features cochlear|mfcc`.
  Honest side-by-side on FSDD digits: speaker-independent 0.603 → 0.634, random
  split 0.941 → 0.964.
- ✅ 5. k-fold cross-validation (`--folds N`): stratified, deterministic, reports
  per-fold accuracy + mean ± std and a pooled confusion/per-class report. E.g.
  noisy synthetic 5-fold 0.850 ± 0.064; FSDD speaker (MFCC) 0.986 ± 0.004.
- ✅ 6. `benchmark` command: times FFT (256–4096), STFT, and cochlear/MFCC feature
  extraction; stable JSON schema (records runtime/OS) for regression tracking.
  Timings are wall-clock / non-deterministic by nature.
- ✅ 7. ONNX seam (no runtime dep): `IFeatureClassifier` abstraction implemented by
  `KnnClassifier`; an ONNX-backed model can implement the same interface. See
  [onnx.md](onnx.md). The native runtime stays optional/unbundled.
- 💡 8. Microphone capture abstraction (strictly opt-in, no upload, documented).
- 💡 9. More datasets via the documented fetch pattern (e.g. ESC-10, CC BY) with
  resampling to a common rate.
- ✅ 10. GitHub Pages demo: static `site/index.html` showcasing the visualization,
  results, and quick start; `.github/workflows/pages.yml` deploys it on push.
  (Requires enabling Pages → "GitHub Actions" in repo settings after first push.)

## Principles to keep

- Keep it publishable and honest at every step; no medical/clinical claims.
- Deterministic outputs; never commit datasets, binaries, or model weights.
- CI stays offline (no dataset downloads); network features are opt-in commands.
