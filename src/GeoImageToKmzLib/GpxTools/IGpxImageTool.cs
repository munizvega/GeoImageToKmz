using TifToKmz.Geometry;

namespace TifToKmz.GpxTools;

public interface IGpxImageTool
{
    /// <summary>
    /// Selecciona las imágenes que contienen al menos un punto del archivo GPX.
    /// </summary>
    /// <param name="gpxPath">Ruta al archivo GPX.</param>
    /// <param name="imagePaths">Colección de rutas a imágenes georeferenciadas.</param>
    /// <returns>Lista de rutas de imágenes que intersectan con el GPX.</returns>
    IEnumerable<string> GetImagesIntersectingGpx(string gpxPath, IEnumerable<string> imagePaths);

    /// <summary>
    /// Calcula el rectángulo más pequeño que contiene a todos los puntos del GPX,
    /// aplicando un margen mínimo en metros.
    /// </summary>
    /// <param name="gpxPath">Ruta al archivo GPX.</param>
    /// <param name="marginInMeters">Margen mínimo en metros.</param>
    /// <returns>Un objeto Rect con las coordenadas WGS84 del área delimitada.</returns>
    Rect GetGpxBoundingBox(string gpxPath, double marginInMeters);

    /// <summary>
    /// Fusiona y recorta una lista de imágenes basándose en un rectángulo de coordenadas.
    /// </summary>
    /// <param name="imagePaths">Colección de rutas a imágenes georeferenciadas.</param>
    /// <param name="cropRect">Rectángulo de recorte en coordenadas WGS84.</param>
    /// <param name="outputPath">Ruta de salida para la imagen resultante.</param>
    void CropImagesToRect(IEnumerable<string> imagePaths, Rect cropRect, string outputPath, IProgress<double>? progress = null);
}
