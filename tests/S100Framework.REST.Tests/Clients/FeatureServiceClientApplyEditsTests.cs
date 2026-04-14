using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

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
}