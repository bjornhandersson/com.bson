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
    public void ProjectPoint_StockholmInCorrectTile()
    {
        double lat = 59.3281936;
        double lng = 18.0440866;
        int z = 10;

        // Stockholm at z10 should be in tile 563/301 (standard slippy map)
        var coord = TileMath.ProjectPoint(lat, lng, z, 563, 301);

        Assert.That(coord, Is.Not.Null);
        Assert.That(coord!.Value.X, Is.InRange(0, 4096));
        Assert.That(coord!.Value.Y, Is.InRange(0, 4096));
    }

    [Test]
    public void ProjectPoint_OutsideTile_ReturnsNull()
    {
        // Stockholm should not be in tile 0/0 at z10
        var coord = TileMath.ProjectPoint(59.3281936, 18.0440866, 10, 0, 0);

        Assert.That(coord, Is.Null);
    }

    [Test]
    public void Contains_StockholmInCorrectTile()
    {
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 563, 301), Is.True);
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 0, 0), Is.False);
    }
}
