using System.Collections.ObjectModel;
using System.Globalization;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.ViewModels;

public sealed class MapTreeItemViewModel : ViewModelBase
{
    private readonly MapTreeItem _model;
    private readonly Action? _onVisibilityChanged;
    private bool _isChecked;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isEditing;
    private string _editingTitle = string.Empty;

    public MapTreeItem Model => _model;
    public string Title => _model.Title;
    public string? FullPath => _model.FullPath;
    public MapTreeItemType ItemType => _model.ItemType;
    public bool IsProject => _model.ItemType == MapTreeItemType.Project;
    public bool IsTracksFolder => _model.ItemType == MapTreeItemType.TracksFolder;
    public bool IsTrack => _model.ItemType == MapTreeItemType.Track;
    public bool IsMapsFolder => _model.ItemType == MapTreeItemType.MapsFolder;
    public bool IsFolder => _model.ItemType == MapTreeItemType.Folder;
    public bool IsMapLayer => _model.ItemType == MapTreeItemType.MapLayer;
    public MapLayer? Layer => _model.Layer;
    public MapTrack? Track => _model.Track;
    public MapProject? Project => _model.Project;

    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    public string EditingTitle
    {
        get => _editingTitle;
        set => SetField(ref _editingTitle, value);
    }

    public ObservableCollection<MapTreeItemViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value))
            {
                _model.IsExpanded = value;
            }
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetField(ref _isChecked, value))
            {
                _model.IsChecked = value;
                if (Layer != null)
                {
                    Layer.IsVisible = value;
                }

                if (Track != null)
                {
                    Track.IsVisible = value;
                }

                // Propagate to children if folder, project, tracks section or maps section
                if (IsFolder || IsProject || IsTracksFolder || IsMapsFolder)
                {
                    foreach (MapTreeItemViewModel child in Children)
                    {
                        child.IsChecked = value;
                    }
                }

                _onVisibilityChanged?.Invoke();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string IconText => ItemType switch
    {
        MapTreeItemType.Project => "📋",
        MapTreeItemType.TracksFolder => "🚩",
        MapTreeItemType.Track => "📍",
        MapTreeItemType.MapsFolder => "🗺️",
        MapTreeItemType.Folder => "📁",
        MapTreeItemType.MapLayer => "🗺️",
        _ => "📁"
    };

    public int TotalMapsCount => CountMaps(this);
    public int TotalTracksCount => CountTracks(this);

    public string DetailsText
    {
        get
        {
            if (Layer != null)
            {
                return $"{Layer.FileSizeFormatted} · {Layer.PixelWidth:N0}×{Layer.PixelHeight:N0}";
            }

            if (Track != null)
            {
                return $"{Track.LengthKm:0.##} km · {Track.PointsCount} pts";
            }

            if (IsTracksFolder)
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.TracksCountFormat, TotalTracksCount);
            }

            if (IsMapsFolder || IsFolder)
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MapsCountFormat, TotalMapsCount);
            }

            if (IsProject)
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MapsTracksCountFormat, TotalMapsCount, TotalTracksCount);
            }

            return string.Empty;
        }
    }

    public MapTreeItemViewModel(MapTreeItem model, Action? onVisibilityChanged = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _onVisibilityChanged = onVisibilityChanged;
        _isChecked = model.IsChecked;
        _isExpanded = model.IsExpanded;

        foreach (MapTreeItem child in model.Children)
        {
            Children.Add(new MapTreeItemViewModel(child, onVisibilityChanged));
        }
    }

    public void BeginEdit()
    {
        if (IsProject)
        {
            EditingTitle = Title;
            IsEditing = true;
        }
    }

    public void CancelEdit()
    {
        IsEditing = false;
        EditingTitle = string.Empty;
    }

    public void NotifyTitleChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DetailsText));
    }

    private static int CountMaps(MapTreeItemViewModel item)
    {
        if (item.Layer != null)
        {
            return 1;
        }

        int count = 0;
        foreach (MapTreeItemViewModel child in item.Children)
        {
            count += CountMaps(child);
        }

        return count;
    }

    private static int CountTracks(MapTreeItemViewModel item)
    {
        if (item.Track != null)
        {
            return 1;
        }

        int count = 0;
        foreach (MapTreeItemViewModel child in item.Children)
        {
            count += CountTracks(child);
        }

        return count;
    }
}
