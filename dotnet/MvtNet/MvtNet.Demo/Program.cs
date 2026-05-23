using MvtNet.Demo;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

CitiesDemo.Map(app);
await EarthquakesDemo.MapAsync(app);
await TimezonesDemo.MapAsync(app);
KmlDemo.Map(app);
GeoJsonPasteDemo.Map(app);

app.MapFallback(ctx =>
{
    ctx.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();
