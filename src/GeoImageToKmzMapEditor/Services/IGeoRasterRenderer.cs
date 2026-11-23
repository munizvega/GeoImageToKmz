using TifToKmzMapEditor.Models;

namespace TifToKmzMapEditor.Services;

public record RasterRenderResult(byte[] BgraBuffer, int Width, int Height);

public interface IGeoRasterRenderer
{
    Task<RasterRenderResult?> RenderViewportAsync(
        IReadOnlyList<MapLayer> layers,
        GeoExtent viewportExtent,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default);
}
