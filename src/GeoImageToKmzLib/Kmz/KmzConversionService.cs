using System.Globalization;
using System.Runtime.InteropServices;
using OSGeo.GDAL;
using OSGeo.OSR;
using TifToKmz.GpxTools;
using TifToKmz.Geometry;
using TifToKmz.ImageTools;
using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.Exporters;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Resources;

namespace TifToKmz.Kmz;

/// <summary>Shared conversion workflow used by the command-line and graphical applications.</summary>
public sealed class KmzConversionService : IKmzConversionService
{
    private readonly IGpxImageTool _gpxImageTool;
    private readonly IGeoImageProcessor _geoImageProcessor;

    public KmzConversionService(IGpxImageTool? gpxImageTool = null, IGeoImageProcessor? geoImageProcessor = null)
    {
        _gpxImageTool = gpxImageTool ?? new GpxImageToolLogger(new GpxImageTool(new GeoImageMerger()));
        _geoImageProcessor = geoImageProcessor ?? new GeoImageProcessor(
            new GeoImageResizerLogger(new GeoImageResizer()),
            new GeoImageMergerLogger(new GeoImageMerger()),
            new GeoImageSplitterLogger(new GeoImageSplitter(GdalDrivers.GTiff)),
            new MapReprojectorLogger(new MapReprojector()),
            new JpegExporterLogger(new JpegExporter()),
            new GeoImageBrightenerLogger(new GeoImageBrightener()));
    }

    public void Convert(string gpxPath,
        string imagesPath,
        string outputPath,
        KmzProcessOptions options,
        EpsgCode targetEpsg = EpsgCode.Wgs84,
        IProgress<string>? progress = null,
        IProgress<double>? detailedProgress = null,
        IProgress<ConversionProgressReport>? progressReport = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(gpxPath) || gpxPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(Strings.ErrorInvalidGpxPath, nameof(gpxPath));
        }

        if (!File.Exists(gpxPath))
        {
            throw new FileNotFoundException(Strings.ErrorGpxNotFound, gpxPath);
        }

        if (string.IsNullOrWhiteSpace(imagesPath) || imagesPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(Strings.ErrorInvalidImagesPath, nameof(imagesPath));
        }

        if (!Directory.Exists(imagesPath))
        {
            throw new DirectoryNotFoundException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorImagesFolderNotFound, imagesPath));
        }

        if (string.IsNullOrWhiteSpace(outputPath) || outputPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(Strings.ErrorInvalidOutputPath, nameof(outputPath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        InitializeGdal();
        ReportProgress(progress, progressReport, ConversionProgressReport.SearchingImages());
        string[] images = GdalHelper.FindSupportedRasterFiles(imagesPath).ToArray();
        List<string> tiles = _gpxImageTool.GetImagesIntersectingGpx(gpxPath, images).ToList();
        cancellationToken.ThrowIfCancellationRequested();
        if (tiles.Count == 0)
        {
            throw new InvalidOperationException(Strings.ErrorNoIntersectingTiles);
        }

        Rect box = _gpxImageTool.GetGpxBoundingBox(gpxPath, 100);
        string workspace = string.IsNullOrWhiteSpace(options.Kmz.WorkspacePath) ? Path.GetTempPath() : options.Kmz.WorkspacePath;
        if (workspace.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new ArgumentException(Strings.ErrorInvalidWorkspacePath, nameof(options));
        }

        string runWorkspace = Path.Combine(Path.GetFullPath(workspace), $"tif2kmz_{Guid.NewGuid():N}");
        Directory.CreateDirectory(runWorkspace);
        string cropped = Path.Combine(runWorkspace, $"temp_cropped_{Guid.NewGuid():N}.tif");

        try
        {
            ReportProgress(progress, progressReport, ConversionProgressReport.CroppingImages(tiles.Count));
            _gpxImageTool.CropImagesToRect(tiles, box, cropped, detailedProgress);
            cancellationToken.ThrowIfCancellationRequested();

            SplitOptions split = SplitOptionsBuilder.WithCreationOptions(options.Split.CreationOptions)
                .WithTileHeight(options.Kmz.TileSize).WithColumnWidth(options.Kmz.TileSize);
            options.Split = split;
            options.Kmz = options.Kmz with { WorkspacePath = runWorkspace };

            _geoImageProcessor.ToKmz([cropped], outputPath, options, targetEpsg, progress, detailedProgress, progressReport, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(runWorkspace))
            {
                Directory.Delete(runWorkspace, recursive: true);
            }
        }
    }

    private static void ReportProgress(IProgress<string>? progress, IProgress<ConversionProgressReport>? progressReport, ConversionProgressReport report)
    {
        progress?.Report(report.Description);
        progressReport?.Report(report);
    }

    private static void InitializeGdal()
    {
        Gdal.AllRegister();
        string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
        string root = Path.GetDirectoryName(typeof(KmzConversionService).Assembly.Location) ?? Directory.GetCurrentDirectory();
        string proj = Path.Combine(root, "runtimes", rid, "native", "maxrev.gdal.core.libshared");
        Environment.SetEnvironmentVariable("PROJ_LIB", proj);
        Gdal.SetConfigOption("PROJ_LIB", proj);
        Osr.SetPROJSearchPaths([proj]);
    }
}
