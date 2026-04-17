using Bson.MvtNet;

namespace MvtNet.Demo;

/// <summary>
/// Indexes cities by geohash prefix for fast tile lookups.
/// Instead of scanning all 10,000 cities per tile request,
/// we use TileGeohash to find which geohash cells overlap
/// the tile and only return cities in those cells.
/// </summary>
public class CityIndex
{
    private const int MaxPrecision = 7;
    private readonly Dictionary<string, List<City>> _index = new();

    public CityIndex(List<City> cities)
    {
        foreach (var city in cities)
        {
            var hash = Geohash.Encode(city.Lat, city.Lng, MaxPrecision);

            // Index at every prefix length so any zoom level can query efficiently
            for (int p = 1; p <= MaxPrecision; p++)
            {
                var prefix = hash[..p];
                if (!_index.TryGetValue(prefix, out var list))
                {
                    list = new List<City>();
                    _index[prefix] = list;
                }

                list.Add(city);
            }
        }
    }

    public List<City> GetCitiesForTile(int z, int x, int y)
    {
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var result = new List<City>();

        foreach (var prefix in prefixes)
        {
            if (_index.TryGetValue(prefix, out var cities))
            {
                result.AddRange(cities);
            }
        }

        return result;
    }
}
