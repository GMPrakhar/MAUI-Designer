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
