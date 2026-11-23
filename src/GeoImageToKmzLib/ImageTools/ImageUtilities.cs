using OSGeo.GDAL;

using TifToKmz.Geometry;

using TifToKmzLib.Geometry;

namespace TifToKmz.ImageTools;

public class ImageUtilities : IImageUtilities
{
    public Size GetImageSize(string inputPath)
    {
        using Dataset sourceDataset = GdalHelper.OpenDataset(inputPath, Access.GA_ReadOnly) ?? throw new InvalidOperationException($"Could not open: {inputPath}");
        return new Size { Height = sourceDataset.RasterYSize, Width = sourceDataset.RasterXSize };
    }

    public double GetQualityScore(string inputPath)
    {
        return GdalHelper.CalculateQualityScore(inputPath);
    }
}
