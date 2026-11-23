using OSGeo.GDAL;

namespace TifToKmz.ImageTools.Resizers;

public class GeoImageResizer : IImageResizer
{
    public string Resize(string fullImagePath, string outputDirectory, double scaleFactor, ResizeOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new ResizeOptions();
        ValidateResizeParameters(fullImagePath, outputDirectory, scaleFactor);

        Directory.CreateDirectory(outputDirectory);

        using Dataset sourceDataset = GdalHelper.OpenDataset(fullImagePath, Access.GA_ReadOnly)
                                      ?? throw new InvalidOperationException($"Could not open image: {fullImagePath}");

        string[] translateOptionsList = options.ToParameters(scaleFactor);

        string extension = Path.GetExtension(fullImagePath);
        string outputImagePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(fullImagePath) + $"_resized{extension}");

        if (File.Exists(outputImagePath))
        {
            using Dataset? existingDataset = GdalHelper.OpenDataset(outputImagePath, Access.GA_ReadOnly);
            if (existingDataset != null)
            {
                int expectedWidth = (int)Math.Round(sourceDataset.RasterXSize * scaleFactor);
                int expectedHeight = (int)Math.Round(sourceDataset.RasterYSize * scaleFactor);

                // Allow some tolerance in size due to rounding in GDAL
                if (Math.Abs(existingDataset.RasterXSize - expectedWidth) <= 1 &&
                    Math.Abs(existingDataset.RasterYSize - expectedHeight) <= 1)
                {
                    return outputImagePath;
                }
            }
        }

        using GDALTranslateOptions translateOptions = new GDALTranslateOptions(translateOptionsList);
        Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
        using Dataset dstDataset = Gdal.wrapper_GDALTranslate(outputImagePath, sourceDataset, translateOptions, progressCallback, null)
                                   ?? throw new InvalidOperationException($"Could not resize image using GDAL Translate: {outputImagePath}");

        return outputImagePath;
    }

    private void ValidateResizeParameters(string fullImagePath, string outputDirectory, double scaleFactor)
    {
        if (string.IsNullOrWhiteSpace(fullImagePath))
        {
            throw new ArgumentException("Image path cannot be empty.", nameof(fullImagePath));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        }

        if (scaleFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scaleFactor), "Scale factor must be greater than zero.");
        }
    }
}
