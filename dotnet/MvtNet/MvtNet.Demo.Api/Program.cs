using MvtNet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ---------------------------------------------------------------------------
// Generate demo data in-memory
// ---------------------------------------------------------------------------
var random = new Random(42);

var cities = new (double Lat, double Lng, string Name)[]
{
    (59.33, 18.07, "Stockholm"),
    (58.59, 16.18, "Norrköping"),
    (58.41, 15.63, "Linköping"),
    (59.86, 17.64, "Uppsala"),
    (58.72, 16.85, "Nyköping"),
    (59.37, 16.51, "Katrineholm"),
    (59.27, 15.21, "Örebro"),
    (58.35, 11.92, "Uddevalla"),
    (57.71, 11.97, "Göteborg"),
    (55.60, 13.00, "Malmö"),
};

// 50k POIs clustered around each city
var pois = new List<(double Lat, double Lng, string Name, string Geohash)>();
foreach (var (cLat, cLng, label) in cities)
{
    for (int i = 0; i < 5_000; i++)
    {
        double angle = random.NextDouble() * Math.PI * 2;
        double dist = 0.08 * Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble() * 0.9999));
        double lat = Math.Clamp(cLat + dist * Math.Sin(angle), -85, 85);
        double lng = cLng + dist * Math.Cos(angle);
        pois.Add((lat, lng, $"{label}-{i}", Geohash.Encode(lat, lng, 7)));
    }
}

// Build a geohash lookup for fast tile queries
var poiByPrefix = new Dictionary<string, List<(double Lat, double Lng, string Name)>>();
foreach (var (lat, lng, name, hash) in pois)
{
    for (int len = 1; len <= 7; len++)
    {
        string prefix = hash[..len];
        if (!poiByPrefix.TryGetValue(prefix, out var list))
        {
            list = new List<(double Lat, double Lng, string Name)>();
            poiByPrefix[prefix] = list;
        }
        list.Add((lat, lng, name));
    }
}

// Route: Stockholm → Norrköping
const int routePoints = 3000;
var route = new (double, double)[routePoints];
for (int i = 0; i < routePoints; i++)
{
    double t = (double)i / (routePoints - 1);
    route[i] = (59.33 + (58.59 - 59.33) * t, 18.07 + (16.18 - 18.07) * t);
}

// Polygon: approximate Lake Mälaren
var lake = new (double, double)[]
{
    (59.45, 16.85), (59.50, 17.20), (59.48, 17.65),
    (59.35, 17.95), (59.28, 17.80), (59.22, 17.30),
    (59.25, 16.95), (59.33, 16.75),
};

Console.WriteLine($"Demo ready: {pois.Count:N0} POIs, {routePoints:N0}-point route, 1 polygon");

// ---------------------------------------------------------------------------
// Tile endpoint
// ---------------------------------------------------------------------------
app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tile = new TileBuilder(z, x, y);

        // Route layer
        tile.Layer("route")
            .AddLineString(
                route,
                new Dictionary<string, object> { ["name"] = "Stockholm → Norrköping" }
            );

        // Endpoint markers
        var markers = tile.Layer("markers");
        markers.AddPoint(59.33, 18.07, new Dictionary<string, object> { ["name"] = "Stockholm" });
        markers.AddPoint(58.59, 16.18, new Dictionary<string, object> { ["name"] = "Norrköping" });

        // Lake polygon
        tile.Layer("areas")
            .AddPolygon(
                lake,
                new Dictionary<string, object> { ["name"] = "Mälaren", ["type"] = "lake" }
            );

        // POI layer — geohash prefix lookup (same pattern as a real DB query)
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var poiLayer = tile.Layer("pois");
        foreach (var prefix in prefixes)
        {
            if (poiByPrefix.TryGetValue(prefix, out var matches))
            {
                foreach (var (lat, lng, name) in matches)
                {
                    poiLayer.AddPoint(lat, lng, new Dictionary<string, object> { ["name"] = name });
                }
            }
        }

        return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();
