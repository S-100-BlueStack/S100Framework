using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class FeatureLayerSchemaTests
{
    [Fact]
    public void Constructor_WithoutLegacySupportsPagination_UsesCapabilitiesAsSourceOfTruth() {
        var capabilities = CreateCapabilities(supportsPagination: true);

        var schema = new FeatureLayerSchema(
            layerId: 0,
            name: "Layer 0",
            geometryType: "esriGeometryPoint",
            srid: 4326,
            hasZ: false,
            hasM: false,
            maxRecordCount: 1000,
            objectIdFieldName: "OBJECTID",
            fields: Array.Empty<FeatureField>(),
            capabilities: capabilities,
            relationships: Array.Empty<FeatureRelationshipInfo>());

        Assert.True(schema.Capabilities.SupportsPagination);

#pragma warning disable CS0618
        Assert.True(schema.SupportsPagination);
#pragma warning restore CS0618
    }

    [Fact]
    public void LegacyConstructor_Throws_WhenSupportsPaginationDoesNotMatchCapabilities() {
        var capabilities = CreateCapabilities(supportsPagination: false);

#pragma warning disable CS0618
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new FeatureLayerSchema(
                layerId: 0,
                name: "Layer 0",
                geometryType: "esriGeometryPoint",
                srid: 4326,
                hasZ: false,
                hasM: false,
                supportsPagination: true,
                maxRecordCount: 1000,
                objectIdFieldName: "OBJECTID",
                fields: Array.Empty<FeatureField>(),
                capabilities: capabilities,
                relationships: Array.Empty<FeatureRelationshipInfo>()));
#pragma warning restore CS0618

        Assert.Contains("Capabilities.SupportsPagination", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureLayerCapabilities CreateCapabilities(bool supportsPagination) {
        return new FeatureLayerCapabilities(
            HasAttachments: false,
            SupportsQueryAttachments: false,
            SupportsAttachmentsResizing: false,
            SupportsTopFeaturesQuery: false,
            SupportsPagination: supportsPagination,
            SupportsPaginationOnAggregatedQueries: false,
            SupportsQueryRelatedPagination: false,
            SupportsAdvancedQueryRelated: false,
            SupportsOrderBy: false,
            SupportsDistinct: false,
            SupportsAsyncApplyEdits: false);
    }
}