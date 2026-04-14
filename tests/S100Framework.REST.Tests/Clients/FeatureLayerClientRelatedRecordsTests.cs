using S100Framework.REST.Abstractions;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientRelatedRecordsTests
{
    [Fact]
    public async Task QueryRelatedRecordsAsync_SendsExpectedParameters_AndMapsGroupedRows() {
        var requestUris = new List<string>();

        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (uri.Contains("/FeatureServer/0/queryRelatedRecords?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "geometryType": "esriGeometryPoint",
                  "spatialReference": {
                    "wkid": 4326,
                    "latestWkid": 4326
                  },
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "NAME", "type": "esriFieldTypeString", "nullable": true }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 100,
                      "relatedRecords": [
                        {
                          "attributes": {
                            "OBJECTID": 1,
                            "NAME": "Related A"
                          },
                          "geometry": {
                            "x": 10,
                            "y": 20
                          }
                        },
                        {
                          "attributes": {
                            "OBJECTID": 2,
                            "NAME": "Related B"
                          },
                          "geometry": {
                            "x": 11,
                            "y": 21
                          }
                        }
                      ]
                    },
                    {
                      "objectId": 200,
                      "relatedRecords": [
                        {
                          "attributes": {
                            "OBJECTID": 3,
                            "NAME": "Related C"
                          },
                          "geometry": {
                            "x": 12,
                            "y": 22
                          }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        IFeatureServiceClient serviceClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });

        var layerClient = serviceClient.GetLayerClient(0);

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [100, 200],
                RelationshipId = 1,
                OutFields = ["OBJECTID", "NAME"],
                DefinitionExpression = "1=1",
                ReturnGeometry = true,
                ReturnZ = false,
                ReturnM = false,
                OutSrid = 4326,
                GeometryPrecision = 3,
                MaxAllowableOffset = 0.5,
                OrderBy = "NAME"
            });

        Assert.Equal(2, groups.Count);

        Assert.Equal(100, groups[0].SourceObjectId);
        Assert.Equal(2, groups[0].Records.Count);
        Assert.Equal(1, groups[0].Records[0].ObjectId);
        Assert.Equal("Related A", groups[0].Records[0].GetRequiredString("NAME"));
        Assert.NotNull(groups[0].Records[0].Geometry);

        Assert.Equal(200, groups[1].SourceObjectId);
        Assert.Single(groups[1].Records);
        Assert.Equal("Related C", groups[1].Records[0].GetRequiredString("NAME"));

        var requestUri = Assert.Single(requestUris);
        Assert.Contains("objectIds=100%2C200", requestUri);
        Assert.Contains("relationshipId=1", requestUri);
        Assert.Contains("outFields=OBJECTID%2CNAME", requestUri);
        Assert.Contains("definitionExpression=1%3D1", requestUri);
        Assert.Contains("returnGeometry=true", requestUri);
        Assert.Contains("returnZ=false", requestUri);
        Assert.Contains("returnM=false", requestUri);
        Assert.Contains("outSR=4326", requestUri);
        Assert.Contains("geometryPrecision=3", requestUri);
        Assert.Contains("maxAllowableOffset=0.5", requestUri);
        Assert.Contains("orderByFields=NAME", requestUri);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_MapsTableResultsWithoutGeometry() {
        var handler = new StubHttpMessageHandler(request => {
            var uri = request.RequestUri!.AbsoluteUri;

            if (uri.Contains("/FeatureServer/0/queryRelatedRecords?")) {
                return StubHttpMessageHandler.Json("""
                {
                  "fields": [
                    { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                    { "name": "PLANNAME", "type": "esriFieldTypeString", "nullable": true }
                  ],
                  "relatedRecordGroups": [
                    {
                      "objectId": 10,
                      "relatedRecords": [
                        {
                          "attributes": {
                            "OBJECTID": 99,
                            "PLANNAME": "Plan X"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var layerClient = new FeatureServiceClient(
            new HttpClient(handler),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var groups = await layerClient.QueryRelatedRecordsAsync(
            new RelatedRecordsQuery {
                ObjectIds = [10],
                RelationshipId = 0,
                OutFields = ["OBJECTID", "PLANNAME"],
                ReturnGeometry = false
            });

        Assert.Single(groups);
        Assert.Single(groups[0].Records);
        Assert.Null(groups[0].Records[0].Geometry);
        Assert.Equal(99, groups[0].Records[0].ObjectId);
        Assert.Equal("Plan X", groups[0].Records[0].GetRequiredString("PLANNAME"));
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_Throws_WhenObjectIdsAreMissing() {
        var layerClient = new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("The HTTP request should not be executed."))),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            }).GetLayerClient(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            layerClient.QueryRelatedRecordsAsync(
                new RelatedRecordsQuery {
                    ObjectIds = [],
                    RelationshipId = 1
                }));

        Assert.Contains("object ID", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}