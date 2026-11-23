namespace TifToKmz.ImageTools.Splitters;

public interface IImageSplitter
{
    IEnumerable<string> Split(string fullImagePath, string fullOutputDirectory, SplitOptions? options = null, IProgress<double>? progress = null);
}
