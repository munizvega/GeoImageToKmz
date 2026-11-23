using System;
#pragma warning disable IDE0055
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using System.Xml;
using OSGeo.GDAL;
using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.Exporters;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Resources;
using TifToKmz;

namespace TifToKmz.Kmz;

public class GeoImageProcessor : IGeoImageProcessor
{
    private readonly IImageResizer _imageResizer;
    private readonly IImageMerger _imageMerger;
    private readonly IImageSplitter _imageSplitter;
    private readonly IJpegExporter _jpegExporter;
    private readonly IMapReprojector _mapReprojector;
    private readonly IImageBrightener _imageBrightener;

    public GeoImageProcessor(IImageResizer imageResizer, IImageMerger imageMerger,
        IImageSplitter imageSplitter, IMapReprojector mapReprojector, IJpegExporter jpegExporter,
        IImageBrightener? imageBrightener = null)
    {
        ValidateConstructorParameters(imageResizer, imageMerger, imageSplitter, mapReprojector, jpegExporter);
        _imageResizer = imageResizer;
        _imageMerger = imageMerger;
        _imageSplitter = imageSplitter;
        _mapReprojector = mapReprojector;
        _jpegExporter = jpegExporter;
        _imageBrightener = imageBrightener ?? new GeoImageBrightenerLogger(new GeoImageBrightener());
    }

    public void ToKmz(IEnumerable<string> inputFilePaths,
        string outputFilePath,
        KmzProcessOptions? options = null,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new KmzProcessOptions();
        ArgumentNullException.ThrowIfNull(inputFilePaths);

        if (!options.Split.HasCustomTileSize && options.Kmz.TileSize > 0)
        {
            options.Split = SplitOptionsBuilder.WithCreationOptions(options.Split.CreationOptions)
                .WithTileHeight(options.Kmz.TileSize)
                .WithColumnWidth(options.Kmz.TileSize);
        }

        List<string> inputFilePathsList = inputFilePaths.
            Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath).Distinct().ToList();

        ValidateToKmzParameters(inputFilePathsList, outputFilePath);
        cancellationToken.ThrowIfCancellationRequested();

        string finalOutput = Path.GetFullPath(outputFilePath);
        string workspace = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Kmz.WorkspacePath) ? Path.GetDirectoryName(finalOutput)! : options.Kmz.WorkspacePath);
        string runDirectory = Path.Combine(workspace, $"kmz-work-{Guid.NewGuid():N}");
        string wgs84Directory = Path.Combine(runDirectory, "wgs84");

        try
        {
            ConcurrentBag<string> wgs84Paths = new ConcurrentBag<string>();
            ReportProgress(progress, progressReport, ConversionProgressReport.ReprojectingImages());

            int totalInputs = inputFilePathsList.Count;
            double[] reprojectionProgress = new double[totalInputs];
            object progressLock = new();

            Parallel.ForEach(
                inputFilePathsList.Select((path, index) => (path, index)),
                new ParallelOptions { CancellationToken = cancellationToken },
                item =>
                {
                    IProgress<double>? itemProgress = detailedProgress == null
                        ? null
                        : new SynchronizedProgress<double>(val =>
                        {
                            lock (progressLock)
                            {
                                reprojectionProgress[item.index] = Math.Clamp(val, 0d, 1d);
                                double sum = 0;
                                for (int i = 0; i < totalInputs; i++)
                                {
                                    sum += reprojectionProgress[i];
                                }

                                detailedProgress.Report(sum / totalInputs);
                            }
                        });

                    wgs84Paths.Add(_mapReprojector.Reproject(item.path, wgs84Directory, targetEpsg, options.Reproject, itemProgress));
                });

            detailedProgress?.Report(1.0);
            string[] wgs84 = wgs84Paths.ToArray();

            string merged = wgs84.Length == 1 ? wgs84[0] : Path.Combine(runDirectory, "merged.tif");

            if (wgs84.Length > 1 && !File.Exists(merged))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportProgress(progress, progressReport, ConversionProgressReport.MergingImages());
                _imageMerger.Merge(wgs84, merged, options.Merge, detailedProgress);
            }

            double scale = CalculateScale(merged, options.Kmz);
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.ResizingImage());
            string resized = _imageResizer.Resize(merged, Path.Combine(runDirectory, "resized"), scale, options.Resize, detailedProgress);

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.SplittingTiles());
            string[] tiles = _imageSplitter.Split(resized, Path.Combine(runDirectory, "tiles"), options.Split, detailedProgress).ToArray();
            if (tiles.Length > options.Kmz.MaxNumberOfTiles)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorTooManyTiles, tiles.Length, options.Kmz.MaxNumberOfTiles));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.BrighteningTiles());
            string brightTilesDirectory = Path.Combine(runDirectory, "bright-tiles");
            string[] brightTiles = new string[tiles.Length];
            int totalTilesToBrighten = tiles.Length;
            for (int i = 0; i < totalTilesToBrighten; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int tileIndex = i;
                IProgress<double>? tileProgress = detailedProgress == null
                    ? null
                    : new SynchronizedProgress<double>(val =>
                    {
                        double overall = (tileIndex + Math.Clamp(val, 0d, 1d)) / totalTilesToBrighten;
                        detailedProgress.Report(overall);
                    });

                brightTiles[i] = _imageBrightener.Brighten(tiles[i], brightTilesDirectory, options.Brighten, tileProgress);
            }

            detailedProgress?.Report(1.0);

            string jpegTilesDirectory = Path.Combine(runDirectory, "jpeg-tiles");
            Directory.CreateDirectory(jpegTilesDirectory);

            List<string> jpegTiles = new List<string>();
            ReportProgress(progress, progressReport, ConversionProgressReport.ExportingJpeg());
            int totalTiles = brightTiles.Length;
            for (int i = 0; i < totalTiles; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string tile = brightTiles[i];
                string jpegPath = Path.Combine(jpegTilesDirectory, Path.GetFileNameWithoutExtension(tile) + ".jpg");
                int tileIndex = i;
                IProgress<double>? tileProgress = detailedProgress == null
                    ? null
                    : new SynchronizedProgress<double>(val =>
                    {
                        double overall = (tileIndex + Math.Clamp(val, 0d, 1d)) / totalTiles;
                        detailedProgress.Report(overall);
                    });

                _jpegExporter.Export(tile, jpegPath, options.Kmz.JpegQuality, tileProgress);
                jpegTiles.Add(jpegPath);
            }

            detailedProgress?.Report(1.0);

            Directory.CreateDirectory(Path.GetDirectoryName(finalOutput)!);
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.CreatingKmz());
            if (File.Exists(finalOutput))
            {
                File.Delete(finalOutput);
            }

            string kml = Path.Combine(runDirectory, "doc.kml");
            WriteKml(jpegTiles, kml);
            using ZipArchive archive = ZipFile.Open(finalOutput, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(kml, "doc.kml", CompressionLevel.Optimal);
            foreach (string tile in jpegTiles)
            {
                archive.CreateEntryFromFile(tile, $"files/{Path.GetFileName(tile)}", CompressionLevel.Optimal);
            }
            ReportProgress(progress, progressReport, ConversionProgressReport.Completed());
        }
        finally
        {
            if (Directory.Exists(runDirectory))
            {
                Directory.Delete(runDirectory, recursive: true);
            }
        }
    }

    public void ToKmzFromSelectedTiles(IEnumerable<string> inputFilePaths,
        string outputFilePath,
        IEnumerable<TifToKmz.Geometry.Rect> selectedTiles,
        KmzProcessOptions? options = null,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new KmzProcessOptions();
        ArgumentNullException.ThrowIfNull(inputFilePaths);
        ArgumentNullException.ThrowIfNull(selectedTiles);

        List<TifToKmz.Geometry.Rect> selectedTilesList = selectedTiles.ToList();
        if (selectedTilesList.Count == 0)
        {
            throw new ArgumentException("At least one selected tile is required.", nameof(selectedTiles));
        }

        if (selectedTilesList.Count > options.Kmz.MaxNumberOfTiles)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorTooManyTiles, selectedTilesList.Count, options.Kmz.MaxNumberOfTiles));
        }

        List<string> inputFilePathsList = inputFilePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath).Distinct().ToList();

        ValidateToKmzParameters(inputFilePathsList, outputFilePath);
        cancellationToken.ThrowIfCancellationRequested();

        string finalOutput = Path.GetFullPath(outputFilePath);
        string workspace = Path.GetFullPath(string.IsNullOrWhiteSpace(options.Kmz.WorkspacePath) ? Path.GetDirectoryName(finalOutput)! : options.Kmz.WorkspacePath);
        string runDirectory = Path.Combine(workspace, $"kmz-work-{Guid.NewGuid():N}");
        string wgs84Directory = Path.Combine(runDirectory, "wgs84");

        try
        {
            double unionMinX = selectedTilesList.Min(t => Math.Min(t.TopLeft.X, t.BottomRight.X));
            double unionMaxX = selectedTilesList.Max(t => Math.Max(t.TopLeft.X, t.BottomRight.X));
            double unionMinY = selectedTilesList.Min(t => Math.Min(t.TopLeft.Y, t.BottomRight.Y));
            double unionMaxY = selectedTilesList.Max(t => Math.Max(t.TopLeft.Y, t.BottomRight.Y));
            TifToKmz.Geometry.Rect unionCropRect = new(new TifToKmz.Geometry.Point(unionMinX, unionMaxY), new TifToKmz.Geometry.Point(unionMaxX, unionMinY));

            ConcurrentBag<string> wgs84Paths = new();
            ReportProgress(progress, progressReport, ConversionProgressReport.ReprojectingImages());

            int totalInputs = inputFilePathsList.Count;
            double[] reprojectionProgress = new double[totalInputs];
            object progressLock = new();

            Parallel.ForEach(
                inputFilePathsList.Select((path, index) => (path, index)),
                new ParallelOptions { CancellationToken = cancellationToken },
                item =>
                {
                    IProgress<double>? itemProgress = detailedProgress == null
                        ? null
                        : new SynchronizedProgress<double>(val =>
                        {
                            lock (progressLock)
                            {
                                reprojectionProgress[item.index] = Math.Clamp(val, 0d, 1d);
                                double sum = 0;
                                for (int i = 0; i < totalInputs; i++)
                                {
                                    sum += reprojectionProgress[i];
                                }

                                detailedProgress.Report(sum / totalInputs);
                            }
                        });

                    wgs84Paths.Add(_mapReprojector.Reproject(item.path, wgs84Directory, targetEpsg, options.Reproject, itemProgress));
                });

            detailedProgress?.Report(1.0);
            string[] wgs84 = wgs84Paths.ToArray();

            string merged = Path.Combine(runDirectory, "merged.tif");
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.MergingImages());
            _imageMerger.Merge(wgs84, merged, options.Merge with { CropRect = unionCropRect }, detailedProgress);

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.SplittingTiles());
            string tilesDirectory = Path.Combine(runDirectory, "tiles");
            Directory.CreateDirectory(tilesDirectory);

            int tileSize = options.Kmz.TileSize > 0 ? options.Kmz.TileSize : 1024;
            List<string> tiles = new();
            int totalSelected = selectedTilesList.Count;

            using (Dataset mergedDataset = Gdal.Open(merged, Access.GA_ReadOnly) ?? throw new InvalidOperationException($"Could not open merged image '{merged}'."))
            {
                for (int i = 0; i < totalSelected; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TifToKmz.Geometry.Rect tileRect = selectedTilesList[i];
                    string tilePath = Path.Combine(tilesDirectory, $"tile_{i:D5}.tif");

                    double tMinX = Math.Min(tileRect.TopLeft.X, tileRect.BottomRight.X);
                    double tMaxX = Math.Max(tileRect.TopLeft.X, tileRect.BottomRight.X);
                    double tMinY = Math.Min(tileRect.TopLeft.Y, tileRect.BottomRight.Y);
                    double tMaxY = Math.Max(tileRect.TopLeft.Y, tileRect.BottomRight.Y);

                    string[] warpParams =
                    [
                        "-te", tMinX.ToString(CultureInfo.InvariantCulture),
                        tMinY.ToString(CultureInfo.InvariantCulture),
                        tMaxX.ToString(CultureInfo.InvariantCulture),
                        tMaxY.ToString(CultureInfo.InvariantCulture),
                        "-te_srs", "EPSG:4326",
                        "-ts", tileSize.ToString(CultureInfo.InvariantCulture),
                        tileSize.ToString(CultureInfo.InvariantCulture),
                        "-r", options.Reproject.ResamplingMethod ?? "cubicspline",
                        "-wm", options.Reproject.WarpMemory.ToString(CultureInfo.InvariantCulture)
                    ];

                    using GDALWarpAppOptions warpAppOptions = new(warpParams);
                    using Dataset tileDataset = Gdal.Warp(tilePath, [mergedDataset], warpAppOptions, null, null)
                        ?? throw new InvalidOperationException($"Could not extract tile '{tilePath}'.");

                    tiles.Add(tilePath);
                    detailedProgress?.Report((double)(i + 1) / totalSelected);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.BrighteningTiles());
            string brightTilesDirectory = Path.Combine(runDirectory, "bright-tiles");
            string[] brightTiles = new string[tiles.Count];
            int totalTilesToBrighten = tiles.Count;
            for (int i = 0; i < totalTilesToBrighten; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int tileIndex = i;
                IProgress<double>? tileProgress = detailedProgress == null
                    ? null
                    : new SynchronizedProgress<double>(val =>
                    {
                        double overall = (tileIndex + Math.Clamp(val, 0d, 1d)) / totalTilesToBrighten;
                        detailedProgress.Report(overall);
                    });

                brightTiles[i] = _imageBrightener.Brighten(tiles[i], brightTilesDirectory, options.Brighten, tileProgress);
            }

            detailedProgress?.Report(1.0);

            string jpegTilesDirectory = Path.Combine(runDirectory, "jpeg-tiles");
            Directory.CreateDirectory(jpegTilesDirectory);

            List<string> jpegTiles = new();
            ReportProgress(progress, progressReport, ConversionProgressReport.ExportingJpeg());
            int totalTiles = brightTiles.Length;
            for (int i = 0; i < totalTiles; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string tile = brightTiles[i];
                string jpegPath = Path.Combine(jpegTilesDirectory, Path.GetFileNameWithoutExtension(tile) + ".jpg");
                int tileIndex = i;
                IProgress<double>? tileProgress = detailedProgress == null
                    ? null
                    : new SynchronizedProgress<double>(val =>
                    {
                        double overall = (tileIndex + Math.Clamp(val, 0d, 1d)) / totalTiles;
                        detailedProgress.Report(overall);
                    });

                _jpegExporter.Export(tile, jpegPath, options.Kmz.JpegQuality, tileProgress);
                jpegTiles.Add(jpegPath);
            }

            detailedProgress?.Report(1.0);

            Directory.CreateDirectory(Path.GetDirectoryName(finalOutput)!);
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(progress, progressReport, ConversionProgressReport.CreatingKmz());
            if (File.Exists(finalOutput))
            {
                File.Delete(finalOutput);
            }

            string kml = Path.Combine(runDirectory, "doc.kml");
            WriteKml(jpegTiles, kml);
            using ZipArchive archive = ZipFile.Open(finalOutput, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(kml, "doc.kml", CompressionLevel.Optimal);
            foreach (string tile in jpegTiles)
            {
                archive.CreateEntryFromFile(tile, $"files/{Path.GetFileName(tile)}", CompressionLevel.Optimal);
            }
            ReportProgress(progress, progressReport, ConversionProgressReport.Completed());
        }
        finally
        {
            if (Directory.Exists(runDirectory))
            {
                Directory.Delete(runDirectory, recursive: true);
            }
        }
    }

    private double CalculateScale(string merged, KmzOptions options)
    {
        using Dataset dataset = Gdal.Open(merged, Access.GA_ReadOnly) ?? throw new InvalidOperationException($"Could not open merged image '{merged}'.");

        // ResizeOptions serializes the scale as a whole percentage (P0). Test
        // those effective dimensions instead of using a square-root estimate;
        // the latter can produce a few extra border tiles after rounding.
        int requestedPercentage = Math.Clamp((int)Math.Floor(options.ResizeFactor * 100), 1, 100);
        for (int percentage = requestedPercentage; percentage >= 1; percentage--)
        {
            int width = Math.Max(1, (int)Math.Ceiling(dataset.RasterXSize * percentage / 100d));
            int height = Math.Max(1, (int)Math.Ceiling(dataset.RasterYSize * percentage / 100d));
            double tileCount = Math.Ceiling(width / (double)options.TileSize) * Math.Ceiling(height / (double)options.TileSize);
            if (tileCount <= options.MaxNumberOfTiles)
            {
                return percentage / 100d;
            }
        }

        return 0.01;
    }

    private void ValidateConstructorParameters(IImageResizer imageResizer, IImageMerger imageMerger,
        IImageSplitter imageSplitter, IMapReprojector mapReprojector, IJpegExporter jpegExporter)
    {
        ArgumentNullException.ThrowIfNull(imageResizer);
        ArgumentNullException.ThrowIfNull(imageMerger);
        ArgumentNullException.ThrowIfNull(imageSplitter);
        ArgumentNullException.ThrowIfNull(mapReprojector);
        ArgumentNullException.ThrowIfNull(jpegExporter);
    }

    private void ValidateToKmzParameters(IEnumerable<string> inputFilePaths, string outputFilePath)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            throw new ArgumentException("Output path cannot be empty.", nameof(outputFilePath));
        }

        if (outputFilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException("Output path contains invalid characters.", nameof(outputFilePath));
        }

        string? fileName = Path.GetFileName(outputFilePath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Output file name contains invalid characters.", nameof(outputFilePath));
        }

        string[] inputs = inputFilePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).ToArray();
        if (inputs.Length == 0 || inputs.Any(p => !File.Exists(p)))
        {
            throw new ArgumentException("At least one existing input image is required.", nameof(inputFilePaths));
        }
    }

    private static void WriteKml(IEnumerable<string> tiles, string outputPath)
    {
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        XDocument document = new(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "kml", new XElement(ns + "Document")));
        XElement root = document.Root!.Element(ns + "Document")!;
        foreach (string tile in tiles)
        {
            using Dataset dataset = Gdal.Open(tile, Access.GA_ReadOnly) ?? throw new InvalidOperationException($"Could not open tile '{tile}'.");
            double[] gt = new double[6]; dataset.GetGeoTransform(gt);
            double[] xs = [gt[0], gt[0] + dataset.RasterXSize * gt[1], gt[0] + dataset.RasterYSize * gt[2], gt[0] + dataset.RasterXSize * gt[1] + dataset.RasterYSize * gt[2]];
            double[] ys = [gt[3], gt[3] + dataset.RasterXSize * gt[4], gt[3] + dataset.RasterYSize * gt[5], gt[3] + dataset.RasterXSize * gt[4] + dataset.RasterYSize * gt[5]];
            XElement overlay = new(ns + "GroundOverlay",
                new XElement(ns + "name", Path.GetFileNameWithoutExtension(tile)),
                new XElement(ns + "Icon", new XElement(ns + "href", $"files/{Path.GetFileName(tile)}")),
                new XElement(ns + "LatLonBox",
                    new XElement(ns + "north", ys.Max().ToString(CultureInfo.InvariantCulture)),
                    new XElement(ns + "south", ys.Min().ToString(CultureInfo.InvariantCulture)),
                    new XElement(ns + "east", xs.Max().ToString(CultureInfo.InvariantCulture)),
                    new XElement(ns + "west", xs.Min().ToString(CultureInfo.InvariantCulture))));
            root.Add(overlay);
        }
        XmlWriterSettings settings = new()
        {
            Indent = true,
            IndentChars = "    ",
            NewLineChars = Environment.NewLine,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };
        using XmlWriter writer = XmlWriter.Create(outputPath, settings);
        document.Save(writer);
    }

    private static void ReportProgress(IProgress<string>? progress, IProgress<ConversionProgressReport>? progressReport, ConversionProgressReport report)
    {
        progress?.Report(report.Description);
        progressReport?.Report(report);
    }

    private sealed class SynchronizedProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
