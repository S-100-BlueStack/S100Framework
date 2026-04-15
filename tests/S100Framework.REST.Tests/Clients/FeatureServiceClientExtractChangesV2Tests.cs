using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientExtractChangesV2Tests
{
    [Fact]
    public async Task ExtractChangesAsync_IncludesLayerQueriesGeometryAttachmentsAndFieldsToCompare() {
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
              "edits": [
                {
                  "id": 0,
                  "objectIds": {
                    "adds": [73143],
                    "updates": [65715],
                    "deletes": []
                  },
                  "fieldUpdates": [65715],
                  "attachments": {
                    "adds": [],
                    "updates": [],
                    "deleteIds": []
                  }
                }
              ],
              "transportType": "esriTransportTypeUrl"
            }
            """);
        });

        var client = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var spatialFilter = ExtractChangesSpatialFilter.FromEnvelope(
            new Envelope(-104, -94.32, 35.6, 41),
            inSrid: 4326);

        var result = await client.ExtractChangesAsync(
            new ExtractChangesRequest {
                Layers = [0],
                LayerServerGens =
                [
                    new ExtractChangesLayerServerGen(0, 1653608093000)
                ],
                LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
                    [0] = new() {
                        QueryOption = ExtractChangesLayerQueryOption.UseFilter,
                        Where = "requires_inspection = 'yes'",
                        UseGeometry = true
                    }
                },
                SpatialFilter = spatialFilter,
                ReturnIdsOnly = true,
                ReturnAttachments = true,
                ReturnAttachmentsDataByUrl = true,
                FieldsToCompare = ["type"]
            });

        Assert.NotNull(requestBody);
        Assert.Contains("layerQueries=", requestBody);
        Assert.Contains("geometry=", requestBody);
        Assert.Contains("geometryType=esriGeometryEnvelope", requestBody);
        Assert.Contains("inSR=4326", requestBody);
        Assert.Contains("returnAttachments=true", requestBody);
        Assert.Contains("returnAttachmentsDataByUrl=true", requestBody);
        Assert.Contains("fieldsToCompare=", requestBody);

        Assert.Equal("esriTransportTypeUrl", result.TransportType);
        Assert.Single(result.Edits);
        Assert.NotNull(result.Edits[0].Attachments);
        Assert.Single(result.Edits[0].FieldUpdates);
    }

    [Fact]
    public async Task ExtractChangesAsync_MapsAttachmentDeleteIdsAlongsideFeatureDeletes() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0?")) {
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
                  "advancedEditingCapabilities": {
                    "supportsAsyncApplyEdits": false
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
                  ],
                  "relationships": [],
                  "hasAttachments": true,
                  "supportsQueryAttachments": true,
                  "supportsAttachmentsResizing": false,
                  "supportsTopFeaturesQuery": false,
                  "extent": {
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            return StubHttpMessageHandler.Json("""
            {
              "edits": [
                {
                  "id": 0,
                  "features": {
                    "adds": [],
                    "updates": [],
                    "deleteIds": ["{FEATURE-GID}"]
                  },
                  "attachments": {
                    "adds": [],
                    "updates": [],
                    "deleteIds": ["{ATT-GID}"]
                  }
                }
              ],
              "layerServerGens": [
                { "id": 0, "serverGen": 1675232949666 }
              ],
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
                ServerGens = new ExtractChangesServerGens {
                    SinceServerGen = 1653608093000
                },
                ReturnIdsOnly = false,
                ReturnDeletes = true,
                ReturnDeletedFeatures = false,
                ReturnAttachments = true,
                ReturnAttachmentsDataByUrl = true
            });

        Assert.Single(result.Edits);
        Assert.NotNull(result.Edits[0].Features);
        Assert.NotNull(result.Edits[0].Attachments);

        Assert.Single(result.Edits[0].Features!.DeleteIds);
        Assert.Equal("{FEATURE-GID}", result.Edits[0].Features.DeleteIds[0]);

        Assert.Single(result.Edits[0].Attachments!.DeleteIds);
        Assert.Equal("{ATT-GID}", result.Edits[0].Attachments.DeleteIds[0]);
    }

    [Fact]
    public async Task ExtractChangesAsync_Throws_WhenFieldsToCompareIsUsedWithoutUpdates() {
        var client = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ExtractChangesAsync(
                new ExtractChangesRequest {
                    Layers = [0],
                    ServerGens = new ExtractChangesServerGens {
                        SinceServerGen = 1653608093000
                    },
                    ReturnUpdates = false,
                    FieldsToCompare = ["type"]
                }));

        Assert.Contains("FieldsToCompare", exception.Message);
    }
}