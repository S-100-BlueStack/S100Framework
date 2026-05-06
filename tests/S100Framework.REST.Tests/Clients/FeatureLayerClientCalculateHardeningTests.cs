using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientCalculateHardeningTests
{
    [Fact]
    public async Task CalculateAsync_Throws_WhenExpressionsCollectionIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Expressions = null!
                },
                cancellationToken));

        Assert.Contains("expression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenRequestedSqlFormatIsNotAdvertised() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var calculateWasCalled = false;
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse("\"standard\"");
            }

            if (IsCalculateRequest(request)) {
                calculateWasCalled = true;
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Where = "OBJECTID = 1",
                    Expressions = [
                        CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                    ],
                    SqlFormat = FeatureQuerySqlFormat.Native
                },
                cancellationToken));

        Assert.Contains("native", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("standard", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(calculateWasCalled);
    }

    [Fact]
    public async Task SubmitCalculateAsync_Throws_WhenRequestedSqlFormatIsNotAdvertised() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var calculateWasCalled = false;
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    "\"standard\"",
                    supportsAsyncCalculate: true);
            }

            if (IsCalculateRequest(request)) {
                calculateWasCalled = true;
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).SubmitCalculateAsync(
                new CalculateRequest {
                    Where = "OBJECTID = 1",
                    Expressions = [
                        CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                    ],
                    SqlFormat = FeatureQuerySqlFormat.Native
                },
                cancellationToken));

        Assert.Contains("native", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(calculateWasCalled);
    }

    [Fact]
    public async Task CalculateAsync_AllowsRequestedSqlFormat_WhenLayerDoesNotAdvertiseSupportedSqlFormats() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var calculateWasCalled = false;
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportedSqlFormatsJson: null);
            }

            if (IsCalculateRequest(request)) {
                calculateWasCalled = true;

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "updatedFeatureCount": 1
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).CalculateAsync(
            new CalculateRequest {
                Where = "OBJECTID = 1",
                Expressions = [
                    CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                ],
                SqlFormat = FeatureQuerySqlFormat.Native
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.True(calculateWasCalled);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsSupportedSqlFormatsInCalculate() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse("\"standard\", \"native\"");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.Contains(FeatureQuerySqlFormat.Standard, schema.Capabilities.SupportedSqlFormatsInCalculate);
        Assert.Contains(FeatureQuerySqlFormat.Native, schema.Capabilities.SupportedSqlFormatsInCalculate);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsLegacySupportedSqlFormatesInCalculateSpelling() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportedSqlFormatsJson: null,
                    legacySupportedSqlFormatsJson: "\"native\"");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.Contains(FeatureQuerySqlFormat.Native, schema.Capabilities.SupportedSqlFormatsInCalculate);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenSqlFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Where = "OBJECTID = 1",
                    Expressions = [
                        CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                    ],
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken));

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitCalculateAsync_Throws_WhenSqlFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).SubmitCalculateAsync(
                new CalculateRequest {
                    Where = "OBJECTID = 1",
                    Expressions = [
                        CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                    ],
                    SqlFormat = (FeatureQuerySqlFormat)999
                },
                cancellationToken));

        Assert.Contains("SqlFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenExpressionKindIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Where = "OBJECTID = 1",
                    Expressions = [
                        new CalculateExpression {
                        Field = "SCORE",
                        Kind = (CalculateExpressionKind)999,
                        Value = 42
                    }
                    ]
                },
                cancellationToken));

        Assert.Contains("Kind", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsCalculateRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/calculate",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(
        string? supportedSqlFormatsJson,
        bool supportsAsyncCalculate = false,
        string? legacySupportedSqlFormatsJson = null) {
        var advancedEditingCapabilitiesJson = CreateAdvancedEditingCapabilitiesJson(
            supportedSqlFormatsJson,
            legacySupportedSqlFormatsJson);

        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "supportsCalculate": true,
          "supportsAsyncCalculate": {{supportsAsyncCalculate.ToString().ToLowerInvariant()}},
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          }{{advancedEditingCapabilitiesJson}},
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "SCORE", "type": "esriFieldTypeDouble", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }

    private static string CreateAdvancedEditingCapabilitiesJson(
        string? supportedSqlFormatsJson,
        string? legacySupportedSqlFormatsJson) {
        var properties = new List<string>();

        if (supportedSqlFormatsJson is not null) {
            properties.Add($"\"supportedSqlFormatsInCalculate\": [{supportedSqlFormatsJson}]");
        }

        if (legacySupportedSqlFormatsJson is not null) {
            properties.Add($"\"supportedSqlFormatesInCalculate\": [{legacySupportedSqlFormatsJson}]");
        }

        if (properties.Count == 0) {
            return string.Empty;
        }

        return $",\n  \"advancedEditingCapabilities\": {{ {string.Join(", ", properties)} }}";
    }
}