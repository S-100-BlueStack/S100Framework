using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureServiceClientCreateReplicaStateExtensionsTests
{
    [Fact]
    public async Task CreateReplicaStateAsync_UsesImmediatePerReplicaResultDownloadsFileAndBuildsState() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/replica.json";
        var expectedContent = Encoding.UTF8.GetBytes("""
        {
          "features": []
        }
        """);
        string? requestBody = null;

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica") {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json($$"""
                {
                  "replicaName": "Replica A",
                  "replicaID": "replica-1",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeData",
                  "syncModel": "perReplica",
                  "targetType": "client",
                  "replicaServerGen": 20,
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(expectedContent)
                };

                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                return response;
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.CreateReplicaStateAsync(
            CreateValidRequest(),
            cancellationToken: cancellationToken);

        Assert.Equal(expectedContent, result.File.Content);
        Assert.Equal("application/json", result.File.ContentType);
        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);

        Assert.Equal("replica-1", result.InitialState.ReplicaId);
        Assert.Equal("Replica A", result.InitialState.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, result.InitialState.SyncModel);
        Assert.Equal(20, result.InitialState.ReplicaServerGen);
        Assert.Empty(result.InitialState.LayerServerGens);

        Assert.Equal("replica-1", result.CreateResult.ReplicaId);
        Assert.Equal(20, result.CreateResult.ReplicaServerGen);

        Assert.NotNull(requestBody);
        Assert.Contains("syncModel=perReplica", requestBody);
        Assert.Contains("transportType=esriTransportTypeURL", requestBody);
        Assert.Contains("dataFormat=json", requestBody);
    }

    [Fact]
    public async Task CreateReplicaStateAsync_UpdatesPerReplicaStateFromAsyncJsonResultFile() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string statusUrl = "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/create-1/status";
        const string resultUrl = "https://example.test/output/replica.json";

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "statusUrl": "{{statusUrl}}"
                }
                """);
            }

            if (uri == statusUrl) {
                return StubHttpMessageHandler.Json($$"""
                {
                  "status": "Completed",
                  "replicaName": "Replica A",
                  "replicaID": "replica-1",
                  "responseType": "esriReplicaResponseTypeData",
                  "transportType": "esriTransportTypeURL",
                  "targetType": "client",
                  "resultUrl": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "replicaName": "Replica A",
                      "responseType": "esriReplicaResponseTypeData",
                      "syncModel": "perReplica",
                      "replicaServerGen": 25
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.CreateReplicaStateAsync(
            CreateValidRequest() with {
                IsAsync = true
            },
            new ReplicaPollingOptions {
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1)
            },
            cancellationToken);

        Assert.Equal(new Uri(resultUrl), result.File.ResultUrl);
        Assert.Equal("replica-1", result.InitialState.ReplicaId);
        Assert.Equal("Replica A", result.InitialState.ReplicaName);
        Assert.Equal(SynchronizeReplicaSyncModel.PerReplica, result.InitialState.SyncModel);
        Assert.Equal(25, result.InitialState.ReplicaServerGen);
        Assert.Equal(25, result.CreateResult.ReplicaServerGen);
    }

    [Fact]
    public async Task CreateReplicaStateAsync_UpdatesPerLayerStateFromJsonResultFile() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/replica.json";

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "replicaID": "replica-1",
                  "replicaName": "Replica A",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeData",
                  "syncModel": "perLayer",
                  "targetType": "client",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                      "replicaID": "replica-1",
                      "replicaName": "Replica A",
                      "responseType": "esriReplicaResponseTypeData",
                      "syncModel": "perLayer",
                      "layerServerGens": [
                        null,
                        { "id": "0", "serverGen": "30" },
                        { "id": 1, "serverGen": 31 }
                      ]
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var result = await client.CreateReplicaStateAsync(
            CreateValidRequest() with {
                SyncModel = CreateReplicaSyncModel.PerLayer
            },
            cancellationToken: cancellationToken);

        Assert.Equal(SynchronizeReplicaSyncModel.PerLayer, result.InitialState.SyncModel);
        Assert.Null(result.InitialState.ReplicaServerGen);

        Assert.Collection(
            result.InitialState.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(30, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(31, second.ServerGen);
            });

        Assert.Collection(
            result.CreateResult.LayerServerGens,
            first => {
                Assert.Equal(0, first.Id);
                Assert.Equal(30, first.ServerGen);
            },
            second => {
                Assert.Equal(1, second.Id);
                Assert.Equal(31, second.ServerGen);
            });
    }

    [Fact]
    public async Task CreateReplicaStateAsync_ThrowsBeforeHttp_WhenTransportIsEmbedded() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateReplicaStateAsync(
                CreateValidRequest() with {
                    TransportType = CreateReplicaTransportType.Embedded
                },
                cancellationToken: cancellationToken));

        Assert.Contains("TransportType.Url", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task CreateReplicaStateAsync_ThrowsBeforeHttp_WhenSyncModelIsNone() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var wasCalled = false;

        var client = CreateClient(_ => {
            wasCalled = true;
            return StubHttpMessageHandler.Json("{}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateReplicaStateAsync(
                CreateValidRequest() with {
                    SyncModel = CreateReplicaSyncModel.None,
                    SpatialFilter = null
                },
                cancellationToken: cancellationToken));

        Assert.Contains("SyncModel None", exception.Message);
        Assert.False(wasCalled);
    }

    [Fact]
    public async Task CreateReplicaStateAsync_Throws_WhenSqliteResultDoesNotExposeGenerationValues() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/replica.geodatabase";

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "replicaName": "Replica A",
                  "replicaID": "replica-1",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeData",
                  "syncModel": "perReplica",
                  "targetType": "client",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent([1, 2, 3, 4])
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateReplicaStateAsync(
                CreateValidRequest() with {
                    DataFormat = CreateReplicaDataFormat.Sqlite
                },
                cancellationToken: cancellationToken));

        Assert.Contains("generation values", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateReplicaStateAsync_Throws_WhenJsonResultDoesNotExposeGenerationValues() {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string resultUrl = "https://example.test/output/replica.json";

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json") {
                return CreateSyncMetadataResponse();
            }

            if (uri == "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica") {
                return StubHttpMessageHandler.Json($$"""
                {
                  "replicaName": "Replica A",
                  "replicaID": "replica-1",
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeData",
                  "syncModel": "perReplica",
                  "targetType": "client",
                  "URL": "{{resultUrl}}"
                }
                """);
            }

            if (uri == resultUrl) {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("""
                    {
                    }
                    """))
                };
            }

            throw new InvalidOperationException($"Unexpected request URI: {uri}");
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateReplicaStateAsync(
                CreateValidRequest(),
                cancellationToken: cancellationToken));

        Assert.Contains("ReplicaServerGen", exception.Message);
    }

    private static CreateReplicaRequest CreateValidRequest() {
        return new CreateReplicaRequest {
            Layers = [0],
            SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 20, 30, 40),
                inSrid: 4326),
            SyncModel = CreateReplicaSyncModel.PerReplica,
            TransportType = CreateReplicaTransportType.Url,
            DataFormat = CreateReplicaDataFormat.Json
        };
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateSyncMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Create,Update,Delete,Query,Sync",
          "syncEnabled": true,
          "syncCapabilities": {
            "supportsPerReplicaSync": true,
            "supportsPerLayerSync": true,
            "supportsSyncModelNone": true,
            "supportsAsync": true
          },
          "supportedExportFormats": "json,sqlite"
        }
        """);
    }
}