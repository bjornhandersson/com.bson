using Bson.MvtNet;
using MvtNet.Demo;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "cities.csv");
var cities = City.LoadFromCsv(csvPath);
var cityIndex = new CityIndex(cities);
Console.WriteLine($"Loaded {cities.Count} cities");

app.UseStaticFiles();

// Demo 1: Brute-force — iterates all 10,000 cities for every tile
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

        var bytes = tile.Build();
        return Results.Bytes(bytes, "application/vnd.mapbox-vector-tile");
    }
);

// Demo 2: Geohash-indexed — only fetches cities that overlap the tile
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

        var bytes = tile.Build();
        return Results.Bytes(bytes, "application/vnd.mapbox-vector-tile");
    }
);

app.MapFallback(ctx =>
{
    ctx.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();
