using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using System.Net.Http;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientExtractChangesV3Tests
{
    [Fact]
    public async Task ExtractChangesAsync_ReturnExtentOnly_MapsExtent_AndSendsGridCell() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "layerServerGens": [
                { "id": 0, "serverGen": 1653614103746 }
              ],
              "extent": {
                "xmin": -104,
                "ymin": 35.6,
                "xmax": -94.32,
                "ymax": 41,
                "spatialReference": {
                  "wkid": 4326,
                  "latestWkid": 4326
                }
              },
              "transportType": "esriTransportTypeUrl"
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.ExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                ReturnExtentOnly = true,
                ChangesExtentGridCell = ExtractChangesExtentGridCell.Medium
            });

        Assert.NotNull(requestBody);
        Assert.Contains("returnExtentOnly=true", requestBody);
        Assert.Contains("changesExtentGridCell=medium", requestBody);

        Assert.NotNull(result.Extent);
        Assert.Equal(-104, result.Extent!.Envelope.MinX);
        Assert.Equal(-94.32, result.Extent.Envelope.MaxX);
        Assert.Equal(4326, result.Extent.Srid);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_ReturnsStatusUrl_WhenServerRespondsAsynchronously() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "statusUrl": "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"
            }
            """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var submission = await client.SubmitExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                ReturnIdsOnly = false
            });

        Assert.True(submission.IsPending);
        Assert.NotNull(submission.StatusUrl);
        Assert.Equal("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123", submission.StatusUrl!.AbsoluteUri);
        Assert.Null(submission.Result);
    }

    [Fact]
    public async Task GetExtractChangesStatusAsync_MapsCompletedStatus() {
        var handler = new StubHttpMessageHandler(_ =>
            StubHttpMessageHandler.Json("""
            {
              "responseType": "esriDataChangesResponseTypeEdits",
              "resultUrl": "https://example.test/output/changes.sqlite",
              "submissionTime": 1653614927000,
              "lastUpdatedTime": 1653614930000,
              "status": "Completed",
              "transportType": "esriTransportTypeUrl"
            }
            """));

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var status = await client.GetExtractChangesStatusAsync(
            new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j123"));

        Assert.True(status.IsTerminal);
        Assert.True(status.IsCompleted);
        Assert.Equal("Completed", status.Status);
        Assert.Equal("esriDataChangesResponseTypeEdits", status.ResponseType);
        Assert.Equal("https://example.test/output/changes.sqlite", status.ResultUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task DownloadExtractChangesFileAsync_DownloadsSqliteResult() {
        var bytes = new byte[] { 1, 2, 3, 4 };

        var handler = new StubHttpMessageHandler(_ => {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new ByteArrayContent(bytes)
            };

            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") {
                FileName = "changes.sqlite"
            };

            return response;
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var result = await client.DownloadExtractChangesFileAsync(
            new Uri("https://example.test/output/changes.sqlite"));

        Assert.Equal(bytes, result.Content);
        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.Equal("changes.sqlite", result.FileName);
        Assert.Equal("https://example.test/output/changes.sqlite", result.ResultUrl.AbsoluteUri);
    }

    [Fact]
    public async Task SubmitExtractChangesAsync_SendsSqlliteDataFormat_WhenRequested() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "statusUrl": "https://example.test/arcgis/rest/services/Test/FeatureServer/jobs/j124"
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var submission = await client.SubmitExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens = [new ExtractChangesLayerServerGen(0, 1653608093000)],
                DataFormat = ExtractChangesDataFormat.Sqlite
            });

        Assert.True(submission.IsPending);
        Assert.NotNull(requestBody);
        Assert.Contains("dataFormat=sqllite", requestBody);
    }
}