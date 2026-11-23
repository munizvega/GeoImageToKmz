namespace TifToKmzLib.Geometry;

public struct Size : IEquatable<Size>
{
    public int Width { get; set; }
    public int Height { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is Size other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Width, Height);
    }

    public static bool operator ==(Size left, Size right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Size left, Size right)
    {
        return !(left == right);
    }

    public bool Equals(Size other)
    {
        return Width == other.Width && Height == other.Height;
    }
}
