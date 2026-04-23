using System.Collections.Concurrent;
using Bson.MvtNet;

namespace MvtNet.Demo;

public static class KmlDemo
{
    private static KmlDocument? _currentUpload;
    private static readonly ConcurrentDictionary<string, byte[]> _iconCache = new();
    private static readonly HttpClient _iconHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static void Map(WebApplication app)
    {
        // User uploads a KML; we parse and hold features in memory (replaces prior upload).
        app.MapPost(
            "/upload",
            async (HttpRequest request) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.BadRequest(new { error = "expected multipart form" });
                }

                var form = await request.ReadFormAsync();
                var file =
                    form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
                if (file is null)
                {
                    return Results.BadRequest(new { error = "no file provided" });
                }

                try
                {
                    await using var stream = file.OpenReadStream();
                    var doc = KmlLoader.Load(stream);
                    _currentUpload = doc;
                    return Results.Ok(
                        new
                        {
                            features = doc.Features.Count,
                            bounds = doc.Bounds is null
                                ? null
                                : new[]
                                {
                                    doc.Bounds.West,
                                    doc.Bounds.South,
                                    doc.Bounds.East,
                                    doc.Bounds.North,
                                },
                            icons = doc.IconUrls,
                        }
                    );
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
        );

        app.MapGet(
            "/tiles/upload/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) =>
            {
                var doc = _currentUpload;
                var tile = new TileBuilder(z, x, y);

                if (doc is not null)
                {
                    var layer = tile.Layer("kml");

                    foreach (var f in doc.Features)
                    {
                        var attrs = BuildAttrs(f);
                        switch (f.Type)
                        {
                            case KmlGeomType.Point:
                                layer.AddPoint(f.Coords[0].Lat, f.Coords[0].Lng, attrs);
                                break;
                            case KmlGeomType.Line:
                                layer.AddLineString(f.Coords, attrs);
                                break;
                            case KmlGeomType.Polygon:
                                layer.AddPolygon(f.Coords, attrs);
                                break;
                        }
                    }
                }

                return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
            }
        );

        // Sidesteps CORS for external KML icon URLs.
        app.MapGet(
            "/icons",
            async (string url) =>
            {
                if (_iconCache.TryGetValue(url, out var cached))
                {
                    return Results.Bytes(cached, GuessImageType(url));
                }

                try
                {
                    var bytes = await _iconHttp.GetByteArrayAsync(url);
                    _iconCache[url] = bytes;
                    return Results.Bytes(bytes, GuessImageType(url));
                }
                catch
                {
                    return Results.NotFound();
                }
            }
        );
    }

    private static Dictionary<string, object> BuildAttrs(KmlFeature f)
    {
        var attrs = new Dictionary<string, object>();
        if (f.Name is not null)
        {
            attrs["name"] = f.Name;
        }
        if (f.Description is not null)
        {
            attrs["description"] = f.Description;
        }
        if (f.IconUrl is not null)
        {
            attrs["icon"] = f.IconUrl;
        }
        attrs["iconScale"] = f.IconScale;
        if (f.LineColor is not null)
        {
            attrs["lineColor"] = f.LineColor;
        }
        attrs["lineWidth"] = f.LineWidth;
        if (f.FillColor is not null)
        {
            attrs["fillColor"] = f.FillColor;
        }
        attrs["filled"] = f.Filled;
        attrs["outlined"] = f.Outlined;
        return attrs;
    }

    private static string GuessImageType(string url) =>
        Path.GetExtension(url).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
}
