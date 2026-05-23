using System.Text.Json;
using Bson.MvtNet;

namespace MvtNet.Demo;

public static class TimezonesDemo
{
    public static async Task MapAsync(WebApplication app)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Timezones", "timezones.geojson");

        JsonDocument? doc = null;
        try
        {
            await using var stream = File.OpenRead(file);
            doc = await JsonDocument.ParseAsync(stream);
            Console.WriteLine("Loaded timezone GeoJSON");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Warning: could not load timezone data ({ex.Message}), timezone demo will be empty"
            );
        }

        app.MapGet(
            "/tiles/timezones/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) =>
            {
                var tile = new TileBuilder(z, x, y);
                if (doc is not null)
                {
                    tile.Layer("timezones").AddGeoJson(doc.RootElement);
                }
                return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
            }
        );
    }
}
