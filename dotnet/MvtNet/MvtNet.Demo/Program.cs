using MvtNet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tileBuilder = new TileBuilder(z, x, y);

        // Points — landmarks
        var points = tileBuilder.Layer("points");
        points.AddPoint(
            59.3281936,
            18.0440866,
            new Dictionary<string, object> { ["name"] = "Stockholm Central" }
        );
        points.AddPoint(
            59.3326,
            18.0649,
            new Dictionary<string, object> { ["name"] = "Östermalm" }
        );
        points.AddPoint(
            59.3190,
            18.0686,
            new Dictionary<string, object> { ["name"] = "Södermalm" }
        );
        points.AddPoint(59.3340, 18.0300, new Dictionary<string, object> { ["name"] = "Norrmalm" });

        // LineString — a route through the city
        var tracks = tileBuilder.Layer("tracks");
        tracks.AddLineString(
            new (double, double)[]
            {
                (59.3340, 18.0300),
                (59.3310, 18.0400),
                (59.3281936, 18.0440866),
                (59.3260, 18.0550),
                (59.3326, 18.0649),
            },
            new Dictionary<string, object> { ["name"] = "City Walk" }
        );

        // Polygon — a geofence around central Stockholm
        var geofences = tileBuilder.Layer("geofences");
        geofences.AddPolygon(
            new (double, double)[]
            {
                (59.3380, 18.0250),
                (59.3380, 18.0750),
                (59.3150, 18.0750),
                (59.3150, 18.0250),
            },
            new Dictionary<string, object> { ["name"] = "Central Zone" }
        );

        return Results.Bytes(tileBuilder.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();
