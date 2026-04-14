using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientExtractChangesTests
{
    [Fact]
    public async Task ExtractChangesAsync_ReturnsIdsOnlyChanges() {
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(request => {
            requestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return StubHttpMessageHandler.Json("""
            {
              "layerServerGens": [
                { "id": 0, "serverGen": 1526588581400 }
              ],
              "edits": [
                {
                  "id": 0,
                  "objectIds": {
                    "adds": [2027, 2028],
                    "updates": [2026],
                    "deletes": []
                  }
                }
              ]
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
                ReturnIdsOnly = true
            });

        Assert.NotNull(requestBody);
        Assert.Contains("layers=%5B0%5D", requestBody);
        Assert.Contains("returnIdsOnly=true", requestBody);
        Assert.Contains("layerServerGens=", requestBody);

        Assert.Single(result.LayerServerGens);
        Assert.Single(result.Edits);
        Assert.Equal(0, result.Edits[0].LayerId);
        Assert.NotNull(result.Edits[0].ObjectIds);
        Assert.Equal(2, result.Edits[0].ObjectIds!.Adds.Count);
    }

    [Fact]
    public async Task ExtractChangesAsync_ReturnsFullFeatureChanges() {
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
                    "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
                  }
                }
                """);
            }

            return StubHttpMessageHandler.Json("""
            {
              "layerServerGens": [
                { "id": 0, "serverGen": 153025 }
              ],
              "edits": [
                {
                  "id": 0,
                  "features": {
                    "adds": [
                      {
                        "geometry": { "x": 10, "y": 20 },
                        "attributes": { "OBJECTID": 125, "NAME": "Added" }
                      }
                    ],
                    "updates": [
                      {
                        "geometry": { "x": 11, "y": 21 },
                        "attributes": { "OBJECTID": 126, "NAME": "Updated" }
                      }
                    ],
                    "deleteIds": ["ABC"]
                  }
                }
              ]
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
                ReturnIdsOnly = false
            });

        Assert.Single(result.Edits);
        Assert.NotNull(result.Edits[0].Features);
        Assert.Single(result.Edits[0].Features!.Adds);
        Assert.Single(result.Edits[0].Features.Updates);
        Assert.Single(result.Edits[0].Features.DeleteIds);

        Assert.Equal(125, result.Edits[0].Features.Adds[0].ObjectId);
        Assert.Equal("Added", result.Edits[0].Features.Adds[0].GetRequiredString("NAME"));
        Assert.Equal("ABC", result.Edits[0].Features.DeleteIds[0]);
    }
}