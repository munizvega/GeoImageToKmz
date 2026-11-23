using OSGeo.GDAL;

using TifToKmz.Geometry;

using TifToKmzLib.Geometry;

namespace TifToKmz.ImageTools.Splitters;

public class GeoImageSplitter : IImageSplitter
{
    private readonly string _driverName;

    public GeoImageSplitter(string driverName)
    {
        ValidateConstructorParameters(driverName);
        _driverName = driverName;
    }

    public IEnumerable<string> Split(string fullImagePath, string fullOutputDirectory, SplitOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new SplitOptions();
        ValidateSplitParameters(fullImagePath, fullOutputDirectory, options);

        Directory.CreateDirectory(fullOutputDirectory);

        using Dataset sourceDataset = GdalHelper.OpenDataset(fullImagePath, Access.GA_ReadOnly)
                                      ?? throw new InvalidOperationException($"Could not open: {fullImagePath}");

        Size imageSize = new Size { Width = sourceDataset.RasterXSize, Height = sourceDataset.RasterYSize };
        Size tileSize = new Size
        {
            Width = (int)options.GetTileWidth(imageSize.Width),
            Height = (int)options.GetTileHeight(imageSize.Height)
        };

        List<string> outputPaths = [];
        int tileIndex = 0;

        for (int yOffset = 0; yOffset < imageSize.Height; yOffset += tileSize.Height)
        {
            int currentTileHeight = Math.Min(tileSize.Height, imageSize.Height - yOffset);

            for (int xOffset = 0; xOffset < imageSize.Width; xOffset += tileSize.Width)
            {
                int currentTileWidth = Math.Min(tileSize.Width, imageSize.Width - xOffset);
                string outputPath = BuildOutputPath(fullImagePath, fullOutputDirectory, tileIndex, xOffset, yOffset);

                if (!File.Exists(outputPath))
                {
                    CreateTile(sourceDataset, outputPath, xOffset, yOffset, currentTileWidth, currentTileHeight, options, progress);
                }

                outputPaths.Add(outputPath);
                tileIndex++;
            }
        }

        return outputPaths;
    }

    private void ValidateConstructorParameters(string driverName)
    {
        if (string.IsNullOrWhiteSpace(driverName))
        {
            throw new ArgumentException("Driver name cannot be empty.", nameof(driverName));
        }
    }

    private void ValidateSplitParameters(string fullImagePath, string fullOutputDirectory, SplitOptions options)
    {
        if (string.IsNullOrWhiteSpace(fullImagePath))
        {
            throw new ArgumentException("Image path cannot be empty.", nameof(fullImagePath));
        }

        if (string.IsNullOrWhiteSpace(fullOutputDirectory))
        {
            throw new ArgumentException("Output directory cannot be empty.", nameof(fullOutputDirectory));
        }

        Size tileSize = new Size
        {
            Width = (int)options.GetTileWidth(100), // Dummy check with arbitrary positive size
            Height = (int)options.GetTileHeight(100)
        };

        if (tileSize.Width <= 0 || tileSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Tile dimensions must be greater than zero.");
        }
    }

    private void CreateTile(Dataset sourceDataset, string outputPath, int xOffset, int yOffset, int tileWidth, int tileHeight, SplitOptions options, IProgress<double>? progress)
    {
        string[] translateOptionsList = options.ToParameters(_driverName, xOffset, yOffset, tileWidth, tileHeight);

        using GDALTranslateOptions translateOptions = new GDALTranslateOptions(translateOptionsList);
        Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
        using Dataset resultDataset = Gdal.wrapper_GDALTranslate(outputPath, sourceDataset, translateOptions, progressCallback, null)
                                      ?? throw new InvalidOperationException($"Could not create tile using GDAL Translate: {outputPath}");
    }

    private string BuildOutputPath(string fullImagePath, string outputDirectory, int tileIndex, int xOffset, int yOffset)
    {
        string inputFileName = Path.GetFileNameWithoutExtension(fullImagePath);
        string extension = Path.GetExtension(fullImagePath).TrimStart('.');

        string fileName = $"{inputFileName}_tile_{tileIndex:D5}_x{xOffset}_y{yOffset}.{extension}";

        return Path.Combine(outputDirectory, fileName);
    }
}
