using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Models;

public sealed class MapProjectData
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = Strings.ProjectDefaultTitle;
    public List<MapProjectLayerData> Layers { get; set; } = new();
    public List<MapProjectTrackData> Tracks { get; set; } = new();
    public MapProjectTreeItemData? RootTreeItem { get; set; }
    public MapProjectExtentData? ViewportExtent { get; set; }
    public MapProjectExportData? ExportSettings { get; set; }
}

public enum ExportAreaMode
{
    TrackContour,
    SelectedTiles,
    SelectedMaps
}

public sealed class MapProjectExportData
{
    public string OutputDirectory { get; set; } = string.Empty;
    public string KmzFileName { get; set; } = Strings.DefaultKmzName;
    public int TileSize { get; set; } = 1024;
    public int MaxTiles { get; set; } = 250;
    public double Scale { get; set; } = 1.0;
    public int Quality { get; set; } = 100;
    public bool BrightnessEnabled { get; set; } = true;
    public double Gamma { get; set; } = 1.6;
    public double Brightness { get; set; } = 8.0;
    public double Contrast { get; set; } = 1.0;
    public bool ExportAll { get; set; } = true;
    public bool ExportCrop { get; set; }
    public ExportAreaMode Mode { get; set; } = ExportAreaMode.SelectedMaps;
    public string? SelectedTrackFilePath { get; set; }
    public double TrackMargin { get; set; } = 100.0;
    public bool LockTileSize { get; set; } = true;
    public bool SelectTrackTiles { get; set; }
    public string? SelectedTilesTrackFilePath { get; set; }
    public double SelectedTilesTrackMargin { get; set; } = 100.0;
    public List<MapProjectExtentData> SelectedTiles { get; set; } = new();
}

public sealed class MapProjectLayerData
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    public int ZIndex { get; set; }
}

public sealed class MapProjectTrackData
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
}

public sealed class MapProjectTreeItemData
{
    public string Title { get; set; } = string.Empty;
    public MapTreeItemType ItemType { get; set; }
    public string? FullPath { get; set; }
    public string? LayerFilePath { get; set; }
    public string? TrackFilePath { get; set; }
    public bool IsExpanded { get; set; } = true;
    public bool IsChecked { get; set; } = true;
    public List<MapProjectTreeItemData> Children { get; set; } = new();
}

public sealed class MapProjectExtentData
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
}
