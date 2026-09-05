# HilbertIndex

Fast geospatial indexing using Hilbert space-filling curves. Convert 2D coordinates into 1D indices that preserve spatial locality - nearby points get similar index values.

## Why Hilbert Index?

The Hilbert curve maps 2D space to 1D while preserving spatial relationships. This means:

- **Nearby locations get similar index numbers**
- **Index ranges naturally represent geographic areas**
- **Perfect for zoom levels and spatial clustering**

## Real-World Examples

### Example 1: Restaurant Discovery App

```csharp
var hilbert = new HilbertCode();

// Stockholm restaurants with their Hilbert indices
var gamla_stan = hilbert.Encode(new Coordinate(18.0686, 59.3251));     // 285,432,891
var sodermalm = hilbert.Encode(new Coordinate(18.0649, 59.3186));      // 285,431,205
var ostermalm = hilbert.Encode(new Coordinate(18.0712, 59.3293));      // 285,433,847

// Notice: Gamla Stan and Södermalm are close geographically AND numerically!
// Index difference: 285,432,891 - 285,431,205 = 1,686 (small difference = nearby)
// But Östermalm is further: 285,433,847 - 285,432,891 = 956 (still relatively close)
```

### Example 2: Delivery Zone Optimization

```csharp
// Pizza delivery zones using index ranges
var hilbert = new HilbertCode();

// Central Stockholm
var zone1_start = hilbert.Encode(new Coordinate(18.050, 59.320));  // 285,400,000
var zone1_end = hilbert.Encode(new Coordinate(18.080, 59.340));    // 285,450,000

// All addresses with Hilbert index between 285,400,000 - 285,450,000
// are in the same delivery zone!
```

### Example 3: Map Tile System

```csharp
// Different zoom levels using index bit-shifting
var hilbert = new HilbertCode(HilbertCode.Resolution.HIGH); // 19-bit resolution

var coordinate = new Coordinate(18.0686, 59.3293);
var fullIndex = hilbert.Encode(coordinate);  // 285,432,891

// Zoom levels by truncating bits
var zoom_level_1 = fullIndex >> 16;  // 4,356    (country level)
var zoom_level_2 = fullIndex >> 12;  // 69,699   (city level)
var zoom_level_3 = fullIndex >> 8;   // 1,115,191 (district level)
var zoom_level_4 = fullIndex >> 4;   // 17,839,055 (street level)

// Same zoom level = same map tile!
```

## Quick Start

### 1. Basic Usage

```csharp
public class Location : IHilbertIndexable
{
    public string Name { get; set; }
    public double X { get; set; }  // Longitude
    public double Y { get; set; }  // Latitude
    public ulong Hid { get; set; } // The magic number!

    public static Location Create(string name, double lon, double lat)
    {
        var hilbert = new HilbertCode();
        return new Location
        {
            Name = name,
            X = lon,
            Y = lat,
            Hid = hilbert.Encode(new Coordinate(lon, lat))
        };
    }
}
```

### 2. Spatial Queries

```csharp
var locations = new[]
{
    Location.Create("Central Station", 18.0586, 59.3293),
    Location.Create("Royal Palace", 18.0717, 59.3268),
    Location.Create("City Hall", 18.0546, 59.3275)
}.OrderBy(l => l.Hid).ToArray(); // Sort by Hilbert index!

var index = new HilbertIndex<Location>(locations);

// Find everything within 500m
var nearby = index.Within(new Coordinate(18.0600, 59.3280), 500);

// Find closest location
var nearest = index.NearestNeighbours(new Coordinate(18.0600, 59.3280)).First();
```

## Installation

```bash
dotnet add package Bson.HilbertIndex
```

## Resolution Levels

| Resolution     | Grid Size      | Precision | Best For               |
| -------------- | -------------- | --------- | ---------------------- |
| LOW (10)       | 1,024²         | ~40m      | City districts         |
| MEDIUM (16)    | 65,536²        | ~650m     | Regional areas         |
| HIGH (19)      | 524,288²       | ~79m      | Street-level (default) |
| ULTRAHIGH (30) | 1,073,741,824² | ~10m      | Building-level         |

## Projections

The curve is drawn over a square integer grid. An `IProjection` decides how the globe is stretched onto that
square, and it is pluggable:

```csharp
// Original behaviour (default): equirectangular, straight lat/lon scaling
var classic = new HilbertCode();

// Web Mercator: conformal cells, aligned with XYZ map tiles
var mercator = new HilbertCode(HilbertCode.Resolution.HIGH, new WebMercatorProjection());

// The index must use the same HilbertCode that produced the items' Hid values
var index = new HilbertIndex<Location>(locations, mercator);
```

| Projection              | Cells                                   | Ground size at order 19       | Notes                                                                     |
| ----------------------- | --------------------------------------- | ----------------------------- | ------------------------------------------------------------------------- |
| `LinearProjection`      | 360/N° wide, 180/N° tall                | ~76m x 38m at equator         | The original 2006 mapping. Default. Cells get very narrow near the poles. |
| `WebMercatorProjection` | Locally square, shrink with cos(lat)    | ~76m x 76m at equator         | Same grid as XYZ tiles at zoom = order (y flipped). Cut off at ±85.05°.   |

A Hilbert id from one projection is not comparable with an id from another. Pick one per dataset and stick to it.

With `WebMercatorProjection`, shifting an id right by two bits per level gives the id of the containing map tile
at that zoom, which makes the Hilbert key double as a tile key.

Implement `IProjection` yourself to use another mapping, for example a cube face projection to get S2 style
uniformity, or a local projected CRS for a single-country dataset.

## License

MIT
