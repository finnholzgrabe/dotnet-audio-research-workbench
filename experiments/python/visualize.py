#!/usr/bin/env python3
"""Visualize the workbench: signal transformation pipeline + ML results.

This is an *experiment helper only* — not part of the .NET build or tests. It
renders PNGs from the artifacts the .NET CLI produces (and from WAV files
directly), so the figures reflect the project's actual pipeline.

It can draw, in a single figure:
  * the transformation pipeline for one audio file
      waveform  ->  STFT spectrogram  ->  cochlear band-energy heatmap
  * the ML results
      confusion-matrix heatmap  +  accuracy comparison across runs

Usage
-----
    python visualize.py --wav samples/generated/chirp.wav \\
        --bands artifacts/chirp-bands.csv \\
        --reports artifacts/easy.json artifacts/noisy.json artifacts/fsdd-speaker.json \\
        --out artifacts/overview.png

Any of --wav / --bands / --reports may be omitted; panels adapt to what is given.

Requires: numpy, matplotlib (not a project dependency; install in a venv).
"""
from __future__ import annotations

import argparse
import csv
import json
import wave
from pathlib import Path

import matplotlib

matplotlib.use("Agg")  # headless / deterministic
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402

plt.rcParams.update(
    {
        "figure.dpi": 120,
        "axes.titlesize": 11,
        "axes.titleweight": "bold",
        "axes.labelsize": 9,
        "xtick.labelsize": 8,
        "ytick.labelsize": 8,
        "font.family": "sans-serif",
    }
)


# --------------------------------------------------------------------------- IO
def read_wav(path: str):
    """Read a 16-bit PCM WAV into mono float32 samples in [-1, 1]."""
    with wave.open(path, "rb") as w:
        n, ch, sr, sw = (
            w.getnframes(),
            w.getnchannels(),
            w.getframerate(),
            w.getsampwidth(),
        )
        raw = w.readframes(n)
    if sw != 2:
        raise ValueError(f"only 16-bit PCM supported, got {sw * 8}-bit")
    data = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0
    if ch > 1:
        data = data.reshape(-1, ch).mean(axis=1)
    return data, sr


def read_bands_csv(path: str):
    """Read the CLI `features bands` CSV -> (times, centers_hz, energies[time, band])."""
    with open(path, newline="") as f:
        rows = list(csv.reader(f))
    header = rows[0]
    centers = [float("".join(c for c in h if (c.isdigit() or c == "."))) for h in header[2:]]
    data = np.array([[float(x) for x in r] for r in rows[1:]])
    times = data[:, 1]
    energies = data[:, 2:]
    return times, np.array(centers), energies


# ---------------------------------------------------------------------- compute
def stft_mag(x: np.ndarray, sr: int, frame: int = 512, hop: int = 256):
    """Magnitude STFT mirroring the .NET pipeline (periodic Hann window)."""
    n = np.arange(frame)
    win = 0.5 * (1.0 - np.cos(2.0 * np.pi * n / frame))
    starts = range(0, len(x) - frame + 1, hop)
    cols = [np.abs(np.fft.rfft(x[s : s + frame] * win)) for s in starts]
    mag = np.array(cols).T if cols else np.zeros((frame // 2 + 1, 0))
    freqs = np.fft.rfftfreq(frame, 1.0 / sr)
    duration = len(x) / sr
    return mag, freqs, duration


def fallback_cochleagram(mag: np.ndarray, freqs: np.ndarray, n_bands: int = 24):
    """If no bands CSV is given, approximate band energies on an ERB-ish axis."""
    def hz_to_erb(f):
        return 21.4 * np.log10(1.0 + 0.00437 * f)

    def erb_to_hz(e):
        return (10 ** (e / 21.4) - 1.0) / 0.00437

    edges = erb_to_hz(np.linspace(hz_to_erb(50.0), hz_to_erb(freqs[-1]), n_bands + 1))
    power = mag**2
    out = np.zeros((n_bands, mag.shape[1]))
    for b in range(n_bands):
        sel = (freqs >= edges[b]) & (freqs < edges[b + 1])
        if sel.any():
            out[b] = power[sel].sum(axis=0)
    centers = 0.5 * (edges[:-1] + edges[1:])
    return np.log10(out + 1e-9), centers


# ------------------------------------------------------------------- ML helpers
def short_label(report: dict, path: str) -> str:
    src = report.get("datasetSource", Path(path).stem)
    if "fsdd" in src or "directory" in src:
        return "FSDD\n" + ("speaker" if "speaker" in src else "digit" if "digit" in src else "")
    if "easy" in src:
        return "easy"
    if "regime" in src:
        return "noisy\nregime"
    if "noisy" in src:
        return "noisy\nrandom"
    return Path(path).stem


def draw_confusion(ax, report: dict):
    classes = report["classes"]
    cm = np.array([[report["confusionMatrix"][a][b] for b in classes] for a in classes], dtype=float)
    norm = cm / np.clip(cm.sum(axis=1, keepdims=True), 1, None)
    im = ax.imshow(norm, cmap="Blues", vmin=0, vmax=1, aspect="auto")
    ax.set_xticks(range(len(classes)), classes, rotation=45, ha="right")
    ax.set_yticks(range(len(classes)), classes)
    ax.set_xlabel("predicted")
    ax.set_ylabel("actual")
    ax.set_title(f"Confusion matrix  (acc={report['accuracy']:.3f})")
    thresh = 0.5
    for i in range(len(classes)):
        for j in range(len(classes)):
            ax.text(
                j,
                i,
                int(cm[i, j]),
                ha="center",
                va="center",
                color="white" if norm[i, j] > thresh else "#222",
                fontsize=8,
            )
    return im


def draw_accuracy(ax, reports):
    labels = [short_label(r, p) for p, r in reports]
    accs = [r["accuracy"] for _, r in reports]
    colors = plt.cm.viridis(np.linspace(0.15, 0.85, len(accs)))
    bars = ax.bar(range(len(accs)), accs, color=colors)
    ax.set_xticks(range(len(accs)), labels, fontsize=8)
    ax.set_ylim(0, 1.05)
    ax.set_ylabel("accuracy")
    ax.set_title("Baseline accuracy by run")
    ax.axhline(0.0, color="#ccc", lw=0.5)
    for b, a in zip(bars, accs):
        ax.text(b.get_x() + b.get_width() / 2, a + 0.02, f"{a:.3f}", ha="center", fontsize=8)


# ------------------------------------------------------------------------- main
def build_figure(args) -> plt.Figure:
    has_pipeline = args.wav is not None
    reports = []
    for p in args.reports or []:
        with open(p) as f:
            reports.append((p, json.load(f)))
    has_results = len(reports) > 0

    n_pipeline = 3 if has_pipeline else 0
    n_results = 1 if has_results else 0
    total_rows = n_pipeline + n_results
    if total_rows == 0:
        raise SystemExit("nothing to draw: pass --wav and/or --reports")

    fig = plt.figure(figsize=(11, 2.6 * total_rows + 0.6), constrained_layout=True)
    fig.suptitle("Audio Research Workbench — pipeline & results", fontsize=13, fontweight="bold")
    gs = fig.add_gridspec(total_rows, 2)
    row = 0

    if has_pipeline:
        x, sr = read_wav(args.wav)
        mag, freqs, duration = stft_mag(x, sr)
        name = Path(args.wav).name

        # 1) waveform
        ax = fig.add_subplot(gs[row, :])
        t = np.linspace(0, len(x) / sr, len(x))
        ax.plot(t, x, lw=0.6, color="#1f77b4")
        ax.set_xlim(0, t[-1] if len(t) else 1)
        ax.set_title(f"1 · Waveform — {name}")
        ax.set_xlabel("time (s)")
        ax.set_ylabel("amplitude")
        row += 1

        # 2) spectrogram
        ax = fig.add_subplot(gs[row, :])
        spec_db = 20 * np.log10(mag + 1e-6)
        im = ax.imshow(
            spec_db,
            origin="lower",
            aspect="auto",
            cmap="magma",
            extent=[0, duration, 0, freqs[-1]],
        )
        ax.set_title("2 · STFT magnitude spectrogram")
        ax.set_xlabel("time (s)")
        ax.set_ylabel("frequency (Hz)")
        fig.colorbar(im, ax=ax, label="dB", pad=0.01)
        row += 1

        # 3) cochleagram
        ax = fig.add_subplot(gs[row, :])
        if args.bands:
            times, centers, energies = read_bands_csv(args.bands)
            coch = energies.T  # [band, time]
            extent = [times[0], times[-1], 0, len(centers)]
        else:
            coch, centers = fallback_cochleagram(mag, freqs)
            extent = [0, duration, 0, len(centers)]
        im = ax.imshow(coch, origin="lower", aspect="auto", cmap="viridis", extent=extent)
        ax.set_title("3 · Cochlear-inspired band energies (ERB bank, log)")
        ax.set_xlabel("time (s)")
        ax.set_ylabel("band (low → high)")
        ticks = np.linspace(0, len(centers) - 1, min(6, len(centers))).astype(int)
        ax.set_yticks(ticks + 0.5, [f"{int(centers[i])}" for i in ticks])
        fig.colorbar(im, ax=ax, label="log energy", pad=0.01)
        row += 1

    if has_results:
        ax_cm = fig.add_subplot(gs[row, 0])
        # Confusion panel: an explicit choice, else the most informative run
        # (lowest accuracy -> most visible off-diagonal structure).
        if args.confusion:
            with open(args.confusion) as f:
                chosen = json.load(f)
        else:
            chosen = min(reports, key=lambda pr: pr[1]["accuracy"])[1]
        im = draw_confusion(ax_cm, chosen)
        fig.colorbar(im, ax=ax_cm, label="row-normalized", pad=0.01)

        ax_acc = fig.add_subplot(gs[row, 1])
        draw_accuracy(ax_acc, reports)

    return fig


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--wav", help="WAV file for the pipeline panels")
    ap.add_argument("--bands", help="CLI `features bands` CSV for the cochleagram (optional)")
    ap.add_argument("--reports", nargs="*", help="one or more `ml baseline` JSON reports")
    ap.add_argument("--confusion", help="report JSON to drive the confusion panel (default: lowest-accuracy run)")
    ap.add_argument("--out", default="artifacts/overview.png", help="output PNG path")
    args = ap.parse_args()

    fig = build_figure(args)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(args.out, bbox_inches="tight")
    print(f"Wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
