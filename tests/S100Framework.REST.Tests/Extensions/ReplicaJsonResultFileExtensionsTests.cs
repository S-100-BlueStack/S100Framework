using System.Text;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class ReplicaJsonResultFileExtensionsTests
{
    [Fact]
    public void ReadJsonResultFile_ParsesCreateReplicaFileResult() {
        var file = new CreateReplicaFileResult(
            Encoding.UTF8.GetBytes("""
            {
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "replicaServerGen": 25
            }
            """),
            "application/json",
            "replica.json",
            new Uri("https://example.test/output/replica.json"));

        var result = file.ReadJsonResultFile();

        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("Replica A", result.ReplicaName);
        Assert.Equal(25, result.ReplicaServerGen);
        Assert.Equal(new Uri("https://example.test/output/replica.json"), result.ResultUrl);
    }

    [Fact]
    public void ReadJsonResultFile_ParsesSynchronizeReplicaFileResult() {
        var file = new SynchronizeReplicaFileResult(
            Encoding.UTF8.GetBytes("""
            {
              "replicaID": "replica-1",
              "responseType": "esriReplicaResponseTypeEdits",
              "edits": [
                {
                  "id": 0,
                  "deleteResults": [
                    {
                      "objectId": 101,
                      "success": true
                    }
                  ]
                }
              ]
            }
            """),
            "application/json",
            "sync.json",
            new Uri("https://example.test/output/sync.json"));

        var result = file.ReadJsonResultFile();

        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("esriReplicaResponseTypeEdits", result.ResponseType);
        Assert.True(result.HasEditResults);

        var layer = Assert.Single(result.Layers);
        var deleteResult = Assert.Single(layer.DeleteResults);

        Assert.Equal(101, deleteResult.ObjectId);
        Assert.True(deleteResult.Success);
    }
}