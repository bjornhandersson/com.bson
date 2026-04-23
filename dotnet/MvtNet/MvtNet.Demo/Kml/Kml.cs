using System.Globalization;
using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using KmlPoint = SharpKml.Dom.Point;
using KmlPolygon = SharpKml.Dom.Polygon;

namespace MvtNet.Demo;

public enum KmlGeomType
{
    Point,
    Line,
    Polygon,
}

public record KmlFeature(
    KmlGeomType Type,
    (double Lat, double Lng)[] Coords,
    string? Name,
    string? Description,
    string? IconUrl,
    double IconScale,
    string? LineColor,
    double LineWidth,
    string? FillColor,
    bool Filled,
    bool Outlined
);

public record KmlBounds(double South, double West, double North, double East);

public record KmlDocument(
    IReadOnlyList<KmlFeature> Features,
    KmlBounds? Bounds,
    IReadOnlyCollection<string> IconUrls
);

public static class KmlLoader
{
    public static KmlDocument Load(Stream stream)
    {
        var parser = new Parser();
        parser.Parse(stream);

        var root = parser.Root switch
        {
            Kml k => k.Feature,
            Feature f => f,
            _ => null,
        };
        if (root is null)
        {
            return new KmlDocument([], null, []);
        }

        var styles = new Dictionary<string, StyleSelector>();
        CollectStyles(root, styles);

        var features = new List<KmlFeature>();
        Walk(root, styles, features);

        var icons = new HashSet<string>();
        foreach (var f in features)
        {
            if (f.IconUrl is not null)
            {
                icons.Add(f.IconUrl);
            }
        }

        return new KmlDocument(features, ComputeBounds(features), icons);
    }

    private static void CollectStyles(Feature feature, Dictionary<string, StyleSelector> styles)
    {
        foreach (var sel in feature.Styles)
        {
            if (!string.IsNullOrEmpty(sel.Id))
            {
                styles[sel.Id] = sel;
            }
        }

        if (feature is Container container)
        {
            foreach (var child in container.Features)
            {
                CollectStyles(child, styles);
            }
        }
    }

    private static void Walk(
        Feature feature,
        IReadOnlyDictionary<string, StyleSelector> styles,
        List<KmlFeature> features
    )
    {
        if (feature is Container container)
        {
            foreach (var child in container.Features)
            {
                Walk(child, styles, features);
            }
            return;
        }

        if (feature is Placemark placemark && placemark.Geometry is not null)
        {
            var style = ResolveStyle(placemark, styles);
            EmitGeometry(
                placemark.Geometry,
                placemark.Name,
                placemark.Description?.Text,
                style,
                features
            );
        }
    }

    private static Style ResolveStyle(
        Placemark placemark,
        IReadOnlyDictionary<string, StyleSelector> styles
    )
    {
        var inline = placemark.Styles.OfType<Style>().FirstOrDefault();
        if (inline is not null)
        {
            return inline;
        }

        if (placemark.StyleUrl is null)
        {
            return new Style();
        }

        var id = placemark.StyleUrl.OriginalString.TrimStart('#');
        if (!styles.TryGetValue(id, out var selector))
        {
            return new Style();
        }

        return selector switch
        {
            Style s => s,
            StyleMapCollection map => ResolveStyleMap(map, styles),
            _ => new Style(),
        };
    }

    private static Style ResolveStyleMap(
        StyleMapCollection map,
        IReadOnlyDictionary<string, StyleSelector> styles
    )
    {
        foreach (var pair in map)
        {
            if (pair.State != StyleState.Normal)
            {
                continue;
            }

            if (pair.Selector is Style inline)
            {
                return inline;
            }

            if (pair.StyleUrl is null)
            {
                continue;
            }

            var id = pair.StyleUrl.OriginalString.TrimStart('#');
            if (styles.TryGetValue(id, out var sel) && sel is Style s)
            {
                return s;
            }
        }

        return new Style();
    }

    private static void EmitGeometry(
        Geometry geom,
        string? name,
        string? description,
        Style style,
        List<KmlFeature> features
    )
    {
        switch (geom)
        {
            case KmlPoint p when p.Coordinate is not null:
                features.Add(
                    MakeFeature(
                        KmlGeomType.Point,
                        [(p.Coordinate.Latitude, p.Coordinate.Longitude)],
                        name,
                        description,
                        style
                    )
                );
                break;

            case LineString l when l.Coordinates is not null:
                features.Add(
                    MakeFeature(
                        KmlGeomType.Line,
                        l.Coordinates.Select(c => (c.Latitude, c.Longitude)).ToArray(),
                        name,
                        description,
                        style
                    )
                );
                break;

            case KmlPolygon poly when poly.OuterBoundary?.LinearRing?.Coordinates is { } coords:
                var ring = coords.Select(c => (c.Latitude, c.Longitude)).ToArray();
                if (ring.Length > 1 && ring[0] == ring[^1])
                {
                    ring = ring[..^1];
                }
                features.Add(MakeFeature(KmlGeomType.Polygon, ring, name, description, style));
                break;

            case MultipleGeometry mg:
                foreach (var child in mg.Geometry)
                {
                    EmitGeometry(child, name, description, style, features);
                }
                break;
        }
    }

    private static KmlFeature MakeFeature(
        KmlGeomType type,
        (double Lat, double Lng)[] coords,
        string? name,
        string? description,
        Style style
    )
    {
        var icon = style.Icon;
        var line = style.Line;
        var poly = style.Polygon;

        return new KmlFeature(
            Type: type,
            Coords: coords,
            Name: name,
            Description: description,
            IconUrl: icon?.Icon?.Href?.OriginalString,
            IconScale: icon?.Scale ?? 1.0,
            LineColor: line?.Color is { } lc ? FormatColor(lc) : null,
            LineWidth: line?.Width ?? 1.0,
            FillColor: poly?.Color is { } pc ? FormatColor(pc) : null,
            Filled: poly?.Fill ?? true,
            Outlined: poly?.Outline ?? true
        );
    }

    private static string FormatColor(Color32 c) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "#{0:x2}{1:x2}{2:x2}{3:x2}",
            c.Red,
            c.Green,
            c.Blue,
            c.Alpha
        );

    private static KmlBounds? ComputeBounds(IReadOnlyList<KmlFeature> features)
    {
        var minLat = double.MaxValue;
        var maxLat = double.MinValue;
        var minLng = double.MaxValue;
        var maxLng = double.MinValue;
        var any = false;

        foreach (var f in features)
        {
            foreach (var (lat, lng) in f.Coords)
            {
                if (lat < minLat)
                {
                    minLat = lat;
                }
                if (lat > maxLat)
                {
                    maxLat = lat;
                }
                if (lng < minLng)
                {
                    minLng = lng;
                }
                if (lng > maxLng)
                {
                    maxLng = lng;
                }
                any = true;
            }
        }

        return any ? new KmlBounds(minLat, minLng, maxLat, maxLng) : null;
    }
}
