using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TifToKmzMapEditor.Models;
using TifToKmzMapEditor.Resources;
using TifToKmzMapEditor.Services;
using TifToKmzMapEditor.ViewModels;

namespace TifToKmzMapEditor.Views;

public sealed class MapViewportControl : Control
{
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<MapViewportControl, MainViewModel?>(nameof(ViewModel));

    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private WriteableBitmap? _renderedBitmap;
    private GeoExtent _renderedExtent = GeoExtent.Empty;
    private CancellationTokenSource? _renderCts;

    // Interaction state
    private bool _isPanning;
    private Point _panStartPoint;
    private GeoExtent _panStartExtent;

    private bool _isSelectingCrop;
    private Point _cropStartScreen;
    private Point _cropCurrentScreen;

    private bool _isMeasuring;
    private Point _measureStartScreen;
    private Point _measureCurrentScreen;

    private const double _screenTileSize = 180.0;

    // Brushes and pens
    private static readonly IBrush _backgroundBrush = new SolidColorBrush(Color.FromRgb(244, 246, 249));
    private static readonly IBrush _gridBrush = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0));
    private static readonly IPen _gridPen = new Pen(_gridBrush, 1);
    private static readonly IBrush _cropFillBrush = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
    private static readonly IPen _cropBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 120, 215)), 2, DashStyle.Dash);
    private static readonly IPen _measurePen = new Pen(new SolidColorBrush(Color.FromRgb(210, 80, 0)), 2.5);
    private static readonly IBrush _measureNodeBrush = new SolidColorBrush(Color.FromRgb(210, 80, 0));
    private static readonly IBrush _overlayTextBg = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
    private static readonly IBrush _overlayTextFg = new SolidColorBrush(Color.FromRgb(30, 30, 35));
    private static readonly IBrush _tileGridBrush = new SolidColorBrush(Color.FromArgb(160, 255, 128, 171));
    private static readonly IPen _tileGridPen = new Pen(_tileGridBrush, 1.2);
    private static readonly IBrush _selectedTileFill = new SolidColorBrush(Color.FromArgb(60, 255, 182, 193));
    private static readonly IPen _selectedTileBorder = new Pen(new SolidColorBrush(Color.FromRgb(255, 105, 180)), 2.0);
    private static readonly IBrush _selectedTileBadgeBg = new SolidColorBrush(Color.FromArgb(210, 255, 105, 180));

    public MapViewportControl()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ViewModel is { } vm)
        {
            vm.Viewport.ViewportExtentChanged -= OnViewportExtentChanged;
            vm.Viewport.ViewportExtentChanged += OnViewportExtentChanged;
            vm.ProjectService.LayersChanged -= OnLayersChanged;
            vm.ProjectService.LayersChanged += OnLayersChanged;
            vm.ProjectService.ProjectsChanged -= OnLayersChanged;
            vm.ProjectService.ProjectsChanged += OnLayersChanged;
        }
        RequestRender();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (ViewModel is { } vm)
        {
            vm.Viewport.ViewportExtentChanged -= OnViewportExtentChanged;
            vm.ProjectService.LayersChanged -= OnLayersChanged;
            vm.ProjectService.ProjectsChanged -= OnLayersChanged;
        }

        try
        {
            _renderCts?.Cancel();
            _renderCts?.Dispose();
            _renderCts = null;
        }
        catch
        {
            // Ignore CTS disposal exceptions
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        RequestRender();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is MainViewModel oldVm)
            {
                oldVm.Viewport.ViewportExtentChanged -= OnViewportExtentChanged;
                oldVm.ProjectService.LayersChanged -= OnLayersChanged;
                oldVm.ProjectService.ProjectsChanged -= OnLayersChanged;
            }

            if (change.NewValue is MainViewModel newVm)
            {
                newVm.Viewport.ViewportExtentChanged += OnViewportExtentChanged;
                newVm.ProjectService.LayersChanged += OnLayersChanged;
                newVm.ProjectService.ProjectsChanged += OnLayersChanged;
                RequestRender();
            }
        }
        else if (change.Property == BoundsProperty)
        {
            RequestRender();
        }
    }

    private void OnViewportExtentChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RequestRender();
            InvalidateVisual();
        });
    }

    private void OnLayersChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RequestRender();
            InvalidateVisual();
        });
    }

    public void RequestRender()
    {
        if (ViewModel == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        GeoExtent extent = ViewModel.Viewport.CurrentExtent;
        if (extent.IsEmpty)
        {
            return;
        }

        extent = GeoExtent.FitToAspectRatio(extent, Bounds.Width, Bounds.Height);
        if (extent != ViewModel.Viewport.CurrentExtent)
        {
            ViewModel.Viewport.CurrentExtent = extent;
        }

        var oldCts = _renderCts;
        _renderCts = new CancellationTokenSource();
        CancellationToken token = _renderCts.Token;

        try
        {
            oldCts?.Cancel();
            oldCts?.Dispose();
        }
        catch
        {
            // Ignore cancellation on previous CTS
        }

        int w = (int)Math.Ceiling(Bounds.Width);
        int h = (int)Math.Ceiling(Bounds.Height);

        if (w > 0 && h > 0)
        {
            ViewModel.Viewport.PixelWidth = Bounds.Width;
            ViewModel.Viewport.PixelHeight = Bounds.Height;
        }

        ViewModel.Viewport.RenderStatusText = Strings.StatusRendering;

        var layers = ViewModel.GetVisibleLayers().ToList();
        var rasterRenderer = ViewModel.RasterRenderer;

        if (layers.Count > 0)
        {
            ViewModel.Viewport.SetLoading(true, Strings.Loading, 0, isIndeterminate: true);
        }
        else
        {
            ViewModel.Viewport.SetLoading(false);
        }

        Task.Run(async () =>
        {
            try
            {
                var result = await rasterRenderer.RenderViewportAsync(layers, extent, w, h, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (result != null)
                    {
                        WriteableBitmap bitmap = new(
                            new PixelSize(result.Width, result.Height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Premul);

                        using (ILockedFramebuffer fb = bitmap.Lock())
                        {
                            Marshal.Copy(result.BgraBuffer, 0, fb.Address, result.BgraBuffer.Length);
                        }

                        _renderedBitmap = bitmap;
                        _renderedExtent = extent;
                    }
                    else if (layers.Count == 0)
                    {
                        _renderedBitmap = null;
                        _renderedExtent = GeoExtent.Empty;
                    }

                    if (ViewModel != null)
                    {
                        ViewModel.Viewport.SetLoading(false);
                        ViewModel.Viewport.RenderStatusText = Strings.StatusReady;
                        ViewModel.Viewport.MemoryStatusText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.StatusBuffer, (w * h * 4 / (1024.0 * 1024.0)));
                    }
                    InvalidateVisual();
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when panning/zooming fast
            }
        }, token);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect localBounds = new(0, 0, Bounds.Width, Bounds.Height);
        context.DrawRectangle(_backgroundBrush, null, localBounds);

        // Draw subtle background grid
        DrawBackgroundGrid(context, localBounds);

        if (ViewModel == null)
        {
            return;
        }

        // Draw raster bitmap
        if (_renderedBitmap != null && !_renderedExtent.IsEmpty)
        {
            GeoExtent current = ViewModel.Viewport.CurrentExtent;
            if (!current.IsEmpty)
            {
                // Calculate where the previously rendered extent sits relative to current viewport
                double x1 = ((_renderedExtent.MinX - current.MinX) / current.Width) * localBounds.Width;
                double x2 = ((_renderedExtent.MaxX - current.MinX) / current.Width) * localBounds.Width;
                double y1 = ((current.MaxY - _renderedExtent.MaxY) / current.Height) * localBounds.Height;
                double y2 = ((current.MaxY - _renderedExtent.MinY) / current.Height) * localBounds.Height;

                Rect dstRect = new(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
                context.DrawImage(_renderedBitmap, dstRect);
            }
        }

        // If no layers and no tracks loaded, display guide message
        if (ViewModel.TotalLayersCount == 0 && ViewModel.TotalTracksCount == 0)
        {
            DrawEmptyStateMessage(context, localBounds);
        }

        // Draw Visible GPX Tracks on top of map
        DrawTracks(context, localBounds);

        // Draw Selected Tiles Grid & Selected Tiles
        DrawSelectedTilesGrid(context, localBounds);

        // Draw Crop Selection Overlay
        DrawCropOverlay(context, localBounds);

        // Draw Measure Overlay
        DrawMeasureOverlay(context);

        // Draw Map Scale & Reticle
        DrawScaleAndReticle(context, localBounds);
    }

    private void DrawTracks(DrawingContext context, Rect bounds)
    {
        if (ViewModel == null || ViewModel.Viewport.CurrentExtent.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        GeoExtent current = ViewModel.Viewport.CurrentExtent;
        IReadOnlyList<MapTrack> visibleTracks = ViewModel.GetVisibleTracks();
        if (visibleTracks.Count == 0)
        {
            return;
        }

        foreach (MapTrack track in visibleTracks)
        {
            if (track.ProjectedPoints.Count < 2)
            {
                continue;
            }

            if (!track.Extent.IsEmpty && !track.Extent.Intersects(current))
            {
                continue;
            }

            StreamGeometry geometry = new();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                bool first = true;
                foreach ((double px, double py) in track.ProjectedPoints)
                {
                    double sx = ((px - current.MinX) / current.Width) * bounds.Width;
                    double sy = ((current.MaxY - py) / current.Height) * bounds.Height;
                    Point pt = new(sx, sy);

                    if (first)
                    {
                        ctx.BeginFigure(pt, false);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(pt);
                    }
                }
                ctx.EndFigure(false);
            }

            // Draw dark casing / halo for high contrast against any satellite/topo raster
            IPen casingPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 20, 25)), 5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, casingPen, geometry);

            // Draw colored track line
            IPen trackPen = new Pen(new SolidColorBrush(track.TrackColor), 3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, trackPen, geometry);

            // Draw start waypoint (green) and end waypoint (red)
            (double startX, double startY) = track.ProjectedPoints[0];
            (double endX, double endY) = track.ProjectedPoints[^1];

            double startSx = ((startX - current.MinX) / current.Width) * bounds.Width;
            double startSy = ((current.MaxY - startY) / current.Height) * bounds.Height;
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(46, 204, 113)), new Pen(Brushes.White, 1.5), new Point(startSx, startSy), 5, 5);

            double endSx = ((endX - current.MinX) / current.Width) * bounds.Width;
            double endSy = ((current.MaxY - endY) / current.Height) * bounds.Height;
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(231, 76, 60)), new Pen(Brushes.White, 1.5), new Point(endSx, endSy), 5, 5);
        }
    }

    private void DrawSelectedTilesGrid(DrawingContext context, Rect bounds)
    {
        if (ViewModel == null || !ViewModel.Export.IsSelectedTilesMode || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        GeoExtent current = ViewModel.Viewport.CurrentExtent;
        if (current.IsEmpty || current.Width <= 0 || current.Height <= 0)
        {
            return;
        }

        double geoTileWidth;
        double geoTileHeight;

        if (ViewModel.Export.LockTileSize && ViewModel.Export.SelectedTiles.Count > 0)
        {
            GeoExtent firstTile = ViewModel.Export.SelectedTiles[0];
            geoTileWidth = firstTile.Width;
            geoTileHeight = firstTile.Height;
        }
        else
        {
            geoTileWidth = (_screenTileSize / bounds.Width) * current.Width;
            geoTileHeight = (_screenTileSize / bounds.Height) * current.Height;
        }

        if (geoTileWidth <= 0 || geoTileHeight <= 0)
        {
            return;
        }

        double screenTileW = (geoTileWidth / current.Width) * bounds.Width;
        double screenTileH = (geoTileHeight / current.Height) * bounds.Height;

        // 1. Draw World-anchored Grid (moves with the map)
        if (screenTileW >= 4 && screenTileH >= 4)
        {
            long startCol = (long)Math.Floor(current.MinX / geoTileWidth);
            long endCol = (long)Math.Ceiling(current.MaxX / geoTileWidth);
            long startRow = (long)Math.Floor(current.MinY / geoTileHeight);
            long endRow = (long)Math.Ceiling(current.MaxY / geoTileHeight);

            if ((endCol - startCol) <= 500 && (endRow - startRow) <= 500)
            {
                for (long c = startCol; c <= endCol; c++)
                {
                    double wx = c * geoTileWidth;
                    double sx = ((wx - current.MinX) / current.Width) * bounds.Width;
                    context.DrawLine(_tileGridPen, new Point(sx, 0), new Point(sx, bounds.Height));
                }
                for (long r = startRow; r <= endRow; r++)
                {
                    double wy = r * geoTileHeight;
                    double sy = ((current.MaxY - wy) / current.Height) * bounds.Height;
                    context.DrawLine(_tileGridPen, new Point(0, sy), new Point(bounds.Width, sy));
                }
            }
        }

        // 2. Draw Selected Tiles (in world coordinates, preserving geography across pan & zoom)
        int index = 1;
        foreach (GeoExtent tileExtent in ViewModel.Export.SelectedTiles)
        {
            if (tileExtent.IsEmpty || !tileExtent.Intersects(current))
            {
                index++;
                continue;
            }

            double sx1 = ((tileExtent.MinX - current.MinX) / current.Width) * bounds.Width;
            double sx2 = ((tileExtent.MaxX - current.MinX) / current.Width) * bounds.Width;
            double sy1 = ((current.MaxY - tileExtent.MaxY) / current.Height) * bounds.Height;
            double sy2 = ((current.MaxY - tileExtent.MinY) / current.Height) * bounds.Height;

            Rect tileRect = new(Math.Min(sx1, sx2), Math.Min(sy1, sy2), Math.Abs(sx2 - sx1), Math.Abs(sy2 - sy1));
            context.DrawRectangle(_selectedTileFill, _selectedTileBorder, tileRect);

            // Draw tile index badge in top-left corner
            string badgeText = $"#{index}";
            FormattedText ft = new(
                badgeText,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                12,
                Brushes.White);

            Rect badgeRect = new(tileRect.X + 4, tileRect.Y + 4, ft.Width + 8, ft.Height + 4);
            context.DrawRectangle(_selectedTileBadgeBg, null, badgeRect);
            context.DrawText(ft, new Point(badgeRect.X + 4, badgeRect.Y + 2));

            index++;
        }
    }

    private static void DrawBackgroundGrid(DrawingContext context, Rect bounds)
    {
        double step = 40;
        for (double x = 0; x < bounds.Width; x += step)
        {
            context.DrawLine(_gridPen, new Point(x, 0), new Point(x, bounds.Height));
        }
        for (double y = 0; y < bounds.Height; y += step)
        {
            context.DrawLine(_gridPen, new Point(0, y), new Point(bounds.Width, y));
        }
    }

    private static void DrawEmptyStateMessage(DrawingContext context, Rect bounds)
    {
        FormattedText title = new(
            Strings.NoMapsLoadedTitle,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
            18,
            new SolidColorBrush(Color.FromArgb(200, 30, 30, 35)));

        FormattedText sub = new(
            Strings.NoMapsLoadedDescription,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal),
            13,
            new SolidColorBrush(Color.FromArgb(140, 40, 40, 50)));

        Point p1 = new(Math.Max(10, (bounds.Width - title.Width) / 2), (bounds.Height / 2) - 30);
        Point p2 = new(Math.Max(10, (bounds.Width - sub.Width) / 2), (bounds.Height / 2) + 5);

        context.DrawText(title, p1);
        context.DrawText(sub, p2);
    }

    private void DrawCropOverlay(DrawingContext context, Rect bounds)
    {
        if (ViewModel == null)
        {
            return;
        }

        GeoExtent crop = ViewModel.Viewport.SelectedCropExtent;
        GeoExtent current = ViewModel.Viewport.CurrentExtent;

        if (_isSelectingCrop)
        {
            double minX = Math.Min(_cropStartScreen.X, _cropCurrentScreen.X);
            double minY = Math.Min(_cropStartScreen.Y, _cropCurrentScreen.Y);
            double w = Math.Abs(_cropCurrentScreen.X - _cropStartScreen.X);
            double h = Math.Abs(_cropCurrentScreen.Y - _cropStartScreen.Y);

            Rect rect = new(minX, minY, w, h);
            context.DrawRectangle(_cropFillBrush, _cropBorderPen, rect);
        }
        else if (!crop.IsEmpty && !current.IsEmpty)
        {
            double x1 = ((crop.MinX - current.MinX) / current.Width) * bounds.Width;
            double x2 = ((crop.MaxX - current.MinX) / current.Width) * bounds.Width;
            double y1 = ((current.MaxY - crop.MaxY) / current.Height) * bounds.Height;
            double y2 = ((current.MaxY - crop.MinY) / current.Height) * bounds.Height;

            Rect rect = new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            context.DrawRectangle(_cropFillBrush, _cropBorderPen, rect);

            // Draw label
            string label = string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.CropOverlayLabel, crop.Width, crop.Height);
            FormattedText ft = new(
                label,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                11,
                _overlayTextFg);

            Rect labelBg = new(rect.X + 6, rect.Y + 6, ft.Width + 10, ft.Height + 6);
            context.DrawRectangle(_overlayTextBg, null, labelBg);
            context.DrawText(ft, new Point(rect.X + 11, rect.Y + 9));
        }
    }

    private void DrawMeasureOverlay(DrawingContext context)
    {
        if (!_isMeasuring)
        {
            return;
        }

        context.DrawLine(_measurePen, _measureStartScreen, _measureCurrentScreen);
        context.DrawEllipse(_measureNodeBrush, null, _measureStartScreen, 5, 5);
        context.DrawEllipse(_measureNodeBrush, null, _measureCurrentScreen, 5, 5);

        if (ViewModel != null && !string.IsNullOrWhiteSpace(ViewModel.Viewport.MeasureDistanceText))
        {
            FormattedText ft = new(
                ViewModel.Viewport.MeasureDistanceText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                12,
                _overlayTextFg);

            Point mid = new((_measureStartScreen.X + _measureCurrentScreen.X) / 2, ((_measureStartScreen.Y + _measureCurrentScreen.Y) / 2) - 15);
            Rect labelBg = new(mid.X - 5, mid.Y - 3, ft.Width + 10, ft.Height + 6);
            context.DrawRectangle(_overlayTextBg, null, labelBg);
            context.DrawText(ft, mid);
        }
    }

    private void DrawScaleAndReticle(DrawingContext context, Rect bounds)
    {
        if (ViewModel == null || ViewModel.Viewport.CurrentExtent.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        GeoExtent ext = ViewModel.Viewport.CurrentExtent;
        double metersPerPixel = ext.Width / bounds.Width;

        // Choose scale bar target pixel width (around 100px)
        double targetMeters = metersPerPixel * 100.0;
        double roundedMeters = RoundToNiceNumber(targetMeters);
        double barPixelWidth = roundedMeters / metersPerPixel;

        if (barPixelWidth > 10 && barPixelWidth < bounds.Width)
        {
            Point barStart = new(20, bounds.Height - 30);
            Point barEnd = new(20 + barPixelWidth, bounds.Height - 30);

            IPen barPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 45)), 3);
            context.DrawLine(barPen, barStart, barEnd);
            context.DrawLine(barPen, new Point(barStart.X, barStart.Y - 5), new Point(barStart.X, barStart.Y + 5));
            context.DrawLine(barPen, new Point(barEnd.X, barEnd.Y - 5), new Point(barEnd.X, barEnd.Y + 5));

            string text;
            if (roundedMeters >= 1000)
            {
                text = $"{roundedMeters / 1000.0:0.##} km";
            }
            else if (roundedMeters >= 1)
            {
                text = $"{roundedMeters:0.##} m";
            }
            else
            {
                text = $"{roundedMeters * 100.0:0.##} cm";
            }

            FormattedText ft = new(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                11,
                _overlayTextFg);

            context.DrawRectangle(_overlayTextBg, null, new Rect(barStart.X + 2, barStart.Y - 22, ft.Width + 8, ft.Height + 4));
            context.DrawText(ft, new Point(barStart.X + 6, barStart.Y - 20));
        }
    }

    private static double RoundToNiceNumber(double val)
    {
        if (val <= 0 || double.IsNaN(val) || double.IsInfinity(val))
        {
            return 1;
        }

        double exp = Math.Floor(Math.Log10(val));
        double frac = val / Math.Pow(10, exp);
        double niceFrac = frac switch
        {
            < 1.5 => 1,
            < 3.5 => 2,
            < 7.5 => 5,
            _ => 10
        };

        return niceFrac * Math.Pow(10, exp);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (ViewModel == null)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);
        Point pos = point.Position;

        if (ViewModel.Viewport.IsCroppingMode && point.Properties.IsLeftButtonPressed)
        {
            _isSelectingCrop = true;
            _cropStartScreen = pos;
            _cropCurrentScreen = pos;
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
        else if (ViewModel.Viewport.IsMeasuringMode && point.Properties.IsLeftButtonPressed)
        {
            _isMeasuring = true;
            _measureStartScreen = pos;
            _measureCurrentScreen = pos;
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
        else if (point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = pos;
            _panStartExtent = ViewModel.Viewport.CurrentExtent;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (ViewModel == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        Point pos = e.GetPosition(this);
        GeoExtent current = ViewModel.Viewport.CurrentExtent;

        if (!current.IsEmpty)
        {
            double gx = current.MinX + ((pos.X / Bounds.Width) * current.Width);
            double gy = current.MaxY - ((pos.Y / Bounds.Height) * current.Height);
            ViewModel.Viewport.CursorGeoX = gx;
            ViewModel.Viewport.CursorGeoY = gy;
        }

        if (_isPanning && !_panStartExtent.IsEmpty)
        {
            double dxPixels = _panStartPoint.X - pos.X;
            double dyPixels = pos.Y - _panStartPoint.Y;

            double dxGeo = (dxPixels / Bounds.Width) * _panStartExtent.Width;
            double dyGeo = (dyPixels / Bounds.Height) * _panStartExtent.Height;

            ViewModel.Viewport.CurrentExtent = new GeoExtent(
                _panStartExtent.MinX + dxGeo,
                _panStartExtent.MinY + dyGeo,
                _panStartExtent.MaxX + dxGeo,
                _panStartExtent.MaxY + dyGeo);

            InvalidateVisual();
        }
        else if (_isSelectingCrop)
        {
            _cropCurrentScreen = pos;
            InvalidateVisual();
        }
        else if (_isMeasuring && !current.IsEmpty)
        {
            _measureCurrentScreen = pos;

            double g1X = current.MinX + ((_measureStartScreen.X / Bounds.Width) * current.Width);
            double g1Y = current.MaxY - ((_measureStartScreen.Y / Bounds.Height) * current.Height);
            double g2X = current.MinX + ((_measureCurrentScreen.X / Bounds.Width) * current.Width);
            double g2Y = current.MaxY - ((_measureCurrentScreen.Y / Bounds.Height) * current.Height);

            double dist = Math.Sqrt(Math.Pow(g2X - g1X, 2) + Math.Pow(g2Y - g1Y, 2));
            ViewModel.Viewport.MeasureDistanceText = dist >= 1000 ? $"Distancia: {dist / 1000:F2} km" : $"Distancia: {dist:F1} m";
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (ViewModel == null)
        {
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(null);

            Point pos = e.GetPosition(this);
            double distMoved = Math.Sqrt(Math.Pow(pos.X - _panStartPoint.X, 2) + Math.Pow(pos.Y - _panStartPoint.Y, 2));

            // If click without significant drag and in SelectedTilesMode -> Toggle Tile Selection
            if (distMoved < 6 && ViewModel.Export.IsSelectedTilesMode)
            {
                GeoExtent current = ViewModel.Viewport.CurrentExtent;
                if (!current.IsEmpty && Bounds.Width > 0 && Bounds.Height > 0)
                {
                    double clickGeoX = current.MinX + ((pos.X / Bounds.Width) * current.Width);
                    double clickGeoY = current.MaxY - ((pos.Y / Bounds.Height) * current.Height);

                    // Check if clicked inside an already selected tile
                    GeoExtent? existing = null;
                    foreach (GeoExtent tile in ViewModel.Export.SelectedTiles)
                    {
                        if (tile.Contains(clickGeoX, clickGeoY))
                        {
                            existing = tile;
                            break;
                        }
                    }

                    if (existing.HasValue)
                    {
                        ViewModel.Export.SelectedTiles.Remove(existing.Value);
                    }
                    else if (ViewModel.Export.SelectedTiles.Count < ViewModel.Export.MaxTiles)
                    {
                        double geoTileWidth;
                        double geoTileHeight;

                        if (ViewModel.Export.LockTileSize && ViewModel.Export.SelectedTiles.Count > 0)
                        {
                            GeoExtent firstTile = ViewModel.Export.SelectedTiles[0];
                            geoTileWidth = firstTile.Width;
                            geoTileHeight = firstTile.Height;
                        }
                        else
                        {
                            geoTileWidth = (_screenTileSize / Bounds.Width) * current.Width;
                            geoTileHeight = (_screenTileSize / Bounds.Height) * current.Height;
                        }

                        if (geoTileWidth > 0 && geoTileHeight > 0)
                        {
                            long col = (long)Math.Floor(clickGeoX / geoTileWidth);
                            long row = (long)Math.Floor(clickGeoY / geoTileHeight);

                            double gx1 = col * geoTileWidth;
                            double gx2 = (col + 1) * geoTileWidth;
                            double gy1 = row * geoTileHeight;
                            double gy2 = (row + 1) * geoTileHeight;

                            GeoExtent newTileExtent = new(gx1, gy1, gx2, gy2);
                            ViewModel.Export.SelectedTiles.Add(newTileExtent);
                        }
                    }

                    InvalidateVisual();
                }
            }

            RequestRender();
        }
        else if (_isSelectingCrop)
        {
            _isSelectingCrop = false;
            e.Pointer.Capture(null);

            GeoExtent current = ViewModel.Viewport.CurrentExtent;
            if (!current.IsEmpty)
            {
                double minScreenX = Math.Min(_cropStartScreen.X, _cropCurrentScreen.X);
                double maxScreenX = Math.Max(_cropStartScreen.X, _cropCurrentScreen.X);
                double minScreenY = Math.Min(_cropStartScreen.Y, _cropCurrentScreen.Y);
                double maxScreenY = Math.Max(_cropStartScreen.Y, _cropCurrentScreen.Y);

                double gMinX = current.MinX + ((minScreenX / Bounds.Width) * current.Width);
                double gMaxX = current.MinX + ((maxScreenX / Bounds.Width) * current.Width);
                double gMaxY = current.MaxY - ((minScreenY / Bounds.Height) * current.Height);
                double gMinY = current.MaxY - ((maxScreenY / Bounds.Height) * current.Height);

                ViewModel.Viewport.SelectedCropExtent = new GeoExtent(gMinX, gMinY, gMaxX, gMaxY);
            }

            InvalidateVisual();
        }
        else if (_isMeasuring)
        {
            _isMeasuring = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (ViewModel == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        GeoExtent current = ViewModel.Viewport.CurrentExtent;
        if (current.IsEmpty)
        {
            return;
        }

        if (Math.Abs(e.Delta.Y) < 0.001)
        {
            return;
        }

        Point pos = e.GetPosition(this);
        double anchorGeoX = current.MinX + ((pos.X / Bounds.Width) * current.Width);
        double anchorGeoY = current.MaxY - ((pos.Y / Bounds.Height) * current.Height);

        double factor = e.Delta.Y > 0 ? 1.25 : 0.8;
        ViewModel.Viewport.ZoomAt(anchorGeoX, anchorGeoY, factor);
        RequestRender();
    }
}
