using System.Globalization;

using OSGeo.GDAL;

namespace TifToKmz.ImageTools.Exporters;

public class JpegExporter : IJpegExporter
{
    public string Export(string inputPath, string outputPath, int quality = 90, IProgress<double>? progress = null)
    {
        ValidateParameters(inputPath, outputPath, quality);

        using Dataset sourceDataset = GdalHelper.OpenDataset(inputPath, Access.GA_ReadOnly)
                                      ?? throw new InvalidOperationException($"Could not open input image: {inputPath}");

        List<string> optionsList =
        [
            "-of", "JPEG",
            "-co", $"QUALITY={quality}"
        ];

        // Analyze bands to handle potential color issues
        int bandCount = sourceDataset.RasterCount;

        int red = 0, green = 0, blue = 0;
        for (int i = 1; i <= bandCount; i++)
        {
            using Band band = sourceDataset.GetRasterBand(i);
            ColorInterp interpretation = band.GetColorInterpretation();

            switch (interpretation)
            {
                case ColorInterp.GCI_RedBand:
                    red = i;
                    break;
                case ColorInterp.GCI_GreenBand:
                    green = i;
                    break;
                case ColorInterp.GCI_BlueBand:
                    blue = i;
                    break;
            }
        }

        if (red > 0 && green > 0 && blue > 0)
        {
            optionsList.AddRange([
                "-b",
                red.ToString(CultureInfo.InvariantCulture),
                "-b",
                green.ToString(CultureInfo.InvariantCulture),
                "-b",
                blue.ToString(CultureInfo.InvariantCulture)
            ]);
        }
        else if (bandCount >= 3)
        {
            // If no explicit interpretation but 3+ bands, assume first three are RGB
            optionsList.AddRange(["-b", "1", "-b", "2", "-b", "3"]);
        }

        // Ensure proper color scaling if it's not a standard 8-bit image, mapping the full
        // dynamic range to 0-255 for JPEG. We use -scale without parameters to automatically map
        // from source min/max to 0-255.
        // Note: We avoid -ot Byte because Jpeg driver usually handles the conversion if -scale is provided.
        // We only add -scale if the bands have valid data to avoid "Failed to compute min/max" errors.
        // 8-bit sources are copied as they are: an automatic per-tile stretch would undo the
        // brightening step and make adjacent tiles look different from each other.
        if (RequiresScaling(sourceDataset) && CanComputeStatistics(sourceDataset))
        {
            optionsList.Add("-scale");
        }

        string[] options = optionsList.ToArray();

        using GDALTranslateOptions translateOptions = new GDALTranslateOptions(options);

        Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
        using Dataset destinationDataset = Gdal.wrapper_GDALTranslate(outputPath, sourceDataset, translateOptions, progressCallback, null)
                                           ?? throw new InvalidOperationException($"Failed to export JPEG to: {outputPath}");

        return outputPath;
    }

    private void ValidateParameters(string inputPath, string outputPath, int quality)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Input path cannot be null or whitespace.", nameof(inputPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or whitespace.", nameof(outputPath));
        }

        if (quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
        }
    }

    private static bool RequiresScaling(Dataset dataset)
    {
        for (int i = 1; i <= dataset.RasterCount; i++)
        {
            using Band band = dataset.GetRasterBand(i);
            if (band.DataType != DataType.GDT_Byte)
            {
                return true;
            }
        }

        return false;
    }

    private bool CanComputeStatistics(Dataset dataset)
    {
        for (int i = 1; i <= dataset.RasterCount; i++)
        {
            using Band band = dataset.GetRasterBand(i);
            try
            {
                // If statistics are already available in metadata, avoid recomputing
                string? cachedMin = band.GetMetadataItem("STATISTICS_MINIMUM", null);
                if (!string.IsNullOrEmpty(cachedMin))
                {
                    continue;
                }

                double[] minMax = new double[2];
                // Use approx_ok = true to avoid scanning 100% of pixels when computing statistics
                band.ComputeStatistics(true, out minMax[0], out minMax[1], out _, out _, null, null);

                // If min and max are the same and it's 0, it might be an empty tile
                // but ComputeStatistics should have succeeded.
                // The error "Failed to compute min/max" usually happens when all pixels are NoData or invalid.
            }
#pragma warning disable CA1031
            catch
#pragma warning restore CA1031
            {
                return false;
            }
        }

        return true;
    }
}
