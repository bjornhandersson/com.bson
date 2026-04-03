using MvtNet;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var xTile = StubTileBuilder.BuildXTile();

app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", (int z, int x, int y) =>
{
    return Results.Bytes(xTile, "application/vnd.mapbox-vector-tile");
});

app.Run();
