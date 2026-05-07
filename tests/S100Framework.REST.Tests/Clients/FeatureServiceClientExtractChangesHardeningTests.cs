using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using S100Framework.REST.Exceptions;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientExtractChangesHardeningTests
{
    [Fact]
    public async Task ExtractChangesAsync_IgnoresNullLayerServerGensEditsAndFeatureItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (IsLayerMetadataRequest(request, 0)) {
                return CreateLayerMetadataResponse();
            }

            if (IsExtractChangesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerServerGens": [
                    null,
                    {
                      "id": 0,
                      "serverGen": 153025
                    }
                  ],
                  "edits": [
                    null,
                    {
                      "id": 0,
                      "features": {
                        "adds": [
                          null,
                          {
                            "geometry": {
                              "x": 10,
                              "y": 20
                            },
                            "attributes": {
                              "OBJECTID": 10,
                              "NAME": "Added"
                            }
                          }
                        ],
                        "updates": [
                          null
                        ],
                        "deletes": [
                          null
                        ],
                        "deleteIds": [
                          "deleted-feature-id"
                        ]
                      }
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                ServerGens = new ExtractChangesServerGens {
                    SinceServerGen = 1653608093000
                },
                ReturnIdsOnly = false
            },
            cancellationToken);

        var layerServerGen = Assert.Single(result.LayerServerGens);

        Assert.Equal(0, layerServerGen.Id);
        Assert.Equal(153025, layerServerGen.ServerGen);

        var edit = Assert.Single(result.Edits);

        Assert.Equal(0, edit.LayerId);

        var featureChanges = Assert.IsType<ExtractChangesFeatureChanges>(edit.Features);

        var addedFeature = Assert.Single(featureChanges.Adds);

        Assert.Equal(10, addedFeature.ObjectId);
        Assert.Equal("Added", addedFeature.GetRequiredString("NAME"));
        Assert.Empty(featureChanges.Updates);
        Assert.Empty(featureChanges.Deletes);

        var deleteId = Assert.Single(featureChanges.DeleteIds);

        Assert.Equal("deleted-feature-id", deleteId);
    }

    [Fact]
    public async Task ExtractChangesAsync_ReturnsEmptyCollections_WhenResponseArraysAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (IsExtractChangesRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [
                    new ExtractChangesLayerServerGen(0, 1653608093000)
                ],
                ReturnIdsOnly = true
            },
            cancellationToken);

        Assert.Empty(result.LayerServerGens);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public async Task ExtractChangesAsync_ReturnsEmptyCollections_WhenResponseArraysAreNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (IsExtractChangesRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "layerServerGens": null,
                  "edits": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.ExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [
                    new ExtractChangesLayerServerGen(0, 1653608093000)
                ],
                ReturnIdsOnly = true
            },
            cancellationToken);

        Assert.Empty(result.LayerServerGens);
        Assert.Empty(result.Edits);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenLayersContainDuplicateValues() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0, 0],
                    ServerGens = new ExtractChangesServerGens {
                        SinceServerGen = 1653608093000
                    }
                },
                cancellationToken));

        Assert.Contains("Layers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenLayerServerGensContainDuplicateLayerIds() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [
                        new ExtractChangesLayerServerGen(0, 1653608093000),
                        new ExtractChangesLayerServerGen(0, 1653608094000)
                    ]
                },
                cancellationToken));

        Assert.Contains("Duplicate layer server generation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenLayerServerGenIdIsNotIncludedInLayers() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [
                        new ExtractChangesLayerServerGen(1, 1653608093000)
                    ]
                },
                cancellationToken));

        Assert.Contains("must also be present in Layers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenFieldsToCompareContainsBlankValue() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    ServerGens = new ExtractChangesServerGens {
                        SinceServerGen = 1653608093000
                    },
                    FieldsToCompare = ["TYPE", " "]
                },
                cancellationToken));

        Assert.Contains("FieldsToCompare", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenDataFormatIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    ServerGens = new ExtractChangesServerGens {
                        SinceServerGen = 1653608093000
                    },
                    DataFormat = (ExtractChangesDataFormat)999
                },
                cancellationToken));

        Assert.Contains("DataFormat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenOutSridIsNotPositive() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    ServerGens = new ExtractChangesServerGens {
                        SinceServerGen = 1653608093000
                    },
                    OutSrid = 0
                },
                cancellationToken));

        Assert.Contains("OutSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_ThrowsFeatureServiceException_WhenStatusUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (IsExtractChangesRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "statusUrl": "not a valid absolute uri"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [
                        new ExtractChangesLayerServerGen(0, 1653608093000)
                    ],
                    DataFormat = ExtractChangesDataFormat.Sqlite
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("extractChanges", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetExtractChangesStatusAsync_ThrowsFeatureServiceException_WhenResultUrlIsInvalid() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsoluteUri == "https://example.test/jobs/extractChanges/status") {
                return StubHttpMessageHandler.Json("""
            {
              "status": "completed",
              "resultUrl": "not a valid absolute uri"
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetExtractChangesStatusAsync(
                new Uri("https://example.test/jobs/extractChanges/status"),
                cancellationToken));

        Assert.Contains("resultUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("extractChanges", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_ThrowsFeatureServiceException_WhenStatusUrlIsNotAString() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            if (IsExtractChangesRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "statusUrl": 123
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.SubmitExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    LayerServerGens = [
                        new ExtractChangesLayerServerGen(0, 1653608093000)
                    ],
                    DataFormat = ExtractChangesDataFormat.Sqlite
                },
                cancellationToken));

        Assert.Contains("statusUrl", exception.Message, StringComparison.Ordinal);
        Assert.Contains("extractChanges", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request, int layerId) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            $"/FeatureServer/{layerId}",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsExtractChangesRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/extractChanges",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Facilities",
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
          "hasAttachments": false,
          "supportsQueryAttachments": false,
          "supportsAttachmentsResizing": false,
          "supportsTopFeaturesQuery": false,
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