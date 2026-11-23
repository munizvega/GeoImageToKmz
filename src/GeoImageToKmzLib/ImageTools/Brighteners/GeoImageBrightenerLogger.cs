using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TifToKmz.ImageTools.Brighteners;

public class GeoImageBrightenerLogger : IImageBrightener
{
    private readonly ILogger<GeoImageBrightenerLogger> _logger;
    private readonly IImageBrightener _inner;

    public GeoImageBrightenerLogger(IImageBrightener inner, ILogger<GeoImageBrightenerLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<GeoImageBrightenerLogger>.Instance;
    }

    public string Brighten(string fullImagePath, string outputDirectory, BrightenOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new BrightenOptions();
        _logger.LogInformation("Brightening image {FullImagePath} to {OutputDirectory} with gamma {Gamma}, brightness {Brightness}% and contrast {Contrast}",
            fullImagePath, outputDirectory, options.Gamma, options.Brightness, options.Contrast);
        try
        {
            string result = _inner.Brighten(fullImagePath, outputDirectory, options, progress);
            _logger.LogInformation("Successfully brightened image {FullImagePath} into {Result}", fullImagePath, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error brightening image {FullImagePath}: {Message}", fullImagePath, ex.Message);
            throw;
        }
    }
}
