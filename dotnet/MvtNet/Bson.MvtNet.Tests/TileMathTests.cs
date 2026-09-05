namespace Bson.MvtNet.Tests;

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
    public void TryProjectWithinBuffer_StockholmInCorrectTile()
    {
        var ctx = TileMath.CreateProjectionContext(10, 563, 301);

        bool inside = TileMath.TryProjectWithinBuffer(59.3281936, 18.0440866, ctx, 0, out var coord);

        Assert.That(inside, Is.True);
        Assert.That(coord.X, Is.InRange(0, 4096));
        Assert.That(coord.Y, Is.InRange(0, 4096));
    }

    [Test]
    public void TryProjectWithinBuffer_OutsideTile_ReturnsFalse()
    {
        var ctx = TileMath.CreateProjectionContext(10, 0, 0);

        Assert.That(TileMath.TryProjectWithinBuffer(59.3281936, 18.0440866, ctx, 0, out _), Is.False);
    }

    [Test]
    public void ProjectWithContext_OutsideTile_ReturnsOutOfRangeCoords()
    {
        // Stockholm projected onto a far-away tile should give coords outside 0..4096
        var ctx = TileMath.CreateProjectionContext(10, 0, 0);

        var coord = TileMath.ProjectWithContext(59.3281936, 18.0440866, ctx);

        Assert.That(coord.X > 4096 || coord.X < 0 || coord.Y > 4096 || coord.Y < 0, Is.True);
    }

    [Test]
    public void TryProjectWithinBuffer_AtHighZoom_StillWorks()
    {
        // z18 — very zoomed in
        double lat = 59.3281936;
        double lng = 18.0440866;

        // Find the correct tile at z18
        double n = Math.Pow(2, 18);
        int x = (int)((lng + 180.0) / 360.0 * n);
        int y = (int)(
            (
                1.0
                - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 1.0 / Math.Cos(lat * Math.PI / 180.0))
                    / Math.PI
            )
            / 2.0
            * n
        );

        var ctx = TileMath.CreateProjectionContext(18, x, y);

        Assert.That(TileMath.TryProjectWithinBuffer(lat, lng, ctx, 0, out var coord), Is.True);
        Assert.That(coord.X, Is.InRange(0, 4096));
        Assert.That(coord.Y, Is.InRange(0, 4096));
    }
}
