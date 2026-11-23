namespace TifToKmz.ImageTools.Brighteners;

public interface IImageBrightener
{
    /// <summary>
    /// Lightens the image so it stays readable on a GPS screen under direct sunlight and returns the
    /// path of the resulting file, or the original path when no adjustment is required.
    /// </summary>
    string Brighten(string fullImagePath, string outputDirectory, BrightenOptions? options = null, IProgress<double>? progress = null);
}
