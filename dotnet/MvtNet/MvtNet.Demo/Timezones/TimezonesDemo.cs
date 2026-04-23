using Bson.MvtNet;

namespace MvtNet.Demo;

public static class TimezonesDemo
{
    public static async Task MapAsync(WebApplication app)
    {
        List<TimezonePolygon> timezones;
        try
        {
            timezones = await TimezoneFeed.LoadAsync();
            Console.WriteLine($"Loaded {timezones.Count} timezone polygons");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Warning: could not load timezone data ({ex.Message}), timezone demo will be empty"
            );
            timezones = [];
        }

        app.MapGet(
            "/tiles/timezones/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) =>
            {
                var tile = new TileBuilder(z, x, y);
                var layer = tile.Layer("timezones");

                foreach (var tz in timezones)
                {
                    layer.AddPolygon(
                        tz.Ring,
                        new Dictionary<string, object>
                        {
                            ["name"] = tz.Name,
                            ["utc_offset"] = tz.UtcOffset,
                        }
                    );
                }

                return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
            }
        );
    }
}
