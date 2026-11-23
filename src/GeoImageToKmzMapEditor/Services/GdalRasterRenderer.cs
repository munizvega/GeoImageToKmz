using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using OSGeo.GDAL;
using TifToKmz.ImageTools;
using TifToKmzMapEditor.Models;

namespace TifToKmzMapEditor.Services;

public sealed class GdalRasterRenderer : IGeoRasterRenderer, IDisposable
{
    private readonly ConcurrentDictionary<string, CachedDatasetEntry> _datasetCache = new(StringComparer.OrdinalIgnoreCase);

    private sealed class CachedDatasetEntry : IDisposable
    {
        public Dataset Dataset { get; }
        public object SyncLock { get; } = new();

        public CachedDatasetEntry(Dataset dataset)
        {
            Dataset = dataset;
        }

        public void Dispose()
        {
            lock (SyncLock)
            {
                Dataset.Dispose();
            }
        }
    }

    public Task<RasterRenderResult?> RenderViewportAsync(
        IReadOnlyList<MapLayer> layers,
        GeoExtent viewportExtent,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (pixelWidth <= 0 || pixelHeight <= 0 || viewportExtent.IsEmpty || layers.Count == 0 || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            byte[] finalBgraBuffer = new byte[pixelWidth * pixelHeight * 4];
            bool anyRendered = false;

            // Render visible layers sorted by ZIndex
            var visibleLayers = layers.Where(l => l.IsVisible && l.Opacity > 0).OrderBy(l => l.ZIndex).ToList();

            foreach (MapLayer layer in visibleLayers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!layer.Extent.Intersects(viewportExtent))
                {
                    continue;
                }

                bool rendered = RenderLayerOntoBuffer(layer, viewportExtent, pixelWidth, pixelHeight, finalBgraBuffer, cancellationToken);
                if (rendered)
                {
                    anyRendered = true;
                }
            }

            if (!anyRendered || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            return new RasterRenderResult(finalBgraBuffer, pixelWidth, pixelHeight);
        }, cancellationToken);
    }

    private CachedDatasetEntry? GetOrCreateDataset(string filePath)
    {
        if (_datasetCache.TryGetValue(filePath, out var entry))
        {
            return entry;
        }

        try
        {
            var ds = GdalHelper.OpenDataset(filePath, Access.GA_ReadOnly);
            if (ds == null)
            {
                return null;
            }

            var newEntry = new CachedDatasetEntry(ds);
            return _datasetCache.GetOrAdd(filePath, newEntry);
        }
        catch
        {
            return null;
        }
    }

    private static void ReadBandAsByte(
        Band band,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        byte[] destination,
        int destinationWidth,
        int destinationHeight)
    {
        int count = destinationWidth * destinationHeight;
        if (band.DataType == DataType.GDT_Byte)
        {
            band.ReadRaster(sourceX, sourceY, sourceWidth, sourceHeight, destination, destinationWidth, destinationHeight, 0, 0);
            return;
        }

        float[] values = ArrayPool<float>.Shared.Rent(count);
        try
        {
            band.ReadRaster(sourceX, sourceY, sourceWidth, sourceHeight, values, destinationWidth, destinationHeight, 0, 0);

            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float value = values[i];
                if (!float.IsNaN(value) && !float.IsInfinity(value))
                {
                    if (value < minimum)
                    {
                        minimum = value;
                    }
                    if (value > maximum)
                    {
                        maximum = value;
                    }
                }
            }

            if (minimum == float.MaxValue)
            {
                Array.Clear(destination, 0, count);
                return;
            }

            if (maximum <= minimum)
            {
                byte singleVal = (byte)Math.Clamp((int)Math.Round(minimum), 0, 255);
                Array.Fill(destination, singleVal, 0, count);
                return;
            }

            float scale = 255.0f / (maximum - minimum);
            for (int i = 0; i < count; i++)
            {
                float value = values[i];
                destination[i] = float.IsNaN(value) || float.IsInfinity(value)
                    ? (byte)0
                    : (byte)Math.Clamp((int)Math.Round((value - minimum) * scale), 0, 255);
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(values);
        }
    }

    private bool RenderLayerOntoBuffer(
        MapLayer layer,
        GeoExtent viewport,
        int screenW,
        int screenH,
        byte[] bgraBuffer,
        CancellationToken cancellationToken)
    {
        GeoExtent inter = layer.Extent.Intersection(viewport);
        if (inter.IsEmpty)
        {
            return false;
        }

        // Screen destination rect
        int dstX = (int)Math.Floor(((inter.MinX - viewport.MinX) / viewport.Width) * screenW);
        int dstRight = (int)Math.Ceiling(((inter.MaxX - viewport.MinX) / viewport.Width) * screenW);
        int dstY = (int)Math.Floor(((viewport.MaxY - inter.MaxY) / viewport.Height) * screenH);
        int dstBottom = (int)Math.Ceiling(((viewport.MaxY - inter.MinY) / viewport.Height) * screenH);

        dstX = Math.Clamp(dstX, 0, screenW - 1);
        dstY = Math.Clamp(dstY, 0, screenH - 1);
        int dstW = Math.Clamp(dstRight - dstX, 1, screenW - dstX);
        int dstH = Math.Clamp(dstBottom - dstY, 1, screenH - dstY);

        if (dstW <= 0 || dstH <= 0)
        {
            return false;
        }

        // Raster source rect
        var (pX1, pY1) = layer.GeoToPixel(inter.MinX, inter.MaxY);
        var (pX2, pY2) = layer.GeoToPixel(inter.MaxX, inter.MinY);

        int srcX = (int)Math.Floor(Math.Min(pX1, pX2));
        int srcY = (int)Math.Floor(Math.Min(pY1, pY2));
        int srcRight = (int)Math.Ceiling(Math.Max(pX1, pX2));
        int srcBottom = (int)Math.Ceiling(Math.Max(pY1, pY2));

        srcX = Math.Clamp(srcX, 0, layer.PixelWidth - 1);
        srcY = Math.Clamp(srcY, 0, layer.PixelHeight - 1);
        int srcW = Math.Clamp(srcRight - srcX, 1, layer.PixelWidth - srcX);
        int srcH = Math.Clamp(srcBottom - srcY, 1, layer.PixelHeight - srcY);

        if (srcW <= 0 || srcH <= 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cached = GetOrCreateDataset(layer.FilePath);
        if (cached == null)
        {
            return false;
        }

        lock (cached.SyncLock)
        {
            Dataset dataset = cached.Dataset;
            int bandCount = dataset.RasterCount;
            if (bandCount == 0)
            {
                return false;
            }

            int sampleCount = dstW * dstH;
            double opacity = Math.Clamp(layer.Opacity, 0.0, 1.0);

            // Fast path for 3 or 4 Byte bands (typical for aerial/ortho/topo rasters)
            if (bandCount >= 3)
            {
                using Band b1 = dataset.GetRasterBand(1);
                if (b1.DataType == DataType.GDT_Byte)
                {
                    int bandsToRead = Math.Min(bandCount, 4);
                    int[] bandMap = bandsToRead == 4 ? [1, 2, 3, 4] : [1, 2, 3];
                    int pixelBytes = bandsToRead;
                    byte[] rentedInterleaved = ArrayPool<byte>.Shared.Rent(sampleCount * pixelBytes);

                    try
                    {
                        CPLErr err = dataset.ReadRaster(
                            srcX, srcY, srcW, srcH,
                            rentedInterleaved,
                            dstW, dstH,
                            bandsToRead,
                            bandMap,
                            pixelSpace: pixelBytes,
                            lineSpace: dstW * pixelBytes,
                            bandSpace: 1);

                        if (err == CPLErr.CE_None)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            for (int y = 0; y < dstH; y++)
                            {
                                int screenRow = (dstY + y) * screenW;
                                int bufRow = y * dstW;

                                for (int x = 0; x < dstW; x++)
                                {
                                    int sampleIdx = (bufRow + x) * pixelBytes;
                                    int targetIdx = (screenRow + (dstX + x)) * 4;

                                    byte r = rentedInterleaved[sampleIdx];
                                    byte g = rentedInterleaved[sampleIdx + 1];
                                    byte b = rentedInterleaved[sampleIdx + 2];
                                    byte a = bandsToRead >= 4 ? rentedInterleaved[sampleIdx + 3] : (byte)255;

                                    double srcAlpha = (a / 255.0) * opacity;
                                    if (srcAlpha <= 0.001)
                                    {
                                        continue;
                                    }

                                    if (srcAlpha >= 0.999)
                                    {
                                        bgraBuffer[targetIdx] = b;
                                        bgraBuffer[targetIdx + 1] = g;
                                        bgraBuffer[targetIdx + 2] = r;
                                        bgraBuffer[targetIdx + 3] = 255;
                                    }
                                    else
                                    {
                                        double invAlpha = 1.0 - srcAlpha;
                                        bgraBuffer[targetIdx] = (byte)Math.Clamp((int)((b * srcAlpha) + (bgraBuffer[targetIdx] * invAlpha)), 0, 255);
                                        bgraBuffer[targetIdx + 1] = (byte)Math.Clamp((int)((g * srcAlpha) + (bgraBuffer[targetIdx + 1] * invAlpha)), 0, 255);
                                        bgraBuffer[targetIdx + 2] = (byte)Math.Clamp((int)((r * srcAlpha) + (bgraBuffer[targetIdx + 2] * invAlpha)), 0, 255);
                                        bgraBuffer[targetIdx + 3] = (byte)Math.Clamp((int)((srcAlpha * 255) + (bgraBuffer[targetIdx + 3] * invAlpha)), 0, 255);
                                    }
                                }
                            }

                            return true;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rentedInterleaved);
                    }
                }
            }

            // General path for single band, indexed palette, or non-byte bands
            byte[]? rentedR = null;
            byte[]? rentedG = null;
            byte[]? rentedB = null;
            byte[]? rentedA = null;

            try
            {
                rentedR = ArrayPool<byte>.Shared.Rent(sampleCount);
                byte[] bufR = rentedR;
                byte[]? bufG = null;
                byte[]? bufB = null;
                byte[]? bufA = null;

                if (bandCount >= 3)
                {
                    rentedG = ArrayPool<byte>.Shared.Rent(sampleCount);
                    rentedB = ArrayPool<byte>.Shared.Rent(sampleCount);
                    bufG = rentedG;
                    bufB = rentedB;

                    using (Band b1 = dataset.GetRasterBand(1))
                    {
                        ReadBandAsByte(b1, srcX, srcY, srcW, srcH, bufR, dstW, dstH);
                    }
                    using (Band b2 = dataset.GetRasterBand(2))
                    {
                        ReadBandAsByte(b2, srcX, srcY, srcW, srcH, bufG, dstW, dstH);
                    }
                    using (Band b3 = dataset.GetRasterBand(3))
                    {
                        ReadBandAsByte(b3, srcX, srcY, srcW, srcH, bufB, dstW, dstH);
                    }

                    if (bandCount >= 4)
                    {
                        using Band b4 = dataset.GetRasterBand(4);
                        if (b4.GetColorInterpretation() == ColorInterp.GCI_AlphaBand)
                        {
                            rentedA = ArrayPool<byte>.Shared.Rent(sampleCount);
                            bufA = rentedA;
                            ReadBandAsByte(b4, srcX, srcY, srcW, srcH, bufA, dstW, dstH);
                        }
                    }
                }
                else
                {
                    // 1 or 2 bands: Grayscale or Indexed / Palette
                    using Band b1 = dataset.GetRasterBand(1);
                    ReadBandAsByte(b1, srcX, srcY, srcW, srcH, bufR, dstW, dstH);

                    using ColorTable? ct = b1.GetRasterColorTable();
                    if (ct != null && ct.GetCount() > 0)
                    {
                        rentedG = ArrayPool<byte>.Shared.Rent(sampleCount);
                        rentedB = ArrayPool<byte>.Shared.Rent(sampleCount);
                        rentedA = ArrayPool<byte>.Shared.Rent(sampleCount);
                        bufG = rentedG;
                        bufB = rentedB;
                        bufA = rentedA;

                        for (int i = 0; i < sampleCount; i++)
                        {
                            byte idx = bufR[i];
                            using ColorEntry? entry = ct.GetColorEntry(idx);
                            if (entry != null)
                            {
                                bufR[i] = (byte)Math.Clamp((int)entry.c1, 0, 255);
                                bufG[i] = (byte)Math.Clamp((int)entry.c2, 0, 255);
                                bufB[i] = (byte)Math.Clamp((int)entry.c3, 0, 255);
                                bufA[i] = (byte)Math.Clamp((int)entry.c4, 0, 255);
                            }
                            else
                            {
                                bufG[i] = bufR[i];
                                bufB[i] = bufR[i];
                                bufA[i] = 255;
                            }
                        }
                    }
                    else
                    {
                        bufG = bufR;
                        bufB = bufR;

                        if (bandCount >= 2)
                        {
                            using Band b2 = dataset.GetRasterBand(2);
                            if (b2.GetColorInterpretation() == ColorInterp.GCI_AlphaBand)
                            {
                                rentedA = ArrayPool<byte>.Shared.Rent(sampleCount);
                                bufA = rentedA;
                                ReadBandAsByte(b2, srcX, srcY, srcW, srcH, bufA, dstW, dstH);
                            }
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Blend into target BGRA buffer
                for (int y = 0; y < dstH; y++)
                {
                    int screenRow = (dstY + y) * screenW;
                    int bufRow = y * dstW;

                    for (int x = 0; x < dstW; x++)
                    {
                        int sampleIdx = bufRow + x;
                        int targetIdx = (screenRow + (dstX + x)) * 4;

                        byte r = bufR[sampleIdx];
                        byte g = bufG[sampleIdx];
                        byte b = bufB[sampleIdx];

                        double srcAlpha = (bufA != null ? (bufA[sampleIdx] / 255.0) : 1.0) * opacity;
                        if (srcAlpha <= 0.001)
                        {
                            continue;
                        }

                        if (srcAlpha >= 0.999)
                        {
                            bgraBuffer[targetIdx] = b;
                            bgraBuffer[targetIdx + 1] = g;
                            bgraBuffer[targetIdx + 2] = r;
                            bgraBuffer[targetIdx + 3] = 255;
                        }
                        else
                        {
                            double invAlpha = 1.0 - srcAlpha;
                            bgraBuffer[targetIdx] = (byte)Math.Clamp((int)((b * srcAlpha) + (bgraBuffer[targetIdx] * invAlpha)), 0, 255);
                            bgraBuffer[targetIdx + 1] = (byte)Math.Clamp((int)((g * srcAlpha) + (bgraBuffer[targetIdx + 1] * invAlpha)), 0, 255);
                            bgraBuffer[targetIdx + 2] = (byte)Math.Clamp((int)((r * srcAlpha) + (bgraBuffer[targetIdx + 2] * invAlpha)), 0, 255);
                            bgraBuffer[targetIdx + 3] = (byte)Math.Clamp((int)((srcAlpha * 255) + (bgraBuffer[targetIdx + 3] * invAlpha)), 0, 255);
                        }
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error renderizando capa '{layer.Name}': {ex.Message}");
                return false;
            }
            finally
            {
                if (rentedR != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedR);
                }
                if (rentedG != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedG);
                }
                if (rentedB != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedB);
                }
                if (rentedA != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedA);
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var entry in _datasetCache.Values)
        {
            entry.Dispose();
        }
        _datasetCache.Clear();
    }
}
