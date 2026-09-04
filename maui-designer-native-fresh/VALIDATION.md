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

## Reparent target instrument check

Prediction written before measurement:

1. If moving-subtree filtering is disabled, `Drop_target_rejects_the_moving_subtree` will fail because the moving Grid and its nested Grid are exposed as valid drop targets.
2. After restoration, both targets must be rejected before any preview or document command runs.

The seeded run failed at the first `Assert.False` with an actual value of `true`. The moving-subtree guard was restored before final validation.

## Native drag and reparent validation

WinUI pointer input was injected through the actual application window at 125% display scaling:

1. A toolbox Label was dragged with native mouse movement and committed near the pointer at window position `(628, 402)`, rather than the default click insertion position.
2. A Grid was added at the root and the existing Label was panned into it.
3. The Label's native parent changed from `designer-root` to `designer-grid-2`.
4. Undo restored the Label to `designer-root` with its original `(24, 24, 160, 48)` bounds.
5. While a Button was held over the Grid, the target rendered a cyan cell preview and status identified `grid-2`; release parented `button-4` to `designer-grid-2`.
6. A Label and Button were added to a `VerticalStackLayout`, then an Entry was dragged between them. Their native Y positions confirmed the resulting Label, Entry, Button order at `0`, `24`, and `68` respectively.

## Dependency-injected control factory instrument check

Prediction written before measurement:

1. A control registered through `RegisterFactory` will initially receive the materializer's empty service provider rather than the catalog's application service provider, so `Materializer_uses_the_catalog_service_provider_for_registered_factories` will fail while resolving `RequiredService`.
2. After routing construction through the catalog, the same dependency instance registered in DI must reach the rendered custom control.

The seeded run failed at the predicted service resolution boundary. Construction
now flows through `IControlCatalog.Create`, and the factory observes the
application service provider.

## Complex Toolkit XAML instrument check

Prediction written before measurement:

1. A Toolkit `Expander.Header` visual property element will survive serialization but initially remain absent from the native projection because the document model only tracks the default content slot.
2. `Toolkit_visual_property_elements_are_rendered_and_round_trip` must therefore fail on the null native `Header` before visual-slot support is implemented.
3. After the fix, the native projection must contain both `Header` and `Content`, while resources, styles, Toolkit behaviors, and the official Toolkit namespace continue to round-trip.
4. Interactive reparenting must clear a named visual slot such as `Header`; otherwise a node dropped into a Grid would still be assigned as a nonexistent `Grid.Header` property. Undo must restore the original slot.

The initial native-projection test process encountered the expected WinUI
thread initialization boundary before it could inspect the `Header`, so the
regression was moved to the platform-neutral document projection boundary.
The named `Header` node now parses explicitly and serializes back through an
`Expander.Header` property element. A separate seeded placement run retained
`Header` after a Grid drop and failed at `Assert.Null`; clearing the slot on
reparent fixed the failure, while undo restores it from the immutable snapshot.

## Runtime extension assembly compatibility

The Windows integration suite loads its own compiled assembly through the same
collectible dependency-resolving context used by the UI. The fixture publishes
an official `XmlnsDefinition`, exposes a bindable numeric property, accepts
visual content, and is then parsed from XAML through the refreshed catalog.
This covers the same runtime boundary used by third-party NuGet control packs
without adding a product-specific control registration.

## Modern MAUI control identity instrument check

Prediction written before measurement:

1. Scanning the MAUI assembly without filtering compatibility shims exposes both
   modern `Grid` and `Microsoft.Maui.Controls.Compatibility.Grid` under the same
   MAUI XAML name.
2. Catalog resolution can consequently select the legacy shim, whose constructor
   throws unless `UseMauiCompatibility` is enabled.
3. A catalog test must fail while duplicate `Grid` identities remain, then pass
   only when the standard MAUI namespace deterministically resolves to the modern
   control and compatibility-only controls are absent.

The seeded run found two MAUI `Grid` matches. Native application telemetry then
confirmed that the selected type was
`Microsoft.Maui.Controls.Compatibility.Grid`, whose constructor rejected the
app because compatibility mode was intentionally not enabled. Compatibility
shims are now excluded from discovery, leaving the modern `Grid` as the sole
standard-namespace match.

## Binding preview compatibility

Prediction written before measurement:

1. A binding-backed text property is blank in a design process without the
   application's binding context unless the designer uses its declared
   `FallbackValue`.
2. The preview parser must handle both ordinary fallback text and quoted values
   containing commas without changing the original markup extension stored in
   the document.

Changing the recognized argument name caused both fallback cases to fail at the
predicted `Assert.True`. Restoring it rendered `Professional` from the native
binding sample while the generated XAML retained the original `{Binding ...}`
expression.

## Native viewport validation

Predictions written before measurement:

1. Zooming around a pointer must keep the same design-space point under that
   pointer; otherwise controls visibly jump while using Ctrl+wheel.
2. Fit must select the limiting axis, respect the 64-DIP viewport margin, and
   center the scaled device surface.
3. Drop hit-testing, move deltas, and resize deltas must be converted back to
   design coordinates at non-100% zoom, while transformed target bounds must
   include the native scale.
4. Native validation must demonstrate device switching, zoom in/out, fit,
   reset, middle-button panning, grid and ruler toggles, dark preview, and
   selection/manipulation on a zoomed canvas.

Observed results:

1. The initial desktop surface fitted at 48%; Zoom In changed it to 58%, and a
   real Ctrl+wheel event zoomed around the pointer to 64%.
2. A real middle-button drag shifted the surface and ruler origins while the
   viewport retained pointer capture.
3. Switching to the 390 x 844 phone preset fitted and centered it at 45%.
4. Grid, ruler, snap, and dark-preview controls remained visible in the
   two-row toolbar; native screenshots confirmed both light and dark grid
   rendering.
5. At 48% zoom, moving a Label by `(80,40)` viewport DIPs produced snapped
   design bounds `(192,104,160,48)`. Resizing by `(48,24)` produced
   `(192,104,264,96)`, confirming inverse-scale manipulation math.
6. Viewport WinUI handlers listen to already-handled routed pointer events so
   panning and Ctrl+wheel remain available over child controls. One-finger
   touch remains available to select/manipulate controls rather than being
   unconditionally captured for canvas panning.
7. Canvas content padding was removed so device dimensions, ruler origin, grid
   lines, snapping, and document coordinates share the same `(0,0)`.
8. Invalid visual trees are rejected transactionally: non-container controls
   cannot receive visual children, and named visual slots cannot receive more
   than one child.
9. Absolute-layout bounds survive write/reparse, stale attached bounds are
   removed when reparenting, descendant namespace declarations are promoted
   safely for export, and runtime control construction/content-setter failures
   render a visible unavailable-control placeholder.
10. Final validation passed 15 core tests and 12 Windows app tests; the signed
    Release host opened a responsive native `MAUI Designer` window.
