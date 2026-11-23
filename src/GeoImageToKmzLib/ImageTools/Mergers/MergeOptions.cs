using System.Globalization;

using TifToKmz.Geometry;

namespace TifToKmz.ImageTools.Mergers;

public record MergeOptions
{
    public bool Multi { get; init; } = true;
    public int WarpMemory { get; init; } = 1024;
    public string[] WarpOptions { get; init; } = ["NUM_THREADS=ALL_CPUS", "OPTIMIZE_SIZE=YES"];
    public string[] CreationOptions { get; init; } = [];
    public Rect? CropRect { get; init; }

    public string[] ToParameters()
    {
        List<string> parameters =
        [
            "-wm", WarpMemory.ToString(CultureInfo.InvariantCulture)
        ];

        if (Multi)
        {
            parameters.Add("-multi");
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

        if (CropRect != null)
        {
            parameters.AddRange(
            [
                "-te",
                CropRect.TopLeft.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CropRect.BottomRight.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CropRect.BottomRight.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CropRect.TopLeft.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-te_srs", "EPSG:4326"
            ]);
        }

        return parameters.ToArray();
    }
}
