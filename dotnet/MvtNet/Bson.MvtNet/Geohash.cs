namespace Bson.MvtNet;

/// <summary>
/// Base32 geohash encode/decode. No external dependencies.
/// </summary>
public static class Geohash
{
    private const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// Encodes a WGS84 coordinate into a geohash string of the given precision.
    /// </summary>
    public static string Encode(double lat, double lng, int precision)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be 1–12.");
        }

        double minLat = -90,
            maxLat = 90;
        double minLng = -180,
            maxLng = 180;
        bool isLng = true;
        int bits = 0;
        int ch = 0;
        Span<char> result = stackalloc char[precision];
        int index = 0;

        while (index < precision)
        {
            if (isLng)
            {
                double mid = (minLng + maxLng) / 2;
                if (lng >= mid)
                {
                    ch |= 1 << (4 - bits);
                    minLng = mid;
                }
                else
                {
                    maxLng = mid;
                }
            }
            else
            {
                double mid = (minLat + maxLat) / 2;
                if (lat >= mid)
                {
                    ch |= 1 << (4 - bits);
                    minLat = mid;
                }
                else
                {
                    maxLat = mid;
                }
            }

            isLng = !isLng;
            bits++;

            if (bits == 5)
            {
                result[index++] = Base32[ch];
                bits = 0;
                ch = 0;
            }
        }

        return result.ToString();
    }

    private static double Clamp(double value, double min, double max) =>
        value < min ? min
        : value > max ? max
        : value;

    /// <summary>
    /// Decodes a geohash into the bounding box it represents.
    /// </summary>
    public static GeohashBounds Decode(string geohash)
    {
        double minLat = -90,
            maxLat = 90;
        double minLng = -180,
            maxLng = 180;
        bool isLng = true;

        foreach (char c in geohash)
        {
            int val = Base32.IndexOf(c);
            if (val < 0)
            {
                throw new ArgumentException($"Invalid geohash character: '{c}'", nameof(geohash));
            }

            for (int bit = 4; bit >= 0; bit--)
            {
                if (isLng)
                {
                    double mid = (minLng + maxLng) / 2;
                    if ((val & (1 << bit)) != 0)
                    {
                        minLng = mid;
                    }
                    else
                    {
                        maxLng = mid;
                    }
                }
                else
                {
                    double mid = (minLat + maxLat) / 2;
                    if ((val & (1 << bit)) != 0)
                    {
                        minLat = mid;
                    }
                    else
                    {
                        maxLat = mid;
                    }
                }

                isLng = !isLng;
            }
        }

        return new GeohashBounds(minLat, maxLat, minLng, maxLng);
    }

    internal static List<string> GetCovering(
        double south,
        double north,
        double west,
        double east,
        int precision
    )
    {
        var sample = Decode(Encode(south, west, precision));
        double cellHeight = sample.North - sample.South;
        double cellWidth = sample.East - sample.West;

        var seen = new HashSet<string>();
        var result = new List<string>();

        double firstCellCenterLat = sample.South + cellHeight / 2;
        double firstCellCenterLng = sample.West + cellWidth / 2;

        double lat = firstCellCenterLat;
        while (lat <= north + cellHeight)
        {
            double lng = firstCellCenterLng;
            while (lng <= east + cellWidth)
            {
                string hash = Encode(
                    Clamp(lat, -90, 90),
                    Clamp(lng, -180, 180),
                    precision
                );

                var cell = Decode(hash);
                if (
                    cell.North >= south
                    && cell.South <= north
                    && cell.East >= west
                    && cell.West <= east
                )
                {
                    if (seen.Add(hash))
                    {
                        result.Add(hash);
                    }
                }

                lng += cellWidth;
            }

            lat += cellHeight;
        }

        return result;
    }
}

/// <summary>
/// WGS84 bounding box of a geohash cell, in degrees.
/// </summary>
/// <param name="South">Southern latitude.</param>
/// <param name="North">Northern latitude.</param>
/// <param name="West">Western longitude.</param>
/// <param name="East">Eastern longitude.</param>
public readonly record struct GeohashBounds(double South, double North, double West, double East);
