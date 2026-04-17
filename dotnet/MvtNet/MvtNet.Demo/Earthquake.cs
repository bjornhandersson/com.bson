using System.Text.Json;

namespace MvtNet.Demo;

public record Earthquake(
    double Lat,
    double Lng,
    double Magnitude,
    double Depth,
    string Place,
    string Time
);

public static class UsgsEarthquakeFeed
{
    private const string FeedUrl =
        "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_month.geojson";

    public static async Task<List<Earthquake>> FetchAsync()
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(30);

        using var stream = await http.GetStreamAsync(FeedUrl);
        using var doc = await JsonDocument.ParseAsync(stream);

        var quakes = new List<Earthquake>();

        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var coords = feature.GetProperty("geometry").GetProperty("coordinates");

            var magEl = props.GetProperty("mag");
            if (magEl.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var mag = magEl.GetDouble();
            var place =
                props.GetProperty("place").ValueKind == JsonValueKind.Null
                    ? "Unknown"
                    : props.GetProperty("place").GetString()!;
            var timeMs = props.GetProperty("time").GetInt64();
            var time = DateTimeOffset
                .FromUnixTimeMilliseconds(timeMs)
                .ToString("yyyy-MM-dd HH:mm UTC");

            var lng = coords[0].GetDouble();
            var lat = coords[1].GetDouble();
            var depth = coords[2].GetDouble();

            quakes.Add(new Earthquake(lat, lng, mag, depth, place, time));
        }

        return quakes;
    }
}
