using OSGeo.GDAL;

namespace TifToKmz.ImageTools.Brighteners;

public class GeoImageBrightener : IImageBrightener
{
    public string Brighten(string fullImagePath, string outputDirectory, BrightenOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new BrightenOptions();
        ValidateBrightenParameters(fullImagePath, outputDirectory);

        if (options.IsNeutral)
        {
            return fullImagePath;
        }

        using Dataset sourceDataset = GdalHelper.OpenDataset(fullImagePath, Access.GA_ReadOnly)
                                      ?? throw new InvalidOperationException($"Could not open image: {fullImagePath}");

        string[] translateOptionsList = options.ToParameters(GetColourBands(sourceDataset));
        if (translateOptionsList.Length == 0)
        {
            // Empty or single valued tile: there is nothing to stretch.
            return fullImagePath;
        }

        Directory.CreateDirectory(outputDirectory);
        string extension = Path.GetExtension(fullImagePath);
        string outputImagePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(fullImagePath) + $"_bright{extension}");

        using GDALTranslateOptions translateOptions = new GDALTranslateOptions(translateOptionsList);
        Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
        using Dataset destinationDataset = Gdal.wrapper_GDALTranslate(outputImagePath, sourceDataset, translateOptions, progressCallback, null)
                                           ?? throw new InvalidOperationException($"Failed to brighten image: {outputImagePath}");

        return outputImagePath;
    }

    /// <summary>Returns the bands carrying colour together with the range their values live in.</summary>
    private static List<(int Index, double Minimum, double Maximum)> GetColourBands(Dataset dataset)
    {
        List<(int Index, double Minimum, double Maximum)> bands = [];
        for (int i = 1; i <= dataset.RasterCount; i++)
        {
            using Band band = dataset.GetRasterBand(i);
            ColorInterp colorInterp = band.GetColorInterpretation();
            bool isAlpha = colorInterp == ColorInterp.GCI_AlphaBand;

            // Transparency must keep its original values, otherwise the tile edges
            // would become opaque. Bands beyond the third one are treated as alpha
            // when the image does not declare the interpretation.
            if (isAlpha || (dataset.RasterCount > 3 && i > 3))
            {
                continue;
            }

            (double minimum, double maximum) = GetRange(band);
            if (maximum > minimum)
            {
                bands.Add((i, minimum, maximum));
            }
        }

        return bands;
    }

    private static (double Minimum, double Maximum) GetRange(Band band)
    {
        if (band.DataType == DataType.GDT_Byte)
        {
            return (0, 255);
        }

        double[] minMax = new double[2];
        band.ComputeRasterMinMax(minMax, 1);
        return (minMax[0], minMax[1]);
    }

    private static void ValidateBrightenParameters(string fullImagePath, string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(fullImagePath))
        {
            throw new ArgumentException("Image path cannot be empty.", nameof(fullImagePath));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        }
    }
}
