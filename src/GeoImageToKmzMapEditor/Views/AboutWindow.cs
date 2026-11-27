using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.Services;

namespace TifToKmzMapEditor.Views;

public sealed class AboutWindow : Window
{
    private const string _gdalWebsiteUrl = "https://gdal.org";
    private const string _licenseWebsiteUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
    private const string _repositoryUrl = "https://github.com/munizvega/GeoImageToKmz";

    public AboutWindow()
    {
        Title = Strings.MenuAbout;
        Width = 500;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        StackPanel root = new()
        {
            Margin = new Thickness(24),
            Spacing = 16
        };

        TextBlock appTitle = new()
        {
            Text = Strings.AppTitle,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(appTitle);

        TextBlock creator = new()
        {
            Text = Strings.AboutCreator,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(creator);

        TextBlock disclaimer = new()
        {
            Text = Strings.AboutDisclaimer,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(disclaimer);
        root.Children.Add(new Separator());

        TextBlock licenseHeading = new()
        {
            Text = Strings.AboutLicense,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        };
        root.Children.Add(licenseHeading);

        TextBlock licenseDescription = new()
        {
            Text = Strings.AboutLicenseDescription,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(licenseDescription);

        Button licenseWebsiteButton = new()
        {
            Content = Strings.AboutLicenseWebsite,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        licenseWebsiteButton.Click += (_, _) => HelpService.OpenExternal(this, _licenseWebsiteUrl);
        root.Children.Add(licenseWebsiteButton);
        root.Children.Add(new Separator());

        TextBlock repositoryHeading = new()
        {
            Text = Strings.AboutRepository,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        };
        root.Children.Add(repositoryHeading);

        Button repositoryButton = new()
        {
            Content = Strings.AboutRepositoryWebsite,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        repositoryButton.Click += (_, _) => HelpService.OpenExternal(this, _repositoryUrl);
        root.Children.Add(repositoryButton);
        root.Children.Add(new Separator());

        TextBlock gdalHeading = new()
        {
            Text = Strings.AboutGdal,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold
        };
        root.Children.Add(gdalHeading);

        TextBlock gdalDescription = new()
        {
            Text = Strings.AboutGdalDescription,
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(gdalDescription);

        Button gdalWebsiteButton = new()
        {
            Content = Strings.AboutGdalWebsite,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        gdalWebsiteButton.Click += (_, _) => HelpService.OpenExternal(this, _gdalWebsiteUrl);
        root.Children.Add(gdalWebsiteButton);

        Button closeButton = new()
        {
            Content = Strings.ButtonClose,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (_, _) => Close();
        root.Children.Add(closeButton);

        Content = root;
    }
}