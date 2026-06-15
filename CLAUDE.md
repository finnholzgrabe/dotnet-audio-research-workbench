# CLAUDE.md

You are acting as a senior software architect and hands-on implementation lead.

Build this repository into a small, credible, publishable research-software project:

**Cochlear Audio Research Workbench**

This is not a medical device project and not a cochlear implant simulator. It is a learning and portfolio project that demonstrates careful audio signal processing, testable research software, and a small machine-learning baseline around cochlear-inspired audio features.

The project should create honest adjacent evidence for roles that ask for:

- C#/.NET research software
- high-quality software modules with documentation
- unit, integration, and system tests
- audio/signal-processing literacy
- measurement or data-acquisition workflows
- collaboration with researchers or algorithm developers

Do not overclaim medical, clinical, or production signal-processing expertise.

## Product Intent

Build a cross-platform developer/research workbench that can:

1. Load WAV audio files and generate small synthetic fixtures.
2. Apply transparent DSP steps such as framing, windowing, FFT/STFT, band-energy extraction, and cochlear-inspired filter-bank features.
3. Run a tiny ML baseline on extracted features.
4. Produce reproducible CLI output, plots, metrics, and experiment logs.
5. Prove correctness with tests around the DSP pipeline and CLI behavior.

The first publishable version should feel like a compact research tool, not a toy notebook.

## Non-Negotiable Principles

- Keep the repository publishable at every milestone.
- Prefer a small, deeply tested slice over a broad unfinished demo.
- Keep C#/.NET as the primary implementation language because that is the target evidence for MED-EL-style roles.
- Python may be used for optional ML experimentation, plotting, or model training, but the main CLI and core DSP pipeline should be in .NET.
- Use public-domain, generated, or clearly licensed sample data only.
- Do not commit large datasets, generated binaries, model weights, or personal files.
- Do not imply medical validation, clinical utility, hearing-aid performance, or cochlear implant safety.
- Every claim in README must be backed by code, tests, docs, or an experiment artifact.
- Every public-facing document should be understandable to a senior engineer reviewing the project in five minutes.

## Recommended Tech Stack

Use installed stable tooling where possible. Do not chase the newest version unless the local machine already supports it.

- .NET SDK: use the installed stable/LTS SDK found by `dotnet --version`; document the version in README.
- Core language: C#.
- Tests: xUnit or NUnit. Prefer xUnit unless the repository already has another convention.
- CLI: `System.CommandLine` if available and stable locally; otherwise a minimal hand-rolled CLI is acceptable for v0.1.
- Audio parsing: start with a small, explicit WAV reader for PCM 16-bit mono/stereo or a stable .NET audio library if it is easy to add.
- FFT: use a mature NuGet package if available; otherwise implement only the smallest needed transform and test it carefully.
- Optional Python: `numpy`, `scipy`, `scikit-learn`, `matplotlib`, `soundfile` or `librosa` for experiments only.
- Optional model exchange: ONNX later, not in the first milestone unless the implementation is already clean.

If package installation or internet access is unavailable, build the deterministic synthetic-audio path first.

## Target Repository Shape

Create and evolve toward this structure:

```text
.
|-- CLAUDE.md
|-- README.md
|-- LICENSE
|-- .editorconfig
|-- .gitignore
|-- src
|   |-- AudioResearch.Core
|   |   |-- Audio
|   |   |-- Dsp
|   |   |-- Features
|   |   `-- Experiments
|   |-- AudioResearch.Cli
|   `-- AudioResearch.ML
|-- tests
|   |-- AudioResearch.Core.Tests
|   `-- AudioResearch.Cli.Tests
|-- docs
|   |-- architecture.md
|   |-- signal-processing-notes.md
|   |-- experiment-log.md
|   |-- medical-disclaimer.md
|   `-- publishing-checklist.md
|-- samples
|   |-- README.md
|   `-- generated
|-- experiments
|   |-- README.md
|   `-- python
|-- artifacts
|   `-- README.md
`-- .github
    `-- workflows
        `-- ci.yml
```

`samples/generated` may contain tiny generated WAV files if they are deterministic and small. `artifacts` should explain what gets generated but should not commit heavy outputs.

## Architecture

Use a clean separation:

- `AudioResearch.Core`: pure domain logic. No console IO, no filesystem assumptions except explicitly passed streams/paths.
- `AudioResearch.Cli`: command parsing, file IO, user-facing output, exit codes.
- `AudioResearch.ML`: feature-vector abstractions and simple ML baseline integration. Keep it replaceable.
- `tests`: deterministic tests for algorithms, fixtures, CLI exit behavior, and output contracts.
- `docs`: concise engineering notes that explain tradeoffs and limitations.

Prefer boring, explicit code. A reviewer should see correctness, testability, and humility.

## Domain Scope

Version 0.1 should focus on these technical pieces:

- WAV loading for PCM fixtures.
- Synthetic audio generation:
  - sine tone
  - sweep/chirp
  - white noise
  - amplitude-modulated tone
  - simple speech-like envelope fixture if practical
- DSP primitives:
  - normalize samples
  - frame audio into overlapping windows
  - Hann window
  - magnitude spectrum
  - STFT
  - band-energy features
  - cochlear-inspired filter bank as an approximation, clearly documented
- Feature export:
  - CSV
  - JSON summary
- ML baseline:
  - classify generated fixtures into simple classes such as tone, sweep, noise, and modulated tone
  - report accuracy on deterministic train/test splits
  - avoid inflated claims

Version 0.2 can add:

- microphone capture abstraction
- optional real audio dataset workflow
- plots
- ONNX inference
- benchmark command
- GitHub Pages demo of generated plots

## Step-by-Step Execution Plan

Work in small commits. Each step must leave the repo in a valid state.

### Step 0 - Repo Hygiene

1. Confirm the current directory is the project root.
2. Initialize Git if needed:
   - `git init`
   - create or switch to branch `main`
3. Ensure these files exist:
   - `README.md`
   - `LICENSE`
   - `.gitignore`
   - `.editorconfig`
   - `CLAUDE.md`
4. Add README sections:
   - What this is
   - What this is not
   - Quick start
   - Architecture
   - Roadmap
   - Medical disclaimer
5. Add `docs/medical-disclaimer.md`.
6. Commit as `chore: initialize project brief`.

Acceptance:

- `git status` is clean after commit.
- README does not claim unfinished features.

### Step 1 - .NET Solution Skeleton

1. Create a solution:
   - `src/AudioResearch.Core`
   - `src/AudioResearch.Cli`
   - `src/AudioResearch.ML`
   - `tests/AudioResearch.Core.Tests`
   - `tests/AudioResearch.Cli.Tests`
2. Add project references:
   - CLI references Core and ML.
   - ML references Core only if needed.
   - Tests reference the projects under test.
3. Add a minimal CLI command:
   - `audio-research --help`
   - `audio-research version`
4. Add CI that runs restore, build, and test.
5. Commit as `chore: add dotnet solution skeleton`.

Acceptance:

- `dotnet build` succeeds.
- `dotnet test` succeeds.
- CLI help is readable.

### Step 2 - Audio Fixtures and WAV IO

1. Implement deterministic synthetic audio generation in Core.
2. Implement PCM WAV writing for generated fixtures.
3. Implement PCM WAV reading for mono and stereo files, converting to normalized floating-point samples.
4. Add CLI commands:
   - `generate tone`
   - `generate noise`
   - `inspect <wav>`
5. Add tests:
   - generated tone length and sample rate
   - WAV round-trip
   - invalid file handling
6. Commit as `feat: add audio fixtures and wav io`.

Acceptance:

- CLI can generate a short WAV into `samples/generated`.
- CLI can inspect that WAV and report sample rate, channels, duration, and peak level.
- Tests are deterministic.

### Step 3 - DSP Pipeline

1. Add framing and Hann window.
2. Add magnitude spectrum and STFT.
3. Add band-energy extraction.
4. Add a documented cochlear-inspired filter bank.
   - Keep it modest and cite that it is an approximation.
   - Do not call it a validated cochlear model.
5. Add CLI commands:
   - `features bands <wav> --out features.csv`
   - `features summary <wav> --out summary.json`
6. Add tests:
   - frame counts
   - window endpoints
   - energy concentration for sine tones
   - stable CSV/JSON schema
7. Commit as `feat: add dsp feature pipeline`.

Acceptance:

- A generated sine tone shows dominant energy in the expected band.
- JSON and CSV outputs are stable enough for examples.

### Step 4 - ML Baseline

1. Build a tiny generated dataset.
2. Extract features for each sample.
3. Train a simple classifier.
   - Prefer simple models: logistic regression, k-nearest neighbors, random forest, or ML.NET equivalent.
   - If using Python, keep it in `experiments/python` and export only small metrics/artifacts.
4. Add CLI command:
   - `ml baseline --dataset samples/generated --out artifacts/baseline-report.json`
5. Document what the baseline proves and what it does not prove.
6. Commit as `feat: add generated-audio ml baseline`.

Acceptance:

- Baseline runs from a clean checkout using generated data.
- Report includes dataset summary, features used, model type, train/test split, and accuracy.
- README states that this is a toy baseline for engineering evidence, not medical ML.

### Step 5 - Research Documentation

1. Write `docs/signal-processing-notes.md`.
2. Write `docs/architecture.md`.
3. Write `docs/experiment-log.md` with one reproducible run.
4. Include diagrams if they clarify the pipeline.
5. Commit as `docs: document dsp pipeline and experiment`.

Acceptance:

- A reviewer can understand the pipeline without reading every line of code.
- Limitations are explicit.

### Step 6 - Publishable Polish

1. Add screenshots or generated plots if available.
2. Add README quick-start commands that actually work.
3. Add GitHub topics suggestion:
   - `audio-processing`
   - `signal-processing`
   - `dotnet`
   - `machine-learning`
   - `research-software`
   - `dsp`
4. Add `docs/publishing-checklist.md`.
5. Run a clean verification:
   - `dotnet format`
   - `dotnet build`
   - `dotnet test`
   - run all README quick-start commands
6. Commit as `docs: prepare repository for publication`.

Acceptance:

- README quick start works from scratch.
- CI is green.
- No generated heavy artifacts are committed.

## Git and Publishing Workflow

Use non-destructive Git commands. Do not rewrite user history without explicit permission.

Recommended local workflow:

```sh
git init
git branch -M main
git add .
git commit -m "chore: initialize cochlear audio research workbench"
```

When ready to publish:

```sh
gh repo create cochlear-audio-research-workbench --public --source=. --remote=origin --push
```

If GitHub CLI is not authenticated, tell the user exactly what command failed and what to run manually. Do not paste tokens into the terminal or docs.

## README Positioning

Use this framing:

> A small .NET-first research-software workbench for experimenting with audio feature extraction, cochlear-inspired filter-bank features, and a reproducible toy ML baseline.

Avoid this framing:

- "cochlear implant simulator"
- "medical-grade"
- "clinical"
- "hearing restoration"
- "validated auditory model"
- "production-ready ML"

## CV and Cover-Letter Evidence Target

Once v0.1 is complete, the project should support this honest statement:

> Built a .NET-first audio research workbench with deterministic WAV fixtures, tested DSP primitives, cochlear-inspired band-energy features, and a reproducible ML baseline for generated audio classification.

Do not use this statement until the code and tests exist.

## Quality Bar

Before calling any milestone done:

- Run the full test suite.
- Run the CLI manually for the README examples.
- Check that generated outputs are deterministic or documented as non-deterministic.
- Check `.gitignore` before committing.
- Keep docs aligned with actual behavior.
- Prefer removing a feature over shipping a flaky or misleading feature.

## Security and Safety Notes

- Do not upload user audio by default.
- Do not collect telemetry.
- Do not include personal recordings in the repo.
- Make microphone capture opt-in.
- Keep data paths local and explicit.
- Document dataset licenses before using external audio.

## First Task For Claude

Start with Step 0 and Step 1 only. Do not implement DSP until the solution skeleton, README, CI, and tests are in place.

Report:

- files created
- commands run
- verification results
- next recommended step
