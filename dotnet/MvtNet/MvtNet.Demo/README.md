# MvtNet Demo

```
dotnet run --project MvtNet.Demo
```

Open http://localhost:5000

## Demos

- **Cities** — 10,000 points from CSV with Simple vs Geohash-indexed tile serving
- **Earthquakes** — live USGS earthquake feed rendered as points
- **Timezones** — world timezone polygons from a bundled GeoJSON file (`AddPolygon`, exercises ring clipping). Data from [treyerl/timezones](https://github.com/treyerl/timezones), MIT, see [ATTRIBUTION](Timezones/ATTRIBUTION.md)
- **KML Upload** — drop a KML file, parsed server-side and served as MVT with its original styling preserved
- **GeoJSON** — paste any GeoJSON, it is parsed server-side and served straight back as vector tiles (`AddGeoJson`)
