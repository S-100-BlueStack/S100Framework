using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientAppendTests
{
    [Fact]
    public async Task SubmitAppendAsync_UploadRequest_PostsToLayerAppendEndpoint() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/FeatureServer", StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(syncEnabled: false, supportsChangeTracking: false);
            }

            if (path.EndsWith("/FeatureServer/0", StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerSchemaResponse(supportsAppend: true);
            }

            if (path.EndsWith("/FeatureServer/0/append", StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "status": "processing",
                  "statusMessage": "Job Status for jobId: 5db2f302-6b0d-4bd9-8e52-0fd7b0a3f2d1",
                  "itemId": "temp-upload-result-item"
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var layerClient = client.GetLayerClient(0);

        var submission = await layerClient.SubmitAppendAsync(
            new FeatureLayerAppendUploadRequest {
                AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
                SourceTableName = "USA",
                FieldMappings =
                [
                    new FeatureServiceAppendFieldMapping {
                        Name = "NAME",
                        Source = "SOURCE_NAME"
                    }
                ],
                AppendFields = ["NAME"],
                Upsert = true,
                UpsertMatchingField = "GLOBALID",
                UpdateGeometry = true,
                RollbackOnFailure = true
            },
            cancellationToken);

        Assert.Equal("processing", submission.Status);
        Assert.Equal("5db2f302-6b0d-4bd9-8e52-0fd7b0a3f2d1", submission.JobId);
        Assert.NotNull(submission.StatusUrl);
        Assert.Contains("/FeatureServer/0/append/jobs/", submission.StatusUrl!.AbsoluteUri);

        var body = Assert.Single(requestBodies);
        var form = ParseFormBody(body);

        Assert.Equal("json", form["f"]);
        Assert.Equal("0c6b928f590f49ebac04761bab413e49", form["appendUploadId"]);
        Assert.Equal("filegdb", form["appendUploadFormat"]);
        Assert.Equal("USA", form["sourceTableName"]);
        Assert.Equal("true", form["upsert"]);
        Assert.Equal("GLOBALID", form["upsertMatchingField"]);
        Assert.Equal("true", form["updateGeometry"]);
        Assert.Equal("true", form["rollbackOnFailure"]);

        using var fieldMappingsDocument = JsonDocument.Parse(form["fieldMappings"]);
        Assert.Equal("NAME", fieldMappingsDocument.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("SOURCE_NAME", fieldMappingsDocument.RootElement[0].GetProperty("source").GetString());

        using var appendFieldsDocument = JsonDocument.Parse(form["appendFields"]);
        Assert.Equal("NAME", appendFieldsDocument.RootElement[0].GetString());
    }

    [Fact]
    public async Task SubmitAppendAsync_Throws_WhenLayerDoesNotAdvertiseAppendSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/FeatureServer", StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(syncEnabled: false, supportsChangeTracking: false);
            }

            if (path.EndsWith("/FeatureServer/0", StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerSchemaResponse(supportsAppend: false);
            }

            throw new InvalidOperationException("HTTP should not be called after layer metadata lookup.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.SubmitAppendAsync(
                new FeatureLayerAppendUploadRequest {
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                    AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase
                },
                cancellationToken));

        Assert.Contains("append", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitAppendAsync_Throws_WhenUpsertIsUsedWithSyncEnabledService() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/FeatureServer", StringComparison.OrdinalIgnoreCase)) {
                return CreateServiceMetadataResponse(syncEnabled: true, supportsChangeTracking: false);
            }

            throw new InvalidOperationException("HTTP should not be called after service metadata lookup.");
        });

        var layerClient = client.GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            layerClient.SubmitAppendAsync(
                new FeatureLayerAppendUploadRequest {
                    AppendUploadId = "0c6b928f590f49ebac04761bab413e49",
                    AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
                    Upsert = true
                },
                cancellationToken));

        Assert.Contains("upsert", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
            ?? string.Empty;
    }

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(
        bool syncEnabled,
        bool supportsChangeTracking) {
        var capabilities = supportsChangeTracking
            ? "Query,ChangeTracking"
            : "Query";

        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Layer 0" }
          ],
          "tables": [],
          "capabilities": "{{capabilities}}",
          "syncEnabled": {{syncEnabled.ToString().ToLowerInvariant()}}
        }
        """);
    }

    private static HttpResponseMessage CreateLayerSchemaResponse(bool supportsAppend) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "advancedEditingCapabilities": {
            "supportsAsyncApplyEdits": false
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
          "supportsAppend": {{supportsAppend.ToString().ToLowerInvariant()}},
          "supportedAppendFormats": [
            "filegdb",
            "pbf"
          ],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
    }
}