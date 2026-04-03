namespace MvtNet.Tests;

public class TileMathTests
{
    [Test]
    public void GetTileBounds_Zoom0_CoversWorld()
    {
        var bounds = TileMath.GetTileBounds(0, 0, 0);

        Assert.That(bounds.West, Is.EqualTo(-180.0).Within(0.001));
        Assert.That(bounds.East, Is.EqualTo(180.0).Within(0.001));
        Assert.That(bounds.North, Is.EqualTo(85.051).Within(0.01));
        Assert.That(bounds.South, Is.EqualTo(-85.051).Within(0.01));
    }

    [Test]
    public void GetTileBounds_HigherZoom_SmallerBounds()
    {
        var z0 = TileMath.GetTileBounds(0, 0, 0);
        var z5 = TileMath.GetTileBounds(5, 16, 10);

        double z0Width = z0.East - z0.West;
        double z5Width = z5.East - z5.West;

        Assert.That(z5Width, Is.LessThan(z0Width));
    }

    [Test]
    public void GetTileBounds_AdjacentTiles_ShareEdges()
    {
        var left = TileMath.GetTileBounds(5, 16, 10);
        var right = TileMath.GetTileBounds(5, 17, 10);

        Assert.That(left.East, Is.EqualTo(right.West).Within(0.0001));
    }

    [Test]
    public void ProjectPoint_StockholmInCorrectTile()
    {
        double lat = 59.3281936;
        double lng = 18.0440866;
        int z = 10;

        var coord = TileMath.ProjectPoint(lat, lng, z, 563, 301);

        Assert.That(coord, Is.Not.Null);
        Assert.That(coord!.Value.X, Is.InRange(0, 4096));
        Assert.That(coord!.Value.Y, Is.InRange(0, 4096));
    }

    [Test]
    public void ProjectPoint_OutsideTile_ReturnsNull()
    {
        var coord = TileMath.ProjectPoint(59.3281936, 18.0440866, 10, 0, 0);

        Assert.That(coord, Is.Null);
    }

    [Test]
    public void Contains_StockholmInCorrectTile()
    {
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 563, 301), Is.True);
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 0, 0), Is.False);
    }

    [Test]
    public void ProjectPointUnclamped_OutsideTile_ReturnsOutOfRangeCoords()
    {
        // Stockholm projected onto a far-away tile should give coords outside 0..4096
        var coord = TileMath.ProjectPointUnclamped(59.3281936, 18.0440866, 10, 0, 0);

        Assert.That(coord.X > 4096 || coord.X < 0 || coord.Y > 4096 || coord.Y < 0, Is.True);
    }

    [Test]
    public void ProjectPoint_AtHighZoom_StillWorks()
    {
        // z18 — very zoomed in
        double lat = 59.3281936;
        double lng = 18.0440866;

        // Find the correct tile at z18
        double n = Math.Pow(2, 18);
        int x = (int)((lng + 180.0) / 360.0 * n);
        int y = (int)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);

        var coord = TileMath.ProjectPoint(lat, lng, 18, x, y);

        Assert.That(coord, Is.Not.Null);
        Assert.That(coord!.Value.X, Is.InRange(0, 4096));
        Assert.That(coord!.Value.Y, Is.InRange(0, 4096));
    }
}
