using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientQueryHardeningTests
{
    [Fact]
    public async Task QueryAsync_IgnoresNullFeatureItems_WhenUsingPagination() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: true);
            }

            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    null,
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "NAME": "Feature A"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var records = new List<FeatureRecord>();

        await foreach (var record in layerClient.QueryAsync(
            new FeatureQuery {
                ReturnGeometry = false
            },
            cancellationToken)) {
            records.Add(record);
        }

        var feature = Assert.Single(records);

        Assert.Equal(10, feature.ObjectId);
        Assert.Equal("Feature A", feature.GetRequiredString("NAME"));
    }

    [Fact]
    public async Task QueryAsync_IgnoresNullFeatureItems_WhenUsingObjectIdFallback() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: false);
            }

            if (IsQueryRequest(request) &&
                request.RequestUri!.Query.Contains("returnIdsOnly=true", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [
                    10
                  ]
                }
                """);
            }

            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": [
                    null,
                    {
                      "attributes": {
                        "OBJECTID": 10,
                        "NAME": "Feature A"
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var records = new List<FeatureRecord>();

        await foreach (var record in layerClient.QueryAsync(
            new FeatureQuery {
                ReturnGeometry = false
            },
            cancellationToken)) {
            records.Add(record);
        }

        var feature = Assert.Single(records);

        Assert.Equal(10, feature.ObjectId);
        Assert.Equal("Feature A", feature.GetRequiredString("NAME"));
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IgnoresNullObjectIdItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [
                    null,
                    10,
                    20
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var objectIds = await layerClient.QueryObjectIdsAsync(
            new FeatureQuery(),
            cancellationToken);

        Assert.Equal([10, 20], objectIds);
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenSqlFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(
                new FeatureQuery {
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken));

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenOutFieldsContainBlankValue_BeforeSchemaLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    OutFields = ["OBJECTID", " "]
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("OutFields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenSqlFormatIsInvalid_BeforeSchemaLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenOutSridIsInvalid_BeforeSchemaLookup() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    OutSrid = 0
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("OutSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenResultTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var layerClient = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: true);
            }

            throw new InvalidOperationException("Feature query endpoint should not be called.");
        }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    ResultType = (FeatureQueryResultType)999
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("ResultType", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            requestUris,
            uri => uri.AbsolutePath.EndsWith("/FeatureServer/0/query", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenMultipatchOptionIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var layerClient = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsPagination: true);
            }

            throw new InvalidOperationException("Feature query endpoint should not be called.");
        }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    MultipatchOption = (FeatureQueryMultipatchOption)999
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("MultipatchOption", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            requestUris,
            uri => uri.AbsolutePath.EndsWith("/FeatureServer/0/query", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task QueryAsync_Throws_WhenMaxAllowableOffsetIsNotFinite_BeforeSchemaLookup(
    double maxAllowableOffset) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."))
            .GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await foreach (var _ in layerClient.QueryAsync(
                new FeatureQuery {
                    MaxAllowableOffset = maxAllowableOffset
                },
                cancellationToken)) {
            }
        });

        Assert.Contains("MaxAllowableOffset", exception.Message, StringComparison.Ordinal);
        Assert.Contains("finite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_ThrowsFeatureServiceException_WhenObjectIdsContainNegativeValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var layerClient = CreateClient(request => {
            if (IsQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "objectIdFieldName": "OBJECTID",
              "objectIds": [
                10,
                -1,
                20
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            layerClient.QueryObjectIdsAsync(
                new FeatureQuery(),
                cancellationToken));

        Assert.Contains("query", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("objectId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Get
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsPagination) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}}
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