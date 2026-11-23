using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Kmz;

namespace TifToKmz;

public record KmzProcessOptions
{
    public string? Language { get; set; }
    public KmzOptions Kmz { get; set; } = new();
    public ReprojectOptions Reproject { get; set; } = new();
    public MergeOptions Merge { get; set; } = new();
    public ResizeOptions Resize { get; set; } = new();
    public SplitOptions Split { get; set; } = new();
    public BrightenOptions Brighten { get; set; } = new();
}
