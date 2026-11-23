using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.ImageTools.Mergers;

namespace TifToKmz.ImageTools.Mergers;

public class GeoImageMergerLogger : IImageMerger
{
    private readonly ILogger<GeoImageMergerLogger> _logger;
    private readonly IImageMerger _inner;

    public GeoImageMergerLogger(IImageMerger inner, ILogger<GeoImageMergerLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<GeoImageMergerLogger>.Instance;
    }

    public void Merge(IEnumerable<string> fullImagePaths, string fullOutputFileName, MergeOptions? options = null, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Merging images into {FullOutputFileName}", fullOutputFileName);
        _inner.Merge(fullImagePaths, fullOutputFileName, options, progress);
        _logger.LogInformation("Successfully merged images into {FullOutputFileName}", fullOutputFileName);
    }
}
