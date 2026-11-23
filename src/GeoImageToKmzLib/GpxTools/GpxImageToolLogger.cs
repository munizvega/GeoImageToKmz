using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TifToKmz.Geometry;

namespace TifToKmz.GpxTools;

public class GpxImageToolLogger : IGpxImageTool
{
    private readonly IGpxImageTool _inner;
    private readonly ILogger<GpxImageToolLogger> _logger;

    public GpxImageToolLogger(IGpxImageTool inner, ILogger<GpxImageToolLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<GpxImageToolLogger>.Instance;
    }

    public IEnumerable<string> GetImagesIntersectingGpx(string gpxPath, IEnumerable<string> imagePaths)
    {
        _logger.LogInformation("Filtering images intersecting GPX: {GpxPath}", gpxPath);
        var images = imagePaths.ToList();

        try
        {
            var result = _inner.GetImagesIntersectingGpx(gpxPath, images).ToList();
            _logger.LogInformation("Found {ResultCount} images intersecting GPX out of {TotalCount} provided.", result.Count, images.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering images with GPX {GpxPath}: {Message}", gpxPath, ex.Message);
            throw;
        }
    }

    public Rect GetGpxBoundingBox(string gpxPath, double marginInMeters)
    {
        _logger.LogInformation("Calculating bounding box for GPX: {GpxPath} with margin: {Margin}m", gpxPath, marginInMeters);

        try
        {
            var result = _inner.GetGpxBoundingBox(gpxPath, marginInMeters);
            _logger.LogInformation("Bounding box calculated successfully: TopLeft({TopLeftY}, {TopLeftX}), BottomRight({BottomRightY}, {BottomRightX})",
                result.TopLeft.Y, result.TopLeft.X, result.BottomRight.Y, result.BottomRight.X);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating bounding box for GPX {GpxPath}: {Message}", gpxPath, ex.Message);
            throw;
        }
    }

    public void CropImagesToRect(IEnumerable<string> imagePaths, Rect cropRect, string outputPath, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Cropping images to rectangle. Output: {OutputPath}", outputPath);

        try
        {
            _inner.CropImagesToRect(imagePaths, cropRect, outputPath, progress);
            _logger.LogInformation("Images cropped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cropping images: {Message}", ex.Message);
            throw;
        }
    }
}
