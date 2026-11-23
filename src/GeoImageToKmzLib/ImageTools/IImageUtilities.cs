using TifToKmz.Geometry;

using TifToKmzLib.Geometry;

namespace TifToKmz.ImageTools;

public interface IImageUtilities
{
    Size GetImageSize(string inputPath);
    double GetQualityScore(string inputPath);
}
