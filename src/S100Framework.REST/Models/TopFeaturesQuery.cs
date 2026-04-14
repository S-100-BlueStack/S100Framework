namespace S100Framework.REST.Models;

public sealed record TopFeaturesQuery
{
    public string Where { get; init; } = "1=1";

    public IReadOnlyList<long>? ObjectIds { get; init; }

    public IReadOnlyList<string>? OutFields { get; init; }

    public bool ReturnGeometry { get; init; } = true;

    public bool? ReturnZ { get; init; }

    public bool? ReturnM { get; init; }

    public int? OutSrid { get; init; }

    public int? GeometryPrecision { get; init; }

    public double? MaxAllowableOffset { get; init; }

    public FeatureSpatialFilter? SpatialFilter { get; init; }

    public TopFeaturesFilter? TopFilter { get; init; }

    public void Validate() {
        TopFilter?.Validate();

        if (TopFilter is null) {
            throw new InvalidOperationException("TopFilter must be provided.");
        }

        if (ObjectIds is { Count: 0 }) {
            throw new InvalidOperationException("ObjectIds must not be empty when provided.");
        }
    }
}