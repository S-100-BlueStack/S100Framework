namespace S100Framework.REST.Models;

public sealed record FeatureQuery
{
    public string Where { get; init; } = "1=1";

    public IReadOnlyList<string>? OutFields { get; init; }

    public bool ReturnGeometry { get; init; } = true;

    public int? PageSize { get; init; }

    public string? OrderBy { get; init; }

    public int? Limit { get; init; }

    public int? OutSrid { get; init; }

    public FeatureSpatialFilter? SpatialFilter { get; init; }
}