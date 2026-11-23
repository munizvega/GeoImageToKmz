using System.Runtime.InteropServices;
using Avalonia;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OSR;
using TifToKmz.ImageTools;

namespace TifToKmzMapEditor;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        InitializeGdal();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    public static void InitializeGdal()
    {
        try
        {
            GdalBase.ConfigureAll();
        }
        catch
        {
            // Fallback manual configuration
            Gdal.AllRegister();
            string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
            string root = Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? Directory.GetCurrentDirectory();
            string projDirectory = Path.Combine(root, "runtimes", rid, "native", "maxrev.gdal.core.libshared");

            if (!Directory.Exists(projDirectory))
            {
                projDirectory = Path.Combine(Directory.GetCurrentDirectory(), "runtimes", rid, "native", "maxrev.gdal.core.libshared");
            }

            Environment.SetEnvironmentVariable("PROJ_LIB", projDirectory);
            Gdal.SetConfigOption("PROJ_LIB", projDirectory);
            Osr.SetPROJSearchPaths([projDirectory]);
        }

        GdalHelper.ConfigureGdalPerformance();
    }
}
