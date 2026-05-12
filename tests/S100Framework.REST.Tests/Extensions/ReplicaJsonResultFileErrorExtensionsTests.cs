using S100Framework.REST.Exceptions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class ReplicaJsonResultFileErrorExtensionsTests
{
    [Fact]
    public void ThrowIfEditErrors_DoesNotThrow_WhenNoEditResultsFailed() {
        var result = new ReplicaJsonResultFile(
            ReplicaId: "replica-1",
            ReplicaName: "Replica A",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            SyncModel: "perReplica",
            TargetType: "client",
            ReplicaServerGen: 25,
            LayerServerGens: [],
            Layers: [
                new ReplicaJsonResultLayer(
                    Id: 0,
                    AddResults: [
                        new ReplicaEditResult(
                            ObjectId: 101,
                            GlobalId: "{11111111-1111-1111-1111-111111111111}",
                            Success: true,
                            ErrorCode: null,
                            ErrorDescription: null,
                            ErrorDetails: [],
                            RawJson: "{}")
                    ],
                    UpdateResults: [],
                    DeleteResults: [],
                    RawJson: "{}")
            ],
            RawJson: "{}",
            ResultUrl: new Uri("https://example.test/output/sync.json"));

        result.ThrowIfEditErrors();
    }

    [Fact]
    public void ThrowIfEditErrors_ThrowsReplicaEditResultsException_WhenEditResultsFailed() {
        var result = CreateResultWithErrors();

        var exception = Assert.Throws<ReplicaEditResultsException>(() =>
            result.ThrowIfEditErrors());

        Assert.Equal(2, exception.Errors.Count);

        Assert.Contains("2 failed edit results", exception.Message);
        Assert.Contains("Layer 0 Update failed", exception.Message);
        Assert.Contains("objectId 102", exception.Message);
        Assert.Contains("globalId {22222222-2222-2222-2222-222222222222}", exception.Message);
        Assert.Contains("code 400", exception.Message);
        Assert.Contains("Update failed.", exception.Message);

        Assert.Collection(
            exception.Errors,
            first => {
                Assert.Equal(0, first.LayerId);
                Assert.Equal(ReplicaEditOperation.Update, first.Operation);
                Assert.Equal(102, first.ObjectId);
            },
            second => {
                Assert.Equal(1, second.LayerId);
                Assert.Equal(ReplicaEditOperation.Delete, second.Operation);
                Assert.Equal(201, second.ObjectId);
            });
    }

    [Fact]
    public void ThrowIfEditErrors_LimitsMessageToFirstFiveErrors() {
        var result = new ReplicaJsonResultFile(
            ReplicaId: "replica-1",
            ReplicaName: "Replica A",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            SyncModel: "perReplica",
            TargetType: "client",
            ReplicaServerGen: 25,
            LayerServerGens: [],
            Layers: [
                new ReplicaJsonResultLayer(
                    Id: 0,
                    AddResults: [],
                    UpdateResults: Enumerable
                        .Range(1, 6)
                        .Select(index => new ReplicaEditResult(
                            ObjectId: index,
                            GlobalId: null,
                            Success: false,
                            ErrorCode: 400,
                            ErrorDescription: $"Failure {index}.",
                            ErrorDetails: [],
                            RawJson: "{}"))
                        .ToArray(),
                    DeleteResults: [],
                    RawJson: "{}")
            ],
            RawJson: "{}",
            ResultUrl: new Uri("https://example.test/output/sync.json"));

        var exception = Assert.Throws<ReplicaEditResultsException>(() =>
            result.ThrowIfEditErrors());

        Assert.Equal(6, exception.Errors.Count);
        Assert.Contains("1 additional edit errors were omitted", exception.Message);
        Assert.Contains("Failure 5.", exception.Message);
        Assert.DoesNotContain("Failure 6.", exception.Message);
    }

    private static ReplicaJsonResultFile CreateResultWithErrors() {
        return new ReplicaJsonResultFile(
            ReplicaId: "replica-1",
            ReplicaName: "Replica A",
            TransportType: "esriTransportTypeURL",
            ResponseType: "esriReplicaResponseTypeEdits",
            SyncModel: "perReplica",
            TargetType: "client",
            ReplicaServerGen: 25,
            LayerServerGens: [],
            Layers: [
                new ReplicaJsonResultLayer(
                    Id: 0,
                    AddResults: [],
                    UpdateResults: [
                        new ReplicaEditResult(
                            ObjectId: 102,
                            GlobalId: "{22222222-2222-2222-2222-222222222222}",
                            Success: false,
                            ErrorCode: 400,
                            ErrorDescription: "Update failed.",
                            ErrorDetails: [
                                "Missing required field."
                            ],
                            RawJson: "{}")
                    ],
                    DeleteResults: [],
                    RawJson: "{}"),
                new ReplicaJsonResultLayer(
                    Id: 1,
                    AddResults: [],
                    UpdateResults: [],
                    DeleteResults: [
                        new ReplicaEditResult(
                            ObjectId: 201,
                            GlobalId: "{33333333-3333-3333-3333-333333333333}",
                            Success: false,
                            ErrorCode: 404,
                            ErrorDescription: "Delete failed.",
                            ErrorDetails: [],
                            RawJson: "{}")
                    ],
                    RawJson: "{}")
            ],
            RawJson: "{}",
            ResultUrl: new Uri("https://example.test/output/sync.json"));
    }
}