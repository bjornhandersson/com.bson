namespace MvtNet.Demo;

public record FlightRoute(
    string From,
    string To,
    (double Lat, double Lng)[] Path,
    double DistanceKm
);

public static class FlightRouteBuilder
{
    public static List<FlightRoute> BuildRoutes(
        List<City> cities,
        int topN = 40,
        int connections = 3
    )
    {
        var top = cities.OrderByDescending(c => c.Population).Take(topN).ToList();
        var routes = new List<FlightRoute>();
        var seen = new HashSet<string>();

        foreach (var city in top)
        {
            var nearest = top.Where(c => c != city)
                .OrderBy(c => HaversineKm(city.Lat, city.Lng, c.Lat, c.Lng))
                .Take(connections);

            foreach (var other in nearest)
            {
                var key =
                    string.Compare(city.Name, other.Name, StringComparison.Ordinal) < 0
                        ? $"{city.Name}-{other.Name}"
                        : $"{other.Name}-{city.Name}";
                if (!seen.Add(key))
                {
                    continue;
                }

                var dist = HaversineKm(city.Lat, city.Lng, other.Lat, other.Lng);
                var path = GreatCircleArc(city.Lat, city.Lng, other.Lat, other.Lng);
                routes.Add(new FlightRoute(city.Name, other.Name, path, dist));
            }
        }

        return routes;
    }

    private static (double Lat, double Lng)[] GreatCircleArc(
        double lat1,
        double lng1,
        double lat2,
        double lng2,
        int segments = 40
    )
    {
        double phi1 = lat1 * Math.PI / 180,
            lam1 = lng1 * Math.PI / 180;
        double phi2 = lat2 * Math.PI / 180,
            lam2 = lng2 * Math.PI / 180;

        double d =
            2
            * Math.Asin(
                Math.Sqrt(
                    Math.Pow(Math.Sin((phi2 - phi1) / 2), 2)
                        + Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin((lam2 - lam1) / 2), 2)
                )
            );

        if (d < 1e-10)
        {
            return [(lat1, lng1), (lat2, lng2)];
        }

        var points = new (double, double)[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double f = (double)i / segments;
            double a = Math.Sin((1 - f) * d) / Math.Sin(d);
            double b = Math.Sin(f * d) / Math.Sin(d);
            double x = a * Math.Cos(phi1) * Math.Cos(lam1) + b * Math.Cos(phi2) * Math.Cos(lam2);
            double y = a * Math.Cos(phi1) * Math.Sin(lam1) + b * Math.Cos(phi2) * Math.Sin(lam2);
            double z = a * Math.Sin(phi1) + b * Math.Sin(phi2);
            points[i] = (
                Math.Atan2(z, Math.Sqrt(x * x + y * y)) * 180 / Math.PI,
                Math.Atan2(y, x) * 180 / Math.PI
            );
        }

        return points;
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLng = (lng2 - lng1) * Math.PI / 180;
        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180)
                * Math.Cos(lat2 * Math.PI / 180)
                * Math.Sin(dLng / 2)
                * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
