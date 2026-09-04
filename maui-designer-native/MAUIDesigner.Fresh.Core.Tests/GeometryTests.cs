using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.Core.Tests;

public sealed class GeometryTests
{
    [Fact]
    public void Grid_hit_testing_uses_measured_track_extents()
    {
        var bounds = new RectD(100, 200, 400, 300);
        double[] rows = [40, 160, 100];
        double[] columns = [60, 240, 100];

        Assert.Equal(new GridCell(0, 0), GridGeometry.LocateCell(new PointD(120, 220), bounds, rows, columns));
        Assert.Equal(new GridCell(1, 1), GridGeometry.LocateCell(new PointD(180, 270), bounds, rows, columns));
        Assert.Equal(new GridCell(2, 2), GridGeometry.LocateCell(new PointD(450, 470), bounds, rows, columns));
    }

    [Fact]
    public void Deepest_valid_drop_target_wins_and_dragged_subtree_is_excluded()
    {
        var root = new DropCandidate(new ElementId("root"), new RectD(0, 0, 500, 500), 0, true);
        var parent = new DropCandidate(new ElementId("parent"), new RectD(20, 20, 300, 300), 1, true);
        var child = new DropCandidate(new ElementId("child"), new RectD(40, 40, 100, 100), 2, true);
        var excluded = new HashSet<ElementId> { child.ElementId };

        DropCandidate? result = DropTargetResolver.Resolve(
            new PointD(60, 60),
            [root, parent, child],
            excluded);

        Assert.Equal(parent.ElementId, result!.ElementId);
    }

    [Fact]
    public void Non_container_and_outside_candidates_are_ignored()
    {
        DropCandidate? result = DropTargetResolver.Resolve(
            new PointD(10, 10),
            [
                new DropCandidate(new ElementId("outside"), new RectD(20, 20, 10, 10), 3, true),
                new DropCandidate(new ElementId("leaf"), new RectD(0, 0, 20, 20), 4, false)
            ]);

        Assert.Null(result);
    }
}
