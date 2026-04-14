namespace S100Framework.REST.Models;

public sealed record FeatureRelationshipInfo(
    int Id,
    string? Name,
    int? RelatedTableId,
    string? Cardinality,
    string? Role,
    string? KeyField,
    bool? Composite);