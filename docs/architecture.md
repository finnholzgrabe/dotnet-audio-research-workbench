# Architecture Notes

The project should keep domain logic separate from command-line and experiment concerns.

## Planned Components

- `AudioResearch.Core`: WAV IO, synthetic audio generation, DSP primitives, feature extraction.
- `AudioResearch.Cli`: command parsing, filesystem IO, user-facing output, exit codes.
- `AudioResearch.ML`: simple model abstractions and baseline training/inference hooks.
- `tests`: deterministic algorithm and CLI tests.
- `experiments`: optional Python notebooks/scripts for exploration only.

## Design Bias

Prefer small, explicit modules with tests over broad framework usage.

The value of this project is not that it solves hearing science. The value is that it shows careful engineering in a research-adjacent audio domain.
