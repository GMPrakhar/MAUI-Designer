# Native designer performance

## Interaction budget

The desktop target is 60 Hz, leaving 16.67 ms per frame. The designer reserves
8 ms for MAUI layout/rendering and 3 ms for WinUI input dispatch, leaving a
5 ms budget for designer work during pointer and selection interactions.

Before measuring, the expected limits are:

| Operation | Budget | Reason |
| --- | ---: | --- |
| Selection outline update | 5 ms | Must complete inside one pointer frame. |
| Literal property preview | 16 ms | Should appear in the next rendered frame. |
| XAML debounce after typing stops | 300 ms | Avoids parsing incomplete markup per keystroke. |
| XAML parse and document swap | 100 ms for typical pages | Keeps feedback within the direct-manipulation threshold. |
| Structural rematerialization | 50 ms up to 2,000 nodes | Structural edits are less frequent but must remain interactive. |

## Audit findings

The original event path rematerialized every native control, rebuilt the
hierarchy and property inspector, and serialized all XAML for both selection
changes and individual property changes. Property materialization also repeated
reflection lookup for every node. This explains delayed focus and property
updates such as `RotationX`.

The optimized path:

- updates selection outlines and handles without replacing the canvas tree;
- applies convertible literal property values to the existing native control;
- caches writable reflection metadata per runtime control type;
- caches XAML type resolution by namespace and local name, invalidating the
  cache whenever extension controls change;
- creates selection handles, outlines, and native context menus only when they
  are actually needed instead of for every document node;
- coalesces document and selection notifications into one dispatcher turn;
- reserves full rematerialization for structural changes, undo/redo, invalidated
  property resets, and successful live-XAML document replacement;
- parses live XAML off the UI thread after a 300 ms debounce;
- displays explicit busy feedback while structural rendering is in progress;
- defers hierarchy work while the toolbox is visible and virtualizes hierarchy
  rows with `CollectionView` when the hierarchy is opened.

## Measurements

The fixed 2,000-node workload is one `VerticalStackLayout` containing 2,000
native `Label` controls. Budgets were not changed after measurement.

### Remaining misses

| Operation | Before optimization | Current | Budget | Result |
| --- | ---: | ---: | ---: | --- |
| Structural rematerialization, 2,000 nodes | 23,757.33 ms | 1,246.96 ms | 50 ms | MISS |
| Live-XAML parse, command, and synchronous projection | 51,966.71 ms | 1,269.75 ms | 100 ms | MISS |

The current implementation is approximately 19 times faster for structural
projection and 41 times faster end-to-end, but creating 2,001 real MAUI
controls plus selectable wrappers still exceeds the fixed large-document
budget. The miss is retained as an explicit scalability defect rather than
hidden by changing the threshold. The process remained responsive with the
full document rendered and used approximately 500 MB working set.

Opening the hierarchy no longer creates 2,001 hierarchy rows eagerly. Native
validation at the end of the same document found 34 realized rows near IDs
1962-1995; the remaining rows stayed virtualized.

### Representative interactive passes

| Operation | Measurement | Budget | Result |
| --- | ---: | ---: | --- |
| Selection outline update | 0.58-1.52 ms | 5 ms | PASS |
| Literal property preview | 4.94-6.45 ms | 16 ms | PASS |
| Typical live-XAML parse and command | 46.47-98.07 ms | 100 ms | PASS |
| Typical structural rematerialization | 27.50-48.87 ms | 50 ms | PASS |

## Profiling

Debug builds include the MAUI DevFlow profiler. For deeper Windows CPU and
allocation analysis, capture a trace from the running designer:

```powershell
dotnet-trace collect --process-id <PID> --providers Microsoft-DotNETCore-SampleProfiler
```

Record native interaction measurements and any budget misses in
`VALIDATION.md`; do not relax a budget to fit a measured result.
