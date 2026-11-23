using TifToKmz.ImageTools.Mergers;

namespace TifToKmz.ImageTools.Mergers;

public interface IImageMerger
{
    void Merge(IEnumerable<string> fullImagePaths, string fullOutputFileName, MergeOptions? options = null, IProgress<double>? progress = null);
}
