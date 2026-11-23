using System.Collections.ObjectModel;

namespace TifToKmzMapEditor.Models;

public enum MapTreeItemType
{
    Project,
    TracksFolder,
    Track,
    MapsFolder,
    Folder,
    MapLayer
}

public sealed class MapTreeItem
{
    public string Title { get; set; }
    public string? FullPath { get; set; }
    public MapTreeItemType ItemType { get; set; }
    public MapProject? Project { get; set; }
    public MapLayer? Layer { get; set; }
    public MapTrack? Track { get; set; }
    public MapTreeItem? Parent { get; set; }
    public ObservableCollection<MapTreeItem> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;
    public bool IsChecked { get; set; } = true;

    public MapTreeItem(string title, MapTreeItemType itemType, string? fullPath = null, MapLayer? layer = null, MapTrack? track = null, MapProject? project = null)
    {
        Title = title;
        ItemType = itemType;
        FullPath = fullPath;
        Layer = layer;
        Track = track;
        Project = project;
    }
}
