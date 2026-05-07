using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerSchemaMetadataTests
{
    [Fact]
    public async Task GetSchemaAsync_MapsCapabilitiesAndRelationships() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0?", StringComparison.OrdinalIgnoreCase)) {
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
                  "supportsAppend": true,
                  "supportsCalculate": true,
                  "supportsAsyncCalculate": true,
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
                    "supportsDistinct": true,
                    "supportsReturningGeometryEnvelope": true,
                    "supportsFullTextSearch": true,
                    "supportsPercentileStatistics": true,
                    "supportsQueryDateBins": true,
                    "supportsQueryAnalytic": true,
                    "supportsReturningQueryExtent": true,
                    "supportsReturningGeometryCentroid": true,
                    "supportsDefaultSR": true,
                    "supportsOutFieldSqlExpression": true,
                    "supportsSqlExpression": true,
                    "supportsHavingClause": true,
                    "supportsQueryWithDistance": true,
                    "supportsQueryWithResultType": true,
                    "supportsQueryWithHistoricMoment": true,
                    "supportsQueryWithDatumTransformation": true,
                    "supportsCoordinatesQuantization": true,
                    "supportsCurrentUserQueries": true,
                    "supportsQueryWithCacheHint": true,
                    "supportsQueryAttachmentsCountOnly": true,
                    "supportsQueryAttachmentOrderByFields": true
                  },
                  "advancedEditingCapabilities": {
                    "supportsAsyncApplyEdits": true
                  },
                  "advancedQueryAnalyticCapabilities": {
                    "supportsAsync": true
                  },
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
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

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.Equal("Facilities", schema.Name);
        Assert.Equal(4326, schema.Srid);

        Assert.True(schema.Capabilities.HasAttachments);
        Assert.True(schema.Capabilities.SupportsQueryAttachments);
        Assert.True(schema.Capabilities.SupportsAttachmentsResizing);
        Assert.True(schema.Capabilities.SupportsTopFeaturesQuery);
        Assert.True(schema.Capabilities.SupportsPagination);
        Assert.True(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.True(schema.Capabilities.SupportsQueryRelatedPagination);
        Assert.True(schema.Capabilities.SupportsAdvancedQueryRelated);
        Assert.True(schema.Capabilities.SupportsOrderBy);
        Assert.True(schema.Capabilities.SupportsDistinct);
        Assert.True(schema.Capabilities.SupportsAsyncApplyEdits);
        Assert.True(schema.Capabilities.SupportsReturningGeometryEnvelope);
        Assert.True(schema.Capabilities.SupportsFullTextSearch);
        Assert.True(schema.Capabilities.SupportsPercentileStatistics);
        Assert.True(schema.Capabilities.SupportsAppend);
        Assert.True(schema.Capabilities.SupportsQueryDateBins);
        Assert.True(schema.Capabilities.SupportsQueryAnalytic);
        Assert.True(schema.Capabilities.SupportsAsyncQueryAnalytic);
        Assert.True(schema.Capabilities.SupportsCalculate);
        Assert.True(schema.Capabilities.SupportsAsyncCalculate);
        Assert.True(schema.Capabilities.SupportsReturningQueryExtent);
        Assert.True(schema.Capabilities.SupportsReturningGeometryCentroid);
        Assert.True(schema.Capabilities.SupportsDefaultSrid);
        Assert.True(schema.Capabilities.SupportsOutFieldSqlExpression);
        Assert.True(schema.Capabilities.SupportsSqlExpression);
        Assert.True(schema.Capabilities.SupportsHavingClause);
        Assert.True(schema.Capabilities.SupportsQueryWithDistance);
        Assert.True(schema.Capabilities.SupportsQueryWithResultType);
        Assert.True(schema.Capabilities.SupportsQueryWithHistoricMoment);
        Assert.True(schema.Capabilities.SupportsQueryWithDatumTransformation);
        Assert.True(schema.Capabilities.SupportsCoordinatesQuantization);
        Assert.True(schema.Capabilities.SupportsCurrentUserQueries);
        Assert.True(schema.Capabilities.SupportsQueryWithCacheHint);
        Assert.True(schema.Capabilities.SupportsQueryAttachmentsCountOnly);
        Assert.True(schema.Capabilities.SupportsQueryAttachmentOrderByFields);

        Assert.Single(schema.Relationships);
        Assert.Equal(7, schema.Relationships[0].Id);
        Assert.Equal("facility_inspections", schema.Relationships[0].Name);
        Assert.Equal(3, schema.Relationships[0].RelatedTableId);
        Assert.Equal("esriRelCardinalityOneToMany", schema.Relationships[0].Cardinality);
        Assert.Equal("esriRelRoleOrigin", schema.Relationships[0].Role);
        Assert.Equal("GLOBALID", schema.Relationships[0].KeyField);
        Assert.True(schema.Relationships[0].Composite);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsMissingOptionalCapabilitiesToFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "id": 0,
                  "name": "Minimal",
                  "objectIdField": "OBJECTID",
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relationships": [],
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
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

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.False(schema.Capabilities.HasAttachments);
        Assert.False(schema.Capabilities.SupportsQueryAttachments);
        Assert.False(schema.Capabilities.SupportsAttachmentsResizing);
        Assert.False(schema.Capabilities.SupportsTopFeaturesQuery);
        Assert.False(schema.Capabilities.SupportsPagination);
        Assert.False(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.False(schema.Capabilities.SupportsQueryRelatedPagination);
        Assert.False(schema.Capabilities.SupportsAdvancedQueryRelated);
        Assert.False(schema.Capabilities.SupportsOrderBy);
        Assert.False(schema.Capabilities.SupportsDistinct);
        Assert.False(schema.Capabilities.SupportsAsyncApplyEdits);
        Assert.False(schema.Capabilities.SupportsReturningGeometryEnvelope);
        Assert.False(schema.Capabilities.SupportsFullTextSearch);
        Assert.False(schema.Capabilities.SupportsPercentileStatistics);
        Assert.False(schema.Capabilities.SupportsAppend);
        Assert.False(schema.Capabilities.SupportsQueryDateBins);
        Assert.False(schema.Capabilities.SupportsQueryAnalytic);
        Assert.False(schema.Capabilities.SupportsAsyncQueryAnalytic);
        Assert.False(schema.Capabilities.SupportsCalculate);
        Assert.False(schema.Capabilities.SupportsAsyncCalculate);
        Assert.False(schema.Capabilities.SupportsReturningQueryExtent);
        Assert.False(schema.Capabilities.SupportsReturningGeometryCentroid);
        Assert.False(schema.Capabilities.SupportsDefaultSrid);
        Assert.False(schema.Capabilities.SupportsOutFieldSqlExpression);
        Assert.False(schema.Capabilities.SupportsSqlExpression);
        Assert.False(schema.Capabilities.SupportsHavingClause);
        Assert.False(schema.Capabilities.SupportsQueryWithDistance);
        Assert.False(schema.Capabilities.SupportsQueryWithResultType);
        Assert.False(schema.Capabilities.SupportsQueryWithHistoricMoment);
        Assert.False(schema.Capabilities.SupportsQueryWithDatumTransformation);
        Assert.False(schema.Capabilities.SupportsCoordinatesQuantization);
        Assert.False(schema.Capabilities.SupportsCurrentUserQueries);
        Assert.False(schema.Capabilities.SupportsQueryWithCacheHint);
        Assert.False(schema.Capabilities.SupportsQueryAttachmentsCountOnly);
        Assert.False(schema.Capabilities.SupportsQueryAttachmentOrderByFields);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsPartiallySpecifiedAdvancedQueryCapabilities() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
        {
          "id": 0,
          "name": "PartialCapabilities",
          "objectIdField": "OBJECTID",
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "advancedQueryCapabilities": {
            "supportsPagination": false,
            "supportsPaginationOnAggregatedQueries": true,
            "supportsQueryRelatedPagination": false,
            "supportsAdvancedQueryRelated": true,
            "supportsOrderBy": false,
            "supportsDistinct": true,
            "supportsReturningGeometryEnvelope": false,
            "supportsFullTextSearch": true,
            "supportsPercentileStatistics": false,
            "supportsQueryDateBins": true,
            "supportsQueryAnalytic": false,
            "supportsReturningQueryExtent": true,
            "supportsReturningGeometryCentroid": false,
            "supportsDefaultSR": true,
            "supportsOutFieldSqlExpression": false,
            "supportsSqlExpression": true,
            "supportsHavingClause": false,
            "supportsQueryWithDistance": true,
            "supportsQueryWithResultType": false,
            "supportsQueryWithHistoricMoment": true,
            "supportsQueryWithDatumTransformation": false,
            "supportsCoordinatesQuantization": true,
            "supportsCurrentUserQueries": false,
            "supportsQueryWithCacheHint": true
          },
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.False(schema.Capabilities.SupportsPagination);
        Assert.True(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.False(schema.Capabilities.SupportsQueryRelatedPagination);
        Assert.True(schema.Capabilities.SupportsAdvancedQueryRelated);
        Assert.False(schema.Capabilities.SupportsOrderBy);
        Assert.True(schema.Capabilities.SupportsDistinct);
        Assert.False(schema.Capabilities.SupportsReturningGeometryEnvelope);
        Assert.True(schema.Capabilities.SupportsFullTextSearch);
        Assert.False(schema.Capabilities.SupportsPercentileStatistics);
        Assert.True(schema.Capabilities.SupportsQueryDateBins);
        Assert.False(schema.Capabilities.SupportsQueryAnalytic);
        Assert.True(schema.Capabilities.SupportsReturningQueryExtent);
        Assert.False(schema.Capabilities.SupportsReturningGeometryCentroid);
        Assert.True(schema.Capabilities.SupportsDefaultSrid);
        Assert.False(schema.Capabilities.SupportsOutFieldSqlExpression);
        Assert.True(schema.Capabilities.SupportsSqlExpression);
        Assert.False(schema.Capabilities.SupportsHavingClause);
        Assert.True(schema.Capabilities.SupportsQueryWithDistance);
        Assert.False(schema.Capabilities.SupportsQueryWithResultType);
        Assert.True(schema.Capabilities.SupportsQueryWithHistoricMoment);
        Assert.False(schema.Capabilities.SupportsQueryWithDatumTransformation);
        Assert.True(schema.Capabilities.SupportsCoordinatesQuantization);
        Assert.False(schema.Capabilities.SupportsCurrentUserQueries);
        Assert.True(schema.Capabilities.SupportsQueryWithCacheHint);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsNullAdvancedQueryCapabilitiesToFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
        {
          "id": 0,
          "name": "NullAdvancedQueryCapabilities",
          "objectIdField": "OBJECTID",
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "advancedQueryCapabilities": null,
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.False(schema.Capabilities.SupportsPagination);
        Assert.False(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.False(schema.Capabilities.SupportsQueryRelatedPagination);
        Assert.False(schema.Capabilities.SupportsAdvancedQueryRelated);
        Assert.False(schema.Capabilities.SupportsOrderBy);
        Assert.False(schema.Capabilities.SupportsDistinct);
        Assert.False(schema.Capabilities.SupportsReturningGeometryEnvelope);
        Assert.False(schema.Capabilities.SupportsFullTextSearch);
        Assert.False(schema.Capabilities.SupportsPercentileStatistics);
        Assert.False(schema.Capabilities.SupportsQueryDateBins);
        Assert.False(schema.Capabilities.SupportsQueryAnalytic);
        Assert.False(schema.Capabilities.SupportsReturningQueryExtent);
        Assert.False(schema.Capabilities.SupportsReturningGeometryCentroid);
        Assert.False(schema.Capabilities.SupportsDefaultSrid);
        Assert.False(schema.Capabilities.SupportsOutFieldSqlExpression);
        Assert.False(schema.Capabilities.SupportsSqlExpression);
        Assert.False(schema.Capabilities.SupportsHavingClause);
        Assert.False(schema.Capabilities.SupportsQueryWithDistance);
        Assert.False(schema.Capabilities.SupportsQueryWithResultType);
        Assert.False(schema.Capabilities.SupportsQueryWithHistoricMoment);
        Assert.False(schema.Capabilities.SupportsQueryWithDatumTransformation);
        Assert.False(schema.Capabilities.SupportsCoordinatesQuantization);
        Assert.False(schema.Capabilities.SupportsCurrentUserQueries);
        Assert.False(schema.Capabilities.SupportsQueryWithCacheHint);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsNullAdvancedEditingAndAnalyticCapabilitiesToFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
        {
          "id": 0,
          "name": "NullNestedCapabilities",
          "objectIdField": "OBJECTID",
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "advancedQueryCapabilities": {
            "supportsQueryAnalytic": true
          },
          "advancedEditingCapabilities": null,
          "advancedQueryAnalyticCapabilities": null,
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.False(schema.Capabilities.SupportsAsyncApplyEdits);
        Assert.True(schema.Capabilities.SupportsQueryAnalytic);
        Assert.False(schema.Capabilities.SupportsAsyncQueryAnalytic);
    }

    [Fact]
    public async Task GetSchemaAsync_IgnoresNullFieldAndRelationshipItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
    {
      "id": 0,
      "name": "Facilities",
      "geometryType": "esriGeometryPoint",
      "objectIdField": "OBJECTID",
      "fields": [
        null,
        { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
        null,
        { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
      ],
      "relationships": [
        null,
        {
          "id": 7,
          "name": "facility_inspections",
          "relatedTableId": 3,
          "cardinality": "esriRelCardinalityOneToMany",
          "role": "esriRelRoleOrigin",
          "keyField": "GLOBALID",
          "composite": true
        },
        null
      ],
      "extent": {
        "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
      }
    }
    """);

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.Collection(
            schema.Fields,
            field => Assert.Equal("OBJECTID", field.Name),
            field => Assert.Equal("NAME", field.Name));

        var relationship = Assert.Single(schema.Relationships);

        Assert.Equal(7, relationship.Id);
        Assert.Equal("facility_inspections", relationship.Name);
    }

    [Fact]
    public void FeatureLayerCapabilities_CanBeConstructedWithOriginalRequiredParameters() {
        var capabilities = new FeatureLayerCapabilities(
            HasAttachments: true,
            SupportsQueryAttachments: true,
            SupportsAttachmentsResizing: false,
            SupportsTopFeaturesQuery: true,
            SupportsPagination: true,
            SupportsPaginationOnAggregatedQueries: false,
            SupportsQueryRelatedPagination: true,
            SupportsAdvancedQueryRelated: true,
            SupportsOrderBy: true,
            SupportsDistinct: true,
            SupportsAsyncApplyEdits: false);

        Assert.True(capabilities.HasAttachments);
        Assert.True(capabilities.SupportsQueryAttachments);
        Assert.False(capabilities.SupportsAttachmentsResizing);
        Assert.True(capabilities.SupportsTopFeaturesQuery);
        Assert.True(capabilities.SupportsPagination);
        Assert.False(capabilities.SupportsPaginationOnAggregatedQueries);
        Assert.True(capabilities.SupportsQueryRelatedPagination);
        Assert.True(capabilities.SupportsAdvancedQueryRelated);
        Assert.True(capabilities.SupportsOrderBy);
        Assert.True(capabilities.SupportsDistinct);
        Assert.False(capabilities.SupportsAsyncApplyEdits);

        Assert.False(capabilities.SupportsReturningGeometryEnvelope);
        Assert.False(capabilities.SupportsFullTextSearch);
        Assert.False(capabilities.SupportsPercentileStatistics);
        Assert.False(capabilities.SupportsAppend);
        Assert.False(capabilities.SupportsQueryDateBins);
        Assert.False(capabilities.SupportsQueryAnalytic);
        Assert.False(capabilities.SupportsAsyncQueryAnalytic);
        Assert.False(capabilities.SupportsCalculate);
        Assert.False(capabilities.SupportsAsyncCalculate);
        Assert.False(capabilities.SupportsReturningQueryExtent);
        Assert.False(capabilities.SupportsReturningGeometryCentroid);
        Assert.False(capabilities.SupportsDefaultSrid);
        Assert.False(capabilities.SupportsOutFieldSqlExpression);
        Assert.False(capabilities.SupportsSqlExpression);
        Assert.False(capabilities.SupportsHavingClause);
        Assert.False(capabilities.SupportsQueryWithDistance);
        Assert.False(capabilities.SupportsQueryWithResultType);
        Assert.False(capabilities.SupportsQueryWithHistoricMoment);
        Assert.False(capabilities.SupportsQueryWithDatumTransformation);
        Assert.False(capabilities.SupportsCoordinatesQuantization);
        Assert.False(capabilities.SupportsCurrentUserQueries);
        Assert.False(capabilities.SupportsQueryWithCacheHint);
    }

    [Fact]
    public async Task GetSchemaAsync_ThrowsFeatureServiceException_WhenRelationshipIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
    {
      "id": 0,
      "name": "Facilities",
      "geometryType": "esriGeometryPoint",
      "objectIdField": "OBJECTID",
      "fields": [
        { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
      ],
      "relationships": [
        {
          "name": "facility_inspections",
          "relatedTableId": 3
        }
      ],
      "extent": {
        "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
      }
    }
    """);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).GetSchemaAsync(cancellationToken));

        Assert.Contains("relationship", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSchemaAsync_ThrowsFeatureServiceException_WhenRelationshipIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateSchemaClient("""
    {
      "id": 0,
      "name": "Facilities",
      "geometryType": "esriGeometryPoint",
      "objectIdField": "OBJECTID",
      "fields": [
        { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
      ],
      "relationships": [
        {
          "id": -1,
          "name": "facility_inspections",
          "relatedTableId": 3
        }
      ],
      "extent": {
        "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
      }
    }
    """);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).GetSchemaAsync(cancellationToken));

        Assert.Contains("relationship", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateSchemaClient(string layerMetadataJson) {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json(layerMetadataJson);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        return new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }
}