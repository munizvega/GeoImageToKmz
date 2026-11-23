using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TifToKmz;
using TifToKmz.Geometry;
using TifToKmz.GpxTools;
using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Kmz;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.ViewModels;

namespace TifToKmzMapEditor.Views;

public sealed class KmzExportWindow : Window
{
    private readonly MainViewModel _mainVm;
    private readonly TextBox _outputPath = new() { PlaceholderText = Strings.KmzExportOutputPathWatermark };
    private readonly ComboBox _modeComboBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _trackPanel = new() { Spacing = 8 };
    private readonly ComboBox _trackComboBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _marginTextBox = new() { Text = "100", PlaceholderText = "100", Width = 45, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12 };
    private readonly StackPanel _tilesPanel = new() { Spacing = 8 };
    private readonly TextBlock _tilesCounterText = new() { FontWeight = FontWeight.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)) };
    private readonly NumericUpDown _tileSizeNumeric = new() { Value = 1024, Minimum = 256, Maximum = 4096, Increment = 256, ShowButtonSpinner = false, Width = 140 };
    private readonly NumericUpDown _jpegQualityNumeric = new() { Value = 85, Minimum = 10, Maximum = 100, Increment = 5, ShowButtonSpinner = false, Width = 140 };
    private readonly TextBlock _stepLabelText = new() { FontSize = 11, FontWeight = FontWeight.SemiBold, IsVisible = false };
    private readonly ProgressBar _totalProgressBar = new() { Minimum = 0, Maximum = 100, Height = 8, CornerRadius = new Avalonia.CornerRadius(4), IsVisible = false };
    private readonly Grid _stepHeaderGrid = new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Margin = new Avalonia.Thickness(0, 2, 0, 0),
        IsVisible = false
    };
    private readonly TextBlock _stepDescriptionText = new()
    {
        FontSize = 11,
        Opacity = 0.85,
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _stepPercentageText = new()
    {
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Opacity = 0.85,
        TextAlignment = TextAlignment.Right,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        [Grid.ColumnProperty] = 1,
        Margin = new Avalonia.Thickness(6, 0, 0, 0)
    };
    private readonly ProgressBar _stepProgressBar = new() { Minimum = 0, Maximum = 100, Height = 6, CornerRadius = new Avalonia.CornerRadius(3), IsVisible = false };
    private readonly TextBlock _statusText = new() { Text = Strings.KmzExportReadyStatus, FontSize = 12, Opacity = 0.8, TextWrapping = TextWrapping.Wrap };
    private readonly Button _exportButton = new() { Content = Strings.KmzExportButton, Padding = new Avalonia.Thickness(20, 8), FontWeight = FontWeight.SemiBold };
    private readonly Button _cancelExportButton = new() { Content = Strings.ExportCancelButton, Padding = new Avalonia.Thickness(16, 8), IsVisible = false };
    private readonly Button _closeButton = new() { Content = Strings.ButtonCancel, Padding = new Avalonia.Thickness(16, 8) };
    private CancellationTokenSource? _exportCts;

    public KmzExportWindow(MainViewModel mainVm)
    {
        _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        Title = Strings.KmzExportTitle;
        Width = 520;
        Height = 490;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        string initialDir = _mainVm.Export.OutputDirectory;
        string defaultKmzName = _mainVm.Export.KmzFileName;
        if (string.IsNullOrWhiteSpace(defaultKmzName))
        {
            string cleanName = _mainVm.ProjectTitleText.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleanName = cleanName.Replace(c, '_');
            }
            defaultKmzName = !string.IsNullOrWhiteSpace(cleanName) ? $"{cleanName}.kmz" : "mapa.kmz";
        }
        if (!defaultKmzName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
        {
            defaultKmzName += ".kmz";
        }
        if (!string.IsNullOrWhiteSpace(initialDir))
        {
            _outputPath.Text = Path.Combine(initialDir, defaultKmzName);
        }

        _stepHeaderGrid.Children.Add(_stepDescriptionText);
        _stepHeaderGrid.Children.Add(_stepPercentageText);

        _modeComboBox.ItemsSource = _mainVm.Export.AvailableModes;
        _modeComboBox.SelectedItem = _mainVm.Export.SelectedModeOption;

        _trackComboBox.ItemsSource = _mainVm.Export.AvailableTracks;
        _trackComboBox.SelectedItem = _mainVm.Export.SelectedTrackOption;

        _marginTextBox.Text = _mainVm.Export.TrackMargin.ToString(CultureInfo.InvariantCulture);
        _tileSizeNumeric.Value = _mainVm.Export.TileSize;
        _jpegQualityNumeric.Value = _mainVm.Export.Quality;

        Grid marginGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        TextBlock marginLabel = new() { Text = Strings.ExportTrackMarginLabel, FontSize = 11, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(marginLabel, 0);
        Grid.SetColumn(_marginTextBox, 1);
        marginGrid.Children.Add(marginLabel);
        marginGrid.Children.Add(_marginTextBox);

        _trackPanel.Children.Add(_trackComboBox);
        _trackPanel.Children.Add(marginGrid);

        DockPanel tilesDock = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
        StackPanel tileButtonsPanel = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
        Button optimizeTilesBtn = new() { Content = Strings.ExportOptimizeSelectedTiles, FontSize = 11, Padding = new Thickness(6, 2) };
        optimizeTilesBtn.Click += (_, _) => _mainVm.Export.OptimizeSelectedTiles();
        Button clearTilesBtn = new() { Content = Strings.ExportClearSelectedTiles, FontSize = 11, Padding = new Thickness(6, 2) };
        clearTilesBtn.Click += (_, _) => _mainVm.Export.ClearSelectedTiles();
        tileButtonsPanel.Children.Add(optimizeTilesBtn);
        tileButtonsPanel.Children.Add(clearTilesBtn);
        DockPanel.SetDock(tileButtonsPanel, Dock.Right);
        tilesDock.Children.Add(tileButtonsPanel);
        tilesDock.Children.Add(_tilesCounterText);

        CheckBox selectTrackTilesCheckBox = new()
        {
            Content = Strings.ExportSelectTrackTiles,
            IsChecked = _mainVm.Export.SelectTrackTiles,
            FontSize = 12
        };
        selectTrackTilesCheckBox.IsCheckedChanged += (_, _) => _mainVm.Export.SelectTrackTiles = selectTrackTilesCheckBox.IsChecked ?? false;

        StackPanel trackTilesSubPanel = new() { Spacing = 6, Margin = new Thickness(16, 0, 0, 0) };
        trackTilesSubPanel.IsVisible = _mainVm.Export.SelectTrackTiles;

        Grid trackComboGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        ComboBox tilesTrackComboBox = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _mainVm.Export.AvailableTracks,
            SelectedItem = _mainVm.Export.SelectedTilesTrackOption,
            FontSize = 12
        };
        tilesTrackComboBox.SelectionChanged += (_, _) =>
        {
            if (tilesTrackComboBox.SelectedItem is TrackExportOption opt)
            {
                _mainVm.Export.SelectedTilesTrackOption = opt;
            }
        };

        Button selectTilesBtn = new()
        {
            Content = new PathIcon
            {
                Data = StreamGeometry.Parse("M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(selectTilesBtn, Strings.ExportSelectTrackTilesButton);
        selectTilesBtn.Click += (_, _) => _mainVm.Export.SelectTilesForTrack();

        Grid.SetColumn(tilesTrackComboBox, 0);
        Grid.SetColumn(selectTilesBtn, 1);
        trackComboGrid.Children.Add(tilesTrackComboBox);
        trackComboGrid.Children.Add(selectTilesBtn);
        trackTilesSubPanel.Children.Add(trackComboGrid);

        Grid tilesMarginGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        TextBlock tilesMarginLabel = new() { Text = Strings.ExportTrackMarginLabel, FontSize = 11, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };
        TextBox tilesMarginTextBox = new()
        {
            Text = _mainVm.Export.SelectedTilesTrackMargin.ToString(CultureInfo.InvariantCulture),
            PlaceholderText = "100",
            FontSize = 12,
            Width = 45,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        tilesMarginTextBox.TextChanged += (_, _) =>
        {
            if (double.TryParse(tilesMarginTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double margin) ||
                double.TryParse(tilesMarginTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out margin))
            {
                _mainVm.Export.SelectedTilesTrackMargin = margin;
            }
        };
        Grid.SetColumn(tilesMarginLabel, 0);
        Grid.SetColumn(tilesMarginTextBox, 1);
        tilesMarginGrid.Children.Add(tilesMarginLabel);
        tilesMarginGrid.Children.Add(tilesMarginTextBox);
        trackTilesSubPanel.Children.Add(tilesMarginGrid);

        CheckBox lockTileSizeCheckBox = new()
        {
            Content = Strings.ExportLockTileSize,
            IsChecked = _mainVm.Export.LockTileSize,
            FontSize = 12
        };
        lockTileSizeCheckBox.IsCheckedChanged += (_, _) => _mainVm.Export.LockTileSize = lockTileSizeCheckBox.IsChecked ?? false;

        _tilesPanel.Children.Add(tilesDock);
        _tilesPanel.Children.Add(selectTrackTilesCheckBox);
        _tilesPanel.Children.Add(trackTilesSubPanel);
        _tilesPanel.Children.Add(lockTileSizeCheckBox);

        void UpdateModePanels()
        {
            ExportAreaMode mode = (_modeComboBox.SelectedItem as ExportAreaModeOption)?.Mode ?? _mainVm.Export.SelectedMode;
            _trackPanel.IsVisible = mode == ExportAreaMode.TrackContour;
            _tilesPanel.IsVisible = mode == ExportAreaMode.SelectedTiles;
            _tilesCounterText.Text = _mainVm.Export.SelectedTilesCounterText;
            trackTilesSubPanel.IsVisible = _mainVm.Export.SelectTrackTiles;
        }

        _modeComboBox.SelectionChanged += (_, _) =>
        {
            if (_modeComboBox.SelectedItem is ExportAreaModeOption opt)
            {
                _mainVm.Export.SelectedModeOption = opt;
            }
            UpdateModePanels();
        };

        _mainVm.Export.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesCounterText))
            {
                _tilesCounterText.Text = _mainVm.Export.SelectedTilesCounterText;
            }
            else if (e.PropertyName is nameof(MapExportViewModel.SelectTrackTiles) or nameof(MapExportViewModel.IsTrackTilesSubPanelVisible))
            {
                if (selectTrackTilesCheckBox.IsChecked != _mainVm.Export.SelectTrackTiles)
                {
                    selectTrackTilesCheckBox.IsChecked = _mainVm.Export.SelectTrackTiles;
                }
                trackTilesSubPanel.IsVisible = _mainVm.Export.SelectTrackTiles;
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesTrackOption))
            {
                if (tilesTrackComboBox.SelectedItem != _mainVm.Export.SelectedTilesTrackOption)
                {
                    tilesTrackComboBox.SelectedItem = _mainVm.Export.SelectedTilesTrackOption;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.LockTileSize))
            {
                if (lockTileSizeCheckBox.IsChecked != _mainVm.Export.LockTileSize)
                {
                    lockTileSizeCheckBox.IsChecked = _mainVm.Export.LockTileSize;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedModeOption))
            {
                _modeComboBox.SelectedItem = _mainVm.Export.SelectedModeOption;
                UpdateModePanels();
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesTrackMargin))
            {
                string text = _mainVm.Export.SelectedTilesTrackMargin.ToString(CultureInfo.InvariantCulture);
                if (tilesMarginTextBox.Text != text)
                {
                    tilesMarginTextBox.Text = text;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.TrackMargin))
            {
                string text = _mainVm.Export.TrackMargin.ToString(CultureInfo.InvariantCulture);
                if (_marginTextBox.Text != text)
                {
                    _marginTextBox.Text = text;
                }
            }
        };

        UpdateModePanels();

        Button browseBtn = new() { Content = Strings.KmzExportBrowse, Padding = new Avalonia.Thickness(12, 6), Margin = new Avalonia.Thickness(6, 0, 0, 0) };
        browseBtn.Click += async (_, _) => await PickOutputFile();

        _exportButton.Padding = new Avalonia.Thickness(16, 6);
        _exportButton.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        _exportButton.Click += ExportClick;

        _closeButton.Command = new Commands.RelayCommand(() => Close());
        _cancelExportButton.Click += (_, _) =>
        {
            if (_exportCts != null && !_exportCts.IsCancellationRequested)
            {
                _cancelExportButton.IsEnabled = false;
                _statusText.Text = Strings.ExportCancellingStatus;
                _exportCts.Cancel();
            }
        };

        StackPanel formPanel = new()
        {
            Margin = new Avalonia.Thickness(24, 20, 24, 10),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = Strings.KmzExportHeader, FontSize = 18, FontWeight = FontWeight.Bold },
                new TextBlock { Text = Strings.KmzExportDescription, Opacity = 0.7, FontSize = 12 },

                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    CornerRadius = new Avalonia.CornerRadius(6),
                    Padding = new Avalonia.Thickness(12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = Strings.KmzExportAreaTitle, FontWeight = FontWeight.SemiBold },
                            _modeComboBox,
                            _trackPanel,
                            _tilesPanel
                        }
                    }
                },

                new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    CornerRadius = new Avalonia.CornerRadius(6),
                    Padding = new Avalonia.Thickness(12),
                    Child = new StackPanel
                    {
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock { Text = Strings.KmzExportTilingSettings, FontWeight = FontWeight.SemiBold },
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
                                Children =
                                {
                                    new TextBlock { Text = Strings.KmzExportTileSize, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, 0, 8, 0) },
                                    _tileSizeNumeric,
                                    new TextBlock { Text = Strings.KmzExportJpegQuality, VerticalAlignment = VerticalAlignment.Center, Margin = new Avalonia.Thickness(14, 0, 8, 0), [Grid.ColumnProperty] = 2 },
                                    _jpegQualityNumeric
                                }
                            }
                        }
                    }
                }
            }
        };

        ScrollViewer scrollViewer = new()
        {
            Content = formPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        StackPanel bottomPanel = new()
        {
            Margin = new Avalonia.Thickness(24, 10, 24, 16),
            Spacing = 8,
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = Strings.KmzExportOutputPathTitle, FontWeight = FontWeight.SemiBold },
                        new DockPanel
                        {
                            Children =
                            {
                                SetDock(_exportButton, Dock.Right),
                                SetDock(browseBtn, Dock.Right),
                                _outputPath
                            }
                        }
                    }
                },
                _stepLabelText,
                _totalProgressBar,
                _stepHeaderGrid,
                _stepProgressBar,
                _statusText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Children =
                    {
                        _closeButton,
                        _cancelExportButton
                    }
                }
            }
        };

        DockPanel rootDock = new();
        DockPanel.SetDock(bottomPanel, Dock.Bottom);
        rootDock.Children.Add(bottomPanel);
        rootDock.Children.Add(scrollViewer);

        Content = rootDock;
    }

    private static Control SetDock(Control ctrl, Dock dock)
    {
        DockPanel.SetDock(ctrl, dock);
        return ctrl;
    }

    private async Task PickOutputFile()
    {
        string? suggestedName = !string.IsNullOrWhiteSpace(_outputPath.Text) ? Path.GetFileName(_outputPath.Text) : null;
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            suggestedName = _mainVm.Export.KmzFileName;
        }
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            suggestedName = "mapa.kmz";
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.KmzExportSaveDialogTitle,
            DefaultExtension = "kmz",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType(Strings.KmzExportKmzFilter) { Patterns = ["*.kmz"] }]
        });

        if (file != null)
        {
            _outputPath.Text = file.Path.LocalPath;
            string? dir = Path.GetDirectoryName(file.Path.LocalPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                _mainVm.Export.OutputDirectory = dir;
            }
        }
    }

    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        string output = _outputPath.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(output))
        {
            _statusText.Text = Strings.KmzExportErrorOutputPath;
            return;
        }

        string? outDir = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(outDir) && Directory.Exists(outDir))
        {
            _mainVm.Export.OutputDirectory = outDir;
        }

        var visibleLayers = _mainVm.ProjectService.Layers.Where(l => l.IsVisible).ToList();
        if (visibleLayers.Count == 0)
        {
            _statusText.Text = Strings.KmzExportErrorNoLayers;
            return;
        }

        ExportAreaMode mode = (_modeComboBox.SelectedItem as ExportAreaModeOption)?.Mode ?? _mainVm.Export.SelectedMode;
        MapTrack? selectedTrack = (_trackComboBox.SelectedItem as TrackExportOption)?.Track;

        if (mode == ExportAreaMode.TrackContour && selectedTrack == null)
        {
            _statusText.Text = Strings.ExportNoTrackSelectedError;
            return;
        }

        if (mode == ExportAreaMode.SelectedTiles && _mainVm.Export.SelectedTiles.Count == 0)
        {
            _statusText.Text = Strings.ExportNoTilesSelectedError;
            return;
        }

        double trackMargin = 100.0;
        if (double.TryParse(_marginTextBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedMargin) ||
            double.TryParse(_marginTextBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out parsedMargin))
        {
            trackMargin = Math.Max(0.0, parsedMargin);
        }

        List<string> inputFiles = visibleLayers
            .OrderBy(l => l.QualityScore)
            .Select(l => l.FilePath)
            .Distinct()
            .ToList();
        List<GeoExtent> selectedTilesSnapshot = _mainVm.Export.SelectedTiles.ToList();
        int totalSteps = mode switch
        {
            ExportAreaMode.TrackContour => 7,
            ExportAreaMode.SelectedTiles => 6,
            _ => (inputFiles.Count > 1 ? 7 : 6)
        };

        _exportButton.IsEnabled = false;
        _closeButton.IsEnabled = false;
        _cancelExportButton.IsVisible = true;
        _cancelExportButton.IsEnabled = true;

        _stepLabelText.IsVisible = true;
        _totalProgressBar.IsVisible = true;
        _totalProgressBar.Value = 0;
        _stepHeaderGrid.IsVisible = true;
        _stepProgressBar.IsVisible = true;
        _stepProgressBar.Value = 0;

        _stepLabelText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ProgressStepFormat, 1, totalSteps);
        _stepDescriptionText.Text = mode == ExportAreaMode.TrackContour ? Strings.StepCroppingTrack : string.Empty;
        _stepPercentageText.Text = "0 %";
        _statusText.Text = Strings.KmzExportExportingStatus;

        _exportCts = new CancellationTokenSource();
        CancellationToken ct = _exportCts.Token;

        int tileSize = (int)(_tileSizeNumeric.Value ?? 1024);
        int jpegQuality = (int)(_jpegQualityNumeric.Value ?? 85);

        try
        {
            await Task.Run(() =>
            {
                string workspaceDir = _mainVm.TempDirectoryService.EffectiveTempDirectory;
                KmzProcessOptions options = new()
                {
                    Kmz = new KmzOptions(workspaceDir)
                    {
                        TileSize = tileSize,
                        JpegQuality = jpegQuality,
                        MaxNumberOfTiles = _mainVm.Export.MaxTiles
                    },
                    Reproject = new ReprojectOptions
                    {
                        WarpMemory = 1024,
                        ResamplingMethod = "cubicspline",
                        Multi = true,
                        DestinationAlpha = true
                    },
                    Resize = new ResizeOptions
                    {
                        Format = "COG"
                    },
                    Brighten = new BrightenOptions
                    {
                        Enabled = _mainVm.Export.BrightnessEnabled,
                        Gamma = _mainVm.Export.Gamma,
                        Brightness = _mainVm.Export.Brightness,
                        Contrast = _mainVm.Export.Contrast
                    }
                };

                SplitOptions split = SplitOptionsBuilder.WithCreationOptions(options.Split.CreationOptions)
                    .WithTileHeight(options.Kmz.TileSize).WithColumnWidth(options.Kmz.TileSize);
                options.Split = split;

                int currentStep = 0;

                void AdvanceStep(string description)
                {
                    currentStep++;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        int stepNum = Math.Min(currentStep, totalSteps);
                        int completed = currentStep - 1;
                        _totalProgressBar.Value = totalSteps > 0 ? ((double)completed / totalSteps) * 100 : 0;
                        _stepProgressBar.Value = 0;
                        _stepLabelText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ProgressStepFormat, stepNum, totalSteps);
                        _stepDescriptionText.Text = description;
                        _stepPercentageText.Text = "0 %";
                        _statusText.Text = description;
                    });
                }

                Progress<double> detailedProg = new(v =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        double pct = Math.Clamp(v * 100, 0, 100);
                        _stepProgressBar.Value = pct;
                        _stepPercentageText.Text = pct.ToString("F0", System.Globalization.CultureInfo.CurrentCulture) + " %";
                    });
                });

                Progress<ConversionProgressReport> reportProg = new(rep =>
                {
                    if (rep.Step == ConversionStep.Completed)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            _totalProgressBar.Value = 100;
                            _stepProgressBar.Value = 100;
                            _stepLabelText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.ProgressStepFormat, totalSteps, totalSteps);
                            _stepPercentageText.Text = "100 %";
                        });
                        return;
                    }

                    AdvanceStep(rep.Description);
                });

                Progress<string> prog = new(msg =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _statusText.Text = msg;
                    });
                });

                if (mode == ExportAreaMode.TrackContour && selectedTrack != null)
                {
                    AdvanceStep(Strings.StepCroppingTrack);
                    string tempDir = Path.Combine(workspaceDir, $"kmz_crop_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempDir);
                    try
                    {
                        string croppedTif = Path.Combine(tempDir, "cropped_region.tif");
                        GpxImageTool gpxTool = new(new GeoImageMerger());

                        List<string> tiles = gpxTool.GetImagesIntersectingGpx(selectedTrack.FilePath, inputFiles).ToList();
                        if (tiles.Count == 0)
                        {
                            tiles = inputFiles;
                        }

                        TifToKmz.Geometry.Rect box = gpxTool.GetGpxBoundingBox(selectedTrack.FilePath, trackMargin);
                        gpxTool.CropImagesToRect(tiles, box, croppedTif, detailedProg);

                        ct.ThrowIfCancellationRequested();

                        GeoImageProcessor processor = new(
                            new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                            new GeoImageMerger(),
                            new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                            new MapReprojector(),
                            new TifToKmz.ImageTools.Exporters.JpegExporter(),
                            new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                        processor.ToKmz([croppedTif], output, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                    }
                    finally
                    {
                        try { Directory.Delete(tempDir, true); } catch { }
                    }
                }
                else if (mode == ExportAreaMode.SelectedTiles)
                {
                    ct.ThrowIfCancellationRequested();

                    string? projectionWkt = _mainVm.ProjectService.ActiveProject?.Layers.FirstOrDefault()?.ProjectionWkt
                        ?? _mainVm.ProjectService.Layers.FirstOrDefault()?.ProjectionWkt;

                    List<TifToKmz.Geometry.Rect> wgs84Tiles = selectedTilesSnapshot
                        .Select(extent => GeoExtentToWgs84Rect(extent, projectionWkt))
                        .ToList();

                    GeoImageProcessor processor = new(
                        new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                        new GeoImageMerger(),
                        new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                        new MapReprojector(),
                        new TifToKmz.ImageTools.Exporters.JpegExporter(),
                        new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                    processor.ToKmzFromSelectedTiles(inputFiles, output, wgs84Tiles, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                }
                else
                {
                    ct.ThrowIfCancellationRequested();

                    GeoImageProcessor processor = new(
                        new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                        new GeoImageMerger(),
                        new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                        new MapReprojector(),
                        new TifToKmz.ImageTools.Exporters.JpegExporter(),
                        new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                    processor.ToKmz(inputFiles, output, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                }
            }, ct);

            _totalProgressBar.Value = 100;
            _stepProgressBar.Value = 100;
            _statusText.Text = Strings.KmzExportSuccessStatus;
        }
        catch (OperationCanceledException)
        {
            _statusText.Text = Strings.ExportCancelledStatus;
        }
        catch (Exception ex)
        {
            _statusText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.KmzExportErrorPrefix, ex.Message);
        }
        finally
        {
            _stepLabelText.IsVisible = false;
            _totalProgressBar.IsVisible = false;
            _stepHeaderGrid.IsVisible = false;
            _stepProgressBar.IsVisible = false;
            _exportButton.IsEnabled = true;
            _closeButton.IsEnabled = true;
            _cancelExportButton.IsVisible = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    private static TifToKmz.Geometry.Rect GeoExtentToWgs84Rect(GeoExtent extent, string? projectionWkt)
    {
        if (string.IsNullOrWhiteSpace(projectionWkt))
        {
            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(extent.MinX, extent.MaxY), new TifToKmz.Geometry.Point(extent.MaxX, extent.MinY));
        }

        try
        {
            using OSGeo.OSR.SpatialReference sourceSrs = new(projectionWkt);
            sourceSrs.SetAxisMappingStrategy(OSGeo.OSR.AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using OSGeo.OSR.SpatialReference wgs84 = new("");
            wgs84.ImportFromEPSG(4326);
            wgs84.SetAxisMappingStrategy(OSGeo.OSR.AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using OSGeo.OSR.CoordinateTransformation transform = new(sourceSrs, wgs84);

            double[] p1 = [extent.MinX, extent.MaxY, 0];
            double[] p2 = [extent.MaxX, extent.MaxY, 0];
            double[] p3 = [extent.MaxX, extent.MinY, 0];
            double[] p4 = [extent.MinX, extent.MinY, 0];

            transform.TransformPoint(p1);
            transform.TransformPoint(p2);
            transform.TransformPoint(p3);
            transform.TransformPoint(p4);

            double minLon = Math.Min(Math.Min(p1[0], p2[0]), Math.Min(p3[0], p4[0]));
            double maxLon = Math.Max(Math.Max(p1[0], p2[0]), Math.Max(p3[0], p4[0]));
            double minLat = Math.Min(Math.Min(p1[1], p2[1]), Math.Min(p3[1], p4[1]));
            double maxLat = Math.Max(Math.Max(p1[1], p2[1]), Math.Max(p3[1], p4[1]));

            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(minLon, maxLat), new TifToKmz.Geometry.Point(maxLon, minLat));
        }
        catch
        {
            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(extent.MinX, extent.MaxY), new TifToKmz.Geometry.Point(extent.MaxX, extent.MinY));
        }
    }
}
