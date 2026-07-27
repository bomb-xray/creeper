# Character art workflow

The knight is authored as pixel data in `design/art.py`, which renders a preview
sheet so the art can be judged by eye. Running

```bash
cd design && python3 art.py
```

writes `design/preview.png` with every pose side by side.

The same script emits `PixelArt.cs`, so the shipped data and the preview can
never drift apart. The layout offsets in `Character.DrawKnight` mirror those in
`art.py`'s `compose()`; change one and change the other.

## Rules

- Whole-number scales only, whole-pixel positions, never rotation. Each of those
  destroys the pixel grid.
- Every part is authored facing **right** and mirrored when the knight turns.
- Palette keys are single characters; `.` is transparent. An unknown character
  renders as magenta so typos are obvious.
