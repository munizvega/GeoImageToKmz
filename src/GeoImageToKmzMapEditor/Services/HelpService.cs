using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Services;

internal static class HelpService
{
    public static void Attach(Window window, string defaultSection)
    {
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.F1 || e.KeyModifiers != KeyModifiers.None)
            {
                return;
            }

            string section = defaultSection;
            for (var visual = window.FocusManager?.GetFocusedElement() as Avalonia.Visual; visual != null; visual = visual.GetVisualParent())
            {
                if (visual is Control { Tag: string tag })
                {
                    section = tag;
                    break;
                }
            }

            e.Handled = true;
            Open(window, section);
        }, RoutingStrategies.Tunnel);
    }

    public static void Open(Window owner, string section)
    {
        try
        {
            string language = Strings.Culture?.TwoLetterISOLanguageName ?? "es";
            string guideFileName = language == "es" ? "guia.html" : $"guia.{language}.html";
            string path = Path.Combine(AppContext.BaseDirectory, "Help", guideFileName);
            if (!File.Exists(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "Help", "guia.html");
            }
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }

            string uri = new Uri(path).AbsoluteUri + "#" + Uri.EscapeDataString(section);
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowOpenError(owner, string.Format(Strings.HelpOpenError, ex.Message));
        }
    }

    public static void OpenExternal(Window owner, string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowOpenError(owner, string.Format(Strings.ExternalLinkOpenError, ex.Message));
        }
    }

    private static void ShowOpenError(Window owner, string message)
    {
        Window dialog = new()
        {
            Title = Strings.MenuHelp,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Avalonia.Thickness(20) }
        };
        _ = dialog.ShowDialog(owner);
    }
}