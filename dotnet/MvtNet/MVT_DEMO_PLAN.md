# MvtNet Demo Plan

## Demo 1: Simple — 10,000 Cities on a Map

### What

A self-contained ASP.NET minimal API serving the world's 10,000 largest cities as vector tiles, rendered on a MapLibre GL JS map.

### Data

- Source: GeoNames cities1000.txt (public domain)
- Filter: top 10,000 cities by population globally
- Fields: name, lat, lng, population, country
- Range: Shanghai (24.8M) down to ~61k population
- CSV already prepared at `MvtNet.Demo/Data/cities.csv`
- Loaded into memory on startup

### Backend

- ASP.NET minimal API
- Single `Program.cs`
- One endpoint: `GET /tiles/{z}/{x}/{y}` — serves MVT bytes via MvtNet
- Loads CSV on startup, serves tiles from in-memory list
- Points go into a "cities" layer with name, population, country as attributes

### Frontend

- Single `wwwroot/index.html`
- MapLibre GL JS via CDN
- Points styled by population (color and/or size)
- Click popup showing city name, country, population

### Project structure

- New project `MvtNet.Demo` in the MvtNet solution
- Project references Bson.MvtNet directly
- Added to MvtNet.slnx

### Files

```
MvtNet/
  MvtNet.slnx              (add MvtNet.Demo)
  MvtNet.Demo/
    MvtNet.Demo.csproj
    Program.cs
    wwwroot/
      index.html
    Data/
      cities.csv            (already prepared, 10k rows)
```

---

## Demo 2: Advanced — Weather Pressure Map (future)

Synthetic weather pressure system showing all three geometry types at volume.

### Geometry

- **Points** — weather station readings across Europe (thousands, with pressure/temperature attributes)
- **Lines** — isobars connecting equal pressure values (contour lines at 4 hPa intervals)
- **Polygons** — high/low pressure zones

### Data generation

Place ~20 pressure centers (highs and lows) across Europe. Compute a smooth pressure field as a weighted sum of Gaussian functions. Then:

1. Sample thousands of station points from the field
2. Extract isobar lines by marching squares or similar contouring on a grid
3. Derive pressure zone polygons from the same field

Fully procedural — no external data, looks realistic.

### Frontend styling

- Isobars colored by pressure value (blue for low, red for high)
- Station points sized/colored by reading
- Pressure zone polygons with semi-transparent fills
- Labels on isobars showing hPa values

### Open questions (for later)

- Europe-focused or wider region?
- Animation (time steps showing pressure system movement)?
