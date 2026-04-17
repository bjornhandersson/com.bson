using System.Globalization;

namespace MvtNet.Demo;

public record City(string Name, double Lat, double Lng, long Population, string Country)
{
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
}
