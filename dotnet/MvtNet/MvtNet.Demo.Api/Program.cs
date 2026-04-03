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

app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tileBuilder = new TileBuilder(z, x, y);

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

        return Results.Bytes(tileBuilder.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();
