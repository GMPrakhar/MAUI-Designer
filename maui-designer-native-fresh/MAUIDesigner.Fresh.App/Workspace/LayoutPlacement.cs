using System.Collections.Immutable;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed record LayoutPlacement(
    int DestinationIndex = -1,
    RectD? Bounds = null,
    ImmutableDictionary<string, DesignerValue?>? PropertyUpdates = null)
{
    public static LayoutPlacement Default { get; } = new();
}
