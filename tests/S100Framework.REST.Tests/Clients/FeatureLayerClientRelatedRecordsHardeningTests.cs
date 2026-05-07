using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientRelatedRecordsHardeningTests
{
    [Fact]
    public async Task QueryRelatedRecordsAsync_MapsMissingRelatedRecordGroupsToEmptyResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1
            },
            cancellationToken);

        Assert.Empty(groups);

        var requestUri = Assert.Single(requestUris);
        Assert.Contains("/FeatureServer/0/queryRelatedRecords?", requestUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/FeatureServer/0?f=json", requestUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_MapsMissingAndNullRelatedRecordsToEmptyRecordLists() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 100
                    },
                    {
                      "objectId": 200,
                      "relatedRecords": null
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200],
                RelationshipId = 1
            },
            cancellationToken);

        Assert.Equal(2, groups.Count);

        Assert.Equal(100, groups[0].SourceObjectId);
        Assert.Empty(groups[0].Records);

        Assert.Equal(200, groups[1].SourceObjectId);
        Assert.Empty(groups[1].Records);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_MapsMissingRelatedRecordGroupsToEmptyResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await layerClient.QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1
            },
            cancellationToken);

        Assert.Empty(counts);
        Assert.Contains(
            requestUris,
            uri => uri.EndsWith("/FeatureServer/0?f=json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryRelatedRecords?", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_MapsMissingAndNullCountsToZero() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relatedRecordGroups": [
                    {
                      "objectId": 100
                    },
                    {
                      "objectId": 200,
                      "count": null
                    },
                    {
                      "objectId": 300,
                      "count": 4
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await layerClient.QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200, 300],
                RelationshipId = 1
            },
            cancellationToken);

        Assert.Equal(3, counts.Count);

        Assert.Equal(100, counts[0].SourceObjectId);
        Assert.Equal(0, counts[0].Count);

        Assert.Equal(200, counts[1].SourceObjectId);
        Assert.Equal(0, counts[1].Count);

        Assert.Equal(300, counts[2].SourceObjectId);
        Assert.Equal(4, counts[2].Count);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_PostFormPreservesDatumTransformationJson_WhenPosted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();
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

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsRelatedRecordsRequest(request)) {
                    relatedRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "relatedRecordGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                DatumTransformationJson = datumTransformationJson
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(relatedRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("application/x-www-form-urlencoded", request.Content?.Headers.ContentType?.MediaType);
        Assert.True(form.ContainsKey("datumTransformation"));

        using var document = JsonDocument.Parse(form["datumTransformation"]);
        var transform = document.RootElement
            .GetProperty("geoTransforms")
            .EnumerateArray()
            .Single();

        Assert.Equal(108190, transform.GetProperty("wkid").GetInt32());
        Assert.True(transform.GetProperty("forward").GetBoolean());
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_PostFormPreservesCountOnlyShape_WhenPosted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsRelatedRecordsRequest(request)) {
                    relatedRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "relatedRecordGroups": [
                        {
                          "objectId": 100,
                          "count": 2
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var counts = await client.GetLayerClient(0).QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                OrderBy = "NAME",
                ResultOffset = 0,
                ResultRecordCount = 10
            },
            cancellationToken);

        var count = Assert.Single(counts);
        Assert.Equal(100, count.SourceObjectId);
        Assert.Equal(2, count.Count);

        var request = Assert.Single(relatedRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("true", form["returnCountOnly"]);
        Assert.Equal("NAME", form["orderByFields"]);
        Assert.Equal("0", form["resultOffset"]);
        Assert.Equal("10", form["resultRecordCount"]);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_IgnoresNullGroupAndRelatedRecordItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "geometryType": "esriGeometryPoint",
              "spatialReference": {
                "wkid": 4326,
                "latestWkid": 4326
              },
              "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
              ],
              "relatedRecordGroups": [
                null,
                {
                  "objectId": 100,
                  "relatedRecords": [
                    null,
                    {
                      "attributes": {
                        "OBJECTID": 1,
                        "NAME": "Related A"
                      },
                      "geometry": {
                        "x": 10,
                        "y": 20
                      }
                    }
                  ]
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                OutFields = ["OBJECTID", "NAME"],
                ReturnGeometry = true
            },
            cancellationToken);

        var group = Assert.Single(groups);

        Assert.Equal(100, group.SourceObjectId);

        var record = Assert.Single(group.Records);

        Assert.Equal(1, record.ObjectId);
        Assert.Equal("Related A", record.GetRequiredString("NAME"));
        Assert.NotNull(record.Geometry);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_IgnoresNullGroupItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "relatedRecordGroups": [
                null,
                {
                  "objectId": 100,
                  "count": 3
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await layerClient.QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1
            },
            cancellationToken);

        var group = Assert.Single(counts);

        Assert.Equal(100, group.SourceObjectId);
        Assert.Equal(3, group.Count);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenObjectIdsContainNegativeValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100, -1],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenObjectIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100, 100],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenOutFieldsContainBlankValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    OutFields = ["OBJECTID", " "]
                },
                cancellationToken));

        Assert.Contains("OutFields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenDefinitionExpressionIsBlank() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    DefinitionExpression = " "
                },
                cancellationToken));

        Assert.Contains("DefinitionExpression", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenOutSridIsNotPositive() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    OutSrid = 0
                },
                cancellationToken));

        Assert.Contains("OutSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenGdbVersionIsBlank() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    GdbVersion = " "
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task QueryRelatedRecordsAsync_Throws_WhenMaxAllowableOffsetIsNotFinite(
    double maxAllowableOffset) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1,
                    MaxAllowableOffset = maxAllowableOffset
                },
                cancellationToken));

        Assert.Contains("MaxAllowableOffset", exception.Message, StringComparison.Ordinal);
        Assert.Contains("finite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_ThrowsFeatureServiceException_WhenGroupObjectIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
              ],
              "relatedRecordGroups": [
                {
                  "relatedRecords": []
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("queryRelatedRecords", exception.Message, StringComparison.Ordinal);
        Assert.Contains("objectId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_ThrowsFeatureServiceException_WhenGroupObjectIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
              ],
              "relatedRecordGroups": [
                {
                  "objectId": -1,
                  "relatedRecords": []
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("queryRelatedRecords", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_ThrowsFeatureServiceException_WhenGroupObjectIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateLayerClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsRelatedRecordsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "relatedRecordGroups": [
                {
                  "count": 2
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.QueryRelatedRecordCountsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [100],
                    RelationshipId = 1
                },
                cancellationToken));

        Assert.Contains("queryRelatedRecords", exception.Message, StringComparison.Ordinal);
        Assert.Contains("objectId", exception.Message, StringComparison.Ordinal);
    }

    private static IFeatureLayerClient CreateLayerClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return CreateClient(
                QueryRequestMethodPreference.Get,
                autoPostQueryLengthThreshold: 10_000,
                handler)
            .GetLayerClient(0);
    }

    private static FeatureServiceClient CreateClient(
        QueryRequestMethodPreference queryRequestMethodPreference,
        int autoPostQueryLengthThreshold,
        Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = queryRequestMethodPreference,
                AutoPostQueryLengthThreshold = autoPostQueryLengthThreshold
            });
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

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content is null
            ? string.Empty
            : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormValue(parts[0]),
                parts => parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string DecodeFormValue(string value) {
        return Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "RelatedLayer",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsAdvancedQueryRelated": true,
            "supportsQueryRelatedPagination": true
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