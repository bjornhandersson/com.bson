using System;

namespace Bson.HilbertIndex
{
    /// <summary>
    /// Maps WGS84 positions onto the square integer grid the Hilbert curve is drawn over, and back.
    ///
    /// The grid has its origin (0, 0) in the lower left (south west) corner and its upper right (north east)
    /// corner at (N, N) where N = 2^CurveOrder - 1, meaning the grid has N + 1 cells per side.
    ///
    /// Implementations decide how the globe is stretched onto that square. See <see cref="LinearProjection"/>
    /// for the original equirectangular mapping and <see cref="WebMercatorProjection"/> for a conformal,
    /// tile aligned mapping.
    /// </summary>
    public interface IProjection
    {
        /// <summary>
        /// Convert position to point in grid where lower left corner is (0, 0) and upper right corner is (N, N)
        /// </summary>
        /// <param name="position">WGS84 position (X = longitude, Y = latitude)</param>
        /// <param name="x">Grid column</param>
        /// <param name="y">Grid row</param>
        /// <param name="N">Highest valid grid index (2^CurveOrder - 1)</param>
        void PositionToPoint(Coordinate position, out int x, out int y, int N);

        /// <summary>
        /// Convert point in grid where lower left corner is (0, 0) and upper right corner is (N, N) to WGS84 position
        /// </summary>
        /// <param name="position">WGS84 position (X = longitude, Y = latitude)</param>
        /// <param name="x">Grid column</param>
        /// <param name="y">Grid row</param>
        /// <param name="N">Highest valid grid index (2^CurveOrder - 1)</param>
        void PointToPosition(out Coordinate position, int x, int y, int N);
    }
}
