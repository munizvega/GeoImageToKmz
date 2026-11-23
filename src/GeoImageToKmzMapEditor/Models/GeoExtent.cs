namespace TifToKmzMapEditor.Models;

public readonly record struct GeoExtent(double MinX, double MinY, double MaxX, double MaxY)
{
    public static GeoExtent Empty => new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

    public double Width => Math.Max(0, MaxX - MinX);
    public double Height => Math.Max(0, MaxY - MinY);
    public double CenterX => MinX + (Width / 2.0);
    public double CenterY => MinY + (Height / 2.0);
    public bool IsEmpty => MinX > MaxX || MinY > MaxY || double.IsInfinity(MinX) || double.IsInfinity(MinY);

    public bool Intersects(GeoExtent other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return false;
        }

        return MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
    }

    public GeoExtent Intersection(GeoExtent other)
    {
        if (!Intersects(other))
        {
            return Empty;
        }

        return new GeoExtent(
            Math.Max(MinX, other.MinX),
            Math.Max(MinY, other.MinY),
            Math.Min(MaxX, other.MaxX),
            Math.Min(MaxY, other.MaxY));
    }

    public GeoExtent Union(GeoExtent other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new GeoExtent(
            Math.Min(MinX, other.MinX),
            Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX),
            Math.Max(MaxY, other.MaxY));
    }

    public GeoExtent Inflate(double factor)
    {
        if (IsEmpty)
        {
            return this;
        }

        double dx = (Width * (factor - 1.0)) / 2.0;
        double dy = (Height * (factor - 1.0)) / 2.0;
        return new GeoExtent(MinX - dx, MinY - dy, MaxX + dx, MaxY + dy);
    }

    public bool Contains(double x, double y)
    {
        if (IsEmpty)
        {
            return false;
        }

        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    public static GeoExtent FitToAspectRatio(GeoExtent extent, double screenWidth, double screenHeight)
    {
        if (extent.IsEmpty || screenWidth <= 0 || screenHeight <= 0 || extent.Height <= 0)
        {
            return extent;
        }

        double screenAspect = screenWidth / screenHeight;
        double extentAspect = extent.Width / extent.Height;

        if (screenAspect > extentAspect)
        {
            // Screen is wider than extent -> expand width centered
            double newWidth = extent.Height * screenAspect;
            double dx = (newWidth - extent.Width) / 2.0;
            return new GeoExtent(extent.MinX - dx, extent.MinY, extent.MaxX + dx, extent.MaxY);
        }
        else
        {
            // Screen is taller than extent -> expand height centered
            double newHeight = extent.Width / screenAspect;
            double dy = (newHeight - extent.Height) / 2.0;
            return new GeoExtent(extent.MinX, extent.MinY - dy, extent.MaxX, extent.MaxY + dy);
        }
    }
}
