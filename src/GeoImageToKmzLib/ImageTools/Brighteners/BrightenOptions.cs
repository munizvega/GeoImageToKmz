namespace TifToKmz.ImageTools.Brighteners;

public record BrightenOptions
{
    private readonly double _gamma = 1.6;
    private readonly double _brightness = 8;
    private readonly double _contrast = 1.0;

    /// <summary>Disables the step so the tiles are used exactly as the splitter produced them.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gamma correction. Values above one lighten the midtones, below one darken them.</summary>
    public double Gamma
    {
        get => _gamma;
        init
        {
            if (value is <= 0 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Gamma must be between 0 and 10.");
            }
            _gamma = value;
        }
    }

    /// <summary>Percentage of the data range used to lift the black point, flattening the darkest areas.</summary>
    public double Brightness
    {
        get => _brightness;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Brightness must be between 0 and 100.");
            }
            _brightness = value;
        }
    }

    /// <summary>Contrast factor. Values above one narrow the source range, increasing contrast.</summary>
    public double Contrast
    {
        get => _contrast;
        init
        {
            if (value is < 0.1 or > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Contrast must be between 0.1 and 10.");
            }
            _contrast = value;
        }
    }

    public string Format { get; init; } = "GTiff";

    public string[] CreationOptions { get; init; } = ["TILED=YES", "COMPRESS=DEFLATE", "PREDICTOR=2", "NUM_THREADS=ALL_CPUS"];

    /// <summary>True when the configured values would leave the pixels untouched.</summary>
    public bool IsNeutral => !Enabled || (Math.Abs(Gamma - 1) < 1e-6 && Brightness < 1e-6 && Math.Abs(Contrast - 1) < 1e-6);

    /// <summary>
    /// Builds the gdal_translate parameters that lighten <paramref name="bands"/>. Each band is
    /// scaled inside its own data range, so the output keeps the source data type, and bands that
    /// are not listed (alpha, masks) are copied verbatim.
    /// </summary>
    public string[] ToParameters(IEnumerable<(int Index, double Minimum, double Maximum)> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);
        List<string> parameters = [];

        foreach ((int index, double minimum, double maximum) in bands)
        {
            double span = maximum - minimum;
            if (span <= 0)
            {
                continue;
            }

            double middle = minimum + span / 2;
            double half = span / 2 / Contrast;
            double sourceMinimum = Math.Max(minimum, middle - half);
            double sourceMaximum = Math.Min(maximum, middle + half);
            if (sourceMaximum - sourceMinimum <= 0)
            {
                continue;
            }

            double destinationMinimum = minimum + span * Brightness / 100;
            parameters.AddRange([
                $"-scale_{index}", Text(sourceMinimum), Text(sourceMaximum), Text(destinationMinimum), Text(maximum),
                $"-exponent_{index}", Text(1 / Gamma)
            ]);
        }

        if (parameters.Count == 0)
        {
            return [];
        }

        parameters.AddRange(["-of", Format]);

        foreach (string co in CreationOptions)
        {
            parameters.Add("-co");
            parameters.Add(co);
        }

        return parameters.ToArray();

        static string Text(double value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }
}
