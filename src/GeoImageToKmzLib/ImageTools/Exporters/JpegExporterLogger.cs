using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TifToKmz.ImageTools.Exporters;

public class JpegExporterLogger : IJpegExporter
{
    private readonly ILogger<JpegExporterLogger> _logger;
    private readonly IJpegExporter _inner;

    public JpegExporterLogger(IJpegExporter inner, ILogger<JpegExporterLogger>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? NullLogger<JpegExporterLogger>.Instance;
    }

    public string Export(string inputPath, string outputPath, int quality = 90, IProgress<double>? progress = null)
    {
        _logger.LogInformation("Exporting image {InputPath} to JPEG at {OutputPath} with quality {Quality}", inputPath, outputPath, quality);
        try
        {
            var result = _inner.Export(inputPath, outputPath, quality, progress);
            _logger.LogInformation("Successfully exported JPEG to {OutputPath}", outputPath);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting JPEG from {InputPath}: {Message}", inputPath, ex.Message);
            throw;
        }
    }
}
