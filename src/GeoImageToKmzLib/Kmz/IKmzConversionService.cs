using TifToKmz.ImageTools.GeoTransforming;

namespace TifToKmz.Kmz;

/// <summary>
/// Interface for KMZ conversion operations from TIFF images and GPX tracks.
/// </summary>
public interface IKmzConversionService
{
    void Convert(string gpxPath,
        string imagesPath,
        string outputPath,
        KmzProcessOptions options,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default);
}
