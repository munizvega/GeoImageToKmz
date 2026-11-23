namespace TifToKmz.ImageTools.Resizers;

public record ResizeOptions
{
    public string Format { get; init; } = "COG";
    public string[] CreationOptions { get; init; } = ["COMPRESS=DEFLATE", "PREDICTOR=2", "INTERLEAVE=PIXEL", "NUM_THREADS=ALL_CPUS"];
    public string ResamplingMethod { get; init; } = "lanczos";

    public string[] ToParameters(double? scaleFactor = null)
    {
        List<string> parameters = [];

        if (scaleFactor.HasValue)
        {
            string percentage = $"{scaleFactor.Value:P0}".Replace(" ", string.Empty, StringComparison.InvariantCulture);
            parameters.AddRange(["-outsize", percentage, percentage]);
            parameters.AddRange(["-r", ResamplingMethod]);
        }

        parameters.AddRange(["-of", Format]);

        foreach (string co in CreationOptions)
        {
            parameters.Add("-co");
            parameters.Add(co);
        }

        return parameters.ToArray();
    }
}
