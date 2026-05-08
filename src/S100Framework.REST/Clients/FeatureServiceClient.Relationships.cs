using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Internal.Http;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides service-level <c>relationships</c> resource operations for feature service endpoints.
/// </summary>
public sealed partial class FeatureServiceClient
{
    /// <inheritdoc />
    public async Task<FeatureServiceRelationshipsResult> GetRelationshipsAsync(
        CancellationToken cancellationToken = default) {
        var metadata = await GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsRelationshipsResource) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not advertise relationships resource support.");
        }

        var uri = UriUtility.WithQuery(
            UriUtility.AppendPath(_serviceUri, "relationships"),
            new Dictionary<string, string?> {
                ["f"] = "json"
            });

        var dto = await GetAsync<EsriServiceRelationshipsResponseDto>(
            uri,
            cancellationToken);

        return new FeatureServiceRelationshipsResult(
            (dto.Relationships ?? Enumerable.Empty<EsriServiceRelationshipDto?>())
                .Where(static relationship => relationship is not null)
                .Select(relationship => MapServiceRelationship(relationship!, uri))
                .ToArray());
    }

    private static FeatureServiceRelationship MapServiceRelationship(
        EsriServiceRelationshipDto dto,
        Uri requestUri) {
        if (!dto.Id.HasValue) {
            throw new FeatureServiceException(
                "The service returned a relationship without an ID.",
                requestUri);
        }

        if (dto.Id.Value < 0) {
            throw new FeatureServiceException(
                "The service returned a relationship with a negative ID.",
                requestUri);
        }

        return new FeatureServiceRelationship(
            dto.Id.Value,
            dto.Name,
            dto.CatalogId,
            dto.BackwardPathLabel,
            dto.OriginLayerId,
            dto.OriginPrimaryKey,
            dto.ForwardPathLabel,
            dto.DestinationLayerId,
            dto.OriginForeignKey,
            dto.RelationshipTableId,
            dto.DestinationPrimaryKey,
            dto.DestinationForeignKey,
            dto.Cardinality,
            dto.Attributed,
            dto.Composite,
            (dto.Rules ?? Enumerable.Empty<EsriServiceRelationshipRuleDto?>())
                .Where(static rule => rule is not null)
                .Select(static rule => MapServiceRelationshipRule(rule!))
                .ToArray());
    }

    private static FeatureServiceRelationshipRule MapServiceRelationshipRule(
        EsriServiceRelationshipRuleDto dto) {
        return new FeatureServiceRelationshipRule(
            dto.RuleId,
            dto.OriginSubtypeCode,
            dto.OriginMinimumCardinality,
            dto.OriginMaximumCardinality,
            dto.DestinationSubtypeCode,
            dto.DestinationMinimumCardinality,
            dto.DestinationMaximumCardinality);
    }
}