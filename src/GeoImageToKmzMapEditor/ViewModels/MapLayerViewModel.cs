using System.Globalization;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.ViewModels;

public sealed class MapLayerViewModel : ViewModelBase
{
    private readonly MapLayer _layer;
    private bool _isSelected;

    public MapLayer Layer => _layer;

    public string Name => _layer.Name;
    public string FilePath => _layer.FilePath;
    public string FileSizeFormatted => _layer.FileSizeFormatted;
    public string DimensionsText => $"{_layer.PixelWidth:N0} × {_layer.PixelHeight:N0} px";
    public string ProjectionName => _layer.ProjectionName;
    public string BandCountText => _layer.BandCount == 1 ? Strings.BandSingular : string.Format(CultureInfo.CurrentCulture, Strings.BandPlural, _layer.BandCount);
    public string ExtentText => $"[{_layer.Extent.MinX:F2}, {_layer.Extent.MinY:F2}] - [{_layer.Extent.MaxX:F2}, {_layer.Extent.MaxY:F2}]";

    public bool IsVisible
    {
        get => _layer.IsVisible;
        set
        {
            if (_layer.IsVisible != value)
            {
                _layer.IsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public double Opacity
    {
        get => _layer.Opacity;
        set
        {
            double clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_layer.Opacity - clamped) > 0.001)
            {
                _layer.Opacity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OpacityPercentText));
            }
        }
    }

    public string OpacityPercentText => $"{(int)(_layer.Opacity * 100)}%";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public MapLayerViewModel(MapLayer layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }
}
