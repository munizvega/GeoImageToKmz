using System.Globalization;
using OSGeo.GDAL;
using OSGeo.OSR;
using TifToKmz.ImageTools;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Models;

public sealed class MapLayer
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; }
    public string Name { get; }
    public long FileSizeBytes { get; }
    public string FileSizeFormatted { get; }

    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public int BandCount { get; }
    public DataType DataType { get; }

    public double[] GeoTransform { get; } = new double[6];
    public string ProjectionWkt { get; }
    public string ProjectionName { get; }
    public GeoExtent Extent { get; }
    public double QualityScore { get; }

    public bool IsVisible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public int ZIndex { get; set; }

    public MapLayer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(Strings.ErrorGeoTiffNotFound, filePath);
        }

        FilePath = Path.GetFullPath(filePath);
        Name = Path.GetFileName(filePath);

        FileInfo fileInfo = new(FilePath);
        FileSizeBytes = fileInfo.Length;
        FileSizeFormatted = FormatBytes(FileSizeBytes);

        using Dataset dataset = GdalHelper.OpenDataset(FilePath, Access.GA_ReadOnly)
            ?? throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorCouldNotOpenRaster, filePath));

        PixelWidth = dataset.RasterXSize;
        PixelHeight = dataset.RasterYSize;
        BandCount = dataset.RasterCount;

        if (dataset.RasterCount > 0)
        {
            using Band band = dataset.GetRasterBand(1);
            DataType = band.DataType;
        }

        try
        {
            dataset.GetGeoTransform(GeoTransform);
        }
        catch
        {
            // Ignored if GeoTransform cannot be retrieved
        }

        if (GeoTransform[1] == 0 && GeoTransform[5] == 0)
        {
            GeoTransform[0] = 0;
            GeoTransform[1] = 1;
            GeoTransform[2] = 0;
            GeoTransform[3] = PixelHeight;
            GeoTransform[4] = 0;
            GeoTransform[5] = -1;
        }

        ProjectionWkt = GdalHelper.GetProjectionWkt(dataset);
        ProjectionName = ExtractProjectionName(ProjectionWkt);
        Extent = CalculateExtent(GeoTransform, PixelWidth, PixelHeight);
        QualityScore = GdalHelper.CalculateQualityScore(dataset);
    }

    public (double X, double Y) PixelToGeo(double px, double py)
    {
        double x = GeoTransform[0] + (px * GeoTransform[1]) + (py * GeoTransform[2]);
        double y = GeoTransform[3] + (px * GeoTransform[4]) + (py * GeoTransform[5]);
        return (x, y);
    }

    public (double Px, double Py) GeoToPixel(double x, double y)
    {
        double det = (GeoTransform[1] * GeoTransform[5]) - (GeoTransform[2] * GeoTransform[4]);
        if (Math.Abs(det) < 1e-12)
        {
            return (0, 0);
        }

        double dx = x - GeoTransform[0];
        double dy = y - GeoTransform[3];

        double px = ((dx * GeoTransform[5]) - (dy * GeoTransform[2])) / det;
        double py = ((GeoTransform[1] * dy) - (GeoTransform[4] * dx)) / det;
        return (px, py);
    }

    private static GeoExtent CalculateExtent(double[] gt, int width, int height)
    {
        double x1 = gt[0];
        double y1 = gt[3];
        double x2 = gt[0] + (width * gt[1]) + (height * gt[2]);
        double y2 = gt[3] + (width * gt[4]) + (height * gt[5]);

        double minX = Math.Min(x1, x2);
        double maxX = Math.Max(x1, x2);
        double minY = Math.Min(y1, y2);
        double maxY = Math.Max(y1, y2);

        if (maxX <= minX)
        {
            maxX = minX + Math.Max(1, width);
        }

        if (maxY <= minY)
        {
            maxY = minY + Math.Max(1, height);
        }

        return new GeoExtent(minX, minY, maxX, maxY);
    }

    private static string ExtractProjectionName(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return Strings.UnknownNoGeoreference;
        }

        try
        {
            using SpatialReference srs = new(wkt);
            string? name = srs.GetName();
            if (!string.IsNullOrWhiteSpace(name))
            {
                string? authCode = srs.GetAuthorityCode(null);
                string? authName = srs.GetAuthorityName(null);
                if (!string.IsNullOrWhiteSpace(authCode))
                {
                    return $"{name} ({authName}:{authCode})";
                }
                return name;
            }
        }
        catch
        {
            // Ignored if SRS parse fails
        }

        return Strings.GeoreferencedWkt;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {suffixes[order]}";
    }
}
