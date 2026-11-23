using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.Models;

public sealed class MapProject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            UpdateTitle();
        }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                UpdateTitle();
                DirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string DisplayName => IsDirty ? $"{Name}*" : Name;

    public event EventHandler? DirtyChanged;

    public string? FilePath { get; set; }

    public List<MapLayer> Layers { get; } = new();
    public List<MapTrack> Tracks { get; } = new();

    public MapTreeItem RootTreeItem { get; }
    public MapTreeItem TracksFolderItem { get; }
    public MapTreeItem MapsFolderItem { get; }

    public MapProject(string name)
    {
        _name = string.IsNullOrWhiteSpace(name) ? Strings.ProjectDefaultTitle : name;

        RootTreeItem = new MapTreeItem(DisplayName, MapTreeItemType.Project)
        {
            Project = this
        };

        TracksFolderItem = new MapTreeItem(Strings.TracksSection, MapTreeItemType.TracksFolder)
        {
            Project = this
        };

        MapsFolderItem = new MapTreeItem(Strings.MapsSection, MapTreeItemType.MapsFolder)
        {
            Project = this
        };

        RootTreeItem.Children.Add(TracksFolderItem);
        RootTreeItem.Children.Add(MapsFolderItem);
    }

    private void UpdateTitle()
    {
        RootTreeItem.Title = DisplayName;
    }

    public string? GetProjectionWkt()
    {
        return Layers.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.ProjectionWkt))?.ProjectionWkt;
    }

    public void AddLayer(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (!Layers.Contains(layer))
        {
            layer.ZIndex = Layers.Count;
            Layers.Add(layer);

            MapTreeItem layerItem = new(layer.Name, MapTreeItemType.MapLayer, layer.FilePath, layer)
            {
                Project = this
            };

            MapsFolderItem.Children.Add(layerItem);

            // Update projection on all tracks if this is the first layer
            string? projWkt = GetProjectionWkt();
            if (!string.IsNullOrWhiteSpace(projWkt))
            {
                foreach (MapTrack track in Tracks)
                {
                    track.UpdateProjectedPoints(projWkt);
                }
            }
        }
    }

    public bool RemoveLayer(MapLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        if (Layers.Remove(layer))
        {
            RemoveLayerTreeItem(MapsFolderItem, layer);
            UpdateZIndices();
            return true;
        }

        return false;
    }

    public void AddTrack(MapTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (!Tracks.Contains(track))
        {
            string? projWkt = GetProjectionWkt();
            track.UpdateProjectedPoints(projWkt);

            Tracks.Add(track);

            MapTreeItem trackItem = new(track.Name, MapTreeItemType.Track, track.FilePath, track: track)
            {
                Project = this
            };

            TracksFolderItem.Children.Add(trackItem);
        }
    }

    public bool RemoveTrack(MapTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (Tracks.Remove(track))
        {
            RemoveTrackTreeItem(TracksFolderItem, track);
            return true;
        }

        return false;
    }

    private static bool RemoveLayerTreeItem(MapTreeItem parent, MapLayer layer)
    {
        for (int i = parent.Children.Count - 1; i >= 0; i--)
        {
            MapTreeItem child = parent.Children[i];
            if (child.Layer == layer)
            {
                parent.Children.RemoveAt(i);
                return true;
            }

            if (child.Children.Count > 0 && RemoveLayerTreeItem(child, layer))
            {
                if (child.Children.Count == 0 && child.ItemType == MapTreeItemType.Folder)
                {
                    parent.Children.RemoveAt(i);
                }
                return true;
            }
        }

        return false;
    }

    private static bool RemoveTrackTreeItem(MapTreeItem parent, MapTrack track)
    {
        for (int i = parent.Children.Count - 1; i >= 0; i--)
        {
            MapTreeItem child = parent.Children[i];
            if (child.Track == track)
            {
                parent.Children.RemoveAt(i);
                return true;
            }

            if (child.Children.Count > 0 && RemoveTrackTreeItem(child, track))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateZIndices()
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            Layers[i].ZIndex = i;
        }
    }
}
