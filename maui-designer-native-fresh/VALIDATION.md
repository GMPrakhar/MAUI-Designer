# Validation log

## Core mutation and geometry instrument check

Predictions written before measurement:

1. If same-parent reordering ignores the requested insertion index and appends instead, `Same_parent_reorder_uses_post_removal_index` will fail with `a,b,c` instead of `c,a,b`.
2. If Grid hit testing divides the surface into equal cells instead of using measured track extents, `Grid_hit_testing_uses_measured_track_extents` will report cell `(0,0)` instead of `(1,1)` for the deliberately unequal tracks.
3. After restoring the implementations, all core tests must pass with warnings treated as errors.

The temporary mutations used for these checks are never committed.

Observed failures matched both predictions: the equal-cell mutation returned `(0,0)`, and the append-only mutation retained `a,b,c`. After restoration, the complete seven-test core suite passed.

## XAML instrument check

Predictions written before the recorded measurement:

1. If wrapper fragments are discarded while writing, `Page_resources_bindings_attached_properties_and_custom_controls_survive` will fail because `ContentPage.Resources` is absent.
2. If malformed XML escapes the reader instead of becoming a diagnostic, `Invalid_xml_returns_diagnostic_without_a_document` will fail with `XmlException`.
3. If unresolved-control diagnostics omit the element name, `Unknown_control_is_rejected_with_its_source_location` will fail because the message does not contain `Mystery`.
4. After restoring each temporary mutation, the complete ten-test suite must pass.

The temporary mutations used for these checks are never committed.

Observed failures matched all three predictions: resource removal lost `ContentPage.Resources`, disabled XML handling surfaced the expected `XmlException`, and the generic unresolved-control message lost `Mystery`. The implementations were then restored.

## Atomic placement instrument check

Prediction written before measurement:

1. If `PlaceElementCommand` reparents a node but skips its layout property updates, `Placement_updates_parent_bounds_and_attached_properties_atomically` will fail because `Grid.Row` remains `0` instead of `2`.
2. After restoring the temporary mutation, all eleven core tests must pass.

The temporary mutation used for this check is never committed.

Observed failure matched the prediction: the moved node retained `Grid.Row="0"` instead of receiving row `2`. The implementation was then restored.

## Visual-content catalog instrument check

Prediction written before measurement:

1. If `Label` is deliberately classified as a child-bearing control, `Only_visual_content_properties_accept_designer_children` will fail at `Assert.False(label.AcceptsChildren)`.
2. After restoring the temporary mutation, the Windows catalog test and all core tests must pass.

The temporary mutation used for this check is never committed.

Observed failure matched the prediction: the seeded `Label` classification reached `Assert.False(label.AcceptsChildren)` with an actual value of `true`. The implementation was then restored.

## Native manipulation validation

Native DevFlow validation exposed that WinUI reports zero deltas on a pan recognizer's terminal event. Before the fix, resizing from `160 x 48` by `(60, 30)` returned to `160 x 48` after the successful gesture. The move and resize handlers now retain the latest running delta.

After the fix:

1. Resize changed the Label bounds from `(24, 24, 160, 48)` to `(24, 24, 220, 78)`.
2. Move changed the bounds to `(64, 44, 220, 78)`.
3. Two undo operations restored first the position and then the size.
4. Two redo operations restored the final `(64, 44, 220, 78)` bounds.

## Single-content ancestor instrument check

Prediction written before measurement:

1. With a Label selected inside a full `ContentView`, `Insertion_skips_full_single_content_ancestors` will initially fail because insertion stops at the immediate parent and throws instead of continuing to the root `AbsoluteLayout`.
2. After correcting ancestor resolution, the new control must be inserted at the root while an explicitly requested second `ContentView` child remains rejected.

The initial run failed at the predicted third insertion with `'ContentView' cannot accept another child control.` Ancestor traversal was then corrected to continue until it finds a container with capacity.
