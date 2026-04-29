namespace S100Framework.REST.Models;

/// <summary>
/// Represents a relationship rule returned by a feature service <c>relationships</c> resource.
/// </summary>
/// <param name="RuleId">
/// The relationship rule ID.
/// </param>
/// <param name="OriginSubtypeCode">
/// The origin subtype code the rule applies to.
/// </param>
/// <param name="OriginMinimumCardinality">
/// The minimum number of destination rows allowed for the origin side.
/// </param>
/// <param name="OriginMaximumCardinality">
/// The maximum number of destination rows allowed for the origin side.
/// </param>
/// <param name="DestinationSubtypeCode">
/// The destination subtype code the rule applies to.
/// </param>
/// <param name="DestinationMinimumCardinality">
/// The minimum number of origin rows allowed for the destination side.
/// </param>
/// <param name="DestinationMaximumCardinality">
/// The maximum number of origin rows allowed for the destination side.
/// </param>
public sealed record FeatureServiceRelationshipRule(
    int? RuleId,
    int? OriginSubtypeCode,
    int? OriginMinimumCardinality,
    int? OriginMaximumCardinality,
    int? DestinationSubtypeCode,
    int? DestinationMinimumCardinality,
    int? DestinationMaximumCardinality);