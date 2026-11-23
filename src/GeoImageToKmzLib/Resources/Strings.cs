using System.Globalization;
using System.Resources;

namespace TifToKmz.Resources;

public static class Strings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    public static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("TifToKmz.Resources.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    public static string GetString(string name) => ResourceManager.GetString(name, _resourceCulture) ?? name;

    public static string ErrorGpxNotFound => GetString(nameof(ErrorGpxNotFound));
    public static string ErrorGpxWithoutPoints => GetString(nameof(ErrorGpxWithoutPoints));
    public static string ErrorImagesFolderNotFound => GetString(nameof(ErrorImagesFolderNotFound));
    public static string ErrorNoIntersectingTiles => GetString(nameof(ErrorNoIntersectingTiles));
    public static string ErrorTooManyTiles => GetString(nameof(ErrorTooManyTiles));
    public static string ErrorInvalidGpxPath => GetString(nameof(ErrorInvalidGpxPath));
    public static string ErrorInvalidImagesPath => GetString(nameof(ErrorInvalidImagesPath));
    public static string ErrorInvalidOutputPath => GetString(nameof(ErrorInvalidOutputPath));
    public static string ErrorInvalidWorkspacePath => GetString(nameof(ErrorInvalidWorkspacePath));
    public static string StepSearchingImages => GetString(nameof(StepSearchingImages));
    public static string StepCroppingImages => GetString(nameof(StepCroppingImages));
    public static string StepReprojecting => GetString(nameof(StepReprojecting));
    public static string StepMerging => GetString(nameof(StepMerging));
    public static string StepResizing => GetString(nameof(StepResizing));
    public static string StepBrightening => GetString(nameof(StepBrightening));
    public static string StepSplitting => GetString(nameof(StepSplitting));
    public static string StepExportingJpeg => GetString(nameof(StepExportingJpeg));
    public static string StepBuildingKmz => GetString(nameof(StepBuildingKmz));
    public static string StepCompleted => GetString(nameof(StepCompleted));
    public static string ConsoleUsage => GetString(nameof(ConsoleUsage));
    public static string ConsoleHelpOptions => GetString(nameof(ConsoleHelpOptions));
    public static string ConsoleHelpLanguageComment => GetString(nameof(ConsoleHelpLanguageComment));
    public static string ConsoleStartingProcessing => GetString(nameof(ConsoleStartingProcessing));
    public static string ConsoleGpxPath => GetString(nameof(ConsoleGpxPath));
    public static string ConsoleImagesPath => GetString(nameof(ConsoleImagesPath));
    public static string ConsoleOutputPath => GetString(nameof(ConsoleOutputPath));
    public static string ConsoleProgressFormat => GetString(nameof(ConsoleProgressFormat));
    public static string ConsoleDetailedProgressFormat => GetString(nameof(ConsoleDetailedProgressFormat));
    public static string ConsoleDone => GetString(nameof(ConsoleDone));
    public static string ConsoleErrorDuringProcessing => GetString(nameof(ConsoleErrorDuringProcessing));
    public static string ErrorInvalidPathCharacters => GetString(nameof(ErrorInvalidPathCharacters));
}
