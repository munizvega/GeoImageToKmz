using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Threading;
using TifToKmz;
using TifToKmz.Geometry;
using TifToKmz.GpxTools;
using TifToKmz.ImageTools.Brighteners;
using TifToKmz.ImageTools.GeoTransforming;
using TifToKmz.ImageTools.Mergers;
using TifToKmz.ImageTools.Resizers;
using TifToKmz.ImageTools.Splitters;
using TifToKmz.Kmz;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;

namespace TifToKmzMapEditor.ViewModels;

public sealed class TrackExportOption
{
    public string DisplayName { get; }
    public MapTrack? Track { get; }

    public TrackExportOption(string displayName, MapTrack? track = null)
    {
        DisplayName = displayName;
        Track = track;
    }

    public override string ToString() => DisplayName;
}

public sealed class ExportAreaModeOption
{
    public ExportAreaMode Mode { get; }
    public string DisplayName { get; }

    public ExportAreaModeOption(ExportAreaMode mode, string displayName)
    {
        Mode = mode;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}

public sealed class MapExportViewModel : ViewModelBase
{
    private const string _configFileName = "export.editor.json";
    private readonly MainViewModel _mainVm;
    private string _outputDirectory = string.Empty;
    private string _kmzFileName = Strings.DefaultKmzName;
    private int _tileSize = 1024;
    private int _maxTiles = 250;
    private double _scale = 1.0;
    private int _quality = 100;
    private bool _brightnessEnabled = true;
    private double _gamma = 1.6;
    private double _brightness = 8.0;
    private double _contrast = 1.0;
    private bool _exportAll = true;
    private bool _exportCrop;
    private ExportAreaModeOption? _selectedModeOption;
    private TrackExportOption? _selectedTrackOption;
    private TrackExportOption? _selectedTilesTrackOption;
    private double _trackMargin = 100.0;
    private double _selectedTilesTrackMargin = 100.0;
    private bool _lockTileSize = true;
    private bool _selectTrackTiles;
    private bool _isExporting;
    private bool _isCancelling;
    private int _currentStep;
    private int _totalSteps;
    private int _completedSteps;
    private double _totalProgressValue;
    private double _stepProgressValue;
    private string _stepLabel = string.Empty;
    private string _stepDescription = string.Empty;
    private string _stepPercentageText = string.Empty;
    private string _statusMessage = Strings.KmzExportReadyStatus;
    private bool _hasStatusError;
    private bool _hasStatusSuccess;
    private bool _isSuppressingDirty;
    private CancellationTokenSource? _exportCts;

    public ObservableCollection<ExportAreaModeOption> AvailableModes { get; } = new();
    public ObservableCollection<TrackExportOption> AvailableTracks { get; } = new();
    public ObservableCollection<GeoExtent> SelectedTiles { get; } = new();
    public bool HasTracks => AvailableTracks.Count > 0;

    public MapExportViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));
        LoadConfig();

        string cleanName = _mainVm.ProjectTitleText.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(c, '_');
        }
        _kmzFileName = !string.IsNullOrWhiteSpace(cleanName) ? $"{cleanName}.kmz" : Strings.DefaultKmzName;

        SelectedTiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedTilesCount));
            OnPropertyChanged(nameof(SelectedTilesCounterText));
            if (!_isSuppressingDirty)
            {
                _mainVm.MarkProjectDirty();
            }
        };

        _mainVm.ProjectService.LayersChanged += (_, _) => RefreshTracks();
        _mainVm.ProjectService.ProjectsChanged += (_, _) => RefreshTracks();
        _mainVm.LanguageService.LanguageChanged += (_, _) =>
        {
            RefreshModes();
            RefreshTracks();
        };

        PropertyChanged += (_, e) =>
        {
            if (!_isSuppressingDirty && e.PropertyName is not (
                nameof(IsExporting) or nameof(IsCancelling) or nameof(CurrentStep) or
                nameof(TotalSteps) or nameof(CompletedSteps) or nameof(TotalProgressValue) or
                nameof(StepProgressValue) or nameof(StepLabel) or nameof(StepDescription) or
                nameof(StepPercentageText) or nameof(StatusMessage) or nameof(HasStatusError) or
                nameof(HasStatusSuccess) or nameof(IsTrackSelected) or nameof(SelectedTrack) or
                nameof(HasSelectedCrop) or nameof(SelectedCropText) or nameof(SelectedTilesCount) or
                nameof(SelectedTilesCounterText)))
            {
                _mainVm.MarkProjectDirty();
            }
        };

        RefreshModes();
        RefreshTracks();
        UpdateCropInfo();
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (SetField(ref _outputDirectory, value))
            {
                if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
                {
                    SaveConfig();
                }
            }
        }
    }

    public string KmzFileName
    {
        get => _kmzFileName;
        set => SetField(ref _kmzFileName, value);
    }

    public int TileSize
    {
        get => _tileSize;
        set
        {
            if (SetField(ref _tileSize, Math.Clamp(value, 256, 4096)))
            {
                _mainVm.Viewport.InvalidateViewport();
            }
        }
    }

    public int MaxTiles
    {
        get => _maxTiles;
        set
        {
            if (SetField(ref _maxTiles, Math.Clamp(value, 1, 1000)))
            {
                OnPropertyChanged(nameof(SelectedTilesCounterText));
            }
        }
    }

    public double Scale
    {
        get => _scale;
        set => SetField(ref _scale, Math.Clamp(value, 0.1, 10.0));
    }

    public int Quality
    {
        get => _quality;
        set => SetField(ref _quality, Math.Clamp(value, 1, 100));
    }

    public bool BrightnessEnabled
    {
        get => _brightnessEnabled;
        set => SetField(ref _brightnessEnabled, value);
    }

    public double Gamma
    {
        get => _gamma;
        set => SetField(ref _gamma, Math.Clamp(value, 0.1, 10.0));
    }

    public double Brightness
    {
        get => _brightness;
        set => SetField(ref _brightness, Math.Clamp(value, 0.0, 100.0));
    }

    public double Contrast
    {
        get => _contrast;
        set => SetField(ref _contrast, Math.Clamp(value, 0.1, 10.0));
    }

    public bool ExportAll
    {
        get => _exportAll;
        set
        {
            if (SetField(ref _exportAll, value) && value)
            {
                ExportCrop = false;
            }
        }
    }

    public bool ExportCrop
    {
        get => _exportCrop;
        set
        {
            if (SetField(ref _exportCrop, value) && value)
            {
                ExportAll = false;
            }
        }
    }

    public ExportAreaModeOption? SelectedModeOption
    {
        get => _selectedModeOption;
        set
        {
            if (SetField(ref _selectedModeOption, value))
            {
                OnPropertyChanged(nameof(SelectedMode));
                OnPropertyChanged(nameof(IsTrackMode));
                OnPropertyChanged(nameof(IsSelectedTilesMode));
                OnPropertyChanged(nameof(IsSelectedMapsMode));
                UpdateTrackContourCrop();
                _mainVm.Viewport.InvalidateViewport();
            }
        }
    }

    public ExportAreaMode SelectedMode => _selectedModeOption?.Mode ?? ExportAreaMode.SelectedMaps;

    public bool IsTrackMode => SelectedMode == ExportAreaMode.TrackContour;

    public bool IsSelectedTilesMode => SelectedMode == ExportAreaMode.SelectedTiles;

    public bool IsSelectedMapsMode => SelectedMode == ExportAreaMode.SelectedMaps;

    public bool LockTileSize
    {
        get => _lockTileSize;
        set
        {
            if (SetField(ref _lockTileSize, value))
            {
                _mainVm.Viewport.InvalidateViewport();
            }
        }
    }

    public bool SelectTrackTiles
    {
        get => _selectTrackTiles;
        set
        {
            if (SetField(ref _selectTrackTiles, value && HasTracks))
            {
                OnPropertyChanged(nameof(IsTrackTilesSubPanelVisible));
            }
        }
    }

    public bool IsTrackTilesSubPanelVisible => IsSelectedTilesMode && SelectTrackTiles;

    public TrackExportOption? SelectedTilesTrackOption
    {
        get => _selectedTilesTrackOption ?? SelectedTrackOption ?? AvailableTracks.FirstOrDefault();
        set
        {
            if (SetField(ref _selectedTilesTrackOption, value))
            {
                OnPropertyChanged(nameof(SelectedTilesTrack));
            }
        }
    }

    public MapTrack? SelectedTilesTrack => _selectedTilesTrackOption?.Track ?? SelectedTrack;

    public int SelectedTilesCount => SelectedTiles.Count;

    public string SelectedTilesCounterText => string.Format(CultureInfo.InvariantCulture, Strings.ExportSelectedTilesCounterFormat, SelectedTiles.Count, MaxTiles);

    public void ClearSelectedTiles()
    {
        SelectedTiles.Clear();
        OnPropertyChanged(nameof(SelectedTilesCount));
        OnPropertyChanged(nameof(SelectedTilesCounterText));
        _mainVm.Viewport.InvalidateViewport();
    }

    public void OptimizeSelectedTiles()
    {
        if (SelectedTiles.Count == 0)
        {
            return;
        }

        int maxTiles = MaxTiles > 0 ? MaxTiles : 250;
        int currentCount = SelectedTiles.Count;

        if (currentCount >= maxTiles)
        {
            return;
        }

        int maxFactor = maxTiles / currentCount;
        if (maxFactor <= 1)
        {
            return;
        }

        int bestCols = 1;
        int bestRows = 1;
        int bestProduct = 1;
        int bestDiff = int.MaxValue;

        for (int c = 1; c <= 100; c++)
        {
            for (int r = 1; r <= 100; r++)
            {
                int product = c * r;
                if (product > maxFactor)
                {
                    continue;
                }

                if (Math.Max(c, r) > 2 * Math.Min(c, r))
                {
                    continue;
                }

                int diff = Math.Abs(c - r);

                if (product > bestProduct || (product == bestProduct && diff < bestDiff))
                {
                    bestProduct = product;
                    bestDiff = diff;
                    bestCols = c;
                    bestRows = r;
                }
            }
        }

        if (bestCols <= 1 && bestRows <= 1)
        {
            return;
        }

        List<GeoExtent> originalTiles = SelectedTiles.ToList();
        List<GeoExtent> newTiles = new(originalTiles.Count * bestCols * bestRows);

        foreach (GeoExtent tile in originalTiles)
        {
            double stepW = tile.Width / bestCols;
            double stepH = tile.Height / bestRows;

            for (int r = 0; r < bestRows; r++)
            {
                for (int c = 0; c < bestCols; c++)
                {
                    double gx1 = tile.MinX + c * stepW;
                    double gx2 = (c == bestCols - 1) ? tile.MaxX : tile.MinX + (c + 1) * stepW;
                    double gy1 = tile.MinY + r * stepH;
                    double gy2 = (r == bestRows - 1) ? tile.MaxY : tile.MinY + (r + 1) * stepH;

                    newTiles.Add(new GeoExtent(gx1, gy1, gx2, gy2));
                }
            }
        }

        _isSuppressingDirty = true;
        try
        {
            SelectedTiles.Clear();
            foreach (GeoExtent tile in newTiles)
            {
                SelectedTiles.Add(tile);
            }
        }
        finally
        {
            _isSuppressingDirty = false;
        }

        _mainVm.MarkProjectDirty();
        OnPropertyChanged(nameof(SelectedTilesCount));
        OnPropertyChanged(nameof(SelectedTilesCounterText));
        _mainVm.Viewport.InvalidateViewport();
    }

    public void RefreshModes()
    {
        ExportAreaMode currentMode = SelectedMode;
        AvailableModes.Clear();
        ExportAreaModeOption trackOpt = new(ExportAreaMode.TrackContour, Strings.ExportModeTrackContour);
        ExportAreaModeOption tilesOpt = new(ExportAreaMode.SelectedTiles, Strings.ExportModeSelectedTiles);
        ExportAreaModeOption mapsOpt = new(ExportAreaMode.SelectedMaps, Strings.ExportModeSelectedMaps);
        AvailableModes.Add(trackOpt);
        AvailableModes.Add(tilesOpt);
        AvailableModes.Add(mapsOpt);

        SelectedModeOption = AvailableModes.FirstOrDefault(m => m.Mode == currentMode) ?? mapsOpt;
    }

    public bool HasSelectedCrop => _mainVm.Viewport.HasSelectedCrop;

    public string SelectedCropText => HasSelectedCrop
        ? string.Format(CultureInfo.CurrentCulture, Strings.KmzExportCropFormat, _mainVm.Viewport.SelectedCropExtent.Width, _mainVm.Viewport.SelectedCropExtent.Height)
        : Strings.KmzExportCropNone;

    public TrackExportOption? SelectedTrackOption
    {
        get => _selectedTrackOption;
        set
        {
            if (SetField(ref _selectedTrackOption, value))
            {
                OnPropertyChanged(nameof(SelectedTrack));
                OnPropertyChanged(nameof(IsTrackSelected));
                if (IsTrackMode)
                {
                    UpdateTrackContourCrop();
                }
            }
        }
    }

    public MapTrack? SelectedTrack => _selectedTrackOption?.Track;

    public bool IsTrackSelected => SelectedTrack != null;

    public double TrackMargin
    {
        get => _trackMargin;
        set
        {
            if (SetField(ref _trackMargin, Math.Max(0.0, value)))
            {
                if (IsTrackMode)
                {
                    UpdateTrackContourCrop();
                }
            }
        }
    }

    public double SelectedTilesTrackMargin
    {
        get => _selectedTilesTrackMargin;
        set => SetField(ref _selectedTilesTrackMargin, Math.Max(0.0, value));
    }

    public void UpdateTrackContourCrop()
    {
        if (IsTrackMode)
        {
            if (SelectedTrack != null && !SelectedTrack.Extent.IsEmpty)
            {
                double margin = TrackMargin;
                GeoExtent ext = SelectedTrack.Extent;
                _mainVm.Viewport.SelectedCropExtent = new GeoExtent(
                    ext.MinX - margin,
                    ext.MinY - margin,
                    ext.MaxX + margin,
                    ext.MaxY + margin);
            }
            else
            {
                _mainVm.Viewport.SelectedCropExtent = GeoExtent.Empty;
            }
        }
        else
        {
            _mainVm.Viewport.SelectedCropExtent = GeoExtent.Empty;
        }
    }

    public void RefreshTracks()
    {
        MapTrack? previousTrack = SelectedTrack;
        MapTrack? previousTilesTrack = _selectedTilesTrackOption?.Track;
        AvailableTracks.Clear();

        TrackExportOption? optionToSelect = null;
        TrackExportOption? tilesOptionToSelect = null;

        foreach (MapTrack track in _mainVm.ProjectService.Tracks)
        {
            TrackExportOption opt = new(track.Name, track);
            AvailableTracks.Add(opt);
            if (previousTrack != null && string.Equals(track.FilePath, previousTrack.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                optionToSelect = opt;
            }
            if (previousTilesTrack != null && string.Equals(track.FilePath, previousTilesTrack.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                tilesOptionToSelect = opt;
            }
        }

        SelectedTrackOption = optionToSelect ?? AvailableTracks.FirstOrDefault();
        SelectedTilesTrackOption = tilesOptionToSelect ?? optionToSelect ?? AvailableTracks.FirstOrDefault();
        OnPropertyChanged(nameof(HasTracks));
        if (!HasTracks)
        {
            bool wasSuppressingDirty = _isSuppressingDirty;
            _isSuppressingDirty = true;
            SelectTrackTiles = false;
            _isSuppressingDirty = wasSuppressingDirty;
        }
        if (IsTrackMode)
        {
            UpdateTrackContourCrop();
        }
    }

    public MapProjectExportData GetExportData()
    {
        return new MapProjectExportData
        {
            OutputDirectory = OutputDirectory,
            KmzFileName = KmzFileName,
            TileSize = TileSize,
            MaxTiles = MaxTiles,
            Scale = Scale,
            Quality = Quality,
            BrightnessEnabled = BrightnessEnabled,
            Gamma = Gamma,
            Brightness = Brightness,
            Contrast = Contrast,
            ExportAll = ExportAll,
            ExportCrop = ExportCrop,
            Mode = SelectedMode,
            SelectedTrackFilePath = SelectedTrack?.FilePath,
            TrackMargin = TrackMargin,
            LockTileSize = LockTileSize,
            SelectTrackTiles = SelectTrackTiles,
            SelectedTilesTrackFilePath = SelectedTilesTrack?.FilePath,
            SelectedTilesTrackMargin = SelectedTilesTrackMargin,
            SelectedTiles = SelectedTiles.Select(t => new MapProjectExtentData
            {
                MinX = t.MinX,
                MinY = t.MinY,
                MaxX = t.MaxX,
                MaxY = t.MaxY
            }).ToList()
        };
    }

    public void LoadExportData(MapProjectExportData? data)
    {
        if (data == null)
        {
            return;
        }

        _isSuppressingDirty = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(data.OutputDirectory))
            {
                OutputDirectory = data.OutputDirectory;
            }

            if (!string.IsNullOrWhiteSpace(data.KmzFileName))
            {
                KmzFileName = data.KmzFileName;
            }

            TileSize = data.TileSize;
            MaxTiles = data.MaxTiles;
            Scale = data.Scale;
            Quality = data.Quality;
            BrightnessEnabled = data.BrightnessEnabled;
            Gamma = data.Gamma;
            Brightness = data.Brightness;
            Contrast = data.Contrast;
            ExportAll = data.ExportAll;
            ExportCrop = data.ExportCrop;
            TrackMargin = data.TrackMargin;
            LockTileSize = data.LockTileSize;
            SelectTrackTiles = data.SelectTrackTiles;
            SelectedTilesTrackMargin = data.SelectedTilesTrackMargin;

            RefreshModes();
            SelectedModeOption = AvailableModes.FirstOrDefault(m => m.Mode == data.Mode) ?? AvailableModes.FirstOrDefault();

            SelectedTiles.Clear();
            if (data.SelectedTiles != null)
            {
                foreach (MapProjectExtentData ext in data.SelectedTiles)
                {
                    SelectedTiles.Add(new GeoExtent(ext.MinX, ext.MinY, ext.MaxX, ext.MaxY));
                }
            }

            RefreshTracks();
            if (!string.IsNullOrWhiteSpace(data.SelectedTrackFilePath))
            {
                TrackExportOption? match = AvailableTracks.FirstOrDefault(t => t.Track != null && string.Equals(t.Track.FilePath, data.SelectedTrackFilePath, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectedTrackOption = match;
                }
            }

            if (!string.IsNullOrWhiteSpace(data.SelectedTilesTrackFilePath))
            {
                TrackExportOption? matchTiles = AvailableTracks.FirstOrDefault(t => t.Track != null && string.Equals(t.Track.FilePath, data.SelectedTilesTrackFilePath, StringComparison.OrdinalIgnoreCase));
                if (matchTiles != null)
                {
                    SelectedTilesTrackOption = matchTiles;
                }
            }
        }
        finally
        {
            _isSuppressingDirty = false;
        }
    }

    public void SelectTilesForTrack(MapTrack? track = null, double? marginInMeters = null)
    {
        track ??= SelectedTilesTrackOption?.Track ?? SelectedTrack ?? AvailableTracks.FirstOrDefault()?.Track;
        if (track == null || track.ProjectedPoints.Count == 0)
        {
            return;
        }

        GeoExtent current = _mainVm.Viewport.CurrentExtent;
        if (current.IsEmpty || current.Width <= 0 || current.Height <= 0)
        {
            current = track.Extent;
            if (current.IsEmpty || current.Width <= 0 || current.Height <= 0)
            {
                return;
            }
        }

        double geoTileWidth;
        double geoTileHeight;

        if (LockTileSize && SelectedTiles.Count > 0)
        {
            geoTileWidth = SelectedTiles[0].Width;
            geoTileHeight = SelectedTiles[0].Height;
        }
        else
        {
            double viewW = _mainVm.Viewport.PixelWidth > 0 ? _mainVm.Viewport.PixelWidth : 800.0;
            double viewH = _mainVm.Viewport.PixelHeight > 0 ? _mainVm.Viewport.PixelHeight : 600.0;
            geoTileWidth = (180.0 / viewW) * current.Width;
            geoTileHeight = (180.0 / viewH) * current.Height;
        }

        if (geoTileWidth <= 0 || geoTileHeight <= 0)
        {
            return;
        }

        double margin = Math.Max(0.0, marginInMeters ?? SelectedTilesTrackMargin);
        double marginX = margin;
        double marginY = margin;

        if (track.Extent.Width < 5.0 && track.Extent.Height < 5.0 && Math.Abs(track.Extent.MinY) <= 90.0)
        {
            double avgLat = (track.Extent.MinY + track.Extent.MaxY) / 2.0;
            double latRad = avgLat * Math.PI / 180.0;
            double cosLat = Math.Max(0.01, Math.Cos(latRad));
            marginX = margin / (111320.0 * cosLat);
            marginY = margin / 111320.0;
        }

        HashSet<(long Col, long Row)> visited = new();
        List<(long Col, long Row)> orderedTiles = new();

        IReadOnlyList<(double X, double Y)> pts = track.ProjectedPoints;
        if (pts.Count == 1)
        {
            double px = pts[0].X;
            double py = pts[0].Y;
            long minCol = (long)Math.Floor((px - marginX) / geoTileWidth);
            long maxCol = (long)Math.Floor((px + marginX) / geoTileWidth);
            long minRow = (long)Math.Floor((py - marginY) / geoTileHeight);
            long maxRow = (long)Math.Floor((py + marginY) / geoTileHeight);

            for (long c = minCol; c <= maxCol; c++)
            {
                for (long r = minRow; r <= maxRow; r++)
                {
                    double rx1 = c * geoTileWidth;
                    double rx2 = (c + 1) * geoTileWidth;
                    double ry1 = r * geoTileHeight;
                    double ry2 = (r + 1) * geoTileHeight;

                    if (PointWithinDistanceToRect(px, py, rx1, ry1, rx2, ry2, marginX, marginY))
                    {
                        visited.Add((c, r));
                        orderedTiles.Add((c, r));
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double x1 = pts[i].X;
                double y1 = pts[i].Y;
                double x2 = pts[i + 1].X;
                double y2 = pts[i + 1].Y;

                long minCol = (long)Math.Floor((Math.Min(x1, x2) - marginX) / geoTileWidth);
                long maxCol = (long)Math.Floor((Math.Max(x1, x2) + marginX) / geoTileWidth);
                long minRow = (long)Math.Floor((Math.Min(y1, y2) - marginY) / geoTileHeight);
                long maxRow = (long)Math.Floor((Math.Max(y1, y2) + marginY) / geoTileHeight);

                for (long c = minCol; c <= maxCol; c++)
                {
                    for (long r = minRow; r <= maxRow; r++)
                    {
                        if (visited.Contains((c, r)))
                        {
                            continue;
                        }

                        double rx1 = c * geoTileWidth;
                        double rx2 = (c + 1) * geoTileWidth;
                        double ry1 = r * geoTileHeight;
                        double ry2 = (r + 1) * geoTileHeight;

                        if (SegmentWithinDistanceToRect(x1, y1, x2, y2, rx1, ry1, rx2, ry2, marginX, marginY))
                        {
                            visited.Add((c, r));
                            orderedTiles.Add((c, r));
                        }
                    }
                }
            }
        }

        SelectedTiles.Clear();
        foreach ((long c, long r) in orderedTiles)
        {
            if (SelectedTiles.Count >= MaxTiles)
            {
                break;
            }

            double gx1 = c * geoTileWidth;
            double gx2 = (c + 1) * geoTileWidth;
            double gy1 = r * geoTileHeight;
            double gy2 = (r + 1) * geoTileHeight;

            SelectedTiles.Add(new GeoExtent(gx1, gy1, gx2, gy2));
        }

        _mainVm.Viewport.InvalidateViewport();
    }

    private static bool PointWithinDistanceToRect(double px, double py, double minX, double minY, double maxX, double maxY, double marginX, double marginY)
    {
        double dx = Math.Max(0.0, Math.Max(minX - px, px - maxX));
        double dy = Math.Max(0.0, Math.Max(minY - py, py - maxY));

        if (marginX <= 0 && marginY <= 0)
        {
            return dx == 0 && dy == 0;
        }

        if (Math.Abs(marginX - marginY) < 1e-9)
        {
            return (dx * dx) + (dy * dy) <= marginX * marginX;
        }

        double normX = marginX > 0 ? dx / marginX : (dx > 0 ? 2.0 : 0.0);
        double normY = marginY > 0 ? dy / marginY : (dy > 0 ? 2.0 : 0.0);
        return (normX * normX) + (normY * normY) <= 1.0;
    }

    private static bool SegmentWithinDistanceToRect(double x1, double y1, double x2, double y2, double minX, double minY, double maxX, double maxY, double marginX, double marginY)
    {
        if (SegmentIntersectsRect(x1, y1, x2, y2, minX, minY, maxX, maxY))
        {
            return true;
        }

        if (marginX <= 0 && marginY <= 0)
        {
            return false;
        }

        if (PointWithinDistanceToRect(x1, y1, minX, minY, maxX, maxY, marginX, marginY) ||
            PointWithinDistanceToRect(x2, y2, minX, minY, maxX, maxY, marginX, marginY))
        {
            return true;
        }

        return PointWithinDistanceToSegment(minX, minY, x1, y1, x2, y2, marginX, marginY) ||
               PointWithinDistanceToSegment(minX, maxY, x1, y1, x2, y2, marginX, marginY) ||
               PointWithinDistanceToSegment(maxX, minY, x1, y1, x2, y2, marginX, marginY) ||
               PointWithinDistanceToSegment(maxX, maxY, x1, y1, x2, y2, marginX, marginY);
    }

    private static bool PointWithinDistanceToSegment(double px, double py, double x1, double y1, double x2, double y2, double marginX, double marginY)
    {
        double segDx = x2 - x1;
        double segDy = y2 - y1;
        double lenSq = (segDx * segDx) + (segDy * segDy);

        double closestX;
        double closestY;

        if (lenSq < 1e-12)
        {
            closestX = x1;
            closestY = y1;
        }
        else
        {
            double t = Math.Clamp((((px - x1) * segDx) + ((py - y1) * segDy)) / lenSq, 0.0, 1.0);
            closestX = x1 + (t * segDx);
            closestY = y1 + (t * segDy);
        }

        double dx = Math.Abs(px - closestX);
        double dy = Math.Abs(py - closestY);

        if (Math.Abs(marginX - marginY) < 1e-9)
        {
            return (dx * dx) + (dy * dy) <= marginX * marginX;
        }

        double normX = marginX > 0 ? dx / marginX : (dx > 0 ? 2.0 : 0.0);
        double normY = marginY > 0 ? dy / marginY : (dy > 0 ? 2.0 : 0.0);
        return (normX * normX) + (normY * normY) <= 1.0;
    }

    private static bool SegmentIntersectsRect(double x1, double y1, double x2, double y2, double minX, double minY, double maxX, double maxY)
    {
        if ((x1 >= minX && x1 <= maxX && y1 >= minY && y1 <= maxY) ||
            (x2 >= minX && x2 <= maxX && y2 >= minY && y2 <= maxY))
        {
            return true;
        }

        return LineSegmentsIntersect(x1, y1, x2, y2, minX, minY, maxX, minY) ||
               LineSegmentsIntersect(x1, y1, x2, y2, minX, maxY, maxX, maxY) ||
               LineSegmentsIntersect(x1, y1, x2, y2, minX, minY, minX, maxY) ||
               LineSegmentsIntersect(x1, y1, x2, y2, maxX, minY, maxX, maxY);
    }

    private static bool LineSegmentsIntersect(double p1x, double p1y, double p2x, double p2y, double p3x, double p3y, double p4x, double p4y)
    {
        double d = ((p2x - p1x) * (p4y - p3y)) - ((p2y - p1y) * (p4x - p3x));
        if (Math.Abs(d) < 1e-12)
        {
            return false;
        }

        double u = (((p3x - p1x) * (p4y - p3y)) - ((p3y - p1y) * (p4x - p3x))) / d;
        double v = (((p3x - p1x) * (p2y - p1y)) - ((p3y - p1y) * (p2x - p1x))) / d;

        return u is >= 0 and <= 1 && v is >= 0 and <= 1;
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetField(ref _isExporting, value))
            {
                OnPropertyChanged(nameof(CanExport));
            }
        }
    }

    public bool IsCancelling
    {
        get => _isCancelling;
        private set => SetField(ref _isCancelling, value);
    }

    public bool CanExport => !IsExporting;

    public int CurrentStep
    {
        get => _currentStep;
        set => SetField(ref _currentStep, value);
    }

    public int TotalSteps
    {
        get => _totalSteps;
        set => SetField(ref _totalSteps, value);
    }

    public int CompletedSteps
    {
        get => _completedSteps;
        set => SetField(ref _completedSteps, value);
    }

    public double TotalProgressValue
    {
        get => _totalProgressValue;
        set => SetField(ref _totalProgressValue, value);
    }

    public double StepProgressValue
    {
        get => _stepProgressValue;
        set => SetField(ref _stepProgressValue, value);
    }

    public double ProgressValue
    {
        get => _stepProgressValue;
        set => StepProgressValue = value;
    }

    public string StepLabel
    {
        get => _stepLabel;
        set => SetField(ref _stepLabel, value);
    }

    public string StepDescription
    {
        get => _stepDescription;
        set => SetField(ref _stepDescription, value);
    }

    public string StepPercentageText
    {
        get => _stepPercentageText;
        set => SetField(ref _stepPercentageText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool HasStatusError
    {
        get => _hasStatusError;
        set => SetField(ref _hasStatusError, value);
    }

    public bool HasStatusSuccess
    {
        get => _hasStatusSuccess;
        set => SetField(ref _hasStatusSuccess, value);
    }

    public void UpdateProjectDefaults(string projectName, string? projectFilePath)
    {
        _isSuppressingDirty = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                string cleanName = projectName.Trim();
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    cleanName = cleanName.Replace(c, '_');
                }

                if (!string.IsNullOrWhiteSpace(cleanName))
                {
                    KmzFileName = $"{cleanName}.kmz";
                }
            }

            if (!string.IsNullOrWhiteSpace(projectFilePath))
            {
                string? dir = Path.GetDirectoryName(projectFilePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    OutputDirectory = dir;
                }
            }
        }
        finally
        {
            _isSuppressingDirty = false;
        }
    }

    public void UpdateCropInfo()
    {
        OnPropertyChanged(nameof(HasSelectedCrop));
        OnPropertyChanged(nameof(SelectedCropText));

        if (!HasSelectedCrop && ExportCrop)
        {
            ExportAll = true;
        }
        else if (HasSelectedCrop && !ExportCrop && !ExportAll)
        {
            ExportCrop = true;
        }
    }

    public void CancelExport()
    {
        if (_exportCts != null && !_exportCts.IsCancellationRequested)
        {
            IsCancelling = true;
            StatusMessage = Strings.ExportCancellingStatus;
            _exportCts.Cancel();
        }
    }

    public async Task ExportKmzAsync()
    {
        if (IsExporting)
        {
            return;
        }

        string outDir = OutputDirectory?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outDir))
        {
            HasStatusError = true;
            HasStatusSuccess = false;
            StatusMessage = Strings.KmzExportErrorOutputPath;
            return;
        }

        if (!Directory.Exists(outDir))
        {
            try
            {
                Directory.CreateDirectory(outDir);
            }
            catch (Exception ex)
            {
                HasStatusError = true;
                HasStatusSuccess = false;
                StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.KmzExportErrorPrefix, ex.Message);
                return;
            }
        }

        string fileName = KmzFileName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            HasStatusError = true;
            HasStatusSuccess = false;
            StatusMessage = Strings.ExportErrorInvalidFileName;
            return;
        }

        if (!fileName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".kmz";
        }

        string fullOutputPath = Path.Combine(outDir, fileName);

        var visibleLayers = _mainVm.GetVisibleLayers();
        if (visibleLayers.Count == 0)
        {
            // If active project has layers that are visible, fallback to active project layers
            if (_mainVm.ProjectService.ActiveProject != null)
            {
                visibleLayers = _mainVm.ProjectService.ActiveProject.Layers.Where(l => l.IsVisible).ToList();
            }

            if (visibleLayers.Count == 0)
            {
                HasStatusError = true;
                HasStatusSuccess = false;
                StatusMessage = Strings.KmzExportErrorNoLayers;
                return;
            }
        }

        MapTrack? selectedTrack = SelectedTrack;
        bool useTrackCrop = SelectedMode == ExportAreaMode.TrackContour;
        double trackMargin = TrackMargin;
        List<string> inputFiles = visibleLayers.Select(l => l.FilePath).Distinct().ToList();

        if (SelectedMode == ExportAreaMode.TrackContour && selectedTrack == null)
        {
            HasStatusError = true;
            HasStatusSuccess = false;
            StatusMessage = Strings.ExportNoTrackSelectedError;
            return;
        }

        if (SelectedMode == ExportAreaMode.SelectedTiles && SelectedTiles.Count == 0)
        {
            HasStatusError = true;
            HasStatusSuccess = false;
            StatusMessage = Strings.ExportNoTilesSelectedError;
            return;
        }

        List<GeoExtent> selectedTilesSnapshot = SelectedTiles.ToList();

        int totalSteps = SelectedMode switch
        {
            ExportAreaMode.TrackContour => 7,
            ExportAreaMode.SelectedTiles => 6,
            _ => (inputFiles.Count > 1 ? 7 : 6)
        };

        TotalSteps = totalSteps;
        CurrentStep = 1;
        CompletedSteps = 0;
        TotalProgressValue = 0;
        StepProgressValue = 0;
        StepLabel = string.Format(CultureInfo.CurrentCulture, Strings.ProgressStepFormat, CurrentStep, TotalSteps);
        StepDescription = SelectedMode == ExportAreaMode.TrackContour ? Strings.StepCroppingTrack : string.Empty;
        StepPercentageText = "0 %";

        IsExporting = true;
        IsCancelling = false;
        HasStatusError = false;
        HasStatusSuccess = false;
        StatusMessage = Strings.KmzExportExportingStatus;

        _exportCts = new CancellationTokenSource();
        CancellationToken ct = _exportCts.Token;

        int tileSize = TileSize;
        int maxTiles = MaxTiles;
        int quality = Quality;
        bool brightenEnabled = BrightnessEnabled;
        double gamma = Gamma;
        double brightness = Brightness;
        double contrast = Contrast;
        ExportAreaMode exportMode = SelectedMode;

        try
        {
            await Task.Run(() =>
            {
                string workspaceDir = _mainVm.TempDirectoryService.EffectiveTempDirectory;
                KmzProcessOptions options = new()
                {
                    Kmz = new KmzOptions(workspaceDir)
                    {
                        TileSize = tileSize,
                        MaxNumberOfTiles = maxTiles,
                        ResizeFactor = 1.0,
                        JpegQuality = quality
                    },
                    Reproject = new ReprojectOptions
                    {
                        WarpMemory = 1024,
                        ResamplingMethod = "cubicspline",
                        Multi = true,
                        DestinationAlpha = true
                    },
                    Resize = new ResizeOptions
                    {
                        Format = "COG"
                    },
                    Brighten = new BrightenOptions
                    {
                        Enabled = brightenEnabled,
                        Gamma = gamma,
                        Brightness = brightness,
                        Contrast = contrast
                    }
                };

                SplitOptions split = SplitOptionsBuilder.WithCreationOptions(options.Split.CreationOptions)
                    .WithTileHeight(options.Kmz.TileSize).WithColumnWidth(options.Kmz.TileSize);
                options.Split = split;

                int currentStep = 0;

                void AdvanceStep(string description)
                {
                    currentStep++;
                    Dispatcher.UIThread.Post(() =>
                    {
                        CurrentStep = Math.Min(currentStep, TotalSteps);
                        CompletedSteps = currentStep - 1;
                        TotalProgressValue = TotalSteps > 0 ? ((double)CompletedSteps / TotalSteps) * 100 : 0;
                        StepProgressValue = 0;
                        StepLabel = string.Format(CultureInfo.CurrentCulture, Strings.ProgressStepFormat, CurrentStep, TotalSteps);
                        StepDescription = description;
                        StepPercentageText = "0 %";
                        StatusMessage = description;
                    });
                }

                Progress<double> detailedProg = new(v =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        double pct = Math.Clamp(v * 100, 0, 100);
                        StepProgressValue = pct;
                        StepPercentageText = pct.ToString("F0", CultureInfo.CurrentCulture) + " %";
                    });
                });

                Progress<ConversionProgressReport> reportProg = new(rep =>
                {
                    if (rep.Step == ConversionStep.Completed)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            CurrentStep = TotalSteps;
                            CompletedSteps = TotalSteps;
                            TotalProgressValue = 100;
                            StepProgressValue = 100;
                            StepLabel = string.Format(CultureInfo.CurrentCulture, Strings.ProgressStepFormat, TotalSteps, TotalSteps);
                            StepPercentageText = "100 %";
                        });
                        return;
                    }

                    AdvanceStep(rep.Description);
                });

                Progress<string> prog = new(msg =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusMessage = msg;
                    });
                });

                if (exportMode == ExportAreaMode.TrackContour && selectedTrack != null)
                {
                    AdvanceStep(Strings.StepCroppingTrack);
                    string tempDir = Path.Combine(workspaceDir, $"kmz_crop_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempDir);
                    try
                    {
                        string croppedTif = Path.Combine(tempDir, "cropped_region.tif");
                        GpxImageTool gpxTool = new(new GeoImageMerger());

                        List<string> tiles = gpxTool.GetImagesIntersectingGpx(selectedTrack.FilePath, inputFiles).ToList();
                        if (tiles.Count == 0)
                        {
                            tiles = inputFiles;
                        }

                        Rect box = gpxTool.GetGpxBoundingBox(selectedTrack.FilePath, trackMargin);

                        gpxTool.CropImagesToRect(tiles, box, croppedTif, detailedProg);

                        ct.ThrowIfCancellationRequested();

                        GeoImageProcessor processor = new(
                            new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                            new GeoImageMerger(),
                            new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                            new MapReprojector(),
                            new TifToKmz.ImageTools.Exporters.JpegExporter(),
                            new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                        processor.ToKmz([croppedTif], fullOutputPath, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                    }
                    finally
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch
                        {
                            // Ignored
                        }
                    }
                }
                else if (exportMode == ExportAreaMode.SelectedTiles)
                {
                    ct.ThrowIfCancellationRequested();

                    string? projectionWkt = _mainVm.ProjectService.ActiveProject?.Layers.FirstOrDefault()?.ProjectionWkt
                        ?? _mainVm.ProjectService.Layers.FirstOrDefault()?.ProjectionWkt;

                    List<TifToKmz.Geometry.Rect> wgs84Tiles = selectedTilesSnapshot
                        .Select(extent => GeoExtentToWgs84Rect(extent, projectionWkt))
                        .ToList();

                    GeoImageProcessor processor = new(
                        new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                        new GeoImageMerger(),
                        new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                        new MapReprojector(),
                        new TifToKmz.ImageTools.Exporters.JpegExporter(),
                        new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                    processor.ToKmzFromSelectedTiles(inputFiles, fullOutputPath, wgs84Tiles, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                }
                else
                {
                    ct.ThrowIfCancellationRequested();

                    GeoImageProcessor processor = new(
                        new TifToKmz.ImageTools.Resizers.GeoImageResizer(),
                        new GeoImageMerger(),
                        new TifToKmz.ImageTools.Splitters.GeoImageSplitter(TifToKmz.ImageTools.GdalDrivers.GTiff),
                        new MapReprojector(),
                        new TifToKmz.ImageTools.Exporters.JpegExporter(),
                        new TifToKmz.ImageTools.Brighteners.GeoImageBrightener());

                    processor.ToKmz(inputFiles, fullOutputPath, options, EpsgCode.Wgs84, prog, detailedProg, reportProg, ct);
                }
            }, ct);

            CurrentStep = TotalSteps;
            CompletedSteps = TotalSteps;
            TotalProgressValue = 100;
            StepProgressValue = 100;
            StepLabel = string.Format(CultureInfo.CurrentCulture, Strings.ProgressStepFormat, TotalSteps, TotalSteps);
            HasStatusSuccess = true;
            HasStatusError = false;
            StatusMessage = Strings.KmzExportSuccessStatus;
        }
        catch (OperationCanceledException)
        {
            HasStatusError = false;
            HasStatusSuccess = false;
            StatusMessage = Strings.ExportCancelledStatus;
        }
        catch (Exception ex)
        {
            HasStatusError = true;
            HasStatusSuccess = false;
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Strings.KmzExportErrorPrefix, ex.Message);
        }
        finally
        {
            IsCancelling = false;
            IsExporting = false;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    private void LoadConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("OutputDirectory", out JsonElement elem))
                {
                    string? dir = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        _outputDirectory = Path.GetFullPath(dir);
                        return;
                    }
                }
            }
        }
        catch
        {
            // Ignore errors loading configuration
        }

        _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            _outputDirectory = AppContext.BaseDirectory;
        }
    }

    private void SaveConfig()
    {
        try
        {
            string configPath = GetConfigFilePath();
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var configObj = new
            {
                OutputDirectory = _outputDirectory
            };

            string json = JsonSerializer.Serialize(configObj, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch
        {
            // Ignore errors saving configuration
        }
    }

    private static string GetConfigFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "TifToKmzMapEditor", _configFileName);
    }

    private static TifToKmz.Geometry.Rect GeoExtentToWgs84Rect(GeoExtent extent, string? projectionWkt)
    {
        if (string.IsNullOrWhiteSpace(projectionWkt))
        {
            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(extent.MinX, extent.MaxY), new TifToKmz.Geometry.Point(extent.MaxX, extent.MinY));
        }

        try
        {
            using OSGeo.OSR.SpatialReference sourceSrs = new(projectionWkt);
            sourceSrs.SetAxisMappingStrategy(OSGeo.OSR.AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using OSGeo.OSR.SpatialReference wgs84 = new("");
            wgs84.ImportFromEPSG(4326);
            wgs84.SetAxisMappingStrategy(OSGeo.OSR.AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);

            using OSGeo.OSR.CoordinateTransformation transform = new(sourceSrs, wgs84);

            double[] p1 = [extent.MinX, extent.MaxY, 0];
            double[] p2 = [extent.MaxX, extent.MaxY, 0];
            double[] p3 = [extent.MaxX, extent.MinY, 0];
            double[] p4 = [extent.MinX, extent.MinY, 0];

            transform.TransformPoint(p1);
            transform.TransformPoint(p2);
            transform.TransformPoint(p3);
            transform.TransformPoint(p4);

            double minLon = Math.Min(Math.Min(p1[0], p2[0]), Math.Min(p3[0], p4[0]));
            double maxLon = Math.Max(Math.Max(p1[0], p2[0]), Math.Max(p3[0], p4[0]));
            double minLat = Math.Min(Math.Min(p1[1], p2[1]), Math.Min(p3[1], p4[1]));
            double maxLat = Math.Max(Math.Max(p1[1], p2[1]), Math.Max(p3[1], p4[1]));

            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(minLon, maxLat), new TifToKmz.Geometry.Point(maxLon, minLat));
        }
        catch
        {
            return new TifToKmz.Geometry.Rect(new TifToKmz.Geometry.Point(extent.MinX, extent.MaxY), new TifToKmz.Geometry.Point(extent.MaxX, extent.MinY));
        }
    }
}
