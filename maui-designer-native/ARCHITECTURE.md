# MAUI Designer Native Fresh

## Design rules

- The immutable `DesignerDocument` is the source of truth. Native MAUI views are projections and never own designer state.
- Every edit goes through `DocumentSession.Execute`. Undo and redo restore validated document snapshots.
- Controls use an assembly-qualified `ControlTypeId`; simple CLR names are display labels, never identities.
- Layout policy, control construction, property metadata/editors, XAML, and native rendering are separate registries.
- Layout hit testing and document mutation live in the platform-neutral core and are covered without UI automation.
- Selection, pointer capture, and drag previews are instance-owned. There are no global mutable events or drag state.
- The UI thread owns native views. Reflection metadata and XAML syntax analysis may run in the background and publish immutable snapshots.

## Project boundaries

| Project | Responsibility |
|---|---|
| `MAUIDesigner.Fresh.Core` | Document tree, commands, history, geometry, drop-target resolution, and extension contracts that do not depend on MAUI |
| `MAUIDesigner.Fresh.App` | MAUI catalog, real-control materialization, shell, canvas/adorners, property editors, layout adapters, and XAML workspace |
| `MAUIDesigner.Fresh.Core.Tests` | Deterministic mutation, parenting, geometry, and history tests |

The app targets .NET 10 and enables the experimental `Microsoft.Maui.DevFlow.Agent` only in Debug builds. DevFlow supplies visual-tree inspection, screenshots, interactions, layout diagnostics, and profiling for repeatable native validation; it is not part of the designer runtime architecture or release build.

## Interaction budget

A 60 Hz display provides 16.67 ms per frame. The designer reserves 8 ms for MAUI layout/rendering and 3 ms for input/platform overhead, leaving **5 ms** for designer work during pointer movement. Drop-target geometry must therefore use a cached layout snapshot and complete within 2 ms, document preview mutation within 1 ms, and adorner updates within 2 ms. Reflection scanning, property discovery, assembly loading, and XAML parsing never run in the pointer-move path.

For documents up to 2,000 elements, a committed mutation should finish within 50 ms. This is an interaction budget, not a fitted benchmark: 50 ms is the upper bound before direct manipulation begins to feel disconnected. Immutable snapshots are retained in a bounded 100-entry history by default.

## Delivery slices

1. Immutable document model, mutation pipeline, history, and pure target geometry.
2. Reflection catalog and searchable toolbox backed by qualified descriptors.
3. Real-control canvas projection, selection adorner, add/delete, and generic properties.
4. Absolute, Grid, stack, generic layout, and single-content placement adapters.
5. Pointer-captured move/resize/reparent with target and cell/insertion highlights.
6. Transactional XAML import/export with namespace and opaque-syntax retention.
7. External assembly loading, explicit factories, and CommunityToolkit registration.
8. Accessibility, keyboard/clipboard workflows, visual polish, and complete validation.
