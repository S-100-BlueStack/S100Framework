using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientRelatedRecordsTests
{
    [Fact]
    public async Task QueryRelatedRecordsAsync_SendsExpectedParameters_AndMapsGroupedRows() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0?f=json", StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: true,
                    supportsQueryRelatedPagination: false);
            }

            if (uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "geometryType": "esriGeometryPoint",
                  "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 100,
                      "relatedRecords": [
                        {
                          "attributes": { "OBJECTID": 1, "NAME": "Related A" },
                          "geometry": { "x": 10, "y": 20 }
                        },
                        {
                          "attributes": { "OBJECTID": 2, "NAME": "Related B" },
                          "geometry": { "x": 11, "y": 21 }
                        }
                      ]
                    },
                    {
                      "objectId": 200,
                      "relatedRecords": [
                        {
                          "attributes": { "OBJECTID": 3, "NAME": "Related C" },
                          "geometry": { "x": 12, "y": 22 }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200],
                RelationshipId = 1,
                OutFields = ["OBJECTID", "NAME"],
                DefinitionExpression = "1=1",
                ReturnGeometry = true,
                ReturnZ = false,
                ReturnM = false,
                OutSrid = 4326,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                OrderBy = "NAME"
            },
            cancellationToken);

        Assert.Equal(2, groups.Count);
        Assert.Equal(100, groups[0].SourceObjectId);
        Assert.Equal(2, groups[0].Records.Count);
        Assert.Equal(1, groups[0].Records[0].ObjectId);
        Assert.Equal("Related A", groups[0].Records[0].GetRequiredString("NAME"));
        Assert.NotNull(groups[0].Records[0].Geometry);

        Assert.Equal(200, groups[1].SourceObjectId);
        Assert.Single(groups[1].Records);
        Assert.Equal("Related C", groups[1].Records[0].GetRequiredString("NAME"));

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("objectIds=100%2C200", requestUri, StringComparison.Ordinal);
        Assert.Contains("relationshipId=1", requestUri, StringComparison.Ordinal);
        Assert.Contains("outFields=OBJECTID%2CNAME", requestUri, StringComparison.Ordinal);
        Assert.Contains("definitionExpression=1%3D1", requestUri, StringComparison.Ordinal);
        Assert.Contains("returnGeometry=true", requestUri, StringComparison.Ordinal);
        Assert.Contains("returnZ=false", requestUri, StringComparison.Ordinal);
        Assert.Contains("returnM=false", requestUri, StringComparison.Ordinal);
        Assert.Contains("outSR=4326", requestUri, StringComparison.Ordinal);
        Assert.Contains("geometryPrecision=3", requestUri, StringComparison.Ordinal);
        Assert.Contains("maxAllowableOffset=0.5", requestUri, StringComparison.Ordinal);
        Assert.Contains("orderByFields=NAME", requestUri, StringComparison.Ordinal);
        Assert.DoesNotContain("returnCountOnly=true", requestUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_SendsPaginationHistoricVersionTimeAndDatumParameters() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();
        var historicMoment = new DateTimeOffset(2026, 01, 15, 12, 30, 0, TimeSpan.Zero);

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: false,
                    supportsQueryRelatedPagination: true);
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relatedRecordGroups": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                ResultOffset = 10,
                ResultRecordCount = 25,
                GdbVersion = "SDE.DEFAULT",
                HistoricMoment = historicMoment,
                TimeReferenceUnknownClient = true,
                DatumTransformationWkid = 108190
            },
            cancellationToken);

        Assert.Empty(groups);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("resultOffset=10", decoded, StringComparison.Ordinal);
        Assert.Contains("resultRecordCount=25", decoded, StringComparison.Ordinal);
        Assert.Contains("gdbVersion=SDE.DEFAULT", decoded, StringComparison.Ordinal);
        Assert.Contains(
            $"historicMoment={historicMoment.ToUnixTimeMilliseconds()}",
            decoded,
            StringComparison.Ordinal);
        Assert.Contains("timeReferenceUnknownClient=true", decoded, StringComparison.Ordinal);
        Assert.Contains("datumTransformation=108190", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_SendsDatumTransformationJson_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();
        const string datumTransformationJson = """
        {
          "geoTransforms": [
            {
              "wkid": 108190,
              "forward": true
            }
          ]
        }
        """;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relatedRecordGroups": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                DatumTransformationJson = datumTransformationJson
            },
            cancellationToken);

        Assert.Empty(groups);

        var requestUri = Assert.Single(requestUris);
        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("datumTransformation=", decoded, StringComparison.Ordinal);
        Assert.Contains("\"geoTransforms\"", decoded, StringComparison.Ordinal);
        Assert.Contains("\"wkid\":108190", decoded.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_SendsReturnCountOnlyAndMapsCounts() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: true,
                    supportsQueryRelatedPagination: false);
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relatedRecordGroups": [
                    {
                      "objectId": 100,
                      "count": 3
                    },
                    {
                      "objectId": 200,
                      "count": 0
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await layerClient.QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200],
                RelationshipId = 1,
                OrderBy = "NAME"
            },
            cancellationToken);

        Assert.Equal(2, counts.Count);
        Assert.Equal(100, counts[0].SourceObjectId);
        Assert.Equal(3, counts[0].Count);
        Assert.Equal(200, counts[1].SourceObjectId);
        Assert.Equal(0, counts[1].Count);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase));

        var decoded = Uri.UnescapeDataString(requestUri);

        Assert.Contains("returnCountOnly=true", decoded, StringComparison.Ordinal);
        Assert.Contains("returnGeometry=true", decoded, StringComparison.Ordinal);
        Assert.Contains("orderByFields=NAME", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_MapsTableResultsWithoutGeometry() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "PLANNAME", "type": "esriFieldTypeString", "nullable": true }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 10,
                      "relatedRecords": [
                        {
                          "attributes": { "OBJECTID": 99, "PLANNAME": "Plan X" }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [10],
                RelationshipId = 0,
                OutFields = ["OBJECTID", "PLANNAME"],
                ReturnGeometry = false
            },
            TestContext.Current.CancellationToken);

        Assert.Single(groups);
        Assert.Single(groups[0].Records);
        Assert.Null(groups[0].Records[0].Geometry);
        Assert.Equal(99, groups[0].Records[0].ObjectId);
        Assert.Equal("Plan X", groups[0].Records[0].GetRequiredString("PLANNAME"));
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenObjectIdsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("object ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenOrderByIsProvidedWithoutAdvancedRelatedQuerySupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: false,
                    supportsQueryRelatedPagination: true);
            }

            throw new InvalidOperationException("Related-record query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    OrderBy = "NAME"
                },
                cancellationToken));

        Assert.Contains("advanced related-record", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_Throws_WhenAdvancedRelatedQueryIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: false,
                    supportsQueryRelatedPagination: true);
            }

            throw new InvalidOperationException("Related-record count query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryRelatedRecordCountsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("advanced related-record", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenPaginationIsProvidedWithoutRelatedPaginationSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsAdvancedQueryRelated: true,
                    supportsQueryRelatedPagination: false);
            }

            throw new InvalidOperationException("Related-record query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    ResultOffset = 10
                },
                cancellationToken));

        Assert.Contains("pagination", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Theory]
    [InlineData(-1, null, "ResultOffset")]
    [InlineData(null, 0, "ResultRecordCount")]
    [InlineData(null, -1, "ResultRecordCount")]
    public async Task QueryRelatedRecordsAsync_Throws_WhenPaginationValuesAreInvalid(
        int? resultOffset,
        int? resultRecordCount,
        string expectedMessagePart) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    ResultOffset = resultOffset,
                    ResultRecordCount = resultRecordCount
                },
                cancellationToken));

        Assert.Contains(expectedMessagePart, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenDatumTransformationValuesConflict() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    DatumTransformationWkid = 108190,
                    DatumTransformationJson = """{"wkid":108190}"""
                },
                cancellationToken));

        Assert.Contains("DatumTransformationWkid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DatumTransformationJson", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenDatumTransformationJsonIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    DatumTransformationJson = "[108190]"
                },
                cancellationToken));

        Assert.Contains("DatumTransformationJson", exception.Message, StringComparison.Ordinal);
    }

    private static IFeatureLayerClient CreateLayerClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsRelatedRecordsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryRelatedRecords",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsAdvancedQueryRelated,
        bool supportsQueryRelatedPagination) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "RelatedLayer",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsAdvancedQueryRelated": {{supportsAdvancedQueryRelated.ToString().ToLowerInvariant()}},
            "supportsQueryRelatedPagination": {{supportsQueryRelatedPagination.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": {
              "wkid": 4326,
              "latestWkid": 4326
            }
          }
        }
        """);
    }
}