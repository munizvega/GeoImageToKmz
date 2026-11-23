using System.Globalization;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.ViewModels;

public sealed class MapViewportViewModel : ViewModelBase
{
    private GeoExtent _currentExtent = GeoExtent.Empty;
    private double _cursorGeoX;
    private double _cursorGeoY;
    private string _cursorCoordinatesText = Strings.CoordinatesEmpty;
    private bool _isCroppingMode;
    private bool _isMeasuringMode;
    private GeoExtent _selectedCropExtent = GeoExtent.Empty;
    private string _measureDistanceText = string.Empty;
    private string _renderStatusText = Strings.StatusReady;
    private string _memoryStatusText = string.Format(CultureInfo.CurrentCulture, Strings.MemoryStatusFormat, 0);
    private bool _isLoading;
    private string _loadingText = Strings.Loading;
    private double _progressValue;
    private bool _isIndeterminate = true;
    private double _zoomScale = 1.0;
    private double _pixelWidth = 800.0;
    private double _pixelHeight = 600.0;

    public GeoExtent CurrentExtent
    {
        get => _currentExtent;
        set
        {
            if (SetField(ref _currentExtent, value))
            {
                OnPropertyChanged(nameof(CenterGeoX));
                OnPropertyChanged(nameof(CenterGeoY));
                OnPropertyChanged(nameof(ExtentWidth));
                OnPropertyChanged(nameof(ExtentHeight));
            }
        }
    }

    public double CenterGeoX => _currentExtent.CenterX;
    public double CenterGeoY => _currentExtent.CenterY;
    public double ExtentWidth => _currentExtent.Width;
    public double ExtentHeight => _currentExtent.Height;

    public double CursorGeoX
    {
        get => _cursorGeoX;
        set
        {
            if (SetField(ref _cursorGeoX, value))
            {
                UpdateCursorText();
            }
        }
    }

    public double CursorGeoY
    {
        get => _cursorGeoY;
        set
        {
            if (SetField(ref _cursorGeoY, value))
            {
                UpdateCursorText();
            }
        }
    }

    public string CursorCoordinatesText
    {
        get => _cursorCoordinatesText;
        private set => SetField(ref _cursorCoordinatesText, value);
    }

    public bool IsCroppingMode
    {
        get => _isCroppingMode;
        set => SetField(ref _isCroppingMode, value);
    }

    public bool IsMeasuringMode
    {
        get => _isMeasuringMode;
        set => SetField(ref _isMeasuringMode, value);
    }

    public GeoExtent SelectedCropExtent
    {
        get => _selectedCropExtent;
        set
        {
            if (SetField(ref _selectedCropExtent, value))
            {
                OnPropertyChanged(nameof(HasSelectedCrop));
                OnPropertyChanged(nameof(CropDimensionsText));
            }
        }
    }

    public bool HasSelectedCrop => !_selectedCropExtent.IsEmpty && _selectedCropExtent.Width > 0 && _selectedCropExtent.Height > 0;

    public string CropDimensionsText => HasSelectedCrop
        ? string.Format(CultureInfo.CurrentCulture, Strings.CropDimensionsFormat, _selectedCropExtent.Width, _selectedCropExtent.Height)
        : Strings.CropDimensionsEmpty;

    public string MeasureDistanceText
    {
        get => _measureDistanceText;
        set => SetField(ref _measureDistanceText, value);
    }

    public string RenderStatusText
    {
        get => _renderStatusText;
        set => SetField(ref _renderStatusText, value);
    }

    public string MemoryStatusText
    {
        get => _memoryStatusText;
        set => SetField(ref _memoryStatusText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string LoadingText
    {
        get => _loadingText;
        set => SetField(ref _loadingText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetField(ref _progressValue, value);
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetField(ref _isIndeterminate, value);
    }

    public void SetLoading(bool isLoading, string? text = null, double progress = 0, bool isIndeterminate = true)
    {
        IsLoading = isLoading;
        LoadingText = text ?? Strings.Loading;
        ProgressValue = progress;
        IsIndeterminate = isIndeterminate;
    }

    public double ZoomScale
    {
        get => _zoomScale;
        set => SetField(ref _zoomScale, value);
    }

    public double PixelWidth
    {
        get => _pixelWidth;
        set => SetField(ref _pixelWidth, value);
    }

    public double PixelHeight
    {
        get => _pixelHeight;
        set => SetField(ref _pixelHeight, value);
    }

    public event EventHandler? ViewportExtentChanged;

    public void InvalidateViewport()
    {
        ViewportExtentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtent(GeoExtent extent)
    {
        if (!extent.IsEmpty)
        {
            CurrentExtent = extent;
            ViewportExtentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Pan(double dxGeo, double dyGeo)
    {
        if (_currentExtent.IsEmpty)
        {
            return;
        }

        CurrentExtent = new GeoExtent(
            _currentExtent.MinX + dxGeo,
            _currentExtent.MinY + dyGeo,
            _currentExtent.MaxX + dxGeo,
            _currentExtent.MaxY + dyGeo);

        ViewportExtentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomAt(double anchorGeoX, double anchorGeoY, double zoomFactor)
    {
        if (_currentExtent.IsEmpty || zoomFactor <= 0)
        {
            return;
        }

        double newWidth = _currentExtent.Width / zoomFactor;
        double newHeight = _currentExtent.Height / zoomFactor;

        // Keep anchor point in the same relative position
        double relX = (_currentExtent.Width > 0) ? (anchorGeoX - _currentExtent.MinX) / _currentExtent.Width : 0.5;
        double relY = (_currentExtent.Height > 0) ? (anchorGeoY - _currentExtent.MinY) / _currentExtent.Height : 0.5;

        double newMinX = anchorGeoX - (relX * newWidth);
        double newMinY = anchorGeoY - (relY * newHeight);
        double newMaxX = newMinX + newWidth;
        double newMaxY = newMinY + newHeight;

        CurrentExtent = new GeoExtent(newMinX, newMinY, newMaxX, newMaxY);
        ZoomScale *= zoomFactor;
        ViewportExtentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        SelectedCropExtent = GeoExtent.Empty;
        MeasureDistanceText = string.Empty;
    }

    private void UpdateCursorText()
    {
        CursorCoordinatesText = string.Format(CultureInfo.CurrentCulture, Strings.CoordinatesPrefix, _cursorGeoX, _cursorGeoY);
    }
}
