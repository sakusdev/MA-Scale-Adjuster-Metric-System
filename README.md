# MA Scale Adjuster Metric System

A Unity Editor extension for editing **Modular Avatar / MA Scale Adjuster** using metric bone lengths instead of manually calculating scale ratios.

## Features

- Treats **1 Unity unit as 1 metre**.
- Measures a bone toward a selected direct child.
- Supports local **X / Y / Z** axes.
- Accepts a target length in metres and calculates the required MA Scale value.
- Supports Unity Undo and prefab-instance overrides.
- Does not modify Modular Avatar itself.

## Requirements

- Unity 2022.3
- Modular Avatar 1.18.0 or newer

## Usage

1. Add **MA Scale Adjuster** to a bone normally.
2. Select the bone.
3. Open **Tools > MA Scale Adjuster Metric System**.
4. Choose the child and axis used for measurement.
5. Enter **Target length (m)**.
6. Click **Apply to MA Scale Adjuster**.

The window can also be opened from the MA Scale Adjuster component context menu with **Edit in Metric System**.

## Measurement

The tool measures the selected child's displacement projected onto the selected local bone axis, accounting for the parent transform's world scale.

```
MA scale = target length (m) / base projected length (m)
```

Only the selected X, Y, or Z component is changed. If the child is not aligned to that axis, the displayed value is the axis-projected length rather than the full 3D bone distance.

## License

MIT
