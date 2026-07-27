# Character art pipeline

`knight.py` is the source of truth for the player sprite. It describes the
knight as posed geometry and renders it through a shading pipeline, then exports
`assets/knight.png`.

```bash
cd design
python3 knight.py          # writes knight_sheet.png for review
```

To re-export the sheet the game uses, run the export snippet at the bottom of
this file's history, or call `render(build(pose))` for each pose and composite
them at a shared crop box.

## How the shading works

Typing pixels by hand caps out at a low quality ceiling. Instead each part is
rasterised into a mask, then:

1. a chamfer **distance transform** gives every pixel its depth inside the shape
2. the **gradient of that depth** approximates a surface normal
3. the normal is lit from the upper left, so rounded forms get a real falloff
4. the result is **quantised onto a per-material colour ramp** (8 shades each)
5. a dark **outline** is traced around the combined silhouette

That is what produces consistent lighting across every part and pose, which is
the thing that separates amateur from professional pixel art.

## Frames

4 idle, 8 walk, then crouch, jump, fall, dash. The walk and idle cycles are
parametric functions of phase, so the frame count can be raised by changing one
number.

`Character.cs` must agree with the exported layout: `FrameWidth`, `FrameHeight`,
`FootX`, `FootY` and the frame index constants.
