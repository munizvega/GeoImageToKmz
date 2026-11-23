namespace TifToKmz.Geometry;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                   (Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public static double CalculatePolylineLengthKm(IReadOnlyList<Point> points)
    {
        if (points == null || points.Count < 2)
        {
            return 0.0;
        }

        double total = 0.0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            total += HaversineKm(points[i].Y, points[i].X, points[i + 1].Y, points[i + 1].X);
        }

        return total;
    }

    public static double CalculateTrackLengthKm(IEnumerable<IReadOnlyList<Point>> segments)
    {
        if (segments == null)
        {
            return 0.0;
        }

        double total = 0.0;
        foreach (var segment in segments)
        {
            total += CalculatePolylineLengthKm(segment);
        }

        return total;
    }
}
