using MvtNet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const double sampleLat = 59.3281936;
const double sampleLng = 18.0440866;

app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    (int z, int x, int y) =>
    {
        var tileBuilder = new TileBuilder(z, x, y);
        var layer = tileBuilder.Layer("points");

        layer.AddPoint(
            sampleLat,
            sampleLng,
            new Dictionary<string, object> { ["name"] = "Stockholm" }
        );

        return Results.Bytes(tileBuilder.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();
