# MvtNet Demo

```
dotnet run --project MvtNet.Demo
```

Open http://localhost:5000

## Demos

- **Cities** — 10,000 points from CSV with Simple vs Geohash-indexed tile serving
- **Earthquakes** — live USGS earthquake feed rendered as points
- **Flight Routes** — great-circle arcs between major cities (`AddLineString` with automatic tile clipping)
- **Timezones** — real Natural Earth timezone boundaries (`AddPolygon`, downloaded and cached on first run)
