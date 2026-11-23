using OSGeo.GDAL;

namespace TifToKmz.ImageTools.Mergers;

public class GeoImageMerger : IImageMerger
{
    public void Merge(IEnumerable<string> fullImagePaths, string fullOutputFileName, MergeOptions? options = null, IProgress<double>? progress = null)
    {
        options ??= new MergeOptions();
        ValidateMergeParameters(fullImagePaths, fullOutputFileName);

        string[] warpOptionsList = options.ToParameters();

        using GDALWarpAppOptions gdalWarpOptions = new(warpOptionsList);
        List<Dataset> sourceDatasets = [];

        try
        {
            var sortedImagePaths = fullImagePaths
                .Select(p => new { Path = p, Quality = GdalHelper.CalculateQualityScore(p) })
                .OrderBy(item => item.Quality)
                .Select(item => item.Path)
                .ToList();

            foreach (string fullImagePath in sortedImagePaths)
            {
                sourceDatasets.Add(GdalHelper.OpenDataset(fullImagePath, Access.GA_ReadOnly) ??
                                   throw new InvalidOperationException($"Could not open '{fullImagePath}'"));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputFileName!)!);

            Gdal.GDALProgressFuncDelegate progressCallback = (complete, _, _) => { progress?.Report(complete); return 1; };
            using Dataset outputDataset = Gdal.Warp(fullOutputFileName, sourceDatasets.ToArray(), gdalWarpOptions, progressCallback, null) ??
                                          throw new InvalidOperationException($"Could not create merged image '{fullOutputFileName}'.");

            outputDataset.FlushCache();
        }
        finally
        {
            foreach (Dataset dataset in sourceDatasets)
            {
                dataset.Dispose();
            }
        }
    }
    private void ValidateMergeParameters(IEnumerable<string> fullImagePaths, string fullOutputFileName)
    {
        ArgumentNullException.ThrowIfNull(fullImagePaths);

        if (!fullImagePaths.Any())
        {
            throw new ArgumentException("At least one image path is required.", nameof(fullImagePaths));
        }

        if (string.IsNullOrWhiteSpace(fullOutputFileName))
        {
            throw new ArgumentException("Output file name cannot be empty.", nameof(fullOutputFileName));
        }

        if (Path.GetDirectoryName(fullOutputFileName) == null)
        {
            throw new ArgumentException($"Full path '{fullOutputFileName}' is not a valid path or could not be created.", nameof(fullOutputFileName));
        }
    }
}
