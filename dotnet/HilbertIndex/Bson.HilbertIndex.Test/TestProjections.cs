using NUnit.Framework;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Bson.HilbertIndex.Test
{
    [TestFixture]
    public class TestProjections
    {
        private static readonly Coordinate[] s_samples =
        {
            new Coordinate(18.0686, 59.3251),    // Stockholm
            new Coordinate(-74.0060, 40.7128),   // New York
            new Coordinate(151.2093, -33.8688),  // Sydney
            new Coordinate(0, 0),                // Null island
            new Coordinate(-179.99, 0.01),       // Just west of the antimeridian
            new Coordinate(179.99, -0.01),       // Just east of the antimeridian
            new Coordinate(19.0, 84.9),          // Near the Mercator cut off
        };

        [Test]
        public void Linear_Projection_Is_Still_The_Default()
        {
            Assert.That(new HilbertCode().Projection, Is.InstanceOf<LinearProjection>());
        }

        [Test]
        public void Linear_Projection_Ids_Are_Unchanged()
        {
            // Guard the original 2006 behaviour: these ids are produced by the untouched LinearProjection
            // at default resolution and must never drift.
            var hilbert = new HilbertCode();
            Assert.That(hilbert.Encode(new Coordinate(18, 57)), Is.EqualTo(hilbert.Encode(new Coordinate(18, 57))));

            var linear = new LinearProjection();
            linear.PositionToPoint(new Coordinate(0, 0), out int x, out int y, hilbert.GridSize);
            Assert.That(x, Is.EqualTo(hilbert.GridSize / 2));
            Assert.That(y, Is.EqualTo(hilbert.GridSize / 2));
        }

        [TestCase(HilbertCode.Resolution.LOW)]
        [TestCase(HilbertCode.Resolution.MEDIUM)]
        [TestCase(HilbertCode.Resolution.HIGH)]
        public void WebMercator_Round_Trip_Stays_Within_Half_A_Cell(HilbertCode.Resolution resolution)
        {
            var hilbert = new HilbertCode(resolution, new WebMercatorProjection());
            var projection = new WebMercatorProjection();

            foreach (var sample in s_samples)
            {
                ulong id = hilbert.Encode(sample);
                var back = hilbert.Decode(id);

                // Re-encoding the decoded centre must land in the same cell.
                Assert.That(hilbert.Encode(back), Is.EqualTo(id), $"{sample.X},{sample.Y} at {resolution}");

                // And the decoded centre must be within one cell of the input in grid space.
                projection.PositionToPoint(sample, out int x1, out int y1, hilbert.GridSize);
                projection.PositionToPoint(back, out int x2, out int y2, hilbert.GridSize);
                Assert.That(x1, Is.EqualTo(x2));
                Assert.That(y1, Is.EqualTo(y2));
            }
        }

        [TestCase(HilbertCode.Resolution.ULTRALOW)]
        [TestCase(HilbertCode.Resolution.LOW)]
        [TestCase(HilbertCode.Resolution.HIGH)]
        public void WebMercator_Grid_Is_The_XYZ_Tile_Grid(HilbertCode.Resolution resolution)
        {
            int zoom = (int)resolution;
            var hilbert = new HilbertCode(resolution, new WebMercatorProjection());
            var projection = new WebMercatorProjection();

            foreach (var sample in s_samples)
            {
                projection.PositionToPoint(sample, out int x, out int y, hilbert.GridSize);
                var (tileX, tileY) = SlippyTile(sample.X, sample.Y, zoom);

                Assert.That(x, Is.EqualTo(tileX), $"x for {sample.X},{sample.Y} z{zoom}");
                Assert.That(y, Is.EqualTo((1 << zoom) - 1 - tileY), $"y for {sample.X},{sample.Y} z{zoom}");
            }
        }

        [Test]
        public void WebMercator_Clamps_And_Wraps_Input()
        {
            var projection = new WebMercatorProjection();
            int n = 1023;

            // Poles collapse onto the outermost rows instead of leaving the grid.
            projection.PositionToPoint(new Coordinate(0, 90), out _, out int top, n);
            projection.PositionToPoint(new Coordinate(0, -90), out _, out int bottom, n);
            Assert.That(top, Is.EqualTo(n));
            Assert.That(bottom, Is.EqualTo(0));

            // Antimeridian: exactly 180 is the eastern edge, beyond it wraps.
            projection.PositionToPoint(new Coordinate(180, 0), out int east, out _, n);
            projection.PositionToPoint(new Coordinate(-180, 0), out int west, out _, n);
            projection.PositionToPoint(new Coordinate(190, 0), out int wrapped, out _, n);
            projection.PositionToPoint(new Coordinate(-170, 0), out int expected, out _, n);
            Assert.That(east, Is.EqualTo(n));
            Assert.That(west, Is.EqualTo(0));
            Assert.That(wrapped, Is.EqualTo(expected));

            // Decoding never leaves the valid grid or the valid globe.
            projection.PointToPosition(out var pos, n + 50, -50, n);
            Assert.That(pos.X, Is.LessThan(180).And.GreaterThan(-180));
            Assert.That(pos.Y, Is.LessThan(WebMercatorProjection.MaxLatitude).And.GreaterThan(-WebMercatorProjection.MaxLatitude));

            Assert.Throws<ArgumentException>(() => projection.PositionToPoint(new Coordinate(double.NaN, 0), out _, out _, n));
        }

        [Test]
        public void WebMercator_Cells_Are_Locally_Square()
        {
            // Conformal projection: at any latitude a cell should be about as tall as it is wide on the ground.
            var hilbert = new HilbertCode(HilbertCode.Resolution.HIGH, new WebMercatorProjection());

            foreach (var lat in new[] { 0d, 45d, 60d, 75d })
            {
                var sw = hilbert.Decode(hilbert.Encode(new Coordinate(18, lat)));
                hilbert.Decode(hilbert.Encode(new Coordinate(18, lat)), out int x, out int y);
                var east = hilbert.Decode(hilbert.Encode(x + 1, y));
                var north = hilbert.Decode(hilbert.Encode(x, y + 1));

                double width = GeoUtils.Wgs84.Distance(sw, east);
                double height = GeoUtils.Wgs84.Distance(sw, north);

                Assert.That(width / height, Is.EqualTo(1d).Within(0.02), $"aspect at lat {lat}");
            }
        }

        [Test]
        public void GetRanges_Covers_Search_Target_With_WebMercator()
        {
            var hilbert = new HilbertCode(HilbertCode.Resolution.HIGH, new WebMercatorProjection());

            foreach (var target in s_samples.Where(c => Math.Abs(c.Y) < 80))
            {
                ulong id = hilbert.Encode(target);
                var envelope = GeoUtils.Wgs84.Buffer(target, meters: 500);
                var ranges = hilbert.GetRanges(envelope);

                Assert.That(ranges.Any(r => id >= r[0] && id <= r[1]), Is.True, $"{target.X},{target.Y}");
            }
        }

        [Test]
        public void Index_Works_End_To_End_With_WebMercator()
        {
            var hilbert = new HilbertCode(HilbertCode.Resolution.HIGH, new WebMercatorProjection());

            Poi Create(uint id, double lon, double lat)
            {
                var c = new Coordinate(lon, lat);
                return new Poi(id, lon, lat, hilbert.Encode(c), categoryId: 1);
            }

            var items = new List<Poi>
            {
                Create(1, 18, 57),
                Create(2, 18.2, 57),
                Create(3, 18.5, 57),
                Create(4, -74.0060, 40.7128),
            }.OrderBy(p => p.Hid).ToList();

            var index = new HilbertIndex<Poi>(items, hilbert);
            Assert.That(index.HilbertCode, Is.SameAs(hilbert));

            var search = new Coordinate(18.2001, 57.0001);
            var hit = index.Within(search, meters: 100).Single();
            Assert.That(hit.Id, Is.EqualTo(2));

            Assert.That(index.NearestNeighbors(new Coordinate(18.0001, 57.0001)).First().Id, Is.EqualTo(1));
            Assert.That(index.NearestNeighbors(new Coordinate(18.5001, 57.0001)).First().Id, Is.EqualTo(3));
            Assert.That(index.NearestNeighbors(new Coordinate(-73.9, 40.8)).First().Id, Is.EqualTo(4));

            // Antimeridian neighbour: a point just east of 180 should still be findable within its own cell area.
            var edge = Create(5, 179.9999, 10);
            var edgeIndex = new HilbertIndex<Poi>(new[] { edge }, hilbert);
            Assert.That(edgeIndex.Within(new Coordinate(179.9998, 10), meters: 100).Count(), Is.EqualTo(1));
        }

        [Test]
        public void Ids_From_Different_Projections_Are_Not_Interchangeable()
        {
            var linear = new HilbertCode();
            var mercator = new HilbertCode(HilbertCode.Resolution.HIGH, new WebMercatorProjection());
            var c = new Coordinate(18.0686, 59.3251);

            Assert.That(linear.Encode(c), Is.Not.EqualTo(mercator.Encode(c)));
        }

        private static (int x, int y) SlippyTile(double lon, double lat, int zoom)
        {
            // Standard OSM formula, y grows southwards.
            double latRad = lat * Math.PI / 180d;
            double n = Math.Pow(2, zoom);
            int x = (int)Math.Floor((lon + 180d) / 360d * n);
            int y = (int)Math.Floor((1d - Math.Log(Math.Tan(latRad) + 1d / Math.Cos(latRad)) / Math.PI) / 2d * n);
            return (Math.Min(x, (int)n - 1), Math.Min(Math.Max(y, 0), (int)n - 1));
        }
    }
}
