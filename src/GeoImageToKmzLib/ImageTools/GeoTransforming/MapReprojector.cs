using System.Diagnostics.CodeAnalysis;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace TifToKmz.ImageTools.GeoTransforming;

public class MapReprojector : IMapReprojector
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "GDAL operations can throw diverse exceptions")]
    public string Reproject(string fullMapImagePath, string outputDirectory, EpsgCode epsgCode, ReprojectOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new ReprojectOptions();
        int targetCode = (int)epsgCode;
        ValidateReprojectParameters(fullMapImagePath, outputDirectory, targetCode);

        Directory.CreateDirectory(outputDirectory);

        using Dataset sourceDataset = GdalHelper.OpenDataset(fullMapImagePath, Access.GA_ReadOnly)
                                      ?? throw new InvalidOperationException($"Could not open map file: {fullMapImagePath}");

        string sourceWellKnownText = GdalHelper.GetProjectionWkt(sourceDataset);
        if (string.IsNullOrWhiteSpace(sourceWellKnownText))
        {
            double[] gt = new double[6];
            try
            {
                sourceDataset.GetGeoTransform(gt);
            }
            catch
            {
                // Ignored
            }

            if (Math.Abs(gt[0]) <= 180 && Math.Abs(gt[3]) <= 90)
            {
                sourceWellKnownText = GetWellKnownText(4326);
            }
            else
            {
                throw new InvalidOperationException($"The image has no source coordinate system: {fullMapImagePath}");
            }
        }

        string targetWellKnownText = GetWellKnownText(targetCode);

        string fullMapImagePathWithoutExtension = Path.GetFileNameWithoutExtension(fullMapImagePath);
        string outputPath = Path.Combine(outputDirectory, $"{fullMapImagePathWithoutExtension}_{epsgCode}.tif");

        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string[] warpOptionsList = options.ToParameters(sourceWellKnownText, targetWellKnownText);

        using GDALWarpAppOptions gdalWarpOptions = new GDALWarpAppOptions(warpOptionsList);
        Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
        using Dataset warpedDataset = Gdal.Warp(outputPath, [sourceDataset], gdalWarpOptions, progressCallback, null) ??
                                      throw new InvalidOperationException($"Could not reproject from {sourceWellKnownText} to {targetWellKnownText} ({outputPath})");

        warpedDataset.FlushCache();
        return outputPath;
    }

    private void ValidateReprojectParameters(string fullMapImagePath, string outputDirectory, int targetCode)
    {
        if (string.IsNullOrWhiteSpace(fullMapImagePath))
        {
            throw new ArgumentException("Map image path cannot be empty.", nameof(fullMapImagePath));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        }

        if (targetCode <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetCode), "Target EPSG code must be positive.");
        }
    }

    private string GetWellKnownText(int epsgCode)
    {
        using SpatialReference targetSrs = new SpatialReference(string.Empty);
        targetSrs.ImportFromEPSG(epsgCode);
        targetSrs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
        targetSrs.ExportToWkt(out string wellKnownText, null);
        return wellKnownText;
    }
}
