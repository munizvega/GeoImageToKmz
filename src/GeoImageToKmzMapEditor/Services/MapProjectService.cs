using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using TifToKmz.ImageTools;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Services;

public sealed class MapProjectService : IMapProjectService
{
    private const string _lastProjectConfigFileName = "lastproject.editor.json";
    private static readonly string[] _supportedPatterns = GdalHelper.SupportedSearchPatterns;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly List<MapProject> _projects = new();
    private MapProject? _activeProject;
    private int _projectCounter;

    public IReadOnlyList<MapProject> Projects => _projects.AsReadOnly();

    public MapProject? ActiveProject
    {
        get => _activeProject ?? _projects.FirstOrDefault();
        set => _activeProject = value;
    }

    public string ProjectName
    {
        get => ActiveProject?.Name ?? Strings.ProjectDefaultTitle;
        set
        {
            if (ActiveProject != null)
            {
                ActiveProject.Name = value;
            }
        }
    }

    public string? ProjectFilePath
    {
        get => ActiveProject?.FilePath;
        set
        {
            if (ActiveProject != null)
            {
                ActiveProject.FilePath = value;
            }
        }
    }

    public IReadOnlyList<MapLayer> Layers => _projects.SelectMany(p => p.Layers).ToList().AsReadOnly();
    public IReadOnlyList<MapTrack> Tracks => _projects.SelectMany(p => p.Tracks).ToList().AsReadOnly();

    public MapTreeItem RootTreeItem => ActiveProject?.RootTreeItem ?? (_projects.Count > 0 ? _projects[0].RootTreeItem : CreateNewProject().RootTreeItem);
    public ObservableCollection<MapTreeItem> ProjectTreeRoots { get; } = new();

    public event EventHandler? LayersChanged;
    public event EventHandler? ProjectsChanged;

    public MapProjectService()
    {
        CreateNewProject();
    }

    public MapProject CreateNewProject(string? name = null)
    {
        _projects.Clear();
        ProjectTreeRoots.Clear();

        _projectCounter++;
        string projName = !string.IsNullOrWhiteSpace(name)
            ? name
            : string.Format(CultureInfo.CurrentCulture, Strings.ProjectNameNumberedFormat, _projectCounter);

        MapProject project = new(projName)
        {
            IsDirty = false
        };
        _projects.Add(project);
        ProjectTreeRoots.Add(project.RootTreeItem);
        _activeProject = project;

        ProjectsChanged?.Invoke(this, EventArgs.Empty);
        LayersChanged?.Invoke(this, EventArgs.Empty);
        return project;
    }

    public void CloseProject(MapProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (_projects.Remove(project))
        {
            ProjectTreeRoots.Remove(project.RootTreeItem);

            if (_activeProject == project)
            {
                _activeProject = _projects.LastOrDefault();
            }

            if (_projects.Count == 0)
            {
                SaveLastOpenedProjectPath(null);
                DeleteAutoSaveFile();
                CreateNewProject();
            }
            else
            {
                ProjectsChanged?.Invoke(this, EventArgs.Empty);
                LayersChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void RenameProject(MapProject project, string newName)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        string cleanName = newName.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            cleanName = cleanName.Replace(c, '_');
        }

        if (string.IsNullOrWhiteSpace(cleanName))
        {
            return;
        }

        if (string.Equals(project.Name, cleanName, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(project.FilePath) && File.Exists(project.FilePath))
        {
            try
            {
                string? dir = Path.GetDirectoryName(project.FilePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string newPath = Path.Combine(dir, $"{cleanName}.ttk");
                    if (!string.Equals(project.FilePath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }
                        File.Move(project.FilePath, newPath);
                        project.FilePath = newPath;
                    }
                }
            }
            catch
            {
                // If file system rename fails, continue updating model
            }
        }

        project.Name = cleanName;
        project.IsDirty = true;
        ProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<MapLayer?> AddMapLayerAsync(string filePath, MapProject? targetProject = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        MapProject project = targetProject ?? ActiveProject ?? CreateNewProject();
        string fullPath = Path.GetFullPath(filePath);

        if (project.Layers.Any(l => string.Equals(l.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return project.Layers.First(l => string.Equals(l.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        MapLayer? layer = await Task.Run(() =>
        {
            try
            {
                return new MapLayer(fullPath);
            }
            catch
            {
                return null;
            }
        });

        if (layer == null)
        {
            return null;
        }

        project.AddLayer(layer);
        project.IsDirty = true;
        LayersChanged?.Invoke(this, EventArgs.Empty);
        return layer;
    }

    public async Task<MapTrack?> AddTrackAsync(string filePath, MapProject? targetProject = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        MapProject project = targetProject ?? ActiveProject ?? CreateNewProject();
        string fullPath = Path.GetFullPath(filePath);

        if (project.Tracks.Any(t => string.Equals(t.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return project.Tracks.First(t => string.Equals(t.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        }

        string? projWkt = project.GetProjectionWkt() ?? GetFirstAvailableProjectionWkt();

        MapTrack? track = await Task.Run(() =>
        {
            try
            {
                return new MapTrack(fullPath, projWkt);
            }
            catch
            {
                return null;
            }
        });

        if (track == null)
        {
            return null;
        }

        project.AddTrack(track);
        project.IsDirty = true;
        LayersChanged?.Invoke(this, EventArgs.Empty);
        return track;
    }

    public async Task<int> AddMapFolderAsync(string folderPath, MapProject? targetProject = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return 0;
        }

        MapProject project = targetProject ?? ActiveProject ?? CreateNewProject();
        string fullFolderPath = Path.GetFullPath(folderPath);
        string folderName = Path.GetFileName(fullFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = fullFolderPath;
        }

        List<string> foundFiles = new();
        foreach (string pattern in _supportedPatterns)
        {
            try
            {
                foundFiles.AddRange(Directory.GetFiles(fullFolderPath, pattern, SearchOption.AllDirectories));
            }
            catch
            {
                // Ignore directory access exceptions
            }
        }

        string[] imageFiles = foundFiles.Distinct().ToArray();
        if (imageFiles.Length == 0)
        {
            return 0;
        }

        List<MapLayer> loadedLayers = await Task.Run(() =>
        {
            List<MapLayer> list = new();
            foreach (string file in imageFiles)
            {
                try
                {
                    string full = Path.GetFullPath(file);
                    if (!project.Layers.Any(l => string.Equals(l.FilePath, full, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new MapLayer(full));
                    }
                }
                catch
                {
                    // Skip invalid rasters
                }
            }
            return list;
        });

        if (loadedLayers.Count == 0)
        {
            return 0;
        }

        MapTreeItem folderNode = new(folderName, MapTreeItemType.Folder, fullFolderPath)
        {
            Project = project
        };

        foreach (MapLayer layer in loadedLayers)
        {
            layer.ZIndex = project.Layers.Count;
            project.Layers.Add(layer);

            MapTreeItem item = new(layer.Name, MapTreeItemType.MapLayer, layer.FilePath, layer)
            {
                Project = project
            };

            folderNode.Children.Add(item);
        }

        if (folderNode.Children.Count > 0)
        {
            project.MapsFolderItem.Children.Add(folderNode);
        }

        string? projWkt = project.GetProjectionWkt();
        if (!string.IsNullOrWhiteSpace(projWkt))
        {
            foreach (MapTrack track in project.Tracks)
            {
                track.UpdateProjectedPoints(projWkt);
            }
        }

        project.IsDirty = true;
        LayersChanged?.Invoke(this, EventArgs.Empty);
        return loadedLayers.Count;
    }

    public void RemoveLayer(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        foreach (MapProject project in _projects)
        {
            if (project.RemoveLayer(layer))
            {
                project.IsDirty = true;
                LayersChanged?.Invoke(this, EventArgs.Empty);
                break;
            }
        }
    }

    public void RemoveTrack(MapTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        foreach (MapProject project in _projects)
        {
            if (project.RemoveTrack(track))
            {
                project.IsDirty = true;
                LayersChanged?.Invoke(this, EventArgs.Empty);
                break;
            }
        }
    }

    public void CloseTreeItem(MapTreeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ItemType == MapTreeItemType.Project && item.Project != null)
        {
            CloseProject(item.Project);
            return;
        }

        if (item.ItemType == MapTreeItemType.MapLayer && item.Layer != null)
        {
            RemoveLayer(item.Layer);
            return;
        }

        if (item.ItemType == MapTreeItemType.Track && item.Track != null)
        {
            RemoveTrack(item.Track);
            return;
        }
    }

    public void ClearAll()
    {
        _projects.Clear();
        ProjectTreeRoots.Clear();
        CreateNewProject(Strings.ProjectDefaultTitle);
    }

    public void MoveLayerUp(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        MapProject? project = _projects.FirstOrDefault(p => p.Layers.Contains(layer));
        if (project == null)
        {
            return;
        }

        int index = project.Layers.IndexOf(layer);
        if (index > 0)
        {
            (project.Layers[index], project.Layers[index - 1]) = (project.Layers[index - 1], project.Layers[index]);
            UpdateZIndices(project);
            project.IsDirty = true;
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MoveLayerDown(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        MapProject? project = _projects.FirstOrDefault(p => p.Layers.Contains(layer));
        if (project == null)
        {
            return;
        }

        int index = project.Layers.IndexOf(layer);
        if (index >= 0 && index < project.Layers.Count - 1)
        {
            (project.Layers[index], project.Layers[index + 1]) = (project.Layers[index + 1], project.Layers[index]);
            UpdateZIndices(project);
            project.IsDirty = true;
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public GeoExtent GetTotalExtent()
    {
        GeoExtent total = GeoExtent.Empty;

        foreach (MapProject project in _projects)
        {
            if (!project.RootTreeItem.IsChecked)
            {
                continue;
            }

            foreach (MapLayer layer in project.Layers)
            {
                if (layer.IsVisible && !layer.Extent.IsEmpty)
                {
                    total = total.IsEmpty ? layer.Extent : total.Union(layer.Extent);
                }
            }

            foreach (MapTrack track in project.Tracks)
            {
                if (track.IsVisible && !track.Extent.IsEmpty)
                {
                    total = total.IsEmpty ? track.Extent : total.Union(track.Extent);
                }
            }
        }

        return total;
    }

    public async Task SaveProjectAsync(string filePath, GeoExtent currentViewportExtent, MapProjectExportData? exportData = null, MapProject? project = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        MapProject target = project ?? ActiveProject ?? throw new InvalidOperationException("No active project to save");

        string fullPath = Path.GetFullPath(filePath);
        if (string.IsNullOrEmpty(Path.GetExtension(fullPath)))
        {
            fullPath += ".ttk";
        }

        target.FilePath = fullPath;
        target.Name = Path.GetFileNameWithoutExtension(fullPath);

        MapProjectData projectData = new()
        {
            Version = 1,
            Name = target.Name,
            Layers = target.Layers.Select(l => new MapProjectLayerData
            {
                FilePath = l.FilePath,
                IsVisible = l.IsVisible,
                Opacity = l.Opacity,
                ZIndex = l.ZIndex
            }).ToList(),
            Tracks = target.Tracks.Select(t => new MapProjectTrackData
            {
                FilePath = t.FilePath,
                IsVisible = t.IsVisible
            }).ToList(),
            RootTreeItem = SerializeTreeItem(target.RootTreeItem),
            ViewportExtent = currentViewportExtent.IsEmpty ? null : new MapProjectExtentData
            {
                MinX = currentViewportExtent.MinX,
                MinY = currentViewportExtent.MinY,
                MaxX = currentViewportExtent.MaxX,
                MaxY = currentViewportExtent.MaxY
            },
            ExportSettings = exportData
        };

        string json = JsonSerializer.Serialize(projectData, _jsonOptions);
        await File.WriteAllTextAsync(target.FilePath, json);
        target.IsDirty = false;
        SaveLastOpenedProjectPath(target.FilePath);
    }

    public async Task<(GeoExtent Extent, MapProjectExportData? ExportData)> OpenProjectAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Project file not found", filePath);
        }

        string fullPath = Path.GetFullPath(filePath);
        string json = await File.ReadAllTextAsync(fullPath);
        MapProjectData? projectData = JsonSerializer.Deserialize<MapProjectData>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Invalid project file content");

        _projects.Clear();
        ProjectTreeRoots.Clear();

        string projName = !string.IsNullOrWhiteSpace(projectData.Name) && !projectData.Name.Equals("autosave", StringComparison.OrdinalIgnoreCase)
            ? projectData.Name
            : Path.GetFileNameWithoutExtension(fullPath);

        MapProject project = new(projName)
        {
            FilePath = fullPath
        };

        // Load layers
        Dictionary<string, MapLayer> loadedLayers = new(StringComparer.OrdinalIgnoreCase);
        foreach (MapProjectLayerData layerData in projectData.Layers)
        {
            string layerPath = ResolveRelativeOrAbsolutePath(fullPath, layerData.FilePath);
            if (File.Exists(layerPath))
            {
                try
                {
                    MapLayer layer = new(layerPath)
                    {
                        IsVisible = layerData.IsVisible,
                        Opacity = layerData.Opacity,
                        ZIndex = layerData.ZIndex
                    };
                    project.Layers.Add(layer);
                    loadedLayers[layer.FilePath] = layer;
                }
                catch
                {
                    // Skip unreadable files
                }
            }
        }

        // Load tracks
        string? projWkt = project.GetProjectionWkt() ?? GetFirstAvailableProjectionWkt();
        Dictionary<string, MapTrack> loadedTracks = new(StringComparer.OrdinalIgnoreCase);
        foreach (MapProjectTrackData trackData in projectData.Tracks)
        {
            string trackPath = ResolveRelativeOrAbsolutePath(fullPath, trackData.FilePath);
            if (File.Exists(trackPath))
            {
                try
                {
                    MapTrack track = new(trackPath, projWkt)
                    {
                        IsVisible = trackData.IsVisible
                    };
                    project.Tracks.Add(track);
                    loadedTracks[track.FilePath] = track;
                }
                catch
                {
                    // Skip unreadable tracks
                }
            }
        }

        // Reconstruct tree
        project.TracksFolderItem.Children.Clear();
        project.MapsFolderItem.Children.Clear();

        if (projectData.RootTreeItem != null)
        {
            foreach (MapProjectTreeItemData childData in projectData.RootTreeItem.Children)
            {
                if (childData.ItemType == MapTreeItemType.TracksFolder)
                {
                    project.TracksFolderItem.IsChecked = childData.IsChecked;
                    project.TracksFolderItem.IsExpanded = childData.IsExpanded;

                    foreach (MapProjectTreeItemData trackChild in childData.Children)
                    {
                        MapTreeItem? item = ReconstructTreeItem(trackChild, loadedLayers, loadedTracks, project, fullPath);
                        if (item != null)
                        {
                            project.TracksFolderItem.Children.Add(item);
                        }
                    }
                }
                else if (childData.ItemType == MapTreeItemType.MapsFolder)
                {
                    project.MapsFolderItem.IsChecked = childData.IsChecked;
                    project.MapsFolderItem.IsExpanded = childData.IsExpanded;

                    foreach (MapProjectTreeItemData mapChild in childData.Children)
                    {
                        MapTreeItem? item = ReconstructTreeItem(mapChild, loadedLayers, loadedTracks, project, fullPath);
                        if (item != null)
                        {
                            project.MapsFolderItem.Children.Add(item);
                        }
                    }
                }
            }
        }

        // Ensure all loaded tracks exist in the tree
        foreach (MapTrack track in project.Tracks)
        {
            if (!project.TracksFolderItem.Children.Any(c => c.Track == track))
            {
                project.TracksFolderItem.Children.Add(new MapTreeItem(track.Name, MapTreeItemType.Track, track.FilePath, track: track, project: project)
                {
                    IsChecked = track.IsVisible
                });
            }
        }

        // Ensure all loaded layers exist in the tree
        foreach (MapLayer layer in project.Layers)
        {
            if (!project.MapsFolderItem.Children.Any(c => c.Layer == layer || (c.Children.Count > 0 && HasLayerRecursive(c, layer))))
            {
                project.MapsFolderItem.Children.Add(new MapTreeItem(layer.Name, MapTreeItemType.MapLayer, layer.FilePath, layer, project: project)
                {
                    IsChecked = layer.IsVisible
                });
            }
        }

        project.IsDirty = false;
        _projects.Add(project);
        ProjectTreeRoots.Add(project.RootTreeItem);
        _activeProject = project;

        SaveLastOpenedProjectPath(fullPath);

        ProjectsChanged?.Invoke(this, EventArgs.Empty);
        LayersChanged?.Invoke(this, EventArgs.Empty);

        GeoExtent extent = projectData.ViewportExtent != null
            ? new GeoExtent(
                projectData.ViewportExtent.MinX,
                projectData.ViewportExtent.MinY,
                projectData.ViewportExtent.MaxX,
                projectData.ViewportExtent.MaxY)
            : GetTotalExtent();

        return (extent, projectData.ExportSettings);
    }

    public async Task AutoSaveProjectAsync(GeoExtent currentViewportExtent, MapProjectExportData? exportData = null)
    {
        string autoSaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TifToKmzMapEditor");
        Directory.CreateDirectory(autoSaveDir);
        string autoSavePath = Path.Combine(autoSaveDir, "autosave.ttk");

        if (ActiveProject != null && (ActiveProject.Layers.Count > 0 || ActiveProject.Tracks.Count > 0))
        {
            MapProject target = ActiveProject;
            MapProjectData projectData = new()
            {
                Version = 1,
                Name = target.Name,
                Layers = target.Layers.Select(l => new MapProjectLayerData
                {
                    FilePath = l.FilePath,
                    IsVisible = l.IsVisible,
                    Opacity = l.Opacity,
                    ZIndex = l.ZIndex
                }).ToList(),
                Tracks = target.Tracks.Select(t => new MapProjectTrackData
                {
                    FilePath = t.FilePath,
                    IsVisible = t.IsVisible
                }).ToList(),
                RootTreeItem = SerializeTreeItem(target.RootTreeItem),
                ViewportExtent = currentViewportExtent.IsEmpty ? null : new MapProjectExtentData
                {
                    MinX = currentViewportExtent.MinX,
                    MinY = currentViewportExtent.MinY,
                    MaxX = currentViewportExtent.MaxX,
                    MaxY = currentViewportExtent.MaxY
                },
                ExportSettings = exportData
            };

            string json = JsonSerializer.Serialize(projectData, _jsonOptions);
            await File.WriteAllTextAsync(autoSavePath, json);
        }
    }

    public async Task<(GeoExtent Extent, MapProjectExportData? ExportData)> TryAutoLoadProjectAsync()
    {
        string? lastPath = GetLastOpenedProjectPath();
        if (!string.IsNullOrWhiteSpace(lastPath) && File.Exists(lastPath))
        {
            try
            {
                var result = await OpenProjectAsync(lastPath);
                if (ActiveProject != null)
                {
                    ActiveProject.IsDirty = false;
                }
                return result;
            }
            catch
            {
                // Fall back to autosave or clean
            }
        }

        string autoSaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TifToKmzMapEditor");
        string autoSavePath = Path.Combine(autoSaveDir, "autosave.ttk");
        string legacyAutoSavePath = Path.Combine(autoSaveDir, "autosave.mapproj");

        string? pathToLoad = File.Exists(autoSavePath) ? autoSavePath : (File.Exists(legacyAutoSavePath) ? legacyAutoSavePath : null);

        if (pathToLoad != null)
        {
            try
            {
                var result = await OpenProjectAsync(pathToLoad);
                if (ActiveProject != null)
                {
                    ActiveProject.FilePath = null;
                    ActiveProject.IsDirty = ActiveProject.Layers.Count > 0 || ActiveProject.Tracks.Count > 0;
                }
                return result;
            }
            catch
            {
                // Ignored if autosave cannot be loaded
            }
        }

        return (GeoExtent.Empty, null);
    }

    public void NewProject()
    {
        SaveLastOpenedProjectPath(null);
        DeleteAutoSaveFile();
        CreateNewProject();
    }

    private static string GetLastProjectConfigPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TifToKmzMapEditor", _lastProjectConfigFileName);
    }

    private static string? GetLastOpenedProjectPath()
    {
        try
        {
            string configPath = GetLastProjectConfigPath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("LastProjectPath", out JsonElement elem))
                {
                    string? path = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch
        {
            // Ignore error
        }
        return null;
    }

    private static void SaveLastOpenedProjectPath(string? filePath)
    {
        try
        {
            string configPath = GetLastProjectConfigPath();
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var configObj = new
            {
                LastProjectPath = filePath
            };

            string json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // Ignore error
        }
    }

    private static void DeleteAutoSaveFile()
    {
        try
        {
            string autoSaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TifToKmzMapEditor");
            string autoSavePath = Path.Combine(autoSaveDir, "autosave.ttk");
            if (File.Exists(autoSavePath))
            {
                File.Delete(autoSavePath);
            }
        }
        catch
        {
            // Ignore deletion error
        }
    }

    private string? GetFirstAvailableProjectionWkt()
    {
        return _projects.Select(p => p.GetProjectionWkt()).FirstOrDefault(w => !string.IsNullOrWhiteSpace(w));
    }

    private static void UpdateZIndices(MapProject project)
    {
        for (int i = 0; i < project.Layers.Count; i++)
        {
            project.Layers[i].ZIndex = i;
        }
    }

    private static bool HasLayerRecursive(MapTreeItem parent, MapLayer layer)
    {
        foreach (MapTreeItem child in parent.Children)
        {
            if (child.Layer == layer || HasLayerRecursive(child, layer))
            {
                return true;
            }
        }
        return false;
    }

    private static MapProjectTreeItemData SerializeTreeItem(MapTreeItem item)
    {
        return new MapProjectTreeItemData
        {
            Title = item.Title,
            ItemType = item.ItemType,
            FullPath = item.FullPath,
            LayerFilePath = item.Layer?.FilePath,
            TrackFilePath = item.Track?.FilePath,
            IsExpanded = item.IsExpanded,
            IsChecked = item.IsChecked,
            Children = item.Children.Select(SerializeTreeItem).ToList()
        };
    }

    private static MapTreeItem? ReconstructTreeItem(
        MapProjectTreeItemData data,
        Dictionary<string, MapLayer> loadedLayers,
        Dictionary<string, MapTrack> loadedTracks,
        MapProject project,
        string projectFilePath)
    {
        MapLayer? layer = null;
        if (!string.IsNullOrWhiteSpace(data.LayerFilePath))
        {
            string resolved = ResolveRelativeOrAbsolutePath(projectFilePath, data.LayerFilePath);
            loadedLayers.TryGetValue(resolved, out layer);
        }

        MapTrack? track = null;
        if (!string.IsNullOrWhiteSpace(data.TrackFilePath))
        {
            string resolved = ResolveRelativeOrAbsolutePath(projectFilePath, data.TrackFilePath);
            loadedTracks.TryGetValue(resolved, out track);
        }

        MapTreeItem item = new(data.Title, data.ItemType, data.FullPath, layer, track, project)
        {
            IsExpanded = data.IsExpanded,
            IsChecked = data.IsChecked
        };

        foreach (MapProjectTreeItemData childData in data.Children)
        {
            MapTreeItem? childItem = ReconstructTreeItem(childData, loadedLayers, loadedTracks, project, projectFilePath);
            if (childItem != null)
            {
                item.Children.Add(childItem);
            }
        }

        return item;
    }

    private static string ResolveRelativeOrAbsolutePath(string baseFilePath, string targetPath)
    {
        if (Path.IsPathRooted(targetPath))
        {
            return targetPath;
        }

        string? dir = Path.GetDirectoryName(baseFilePath);
        return string.IsNullOrEmpty(dir) ? targetPath : Path.GetFullPath(Path.Combine(dir, targetPath));
    }
}
