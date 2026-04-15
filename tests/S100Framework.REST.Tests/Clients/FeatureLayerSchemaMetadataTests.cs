using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerSchemaMetadataTests
{
    [Fact]
    public async Task GetSchemaAsync_MapsCapabilitiesAndRelationships() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "Facilities",
                  "geometryType": "esriGeometryPoint",
                  "hasZ": false,
                  "hasM": false,
                  "hasAttachments": true,
                  "supportsQueryAttachments": true,
                  "supportsAttachmentsResizing": true,
                  "supportsTopFeaturesQuery": true,
                  "maxRecordCount": 2000,
                  "objectIdField": "OBJECTID",
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "NAME", "type": "esriFieldTypeString", "nullable": true, "length": 255 }
                  ],
                  "relationships": [
                    {
                      "id": 7,
                      "name": "facility_inspections",
                      "relatedTableId": 3,
                      "cardinality": "esriRelCardinalityOneToMany",
                      "role": "esriRelRoleOrigin",
                      "keyField": "GLOBALID",
                      "composite": true
                    }
                  ],
                  "advancedQueryCapabilities": {
                    "supportsPagination": true,
                    "supportsPaginationOnAggregatedQueries": true,
                    "supportsQueryRelatedPagination": true,
                    "supportsAdvancedQueryRelated": true,
                    "supportsOrderBy": true,
                    "supportsDistinct": true
                  },
                                  "advancedEditingCapabilities": {
                  "supportsAsyncApplyEdits": true
                },
                  "extent": {
                    "spatialReference": {
                      "wkid": 4326,
                      "latestWkid": 4326
                    }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var schema = await client.GetLayerClient(0).GetSchemaAsync();

        Assert.Equal("Facilities", schema.Name);
        Assert.Equal(4326, schema.Srid);
        Assert.True(schema.SupportsPagination);

        Assert.True(schema.Capabilities.HasAttachments);
        Assert.True(schema.Capabilities.SupportsQueryAttachments);
        Assert.True(schema.Capabilities.SupportsAttachmentsResizing);
        Assert.True(schema.Capabilities.SupportsTopFeaturesQuery);
        Assert.True(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.True(schema.Capabilities.SupportsQueryRelatedPagination);
        Assert.True(schema.Capabilities.SupportsAdvancedQueryRelated);
        Assert.True(schema.Capabilities.SupportsOrderBy);
        Assert.True(schema.Capabilities.SupportsDistinct);
        Assert.True(schema.Capabilities.SupportsAsyncApplyEdits);

        Assert.Single(schema.Relationships);
        Assert.Equal(7, schema.Relationships[0].Id);
        Assert.Equal("facility_inspections", schema.Relationships[0].Name);
        Assert.Equal(3, schema.Relationships[0].RelatedTableId);
        Assert.Equal("esriRelCardinalityOneToMany", schema.Relationships[0].Cardinality);
        Assert.Equal("esriRelRoleOrigin", schema.Relationships[0].Role);
        Assert.Equal("GLOBALID", schema.Relationships[0].KeyField);
        Assert.True(schema.Relationships[0].Composite);
    }
}