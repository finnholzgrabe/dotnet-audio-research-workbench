# Samples

This folder holds small, deterministic, generated audio fixtures.

- `generated/` is the default output directory for the `generate` command. Its
  contents (`*.wav`) are **git-ignored** because they are fully regenerable:

  ```sh
  DLL=src/AudioResearch.Cli/bin/Release/net8.0/AudioResearch.Cli.dll
  dotnet "$DLL" generate tone  --freq 440 --seconds 1 --out samples/generated/tone_440hz.wav
  dotnet "$DLL" generate noise --seconds 1            --out samples/generated/noise.wav
  dotnet "$DLL" generate chirp --start 200 --end 4000 --seconds 1 --out samples/generated/chirp.wav
  dotnet "$DLL" generate am    --carrier 1000 --mod 8 --seconds 1 --out samples/generated/am.wav
  ```

## Data policy

- Only generated, public-domain, or clearly licensed audio belongs here.
- Do not commit personal recordings or external datasets.
- Document the license of any external audio before using it.
