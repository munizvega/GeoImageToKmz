using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using TifToKmz.ImageTools;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.Services;
using TifToKmzMapEditor.ViewModels;

namespace TifToKmzMapEditor.Views;

public sealed class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MapViewportControl _mapControl;
    private readonly TextBlock _projectBadgeText = new();
    private Button? _saveProjectBtn;
    private MenuItem? _saveProjectMenuItem;
    private bool _isForceClosing;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        Title = Strings.AppTitle;
        Width = 1380;
        Height = 840;
        MinWidth = 950;
        MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _mapControl = new MapViewportControl { ViewModel = _viewModel };

        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ProjectTitleText) or nameof(MainViewModel.TotalLayersCount) or nameof(MainViewModel.TotalTracksCount) or nameof(MainViewModel.TotalProjectsCount) or nameof(MainViewModel.CanSave) or nameof(MainViewModel.IsDirty))
            {
                UpdateProjectBadge();
            }
        };

        _viewModel.LanguageService.LanguageChanged += (_, _) =>
        {
            BuildUi();
            UpdateProjectBadge();
            _mapControl.InvalidateVisual();
        };

        Closing += async (_, e) =>
        {
            if (_viewModel.IsDirty && !_isForceClosing)
            {
                e.Cancel = true;
                bool proceed = await ConfirmAndHandleUnsavedChangesAsync();
                if (proceed)
                {
                    _isForceClosing = true;
                    Close();
                }
            }
        };

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            UpdateProjectBadge();
        };

        BuildUi();
        UpdateProjectBadge();
    }

    private void UpdateProjectBadge()
    {
        _projectBadgeText.Text = $"📁 {_viewModel.ProjectTitleText} ({_viewModel.TotalLayersCount} mapas, {_viewModel.TotalTracksCount} tracks)";
        Title = $"{Strings.AppTitle} - {_viewModel.ProjectTitleText}";
        if (_saveProjectBtn != null)
        {
            _saveProjectBtn.IsEnabled = _viewModel.CanSave;
        }
        if (_saveProjectMenuItem != null)
        {
            _saveProjectMenuItem.IsEnabled = _viewModel.CanSave;
        }
    }

    private void BuildUi()
    {
        DetachFromParent(_mapControl);
        DetachFromParent(_projectBadgeText);

        Grid rootGrid = new()
        {
            RowDefinitions = new RowDefinitions("Auto, Auto, *")
        };

        // Top Classic Menu Bar
        Control menuBar = CreateTopMenuBar();
        Grid.SetRow(menuBar, 0);
        rootGrid.Children.Add(menuBar);

        // Top Toolbar spanning across all sections
        Control toolbar = CreateTopToolbar();
        Grid.SetRow(toolbar, 1);
        rootGrid.Children.Add(toolbar);

        // Main content area with 3 sections:
        // Left: Tree and Search (280px)
        // Splitter 1 (4px)
        // Middle: Map Viewport Control with floating controls (*) - widest section
        // Splitter 2 (4px)
        // Right: Export Options Panel (320px)
        Grid mainGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("280, 4, *, 4, 320")
        };

        // Left Panel (Tree and Search)
        Control leftPanel = CreateLeftPanel();
        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // Splitter 1
        GridSplitter splitter1 = new()
        {
            Width = 4,
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(Color.FromRgb(225, 228, 234))
        };
        Grid.SetColumn(splitter1, 1);
        mainGrid.Children.Add(splitter1);

        // Middle Panel (Map Editor Canvas with Floating Zoom Controls)
        Panel middlePanel = new();
        middlePanel.Children.Add(_mapControl);

        StackPanel zoomControls = new()
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 16, 0)
        };

        Button zoomInBtn = CreateFloatingButton("+", Strings.ZoomInToolTip, () =>
        {
            if (!_viewModel.Viewport.CurrentExtent.IsEmpty)
            {
                _viewModel.Viewport.ZoomAt(_viewModel.Viewport.CenterGeoX, _viewModel.Viewport.CenterGeoY, 1.3);
                _mapControl.RequestRender();
            }
        });

        Button zoomOutBtn = CreateFloatingButton("−", Strings.ZoomOutToolTip, () =>
        {
            if (!_viewModel.Viewport.CurrentExtent.IsEmpty)
            {
                _viewModel.Viewport.ZoomAt(_viewModel.Viewport.CenterGeoX, _viewModel.Viewport.CenterGeoY, 1.0 / 1.3);
                _mapControl.RequestRender();
            }
        });

        Button zoomFitBtn = CreateFloatingButton("⛶", Strings.ZoomFitToolTip, () =>
        {
            _viewModel.ZoomToFullExtent();
            _mapControl.RequestRender();
        });

        zoomControls.Children.Add(zoomInBtn);
        zoomControls.Children.Add(zoomOutBtn);
        zoomControls.Children.Add(zoomFitBtn);

        middlePanel.Children.Add(zoomControls);

        // Bottom Loading Progress Overlay
        Border loadingOverlay = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 24),
            Background = new SolidColorBrush(Color.FromArgb(235, 24, 28, 36)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 8, 16, 10),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 12,
                Color = Color.FromArgb(100, 0, 0, 0),
                OffsetY = 4
            })
        };
        loadingOverlay.Bind(Visual.IsVisibleProperty, new Binding("Viewport.IsLoading"));

        StackPanel loadingStack = new()
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 220
        };

        TextBlock loadingText = new()
        {
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        loadingText.Bind(TextBlock.TextProperty, new Binding("Viewport.LoadingText"));

        ProgressBar loadingBar = new()
        {
            Height = 4,
            Minimum = 0,
            Maximum = 100,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246))
        };
        loadingBar.Bind(ProgressBar.IsIndeterminateProperty, new Binding("Viewport.IsIndeterminate"));
        loadingBar.Bind(ProgressBar.ValueProperty, new Binding("Viewport.ProgressValue"));

        loadingStack.Children.Add(loadingText);
        loadingStack.Children.Add(loadingBar);
        loadingOverlay.Child = loadingStack;

        middlePanel.Children.Add(loadingOverlay);

        Grid.SetColumn(middlePanel, 2);
        mainGrid.Children.Add(middlePanel);

        // Splitter 2
        GridSplitter splitter2 = new()
        {
            Width = 4,
            ResizeDirection = GridResizeDirection.Columns,
            Background = new SolidColorBrush(Color.FromRgb(225, 228, 234))
        };
        Grid.SetColumn(splitter2, 3);
        mainGrid.Children.Add(splitter2);

        // Right Panel (Export Options)
        Control rightPanel = CreateExportPanel();
        Grid.SetColumn(rightPanel, 4);
        mainGrid.Children.Add(rightPanel);

        Grid.SetRow(mainGrid, 2);
        rootGrid.Children.Add(mainGrid);

        Content = rootGrid;
    }

    private Control CreateTopMenuBar()
    {
        Menu menu = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        // File Menu
        MenuItem fileMenu = new()
        {
            Header = Strings.MenuFile
        };

        // New Project
        MenuItem newProjectItem = new()
        {
            Header = Strings.NewProject,
            InputGesture = new KeyGesture(Key.N, KeyModifiers.Control)
        };
        newProjectItem.Click += async (_, _) =>
        {
            if (await ConfirmAndHandleUnsavedChangesAsync())
            {
                _viewModel.NewProject();
            }
        };
        fileMenu.Items.Add(newProjectItem);

        // Open Project
        MenuItem openProjectItem = new()
        {
            Header = Strings.OpenProject,
            InputGesture = new KeyGesture(Key.O, KeyModifiers.Control)
        };
        openProjectItem.Click += async (_, _) => await PickAndOpenProjectFile();
        fileMenu.Items.Add(openProjectItem);

        // Save Project
        _saveProjectMenuItem = new MenuItem
        {
            Header = Strings.SaveProject,
            InputGesture = new KeyGesture(Key.S, KeyModifiers.Control),
            IsEnabled = _viewModel.CanSave
        };
        _saveProjectMenuItem.Click += async (_, _) => await SaveCurrentProject(promptIfNew: true);
        fileMenu.Items.Add(_saveProjectMenuItem);

        // Save Project As
        MenuItem saveProjectAsItem = new()
        {
            Header = Strings.SaveProjectAs,
            InputGesture = new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift)
        };
        saveProjectAsItem.Click += async (_, _) => await SaveCurrentProjectAs();
        fileMenu.Items.Add(saveProjectAsItem);

        fileMenu.Items.Add(new Separator());

        // Load Maps
        MenuItem loadMapsItem = new()
        {
            Header = Strings.LoadMaps
        };
        loadMapsItem.Click += async (_, _) => await PickMapFiles();
        fileMenu.Items.Add(loadMapsItem);

        // Load Tracks
        MenuItem loadTracksItem = new()
        {
            Header = Strings.LoadTracks
        };
        loadTracksItem.Click += async (_, _) => await PickTrackFiles();
        fileMenu.Items.Add(loadTracksItem);

        fileMenu.Items.Add(new Separator());

        // Exit
        MenuItem exitItem = new()
        {
            Header = Strings.MenuExit,
            InputGesture = new KeyGesture(Key.F4, KeyModifiers.Alt)
        };
        exitItem.Click += (_, _) => Close();
        fileMenu.Items.Add(exitItem);

        menu.Items.Add(fileMenu);

        MenuItem optionsMenu = new()
        {
            Header = Strings.MenuOptions
        };

        // Language Submenu
        MenuItem languageMenu = new()
        {
            Header = Strings.MenuLanguage
        };

        string currentLang = _viewModel.LanguageService.CurrentLanguage;
        bool isSpanish = string.Equals(currentLang, "es", StringComparison.OrdinalIgnoreCase);
        bool isEnglish = string.Equals(currentLang, "en", StringComparison.OrdinalIgnoreCase);

        MenuItem spanishItem = new()
        {
            Header = (isSpanish ? "✓ " : "    ") + Strings.LanguageSpanish
        };
        spanishItem.Click += (_, _) =>
        {
            _viewModel.LanguageService.SetLanguage("es");
            RebuildWindowUi();
        };

        MenuItem englishItem = new()
        {
            Header = (isEnglish ? "✓ " : "    ") + Strings.LanguageEnglish
        };
        englishItem.Click += (_, _) =>
        {
            _viewModel.LanguageService.SetLanguage("en");
            RebuildWindowUi();
        };

        languageMenu.Items.Add(spanishItem);
        languageMenu.Items.Add(englishItem);
        optionsMenu.Items.Add(languageMenu);

        // Directories Menu Item
        MenuItem directoriesItem = new()
        {
            Header = Strings.MenuDirectories
        };
        directoriesItem.Click += async (_, _) =>
        {
            TempDirectoriesWindow dialog = new(_viewModel.TempDirectoryService, msg =>
            {
                _viewModel.StatusMessage = msg;
            });
            await dialog.ShowDialog(this);
        };
        optionsMenu.Items.Add(directoriesItem);

        menu.Items.Add(optionsMenu);
        return menu;
    }

    private void RebuildWindowUi()
    {
        DetachFromParent(_mapControl);
        DetachFromParent(_projectBadgeText);
        Content = null;
        BuildUi();
        UpdateProjectBadge();
    }

    private static void DetachFromParent(Control? control)
    {
        if (control == null)
        {
            return;
        }

        if (control.Parent is Panel panel)
        {
            panel.Children.Remove(control);
        }
        else if (control.Parent is Decorator decorator)
        {
            decorator.Child = null;
        }
        else if (control.Parent is ContentControl contentControl)
        {
            contentControl.Content = null;
        }
    }

    private Control CreateTopToolbar()
    {
        Border toolbarBorder = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(222, 226, 230)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(6, 4)
        };

        DockPanel dockPanel = new();

        // Right side: Project Badge
        Border badge = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(235, 240, 248)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 212, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _projectBadgeText
        };
        _projectBadgeText.FontSize = 12;
        _projectBadgeText.FontWeight = FontWeight.SemiBold;
        _projectBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(30, 60, 110));

        DockPanel.SetDock(badge, Dock.Right);
        dockPanel.Children.Add(badge);

        // Left side: Toolbar buttons
        StackPanel buttonsPanel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        // New Project Button
        Button newProjectBtn = CreateToolbarButton("📄", Strings.NewProjectToolTip, async () =>
        {
            if (await ConfirmAndHandleUnsavedChangesAsync())
            {
                _viewModel.NewProject();
            }
        });
        buttonsPanel.Children.Add(newProjectBtn);

        // Open Project Button
        Button openProjectBtn = CreateToolbarButton("📂", Strings.OpenProjectToolTip, async () => await PickAndOpenProjectFile());
        buttonsPanel.Children.Add(openProjectBtn);

        // Save Project Button
        _saveProjectBtn = CreateToolbarButton("💾", Strings.SaveProjectToolTip, async () => await SaveCurrentProject(promptIfNew: true));
        _saveProjectBtn.IsEnabled = _viewModel.CanSave;
        buttonsPanel.Children.Add(_saveProjectBtn);

        dockPanel.Children.Add(buttonsPanel);
        toolbarBorder.Child = dockPanel;
        return toolbarBorder;
    }

    private static Button CreateToolbarButton(string icon, string toolTip, Action onClick)
    {
        Button btn = new()
        {
            Content = new TextBlock
            {
                Text = icon,
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        btn.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        btn.Resources["ButtonBorderBrushDisabled"] = Brushes.Transparent;
        btn.PropertyChanged += (_, e) =>
        {
            if (e.Property == Button.IsEnabledProperty)
            {
                btn.Opacity = btn.IsEnabled ? 1.0 : 0.4;
            }
        };
        btn.Opacity = btn.IsEnabled ? 1.0 : 0.4;
        ToolTip.SetTip(btn, toolTip);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Control CreateToolbarSeparator()
    {
        return new Border
        {
            Width = 1,
            Height = 20,
            Background = new SolidColorBrush(Color.FromRgb(215, 220, 230)),
            Margin = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Button CreateFloatingButton(string text, string toolTip, Action onClick)
    {
        Button btn = new()
        {
            Content = text,
            Width = 36,
            Height = 36,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(215, 220, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(btn, toolTip);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Control CreateLeftPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 253)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 234)),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        DockPanel dockPanel = new();

        // Search Bar at the top of the tree panel
        TextBox searchBox = new()
        {
            PlaceholderText = Strings.SearchTrackOrMapWatermark,
            Margin = new Thickness(10, 10, 10, 8),
            CornerRadius = new CornerRadius(6)
        };
        searchBox.TextChanged += (_, _) => _viewModel.SearchFilter = searchBox.Text ?? string.Empty;
        DockPanel.SetDock(searchBox, Dock.Top);
        dockPanel.Children.Add(searchBox);

        // TreeView showing loaded maps under Project root
        TreeView treeView = new()
        {
            ItemsSource = _viewModel.TreeRoots,
            SelectionMode = SelectionMode.Multiple,
            Margin = new Thickness(4, 0, 4, 4),
            ItemTemplate = new FuncTreeDataTemplate<MapTreeItemViewModel>((item, _) =>
            {
                StackPanel nodePanel = new()
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(2),
                    Background = Brushes.Transparent
                };

                CheckBox checkBox = new()
                {
                    IsChecked = item.IsChecked,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 2, 0)
                };

                bool updating = false;
                checkBox.IsCheckedChanged += (_, _) =>
                {
                    if (!updating)
                    {
                        updating = true;
                        item.IsChecked = checkBox.IsChecked == true;
                        updating = false;
                    }
                };

                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MapTreeItemViewModel.IsChecked) && !updating)
                    {
                        updating = true;
                        checkBox.IsChecked = item.IsChecked;
                        updating = false;
                    }
                };

                TextBlock icon = new() { Text = item.IconText, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };

                Panel titleContainer = new()
                {
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock title = new()
                {
                    Text = item.Title,
                    FontWeight = item.IsProject ? FontWeight.Bold : (item.IsFolder ? FontWeight.SemiBold : FontWeight.Normal),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsVisible = !item.IsEditing
                };

                TextBox editBox = new()
                {
                    Text = item.EditingTitle,
                    FontSize = 13,
                    Padding = new Thickness(2, 1),
                    Margin = new Thickness(0),
                    MinWidth = 80,
                    IsVisible = item.IsEditing,
                    VerticalAlignment = VerticalAlignment.Center
                };

                titleContainer.Children.Add(title);
                titleContainer.Children.Add(editBox);

                TextBlock details = new() { Text = $"({item.DetailsText})", FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };

                void CommitEdit()
                {
                    if (item.IsEditing)
                    {
                        string newText = editBox.Text ?? string.Empty;
                        item.IsEditing = false;
                        if (!string.IsNullOrWhiteSpace(newText) && item.Project != null)
                        {
                            _viewModel.RenameProject(item, newText);
                            UpdateProjectBadge();
                        }
                    }
                }

                editBox.KeyDown += (_, ke) =>
                {
                    if (ke.Key == Key.Enter)
                    {
                        CommitEdit();
                        ke.Handled = true;
                    }
                    else if (ke.Key == Key.Escape)
                    {
                        item.CancelEdit();
                        ke.Handled = true;
                    }
                };

                editBox.LostFocus += (_, _) =>
                {
                    CommitEdit();
                };

                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MapTreeItemViewModel.IsEditing))
                    {
                        title.IsVisible = !item.IsEditing;
                        editBox.IsVisible = item.IsEditing;
                        details.IsVisible = !item.IsEditing;
                        if (item.IsEditing)
                        {
                            editBox.Text = item.EditingTitle;
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                editBox.Focus();
                                editBox.SelectAll();
                            });
                        }
                    }
                    else if (e.PropertyName == nameof(MapTreeItemViewModel.Title))
                    {
                        title.Text = item.Title;
                    }
                    else if (e.PropertyName == nameof(MapTreeItemViewModel.DetailsText))
                    {
                        details.Text = $"({item.DetailsText})";
                    }
                };

                bool wasSelectedOnPointerPress = false;
                title.PointerPressed += (_, pe) =>
                {
                    if (pe.GetCurrentPoint(title).Properties.IsLeftButtonPressed)
                    {
                        wasSelectedOnPointerPress = (_viewModel.SelectedTreeItem == item);
                    }
                };

                title.Tapped += (_, _) =>
                {
                    if (wasSelectedOnPointerPress && item.IsProject && !item.IsEditing)
                    {
                        item.BeginEdit();
                    }
                };

                nodePanel.Children.Add(checkBox);
                nodePanel.Children.Add(icon);
                nodePanel.Children.Add(titleContainer);
                nodePanel.Children.Add(details);

                // Right-click context menu
                ContextMenu contextMenu = new();
                MenuItem zoomMenuItem = new()
                {
                    Header = Strings.ZoomHere
                };
                zoomMenuItem.Click += (_, _) => _viewModel.ZoomToTreeItem(item);
                contextMenu.Items.Add(zoomMenuItem);

                if (!string.IsNullOrWhiteSpace(item.FullPath))
                {
                    MenuItem openFolderMenuItem = new()
                    {
                        Header = Strings.OpenContainingFolder
                    };
                    openFolderMenuItem.Click += (_, _) => FileExplorerHelper.OpenFolder(item.FullPath!);
                    contextMenu.Items.Add(openFolderMenuItem);
                }

                if (item.IsProject)
                {
                    MenuItem renameMenuItem = new()
                    {
                        Header = Strings.Rename
                    };
                    renameMenuItem.Click += (_, _) => item.BeginEdit();
                    contextMenu.Items.Add(renameMenuItem);

                    MenuItem closeProjectMenuItem = new()
                    {
                        Header = Strings.CloseProject
                    };
                    closeProjectMenuItem.Click += async (_, _) =>
                    {
                        if (await ConfirmAndHandleUnsavedChangesAsync())
                        {
                            _viewModel.CloseProject(item.Project);
                        }
                    };
                    contextMenu.Items.Add(closeProjectMenuItem);
                }
                else if (item.IsMapLayer)
                {
                    MenuItem closeMapMenuItem = new()
                    {
                        Header = Strings.CloseMap
                    };
                    closeMapMenuItem.Click += (_, _) => _viewModel.CloseMapLayer(item);
                    contextMenu.Items.Add(closeMapMenuItem);

                    MenuItem closeSelectedMapsMenuItem = new()
                    {
                        Header = Strings.CloseSelectedMaps
                    };
                    closeSelectedMapsMenuItem.Click += (_, _) => _viewModel.CloseSelectedMaps(item);
                    contextMenu.Items.Add(closeSelectedMapsMenuItem);
                }
                else if (item.IsTrack)
                {
                    MenuItem closeTrackMenuItem = new()
                    {
                        Header = Strings.CloseTrack
                    };
                    closeTrackMenuItem.Click += (_, _) => _viewModel.CloseTrack(item);
                    contextMenu.Items.Add(closeTrackMenuItem);
                }
                else if (item.IsTracksFolder)
                {
                    MenuItem loadTracksMenuItem = new()
                    {
                        Header = Strings.LoadTracks
                    };
                    loadTracksMenuItem.Click += async (_, _) => await PickTrackFiles(item.Project);
                    contextMenu.Items.Add(loadTracksMenuItem);
                }
                else if (item.IsMapsFolder)
                {
                    MenuItem loadMapsMenuItem = new()
                    {
                        Header = Strings.LoadMaps
                    };
                    loadMapsMenuItem.Click += async (_, _) => await PickMapFiles(item.Project);
                    contextMenu.Items.Add(loadMapsMenuItem);

                    MenuItem closeSelectedMapsMenuItem = new()
                    {
                        Header = Strings.CloseSelectedMaps
                    };
                    closeSelectedMapsMenuItem.Click += (_, _) => _viewModel.CloseSelectedMaps();
                    contextMenu.Items.Add(closeSelectedMapsMenuItem);
                }

                nodePanel.ContextMenu = contextMenu;

                return nodePanel;
            }, item => item.Children)
        };

        treeView.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters =
            {
                new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(MapTreeItemViewModel.IsExpanded)) { Mode = BindingMode.TwoWay }),
                new Setter(TreeViewItem.IsSelectedProperty, new Binding(nameof(MapTreeItemViewModel.IsSelected)) { Mode = BindingMode.TwoWay })
            }
        });

        treeView.SelectionChanged += (_, _) =>
        {
            var selectedList = treeView.SelectedItems?.OfType<MapTreeItemViewModel>().ToList() ?? new List<MapTreeItemViewModel>();
            _viewModel.UpdateSelectedTreeItems(selectedList);
        };

        treeView.DoubleTapped += (_, _) =>
        {
            if (treeView.SelectedItem is MapTreeItemViewModel selected)
            {
                _viewModel.ZoomToTreeItem(selected);
            }
        };

        treeView.KeyDown += (_, e) =>
        {
            if (e.Key == Key.F2 && treeView.SelectedItem is MapTreeItemViewModel selected && selected.IsProject)
            {
                selected.BeginEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                if (_viewModel.SelectedTreeItems.Any(i => i.IsMapLayer))
                {
                    _viewModel.CloseSelectedMaps();
                    e.Handled = true;
                }
                else if (treeView.SelectedItem is MapTreeItemViewModel sel)
                {
                    if (sel.IsMapLayer)
                    {
                        _viewModel.CloseMapLayer(sel);
                        e.Handled = true;
                    }
                    else if (sel.IsTrack)
                    {
                        _viewModel.CloseTrack(sel);
                        e.Handled = true;
                    }
                }
            }
        };

        dockPanel.Children.Add(treeView);
        border.Child = dockPanel;
        return border;
    }

    private Control CreateExportPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 253)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 234)),
            BorderThickness = new Thickness(1, 0, 0, 0)
        };

        DockPanel dockPanel = new();

        // Header
        Border headerBorder = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 234)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 10)
        };

        StackPanel headerPanel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(new TextBlock
        {
            Text = "📦",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = Strings.ExportOptionsTitle,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 60, 110)),
            VerticalAlignment = VerticalAlignment.Center
        });
        headerBorder.Child = headerPanel;
        DockPanel.SetDock(headerBorder, Dock.Top);
        dockPanel.Children.Add(headerBorder);

        // Content panel inside ScrollViewer
        StackPanel contentPanel = new()
        {
            Margin = new Thickness(12),
            Spacing = 10
        };

        // --- Card 1: Modo de exportación de área ---
        StackPanel areaCardContent = new() { Spacing = 6 };
        areaCardContent.Children.Add(new TextBlock
        {
            Text = Strings.ExportAreaGroup,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 50, 70))
        });

        ComboBox modeComboBox = new()
        {
            ItemsSource = _viewModel.Export.AvailableModes,
            SelectedItem = _viewModel.Export.SelectedModeOption,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 12
        };

        modeComboBox.SelectionChanged += (_, _) =>
        {
            if (modeComboBox.SelectedItem is ExportAreaModeOption option)
            {
                _viewModel.Export.SelectedModeOption = option;
            }
        };
        areaCardContent.Children.Add(modeComboBox);

        // Subpanel for Track Contour
        StackPanel trackPanel = new() { Spacing = 6 };
        trackPanel.IsVisible = _viewModel.Export.IsTrackMode;

        ComboBox trackComboBox = new()
        {
            ItemsSource = _viewModel.Export.AvailableTracks,
            SelectedItem = _viewModel.Export.SelectedTrackOption,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 12
        };

        trackComboBox.SelectionChanged += (_, _) =>
        {
            if (trackComboBox.SelectedItem is TrackExportOption option)
            {
                _viewModel.Export.SelectedTrackOption = option;
            }
        };

        trackPanel.Children.Add(trackComboBox);

        TextBox marginTextBox = new()
        {
            Text = _viewModel.Export.TrackMargin.ToString(CultureInfo.InvariantCulture),
            PlaceholderText = "100",
            FontSize = 12,
            Width = 45,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        marginTextBox.TextChanged += (_, _) =>
        {
            if (double.TryParse(marginTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double margin) ||
                double.TryParse(marginTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out margin))
            {
                _viewModel.Export.TrackMargin = margin;
            }
        };

        trackPanel.Children.Add(CreateSettingsGrid((Strings.ExportTrackMarginLabel, marginTextBox)));
        areaCardContent.Children.Add(trackPanel);

        // Subpanel for Selected Tiles
        StackPanel tilesPanel = new() { Spacing = 6 };
        tilesPanel.IsVisible = _viewModel.Export.IsSelectedTilesMode;

        DockPanel counterDock = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 2, 0, 0) };
        StackPanel tileButtonsPanel = new() { Orientation = Orientation.Horizontal, Spacing = 4 };

        Button optimizeTilesBtn = new()
        {
            Content = Strings.ExportOptimizeSelectedTiles,
            FontSize = 11,
            Padding = new Thickness(6, 2)
        };
        optimizeTilesBtn.Click += (_, _) => _viewModel.Export.OptimizeSelectedTiles();

        Button clearTilesBtn = new()
        {
            Content = Strings.ExportClearSelectedTiles,
            FontSize = 11,
            Padding = new Thickness(6, 2)
        };
        clearTilesBtn.Click += (_, _) => _viewModel.Export.ClearSelectedTiles();

        tileButtonsPanel.Children.Add(optimizeTilesBtn);
        tileButtonsPanel.Children.Add(clearTilesBtn);
        DockPanel.SetDock(tileButtonsPanel, Dock.Right);

        TextBlock counterTextBlock = new()
        {
            Text = _viewModel.Export.SelectedTilesCounterText,
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215))
        };

        counterDock.Children.Add(tileButtonsPanel);
        counterDock.Children.Add(counterTextBlock);
        tilesPanel.Children.Add(counterDock);

        CheckBox selectTrackTilesCheckBox = new()
        {
            Content = Strings.ExportSelectTrackTiles,
            IsChecked = _viewModel.Export.SelectTrackTiles,
            FontSize = 12
        };
        selectTrackTilesCheckBox.IsCheckedChanged += (_, _) => _viewModel.Export.SelectTrackTiles = selectTrackTilesCheckBox.IsChecked ?? false;
        tilesPanel.Children.Add(selectTrackTilesCheckBox);

        StackPanel trackTilesSubPanel = new() { Spacing = 6, Margin = new Thickness(16, 0, 0, 0) };
        trackTilesSubPanel.IsVisible = _viewModel.Export.SelectTrackTiles;

        Grid trackComboGrid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        ComboBox tilesTrackComboBox = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = _viewModel.Export.AvailableTracks,
            SelectedItem = _viewModel.Export.SelectedTilesTrackOption,
            FontSize = 12
        };
        tilesTrackComboBox.SelectionChanged += (_, _) =>
        {
            if (tilesTrackComboBox.SelectedItem is TrackExportOption opt)
            {
                _viewModel.Export.SelectedTilesTrackOption = opt;
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
        selectTilesBtn.Click += (_, _) => _viewModel.Export.SelectTilesForTrack();

        Grid.SetColumn(tilesTrackComboBox, 0);
        Grid.SetColumn(selectTilesBtn, 1);
        trackComboGrid.Children.Add(tilesTrackComboBox);
        trackComboGrid.Children.Add(selectTilesBtn);
        trackTilesSubPanel.Children.Add(trackComboGrid);

        TextBox tilesMarginTextBox = new()
        {
            Text = _viewModel.Export.SelectedTilesTrackMargin.ToString(CultureInfo.InvariantCulture),
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
                _viewModel.Export.SelectedTilesTrackMargin = margin;
            }
        };
        trackTilesSubPanel.Children.Add(CreateSettingsGrid((Strings.ExportTrackMarginLabel, tilesMarginTextBox)));

        tilesPanel.Children.Add(trackTilesSubPanel);

        CheckBox lockTileSizeCheckBox = new()
        {
            Content = Strings.ExportLockTileSize,
            IsChecked = _viewModel.Export.LockTileSize,
            FontSize = 12
        };
        lockTileSizeCheckBox.IsCheckedChanged += (_, _) => _viewModel.Export.LockTileSize = lockTileSizeCheckBox.IsChecked ?? false;
        tilesPanel.Children.Add(lockTileSizeCheckBox);

        areaCardContent.Children.Add(tilesPanel);

        _viewModel.Export.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MapExportViewModel.SelectedModeOption) or nameof(MapExportViewModel.SelectedMode))
            {
                trackPanel.IsVisible = _viewModel.Export.IsTrackMode;
                tilesPanel.IsVisible = _viewModel.Export.IsSelectedTilesMode;
                if (modeComboBox.SelectedItem != _viewModel.Export.SelectedModeOption)
                {
                    modeComboBox.SelectedItem = _viewModel.Export.SelectedModeOption;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesCounterText))
            {
                counterTextBlock.Text = _viewModel.Export.SelectedTilesCounterText;
            }
            else if (e.PropertyName is nameof(MapExportViewModel.SelectTrackTiles) or nameof(MapExportViewModel.IsTrackTilesSubPanelVisible))
            {
                if (selectTrackTilesCheckBox.IsChecked != _viewModel.Export.SelectTrackTiles)
                {
                    selectTrackTilesCheckBox.IsChecked = _viewModel.Export.SelectTrackTiles;
                }
                trackTilesSubPanel.IsVisible = _viewModel.Export.SelectTrackTiles;
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesTrackOption))
            {
                if (tilesTrackComboBox.SelectedItem != _viewModel.Export.SelectedTilesTrackOption)
                {
                    tilesTrackComboBox.SelectedItem = _viewModel.Export.SelectedTilesTrackOption;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.LockTileSize))
            {
                if (lockTileSizeCheckBox.IsChecked != _viewModel.Export.LockTileSize)
                {
                    lockTileSizeCheckBox.IsChecked = _viewModel.Export.LockTileSize;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTrackOption))
            {
                if (trackComboBox.SelectedItem != _viewModel.Export.SelectedTrackOption)
                {
                    trackComboBox.SelectedItem = _viewModel.Export.SelectedTrackOption;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.TrackMargin))
            {
                string text = _viewModel.Export.TrackMargin.ToString(CultureInfo.InvariantCulture);
                if (marginTextBox.Text != text)
                {
                    marginTextBox.Text = text;
                }
            }
            else if (e.PropertyName == nameof(MapExportViewModel.SelectedTilesTrackMargin))
            {
                string text = _viewModel.Export.SelectedTilesTrackMargin.ToString(CultureInfo.InvariantCulture);
                if (tilesMarginTextBox.Text != text)
                {
                    tilesMarginTextBox.Text = text;
                }
            }
        };

        contentPanel.Children.Add(CreateCard(areaCardContent));

        // --- Card 3: Configuración de salida ---
        StackPanel outputSettingsCard = new() { Spacing = 8 };
        outputSettingsCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportOutputSettingsGroup,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 50, 70))
        });

        NumericUpDown tileSizeNumeric = new()
        {
            Value = _viewModel.Export.TileSize,
            Minimum = 256,
            Maximum = 4096,
            Increment = 256,
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        tileSizeNumeric.ValueChanged += (_, _) => _viewModel.Export.TileSize = (int)(tileSizeNumeric.Value ?? 1024);

        NumericUpDown maxTilesNumeric = new()
        {
            Value = _viewModel.Export.MaxTiles,
            Minimum = 1,
            Maximum = 1000,
            Increment = 25,
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        maxTilesNumeric.ValueChanged += (_, _) => _viewModel.Export.MaxTiles = (int)(maxTilesNumeric.Value ?? 250);

        NumericUpDown qualityNumeric = new()
        {
            Value = _viewModel.Export.Quality,
            Minimum = 1,
            Maximum = 100,
            Increment = 5,
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        qualityNumeric.ValueChanged += (_, _) => _viewModel.Export.Quality = (int)(qualityNumeric.Value ?? 100);

        Grid outputGrid = CreateSettingsGrid(
            (Strings.ExportTileSizeLabel, tileSizeNumeric),
            (Strings.ExportMaxTilesLabel, maxTilesNumeric),
            (Strings.ExportJpegQualityLabel, qualityNumeric));
        outputSettingsCard.Children.Add(outputGrid);
        contentPanel.Children.Add(CreateCard(outputSettingsCard));

        // --- Card 4: Ajustes de brillo ---
        StackPanel brightnessCard = new() { Spacing = 8 };
        brightnessCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportBrightnessGroup,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 50, 70))
        });

        brightnessCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportBrightnessDescription,
            FontSize = 11,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 105))
        });

        CheckBox brightnessCheck = new()
        {
            Content = Strings.ExportBrightnessEnable,
            IsChecked = _viewModel.Export.BrightnessEnabled,
            FontSize = 12
        };

        NumericUpDown gammaNumeric = new()
        {
            Value = (decimal)_viewModel.Export.Gamma,
            Minimum = (decimal)0.1,
            Maximum = (decimal)10.0,
            Increment = (decimal)0.1,
            FormatString = "0.0",
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = _viewModel.Export.BrightnessEnabled
        };
        gammaNumeric.ValueChanged += (_, _) => _viewModel.Export.Gamma = (double)(gammaNumeric.Value ?? 1.6m);

        NumericUpDown brightnessPercentNumeric = new()
        {
            Value = (decimal)_viewModel.Export.Brightness,
            Minimum = (decimal)0.0,
            Maximum = (decimal)100.0,
            Increment = (decimal)1.0,
            FormatString = "0.0",
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = _viewModel.Export.BrightnessEnabled
        };
        brightnessPercentNumeric.ValueChanged += (_, _) => _viewModel.Export.Brightness = (double)(brightnessPercentNumeric.Value ?? 8.0m);

        NumericUpDown contrastNumeric = new()
        {
            Value = (decimal)_viewModel.Export.Contrast,
            Minimum = (decimal)0.1,
            Maximum = (decimal)10.0,
            Increment = (decimal)0.1,
            FormatString = "0.0",
            ShowButtonSpinner = false,
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = _viewModel.Export.BrightnessEnabled
        };
        contrastNumeric.ValueChanged += (_, _) => _viewModel.Export.Contrast = (double)(contrastNumeric.Value ?? 1.0m);

        brightnessCheck.IsCheckedChanged += (_, _) =>
        {
            bool enabled = brightnessCheck.IsChecked == true;
            _viewModel.Export.BrightnessEnabled = enabled;
            gammaNumeric.IsEnabled = enabled;
            brightnessPercentNumeric.IsEnabled = enabled;
            contrastNumeric.IsEnabled = enabled;
        };

        Grid brightnessGrid = CreateSettingsGrid(
            (Strings.ExportGammaLabel, gammaNumeric),
            (Strings.ExportBrightnessPercentLabel, brightnessPercentNumeric),
            (Strings.ExportContrastLabel, contrastNumeric));

        brightnessCard.Children.Add(brightnessCheck);
        brightnessCard.Children.Add(brightnessGrid);
        brightnessCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportBrightnessExplanation,
            FontSize = 11,
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 115))
        });
        contentPanel.Children.Add(CreateCard(brightnessCard));

        // --- Panel inferior fijo: Destino del archivo, Acción y Progreso ---
        Border actionBorder = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 228, 234)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 10)
        };
        DockPanel.SetDock(actionBorder, Dock.Bottom);
        dockPanel.Children.Add(actionBorder);

        StackPanel actionCard = new() { Spacing = 6 };

        actionCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportDestinationGroup,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 50, 70))
        });

        actionCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportDirectoryLabel,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 115))
        });

        TextBox outputDirBox = new()
        {
            Text = _viewModel.Export.OutputDirectory,
            PlaceholderText = Strings.ExportDirectoryWatermark,
            FontSize = 12
        };
        outputDirBox.TextChanged += (_, _) => _viewModel.Export.OutputDirectory = outputDirBox.Text ?? string.Empty;

        Button browseBtn = new()
        {
            Content = Strings.KmzExportBrowse,
            Padding = new Thickness(10, 5),
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 12
        };
        browseBtn.Click += async (_, _) =>
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Strings.ExportSelectFolderDialogTitle,
                AllowMultiple = false
            });
            if (folders.Count > 0)
            {
                _viewModel.Export.OutputDirectory = folders[0].Path.LocalPath;
                outputDirBox.Text = _viewModel.Export.OutputDirectory;
            }
        };

        DockPanel dirDock = new();
        DockPanel.SetDock(browseBtn, Dock.Right);
        dirDock.Children.Add(browseBtn);
        dirDock.Children.Add(outputDirBox);
        actionCard.Children.Add(dirDock);

        actionCard.Children.Add(new TextBlock
        {
            Text = Strings.ExportKmzNameLabel,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 115)),
            Margin = new Thickness(0, 4, 0, 0)
        });

        TextBox kmzNameBox = new()
        {
            Text = _viewModel.Export.KmzFileName,
            PlaceholderText = Strings.ExportKmzNameWatermark,
            FontSize = 12
        };
        kmzNameBox.TextChanged += (_, _) => _viewModel.Export.KmzFileName = kmzNameBox.Text ?? string.Empty;
        actionCard.Children.Add(kmzNameBox);

        Button exportBtn = new()
        {
            Content = new TextBlock
            {
                Text = "🚀 " + Strings.KmzExportButton,
                FontWeight = FontWeight.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center
            },
            Background = new SolidColorBrush(Color.FromRgb(30, 96, 180)),
            Foreground = Brushes.White,
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(6)
        };

        Button cancelBtn = new()
        {
            Content = Strings.ExportCancelButton,
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(6),
            IsVisible = _viewModel.Export.IsExporting
        };

        StackPanel progressSection = new()
        {
            Spacing = 4,
            IsVisible = _viewModel.Export.IsExporting
        };

        TextBlock stepLabelText = new()
        {
            Text = _viewModel.Export.StepLabel,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(40, 50, 70))
        };

        ProgressBar totalStepsProgressBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Value = _viewModel.Export.TotalProgressValue
        };

        Grid stepHeaderGrid = new()
        {
            Margin = new Thickness(0, 2, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        TextBlock stepDescriptionText = new()
        {
            Text = _viewModel.Export.StepDescription,
            FontSize = 11,
            Opacity = 0.85,
            Foreground = new SolidColorBrush(Color.FromRgb(50, 60, 75)),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock stepPercentageText = new()
        {
            Text = _viewModel.Export.StepPercentageText,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.85,
            Foreground = new SolidColorBrush(Color.FromRgb(50, 60, 75)),
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            [Grid.ColumnProperty] = 1,
            Margin = new Thickness(6, 0, 0, 0)
        };

        stepHeaderGrid.Children.Add(stepDescriptionText);
        stepHeaderGrid.Children.Add(stepPercentageText);

        ProgressBar stepDetailProgressBar = new()
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Value = _viewModel.Export.StepProgressValue
        };

        progressSection.Children.Add(stepLabelText);
        progressSection.Children.Add(totalStepsProgressBar);
        progressSection.Children.Add(stepHeaderGrid);
        progressSection.Children.Add(stepDetailProgressBar);

        TextBlock statusText = new()
        {
            Text = _viewModel.Export.StatusMessage,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 105))
        };

        exportBtn.Click += async (_, _) => await _viewModel.Export.ExportKmzAsync();
        cancelBtn.Click += (_, _) => _viewModel.Export.CancelExport();

        actionCard.Children.Add(exportBtn);
        actionCard.Children.Add(cancelBtn);
        actionCard.Children.Add(progressSection);
        actionCard.Children.Add(statusText);
        actionBorder.Child = actionCard;

        // ViewModel property change sync
        _viewModel.Export.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MapExportViewModel.OutputDirectory):
                    if (outputDirBox.Text != _viewModel.Export.OutputDirectory)
                    {
                        outputDirBox.Text = _viewModel.Export.OutputDirectory;
                    }
                    break;
                case nameof(MapExportViewModel.KmzFileName):
                    if (kmzNameBox.Text != _viewModel.Export.KmzFileName)
                    {
                        kmzNameBox.Text = _viewModel.Export.KmzFileName;
                    }
                    break;
                case nameof(MapExportViewModel.SelectedTrackOption):
                    if (trackComboBox.SelectedItem != _viewModel.Export.SelectedTrackOption)
                    {
                        trackComboBox.SelectedItem = _viewModel.Export.SelectedTrackOption;
                    }
                    break;
                case nameof(MapExportViewModel.TrackMargin):
                    string marginStr = _viewModel.Export.TrackMargin.ToString(CultureInfo.InvariantCulture);
                    if (marginTextBox.Text != marginStr && double.TryParse(marginTextBox.Text, out double curMargin) && Math.Abs(curMargin - _viewModel.Export.TrackMargin) > 1e-4)
                    {
                        marginTextBox.Text = marginStr;
                    }
                    break;
                case nameof(MapExportViewModel.IsExporting):
                    exportBtn.IsEnabled = !_viewModel.Export.IsExporting;
                    cancelBtn.IsVisible = _viewModel.Export.IsExporting;
                    cancelBtn.IsEnabled = !_viewModel.Export.IsCancelling;
                    progressSection.IsVisible = _viewModel.Export.IsExporting;
                    break;
                case nameof(MapExportViewModel.IsCancelling):
                    cancelBtn.IsEnabled = !_viewModel.Export.IsCancelling;
                    break;
                case nameof(MapExportViewModel.TotalProgressValue):
                    totalStepsProgressBar.Value = _viewModel.Export.TotalProgressValue;
                    break;
                case nameof(MapExportViewModel.StepProgressValue):
                    stepDetailProgressBar.Value = _viewModel.Export.StepProgressValue;
                    break;
                case nameof(MapExportViewModel.StepLabel):
                    stepLabelText.Text = _viewModel.Export.StepLabel;
                    break;
                case nameof(MapExportViewModel.StepDescription):
                    stepDescriptionText.Text = _viewModel.Export.StepDescription;
                    break;
                case nameof(MapExportViewModel.StepPercentageText):
                    stepPercentageText.Text = _viewModel.Export.StepPercentageText;
                    break;
                case nameof(MapExportViewModel.StatusMessage):
                    statusText.Text = _viewModel.Export.StatusMessage;
                    break;
                case nameof(MapExportViewModel.HasStatusError):
                case nameof(MapExportViewModel.HasStatusSuccess):
                    if (_viewModel.Export.HasStatusError)
                    {
                        statusText.Foreground = new SolidColorBrush(Color.FromRgb(210, 40, 40));
                    }
                    else if (_viewModel.Export.HasStatusSuccess)
                    {
                        statusText.Foreground = new SolidColorBrush(Color.FromRgb(25, 140, 50));
                    }
                    else
                    {
                        statusText.Foreground = new SolidColorBrush(Color.FromRgb(80, 90, 105));
                    }
                    break;
            }
        };

        ScrollViewer scrollViewer = new()
        {
            Content = contentPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        dockPanel.Children.Add(scrollViewer);
        border.Child = dockPanel;
        return border;
    }

    private static Border CreateCard(Control content)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 230, 237)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = content
        };
    }

    private static Grid CreateSettingsGrid(params (string label, Control control)[] fields)
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 8
        };

        for (int i = 0; i < fields.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            TextBlock label = new()
            {
                Text = fields[i].label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 100, 115)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            Grid.SetRow(fields[i].control, i);
            Grid.SetColumn(fields[i].control, 1);
            grid.Children.Add(fields[i].control);
        }

        return grid;
    }

    private async Task PickMapFiles(MapProject? project = null)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.OpenRasterDialogTitle,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.GeoTiffFilterName) { Patterns = GdalHelper.SupportedSearchPatterns },
                new FilePickerFileType(Strings.AllFilesFilterName) { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
        {
            var paths = files.Select(f => f.Path.LocalPath).Where(File.Exists).ToList();
            await _viewModel.AddMapLayersAsync(paths, project);
        }
    }

    private async Task PickTrackFiles(MapProject? project = null)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.OpenTrackDialogTitle,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.GpxFilterName) { Patterns = ["*.gpx"] },
                new FilePickerFileType(Strings.AllFilesFilterName) { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
        {
            var paths = files.Select(f => f.Path.LocalPath).Where(File.Exists).ToList();
            await _viewModel.AddTrackFilesAsync(paths, project);
        }
    }

    private async Task<bool> ConfirmAndHandleUnsavedChangesAsync()
    {
        if (!_viewModel.IsDirty)
        {
            return true;
        }

        SaveConfirmationDialog dialog = new(_viewModel.ProjectName);
        await dialog.ShowDialog(this);

        switch (dialog.Result)
        {
            case SavePromptResult.Save:
                return await SaveCurrentProject(promptIfNew: true);
            case SavePromptResult.Discard:
                return true;
            case SavePromptResult.Cancel:
            default:
                return false;
        }
    }

    private async Task PickAndOpenProjectFile()
    {
        if (!await ConfirmAndHandleUnsavedChangesAsync())
        {
            return;
        }

        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.OpenProjectDialogTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.ProjectFilterName) { Patterns = ["*.ttk"] }
            ]
        });

        if (files.Count > 0)
        {
            string path = files[0].Path.LocalPath;
            if (File.Exists(path))
            {
                await _viewModel.OpenProjectAsync(path);
                UpdateProjectBadge();
            }
        }
    }

    private async Task<bool> SaveCurrentProjectAs()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            return false;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.SaveProjectDialogTitle,
            DefaultExtension = "ttk",
            SuggestedFileName = _viewModel.ProjectName,
            FileTypeChoices =
            [
                new FilePickerFileType(Strings.ProjectFilterName) { Patterns = ["*.ttk"] }
            ]
        });

        if (file != null)
        {
            bool success = await _viewModel.SaveProjectAsync(file.Path.LocalPath);
            UpdateProjectBadge();
            return success;
        }

        return false;
    }

    private async Task<bool> SaveCurrentProject(bool promptIfNew)
    {
        if (string.IsNullOrEmpty(_viewModel.ProjectFilePath) && promptIfNew)
        {
            return await SaveCurrentProjectAs();
        }
        else
        {
            bool success = await _viewModel.SaveProjectAsync();
            UpdateProjectBadge();
            return success;
        }
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null)
        {
            return;
        }

        List<string> paths = files.Select(f => f.Path.LocalPath).ToList();
        List<string> projectFiles = paths.Where(p =>
            p.EndsWith(".ttk", StringComparison.OrdinalIgnoreCase)).ToList();

        if (projectFiles.Count > 0)
        {
            if (await ConfirmAndHandleUnsavedChangesAsync())
            {
                await _viewModel.OpenProjectAsync(projectFiles[0]);
                UpdateProjectBadge();
            }
            return;
        }

        List<string> mapFiles = paths.Where(GdalHelper.IsSupportedRasterFile).ToList();
        if (mapFiles.Count > 0)
        {
            await _viewModel.AddMapFilesAsync(mapFiles);
        }

        List<string> trackFiles = paths.Where(p => p.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase)).ToList();
        if (trackFiles.Count > 0)
        {
            await _viewModel.AddTrackFilesAsync(trackFiles);
        }

        List<string> folders = paths.Where(Directory.Exists).ToList();
        foreach (string folder in folders)
        {
            await _viewModel.AddMapFolderAsync(folder);
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }
}
