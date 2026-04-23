using Bson.MvtNet;

namespace MvtNet.Demo;

public static class EarthquakesDemo
{
    public static async Task MapAsync(WebApplication app)
    {
        List<Earthquake> earthquakes;
        try
        {
            earthquakes = await UsgsEarthquakeFeed.FetchAsync();
            Console.WriteLine($"Loaded {earthquakes.Count} earthquakes from USGS");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Warning: could not fetch USGS data ({ex.Message}), earthquake demo will be empty"
            );
            earthquakes = [];
        }

        app.MapGet(
            "/tiles/earthquakes/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) =>
            {
                var tile = new TileBuilder(z, x, y);
                var layer = tile.Layer("earthquakes");

                foreach (var eq in earthquakes)
                {
                    layer.AddPoint(
                        eq.Lat,
                        eq.Lng,
                        new Dictionary<string, object>
                        {
                            ["magnitude"] = eq.Magnitude,
                            ["depth"] = eq.Depth,
                            ["place"] = eq.Place,
                            ["time"] = eq.Time,
                        }
                    );
                }

                return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
            }
        );
    }
}
