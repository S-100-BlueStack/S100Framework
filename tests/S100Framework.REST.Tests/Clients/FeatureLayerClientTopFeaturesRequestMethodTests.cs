using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientTopFeaturesRequestMethodTests
{
    [Fact]
    public async Task QueryTopFeaturesAsync_UsesGet_WhenPreferenceIsGet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Get,
            autoPostQueryLengthThreshold: 1,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "objectIdFieldName": "OBJECTID",
                      "features": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateTopFeaturesQuery(where: "STATUS = 'Active'"),
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(topFeatureRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("json", query["f"]);
        Assert.Equal("STATUS = 'Active'", query["where"]);
        Assert.Equal("true", query["returnGeometry"]);
        Assert.Equal("*", query["outFields"]);
        Assert.True(query.ContainsKey("topFilter"));
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "objectIdFieldName": "OBJECTID",
                      "features": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            new TopFeaturesQuery {
                Where = "STATUS = 'Active'",
                OutFields = ["OBJECTID", "NAME"],
                ReturnGeometry = false,
                ReturnZ = true,
                ReturnM = false,
                OutSrid = 25832,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(topFeatureRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.NotNull(request.Content);
        Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType?.MediaType);

        Assert.Equal("json", form["f"]);
        Assert.Equal("STATUS = 'Active'", form["where"]);
        Assert.Equal("OBJECTID,NAME", form["outFields"]);
        Assert.Equal("false", form["returnGeometry"]);
        Assert.Equal("true", form["returnZ"]);
        Assert.Equal("false", form["returnM"]);
        Assert.Equal("25832", form["outSR"]);
        Assert.Equal("3", form["geometryPrecision"]);
        Assert.Equal("0.5", form["maxAllowableOffset"]);
        Assert.Contains("\"groupByFields\":\"REGION\"", form["topFilter"], StringComparison.Ordinal);
        Assert.Contains("\"orderByFields\":\"SCORE DESC\"", form["topFilter"], StringComparison.Ordinal);
        Assert.Contains("\"topCount\":2", form["topFilter"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_UsesPostForm_WhenAutoThresholdIsExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 180,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "objectIdFieldName": "OBJECTID",
                      "features": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateTopFeaturesQuery(
                where: $"NAME IN ({string.Join(",", Enumerable.Range(0, 50).Select(value => $"'{value:D3}'"))})"),
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(topFeatureRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.StartsWith("NAME IN", form["where"], StringComparison.Ordinal);
        Assert.True(form.ContainsKey("topFilter"));
    }

    [Fact]
    public async Task QueryTopFeaturesAsync_UsesGet_WhenAutoThresholdIsNotExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "objectIdFieldName": "OBJECTID",
                      "features": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryTopFeaturesAsync(
            CreateTopFeaturesQuery(where: "OBJECTID > 0"),
            cancellationToken);

        Assert.Empty(result);

        var request = Assert.Single(topFeatureRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("OBJECTID > 0", query["where"]);
        Assert.True(query.ContainsKey("topFilter"));
    }

    [Fact]
    public async Task QueryTopFeatureObjectIdsAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "objectIdFieldName": "OBJECTID",
                      "objectIds": [10, 20]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var objectIds = await client.GetLayerClient(0).QueryTopFeatureObjectIdsAsync(
            CreateTopFeaturesQuery(where: "STATUS = 'Active'"),
            cancellationToken);

        Assert.Equal([10, 20], objectIds);

        var request = Assert.Single(topFeatureRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.Equal("STATUS = 'Active'", form["where"]);
        Assert.Equal("true", form["returnIdsOnly"]);
        Assert.True(form.ContainsKey("topFilter"));
        Assert.False(form.ContainsKey("returnGeometry"));
        Assert.False(form.ContainsKey("outFields"));
    }

    [Fact]
    public async Task QueryTopFeatureCountAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var topFeatureRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsTopFeaturesRequest(request)) {
                    topFeatureRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "count": 4
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var result = await client.GetLayerClient(0).QueryTopFeatureCountAsync(
            new TopFeaturesQuery {
                Where = "STATUS = 'Active'",
                OutSrid = 25832,
                TopFilter = new TopFeaturesFilter {
                    GroupByFields = ["REGION"],
                    OrderByFields = ["SCORE DESC"],
                    TopCount = 2
                }
            },
            cancellationToken);

        Assert.Equal(4, result.Count);
        Assert.Null(result.Extent);

        var request = Assert.Single(topFeatureRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.Equal("STATUS = 'Active'", form["where"]);
        Assert.Equal("true", form["returnCountOnly"]);
        Assert.Equal("25832", form["outSR"]);
        Assert.True(form.ContainsKey("topFilter"));
        Assert.False(form.ContainsKey("returnIdsOnly"));
        Assert.False(form.ContainsKey("returnGeometry"));
        Assert.False(form.ContainsKey("outFields"));
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

    private static TopFeaturesQuery CreateTopFeaturesQuery(string where) {
        return new TopFeaturesQuery {
            Where = where,
            TopFilter = new TopFeaturesFilter {
                GroupByFields = ["REGION"],
                OrderByFields = ["SCORE DESC"],
                TopCount = 2
            }
        };
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsTopFeaturesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryTopFeatures",
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
          "name": "TopLayer",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
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
          },
          "supportsTopFeaturesQuery": true
        }
        """);
    }
}