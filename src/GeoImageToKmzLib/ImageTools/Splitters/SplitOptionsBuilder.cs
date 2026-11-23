namespace TifToKmz.ImageTools.Splitters;

public static class SplitOptionsBuilder
{
    public static SplitOptionsRowsBuilder WithCreationOptions(string[] creationOptions)
    {
        return new SplitOptionsRowsBuilder(creationOptions);
    }

    public class SplitOptionsRowsBuilder
    {
        private readonly string[] _creationOptions;

        public SplitOptionsRowsBuilder(string[] creationOptions)
        {
            _creationOptions = creationOptions;
        }

        public SplitOptionsColumnsBuilder WithNumberOfRows(int numberOfRows)
        {
            return new SplitOptionsColumnsBuilder(_creationOptions, x => x / (double)numberOfRows);
        }

        public SplitOptionsColumnsBuilder WithTileHeight(int tileHeight)
        {
            return new SplitOptionsColumnsBuilder(_creationOptions, _ => tileHeight);
        }
    }

    public class SplitOptionsColumnsBuilder
    {
        private readonly string[] _creationOptions;
        private readonly Func<int, double> _tileHeightCalculator;

        public SplitOptionsColumnsBuilder(string[] creationOptions, Func<int, double> tileHeightCalculator)
        {
            _creationOptions = creationOptions;
            _tileHeightCalculator = tileHeightCalculator;
        }

        public SplitOptions WithNumberOfColumns(int numberOfColumns)
        {
            return new SplitOptions(x => x / (double)numberOfColumns, _tileHeightCalculator)
            {
                CreationOptions = _creationOptions
            };
        }

        public SplitOptions WithColumnWidth(int tileWidth)
        {
            return new SplitOptions(_ => tileWidth, _tileHeightCalculator)
            {
                CreationOptions = _creationOptions
            };
        }
    }
}
