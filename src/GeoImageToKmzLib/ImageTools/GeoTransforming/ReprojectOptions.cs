using System.Globalization;

namespace TifToKmz.ImageTools.GeoTransforming;

public record ReprojectOptions
{
    public string[] CreationOptions { get; init; } = ["TILED=YES", "COMPRESS=DEFLATE", "BIGTIFF=IF_SAFER"];
    public string ResamplingMethod { get; init; } = "cubicspline";
    public int WarpMemory { get; init; } = 1024;
    public bool Multi { get; init; } = true;
    public string[] WarpOptions { get; init; } = ["NUM_THREADS=ALL_CPUS", "OPTIMIZE_SIZE=YES"];
    public bool DestinationAlpha { get; init; } = true;

    public string[] ToParameters(string? sourceWellKnownText = null, string? targetWellKnownText = null)
    {
        List<string> parameters = [];

        if (!string.IsNullOrWhiteSpace(sourceWellKnownText))
        {
            parameters.Add("-s_srs");
            parameters.Add(sourceWellKnownText);
        }

        if (!string.IsNullOrWhiteSpace(targetWellKnownText))
        {
            parameters.Add("-t_srs");
            parameters.Add(targetWellKnownText);
        }

        parameters.AddRange(
        [
            "-of", "GTiff",
            "-r", ResamplingMethod,
            "-wm", WarpMemory.ToString(CultureInfo.InvariantCulture)
        ]);

        if (Multi)
        {
            parameters.Add("-multi");
        }

        if (DestinationAlpha)
        {
            parameters.Add("-dstalpha");
        }

        foreach (string co in CreationOptions)
        {
            parameters.Add("-co");
            parameters.Add(co);
        }

        foreach (string wo in WarpOptions)
        {
            parameters.Add("-wo");
            parameters.Add(wo);
        }

        return parameters.ToArray();
    }
}
