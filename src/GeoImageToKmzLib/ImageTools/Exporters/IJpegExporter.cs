namespace TifToKmz.ImageTools.Exporters;

public interface IJpegExporter
{
    string Export(string inputPath, string outputPath, int quality = 100, IProgress<double>? progress = null);
}
