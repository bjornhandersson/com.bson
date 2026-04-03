using MvtNet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Generate a 3000-point route: Stockholm → Norrköping (~200km)
const int routePoints = 3000;
const double startLat = 59.33,
    startLng = 18.04;
const double endLat = 58.59,
    endLng = 16.18;

var route = new (double, double)[routePoints];
for (int i = 0; i < routePoints; i++)
{
    double t = (double)i / (routePoints - 1);
    route[i] = (startLat + (endLat - startLat) * t, startLng + (endLng - startLng) * t);
}

// In-memory POI store keyed by geohash
var poiStore = new Dictionary<string, List<Poi>>();

var stockholmPois = new (double Lat, double Lng, string Name)[]
{
    (59.3293, 18.0686, "Kungliga Slottet"),
    (59.3275, 18.0716, "Storkyrkan"),
    (59.3252, 18.0707, "Riksdagshuset"),
    (59.3326, 18.0649, "Kungsträdgården"),
    (59.3350, 18.0597, "T-Centralen"),
    (59.3190, 18.0686, "Södermalm"),
    (59.3147, 18.0935, "Fotografiska"),
    (59.3280, 18.0488, "Stadshuset"),
    (59.3320, 18.1028, "Djurgården"),
    (59.3289, 18.0924, "Vasamuseet"),
    (59.3285, 18.0985, "Nordiska Museet"),
    (59.3274, 18.1013, "ABBA Museet"),
    (59.3233, 18.0565, "Medborgarplatsen"),
    (59.3382, 18.0760, "Östermalms Saluhall"),
    (59.3423, 18.0467, "Odenplan"),
    (59.3090, 18.0790, "Hammarby Sjöstad"),
    (59.3370, 18.0380, "Vasastan"),
    (59.3456, 18.0559, "Roslagstull"),
    (59.3180, 18.0567, "Hornstull"),
    (59.3354, 18.0880, "Stureplan"),
    (59.3500, 18.0220, "Solna centrum"),
    (59.3100, 18.1100, "Nacka"),
    (59.3600, 18.0000, "Sundbyberg"),
    (59.3060, 18.0630, "Globen"),
    (59.2850, 18.0750, "Farsta"),
    (59.3660, 18.0050, "Ulriksdal"),
    (59.3160, 18.0400, "Liljeholmen"),
    (59.2980, 18.0350, "Fruängen"),
    (59.3550, 18.1050, "Lidingö"),
    (59.3400, 18.0300, "Sankt Eriksplan"),
};

foreach (var (lat, lng, name) in stockholmPois)
{
    // Store at precision 7 for fine-grained lookup
    string hash = Geohash.Encode(lat, lng, 7);
    if (!poiStore.TryGetValue(hash, out var list))
    {
        list = new List<Poi>();
        poiStore[hash] = list;
    }

    list.Add(new Poi(lat, lng, name));
}

app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tileBuilder = new TileBuilder(z, x, y);

        // Route layer
        tileBuilder
            .Layer("route")
            .AddLineString(
                route,
                new Dictionary<string, object> { ["name"] = "Stockholm → Norrköping" }
            );

        var points = tileBuilder.Layer("points");
        points.AddPoint(
            startLat,
            startLng,
            new Dictionary<string, object> { ["name"] = "Stockholm" }
        );
        points.AddPoint(endLat, endLng, new Dictionary<string, object> { ["name"] = "Norrköping" });

        // POI layer — query via geohash prefixes
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var pois = tileBuilder.Layer("pois");

        foreach (var prefix in prefixes)
        {
            foreach (var (hash, poiList) in poiStore)
            {
                if (hash.StartsWith(prefix))
                {
                    foreach (var poi in poiList)
                    {
                        pois.AddPoint(
                            poi.Lat,
                            poi.Lng,
                            new Dictionary<string, object> { ["name"] = poi.Name }
                        );
                    }
                }
            }
        }

        return Results.Bytes(tileBuilder.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();

record Poi(double Lat, double Lng, string Name);
