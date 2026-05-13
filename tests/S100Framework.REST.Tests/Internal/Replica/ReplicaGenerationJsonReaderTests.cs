using System.Text;
using S100Framework.REST.Internal.Replica;
using Xunit;

namespace S100Framework.REST.Tests.Internal.Replica;

public sealed class ReplicaGenerationJsonReaderTests
{
    [Fact]
    public void Read_MapsReplicaGenerationAndMetadata() {
        var result = ReplicaGenerationJsonReader.Read(
            Encoding.UTF8.GetBytes("""
            {
              "replicaID": "replica-1",
              "replicaName": "Replica A",
              "transportType": "esriTransportTypeURL",
              "responseType": "esriReplicaResponseTypeData",
              "syncModel": "perReplica",
              "targetType": "client",
              "replicaServerGen": "25"
            }
            """),
            new Uri("https://example.test/output/replica.json"),
            "createReplica");

        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("Replica A", result.ReplicaName);
        Assert.Equal("esriTransportTypeURL", result.TransportType);
        Assert.Equal("esriReplicaResponseTypeData", result.ResponseType);
        Assert.Equal("perReplica", result.SyncModel);
        Assert.Equal("client", result.TargetType);
        Assert.Equal(25, result.ReplicaServerGen);
        Assert.Empty(result.LayerServerGens);
    }

    [Fact]
    public void Read_MapsLayerServerGensAndIgnoresNullItems() {
        var result = ReplicaGenerationJsonReader.Read(
            Encoding.UTF8.GetBytes("""
            {
              "layerServerGens": [
                null,
                { "id": "0", "serverGen": "30" },
                { "id": 1, "serverGen": 31 }
              ]
            }
            """),
            new Uri("https://example.test/output/sync.json"),
            "synchronizeReplica");

        Assert.Collection(
            result.LayerServerGens,
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
    public void Read_Throws_WhenJsonIsEmpty() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaGenerationJsonReader.Read(
                [],
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenJsonIsInvalid() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaGenerationJsonReader.Read(
                Encoding.UTF8.GetBytes("{"),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("could not be parsed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenReplicaServerGenIsInvalid() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaGenerationJsonReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "replicaServerGen": "not a number"
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("replicaServerGen", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenLayerServerGenIsMissingId() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaGenerationJsonReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "layerServerGens": [
                    { "serverGen": 31 }
                  ]
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_Throws_WhenLayerServerGenIsNegative() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReplicaGenerationJsonReader.Read(
                Encoding.UTF8.GetBytes("""
                {
                  "layerServerGens": [
                    { "id": 1, "serverGen": -1 }
                  ]
                }
                """),
                new Uri("https://example.test/output/sync.json"),
                "synchronizeReplica"));

        Assert.Contains("negative serverGen", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_MapsLowerCamelReplicaIdAlias() {
        var result = ReplicaGenerationJsonReader.Read(
            Encoding.UTF8.GetBytes("""
        {
          "replicaId": "replica-1",
          "replicaName": "Replica A"
        }
        """),
            new Uri("https://example.test/output/sync.json"),
            "synchronizeReplica");

        Assert.Equal("replica-1", result.ReplicaId);
        Assert.Equal("Replica A", result.ReplicaName);
    }
}