using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using TifToKmzMapEditor.Commands;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.Services;

namespace TifToKmzMapEditor.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IMapProjectService _projectService;
    private readonly IGeoRasterRenderer _rasterRenderer;
    private readonly ITempDirectoryService _tempDirectoryService;
    private readonly ILanguageService _languageService;
    private MapLayerViewModel? _selectedLayer;
    private MapTreeItemViewModel? _selectedTreeItem;
    private string _searchFilter = string.Empty;
    private string _statusMessage = Strings.StatusReady;
    private bool _isLoading;

    public IMapProjectService ProjectService => _projectService;
    public IGeoRasterRenderer RasterRenderer => _rasterRenderer;
    public ITempDirectoryService TempDirectoryService => _tempDirectoryService;
    public ILanguageService LanguageService => _languageService;
    public MapViewportViewModel Viewport { get; } = new();
    public MapExportViewModel Export { get; }

    public ObservableCollection<MapTreeItemViewModel> TreeRoots { get; } = new();
    public ObservableCollection<MapTreeItemViewModel> SelectedTreeItems { get; } = new();
    public ObservableCollection<MapLayerViewModel> LayerViewModels { get; } = new();

    public string ProjectName => _projectService.ProjectName;
    public string? ProjectFilePath => _projectService.ProjectFilePath;
    public string ProjectTitleText => _projectService.ActiveProject?.DisplayName
        ?? (string.IsNullOrWhiteSpace(_projectService.ProjectName) ? Strings.ProjectDefaultTitle : _projectService.ProjectName);

    public bool CanSave => _projectService.ActiveProject?.IsDirty == true;
    public bool IsDirty => _projectService.ActiveProject?.IsDirty == true;

    public void MarkProjectDirty()
    {
        if (_projectService.ActiveProject != null && !_projectService.ActiveProject.IsDirty)
        {
            _projectService.ActiveProject.IsDirty = true;
        }

        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(ProjectTitleText));
        OnPropertyChanged(nameof(ProjectName));
        if (TreeRoots.Count > 0)
        {
            TreeRoots[0].NotifyTitleChanged();
        }
    }

    public MapLayerViewModel? SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (SetField(ref _selectedLayer, value))
            {
                OnPropertyChanged(nameof(HasSelectedLayer));
            }
        }
    }

    public bool HasSelectedLayer => _selectedLayer != null;

    public MapTreeItemViewModel? SelectedTreeItem
    {
        get => _selectedTreeItem;
        set
        {
            if (SetField(ref _selectedTreeItem, value))
            {
                if (_selectedTreeItem?.Layer != null)
                {
                    SelectedLayer = LayerViewModels.FirstOrDefault(l => l.Layer == _selectedTreeItem.Layer);
                }

                if (_selectedTreeItem?.Project != null)
                {
                    _projectService.ActiveProject = _selectedTreeItem.Project;
                    OnPropertyChanged(nameof(ProjectName));
                    OnPropertyChanged(nameof(ProjectTitleText));
                }
            }
        }
    }

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetField(ref _searchFilter, value))
            {
                RefreshTree();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public int TotalLayersCount => _projectService.Layers.Count;
    public int TotalTracksCount => _projectService.Tracks.Count;
    public int TotalProjectsCount => _projectService.Projects.Count;
    public string LayerStatsText => TotalLayersCount == 1 ? Strings.LayersLoadedSingular : string.Format(CultureInfo.CurrentCulture, Strings.LayersLoadedPlural, TotalLayersCount);

    // Commands
    public ICommand NewProjectCommand { get; }
    public ICommand ZoomToFullExtentCommand { get; }
    public ICommand ZoomToSelectedLayerCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand RemoveSelectedLayerCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand MoveLayerUpCommand { get; }
    public ICommand MoveLayerDownCommand { get; }
    public ICommand ToggleCroppingCommand { get; }
    public ICommand ToggleMeasuringCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CloseSelectedMapsCommand { get; }

    public MainViewModel(
        IMapProjectService projectService,
        IGeoRasterRenderer rasterRenderer,
        ITempDirectoryService tempDirectoryService,
        ILanguageService languageService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _rasterRenderer = rasterRenderer ?? throw new ArgumentNullException(nameof(rasterRenderer));
        _tempDirectoryService = tempDirectoryService ?? throw new ArgumentNullException(nameof(tempDirectoryService));
        _languageService = languageService ?? throw new ArgumentNullException(nameof(languageService));

        _projectService.LayersChanged += (_, _) => OnLayersChanged();
        _projectService.ProjectsChanged += (_, _) =>
        {
            if (_projectService.ActiveProject != null)
            {
                _projectService.ActiveProject.DirtyChanged -= OnProjectDirtyChanged;
                _projectService.ActiveProject.DirtyChanged += OnProjectDirtyChanged;
            }
            OnLayersChanged();
        };
        _languageService.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(ProjectTitleText));
            OnPropertyChanged(nameof(LayerStatsText));
            RefreshTree();
        };

        if (_projectService.ActiveProject != null)
        {
            _projectService.ActiveProject.DirtyChanged += OnProjectDirtyChanged;
        }

        NewProjectCommand = new RelayCommand(NewProject);
        ZoomToFullExtentCommand = new RelayCommand(ZoomToFullExtent);
        ZoomToSelectedLayerCommand = new RelayCommand(ZoomToSelectedLayer, () => HasSelectedLayer);
        ZoomInCommand = new RelayCommand(() => Viewport.ZoomAt(Viewport.CenterGeoX, Viewport.CenterGeoY, 1.4));
        ZoomOutCommand = new RelayCommand(() => Viewport.ZoomAt(Viewport.CenterGeoX, Viewport.CenterGeoY, 1.0 / 1.4));
        RemoveSelectedLayerCommand = new RelayCommand(RemoveSelectedLayer, () => HasSelectedLayer);
        ClearAllCommand = new RelayCommand(ClearAll, () => _projectService.Layers.Count > 0 || _projectService.Tracks.Count > 0);
        MoveLayerUpCommand = new RelayCommand(MoveLayerUp, () => HasSelectedLayer);
        MoveLayerDownCommand = new RelayCommand(MoveLayerDown, () => HasSelectedLayer);
        ToggleCroppingCommand = new RelayCommand(ToggleCropping);
        ToggleMeasuringCommand = new RelayCommand(ToggleMeasuring);
        ClearSelectionCommand = new RelayCommand(() => Viewport.ClearSelection());
        CloseSelectedMapsCommand = new RelayCommand(() => CloseSelectedMaps());

        Export = new MapExportViewModel(this);
        Viewport.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MapViewportViewModel.HasSelectedCrop) or nameof(MapViewportViewModel.SelectedCropExtent) or nameof(MapViewportViewModel.IsCroppingMode))
            {
                Export.UpdateCropInfo();
            }
        };
        Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);

        RefreshTree();
    }

    private void OnProjectDirtyChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(ProjectTitleText));
        OnPropertyChanged(nameof(ProjectName));
        if (TreeRoots.Count > 0)
        {
            TreeRoots[0].NotifyTitleChanged();
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var (autoLoadedExtent, autoLoadedExport) = await _projectService.TryAutoLoadProjectAsync();
            if (autoLoadedExport != null)
            {
                Export.LoadExportData(autoLoadedExport);
            }

            if (!autoLoadedExtent.IsEmpty)
            {
                Viewport.SetExtent(autoLoadedExtent.Inflate(1.05));
                StatusMessage = Strings.StatusReady;
                OnPropertyChanged(nameof(ProjectName));
                OnPropertyChanged(nameof(ProjectTitleText));
                OnPropertyChanged(nameof(ProjectFilePath));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(IsDirty));
                Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
            }
        }
        catch
        {
            // Ignore auto-load errors
        }
    }

    public void NewProject()
    {
        MapProject proj = _projectService.CreateNewProject();
        SelectedLayer = null;
        SelectedTreeItem = null;
        StatusMessage = Strings.ProjectNewStatus;
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectTitleText));
        OnPropertyChanged(nameof(ProjectFilePath));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsDirty));
        Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
        _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
    }

    public void CloseProject(MapProject? project)
    {
        if (project != null)
        {
            _projectService.CloseProject(project);
            SelectedLayer = null;
            SelectedTreeItem = null;
            Viewport.InvalidateViewport();
            StatusMessage = Strings.StatusReady;
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectTitleText));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsDirty));
            Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
            _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
        }
    }

    public void RenameProject(MapTreeItemViewModel item, string newName)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Project != null)
        {
            _projectService.RenameProject(item.Project, newName);
            item.NotifyTitleChanged();
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectTitleText));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsDirty));
            Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ProjectRenamedStatus, item.Project.Name);
            _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
        }
    }

    public void UpdateSelectedTreeItems(IEnumerable<MapTreeItemViewModel> items)
    {
        SelectedTreeItems.Clear();
        foreach (MapTreeItemViewModel item in items)
        {
            SelectedTreeItems.Add(item);
        }

        MapTreeItemViewModel? primary = SelectedTreeItems.LastOrDefault();
        SelectedTreeItem = primary;
    }

    public void SelectTreeItem(MapTreeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ExpandParents(TreeRoots, item);
        ClearAllSelections(TreeRoots);
        item.IsSelected = true;
        SelectedTreeItems.Clear();
        SelectedTreeItems.Add(item);
        SelectedTreeItem = item;
    }

    public void CloseMapLayer(MapTreeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _projectService.CloseTreeItem(item.Model);
        SelectedTreeItems.Remove(item);
        if (SelectedTreeItem == item)
        {
            SelectedTreeItem = SelectedTreeItems.LastOrDefault();
            SelectedLayer = SelectedTreeItem?.Layer != null ? LayerViewModels.FirstOrDefault(l => l.Layer == SelectedTreeItem.Layer) : null;
        }
        Viewport.InvalidateViewport();
        _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent);
    }

    public void CloseSelectedMaps(MapTreeItemViewModel? contextItem = null)
    {
        List<MapTreeItemViewModel> selectedMapItems = SelectedTreeItems.Where(i => i.IsMapLayer).ToList();
        if (contextItem != null && contextItem.IsMapLayer && !selectedMapItems.Contains(contextItem))
        {
            selectedMapItems.Add(contextItem);
        }

        if (selectedMapItems.Count == 0 && SelectedTreeItem != null && SelectedTreeItem.IsMapLayer)
        {
            selectedMapItems.Add(SelectedTreeItem);
        }

        if (selectedMapItems.Count == 0)
        {
            return;
        }

        foreach (MapTreeItemViewModel item in selectedMapItems)
        {
            _projectService.CloseTreeItem(item.Model);
            SelectedTreeItems.Remove(item);
            if (SelectedTreeItem == item)
            {
                SelectedTreeItem = null;
                SelectedLayer = null;
            }
        }

        Viewport.InvalidateViewport();
        _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent);
    }

    public void CloseTrack(MapTreeItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _projectService.CloseTreeItem(item.Model);
        SelectedTreeItems.Remove(item);
        if (SelectedTreeItem == item)
        {
            SelectedTreeItem = SelectedTreeItems.LastOrDefault();
        }
        Viewport.InvalidateViewport();
        _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent);
    }

    public async Task<bool> SaveProjectAsync(string? filePath = null)
    {
        string? targetPath = filePath ?? _projectService.ProjectFilePath;
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        try
        {
            await _projectService.SaveProjectAsync(targetPath, Viewport.CurrentExtent, Export.GetExportData());
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ProjectSavedStatus, Path.GetFileName(targetPath));
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectTitleText));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsDirty));
            if (TreeRoots.Count > 0)
            {
                TreeRoots[0].NotifyTitleChanged();
            }
            Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
            _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ProjectSaveError, ex.Message);
            return false;
        }
    }

    public async Task<bool> OpenProjectAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        IsLoading = true;
        Viewport.SetLoading(true, Strings.Loading, 0, isIndeterminate: true);
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.LoadingFile, Path.GetFileName(filePath));
        try
        {
            var (extent, exportData) = await _projectService.OpenProjectAsync(filePath);
            SelectedLayer = null;
            SelectedTreeItem = null;

            if (exportData != null)
            {
                Export.LoadExportData(exportData);
            }

            if (!extent.IsEmpty)
            {
                Viewport.SetExtent(extent.Inflate(1.05));
            }
            else
            {
                ZoomToFullExtent();
            }

            Viewport.InvalidateViewport();
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ProjectLoadedStatus, Path.GetFileName(filePath));
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectTitleText));
            OnPropertyChanged(nameof(ProjectFilePath));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsDirty));
            Export.UpdateProjectDefaults(ProjectTitleText, ProjectFilePath);
            _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ProjectOpenError, ex.Message);
            return false;
        }
        finally
        {
            IsLoading = false;
            Viewport.SetLoading(false);
        }
    }

    public async Task AddMapLayerAsync(string filePath, MapProject? project = null)
    {
        IsLoading = true;
        Viewport.SetLoading(true, Strings.Loading, 0, isIndeterminate: true);
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.LoadingFile, Path.GetFileName(filePath));
        try
        {
            MapLayer? layer = await _projectService.AddMapLayerAsync(filePath, project);
            if (layer != null)
            {
                if (Viewport.CurrentExtent.IsEmpty)
                {
                    Viewport.SetExtent(layer.Extent.Inflate(1.05));
                }
                SelectTreeItemForLayer(layer);
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.FileLoadedStatus, layer.Name);
            }
            else
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.CouldNotLoadFile, Path.GetFileName(filePath));
            }
        }
        finally
        {
            IsLoading = false;
            Viewport.SetLoading(false);
        }
    }

    public async Task AddMapLayersAsync(IEnumerable<string> filePaths, MapProject? project = null)
    {
        IsLoading = true;
        var fileList = filePaths.ToList();
        Viewport.SetLoading(true, Strings.Loading, 0, isIndeterminate: fileList.Count <= 1);
        int count = 0;
        MapLayer? lastLayer = null;
        try
        {
            for (int i = 0; i < fileList.Count; i++)
            {
                string file = fileList[i];
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.LoadingFile, Path.GetFileName(file));
                Viewport.SetLoading(true, Strings.Loading, (double)i / Math.Max(1, fileList.Count) * 100.0, isIndeterminate: fileList.Count <= 1);
                MapLayer? layer = await _projectService.AddMapLayerAsync(file, project);
                if (layer != null)
                {
                    count++;
                    lastLayer = layer;
                }
            }

            if (count > 0 && Viewport.CurrentExtent.IsEmpty)
            {
                ZoomToFullExtent();
            }

            if (lastLayer != null)
            {
                SelectTreeItemForLayer(lastLayer);
            }

            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.MapsLoadedCount, count);
        }
        finally
        {
            IsLoading = false;
            Viewport.SetLoading(false);
        }
    }

    public Task AddMapFilesAsync(IEnumerable<string> filePaths, MapProject? project = null) => AddMapLayersAsync(filePaths, project);

    public async Task AddTrackFileAsync(string filePath, MapProject? project = null)
    {
        IsLoading = true;
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.LoadingFile, Path.GetFileName(filePath));
        try
        {
            MapTrack? track = await _projectService.AddTrackAsync(filePath, project);
            if (track != null)
            {
                if (Viewport.CurrentExtent.IsEmpty && !track.Extent.IsEmpty)
                {
                    Viewport.SetExtent(track.Extent.Inflate(1.05));
                }
                SelectTreeItemForTrack(track);
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.TrackLoadedStatus, track.Name);
            }
            else
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.CouldNotLoadFile, Path.GetFileName(filePath));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddTrackFilesAsync(IEnumerable<string> filePaths, MapProject? project = null)
    {
        IsLoading = true;
        int count = 0;
        MapTrack? lastTrack = null;
        try
        {
            foreach (string file in filePaths)
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.LoadingFile, Path.GetFileName(file));
                MapTrack? track = await _projectService.AddTrackAsync(file, project);
                if (track != null)
                {
                    count++;
                    lastTrack = track;
                }
            }

            if (count > 0 && Viewport.CurrentExtent.IsEmpty)
            {
                ZoomToFullExtent();
            }

            if (lastTrack != null)
            {
                SelectTreeItemForTrack(lastTrack);
            }

            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.TracksLoadedCount, count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddMapFolderAsync(string folderPath, MapProject? project = null)
    {
        IsLoading = true;
        Viewport.SetLoading(true, Strings.Loading, 0, isIndeterminate: true);
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.ScanningFolder, Path.GetFileName(folderPath));
        try
        {
            int added = await _projectService.AddMapFolderAsync(folderPath, project);
            if (added > 0)
            {
                if (Viewport.CurrentExtent.IsEmpty)
                {
                    ZoomToFullExtent();
                }
                ExpandAndSelectFolder(folderPath);
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.FolderMapsAdded, added, Path.GetFileName(folderPath));
            }
            else
            {
                StatusMessage = Strings.NoMapsLoadedStatus;
            }
        }
        finally
        {
            IsLoading = false;
            Viewport.SetLoading(false);
        }
    }

    public void SelectTreeItemForLayer(MapLayer layer)
    {
        MapTreeItemViewModel? item = FindTreeItemByLayer(TreeRoots, layer);
        if (item != null)
        {
            ExpandParents(TreeRoots, item);
            ClearAllSelections(TreeRoots);
            item.IsSelected = true;
            SelectedTreeItems.Clear();
            SelectedTreeItems.Add(item);
            SelectedTreeItem = item;
            SelectedLayer = LayerViewModels.FirstOrDefault(l => l.Layer == layer);
        }
    }

    public void SelectTreeItemForTrack(MapTrack track)
    {
        MapTreeItemViewModel? item = FindTreeItemByTrack(TreeRoots, track);
        if (item != null)
        {
            ExpandParents(TreeRoots, item);
            ClearAllSelections(TreeRoots);
            item.IsSelected = true;
            SelectedTreeItems.Clear();
            SelectedTreeItems.Add(item);
            SelectedTreeItem = item;
        }
    }

    private void ExpandAndSelectFolder(string folderPath)
    {
        MapTreeItemViewModel? item = FindTreeItemByPath(TreeRoots, folderPath);
        if (item != null)
        {
            item.IsExpanded = true;
            ExpandParents(TreeRoots, item);
            ClearAllSelections(TreeRoots);
            item.IsSelected = true;
            SelectedTreeItems.Clear();
            SelectedTreeItems.Add(item);
            SelectedTreeItem = item;
        }
    }

    private static void ClearAllSelections(IEnumerable<MapTreeItemViewModel> items)
    {
        foreach (MapTreeItemViewModel item in items)
        {
            item.IsSelected = false;
            ClearAllSelections(item.Children);
        }
    }

    private static MapTreeItemViewModel? FindTreeItemByLayer(IEnumerable<MapTreeItemViewModel> items, MapLayer layer)
    {
        foreach (MapTreeItemViewModel item in items)
        {
            if (item.Layer == layer)
            {
                return item;
            }

            MapTreeItemViewModel? found = FindTreeItemByLayer(item.Children, layer);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static MapTreeItemViewModel? FindTreeItemByTrack(IEnumerable<MapTreeItemViewModel> items, MapTrack track)
    {
        foreach (MapTreeItemViewModel item in items)
        {
            if (item.Track == track)
            {
                return item;
            }

            MapTreeItemViewModel? found = FindTreeItemByTrack(item.Children, track);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static MapTreeItemViewModel? FindTreeItemByPath(IEnumerable<MapTreeItemViewModel> items, string path)
    {
        foreach (MapTreeItemViewModel item in items)
        {
            if (string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            MapTreeItemViewModel? found = FindTreeItemByPath(item.Children, path);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static bool ExpandParents(IEnumerable<MapTreeItemViewModel> items, MapTreeItemViewModel target)
    {
        foreach (MapTreeItemViewModel item in items)
        {
            if (item == target)
            {
                return true;
            }

            if (ExpandParents(item.Children, target))
            {
                item.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<MapTrack> GetVisibleTracks()
    {
        List<MapTrack> list = new();
        foreach (MapProject project in _projectService.Projects)
        {
            if (!project.RootTreeItem.IsChecked || !project.TracksFolderItem.IsChecked)
            {
                continue;
            }

            foreach (MapTrack track in project.Tracks)
            {
                if (track.IsVisible)
                {
                    list.Add(track);
                }
            }
        }
        return list;
    }

    public IReadOnlyList<MapLayer> GetVisibleLayers()
    {
        List<MapLayer> list = new();
        foreach (MapProject project in _projectService.Projects)
        {
            if (!project.RootTreeItem.IsChecked || !project.MapsFolderItem.IsChecked)
            {
                continue;
            }

            foreach (MapLayer layer in project.Layers)
            {
                if (layer.IsVisible)
                {
                    list.Add(layer);
                }
            }
        }
        return list;
    }

    public void ZoomToFullExtent()
    {
        GeoExtent total = _projectService.GetTotalExtent();
        if (!total.IsEmpty)
        {
            Viewport.SetExtent(total.Inflate(1.05));
        }
    }

    public void ZoomToSelectedLayer()
    {
        if (SelectedLayer != null && !SelectedLayer.Layer.Extent.IsEmpty)
        {
            Viewport.SetExtent(SelectedLayer.Layer.Extent.Inflate(1.05));
        }
    }

    public void ZoomToTreeItem(MapTreeItemViewModel item)
    {
        if (item == null)
        {
            return;
        }

        if (item.Layer != null && !item.Layer.Extent.IsEmpty)
        {
            Viewport.SetExtent(item.Layer.Extent.Inflate(1.05));
            return;
        }

        if (item.Track != null && !item.Track.Extent.IsEmpty)
        {
            Viewport.SetExtent(item.Track.Extent.Inflate(1.05));
            return;
        }

        // Calculate combined extent of all descendant layers and tracks
        GeoExtent combined = GeoExtent.Empty;
        void Accumulate(MapTreeItemViewModel node)
        {
            if (node.Layer != null && !node.Layer.Extent.IsEmpty)
            {
                combined = combined.IsEmpty ? node.Layer.Extent : combined.Union(node.Layer.Extent);
            }
            if (node.Track != null && !node.Track.Extent.IsEmpty)
            {
                combined = combined.IsEmpty ? node.Track.Extent : combined.Union(node.Track.Extent);
            }
            foreach (MapTreeItemViewModel child in node.Children)
            {
                Accumulate(child);
            }
        }

        Accumulate(item);
        if (!combined.IsEmpty)
        {
            Viewport.SetExtent(combined.Inflate(1.05));
        }
    }

    public void RemoveSelectedLayer()
    {
        if (SelectedLayer != null)
        {
            _projectService.RemoveLayer(SelectedLayer.Layer);
            SelectedLayer = null;
        }
    }

    public void ClearAll()
    {
        _projectService.ClearAll();
        SelectedLayer = null;
        SelectedTreeItem = null;
        Viewport.CurrentExtent = GeoExtent.Empty;
        Viewport.ClearSelection();
    }

    public void MoveLayerUp()
    {
        if (SelectedLayer != null)
        {
            _projectService.MoveLayerUp(SelectedLayer.Layer);
        }
    }

    public void MoveLayerDown()
    {
        if (SelectedLayer != null)
        {
            _projectService.MoveLayerDown(SelectedLayer.Layer);
        }
    }

    private void ToggleCropping()
    {
        Viewport.IsCroppingMode = !Viewport.IsCroppingMode;
        if (Viewport.IsCroppingMode)
        {
            Viewport.IsMeasuringMode = false;
            StatusMessage = Strings.ModeCroppingActive;
        }
        else
        {
            StatusMessage = Strings.ModeNavigationActive;
        }
    }

    private void ToggleMeasuring()
    {
        Viewport.IsMeasuringMode = !Viewport.IsMeasuringMode;
        if (Viewport.IsMeasuringMode)
        {
            Viewport.IsCroppingMode = false;
            StatusMessage = Strings.ModeMeasuringActive;
        }
        else
        {
            StatusMessage = Strings.ModeNavigationActive;
        }
    }

    private void OnLayersChanged()
    {
        LayerViewModels.Clear();
        foreach (MapLayer layer in _projectService.Layers)
        {
            MapLayerViewModel vm = new(layer);
            vm.PropertyChanged += (_, _) =>
            {
                Viewport.InvalidateViewport();
                MarkProjectDirty();
                _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
            };
            LayerViewModels.Add(vm);
        }

        RefreshTree();
        OnPropertyChanged(nameof(TotalLayersCount));
        OnPropertyChanged(nameof(TotalTracksCount));
        OnPropertyChanged(nameof(TotalProjectsCount));
        OnPropertyChanged(nameof(LayerStatsText));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectTitleText));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsDirty));
        _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
    }

    private void RefreshTree()
    {
        TreeRoots.Clear();

        Action onVisibilityChanged = () =>
        {
            Viewport.InvalidateViewport();
            MarkProjectDirty();
            _ = _projectService.AutoSaveProjectAsync(Viewport.CurrentExtent, Export.GetExportData());
        };

        foreach (MapTreeItem projectRoot in _projectService.ProjectTreeRoots)
        {
            if (string.IsNullOrWhiteSpace(_searchFilter))
            {
                TreeRoots.Add(new MapTreeItemViewModel(projectRoot, onVisibilityChanged));
            }
            else
            {
                MapTreeItem? filteredRoot = FilterTreeItem(projectRoot, _searchFilter);
                if (filteredRoot != null)
                {
                    TreeRoots.Add(new MapTreeItemViewModel(filteredRoot, onVisibilityChanged));
                }
            }
        }
    }

    private static MapTreeItem? FilterTreeItem(MapTreeItem item, string filter)
    {
        if (item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return item;
        }

        List<MapTreeItem> matchingChildren = new();
        foreach (MapTreeItem child in item.Children)
        {
            MapTreeItem? filteredChild = FilterTreeItem(child, filter);
            if (filteredChild != null)
            {
                matchingChildren.Add(filteredChild);
            }
        }

        if (matchingChildren.Count > 0)
        {
            MapTreeItem copy = new(item.Title, item.ItemType, item.FullPath, item.Layer, item.Track, item.Project)
            {
                IsExpanded = true,
                IsChecked = item.IsChecked
            };

            foreach (MapTreeItem child in matchingChildren)
            {
                copy.Children.Add(child);
            }

            return copy;
        }

        return null;
    }
}
