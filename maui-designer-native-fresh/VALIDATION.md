# Validation log

## Core mutation and geometry instrument check

Predictions written before measurement:

1. If same-parent reordering ignores the requested insertion index and appends instead, `Same_parent_reorder_uses_post_removal_index` will fail with `a,b,c` instead of `c,a,b`.
2. If Grid hit testing divides the surface into equal cells instead of using measured track extents, `Grid_hit_testing_uses_measured_track_extents` will report cell `(0,0)` instead of `(1,1)` for the deliberately unequal tracks.
3. After restoring the implementations, all core tests must pass with warnings treated as errors.

The temporary mutations used for these checks are never committed.

Observed failures matched both predictions: the equal-cell mutation returned `(0,0)`, and the append-only mutation retained `a,b,c`. After restoration, the complete seven-test core suite passed.
