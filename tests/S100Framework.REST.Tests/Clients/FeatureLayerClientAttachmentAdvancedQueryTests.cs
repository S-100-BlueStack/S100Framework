using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentAdvancedQueryTests
{
    [Fact]
    public async Task QueryAttachmentsAsync_SendsGlobalIdsPaginationAndOrderByFields() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: true,
                    supportsOrderByFields: true);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": [
                    {
                      "parentObjectId": 100,
                      "parentGlobalId": "{parent-global-id}",
                      "attachmentInfos": [
                        {
                          "id": 1,
                          "name": "inspection.pdf",
                          "contentType": "application/pdf",
                          "size": 2048,
                          "globalId": "{attachment-global-id}"
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                GlobalIds = ["{parent-global-id}"],
                ResultOffset = 5,
                ResultRecordCount = 10,
                OrderByFields = ["size DESC", "name ASC"],
                ReturnMetadata = true
            },
            cancellationToken);

        var group = Assert.Single(groups);
        Assert.Equal(100, group.SourceObjectId);
        Assert.Equal("{parent-global-id}", group.SourceGlobalId);

        var attachment = Assert.Single(group.Attachments);
        Assert.Equal(1, attachment.AttachmentId);
        Assert.Equal("inspection.pdf", attachment.Name);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(2048, attachment.Size);
        Assert.Equal("{attachment-global-id}", attachment.GlobalId);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryAttachments?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("{parent-global-id}", query["globalIds"]);
        Assert.Equal("5", query["resultOffset"]);
        Assert.Equal("10", query["resultRecordCount"]);
        Assert.Equal("size DESC,name ASC", query["orderByFields"]);
        Assert.Equal("true", query["returnMetadata"]);
        Assert.False(query.ContainsKey("objectIds"));
        Assert.False(query.ContainsKey("returnCountOnly"));
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_SendsReturnCountOnlyAndMapsCounts() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: true,
                    supportsOrderByFields: true);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": [
                    {
                      "parentObjectId": 100,
                      "parentGlobalId": "{parent-global-id-a}",
                      "count": 2
                    },
                    {
                      "parentObjectId": 200,
                      "parentGlobalId": "{parent-global-id-b}",
                      "count": 0
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await client.GetLayerClient(0).QueryAttachmentCountsAsync(
            new AttachmentQuery {
                DefinitionExpression = "STATUS = 'Active'",
                OrderByFields = ["size DESC"]
            },
            cancellationToken);

        Assert.Equal(2, counts.Count);

        Assert.Equal(100, counts[0].SourceObjectId);
        Assert.Equal("{parent-global-id-a}", counts[0].SourceGlobalId);
        Assert.Equal(2, counts[0].Count);

        Assert.Equal(200, counts[1].SourceObjectId);
        Assert.Equal("{parent-global-id-b}", counts[1].SourceGlobalId);
        Assert.Equal(0, counts[1].Count);

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryAttachments?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("STATUS = 'Active'", query["definitionExpression"]);
        Assert.Equal("size DESC", query["orderByFields"]);
        Assert.Equal("true", query["returnCountOnly"]);
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_MapsMissingCountToZero() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: true,
                    supportsOrderByFields: false);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": [
                    {
                      "parentObjectId": 100,
                      "parentGlobalId": "{parent-global-id}"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await client.GetLayerClient(0).QueryAttachmentCountsAsync(
            new AttachmentQuery {
                ObjectIds = [100]
            },
            cancellationToken);

        var count = Assert.Single(counts);

        Assert.Equal(100, count.SourceObjectId);
        Assert.Equal("{parent-global-id}", count.SourceGlobalId);
        Assert.Equal(0, count.Count);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_Throws_WhenOrderByFieldsAreProvidedWithoutCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: true,
                    supportsOrderByFields: false);
            }

            throw new InvalidOperationException("Attachment query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryAttachmentsAsync(
                new AttachmentQuery {
                    ObjectIds = [100],
                    OrderByFields = ["size DESC"]
                },
                cancellationToken));

        Assert.Contains("order-by", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_Throws_WhenCountOnlyIsNotSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: false,
                    supportsOrderByFields: true);
            }

            throw new InvalidOperationException("Attachment count query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryAttachmentCountsAsync(
                new AttachmentQuery {
                    ObjectIds = [100]
                },
                cancellationToken));

        Assert.Contains("attachment count", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_PostFormPreservesGlobalIdsPaginationAndOrderByFields() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse(
                        supportsCountOnly: true,
                        supportsOrderByFields: true);
                }

                if (IsAttachmentQueryRequest(request)) {
                    requestBodies.Add(ReadRequestBody(request));

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
                GlobalIds = ["{parent-global-id-a}", "{parent-global-id-b}"],
                ResultOffset = 10,
                ResultRecordCount = 25,
                OrderByFields = ["size DESC", "name ASC"]
            },
            cancellationToken);

        Assert.Empty(groups);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("{parent-global-id-a},{parent-global-id-b}", form["globalIds"]);
        Assert.Equal("10", form["resultOffset"]);
        Assert.Equal("25", form["resultRecordCount"]);
        Assert.Equal("size DESC,name ASC", form["orderByFields"]);
        Assert.False(form.ContainsKey("objectIds"));
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_PostFormPreservesReturnCountOnly() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(
            QueryRequestMethodPreference.Post,
            request => {
                if (IsLayerMetadataRequest(request)) {
                    return CreateLayerMetadataResponse(
                        supportsCountOnly: true,
                        supportsOrderByFields: false);
                }

                if (IsAttachmentQueryRequest(request)) {
                    requestBodies.Add(ReadRequestBody(request));

                    return StubHttpMessageHandler.Json("""
                    {
                      "attachmentGroups": [
                        {
                          "parentObjectId": 100,
                          "count": 3
                        }
                      ]
                    }
                    """);
                }

                throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
            });

        var counts = await client.GetLayerClient(0).QueryAttachmentCountsAsync(
            new AttachmentQuery {
                ObjectIds = [100]
            },
            cancellationToken);

        Assert.Equal(3, Assert.Single(counts).Count);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("100", form["objectIds"]);
        Assert.Equal("true", form["returnCountOnly"]);
    }

    [Fact]
    public void AttachmentQuery_Throws_WhenObjectIdsAndGlobalIdsAreCombined() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                GlobalIds = ["{parent-global-id}"]
            }.Validate());

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GlobalIds", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, null, "ResultOffset")]
    [InlineData(null, 0, "ResultRecordCount")]
    [InlineData(null, -1, "ResultRecordCount")]
    public void AttachmentQuery_Throws_WhenPaginationValuesAreInvalid(
        int? resultOffset,
        int? resultRecordCount,
        string expectedMessagePart) {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                ResultOffset = resultOffset,
                ResultRecordCount = resultRecordCount
            }.Validate());

        Assert.Contains(expectedMessagePart, exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return CreateClient(QueryRequestMethodPreference.Get, handler);
    }

    private static FeatureServiceClient CreateClient(
        QueryRequestMethodPreference queryRequestMethodPreference,
        Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = queryRequestMethodPreference,
                AutoPostQueryLengthThreshold = 10_000
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

    private static HttpResponseMessage CreateLayerMetadataResponse(
        bool supportsCountOnly,
        bool supportsOrderByFields) {
        return StubHttpMessageHandler.Json($$"""
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
            "supportsPagination": true,
            "supportsQueryAttachmentsCountOnly": {{supportsCountOnly.ToString().ToLowerInvariant()}},
            "supportsQueryAttachmentOrderByFields": {{supportsOrderByFields.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "GLOBALID", "type": "esriFieldTypeGlobalID", "nullable": false },
            { "name": "STATUS", "type": "esriFieldTypeString", "nullable": true }
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