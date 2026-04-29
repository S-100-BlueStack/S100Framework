namespace S100Framework.REST.Models;

/// <summary>
/// Represents a relationship class returned by a feature service <c>relationships</c> resource.
/// </summary>
/// <param name="Id">
/// The unique relationship ID within the feature service.
/// </param>
/// <param name="Name">
/// The relationship name.
/// </param>
/// <param name="CatalogId">
/// The backend catalog identifier, when returned by the service.
/// </param>
/// <param name="BackwardPathLabel">
/// The label used when navigating from destination to origin.
/// </param>
/// <param name="OriginLayerId">
/// The origin layer or table ID.
/// </param>
/// <param name="OriginPrimaryKey">
/// The origin primary key field.
/// </param>
/// <param name="ForwardPathLabel">
/// The label used when navigating from origin to destination.
/// </param>
/// <param name="DestinationLayerId">
/// The destination layer or table ID.
/// </param>
/// <param name="OriginForeignKey">
/// The origin foreign key field.
/// </param>
/// <param name="RelationshipTableId">
/// The intermediate relationship table ID for attributed relationships.
/// </param>
/// <param name="DestinationPrimaryKey">
/// The destination primary key field.
/// </param>
/// <param name="DestinationForeignKey">
/// The destination foreign key field.
/// </param>
/// <param name="Cardinality">
/// The raw Esri relationship cardinality value.
/// </param>
/// <param name="Attributed">
/// Indicates whether the relationship is attributed, when returned by the service.
/// </param>
/// <param name="Composite">
/// Indicates whether the relationship is composite, when returned by the service.
/// </param>
/// <param name="Rules">
/// The relationship rules returned by the service.
/// </param>
public sealed record FeatureServiceRelationship(
    int Id,
    string? Name,
    string? CatalogId,
    string? BackwardPathLabel,
    int? OriginLayerId,
    string? OriginPrimaryKey,
    string? ForwardPathLabel,
    int? DestinationLayerId,
    string? OriginForeignKey,
    int? RelationshipTableId,
    string? DestinationPrimaryKey,
    string? DestinationForeignKey,
    string? Cardinality,
    bool? Attributed,
    bool? Composite,
    IReadOnlyList<FeatureServiceRelationshipRule> Rules);