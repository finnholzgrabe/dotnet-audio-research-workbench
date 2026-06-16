# Signal-Processing Notes

This document explains the DSP choices in `AudioResearch.Core.Dsp` and
`AudioResearch.Core.Features`, and is intentionally honest about what is and is
not modelled.

## Sample representation

Audio is carried as interleaved `float` samples in the nominal range `[-1, 1]`
(`AudioBuffer`). WAV IO reads/writes canonical 16-bit signed PCM; the read path
scales by `1/32768` and the write path scales by `32767` with clamping. The
round-trip is therefore lossy at the 16-bit quantization step (~3 × 10⁻⁵), which
the tests assert with a small tolerance.

## Normalization

`FeatureExtractor.Normalize` performs peak normalization (divide by the maximum
absolute sample) so feature extraction is invariant to overall gain. Silence is
returned unchanged.

## Framing and windowing

- **Framing** (`Framing.Split`) cuts the signal into overlapping frames of
  `frameSize` advancing by `hop`. Partial trailing frames are dropped, so the
  frame count is `1 + (N - frameSize) / hop` (and `0` if `N < frameSize`).
- **Window** (`Windows.Hann`) is the *periodic* Hann window
  `w[n] = 0.5 (1 - cos(2πn / N))`. The periodic form (divisor `N`, not `N-1`)
  is the convention for spectral analysis and overlap-add; it is zero at `n = 0`
  and peaks at `n = N/2`. Defaults: `frameSize = 512`, `hop = 256` (50% overlap)
  at a 16 kHz sample rate.

## FFT and magnitude spectrum

`Fourier.Transform` is an in-place iterative radix-2 Cooley–Tukey FFT
(bit-reversal permutation + butterfly stages) operating on
`System.Numerics.Complex`. Frames are zero-padded up to the next power of two.
`MagnitudeSpectrum` returns the `fftSize/2 + 1` non-negative-frequency bins; bin
`k` maps to `k · sampleRate / fftSize` Hz.

Sanity properties exercised by tests: the FFT of a unit impulse is flat
(all-ones magnitude), and the magnitude spectrum of a pure sine peaks at the bin
nearest its frequency.

## Band energy

`BandEnergy.FromMagnitude` integrates squared magnitude into equal-width linear
bands across `0..Nyquist`. This is the simplest possible spectral summary and is
useful as a baseline against the cochlear filter bank.

## Cochlear-inspired filter bank

`CochlearFilterBank` is a triangular band-pass filter bank whose centers are
spaced evenly on the **ERB-rate scale** (Glasberg & Moore, 1990):

```text
ERB-rate(f) = 21.4 · log10(1 + 0.00437 · f)
```

The basilar membrane resolves frequency on an approximately logarithmic
(tonotopic) axis with finer resolution at low frequencies; ERB spacing captures
that qualitative behaviour. Each filter integrates `weight · magnitude²` across
the FFT bins it overlaps. `FeatureExtractor` then applies `log10(energy + 1e-9)`
compression, mirroring the ear's roughly logarithmic loudness response.

**This is a coarse engineering approximation for feature extraction only.** It is
not a validated auditory model: there is no outer/middle-ear filtering, no
nonlinear compression or two-tone suppression, no level-dependent filter
shapes, and no hair-cell or neural transduction stage. Do not draw perceptual or
clinical conclusions from it.

## Summary feature vector

`FeatureExtractor.Summarize` produces a fixed-length, named vector:

- `bandNN_mean` / `bandNN_std` — per-band mean and temporal standard deviation
  of the log cochlear energies (captures *where* energy sits and how much it
  *fluctuates* over time).
- `spectral_centroid_mean_hz` / `spectral_centroid_std_hz` — mean and spread of
  the power-weighted spectral centroid across frames (a moving centroid, as in a
  chirp, raises the std).
- `spectral_flatness_mean` — geometric/arithmetic mean ratio of power (near 1 for
  noise, near 0 for tones).
- `rms`, `zero_crossing_rate` — simple global time-domain descriptors.

These features are deliberately chosen so the four synthetic classes are
separable: a steady tone concentrates energy and barely moves; noise is spectrally
flat; a sweep moves its centroid; an AM tone has high temporal band-energy
variance.

## References

- B. R. Glasberg and B. C. J. Moore, "Derivation of auditory filter shapes from
  notched-noise data," *Hearing Research*, 47(1–2), 1990.
