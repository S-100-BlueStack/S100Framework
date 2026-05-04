using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientValidateSqlTests
{
    [Fact]
    public async Task ValidateSqlAsync_SendsSqlAndSqlType() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: true);
            }

            if (IsValidateSqlRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "isValidSQL": true
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.ValidateSqlAsync(
            new ValidateSqlRequest {
                Sql = "POPULATION > 300000",
                SqlType = ValidateSqlType.Where
            },
            cancellationToken);

        Assert.True(result.IsValidSql);
        Assert.Empty(result.ValidationErrors);

        Assert.Equal(2, requestUris.Count);

        var query = ParseQuery(requestUris[1]);

        Assert.Equal("json", query["f"]);
        Assert.Equal("POPULATION > 300000", query["sql"]);
        Assert.Equal("where", query["sqlType"]);
    }

    [Fact]
    public async Task ValidateSqlAsync_MapsValidationErrors() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: true);
            }

            if (IsValidateSqlRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "isValidSQL": false,
                  "validationErrors": [
                    {
                      "errorCode": 3008,
                      "description": "Invalid field name [UNKNOWN_FIELD]"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var result = await layerClient.ValidateSqlAsync(
            new ValidateSqlRequest {
                Sql = "UNKNOWN_FIELD = 1",
                SqlType = ValidateSqlType.Where
            },
            cancellationToken);

        Assert.False(result.IsValidSql);

        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal(3008, error.ErrorCode);
        Assert.Equal("Invalid field name [UNKNOWN_FIELD]", error.Description);
    }

    [Fact]
    public async Task ValidateSqlAsync_IgnoresNullValidationErrorItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: true);
            }

            if (IsValidateSqlRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "isValidSQL": false,
                  "validationErrors": [
                    null,
                    {
                      "errorCode": 3002,
                      "description": "Sql expression syntax error."
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).ValidateSqlAsync(
            new ValidateSqlRequest {
                Sql = "POPULATION >",
                SqlType = ValidateSqlType.Where
            },
            cancellationToken);

        var error = Assert.Single(result.ValidationErrors);
        Assert.Equal(3002, error.ErrorCode);
    }

    [Fact]
    public async Task ValidateSqlAsync_Throws_WhenSqlIsEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.ValidateSqlAsync(
                new ValidateSqlRequest {
                    Sql = " "
                },
                cancellationToken));

        Assert.Contains("Sql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSqlAsync_Throws_WhenSqlTypeIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).ValidateSqlAsync(
                new ValidateSqlRequest {
                    Sql = "POPULATION > 300000",
                    SqlType = (ValidateSqlType)999
                },
                cancellationToken));

        Assert.Contains("SqlType", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateSqlAsync_Throws_WhenLayerDoesNotAdvertiseValidateSqlSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var validateSqlWasCalled = false;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: false);
            }

            if (IsValidateSqlRequest(request)) {
                validateSqlWasCalled = true;
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).ValidateSqlAsync(
                new ValidateSqlRequest {
                    Sql = "POPULATION > 300000",
                    SqlType = ValidateSqlType.Where
                },
                cancellationToken));

        Assert.Contains("validateSQL", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(validateSqlWasCalled);
    }

    [Fact]
    public async Task ValidateSqlAsync_Throws_WhenServerOmitsIsValidSql() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: true);
            }

            if (IsValidateSqlRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "validationErrors": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).ValidateSqlAsync(
                new ValidateSqlRequest {
                    Sql = "POPULATION > 300000",
                    SqlType = ValidateSqlType.Where
                },
                cancellationToken));

        Assert.Contains("isValidSQL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsValidateSqlCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(supportsValidateSql: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsValidateSql);
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

    private static bool IsValidateSqlRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/validateSQL",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsValidateSql) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "supportsValidateSQL": {{supportsValidateSql.ToString().ToLowerInvariant()}},
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "POPULATION", "type": "esriFieldTypeInteger", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}