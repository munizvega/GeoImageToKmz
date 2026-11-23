using System.Globalization;
using TifToKmz.Resources;

namespace TifToKmz.Kmz;

/// <summary>
/// Identifies the discrete steps of the KMZ conversion pipeline.
/// </summary>
public enum ConversionStep
{
    NonStarted = 0,
    SearchingImages = 1,
    CroppingImages = 2,
    ReprojectingImages = 3,
    MergingImages = 4,
    ResizingImage = 5,
    SplittingTiles = 6,
    BrighteningTiles = 7,
    ExportingJpeg = 8,
    CreatingKmz = 9,
    Completed = 10
}

/// <summary>
/// Strongly-typed progress report describing the current step of the conversion.
/// </summary>
public record ConversionProgressReport(
    ConversionStep Step,
    int StepNumber,
    int TotalSteps,
    string Description)
{
    public const int DefaultTotalSteps = 9;

    public static ConversionProgressReport SearchingImages() =>
        new(ConversionStep.SearchingImages, 1, DefaultTotalSteps, Strings.StepSearchingImages);

    public static ConversionProgressReport CroppingImages(int count) =>
        new(ConversionStep.CroppingImages, 2, DefaultTotalSteps, string.Format(CultureInfo.CurrentCulture, Strings.StepCroppingImages, count));

    public static ConversionProgressReport ReprojectingImages() =>
        new(ConversionStep.ReprojectingImages, 3, DefaultTotalSteps, Strings.StepReprojecting);

    public static ConversionProgressReport MergingImages() =>
        new(ConversionStep.MergingImages, 4, DefaultTotalSteps, Strings.StepMerging);

    public static ConversionProgressReport ResizingImage() =>
        new(ConversionStep.ResizingImage, 5, DefaultTotalSteps, Strings.StepResizing);

    public static ConversionProgressReport SplittingTiles() =>
        new(ConversionStep.SplittingTiles, 6, DefaultTotalSteps, Strings.StepSplitting);

    public static ConversionProgressReport BrighteningTiles() =>
        new(ConversionStep.BrighteningTiles, 7, DefaultTotalSteps, Strings.StepBrightening);

    public static ConversionProgressReport ExportingJpeg() =>
        new(ConversionStep.ExportingJpeg, 8, DefaultTotalSteps, Strings.StepExportingJpeg);

    public static ConversionProgressReport CreatingKmz() =>
        new(ConversionStep.CreatingKmz, 9, DefaultTotalSteps, Strings.StepBuildingKmz);

    public static ConversionProgressReport Completed() =>
        new(ConversionStep.Completed, 9, DefaultTotalSteps, Strings.StepCompleted);

    public override string ToString() => Description;
}
