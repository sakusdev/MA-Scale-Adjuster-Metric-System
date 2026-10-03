# MA Scale Adjuster Metric System

A Unity Editor extension for editing **Modular Avatar / MA Scale Adjuster** from real metric bone lengths instead of manually calculating scale ratios.

## Features

- Treats **1 Unity unit as 1 metre**.
- Measures from the Scale Adjuster bone to a selected direct child.
- Supports **Full Length** and **Axis Projected** measurement.
- Auto-detects the bone's dominant local X / Y / Z axis.
- Solves the required MA Scale value from a target length in metres.
- Correctly handles rotated bones and non-uniform parent scale.
- Can reposition direct child bones using the same coordinate conversion used by Modular Avatar's Scale Adjuster tool.
- Supports Unity Undo and prefab-instance overrides.
- Does not patch or modify Modular Avatar.

## Requirements

- Unity 2022.3
- Modular Avatar 1.18.0 or newer

## Usage

1. Add **MA Scale Adjuster** to the bone normally.
2. Select that bone.
3. Open **Tools > MA Scale Adjuster Metric System**.
4. Choose the child used as the bone endpoint.
5. Select or auto-detect the primary axis.
6. Choose **Full Length** or **Axis Projected**.
7. Enter **Target length (m)**.
8. Click **Apply to MA Scale Adjuster**.

You can also open the tool from the MA Scale Adjuster component context menu with **Edit in Metric System**.

## Child position adjustment

**Adjust child positions like Modular Avatar** is enabled by default.

When enabled, the target length is the actual parent-to-child transform distance after applying the change. All direct children are repositioned using the same scale-coordinate conversion strategy as Modular Avatar's editor tool.

When disabled, child transforms are left untouched. The metric value then describes the virtual Scale Adjuster deformation vector rather than the actual transform distance.

## Measurement modes

**Full Length** measures the complete 3D endpoint distance.

**Axis Projected** measures the absolute component of the endpoint vector along the selected bone-local axis.

Only the selected MA Scale component is solved; the other two components are preserved.

## Safety and edge cases

The child-adjustment path follows Modular Avatar's small positive effective-scale clamp to avoid losing child-position information at exactly zero scale. If the requested metric length cannot be reached by changing only the selected axis, the tool reports the target as unreachable instead of writing an invalid scale.

## License

MIT. See [Third Party Notices](Third%20Party%20Notices.md) for attribution related to Modular Avatar-compatible editor behavior.
