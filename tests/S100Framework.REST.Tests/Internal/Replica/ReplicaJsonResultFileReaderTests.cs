using S100Framework.REST.Internal.Replica;
using S100Framework.REST.Models;
using System.Text;
using Xunit;

namespace S100Framework.REST.Tests.Internal.Replica;

public sealed class ReplicaJsonResultFileReaderTests
{
    [Fact]
    public void Read_MapsReplicaMetadataGenerationsAndEditResults() {
        var result = ReplicaJsonResultFileReader.Read(
            Encoding.UTF8.GetBytes("""
            {
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeEdits",
              "syncModel": "perReplica",
              "replicaServerGen": 25,
              "edits": [
                {
                  "id": 0,
                  "addResults": [
                    {
                      "objectId": 101,
                      "globalId": "{11111111-1111-1111-1111-111111111111}",
                      "success": true
                    }
                  ],
                  "updateResults": [
                    {
                      "objectID": "102",
                      "globalID": "{22222222-2222-2222-2222-222222222222}",
                      "success": false,
                      "error": {
                        "code": 400,
                        "description": "Update failed.",
                        "details": [
                          null,
                          "",
                          "Missing required field."
                        ]
                      }
                    }
                  ],
                  "deleteResults": [
                    null
                  ]
                }
              ]
            }
            """),
            new Uri("https://example.test/output/sync.json"),
            "synchronizeReplica");

        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("Replica A", result.ReplicaName);
        Assert.Equal("esriTransportTypeURL", result.TransportType);
        Assert.Equal("esriReplicaResponseTypeEdits", result.ResponseType);
        Assert.Equal("perReplica", result.SyncModel);
        Assert.Equal(25, result.ReplicaServerGen);
        Assert.True(result.HasEditResults);
        Assert.True(result.HasEditErrors);
        Assert.Equal(2, result.EditResultCount);
        Assert.Equal(1, result.SuccessfulEditResultCount);
        Assert.Equal(1, result.FailedEditResultCount);
        Assert.Single(result.GetEditErrors());
        var contextualError = Assert.Single(result.GetLayerEditErrors());
        Assert.Equal(0, contextualError.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, contextualError.Operation);
        Assert.Equal(102, contextualError.ObjectId);

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.Id);
        Assert.True(layer.HasEditResults);
        Assert.True(layer.HasEditErrors);
        Assert.Equal(2, layer.EditResultCount);
        Assert.Equal(1, layer.SuccessfulEditResultCount);
        Assert.Equal(1, layer.FailedEditResultCount);

        var addResult = Assert.Single(layer.AddResults);
        Assert.Equal(101, addResult.ObjectId);
        Assert.Equal("{11111111-1111-1111-1111-111111111111}", addResult.GlobalId);
        Assert.True(addResult.Success);
        Assert.False(addResult.HasError);

        var updateResult = Assert.Single(layer.UpdateResults);
        Assert.Equal(102, updateResult.ObjectId);
        Assert.Equal("{22222222-2222-2222-2222-222222222222}", updateResult.GlobalId);
        Assert.False(updateResult.Success);
        Assert.True(updateResult.HasError);
        Assert.Equal(400, updateResult.ErrorCode);
        Assert.Equal("Update failed.", updateResult.ErrorDescription);
        Assert.Equal(["Missing required field."], updateResult.ErrorDetails);

        Assert.Empty(layer.DeleteResults);
        Assert.Contains("\"addResults\"", layer.RawJson);
        Assert.Contains("\"replicaID\"", result.RawJson);
    }

    [Fact]
    public void Read_MapsLayerServerGensAndLayerEntriesFromLayersArray() {
        var result = ReplicaJsonResultFileReader.Read(
            Encoding.UTF8.GetBytes("""
            {
              "responseType": "esriReplicaResponseTypeData",
              "layerServerGens": [
                { "id": 0, "serverGen": 30 }
              ],
              "layers": [
                {
                  "id": 0,
                  "features": []
                }
              ]
            }
            """),
            new Uri("https://example.test/output/replica.json"),
            "createReplica");

        var layerServerGen = Assert.Single(result.LayerServerGens);
        Assert.Equal(0, layerServerGen.Id);
        Assert.Equal(30, layerServerGen.ServerGen);

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.Id);
        Assert.False(layer.HasEditResults);
        Assert.Contains("\"features\"", layer.RawJson);
    }

    [Fact]
    public void Read_IgnoresNullLayerEntries() {
        var result = ReplicaJsonResultFileReader.Read(
            Encoding.UTF8.GetBytes("""
            {
              "edits": [
                null,
                {
                  "id": 0,
                  "addResults": []
                }
              ]
            }
            """),
            new Uri("https://example.test/output/sync.json"),
            "synchronizeReplica");

        var layer = Assert.Single(result.Layers);
        Assert.Equal(0, layer.Id);
    }

    [Fact]
    public void Read_Throws_WhenEditsValueIsNotArray() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaJsonResultFileReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "edits": {}
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("edits", exception.Message);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenLayerIdIsMissing() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaJsonResultFileReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "edits": [
                    {
                      "addResults": []
                    }
                  ]
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenObjectIdIsInvalid() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaJsonResultFileReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "edits": [
                    {
                      "id": 0,
                      "addResults": [
                        {
                          "objectId": "not a number"
                        }
                      ]
                    }
                  ]
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("objectId", exception.Message);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenObjectIdIsNegative() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaJsonResultFileReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "edits": [
                    {
                      "id": 0,
                      "addResults": [
                        {
                          "objectId": -1
                        }
                      ]
                    }
                  ]
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("negative objectId", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}