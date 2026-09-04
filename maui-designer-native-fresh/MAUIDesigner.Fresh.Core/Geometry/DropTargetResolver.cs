using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.Core.Geometry;

public sealed record DropCandidate(
    ElementId ElementId,
    RectD Bounds,
    int Depth,
    bool AcceptsChildren);

public static class DropTargetResolver
{
    public static DropCandidate? Resolve(
        PointD pointer,
        IEnumerable<DropCandidate> candidates,
        IReadOnlySet<ElementId>? excludedIds = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(candidate =>
                candidate.AcceptsChildren &&
                candidate.Bounds.Contains(pointer) &&
                (excludedIds is null || !excludedIds.Contains(candidate.ElementId)))
            .OrderByDescending(candidate => candidate.Depth)
            .ThenBy(candidate => candidate.Bounds.Area)
            .ThenBy(candidate => candidate.ElementId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
