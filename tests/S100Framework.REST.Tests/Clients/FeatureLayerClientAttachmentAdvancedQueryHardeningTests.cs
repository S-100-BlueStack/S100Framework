using System.Net.Http;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAttachmentAdvancedQueryHardeningTests
{
    [Fact]
    public async Task QueryAttachmentsAsync_MapsMissingAttachmentGroupsToEmptyResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: false,
                    supportsOrderByFields: false);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100]
            },
            cancellationToken);

        Assert.Empty(groups);

        Assert.Contains(
            requestUris,
            uri => uri.EndsWith("/FeatureServer/0?f=json", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryAttachments?", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QueryAttachmentsAsync_MapsMissingAndNullAttachmentInfosToEmptyAttachmentLists() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: false,
                    supportsOrderByFields: false);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": [
                    {
                      "parentObjectId": 100,
                      "parentGlobalId": "{parent-global-id-a}"
                    },
                    {
                      "parentObjectId": 200,
                      "parentGlobalId": "{parent-global-id-b}",
                      "attachmentInfos": null
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100, 200]
            },
            cancellationToken);

        Assert.Equal(2, groups.Count);

        Assert.Equal(100, groups[0].SourceObjectId);
        Assert.Equal("{parent-global-id-a}", groups[0].SourceGlobalId);
        Assert.Empty(groups[0].Attachments);

        Assert.Equal(200, groups[1].SourceObjectId);
        Assert.Equal("{parent-global-id-b}", groups[1].SourceGlobalId);
        Assert.Empty(groups[1].Attachments);
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_MapsMissingAttachmentGroupsToEmptyResult() {
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

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
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

        Assert.Empty(counts);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/queryAttachments?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(queryRequest));

        Assert.Equal("true", query["returnCountOnly"]);
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_MapsNullCountToZero() {
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
                      "parentGlobalId": "{parent-global-id}",
                      "count": null
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
    public async Task QueryAttachmentCountsAsync_MapsGlobalIdOnlyCountGroups() {
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
                      "parentGlobalId": "{parent-global-id}",
                      "count": 5
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var counts = await client.GetLayerClient(0).QueryAttachmentCountsAsync(
            new AttachmentQuery {
                GlobalIds = ["{parent-global-id}"]
            },
            cancellationToken);

        var count = Assert.Single(counts);

        Assert.Null(count.SourceObjectId);
        Assert.Equal("{parent-global-id}", count.SourceGlobalId);
        Assert.Equal(5, count.Count);
    }

    [Fact]
    public async Task QueryAttachmentsAsync_DoesNotRequireCountOnlyCapability_ForNormalQueries() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse(
                    supportsCountOnly: false,
                    supportsOrderByFields: false);
            }

            if (IsAttachmentQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "attachmentGroups": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var groups = await client.GetLayerClient(0).QueryAttachmentsAsync(
            new AttachmentQuery {
                ObjectIds = [100]
            },
            cancellationToken);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task QueryAttachmentCountsAsync_Throws_WhenOrderByFieldsAreProvidedWithoutCapability() {
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

            throw new InvalidOperationException("Attachment count query should not be executed.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).QueryAttachmentCountsAsync(
                new AttachmentQuery {
                    ObjectIds = [100],
                    OrderByFields = ["size DESC"]
                },
                cancellationToken));

        Assert.Contains("order-by", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public void AttachmentQuery_AllowsDefinitionExpressionWithoutObjectIdsOrGlobalIds() {
        var query = new AttachmentQuery {
            DefinitionExpression = "STATUS = 'Active'"
        };

        query.Validate();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AttachmentQuery_Throws_WhenGlobalIdsContainEmptyValues(string globalId) {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                GlobalIds = [globalId]
            }.Validate());

        Assert.Contains("GlobalIds", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AttachmentQuery_Throws_WhenOrderByFieldsContainEmptyValues(string orderByField) {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                OrderByFields = [orderByField]
            }.Validate());

        Assert.Contains("OrderByFields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachmentQuery_Throws_WhenMinimumSizeIsGreaterThanMaximumSize() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                MinimumSizeBytes = 4096,
                MaximumSizeBytes = 1024
            }.Validate());

        Assert.Contains("MinimumSizeBytes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MaximumSizeBytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachmentQuery_Throws_WhenMinimumSizeIsNegative() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                MinimumSizeBytes = -1L
            }.Validate());

        Assert.Contains("MinimumSizeBytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachmentQuery_Throws_WhenMaximumSizeIsNegative() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AttachmentQuery {
                ObjectIds = [100],
                MaximumSizeBytes = -1L
            }.Validate());

        Assert.Contains("MaximumSizeBytes", exception.Message, StringComparison.Ordinal);
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

    private static bool IsAttachmentQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/queryAttachments",
            StringComparison.OrdinalIgnoreCase) == true;
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