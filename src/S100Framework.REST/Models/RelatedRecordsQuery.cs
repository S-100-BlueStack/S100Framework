namespace S100Framework.REST.Models;

public sealed record RelatedRecordsQuery
{
    public IReadOnlyList<long> ObjectIds { get; init; } = Array.Empty<long>();

    public required int RelationshipId { get; init; }

    public IReadOnlyList<string>? OutFields { get; init; }

    public string? DefinitionExpression { get; init; }

    public bool ReturnGeometry { get; init; } = true;

    public bool? ReturnZ { get; init; }

    public bool? ReturnM { get; init; }

    public int? OutSrid { get; init; }

    public int? GeometryPrecision { get; init; }

    public double? MaxAllowableOffset { get; init; }

    public string? OrderBy { get; init; }

    public void Validate() {
        if (ObjectIds.Count == 0) {
            throw new InvalidOperationException("At least one source object ID must be provided.");
        }

        if (RelationshipId < 0) {
            throw new InvalidOperationException("RelationshipId must be greater than or equal to zero.");
        }
    }
}