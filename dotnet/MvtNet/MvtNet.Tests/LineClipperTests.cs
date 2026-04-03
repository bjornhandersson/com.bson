namespace MvtNet.Tests;

public class LineClipperTests
{
    private const uint Extent = 4096;

    [Test]
    public void Clip_LineFullyInside_ReturnsSingleSegment()
    {
        TileCoord[] coords = [new(100, 100), new(200, 200), new(300, 300)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0], Has.Length.EqualTo(3));
    }

    [Test]
    public void Clip_LineFullyOutside_ReturnsEmpty()
    {
        // Line far to the right of tile
        TileCoord[] coords = [new(5000, 100), new(6000, 200)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(0));
    }

    [Test]
    public void Clip_LineCrossingOnce_ReturnsSingleClippedSegment()
    {
        // Line starts inside, exits right
        TileCoord[] coords = [new(2000, 2000), new(5000, 2000)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0], Has.Length.EqualTo(2));

        // End point should be clipped to near extent + buffer
        Assert.That(segments[0][1].X, Is.LessThanOrEqualTo((int)(Extent * 1.05) + 1));
    }

    [Test]
    public void Clip_LineEnteringTile_ReturnsSingleClippedSegment()
    {
        // Line starts outside left, enters tile
        TileCoord[] coords = [new(-1000, 2000), new(2000, 2000)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0], Has.Length.EqualTo(2));
    }

    [Test]
    public void Clip_ZigzagCrossingMultipleTimes_ReturnsMultipleSegments()
    {
        // Line enters tile, exits, enters again
        TileCoord[] coords =
        [
            new(-500, 1000), // outside left
            new(2000, 1000), // inside
            new(5000, 1000), // outside right
            new(5000, 3000), // outside right
            new(2000, 3000), // inside
            new(-500, 3000), // outside left
        ];

        var segments = LineClipper.Clip(coords, Extent);

        // Should produce at least 2 separate segments
        Assert.That(segments, Has.Count.GreaterThanOrEqualTo(2));

        foreach (var segment in segments)
        {
            Assert.That(segment, Has.Length.GreaterThanOrEqualTo(2));
        }
    }

    [Test]
    public void Clip_LinePassingThroughTile_BothEndsClipped()
    {
        // Line crosses entire tile horizontally
        TileCoord[] coords = [new(-2000, 2000), new(6000, 2000)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(1));
        Assert.That(segments[0], Has.Length.EqualTo(2));

        // Both ends should be clipped
        double buffer = Extent * 0.05;
        Assert.That(segments[0][0].X, Is.GreaterThanOrEqualTo((int)(-buffer) - 1));
        Assert.That(segments[0][1].X, Is.LessThanOrEqualTo((int)(Extent + buffer) + 1));
    }

    [Test]
    public void Clip_LineParallelOutside_ReturnsEmpty()
    {
        // Line runs parallel above the tile
        TileCoord[] coords = [new(0, -1000), new(4096, -1000)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(0));
    }

    [Test]
    public void Clip_SinglePointLine_ReturnsEmpty()
    {
        TileCoord[] coords = [new(100, 100)];

        var segments = LineClipper.Clip(coords, Extent);

        Assert.That(segments, Has.Count.EqualTo(0));
    }

    [Test]
    public void Clip_LongRouteZoomedIn_FarFewerPoints()
    {
        // Simulate the 3000-point 200km route scenario
        var coords = new TileCoord[3000];
        for (int i = 0; i < 3000; i++)
        {
            // Line spanning far beyond the tile in X
            coords[i] = new TileCoord(-50000 + i * 40, 2000 + (i % 100));
        }

        var segments = LineClipper.Clip(coords, Extent);

        // Total clipped points should be far fewer than 3000
        int totalPoints = 0;
        foreach (var segment in segments)
        {
            totalPoints += segment.Length;
        }

        Assert.That(totalPoints, Is.LessThan(500));
    }
}
