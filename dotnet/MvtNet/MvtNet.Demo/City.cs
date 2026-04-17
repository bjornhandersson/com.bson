using System.Globalization;
using System.IO.Compression;

namespace MvtNet.Demo;

public record City(string Name, double Lat, double Lng, long Population, string Country)
{
    private const string GeoNamesUrl = "http://download.geonames.org/export/dump/cities15000.zip";

    private static readonly string CsvPath = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "cities.csv"
    );

    public static async Task<List<City>> LoadAsync(int topN = 10_000)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CsvPath)!);

        if (!File.Exists(CsvPath))
        {
            Console.WriteLine("Downloading city data from GeoNames (one-time)...");
            await DownloadAndConvertAsync(topN);
        }

        return LoadFromCsv(CsvPath);
    }

    public static List<City> LoadFromCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        var cities = new List<City>(lines.Length - 1);

        foreach (var line in lines.AsSpan(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 5)
            {
                continue;
            }

            cities.Add(
                new City(
                    parts[0].Trim(),
                    double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                    double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                    long.Parse(parts[3].Trim(), CultureInfo.InvariantCulture),
                    parts[4].Trim()
                )
            );
        }

        return cities;
    }

    private static async Task DownloadAndConvertAsync(int topN)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        var zipBytes = await http.GetByteArrayAsync(GeoNamesUrl);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entry = archive.Entries.First(e => e.Name.EndsWith(".txt"));
        using var reader = new StreamReader(entry.Open());
        var text = await reader.ReadToEndAsync();

        // GeoNames TSV: 1=name, 4=lat, 5=lng, 8=country, 14=population
        var cities = new List<(string Name, double Lat, double Lng, long Pop, string Country)>();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cols = line.Split('\t');
            if (cols.Length < 15)
            {
                continue;
            }

            if (!long.TryParse(cols[14], out var pop) || pop <= 0)
            {
                continue;
            }

            var name = cols[1].Replace(",", " ");
            cities.Add(
                (
                    name,
                    double.Parse(cols[4], CultureInfo.InvariantCulture),
                    double.Parse(cols[5], CultureInfo.InvariantCulture),
                    pop,
                    cols[8]
                )
            );
        }

        var top = cities.OrderByDescending(c => c.Pop).Take(topN);

        using var writer = new StreamWriter(CsvPath);
        await writer.WriteLineAsync("name,lat,lng,population,country");
        foreach (var c in top)
        {
            await writer.WriteLineAsync(
                $"{c.Name},{c.Lat.ToString("F5", CultureInfo.InvariantCulture)},{c.Lng.ToString("F5", CultureInfo.InvariantCulture)},{c.Pop},{c.Country}"
            );
        }
    }
}
