using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.ImageTools.GeoTransforming;

namespace TifToKmz.ImageTools.GeoTransforming;

public class MapReprojectorLogger : IMapReprojector
{
    private readonly ILogger<MapReprojectorLogger> _logger;
    private readonly IMapReprojector _inner;

    public MapReprojectorLogger(IMapReprojector inner, ILogger<MapReprojectorLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<MapReprojectorLogger>.Instance;
    }

    public string Reproject(string fullMapImagePath, string outputDirectory, EpsgCode epsgCode, ReprojectOptions? options = null, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Reprojecting image {FullMapImagePath} to {OutputDirectory} with target EPSG {TargetCode}", fullMapImagePath, outputDirectory, epsgCode);
        string result = _inner.Reproject(fullMapImagePath, outputDirectory, epsgCode, options, progress);
        _logger.LogInformation("Successfully reprojected image {FullMapImagePath}", fullMapImagePath);
        return result;
    }
}
