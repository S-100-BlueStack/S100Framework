using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientCreateReplicaTests
{
    [Fact]
    public async Task SubmitCreateReplicaAsync_SendsExpectedRequestAndMapsUrlResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request))
            {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request))
            {
                requestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

                return StubHttpMessageHandler.Json("""
                {
                  "transportType": "esriTransportTypeURL",
                  "responseType": "esriReplicaResponseTypeData",
                  "replicaName": "Replica A",
                  "replicaID": "replica-1",
                  "syncModel": "perLayer",
                  "targetType": "client",
                  "layerServerGens": [
                    { "id": 0, "serverGen": 1526605677436 }
                  ],
                  "URL": "https://example.test/output/replica.geodatabase"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitCreateReplicaAsync(
            new CreateReplicaRequest
            {
                ReplicaName = "Replica A",
                Layers = [0],
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 20, 30, 40),
                    inSrid: 4326),
                ReplicaSrid = 4326,
                SyncModel = CreateReplicaSyncModel.PerLayer,
                DataFormat = CreateReplicaDataFormat.Sqlite,
                ReturnAttachments = true,
                ReturnAttachmentsDataByUrl = true,
                LayerQueries = new Dictionary<int, CreateReplicaLayerQuery>
                {
                    [0] = new()
                    {
                        Where = "TYPE = 2",
                        UseGeometry = true
                    }
                }
            },
            cancellationToken);

        Assert.False(submission.IsPending);
        Assert.Null(submission.StatusUrl);

        var result = Assert.IsType<CreateReplicaResult>(submission.Result);
        Assert.Equal("Replica A", result.ReplicaName);
        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("esriTransportTypeURL", result.TransportType);
        Assert.Equal("perLayer", result.SyncModel);
        Assert.Equal("client", result.TargetType);
        Assert.Equal("https://example.test/output/replica.geodatabase", result.ResultUrl!.AbsoluteUri);

        var layerServerGen = Assert.Single(result.LayerServerGens);
        Assert.Equal(0, layerServerGen.Id);
        Assert.Equal(1526605677436, layerServerGen.ServerGen);

        Assert.NotNull(requestBody);
        Assert.Contains("f=json", requestBody);
        Assert.Contains("replicaName=Replica+A", requestBody);
        Assert.Contains("layers=%5B0%5D", requestBody);
        Assert.Contains("transportType=esriTransportTypeURL", requestBody);
        Assert.Contains("returnAttachments=true", requestBody);
        Assert.Contains("returnAttachmentsDataByUrl=true", requestBody);
        Assert.Contains("async=false", requestBody);
        Assert.Contains("syncModel=perLayer", requestBody);
        Assert.Contains("dataFormat=sqlite", requestBody);
        Assert.Contains("targetType=client", requestBody);
        Assert.Contains("replicaSR=4326", requestBody);
        Assert.Contains("layerQueries=", requestBody);
    }

    [Fact]
    public async Task SubmitCreateReplicaAsync_ReturnsStatusUrl_WhenServerRespondsAsynchronously()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request))
            {
                return CreateSyncMetadataResponse();
            }

            if (IsCreateReplicaRequest(request))
            {
                return StubHttpMessageHandler.Json("""
                {
                  "statusUrl": "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var submission = await client.SubmitCreateReplicaAsync(
            new CreateReplicaRequest
            {
                Layers = [0],
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 20, 30, 40),
                    inSrid: 4326),
                IsAsync = true
            },
            cancellationToken);

        Assert.True(submission.IsPending);
        Assert.Null(submission.Result);
        Assert.Equal(
            "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123",
            submission.StatusUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task GetCreateReplicaStatusAsync_MapsCompletedStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            StubHttpMessageHandler.Json("""
            {
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeData",
              "replicaName": "Replica A",
              "replicaID": "replica-1",
              "targetType": "client",
              "resultUrl": "https://example.test/output/replica.json",
              "submissionTime": "1653614927000",
              "lastUpdatedTime": 1653614930000,
              "status": "Completed"
            }
            """));

        var status = await client.GetCreateReplicaStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"),
            cancellationToken);

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("Completed", status.Status);
        Assert.Equal("Replica A", status.ReplicaName);
        Assert.Equal("replica-1", status.ReplicaId);
        Assert.Equal("client", status.TargetType);
        Assert.Equal("https://example.test/output/replica.json", status.ResultUrl!.AbsoluteUri);
        Assert.Equal(1653614927000, status.SubmissionTime);
        Assert.Equal(1653614930000, status.LastUpdatedTime);
    }

    [Fact]
    public async Task DownloadCreateReplicaFileAsync_DownloadsReplicaFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = new byte[] { 1, 2, 3, 4 };

        var client = CreateClient(_ => {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/octet-stream");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue(
                "attachment")
            {
                FileName = "replica.geodatabase"
            };

            return response;
        });

        var result = await client.DownloadCreateReplicaFileAsync(
            new Uri("https://example.test/output/replica.geodatabase"),
            cancellationToken);

        Assert.Equal(bytes, result.Content);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Equal("replica.geodatabase", result.FileName);
        Assert.Equal("https://example.test/output/replica.geodatabase", result.ResultUrl.AbsoluteUri);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions
            {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request)
    {
        return request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json";
    }

    private static bool IsCreateReplicaRequest(HttpRequestMessage request)
    {
        return request.Method == HttpMethod.Post &&
               request.RequestUri?.AbsoluteUri ==
               "https://example.test/arcgis/rest/services/Test/FeatureServer/createReplica";
    }

    private static HttpResponseMessage CreateSyncMetadataResponse()
    {
        return StubHttpMessageHandler.Json("""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "Query,Sync",
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