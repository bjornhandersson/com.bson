using System.Text;
using Dapper;
using MvtNet;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString =
    Environment.GetEnvironmentVariable("MYSQL_CONNECTION")
    ?? "Server=localhost;Port=3306;Database=mvtnet;User=mvtnet;Password=mvtnet;";

// ---------------------------------------------------------------------------
// Wait for MySQL and seed if needed
// ---------------------------------------------------------------------------
await WaitForMySql(connectionString);
await SeedIfEmpty(connectionString);

// ---------------------------------------------------------------------------
// Generate the Stockholm → Norrköping route (kept in-memory, it's static)
// ---------------------------------------------------------------------------
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

// ---------------------------------------------------------------------------
// Tile endpoint
// ---------------------------------------------------------------------------
app.Use(
    async (context, next) =>
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await next();
        sw.Stop();
        var path = context.Request.Path;
        var status = context.Response.StatusCode;
        var bytes = context.Response.ContentLength ?? 0;
        Console.WriteLine($"{status} {path} {bytes:N0}B {sw.ElapsedMilliseconds}ms");
    }
);

app.MapGet(
    "/tiles/{z:int}/{x:int}/{y:int}",
    async (int z, int x, int y) =>
    {
        var tileBuilder = new TileBuilder(z, x, y);

        // Route layer
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

        // POI layer — query MySQL via geohash prefixes
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var pois = tileBuilder.Layer("pois");

        await using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync();

        var querySw = System.Diagnostics.Stopwatch.StartNew();

        // Build: WHERE geohash LIKE @p0 OR geohash LIKE @p1 OR ...
        // B-tree index on geohash makes each LIKE 'prefix%' a range scan
        var sb = new StringBuilder("SELECT lat, lng, name FROM pois WHERE ");
        var parameters = new DynamicParameters();

        for (int i = 0; i < prefixes.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" OR ");
            }
            sb.Append($"geohash LIKE @p{i}");
            parameters.Add($"p{i}", prefixes[i] + "%");
        }

        var rows = (await conn.QueryAsync<PoiRow>(sb.ToString(), parameters)).AsList();
        querySw.Stop();

        foreach (var row in rows)
        {
            pois.AddPoint(
                row.lat,
                row.lng,
                new Dictionary<string, object> { ["name"] = row.name }
            );
        }

        Console.WriteLine(
            $"  z{z} geohash: {prefixes.Count} prefixes (len {prefixes[0].Length}) → {rows.Count:N0} rows in {querySw.ElapsedMilliseconds}ms"
        );

        return Results.Bytes(tileBuilder.Build(), "application/vnd.mapbox-vector-tile");
    }
);

app.Run();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static async Task WaitForMySql(string connectionString)
{
    Console.Write("Waiting for MySQL...");
    for (int attempt = 0; attempt < 30; attempt++)
    {
        try
        {
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            Console.WriteLine(" connected.");
            return;
        }
        catch
        {
            Console.Write(".");
            await Task.Delay(2000);
        }
    }

    throw new Exception("MySQL not reachable after 60 seconds.");
}

static async Task SeedIfEmpty(string connectionString)
{
    await using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();

    // Create table
    await conn.ExecuteAsync(
        """
        CREATE TABLE IF NOT EXISTS pois (
            id        INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
            geohash   VARCHAR(12)  NOT NULL,
            lat       DOUBLE       NOT NULL,
            lng       DOUBLE       NOT NULL,
            name      VARCHAR(100) NOT NULL,
            INDEX idx_geohash (geohash)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """
    );

    var count = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM pois");
    if (count > 0)
    {
        Console.WriteLine($"Database already seeded with {count:N0} POIs.");
        return;
    }

    Console.Write("Seeding 1M POIs...");
    var random = new Random(42);

    var clusters = new (double Lat, double Lng, double Radius, int Count, string Label)[]
    {
        // Europe
        (48.8566, 2.3522, 0.15, 30_000, "Paris"),
        (51.5074, -0.1278, 0.15, 30_000, "London"),
        (52.5200, 13.4050, 0.12, 25_000, "Berlin"),
        (41.9028, 12.4964, 0.10, 20_000, "Rome"),
        (40.4168, -3.7038, 0.12, 20_000, "Madrid"),
        (55.6761, 12.5683, 0.08, 15_000, "Copenhagen"),
        (60.1699, 24.9384, 0.08, 15_000, "Helsinki"),
        (52.3676, 4.9041, 0.08, 15_000, "Amsterdam"),
        (50.0755, 14.4378, 0.08, 12_000, "Prague"),
        (47.4979, 19.0402, 0.08, 12_000, "Budapest"),
        (38.7223, -9.1393, 0.10, 10_000, "Lisbon"),
        (59.9139, 10.7522, 0.08, 10_000, "Oslo"),
        // Asia
        (35.6762, 139.6503, 0.20, 80_000, "Tokyo"),
        (37.5665, 126.9780, 0.15, 50_000, "Seoul"),
        (31.2304, 121.4737, 0.20, 60_000, "Shanghai"),
        (39.9042, 116.4074, 0.20, 55_000, "Beijing"),
        (22.3193, 114.1694, 0.10, 30_000, "Hong Kong"),
        (1.3521, 103.8198, 0.05, 20_000, "Singapore"),
        (13.7563, 100.5018, 0.15, 20_000, "Bangkok"),
        (28.6139, 77.2090, 0.20, 35_000, "Delhi"),
        (19.0760, 72.8777, 0.15, 30_000, "Mumbai"),
        // Americas
        (40.7128, -74.0060, 0.15, 55_000, "New York"),
        (34.0522, -118.2437, 0.20, 45_000, "Los Angeles"),
        (41.8781, -87.6298, 0.12, 30_000, "Chicago"),
        (-23.5505, -46.6333, 0.20, 45_000, "São Paulo"),
        (19.4326, -99.1332, 0.15, 35_000, "Mexico City"),
        (-34.6037, -58.3816, 0.15, 30_000, "Buenos Aires"),
        (49.2827, -123.1207, 0.10, 16_000, "Vancouver"),
        // Africa & Middle East
        (-33.9249, 18.4241, 0.12, 20_000, "Cape Town"),
        (30.0444, 31.2357, 0.15, 25_000, "Cairo"),
        (25.2048, 55.2708, 0.10, 15_000, "Dubai"),
        (6.5244, 3.3792, 0.15, 20_000, "Lagos"),
        (-1.2921, 36.8219, 0.10, 15_000, "Nairobi"),
        // Oceania
        (-33.8688, 151.2093, 0.15, 25_000, "Sydney"),
        (-36.8485, 174.7633, 0.10, 15_000, "Auckland"),
        // Stockholm — 10,000 dense POIs
        (59.33, 18.07, 0.06, 10_000, "Stockholm"),
    };

    // Bulk insert in batches of 5000 using multi-row VALUES
    const int batchSize = 5000;
    var batch = new List<(string geohash, double lat, double lng, string name)>(batchSize);
    int totalGenerated = 0;

    foreach (var (cLat, cLng, radius, clusterCount, label) in clusters)
    {
        for (int i = 0; i < clusterCount; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double dist = radius * Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble() * 0.9999));
            double lat = Math.Clamp(cLat + dist * Math.Sin(angle), -85, 85);
            double lng = cLng + dist * Math.Cos(angle);
            string name = $"{label}-{i}";
            string hash = Geohash.Encode(lat, lng, 7);

            batch.Add((hash, lat, lng, name));
            totalGenerated++;

            if (batch.Count >= batchSize)
            {
                await FlushBatch(conn, batch);
                batch.Clear();
            }
        }
    }

    if (batch.Count > 0)
    {
        await FlushBatch(conn, batch);
    }

    Console.WriteLine($" done. {totalGenerated:N0} POIs inserted.");
}

static async Task FlushBatch(
    MySqlConnection conn,
    List<(string geohash, double lat, double lng, string name)> batch
)
{
    var sb = new StringBuilder("INSERT INTO pois (geohash, lat, lng, name) VALUES ");
    var parameters = new DynamicParameters();

    for (int i = 0; i < batch.Count; i++)
    {
        if (i > 0)
        {
            sb.Append(',');
        }
        sb.Append($"(@g{i},@la{i},@lo{i},@n{i})");
        parameters.Add($"g{i}", batch[i].geohash);
        parameters.Add($"la{i}", batch[i].lat);
        parameters.Add($"lo{i}", batch[i].lng);
        parameters.Add($"n{i}", batch[i].name);
    }

    await conn.ExecuteAsync(sb.ToString(), parameters);
}

record struct PoiRow(double lat, double lng, string name);
