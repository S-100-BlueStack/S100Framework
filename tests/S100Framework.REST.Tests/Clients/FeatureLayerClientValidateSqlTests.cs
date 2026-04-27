using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
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

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/validateSQL",
                StringComparison.OrdinalIgnoreCase)) {
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

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(requestUri);

        Assert.Equal("json", query["f"]);
        Assert.Equal("POPULATION > 300000", query["sql"]);
        Assert.Equal("where", query["sqlType"]);
    }

    [Fact]
    public async Task ValidateSqlAsync_MapsValidationErrors() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/validateSQL",
                StringComparison.OrdinalIgnoreCase)) {
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

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
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