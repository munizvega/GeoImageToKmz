using TifToKmz.ImageTools.Resizers;

namespace TifToKmz.ImageTools.Resizers;

public interface IImageResizer
{
    string Resize(string fullImagePath, string outputDirectory, double scaleFactor, ResizeOptions? options = null, IProgress<double>? progress = null);
}
