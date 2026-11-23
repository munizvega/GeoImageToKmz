using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TifToKmz.GpxTools;
using TifToKmz.ImageTools;
using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.Exporters;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Kmz;

namespace TifToKmz.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTifToKmz(this IServiceCollection services)
    {
        services.AddSingleton<IImageUtilities>(sp =>
            new ImageUtilitiesLogger(new ImageUtilities(), sp.GetService<ILogger<ImageUtilitiesLogger>>()));

        services.AddSingleton<IImageMerger>(sp =>
            new GeoImageMergerLogger(new GeoImageMerger(), sp.GetService<ILogger<GeoImageMergerLogger>>()));

        services.AddSingleton<IImageResizer>(sp =>
            new GeoImageResizerLogger(new GeoImageResizer(), sp.GetService<ILogger<GeoImageResizerLogger>>()));

        services.AddSingleton<IMapReprojector>(sp =>
            new MapReprojectorLogger(new MapReprojector(), sp.GetService<ILogger<MapReprojectorLogger>>()));

        services.AddSingleton<IJpegExporter>(sp =>
            new JpegExporterLogger(new JpegExporter(), sp.GetService<ILogger<JpegExporterLogger>>()));

        services.AddSingleton<IImageSplitter>(sp =>
            new GeoImageSplitterLogger(new GeoImageSplitter(GdalDrivers.GTiff), sp.GetService<ILogger<GeoImageSplitterLogger>>()));

        services.AddSingleton<IImageBrightener>(sp =>
            new GeoImageBrightenerLogger(new GeoImageBrightener(), sp.GetService<ILogger<GeoImageBrightenerLogger>>()));

        services.AddSingleton<IGpxImageTool>(sp =>
            new GpxImageToolLogger(new GpxImageTool(sp.GetRequiredService<IImageMerger>()), sp.GetService<ILogger<GpxImageToolLogger>>()));

        services.AddSingleton<IGeoImageProcessor, GeoImageProcessor>();
        services.AddSingleton<IKmzConversionService, KmzConversionService>();

        return services;
    }
}
