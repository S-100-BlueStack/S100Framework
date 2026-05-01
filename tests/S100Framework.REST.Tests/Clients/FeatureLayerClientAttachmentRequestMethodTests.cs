using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentRequestMethodTests
{
    [Fact]
    public async Task QueryAttachmentsAsync_UsesGet_WhenPreferenceIsGet() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Get,
            autoPostQueryLengthThreshold: 1,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": [
                        {
                          "parentObjectId": 100,
                          "parentGlobalId": "{parent-global-id}",
                          "attachmentInfos": [
                            {
                              "id": 1,
                              "name": "photo.jpg",
                              "contentType": "image/jpeg",
                              "size": 12345,
                              "globalId": "{attachment-global-id}",
                              "url": "https://example.test/photo.jpg"
                            }
                          ]
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100],
                ReturnUrl = true
            },
            cancellationToken);

        var group = Assert.Single(groups);
        Assert.Equal(100, group.SourceObjectId);
        Assert.Equal("{parent-global-id}", group.SourceGlobalId);

        var attachment = Assert.Single(group.Attachments);
        Assert.Equal(1, attachment.AttachmentId);
        Assert.Equal(100, attachment.ParentObjectId);
        Assert.Equal("{parent-global-id}", attachment.ParentGlobalId);
        Assert.Equal("photo.jpg", attachment.Name);
        Assert.Equal("image/jpeg", attachment.ContentType);
        Assert.Equal(12345, attachment.Size);
        Assert.Equal("{attachment-global-id}", attachment.GlobalId);
        Assert.Equal("https://example.test/photo.jpg", attachment.Url);

        var request = Assert.Single(attachmentRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("json", query["f"]);
        Assert.Equal("100", query["objectIds"]);
        Assert.Equal("true", query["returnUrl"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_UsesPostForm_WhenPreferenceIsPost() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100, 200],
                DefinitionExpression = "STATUS = 'Active'",
                AttachmentTypes = ["image/jpeg", "application/pdf"],
                Keywords = ["survey", "inspection"],
                MinimumSizeBytes = 1024,
                MaximumSizeBytes = 4096,
                ReturnUrl = true,
                ReturnMetadata = true
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.NotNull(request.Content);
        Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType?.MediaType);

        Assert.Equal("json", form["f"]);
        Assert.Equal("100,200", form["objectIds"]);
        Assert.Equal("STATUS = 'Active'", form["definitionExpression"]);
        Assert.Equal("image/jpeg,application/pdf", form["attachmentTypes"]);
        Assert.Equal("survey,inspection", form["keywords"]);
        Assert.Equal("1024,4096", form["size"]);
        Assert.Equal("true", form["returnUrl"]);
        Assert.Equal("true", form["returnMetadata"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_UsesPostForm_WhenAutoThresholdIsExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 180,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = Enumerable.Range(1, 100)
                    .Select(static value => (long)value)
                    .ToArray(),
                DefinitionExpression = $"NAME IN ({string.Join(",", Enumerable.Range(0, 50).Select(value => $"'{value:D3}'"))})",
                ReturnMetadata = true
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
        Assert.Equal("json", form["f"]);
        Assert.StartsWith("1,2,3", form["objectIds"], StringComparison.Ordinal);
        Assert.StartsWith("NAME IN", form["definitionExpression"], StringComparison.Ordinal);
        Assert.Equal("true", form["returnMetadata"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_UsesGet_WhenAutoThresholdIsNotExceeded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Auto,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100],
                DefinitionExpression = "STATUS = 'Active'",
                ReturnUrl = true
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Content);
        Assert.Equal("100", query["objectIds"]);
        Assert.Equal("STATUS = 'Active'", query["definitionExpression"]);
        Assert.Equal("true", query["returnUrl"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_PostFormPreservesOpenEndedSizeRange_WhenOnlyMinimumIsProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100],
                MinimumSizeBytes = 1024
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("1024,", form["size"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_PostFormPreservesOpenEndedSizeRange_WhenOnlyMaximumIsProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();
        var formBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);
                    formBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100],
                MaximumSizeBytes = 4096
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var form = ParseFormBody(Assert.Single(formBodies));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(",4096", form["size"]);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_DoesNotSendOptionalFalseFlags_WhenFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var attachmentRequests = new List<HttpRequestMessage>();

        var client = CreateClient(
            QueryRequestMethodPreference.Get,
            autoPostQueryLengthThreshold: 10_000,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse();
                }

                if (IsAttachmentQueryRequest(request)) {
                    attachmentRequests.Add(request);

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": []
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100],
                ReturnUrl = false,
                ReturnMetadata = false
            },
            cancellationToken);

        Assert.Empty(groups);

        var request = Assert.Single(attachmentRequests);
        var query = ParseQuery(request.RequestUri!);

        Assert.False(query.ContainsKey("returnUrl"));
        Assert.False(query.ContainsKey("returnMetadata"));
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

    private static bool IsAttachmentQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryAttachments",
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
          "name": "AttachmentLayer",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "hasAttachments": true,
          "supportsQueryAttachments": true,
          "supportsAttachmentsResizing": true,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "STATUS", "type": "esriFieldTypeString", "nullable": true },
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