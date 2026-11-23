using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.ImageTools.Splitters;

namespace TifToKmz.ImageTools.Splitters;

public class GeoImageSplitterLogger : IImageSplitter
{
    private readonly ILogger<GeoImageSplitterLogger> _logger;
    private readonly IImageSplitter _inner;

    public GeoImageSplitterLogger(IImageSplitter inner, ILogger<GeoImageSplitterLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<GeoImageSplitterLogger>.Instance;
    }

    public IEnumerable<string> Split(string fullImagePath, string fullOutputDirectory, SplitOptions? options = null, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Splitting image {FullImagePath} into {FullOutputDirectory}", fullImagePath, fullOutputDirectory);
        return _inner.Split(fullImagePath, fullOutputDirectory, options, progress);
    }
}
