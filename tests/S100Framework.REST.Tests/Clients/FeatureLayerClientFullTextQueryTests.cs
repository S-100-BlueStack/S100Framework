using System.Net.Http;
using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientFullTextQueryTests
{
    [Fact]
    public async Task GetSchemaAsync_SetsSupportsFullTextSearch_WhenMetadataExposesCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsPagination: true,
                    supportsReturningGeometryEnvelope: false,
                    supportsFullTextSearch: true);
            }

            throw new InvalidOperationException("Unexpected request.");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsFullTextSearch);
    }

    [Fact]
    public async Task QueryCountAsync_IncludesSerializedFullTextExpression_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 7
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var count = await layerClient.QueryCountAsync(
            new FeatureQuery {
                FullText =
                [
                    new FeatureQueryFullTextExpression
                    {
                        OnFields = ["NAME", "STATE_NAME"],
                        SearchTerm = "broken pipe",
                        SearchType = FeatureQueryFullTextSearchType.Simple,
                        Operator = FeatureQueryFullTextOperator.Or
                    }
                ]
            },
            cancellationToken);

        Assert.Equal(7, count);

        var queryRequest = Assert.Single(requestUris);
        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("returnCountOnly=true", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("fullText=[{", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"""onFields"":[""NAME"",""STATE_NAME""]", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"""searchTerm"":""broken pipe""", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"""searchType"":""simple""", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains(@"""operator"":""or""", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryObjectIdsAsync_IncludesMixedFullTextExpressions_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase)) {
                return StubHttpMessageHandler.Json("""
            {
              "objectIdFieldName": "OBJECTID",
              "objectIds": [1, 2, 3]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = client.GetLayerClient(0);

        var objectIds = await layerClient.QueryObjectIdsAsync(
            new FeatureQuery {
                FullText =
                [
                    new FeatureQueryFullTextExpression
                {
                    OnFields = ["NOTES"],
                    SearchTerm = "broken",
                    SearchOperator = FeatureQueryFullTextSearchOperator.Or
                },
                new FeatureQueryFullTextExpression
                {
                    SqlExpression = "PIPE_LENGTH > 10"
                }
                ]
            },
            cancellationToken);

        Assert.Equal([1L, 2L, 3L], objectIds);

        var queryRequest = Assert.Single(requestUris);
        var uri = new Uri(queryRequest);

        var fullTextParameter = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Single(part => part.StartsWith("fullText=", StringComparison.Ordinal))
            .Substring("fullText=".Length);

        var fullTextJson = Uri.UnescapeDataString(fullTextParameter);

        using var document = JsonDocument.Parse(fullTextJson);
        var expressions = document.RootElement;

        Assert.Equal(JsonValueKind.Array, expressions.ValueKind);
        Assert.Equal(2, expressions.GetArrayLength());

        var firstExpression = expressions[0];
        Assert.Equal("broken", firstExpression.GetProperty("searchTerm").GetString());
        Assert.Equal("or", firstExpression.GetProperty("searchOperator").GetString());

        var onFields = firstExpression.GetProperty("onFields");
        Assert.Equal(JsonValueKind.Array, onFields.ValueKind);
        Assert.Equal("NOTES", onFields[0].GetString());

        var secondExpression = expressions[1];
        Assert.Equal("PIPE_LENGTH > 10", secondExpression.GetProperty("sqlExpression").GetString());
    }

    [Fact]
    public async Task QueryCountAsync_Throws_WhenFullTextExpressionDoesNotContainSearchOrSqlExpression() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));
        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryCountAsync(
                new FeatureQuery {
                    FullText =
                    [
                        new FeatureQueryFullTextExpression
                        {
                            OnFields = ["NAME"]
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("FullText", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SqlExpression", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsPagination,
        bool supportsReturningGeometryEnvelope,
        bool supportsFullTextSearch) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPolygon",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": {{supportsPagination.ToString().ToLowerInvariant()}},
            "supportsReturningGeometryEnvelope": {{supportsReturningGeometryEnvelope.ToString().ToLowerInvariant()}},
            "supportsFullTextSearch": {{supportsFullTextSearch.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "NAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "STATE_NAME", "type": "esriFieldTypeString", "nullable": true },
            { "name": "NOTES", "type": "esriFieldTypeString", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}