namespace S100Framework.REST.Models;

/// <summary>
/// Describes a relationship exposed by a layer.
/// </summary>
/// <param name="Id">
/// The relationship ID.
/// </param>
/// <param name="Name">
/// The relationship name, when available.
/// </param>
/// <param name="RelatedTableId">
/// The related table or layer ID, when available.
/// </param>
/// <param name="Cardinality">
/// The relationship cardinality, when available.
/// </param>
/// <param name="Role">
/// The relationship role, when available.
/// </param>
/// <param name="KeyField">
/// The key field used by the relationship, when available.
/// </param>
/// <param name="Composite">
/// Indicates whether the relationship is composite, when available.
/// </param>
public sealed record FeatureRelationshipInfo(
    int Id,
    string? Name,
    int? RelatedTableId,
    string? Cardinality,
    string? Role,
    string? KeyField,
    bool? Composite);