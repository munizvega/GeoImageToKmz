using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TifToKmz.DependencyInjection;
using TifToKmzMapEditor.Services;
using TifToKmzMapEditor.ViewModels;
using TifToKmzMapEditor.Views;

namespace TifToKmzMapEditor;

public sealed class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        Program.InitializeGdal();
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(b => b.AddConsole());
        serviceCollection.AddTifToKmz();

        serviceCollection.AddSingleton<IGeoRasterRenderer, GdalRasterRenderer>();
        serviceCollection.AddSingleton<IMapProjectService, MapProjectService>();
        serviceCollection.AddSingleton<ITempDirectoryService, TempDirectoryService>();
        serviceCollection.AddSingleton<ILanguageService, LanguageService>();
        serviceCollection.AddTransient<MainViewModel>();
        serviceCollection.AddTransient<MainWindow>();

        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
