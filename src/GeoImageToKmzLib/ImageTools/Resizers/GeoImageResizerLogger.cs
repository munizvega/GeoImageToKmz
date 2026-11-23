using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.ImageTools.Resizers;

namespace TifToKmz.ImageTools.Resizers;

public class GeoImageResizerLogger : IImageResizer
{
    private readonly ILogger<GeoImageResizerLogger> _logger;
    private readonly IImageResizer _inner;

    public GeoImageResizerLogger(IImageResizer inner, ILogger<GeoImageResizerLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<GeoImageResizerLogger>.Instance;
    }

    public string Resize(string fullImagePath, string outputDirectory, double scaleFactor, ResizeOptions? options = null, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Resizing image {FullImagePath} to {OutputDirectory} with scale factor {ScaleFactor}", fullImagePath, outputDirectory, scaleFactor);
        string result = _inner.Resize(fullImagePath, outputDirectory, scaleFactor, options, progress);
        _logger.LogInformation("Successfully resized image {FullImagePath}", fullImagePath);
        return result;
    }
}
