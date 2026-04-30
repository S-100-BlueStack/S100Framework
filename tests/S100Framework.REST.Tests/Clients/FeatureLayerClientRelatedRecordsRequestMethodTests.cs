using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientRelatedRecordsRequestMethodTests
{
    [Fact]
    public async Task QueryRelatedRecordsAsync_UsesGet_WhenPreferenceIsGet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Get,
            autoPostQueryLengthThreshold: 1,
            request => {
                if (IsRelatedRecordsRequest(request)) {
                    relatedRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "relatedRecordGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1
            },
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(relatedRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("json", query["f"]);
        Assert.Equal("100", query["objectIds"]);
        Assert.Equal("1", query["relationshipId"]);
        Assert.Equal("*", query["outFields"]);
        Assert.Equal("true", query["returnGeometry"]);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

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

        var result = await client.GetLayerClient(0).QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200],
                RelationshipId = 1,
                OutFields = ["OBJECTID", "NAME"],
                DefinitionExpression = "STATUS = 'Active'",
                ReturnGeometry = false
            },
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(relatedRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.NotNull(request.Content);
        Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType?.MediaType);
        Assert.Equal("json", form["f"]);
        Assert.Equal("100,200", form["objectIds"]);
        Assert.Equal("1", form["relationshipId"]);
        Assert.Equal("OBJECTID,NAME", form["outFields"]);
        Assert.Equal("STATUS = 'Active'", form["definitionExpression"]);
        Assert.Equal("false", form["returnGeometry"]);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_UsesPostForm_WhenAutoThresholdIsExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 150,
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

        var result = await client.GetLayerClient(0).QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = Enumerable.Range(1, 100).Select(static value => (long)value).ToArray(),
                RelationshipId = 1,
                DefinitionExpression = $"NAME IN ({string.Join(",", Enumerable.Range(0, 50).Select(value => $"'{value:D3}'"))})"
            },
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(relatedRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.StartsWith("1,2,3", form["objectIds"], StringComparison.Ordinal);
        Assert.Equal("1", form["relationshipId"]);
        Assert.StartsWith("NAME IN", form["definitionExpression"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_UsesGet_WhenAutoThresholdIsNotExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var relatedRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsRelatedRecordsRequest(request)) {
                    relatedRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "relatedRecordGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                DefinitionExpression = "STATUS = 'Active'"
            },
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(relatedRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("100", query["objectIds"]);
        Assert.Equal("1", query["relationshipId"]);
        Assert.Equal("STATUS = 'Active'", query["definitionExpression"]);
    }

    [Fact]
    public async Task QueryRelatedRecordCountsAsync_UsesPostForm_WhenPreferenceIsPost() {
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

        var result = await client.GetLayerClient(0).QueryRelatedRecordCountsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100],
                RelationshipId = 1,
                OrderBy = "NAME"
            },
            cancellationToken);

        var group = Assert.Single(result);
        Assert.Equal(100, group.SourceObjectId);
        Assert.Equal(2, group.Count);

        var request = Assert.Single(relatedRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.Equal("100", form["objectIds"]);
        Assert.Equal("1", form["relationshipId"]);
        Assert.Equal("NAME", form["orderByFields"]);
        Assert.Equal("true", form["returnCountOnly"]);
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

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormValue(parts[0]),
                parts => parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty,
                StringComparer.Ordinal);
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