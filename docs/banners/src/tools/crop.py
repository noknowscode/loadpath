#!/usr/bin/env python3
"""Cuts src/cap/ from the raw captures cap.js wrote (python3 crop.py <raw capture folder> <src/cap folder>).

Each entry: output name, raw capture, crop box in CSS px of that capture (None: the whole capture), the capture's device
pixel ratio, and the scale it is stored at. A box is cut at the capture's own pixels; a capture stored below its DPR is
downsampled (Lanczos). Nothing is ever upsampled.

Raw capture names: <L|D>-<state>, L Light and D Dark. desk-*: 1440 x 900 at DPR 2 (session A, A2 for the palette);
n-*: the canvas of the Normal layout, 1080 x 720 at DPR 3, clipped to y 56..636 (session B); w-*: 1440 x 900 at DPR 3
(session B); ph-*: 390 x 844 at DPR 2 (session C).
"""
import os
import sys
from PIL import Image

CAPS = [
    # whole screens for the device frames (stored at their DPR)
    ("desk-m12-light@2x", "L-desk-m12", None, 2, 2),
    ("desk-warren-light@2x", "L-desk-warren", None, 2, 2),
    ("desk-warren-dark@2x", "D-desk-warren", None, 2, 2),
    ("desk-util-dark@2x", "D-desk-util", None, 2, 2),
    ("phone-pratt-sheet-dark@2x", "D-ph-pratt-sheet", None, 2, 2),
    ("phone-roof-util-light@2x", "L-ph-roof-util", None, 2, 2),
    ("phone-pratt-dark@2x", "D-ph-pratt", None, 2, 2),
    ("phone-warren-sheet-dark@2x", "D-ph-warren-sheet", None, 2, 2),
    ("phone-roof-light@2x", "L-ph-roof", None, 2, 2),
    # canvas crops at DPR 3 (the Normal layout's canvas, tool rail left out)
    ("truss-forces-dark@3x", "D-n-warren", (96, 140, 928, 380), 3, 3),
    ("pratt-fail-light@3x", "L-n-pratt-forces", (104, 176, 556, 196), 3, 3),
    ("util-macro-dark@3x", "D-n-warren-util", (96, 118, 600, 300), 3, 3),
    # the inspector panel at DPR 3 (Widest layout, 1440 x 900): member M12's result (Dark), and the overloaded Pratt's result and
    # failure list (Light)
    ("inspector-result-dark@3x", "D-w-m12", (1108, 606, 330, 202), 3, 3),
    ("inspector-failures-light@3x", "L-w-pratt", (1108, 402, 330, 448), 3, 3),
    # the command palette (query "toggle", a frame without the text caret) and the screen it opens over (session A4)
    ("palette-dark@2x", "D-desk-palette-toggle-b", (460, 96, 520, 272), 2, 2),
    ("desk-palette-dark@2x", "D-desk-palette-toggle-b", None, 2, 2),
    # the six presets at their defaults, Light and Dark, for the mosaic wall (canvas inside its frame, rail left out)
    ("tile-pratt-light@1.25x", "L-n-preset-pratt", (110, 60, 950, 480), 3, 1.25),
    ("tile-howe-light@1.25x", "L-n-preset-howe", (110, 60, 950, 480), 3, 1.25),
    ("tile-warren-light@1.25x", "L-n-preset-warren", (110, 60, 950, 480), 3, 1.25),
    ("tile-k-light@1.25x", "L-n-preset-k", (110, 60, 950, 480), 3, 1.25),
    ("tile-fink-light@1.25x", "L-n-preset-fink", (110, 60, 950, 480), 3, 1.25),
    ("tile-scissor-light@1.25x", "L-n-preset-scissor", (110, 60, 950, 480), 3, 1.25),
    ("tile-pratt-dark@1.25x", "D-n-preset-pratt", (110, 60, 950, 480), 3, 1.25),
    ("tile-howe-dark@1.25x", "D-n-preset-howe", (110, 60, 950, 480), 3, 1.25),
    ("tile-k-dark@1.25x", "D-n-preset-k", (110, 60, 950, 480), 3, 1.25),
    ("tile-fink-dark@1.25x", "D-n-preset-fink", (110, 60, 950, 480), 3, 1.25),
    ("tile-scissor-dark@1.25x", "D-n-preset-scissor", (110, 60, 950, 480), 3, 1.25),
    ("tile-warren-sample-dark@1.25x", "D-n-warren", (110, 60, 950, 480), 3, 1.25),
    # the Warren sample's canvas in its four renderings (forces and utilization, Light and Dark), same framing
    ("layer-forces-light@1.5x", "L-n-warren", (110, 135, 900, 355), 3, 1.5),
    ("layer-util-dark@1.5x", "D-n-warren-util", (110, 135, 900, 355), 3, 1.5),
    ("layer-util-light@1.5x", "L-n-warren-util", (110, 135, 900, 355), 3, 1.5),
    ("layer-forces-dark@1.5x", "D-n-warren", (110, 135, 900, 355), 3, 1.5),
]


def main(raw, out):
    os.makedirs(out, exist_ok=True)
    for name, src, box, dpr, scale in CAPS:
        if not os.path.exists(os.path.join(raw, src + ".png")):
            print("missing", src)
            continue
        im = Image.open(os.path.join(raw, src + ".png")).convert("RGB")
        if box:
            x, y, w, h = box
            im = im.crop((round(x * dpr), round(y * dpr), round((x + w) * dpr), round((y + h) * dpr)))
        if scale < dpr:
            im = im.resize((round(im.width * scale / dpr), round(im.height * scale / dpr)), Image.LANCZOS)
        assert scale <= dpr, "never upsample"
        im.save(os.path.join(out, name + ".png"), optimize=True)
        print(name, im.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
