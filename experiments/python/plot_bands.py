#!/usr/bin/env python3
"""Optional plot of the cochlear band-energy CSV produced by the .NET CLI.

This is an *experiment helper only* -- it is not part of the build or tests and
has no influence on the shipped pipeline. It reads the CSV written by:

    audio-research features bands <wav> --out artifacts/tone-bands.csv

and renders a band-energy-over-time heatmap.

Usage:
    python experiments/python/plot_bands.py artifacts/tone-bands.csv [out.png]

Requires: numpy, matplotlib (install separately; not a project dependency).
"""
import csv
import sys


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    in_path = sys.argv[1]
    out_path = sys.argv[2] if len(sys.argv) > 2 else "artifacts/tone-bands.png"

    try:
        import numpy as np
        import matplotlib.pyplot as plt
    except ImportError:
        print("numpy and matplotlib are required: pip install numpy matplotlib")
        return 1

    with open(in_path, newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        rows = [[float(x) for x in row] for row in reader]

    data = np.array(rows)
    times = data[:, 1]
    energies = data[:, 2:]  # drop frame_index, time_s
    band_labels = header[2:]

    plt.figure(figsize=(9, 4))
    plt.imshow(
        energies.T,
        aspect="auto",
        origin="lower",
        extent=[times[0], times[-1], 0, energies.shape[1]],
    )
    plt.colorbar(label="log10 band energy")
    plt.xlabel("time (s)")
    plt.ylabel("cochlear band index")
    plt.title(f"Cochlear band energies: {in_path}")
    plt.yticks(range(0, len(band_labels), max(1, len(band_labels) // 8)))
    plt.tight_layout()
    plt.savefig(out_path, dpi=120)
    print(f"Wrote {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
