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
- Can keep Humanoid feet grounded after leg-length edits by vertically compensating the Hips transform.
- Supports Unity Undo and prefab-instance overrides.
- Does not patch or modify Modular Avatar.

## Installation

### Recommended: VCC / VPM

Repository URL:

```text
https://raw.githubusercontent.com/sakusdev/MA-Scale-Adjuster-Metric-System/vpm/index.json
```

1. Open **VRChat Creator Companion**.
2. Open **Settings > Packages**.
3. Click **Add Repository**.
4. Paste the repository URL above and add it.
5. Open **Manage Project** for your avatar project.
6. Add **MA Scale Adjuster Metric System** with the `+` button.

One-click VCC link:

```text
vcc://vpm/addRepo?url=https%3A%2F%2Fraw.githubusercontent.com%2Fsakusdev%2FMA-Scale-Adjuster-Metric-System%2Fvpm%2Findex.json
```

Modular Avatar is declared as a VPM dependency. If VCC cannot resolve it, add the Modular Avatar VPM repository first.

### GitHub Release ZIP

Each released version also provides a VPM-compatible ZIP on the GitHub Releases page. The ZIP has `package.json` at its root and can be used as a local package if needed.

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

## Keep feet grounded

Enable **Keep feet grounded (world Y)** when changing leg length.

For a Humanoid avatar, the tool automatically uses **LeftFoot** and **RightFoot** as ground references and **Hips** as the compensation transform. After the Scale Adjuster edit, it compares the lowest foot height before and after the change, then moves the compensation transform only on world Y so the lowest foot stays at the same height.

Using the lowest of both feet is intentional: it avoids repeatedly lifting the avatar when the left and right legs are edited one after another.

For non-Humanoid rigs, you can manually assign one or two ground-reference transforms and a compensation transform. The compensation transform must be the adjusted bone itself or an ancestor of it, and every ground reference must be below that transform.

This feature requires **Adjust child positions like Modular Avatar** because Scale Adjuster values alone do not move the real child-bone transforms.

## Measurement modes

**Full Length** measures the complete 3D endpoint distance.

**Axis Projected** measures the absolute component of the endpoint vector along the selected bone-local axis.

Only the selected MA Scale component is solved; the other two components are preserved.

## Safety and edge cases

The child-adjustment path follows Modular Avatar's small positive effective-scale clamp to avoid losing child-position information at exactly zero scale. If the requested metric length cannot be reached by changing only the selected axis, the tool reports the target as unreachable instead of writing an invalid scale.

## License

MIT. See [Third Party Notices](Third%20Party%20Notices.md) for attribution related to Modular Avatar-compatible editor behavior.
