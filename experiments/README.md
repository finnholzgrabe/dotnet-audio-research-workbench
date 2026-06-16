# Experiments

Optional, exploratory work that is **not** part of the shipped .NET pipeline.

The core DSP pipeline and CLI are C#/.NET. Python here is for optional plotting
or cross-checking only, and must never become a dependency of the build or tests.

## Layout

- `python/` — optional scripts (plots, sanity checks). Keep outputs out of git;
  write any generated images/CSVs to `artifacts/`.

## Conventions

- Export only small metrics/artifacts, never large data or model weights.
- Prefer reading the deterministic JSON/CSV the CLI already produces over
  re-implementing DSP in Python.
