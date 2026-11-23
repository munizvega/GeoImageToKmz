using System.Globalization;

namespace TifToKmz.ImageTools.Splitters;

public record SplitOptions
{
    private readonly Func<int, double>? _tileWidthCalculator;
    private readonly Func<int, double>? _tileHeightCalculator;

    public string[] CreationOptions { get; init; } = [];
    public string[] TranslateOptions { get; init; } = ["NUM_THREADS=ALL_CPUS"];

    public bool HasCustomTileSize => _tileWidthCalculator != null || _tileHeightCalculator != null;

    public SplitOptions() { }

    public SplitOptions(Func<int, double> tileWidthCalculator, Func<int, double> tileHeightCalculator)
    {
        _tileWidthCalculator = tileWidthCalculator;
        _tileHeightCalculator = tileHeightCalculator;
    }

    public double GetTileWidth(int imageWidth)
    {
        return _tileWidthCalculator?.Invoke(imageWidth) ?? imageWidth;
    }

    public double GetTileHeight(int imageHeight)
    {
        return _tileHeightCalculator?.Invoke(imageHeight) ?? imageHeight;
    }

    public string[] ToParameters(string driverName, int? xOffset = null, int? yOffset = null, int? tileWidth = null, int? tileHeight = null)
    {
        List<string> parameters = [];

        if (xOffset.HasValue && yOffset.HasValue && tileWidth.HasValue && tileHeight.HasValue)
        {
            parameters.AddRange([
                "-srcwin",
                xOffset.Value.ToString(CultureInfo.InvariantCulture),
                yOffset.Value.ToString(CultureInfo.InvariantCulture),
                tileWidth.Value.ToString(CultureInfo.InvariantCulture),
                tileHeight.Value.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        parameters.AddRange(["-of", driverName]);

        foreach (string co in CreationOptions)
        {
            parameters.Add("-co");
            parameters.Add(co);
        }

        foreach (string to in TranslateOptions)
        {
            parameters.Add("-co");
            parameters.Add(to);
        }

        return parameters.ToArray();
    }
}
