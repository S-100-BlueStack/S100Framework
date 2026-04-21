using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;
using S100Framework.REST.Exceptions;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientApplyEditsTests
{
    [Fact]
    public async Task ApplyEditsAsync_PostsServiceLevelMultiLayerEdits() {
        HttpMethod? method = null;
        string? requestUri = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            method = request.Method;
            requestUri = request.RequestUri!.AbsoluteUri;
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            [
              {
                "id": 0,
                "addResults": [
                  { "success": true, "objectId": 101 }
                ],
                "updateResults": [],
                "deleteResults": []
              },
              {
                "id": 1,
                "addResults": [],
                "updateResults": [],
                "deleteResults": [
                  { "success": true, "objectId": 202 }
                ]
              }
            ]
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.ApplyEditsAsync(
            new FeatureServiceEdits {
                Layers =
                [
                    new ServiceLayerEdits
                    {
                        LayerId = 0,
                        Adds =
                        [
                            new EditableFeature(
                                geometryFactory.CreatePoint(new Coordinate(10, 20)),
                                new Dictionary<string, object?>
                                {
                                    ["NAME"] = "Added feature"
                                })
                        ]
                    },
                    new ServiceLayerEdits
                    {
                        LayerId = 1,
                        DeleteObjectIds = [202]
                    }
                ]
            });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://example.test/arcgis/rest/services/Test/FeatureServer/applyEdits", requestUri);
        Assert.NotNull(requestBody);

        var decodedBody = Uri.UnescapeDataString(requestBody!);

        Assert.Contains("f=json", decodedBody);
        Assert.Contains("rollbackOnFailure=true", decodedBody);
        Assert.Contains("useGlobalIds=false", decodedBody);
        Assert.Contains("\"id\":0", decodedBody);
        Assert.Contains("\"id\":1", decodedBody);
        Assert.Contains("\"adds\"", decodedBody);
        Assert.Contains("\"deletes\":[202]", decodedBody);

        Assert.Equal(2, result.LayerResults.Count);
        Assert.Equal(0, result.LayerResults[0].LayerId);
        Assert.Single(result.LayerResults[0].AddResults);
        Assert.Equal(101, result.LayerResults[0].AddResults[0].ObjectId);

        Assert.Equal(1, result.LayerResults[1].LayerId);
        Assert.Single(result.LayerResults[1].DeleteResults);
        Assert.Equal(202, result.LayerResults[1].DeleteResults[0].ObjectId);
    }

    [Fact]
    public async Task ApplyEditsAsync_UsesGlobalIdsForDeletes_WhenEnabled() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            [
              {
                "id": 0,
                "addResults": [],
                "updateResults": [],
                "deleteResults": [
                  { "success": true, "globalId": "{ABC}" }
                ]
              }
            ]
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.ApplyEditsAsync(
            new FeatureServiceEdits {
                UseGlobalIds = true,
                Layers =
                [
                    new ServiceLayerEdits
                    {
                        LayerId = 0,
                        DeleteGlobalIds = ["{ABC}"]
                    }
                ]
            });

        var decodedBody = Uri.UnescapeDataString(requestBody!);

        Assert.Contains("useGlobalIds=true", decodedBody);
        Assert.Contains("\"deletes\":[\"{ABC}\"]", decodedBody);

        Assert.Single(result.LayerResults);
        Assert.Single(result.LayerResults[0].DeleteResults);
        Assert.Equal("{ABC}", result.LayerResults[0].DeleteResults[0].GlobalId);
    }

    [Fact]
    public async Task ApplyEditsAsync_Throws_WhenDeleteObjectIdsAreUsedWithGlobalIds() {
        var client = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ApplyEditsAsync(
                new FeatureServiceEdits {
                    UseGlobalIds = true,
                    Layers =
                    [
                        new ServiceLayerEdits
                        {
                            LayerId = 0,
                            DeleteObjectIds = [1]
                        }
                    ]
                }));

        Assert.Contains("DeleteObjectIds", exception.Message);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_PostsAsyncFlagAndReturnsStatusUrl_WhenServiceSupportsAsyncApplyEdits() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return StubHttpMessageHandler.Json(CreateServiceMetadataResponse(supportsAsyncApplyEdits: true));
            }

            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
        {
          "statusUrl": "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status"
        }
        """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var submission = await client.SubmitApplyEditsAsync(
            new FeatureServiceEdits {
                Layers =
                [
                    new ServiceLayerEdits
                {
                    LayerId = 0,
                    DeleteObjectIds = [202]
                }
                ]
            });

        var decodedBody = Uri.UnescapeDataString(requestBody!);

        Assert.True(submission.IsPending);
        Assert.Equal(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status"),
            submission.StatusUrl);
        Assert.Contains("async=true", decodedBody);
        Assert.Contains("\"id\":0", decodedBody);
        Assert.Contains("\"deletes\":[202]", decodedBody);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_Throws_WhenServiceDoesNotSupportAsyncApplyEdits() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json(CreateServiceMetadataResponse(supportsAsyncApplyEdits: false)));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.SubmitApplyEditsAsync(
                new FeatureServiceEdits {
                    Layers =
                    [
                        new ServiceLayerEdits
                    {
                        LayerId = 0,
                        DeleteObjectIds = [202]
                    }
                    ]
                }));

        Assert.Contains("does not support asynchronous applyEdits", exception.Message);
    }

    [Fact]
    public async Task GetApplyEditsStatusAsync_MapsAsyncStatusPayload() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        {
          "status": "COMPLETED",
          "resultUrl": "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json",
          "submissionTime": 1000,
          "lastUpdatedTime": 2000
        }
        """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var status = await client.GetApplyEditsStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status"));

        Assert.Equal("COMPLETED", status.Status);
        Assert.True(status.IsCompleted);
        Assert.Equal(
            new Uri("https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json"),
            status.ResultUrl);
        Assert.Equal(1000, status.SubmissionTime);
        Assert.Equal(2000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task GetApplyEditsResultAsync_MapsServiceLevelAsyncResult() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
        [
          {
            "id": 0,
            "addResults": [
              { "success": true, "objectId": 101 }
            ],
            "updateResults": [],
            "deleteResults": []
          },
          {
            "id": 1,
            "addResults": [],
            "updateResults": [],
            "deleteResults": [
              { "success": true, "objectId": 202 }
            ]
          }
        ]
        """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.GetApplyEditsResultAsync(
            new Uri("https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json"));

        Assert.Equal(2, result.LayerResults.Count);
        Assert.Equal(0, result.LayerResults[0].LayerId);
        Assert.Equal(101, result.LayerResults[0].AddResults[0].ObjectId);
        Assert.Equal(1, result.LayerResults[1].LayerId);
        Assert.Equal(202, result.LayerResults[1].DeleteResults[0].ObjectId);
    }

    private static string CreateServiceMetadataResponse(bool supportsAsyncApplyEdits) {
        return $$"""
    {
      "layers": [],
      "tables": [],
      "capabilities": "Create,Update,Delete,Editing,Query",
      "advancedEditingCapabilities": {
        "supportsAsyncApplyEdits": {{supportsAsyncApplyEdits.ToString().ToLowerInvariant()}}
      }
    }
    """;
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_ReturnsImmediateResult_WhenSubmitReturnsResult() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return StubHttpMessageHandler.Json(CreateServiceMetadataResponse(supportsAsyncApplyEdits: true));
            }

            return StubHttpMessageHandler.Json("""
        [
          {
            "id": 0,
            "addResults": [
              { "success": true, "objectId": 101 }
            ],
            "updateResults": [],
            "deleteResults": []
          }
        ]
        """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.WaitForApplyEditsCompletionAsync(
            new FeatureServiceEdits {
                Layers =
                [
                    new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteObjectIds = [202]
                }
                ]
            },
            new ApplyEditsWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.Single(result.LayerResults);
        Assert.Equal(0, result.LayerResults[0].LayerId);
        Assert.Equal(101, result.LayerResults[0].AddResults[0].ObjectId);
        Assert.DoesNotContain(
            requestUris,
            uri => uri.Contains("/jobs/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_PollsStatusUntilCompleted_AndReturnsResult() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";
        const string resultUrl = "https://example.test/arcgis/rest/directories/arcgisoutput/Test_MapServer/applyEdits-123.json";

        var statusCalls = 0;

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                statusCalls++;

                return statusCalls == 1
                    ? StubHttpMessageHandler.Json("""
                {
                  "status": "InProgress"
                }
                """)
                    : StubHttpMessageHandler.Json($$"""
                {
                  "status": "Completed",
                  "resultUrl": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return StubHttpMessageHandler.Json("""
            [
              {
                "id": 0,
                "addResults": [
                  { "success": true, "objectId": 101 }
                ],
                "updateResults": [],
                "deleteResults": []
              }
            ]
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.WaitForApplyEditsCompletionAsync(
            new Uri(statusUrl),
            new ApplyEditsWaitOptions {
                PollInterval = TimeSpan.Zero,
                Timeout = TimeSpan.FromSeconds(1)
            });

        Assert.Equal(2, statusCalls);
        Assert.Single(result.LayerResults);
        Assert.Equal(101, result.LayerResults[0].AddResults[0].ObjectId);
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_Throws_WhenJobEndsInFailedStatus() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "status": "Failed"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.WaitForApplyEditsCompletionAsync(
                new Uri(statusUrl),
                new ApplyEditsWaitOptions {
                    PollInterval = TimeSpan.Zero,
                    Timeout = TimeSpan.FromSeconds(1)
                }));

        Assert.Contains("terminal status 'Failed'", exception.Message);
    }

    [Fact]
    public async Task SubmitApplyEditsAsync_Throws_WhenServerReturnsEmptyStatusUrl() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return StubHttpMessageHandler.Json(CreateServiceMetadataResponse(supportsAsyncApplyEdits: true));
            }

            return StubHttpMessageHandler.Json("""
        {
          "statusUrl": ""
        }
        """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitApplyEditsAsync(
                new FeatureServiceEdits {
                    Layers =
                    [
                        new ServiceLayerEdits {
                        LayerId = 0,
                        DeleteObjectIds = [202]
                    }
                    ]
                }));

        Assert.Contains("empty statusUrl", exception.Message);
    }

    [Fact]
    public async Task WaitForApplyEditsCompletionAsync_Throws_WhenCompletedStatusHasNoResultUrl() {
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/applyEdits-123/status";

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json("""
            {
              "status": "Completed"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.WaitForApplyEditsCompletionAsync(
                new Uri(statusUrl),
                new ApplyEditsWaitOptions {
                    PollInterval = TimeSpan.Zero,
                    Timeout = TimeSpan.FromSeconds(1)
                }));

        Assert.Contains("completed without a resultUrl", exception.Message);
    }
}