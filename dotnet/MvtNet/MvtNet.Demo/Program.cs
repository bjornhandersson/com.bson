using Bson.MvtNet;
using MvtNet.Demo;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// --- Cities (CSV) ---
var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "cities.csv");
var cities = City.LoadFromCsv(csvPath);
var cityIndex = new CityIndex(cities);
Console.WriteLine($"Loaded {cities.Count} cities");

// --- Earthquakes (USGS live feed) ---
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

app.UseStaticFiles();

// ============================================================
// Cities — brute-force
// ============================================================
app.MapGet(
    "/tiles/simple/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tile = new TileBuilder(z, x, y);
        var layer = tile.Layer("cities");

        foreach (var city in cities)
        {
            layer.AddPoint(
                city.Lat,
                city.Lng,
                new KeyValuePair<string, object>[]
                {
                    new("name", city.Name),
                    new("population", city.Population),
                    new("country", city.Country),
                }
            );
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
);

// ============================================================
// Cities — geohash-indexed
// ============================================================
app.MapGet(
    "/tiles/geohash/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tileCities = cityIndex.GetCitiesForTile(z, x, y);

        var tile = new TileBuilder(z, x, y);
        var layer = tile.Layer("cities");

        foreach (var city in tileCities)
        {
            layer.AddPoint(
                city.Lat,
                city.Lng,
                new KeyValuePair<string, object>[]
                {
                    new("name", city.Name),
                    new("population", city.Population),
                    new("country", city.Country),
                }
            );
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
);

// ============================================================
// Earthquakes — real USGS data
// ============================================================
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
                new KeyValuePair<string, object>[]
                {
                    new("magnitude", eq.Magnitude),
                    new("depth", eq.Depth),
                    new("place", eq.Place),
                    new("time", eq.Time),
                }
            );
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.MapFallback(ctx =>
{
    ctx.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();
