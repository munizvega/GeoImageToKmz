namespace TifToKmz.Geometry;

public class Rect
{
    public Rect(Point topLeft, Point bottomRight)
    {
        TopLeft = topLeft;
        BottomRight = bottomRight;
    }

    public Point TopLeft { get; set; }

    public Point BottomRight { get; set; }
}
