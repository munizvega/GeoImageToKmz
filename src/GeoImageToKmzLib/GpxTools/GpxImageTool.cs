using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;
using OSGeo.GDAL;
using OSGeo.OSR;
using TifToKmz.Geometry;
using TifToKmz.ImageTools;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.Resources;

namespace TifToKmz.GpxTools;

public class GpxImageTool : IGpxImageTool
{
    private readonly IImageMerger _mergerForCrop;
    private readonly ConcurrentDictionary<string, (DateTime LastWriteTimeUtc, List<Point> Points)> _gpxPointsCache = new();

    public GpxImageTool(IImageMerger mergerForCrop)
    {
        _mergerForCrop = mergerForCrop ?? throw new ArgumentNullException(nameof(mergerForCrop));
    }

    public IEnumerable<string> GetImagesIntersectingGpx(string gpxPath, IEnumerable<string> imagePaths)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);

        if (!File.Exists(gpxPath))
        {
            throw new FileNotFoundException(Strings.ErrorGpxNotFound, gpxPath);
        }

        List<Point> gpxPoints = ParseGpxPoints(gpxPath).ToList();
        if (gpxPoints.Count == 0)
        {
            yield break;
        }

        double gpxMinLon = gpxPoints.Min(p => p.X);
        double gpxMaxLon = gpxPoints.Max(p => p.X);
        double gpxMinLat = gpxPoints.Min(p => p.Y);
        double gpxMaxLat = gpxPoints.Max(p => p.Y);

        foreach (var imagePath in imagePaths)
        {
            if (ImageIntersectsPoints(imagePath, gpxPoints, gpxMinLon, gpxMinLat, gpxMaxLon, gpxMaxLat))
            {
                yield return imagePath;
            }
        }
    }

    public Rect GetGpxBoundingBox(string gpxPath, double marginInMeters)
    {
        List<Point> points = ParseGpxPoints(gpxPath).ToList();
        if (points.Count == 0)
        {
            throw new InvalidOperationException(Strings.ErrorGpxWithoutPoints);
        }

        // 1. Calculate the center to determine the UTM zone
        double avgLon = points.Average(p => p.X);
        double avgLat = points.Average(p => p.Y);

        int utmZone = (int)Math.Floor((avgLon + 180.0) / 6.0) + 1;
        bool isNorth = avgLat >= 0;

        using SpatialReference wgs84 = new SpatialReference("");
        wgs84.ImportFromEPSG(4326);
        wgs84.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

        using SpatialReference utm = new SpatialReference("");
        utm.SetUTM(utmZone, isNorth ? 1 : 0);
        utm.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

        using CoordinateTransformation toUtm = new CoordinateTransformation(wgs84, utm);
        using CoordinateTransformation fromUtm = new CoordinateTransformation(utm, wgs84);

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        // 2. Project points and find bounding box in meters
        foreach (Point p in points)
        {
            double[] coords = { p.X, p.Y, 0 };
            toUtm.TransformPoint(coords);

            minX = Math.Min(minX, coords[0]);
            minY = Math.Min(minY, coords[1]);
            maxX = Math.Max(maxX, coords[0]);
            maxY = Math.Max(maxY, coords[1]);
        }

        // 3. Apply margin (non-negative)
        double actualMargin = Math.Max(marginInMeters, 0.0);
        minX -= actualMargin;
        minY -= actualMargin;
        maxX += actualMargin;
        maxY += actualMargin;

        // 4. Reproject corners back to WGS84
        double[] topLeftCoords = { minX, maxY, 0 };
        fromUtm.TransformPoint(topLeftCoords);

        double[] bottomRightCoords = { maxX, minY, 0 };
        fromUtm.TransformPoint(bottomRightCoords);

        return new Rect(
            new Point { X = topLeftCoords[0], Y = topLeftCoords[1] },
            new Point { X = bottomRightCoords[0], Y = bottomRightCoords[1] }
        );
    }

    public void CropImagesToRect(IEnumerable<string> imagePaths, Rect cropRect, string outputPath, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentNullException.ThrowIfNull(cropRect);

        MergeOptions options = new MergeOptions
        {
            CropRect = cropRect
        };

        _mergerForCrop.Merge(imagePaths, outputPath, options, progress);
    }

    private List<Point> ParseGpxPoints(string gpxPath)
    {
        if (!File.Exists(gpxPath))
        {
            throw new FileNotFoundException(Strings.ErrorGpxNotFound, gpxPath);
        }

        DateTime lastWriteTime = File.GetLastWriteTimeUtc(gpxPath);
        if (_gpxPointsCache.TryGetValue(gpxPath, out var cached) && cached.LastWriteTimeUtc == lastWriteTime)
        {
            return cached.Points;
        }

        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using XmlReader reader = XmlReader.Create(gpxPath, xmlReaderSettings);
        XDocument doc = XDocument.Load(reader);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        // Buscamos puntos en trkpt (tracks) y wpt (waypoints)
        var points = doc.Descendants(ns + "trkpt")
            .Concat(doc.Descendants(ns + "wpt"))
            .Select(p => new Point
            {
                X = double.Parse(p.Attribute("lon")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                Y = double.Parse(p.Attribute("lat")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToList();

        _gpxPointsCache[gpxPath] = (lastWriteTime, points);
        return points;
    }

    private bool ImageIntersectsPoints(string imagePath, List<Point> points, double gpxMinLon, double gpxMinLat, double gpxMaxLon, double gpxMaxLat)
    {
        using Dataset? ds = GdalHelper.OpenDataset(imagePath, Access.GA_ReadOnly);
        if (ds == null)
        {
            return false;
        }

        double[] geoTransform = new double[6];
        ds.GetGeoTransform(geoTransform);

        string projection = GdalHelper.GetProjectionWkt(ds);
        using SpatialReference imageSrs = !string.IsNullOrWhiteSpace(projection)
            ? new SpatialReference(projection)
            : new SpatialReference("");

        if (string.IsNullOrWhiteSpace(projection))
        {
            imageSrs.ImportFromEPSG(4326);
        }
        imageSrs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

        using SpatialReference wgs84Srs = new SpatialReference("");
        wgs84Srs.ImportFromEPSG(4326);
        wgs84Srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

        int width = ds.RasterXSize;
        int height = ds.RasterYSize;

        // Calcular el bounding box en coordenadas del sistema de la imagen
        double minX = geoTransform[0];
        double maxY = geoTransform[3];
        double maxX = minX + (width * geoTransform[1]) + (height * geoTransform[2]);
        double minY = maxY + (width * geoTransform[4]) + (height * geoTransform[5]);

        // Asegurar orden de min/max
        if (minX > maxX)
        {
            (minX, maxX) = (maxX, minX);
        }

        if (minY > maxY)
        {
            (minY, maxY) = (maxY, minY);
        }

        // Proyectar las 4 esquinas de la imagen a WGS84 para obtener su Bounding Box en WGS84
        using CoordinateTransformation imageToWgs84 = new CoordinateTransformation(imageSrs, wgs84Srs);

        double[][] corners =
        [
            [geoTransform[0], geoTransform[3], 0],
            [geoTransform[0] + (width * geoTransform[1]), geoTransform[3] + (width * geoTransform[4]), 0],
            [geoTransform[0] + (height * geoTransform[2]), geoTransform[3] + (height * geoTransform[5]), 0],
            [geoTransform[0] + (width * geoTransform[1]) + (height * geoTransform[2]), geoTransform[3] + (width * geoTransform[4]) + (height * geoTransform[5]), 0]
        ];

        double imgMinLon = double.MaxValue;
        double imgMaxLon = double.MinValue;
        double imgMinLat = double.MaxValue;
        double imgMaxLat = double.MinValue;

        foreach (double[] corner in corners)
        {
            imageToWgs84.TransformPoint(corner);
            imgMinLon = Math.Min(imgMinLon, corner[0]);
            imgMaxLon = Math.Max(imgMaxLon, corner[0]);
            imgMinLat = Math.Min(imgMinLat, corner[1]);
            imgMaxLat = Math.Max(imgMaxLat, corner[1]);
        }

        // Descarte rápido O(1): Si los rectángulos envolventes no se solapan, la imagen no intersecta
        if (gpxMaxLon < imgMinLon || gpxMinLon > imgMaxLon || gpxMaxLat < imgMinLat || gpxMinLat > imgMaxLat)
        {
            return false;
        }

        using CoordinateTransformation wgs84ToImage = new CoordinateTransformation(wgs84Srs, imageSrs);

        // Verificación detallada: transformar únicamente los puntos que caen dentro del Bounding Box WGS84 de la imagen
        foreach (var point in points)
        {
            if (point.X < imgMinLon || point.X > imgMaxLon || point.Y < imgMinLat || point.Y > imgMaxLat)
            {
                continue;
            }

            double[] p = [point.X, point.Y, 0];
            wgs84ToImage.TransformPoint(p);

            double tx = p[0];
            double ty = p[1];

            if (tx >= minX && tx <= maxX && ty >= minY && ty <= maxY)
            {
                return true;
            }
        }

        return false;
    }
}
