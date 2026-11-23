using TifToKmz.Geometry;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz;

namespace TifToKmz.Kmz;

public interface IGeoImageProcessor
{
    void ToKmz(IEnumerable<string> inputFilePaths,
        string outputFilePath,
        KmzProcessOptions? options = null,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default);

    void ToKmzFromSelectedTiles(IEnumerable<string> inputFilePaths,
        string outputFilePath,
        IEnumerable<Rect> selectedTiles,
        KmzProcessOptions? options = null,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default);
}
