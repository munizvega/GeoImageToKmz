namespace TifToKmz.ImageTools.GeoTransforming;

public interface IMapReprojector
{
    string Reproject(string fullMapImagePath, string outputDirectory, EpsgCode epsgCode, ReprojectOptions? options = null, IProgress<double>? progress = null);
}
