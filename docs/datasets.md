# Datasets

This project ships **no audio data**. Fixtures are either generated on the fly or
fetched on demand into git-ignored local folders. External datasets are used only
when clearly licensed, and their license/attribution is recorded here.

## Synthetic (default, offline)

The `DatasetBuilder` produces deterministic labeled fixtures with no download:

- `Build` — trivially separable classes (tone / sweep / noise / modulated).
- `BuildVaried` — overlapping parameter ranges + seeded additive noise at a range
  of SNRs + light gain/DC augmentation (a genuinely harder task).
- `BuildGeneralizationSplit` — train on a low-frequency regime, test on a disjoint
  high-frequency regime (measures generalization, not memorization).

This is the path CI uses, because CI must not depend on network downloads.

## Free Spoken Digit Dataset (FSDD)

A real, optional dataset for a more credible baseline (speaker or digit
classification).

| | |
| --- | --- |
| Source | <https://github.com/Jakobovski/free-spoken-digit-dataset> |
| Pinned release | `v1.0.9` |
| Archive SHA-256 | `8bc44cde129d505cbbb6b365c09f80c663f2aa77578afc634e6141a2f87100c0` |
| Contents | 2500 recordings, 5 speakers (george, jackson, nicolas, theo, yweweler) |
| Format | 8 kHz, mono, 16-bit PCM WAV (read directly by `WavFile`, no resampling) |
| Size | ~12 MB compressed |
| Filename pattern | `{digit}_{speaker}_{index}.wav` (e.g. `7_jackson_32.wav`) |
| License | **CC BY-SA 4.0** (<https://creativecommons.org/licenses/by-sa/4.0/>) |

### Fetch and use

```sh
DLL=src/AudioResearch.Cli/bin/Release/net8.0/AudioResearch.Cli.dll

# Download + verify checksum + extract into data/fsdd/ (git-ignored)
dotnet "$DLL" dataset fetch fsdd

# Speaker classification (5 classes)
dotnet "$DLL" ml baseline --dataset data/fsdd/recordings --labels speaker --out artifacts/fsdd-speaker.json

# Digit classification (10 classes) for contrast
dotnet "$DLL" ml baseline --dataset data/fsdd/recordings --labels digit --out artifacts/fsdd-digit.json

# Speaker-independent digit classification (leave-speakers-out) -- the honest measure
dotnet "$DLL" ml baseline --dataset data/fsdd/recordings --labels digit --group-by speaker
```

The fetch command verifies the archive against the pinned SHA-256 before use and
writes `data/fsdd/ATTRIBUTION.txt`.

### Random vs. speaker-independent split

A plain random split lets the same speaker land in both train and test, which
inflates the apparent digit accuracy (0.941). With `--group-by speaker`, whole
speakers are held out, so the model is tested on voices it never heard. That is
the honest generalization number for digit recognition:

| Split | Digit accuracy |
| --- | --- |
| random (`--labels digit`) | 0.941 |
| speaker-independent (`--group-by speaker`) | 0.603 |

Sanity: `--labels speaker --group-by speaker` yields 0.000 — held-out speakers are
unseen classes, confirming the split leaks nothing.

### Licensing and handling

- **Attribution (CC BY-SA 4.0):** FSDD by Jakobovski et al.; share-alike applies to
  redistributed data/derivatives of the data.
- The data is **never committed** (`data/` and `*.wav` are git-ignored). It is
  fetched only when the user explicitly runs `dataset fetch`.
- No audio is uploaded anywhere; the only network access is the GET of the pinned
  release archive.

## Adding another dataset

1. Confirm a permissive license (CC0 / CC BY / CC BY-SA preferred; avoid NC if
   commercial reuse matters).
2. Pin a release and record its SHA-256 here.
3. Fetch into a git-ignored folder; never commit the audio.
4. Document size, format, and attribution in this file.
