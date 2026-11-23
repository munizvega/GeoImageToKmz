using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace TifToKmz.ImageTools;

[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "GDAL operations and directory scanning can throw diverse exceptions")]
public static class GdalHelper
{
    public static readonly string[] SupportedExtensions =
    [
        ".tif", ".tiff", ".jp2", ".j2k", ".jpc", ".j2c",
        ".jpg", ".jpeg", ".png",
        ".ecw", ".sid", ".vrt", ".img", ".mbtiles",
        ".webp", ".bmp", ".gif"
    ];

    public static readonly string[] SupportedSearchPatterns =
    [
        "*.tif", "*.tiff", "*.jp2", "*.j2k", "*.jpc", "*.j2c",
        "*.jpg", "*.jpeg", "*.png",
        "*.ecw", "*.sid", "*.vrt", "*.img", "*.mbtiles",
        "*.webp", "*.bmp", "*.gif"
    ];

    public static void ConfigureGdalPerformance()
    {
        try
        {
            Gdal.SetConfigOption("GDAL_CACHEMAX", "512");
            Gdal.SetConfigOption("GDAL_NUM_THREADS", "ALL_CPUS");
            Gdal.SetConfigOption("OPENJPEG_NUM_THREADS", "ALL_CPUS");
            Gdal.SetConfigOption("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR");
            Gdal.SetConfigOption("VSI_CACHE", "TRUE");
            Gdal.SetConfigOption("VSI_CACHE_SIZE", "67108864");
            Gdal.SetConfigOption("GDAL_SWATH_SIZE", "67108864");
        }
        catch
        {
            // Ignore config errors if GDAL not yet initialized
        }
    }

    public static bool IsSupportedRasterFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string ext = Path.GetExtension(filePath);
        return SupportedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<string> FindSupportedRasterFiles(string directoryPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Enumerable.Empty<string>();
        }

        HashSet<string> results = new(StringComparer.OrdinalIgnoreCase);
        foreach (string pattern in SupportedSearchPatterns)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(directoryPath, pattern, searchOption))
                {
                    results.Add(file);
                }
            }
            catch
            {
                // Ignore permissions/directory access errors
            }
        }

        return results;
    }

    public static Dataset? OpenDataset(string filePath, Access access = Access.GA_ReadOnly)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return Gdal.Open(filePath, access);
    }

    public static double CalculateQualityScore(Dataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        double[] gt = new double[6];
        try
        {
            dataset.GetGeoTransform(gt);
        }
        catch
        {
            gt[1] = 1.0;
            gt[5] = -1.0;
        }

        double pixelWidth = Math.Sqrt((gt[1] * gt[1]) + (gt[4] * gt[4]));
        double pixelHeight = Math.Sqrt((gt[2] * gt[2]) + (gt[5] * gt[5]));

        if (pixelWidth <= 0 || double.IsNaN(pixelWidth) || double.IsInfinity(pixelWidth))
        {
            pixelWidth = 1.0;
        }

        if (pixelHeight <= 0 || double.IsNaN(pixelHeight) || double.IsInfinity(pixelHeight))
        {
            pixelHeight = 1.0;
        }

        bool isGeographic = false;
        try
        {
            string proj = dataset.GetProjectionRef() ?? dataset.GetProjection() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(proj))
            {
                using SpatialReference srs = new(proj);
                isGeographic = srs.IsGeographic() != 0;
            }
            else
            {
                using SpatialReference? dsSrs = dataset.GetSpatialRef();
                if (dsSrs != null)
                {
                    isGeographic = dsSrs.IsGeographic() != 0;
                }
            }
        }
        catch
        {
            if (Math.Abs(gt[0]) <= 180 && Math.Abs(gt[3]) <= 90 && pixelWidth < 1.0 && pixelHeight < 1.0)
            {
                isGeographic = true;
            }
        }

        double metersPerXUnit = isGeographic ? 111320.0 : 1.0;
        double metersPerYUnit = isGeographic ? 111320.0 : 1.0;

        double pixelWidthMeters = pixelWidth * metersPerXUnit;
        double pixelHeightMeters = pixelHeight * metersPerYUnit;
        double pixelAreaMetersSq = pixelWidthMeters * pixelHeightMeters;

        if (pixelAreaMetersSq <= 1e-12 || double.IsNaN(pixelAreaMetersSq) || double.IsInfinity(pixelAreaMetersSq))
        {
            pixelAreaMetersSq = 1e-12;
        }

        // Higher quality = smaller ground pixel area -> higher quality score
        return 1.0 / pixelAreaMetersSq;
    }

    public static double CalculateQualityScore(string filePath)
    {
        using Dataset? dataset = OpenDataset(filePath, Access.GA_ReadOnly);
        if (dataset == null)
        {
            return 0.0;
        }

        return CalculateQualityScore(dataset);
    }

    public static string GetProjectionWkt(Dataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        string wkt = dataset.GetProjectionRef() ?? dataset.GetProjection() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(wkt))
        {
            return wkt;
        }

        try
        {
            using SpatialReference? srs = dataset.GetSpatialRef();
            if (srs != null)
            {
                srs.ExportToWkt(out string exportedWkt, null);
                if (!string.IsNullOrWhiteSpace(exportedWkt))
                {
                    return exportedWkt;
                }
            }
        }
        catch
        {
            // Ignore SRS export failure
        }

        return string.Empty;
    }
}
