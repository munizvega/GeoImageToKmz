using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.Geometry;

using TifToKmzLib.Geometry;

namespace TifToKmz.ImageTools;

public class ImageUtilitiesLogger : IImageUtilities
{
    private readonly ILogger<ImageUtilitiesLogger> _logger;
    private readonly IImageUtilities _inner;

    public ImageUtilitiesLogger(IImageUtilities inner, ILogger<ImageUtilitiesLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<ImageUtilitiesLogger>.Instance;
    }

    public Size GetImageSize(string inputPath)
    {
        _logger.LogInformation("Getting image size for {InputPath}", inputPath);
        try
        {
            var result = _inner.GetImageSize(inputPath);
            _logger.LogInformation("Successfully got image size for {InputPath}: {Width}x{Height}", inputPath, result.Width, result.Height);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image size for {InputPath}: {Message}", inputPath, ex.Message);
            throw;
        }
    }

    public double GetQualityScore(string inputPath)
    {
        _logger.LogInformation("Getting quality score for {InputPath}", inputPath);
        try
        {
            var result = _inner.GetQualityScore(inputPath);
            _logger.LogInformation("Successfully got quality score for {InputPath}: {QualityScore}", inputPath, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quality score for {InputPath}: {Message}", inputPath, ex.Message);
            throw;
        }
    }
}
