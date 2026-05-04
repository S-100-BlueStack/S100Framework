using System.Text.Json.Serialization;

namespace S100Framework.REST.Internal.Dto;

internal sealed class EsriServiceRelationshipsResponseDto
{
    [JsonPropertyName("relationships")]
    public List<EsriServiceRelationshipDto?>? Relationships { get; init; }
}

internal sealed class EsriServiceRelationshipDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("catalogID")]
    public string? CatalogId { get; init; }

    [JsonPropertyName("backwardPathLabel")]
    public string? BackwardPathLabel { get; init; }

    [JsonPropertyName("originLayerId")]
    public int? OriginLayerId { get; init; }

    [JsonPropertyName("originPrimaryKey")]
    public string? OriginPrimaryKey { get; init; }

    [JsonPropertyName("forwardPathLabel")]
    public string? ForwardPathLabel { get; init; }

    [JsonPropertyName("destinationLayerId")]
    public int? DestinationLayerId { get; init; }

    [JsonPropertyName("originForeignKey")]
    public string? OriginForeignKey { get; init; }

    [JsonPropertyName("relationshipTableId")]
    public int? RelationshipTableId { get; init; }

    [JsonPropertyName("destinationPrimaryKey")]
    public string? DestinationPrimaryKey { get; init; }

    [JsonPropertyName("destinationForeignKey")]
    public string? DestinationForeignKey { get; init; }

    [JsonPropertyName("rules")]
    public List<EsriServiceRelationshipRuleDto?>? Rules { get; init; }

    [JsonPropertyName("cardinality")]
    public string? Cardinality { get; init; }

    [JsonPropertyName("attributed")]
    public bool? Attributed { get; init; }

    [JsonPropertyName("composite")]
    public bool? Composite { get; init; }
}

internal sealed class EsriServiceRelationshipRuleDto
{
    [JsonPropertyName("ruleID")]
    public int? RuleId { get; init; }

    [JsonPropertyName("originSubtypeCode")]
    public int? OriginSubtypeCode { get; init; }

    [JsonPropertyName("originMinimumCardinality")]
    public int? OriginMinimumCardinality { get; init; }

    [JsonPropertyName("originMaximumCardinality")]
    public int? OriginMaximumCardinality { get; init; }

    [JsonPropertyName("destinationSubtypeCode")]
    public int? DestinationSubtypeCode { get; init; }

    [JsonPropertyName("destinationMinimumCardinality")]
    public int? DestinationMinimumCardinality { get; init; }

    [JsonPropertyName("destinationMaximumCardinality")]
    public int? DestinationMaximumCardinality { get; init; }
}