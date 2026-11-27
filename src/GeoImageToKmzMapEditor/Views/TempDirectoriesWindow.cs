using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.Services;

namespace TifToKmzMapEditor.Views;

public sealed class TempDirectoriesWindow : Window
{
    private readonly ITempDirectoryService _tempService;
    private readonly Action<string> _onStatusUpdated;
    private readonly TextBlock _currentPathText = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _customPathTextBox = new() { PlaceholderText = Strings.CustomTempDirectoryWatermark, Height = 32 };
    private readonly TextBlock _sizeText = new() { Opacity = 0.8, FontSize = 12 };
    private readonly TextBlock _messageText = new() { Opacity = 0.9, FontSize = 12, Foreground = Brushes.ForestGreen, TextWrapping = TextWrapping.Wrap };

    private const string _folderIconPath = "M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z";
    private const string _trashIconPath = "M15 4V3H9v1H4v2h1v13c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V6h1V4h-5zm2 15H7V6h10v13zM9 8h2v9H9V8zm4 0h2v9h-2V8z";
    private const string _resetIconPath = "M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8z";

    public TempDirectoriesWindow(ITempDirectoryService tempService, Action<string> onStatusUpdated)
    {
        _tempService = tempService ?? throw new ArgumentNullException(nameof(tempService));
        _onStatusUpdated = onStatusUpdated ?? throw new ArgumentNullException(nameof(onStatusUpdated));
        HelpService.Attach(this, "directorios-temporales");

        Title = Strings.TempDirectoriesTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildUi();
        RefreshState();
    }

    private void BuildUi()
    {
        StackPanel panel = new()
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16
        };

        // Header
        panel.Children.Add(new TextBlock
        {
            Text = Strings.TempDirectoriesTitle,
            FontSize = 18,
            FontWeight = FontWeight.Bold
        });

        panel.Children.Add(new TextBlock
        {
            Text = Strings.TempDirectoriesDescription,
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        // Current Directory Section
        Border currentBorder = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(12)
        };

        StackPanel currentPanel = new()
        {
            Spacing = 6
        };

        currentPanel.Children.Add(new TextBlock
        {
            Text = Strings.CurrentTempDirectoryLabel,
            FontSize = 12,
            Opacity = 0.7
        });

        Grid currentPathGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*, 8, Auto, 6, Auto")
        };

        Grid.SetColumn(_currentPathText, 0);
        _currentPathText.VerticalAlignment = VerticalAlignment.Center;
        currentPathGrid.Children.Add(_currentPathText);

        Button openCurrentDirBtn = CreateIconButton(
            _folderIconPath,
            Strings.OpenContainingFolder,
            () =>
            {
                string dir = _tempService.EffectiveTempDirectory;
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    FileExplorerHelper.OpenFolder(dir);
                }
            });
        Grid.SetColumn(openCurrentDirBtn, 2);
        currentPathGrid.Children.Add(openCurrentDirBtn);

        Button clearBtn = CreateIconButton(
            _trashIconPath,
            Strings.ButtonClearTemp,
            ClearTempDirectory);
        Grid.SetColumn(clearBtn, 4);
        currentPathGrid.Children.Add(clearBtn);

        currentPanel.Children.Add(currentPathGrid);
        currentPanel.Children.Add(_sizeText);

        currentBorder.Child = currentPanel;
        panel.Children.Add(currentBorder);

        // Custom Directory Picker Section
        StackPanel customPanel = new()
        {
            Spacing = 6
        };

        customPanel.Children.Add(new TextBlock
        {
            Text = Strings.CustomTempDirectoryLabel,
            FontWeight = FontWeight.SemiBold
        });

        Grid pathGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*, 8, Auto, 6, Auto")
        };

        Grid.SetColumn(_customPathTextBox, 0);
        pathGrid.Children.Add(_customPathTextBox);

        Button browseBtn = CreateIconButton(
            _folderIconPath,
            Strings.ButtonBrowse,
            async () => await BrowseCustomFolder());
        Grid.SetColumn(browseBtn, 2);
        pathGrid.Children.Add(browseBtn);

        Button resetBtn = CreateIconButton(
            _resetIconPath,
            Strings.ButtonResetDefault,
            () =>
            {
                _tempService.ResetToDefault();
                _customPathTextBox.Text = string.Empty;
                _messageText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TempDirectoryUpdatedStatus, _tempService.EffectiveTempDirectory);
                _onStatusUpdated(_messageText.Text);
                RefreshState();
            });
        Grid.SetColumn(resetBtn, 4);
        pathGrid.Children.Add(resetBtn);

        customPanel.Children.Add(pathGrid);
        panel.Children.Add(customPanel);

        panel.Children.Add(_messageText);

        // Bottom Save / Close buttons
        DockPanel bottomPanel = new()
        {
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        Button closeBtn = new()
        {
            Content = Strings.Close,
            Padding = new Avalonia.Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeBtn.Click += (_, _) => Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        bottomPanel.Children.Add(closeBtn);

        Button saveBtn = new()
        {
            Content = Strings.ButtonSave,
            Padding = new Avalonia.Thickness(16, 6),
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        saveBtn.Click += (_, _) =>
        {
            string newPath = _customPathTextBox.Text?.Trim() ?? string.Empty;
            _tempService.SetCustomTempDirectory(newPath);
            _messageText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TempDirectoryUpdatedStatus, _tempService.EffectiveTempDirectory);
            _onStatusUpdated(_messageText.Text);
            RefreshState();
        };
        DockPanel.SetDock(saveBtn, Dock.Right);
        bottomPanel.Children.Add(saveBtn);

        panel.Children.Add(bottomPanel);

        Content = panel;
    }

    private void RefreshState()
    {
        _currentPathText.Text = _tempService.EffectiveTempDirectory;
        _customPathTextBox.Text = _tempService.CustomTempDirectory;

        long sizeBytes = _tempService.GetTempDirectorySize();
        double sizeMb = sizeBytes / (1024.0 * 1024.0);
        _sizeText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TempDirectorySizeFormat, sizeMb);
    }

    private async Task BrowseCustomFolder()
    {
        TopLevel? topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.SelectTempFolderDialogTitle,
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            string path = folders[0].Path.LocalPath;
            _customPathTextBox.Text = path;
        }
    }

    private void ClearTempDirectory()
    {
        var (deletedFiles, deletedDirs, freedBytes) = _tempService.ClearTempDirectory();
        double freedMb = freedBytes / (1024.0 * 1024.0);
        _messageText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.TempDirectoryClearedSuccess, deletedFiles, deletedDirs, freedMb);
        _onStatusUpdated(_messageText.Text);
        RefreshState();
    }

    private static Button CreateIconButton(string pathData, string tooltip, Action onClick)
    {
        Button btn = new()
        {
            Content = new PathIcon
            {
                Data = StreamGeometry.Parse(pathData),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(btn, tooltip);
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
