using System.Collections.ObjectModel;
using TifToKmzMapEditor.Models;

namespace TifToKmzMapEditor.Services;

public interface IMapProjectService
{
    IReadOnlyList<MapProject> Projects { get; }
    MapProject? ActiveProject { get; set; }

    string ProjectName { get; set; }
    string? ProjectFilePath { get; set; }
    IReadOnlyList<MapLayer> Layers { get; }
    IReadOnlyList<MapTrack> Tracks { get; }
    MapTreeItem RootTreeItem { get; }
    ObservableCollection<MapTreeItem> ProjectTreeRoots { get; }

    event EventHandler? LayersChanged;
    event EventHandler? ProjectsChanged;

    MapProject CreateNewProject(string? name = null);
    void CloseProject(MapProject project);
    void RenameProject(MapProject project, string newName);
    Task<MapLayer?> AddMapLayerAsync(string filePath, MapProject? targetProject = null);
    Task<int> AddMapFolderAsync(string folderPath, MapProject? targetProject = null);
    Task<MapTrack?> AddTrackAsync(string filePath, MapProject? targetProject = null);
    void RemoveLayer(MapLayer layer);
    void RemoveTrack(MapTrack track);
    void CloseTreeItem(MapTreeItem item);
    void ClearAll();
    void MoveLayerUp(MapLayer layer);
    void MoveLayerDown(MapLayer layer);
    GeoExtent GetTotalExtent();

    Task SaveProjectAsync(string filePath, GeoExtent currentViewportExtent, MapProjectExportData? exportData = null, MapProject? project = null);
    Task<(GeoExtent Extent, MapProjectExportData? ExportData)> OpenProjectAsync(string filePath);
    Task AutoSaveProjectAsync(GeoExtent currentViewportExtent, MapProjectExportData? exportData = null);
    Task<(GeoExtent Extent, MapProjectExportData? ExportData)> TryAutoLoadProjectAsync();
    void NewProject();
}
