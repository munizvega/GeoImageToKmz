using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Media;
using OSGeo.OSR;
using TifToKmz.Geometry;

namespace TifToKmzMapEditor.Models;

public sealed class MapTrack
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; }
    public string Name { get; }
    public IReadOnlyList<Point> Wgs84Points { get; }
    public IReadOnlyList<(double X, double Y)> ProjectedPoints { get; private set; } = [];
    public GeoExtent Extent { get; private set; } = GeoExtent.Empty;
    public double LengthKm { get; }
    public int PointsCount => Wgs84Points.Count;

    public bool IsVisible { get; set; } = true;
    public Color TrackColor { get; set; } = Color.FromRgb(255, 69, 0);

    public MapTrack(string filePath, string? targetProjectionWkt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("GPX file not found", filePath);
        }

        FilePath = Path.GetFullPath(filePath);
        Name = Path.GetFileNameWithoutExtension(filePath);

        var (points, length) = ParseGpx(FilePath);
        Wgs84Points = points;
        LengthKm = length;

        UpdateProjectedPoints(targetProjectionWkt);
    }

    public void UpdateProjectedPoints(string? targetProjectionWkt)
    {
        if (Wgs84Points.Count == 0)
        {
            ProjectedPoints = [];
            Extent = GeoExtent.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(targetProjectionWkt))
        {
            ProjectedPoints = Wgs84Points.Select(p => (p.X, p.Y)).ToList();
        }
        else
        {
            try
            {
                using SpatialReference wgs84 = new("");
                wgs84.ImportFromEPSG(4326);
                wgs84.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

                using SpatialReference targetSrs = new(targetProjectionWkt);
                targetSrs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

                using CoordinateTransformation transform = new(wgs84, targetSrs);
                List<(double X, double Y)> projected = new(Wgs84Points.Count);

                foreach (Point pt in Wgs84Points)
                {
                    double[] coords = [pt.X, pt.Y, 0];
                    transform.TransformPoint(coords);
                    projected.Add((coords[0], coords[1]));
                }

                ProjectedPoints = projected;
            }
            catch
            {
                ProjectedPoints = Wgs84Points.Select(p => (p.X, p.Y)).ToList();
            }
        }

        if (ProjectedPoints.Count > 0)
        {
            double minX = ProjectedPoints.Min(p => p.X);
            double maxX = ProjectedPoints.Max(p => p.X);
            double minY = ProjectedPoints.Min(p => p.Y);
            double maxY = ProjectedPoints.Max(p => p.Y);

            if (Math.Abs(maxX - minX) < 1e-6)
            {
                minX -= 10;
                maxX += 10;
            }

            if (Math.Abs(maxY - minY) < 1e-6)
            {
                minY -= 10;
                maxY += 10;
            }

            Extent = new GeoExtent(minX, minY, maxX, maxY);
        }
        else
        {
            Extent = GeoExtent.Empty;
        }
    }

    private static (List<Point> AllPoints, double LengthKm) ParseGpx(string gpxPath)
    {
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using XmlReader reader = XmlReader.Create(gpxPath, xmlReaderSettings);
        XDocument doc = XDocument.Load(reader);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        List<List<Point>> segments = [];
        List<Point> allPoints = [];

        // 1. Process track segments (<trk><trkseg><trkpt .../>)
        var trkElements = doc.Descendants(ns + "trk").ToList();
        foreach (var trk in trkElements)
        {
            var trksegs = trk.Descendants(ns + "trkseg").ToList();
            if (trksegs.Count > 0)
            {
                foreach (var trkseg in trksegs)
                {
                    var segPoints = trkseg.Descendants(ns + "trkpt")
                        .Select(p => new Point
                        {
                            X = double.Parse(p.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture),
                            Y = double.Parse(p.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture)
                        })
                        .ToList();

                    if (segPoints.Count > 0)
                    {
                        segments.Add(segPoints);
                        allPoints.AddRange(segPoints);
                    }
                }
            }
            else
            {
                var trkPoints = trk.Descendants(ns + "trkpt")
                    .Select(p => new Point
                    {
                        X = double.Parse(p.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture),
                        Y = double.Parse(p.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture)
                    })
                    .ToList();

                if (trkPoints.Count > 0)
                {
                    segments.Add(trkPoints);
                    allPoints.AddRange(trkPoints);
                }
            }
        }

        // 2. Process routes (<rte><rtept .../>)
        foreach (var rte in doc.Descendants(ns + "rte"))
        {
            var rtePoints = rte.Descendants(ns + "rtept")
                .Select(p => new Point
                {
                    X = double.Parse(p.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture),
                    Y = double.Parse(p.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture)
                })
                .ToList();

            if (rtePoints.Count > 0)
            {
                segments.Add(rtePoints);
                allPoints.AddRange(rtePoints);
            }
        }

        // 3. Process standalone waypoints (<wpt .../>)
        var waypoints = doc.Descendants(ns + "wpt")
            .Select(p => new Point
            {
                X = double.Parse(p.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture),
                Y = double.Parse(p.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture)
            })
            .ToList();

        allPoints.AddRange(waypoints);

        double totalLength = GeoDistance.CalculateTrackLengthKm(segments);
        return (allPoints, totalLength);
    }
}
