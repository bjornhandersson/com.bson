using System;

namespace Bson.HilbertIndex
{
    /// <summary>
    /// Web Mercator (EPSG:3857) projection. With curve order z the grid equals the XYZ tile grid at zoom z, y flipped.
    /// Longitude wraps, latitude is clamped to +/- <see cref="MaxLatitude"/>. Decoding returns the cell centre.
    /// </summary>
    public class WebMercatorProjection : IProjection
    {
        public const double MaxLatitude = 85.05112877980659;

        private const double DegToRad = Math.PI / 180d;
        private const double RadToDeg = 180d / Math.PI;

        public void PositionToPoint(Coordinate position, out int x, out int y, int N)
        {
            if (position == null)
                throw new ArgumentNullException(nameof(position));
            if (double.IsNaN(position.X) || double.IsNaN(position.Y))
                throw new ArgumentException("Coordinate contains NaN", nameof(position));

            double cells = (double)N + 1d;

            double lon = WrapLongitude(position.X);
            double lat = Math.Max(-MaxLatitude, Math.Min(MaxLatitude, position.Y));

            double u = (lon + 180d) / 360d;
            double latRad = lat * DegToRad;
            double v = (1d - Math.Log(Math.Tan(latRad) + 1d / Math.Cos(latRad)) / Math.PI) / 2d;

            x = Clamp((int)Math.Floor(u * cells), N);
            y = N - Clamp((int)Math.Floor(v * cells), N);
        }

        public void PointToPosition(out Coordinate position, int x, int y, int N)
        {
            double cells = (double)N + 1d;

            double u = (Clamp(x, N) + 0.5d) / cells;
            double v = (Clamp(y, N) + 0.5d) / cells;

            double lon = u * 360d - 180d;
            double lat = Math.Atan(Math.Sinh(Math.PI * (2d * v - 1d))) * RadToDeg;

            position = new Coordinate(lon, lat);
        }

        private static double WrapLongitude(double lon)
        {
            if (lon >= -180d && lon <= 180d)
                return lon;
            return ((lon + 180d) % 360d + 360d) % 360d - 180d;
        }

        private static int Clamp(int value, int max)
            => value < 0 ? 0 : (value > max ? max : value);
    }
}
