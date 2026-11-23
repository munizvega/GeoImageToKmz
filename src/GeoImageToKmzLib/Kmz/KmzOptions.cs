namespace TifToKmz.Kmz;

public record KmzOptions
{
    private readonly int _tileSize = 1024;
    private readonly int _maxNumberOfTiles = 100;
    private readonly double _resizeFactor = 1.0;
    private readonly int _jpegQuality = 100;

    public KmzOptions() { }

    public KmzOptions(string workspacePath)
    {
        WorkspacePath = workspacePath;
    }

    public int TileSize
    {
        get => _tileSize;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _tileSize = value;
        }
    }

    public int MaxNumberOfTiles
    {
        get => _maxNumberOfTiles;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxNumberOfTiles = value;
        }
    }

    public double ResizeFactor
    {
        get => _resizeFactor;
        init
        {
            if (value is <= 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _resizeFactor = value;
        }
    }

    public string WorkspacePath { get; init; } = string.Empty;

    public int JpegQuality
    {
        get => _jpegQuality;
        init
        {
            if (value is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Quality must be between 1 and 100.");
            }
            _jpegQuality = value;
        }
    }
}
