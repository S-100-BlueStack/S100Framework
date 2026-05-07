using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientServiceQueryHardeningTests
{
    [Fact]
    public async Task QueryAsync_ReturnsEmptyLayers_WhenLayersPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(CreateRequest(), cancellationToken);

        Assert.Empty(result.Layers);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyLayers_WhenLayersPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(CreateRequest(), cancellationToken);

        Assert.Empty(result.Layers);
    }

    [Fact]
    public async Task QueryAsync_IgnoresNullLayerAndFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    null,
                    {
                      "id": 0,
                      "objectIdFieldName": "OBJECTID",
                      "features": [
                        null,
                        {
                          "attributes": {
                            "OBJECTID": 10,
                            "NAME": "Harbor A"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(CreateRequest(), cancellationToken);

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.LayerId);

        var record = Assert.Single(layer.Records);
        Assert.Equal(10, record.ObjectId);
        Assert.Equal("Harbor A", record.Attributes["NAME"]);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEmptyRecords_WhenFeaturesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    {
                      "id": 0
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryAsync(CreateRequest(), cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Empty(layer.Records);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IgnoresNullLayerAndObjectIdItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    null,
                    {
                      "id": 0,
                      "objectIdFieldName": "OBJECTID",
                      "objectIds": [
                        null,
                        10,
                        20
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryObjectIdsAsync(CreateRequest(), cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Equal("OBJECTID", layer.ObjectIdFieldName);
        Assert.Equal([10, 20], layer.ObjectIds);
    }

    [Fact]
    public async Task QueryCountAsync_IgnoresNullLayerItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    null,
                    {
                      "id": 0,
                      "count": 42
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryCountAsync(CreateRequest(), cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Equal(42, layer.Count);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_IgnoresNullLayerItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layers": [
                    null,
                    {
                      "id": 0,
                      "uniqueIdFieldNames": "GLOBALID",
                      "uniqueIds": [
                        "a",
                        "b"
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryUniqueIdsAsync(CreateRequest(), cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Equal(["GLOBALID"], layer.UniqueIdFieldNames);
        Assert.Equal("a", layer.UniqueIds[0].SingleValue);
        Assert.Equal("b", layer.UniqueIds[1].SingleValue);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_IgnoresNullUniqueIdItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "id": 0,
                  "uniqueIdFieldNames": "GLOBALID",
                  "uniqueIds": [
                    null,
                    "alpha",
                    null,
                    "beta"
                  ]
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.QueryUniqueIdsAsync(
            CreateRequest(),
            cancellationToken);

        var layer = Assert.Single(result.Layers);

        Assert.Equal(0, layer.LayerId);
        Assert.Equal(["GLOBALID"], layer.UniqueIdFieldNames);

        Assert.Collection(
            layer.UniqueIds,
            uniqueId => Assert.Equal("alpha", uniqueId.SingleValue),
            uniqueId => Assert.Equal("beta", uniqueId.SingleValue));
    }

    [Fact]
    public async Task QueryAsync_Throws_WhenSqlFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryAsync(
                CreateRequest() with {
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken));

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task QueryAsync_Throws_WhenMaxAllowableOffsetIsNotFinite(
    double maxAllowableOffset) {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryAsync(
                CreateRequest() with {
                    MaxAllowableOffset = maxAllowableOffset
                },
                cancellationToken));

        Assert.Contains("MaxAllowableOffset", exception.Message, StringComparison.Ordinal);
        Assert.Contains("finite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_ThrowsFeatureServiceException_WhenLayerIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "objectIdFieldName": "OBJECTID",
                  "features": []
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryAsync(CreateRequest(), cancellationToken));

        Assert.Contains("service query", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("layer", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryCountAsync_ThrowsFeatureServiceException_WhenLayerIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "id": -1,
                  "count": 42
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryCountAsync(CreateRequest(), cancellationToken));

        Assert.Contains("service query", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_ThrowsFeatureServiceException_WhenLayerIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [10]
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryObjectIdsAsync(CreateRequest(), cancellationToken));

        Assert.Contains("service query", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryUniqueIdsAsync_ThrowsFeatureServiceException_WhenLayerIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse();
            }

            if (IsServiceQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "layers": [
                {
                  "id": -1,
                  "uniqueIdFieldNames": "GLOBALID",
                  "uniqueIds": ["alpha"]
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.QueryUniqueIdsAsync(CreateRequest(), cancellationToken));

        Assert.Contains("service query", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceQueryRequest CreateRequest() {
        return new FeatureServiceQueryRequest {
            LayerDefinitions = [
                new FeatureServiceLayerQueryDefinition {
                    LayerId = 0,
                    Where = "1=1",
                    OutFields = ["OBJECTID", "NAME"]
                }
            ],
            ReturnGeometry = false
        };
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsServiceQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Query"
        }
        """);
    }
}