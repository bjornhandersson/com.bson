using Bson.MvtNet;
using MvtNet.Demo;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// --- Cities (CSV) ---
var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "cities.csv");
var cities = City.LoadFromCsv(csvPath);
var cityIndex = new CityIndex(cities);
Console.WriteLine($"Loaded {cities.Count} cities");

// --- Routes (great-circle arcs between top cities) ---
var routes = FlightRouteBuilder.BuildRoutes(cities);
Console.WriteLine($"Generated {routes.Count} flight routes");

// --- Timezones (Natural Earth boundaries) ---
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
                new Dictionary<string, object>
                {
                    ["name"] = city.Name,
                    ["population"] = city.Population,
                    ["country"] = city.Country,
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
                new Dictionary<string, object>
                {
                    ["name"] = city.Name,
                    ["population"] = city.Population,
                    ["country"] = city.Country,
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

// ============================================================
// Routes — great-circle flight paths (linestrings)
// ============================================================
app.MapGet(
    "/tiles/routes/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tile = new TileBuilder(z, x, y);
        var layer = tile.Layer("routes");

        foreach (var route in routes)
        {
            layer.AddLineString(
                route.Path,
                new Dictionary<string, object>
                {
                    ["from"] = route.From,
                    ["to"] = route.To,
                    ["distance_km"] = Math.Round(route.DistanceKm),
                }
            );
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
);

// ============================================================
// Timezones — Natural Earth boundaries (polygons)
// ============================================================
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
                new Dictionary<string, object> { ["name"] = tz.Name, ["utc_offset"] = tz.UtcOffset }
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
