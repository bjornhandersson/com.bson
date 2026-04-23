using Bson.MvtNet;

namespace MvtNet.Demo;

public static class CitiesDemo
{
    public static void Map(WebApplication app)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "Cities", "cities.csv");
        var cities = City.LoadFromCsv(csvPath);
        var index = new CityIndex(cities);
        Console.WriteLine($"Loaded {cities.Count} cities");

        app.MapGet(
            "/tiles/simple/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) => BuildTile(z, x, y, cities)
        );

        app.MapGet(
            "/tiles/geohash/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) => BuildTile(z, x, y, index.GetCitiesForTile(z, x, y))
        );
    }

    private static IResult BuildTile(int z, int x, int y, IEnumerable<City> cities)
    {
        var tile = new TileBuilder(z, x, y);
        var layer = tile.Layer("cities");

        foreach (var c in cities)
        {
            layer.AddPoint(
                c.Lat,
                c.Lng,
                new Dictionary<string, object>
                {
                    ["name"] = c.Name,
                    ["population"] = c.Population,
                    ["country"] = c.Country,
                }
            );
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
}
