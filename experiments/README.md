# Experiments

Optional, exploratory work that is **not** part of the shipped .NET pipeline.

The core DSP pipeline and CLI are C#/.NET. Python here is for optional plotting
only, and must never become a dependency of the build or tests.

## Visualization (`python/visualize.py`)

Renders a single figure showing the transformation pipeline (waveform → STFT
spectrogram → cochlear band-energy heatmap) and the ML results (confusion matrix
+ accuracy comparison) from the artifacts the .NET CLI produces.

### One-time setup (isolated venv, git-ignored)

```sh
python3 -m venv experiments/python/.venv
experiments/python/.venv/bin/python -m pip install numpy matplotlib
```

### Generate inputs with the .NET CLI, then render

```sh
DLL=src/AudioResearch.Cli/bin/Release/net8.0/AudioResearch.Cli.dll

# pipeline inputs
dotnet "$DLL" generate chirp --start 200 --end 6000 --seconds 1.2 --out samples/generated/chirp_demo.wav
dotnet "$DLL" features bands samples/generated/chirp_demo.wav --out artifacts/chirp-bands.csv --filters 28

# ML reports across modes
dotnet "$DLL" ml baseline --difficulty easy  --out artifacts/easy.json
dotnet "$DLL" ml baseline                     --out artifacts/noisy.json
dotnet "$DLL" ml baseline --split regime      --out artifacts/regime.json
# optional real data: dotnet "$DLL" dataset fetch fsdd
#                     dotnet "$DLL" ml baseline --dataset data/fsdd/recordings --labels speaker --out artifacts/fsdd-speaker.json

# render
experiments/python/.venv/bin/python experiments/python/visualize.py \
  --wav samples/generated/chirp_demo.wav \
  --bands artifacts/chirp-bands.csv \
  --reports artifacts/easy.json artifacts/noisy.json artifacts/regime.json \
  --out artifacts/overview.png
```

`--wav` / `--bands` / `--reports` are all optional; panels adapt to what you pass.
`--confusion <report.json>` chooses which run drives the confusion panel (default:
the lowest-accuracy run, which shows the most off-diagonal structure). If `--bands`
is omitted, the cochleagram is approximated in Python from the spectrogram.

## Conventions

- Export only small artifacts; never large data or model weights.
- Prefer reading the deterministic JSON/CSV the CLI already produces over
  re-implementing DSP in Python.
