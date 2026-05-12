using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaJsonResultDiagnosticTests
{
    [Fact]
    public void GetLayerEditResults_ReturnsLayerAndOperationContext() {
        var file = CreateResultFile();

        var results = file.GetLayerEditResults().ToArray();

        Assert.Collection(
            results,
            first => {
                Assert.Equal(0, first.LayerId);
                Assert.Equal(ReplicaEditOperation.Add, first.Operation);
                Assert.Equal(101, first.ObjectId);
                Assert.True(first.IsSuccessful);
                Assert.False(first.IsFailed);
            },
            second => {
                Assert.Equal(0, second.LayerId);
                Assert.Equal(ReplicaEditOperation.Update, second.Operation);
                Assert.Equal(102, second.ObjectId);
                Assert.False(second.IsSuccessful);
                Assert.True(second.IsFailed);
                Assert.Equal(400, second.ErrorCode);
                Assert.Equal("Update failed.", second.ErrorDescription);
            },
            third => {
                Assert.Equal(1, third.LayerId);
                Assert.Equal(ReplicaEditOperation.Delete, third.Operation);
                Assert.Equal(201, third.ObjectId);
                Assert.True(third.IsSuccessful);
                Assert.False(third.IsFailed);
            });
    }

    [Fact]
    public void GetLayerEditErrors_ReturnsOnlyFailedResultsWithContext() {
        var file = CreateResultFile();

        var error = Assert.Single(file.GetLayerEditErrors());

        Assert.Equal(0, error.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, error.Operation);
        Assert.Equal(102, error.ObjectId);
        Assert.Equal("{22222222-2222-2222-2222-222222222222}", error.GlobalId);
        Assert.Equal(400, error.ErrorCode);
        Assert.Equal("Update failed.", error.ErrorDescription);
        Assert.Equal(["Missing required field."], error.ErrorDetails);
    }

    [Fact]
    public void LayerGetLayerEditErrors_ReturnsOnlyFailedResultsForLayer() {
        var layer = CreateResultFile().Layers[0];

        var error = Assert.Single(layer.GetLayerEditErrors());

        Assert.Equal(0, error.LayerId);
        Assert.Equal(ReplicaEditOperation.Update, error.Operation);
        Assert.Equal(102, error.ObjectId);
    }

    private static ReplicaJsonResultFile CreateResultFile() {
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
                            Success: true,
                            ErrorCode: null,
                            ErrorDescription: null,
                            ErrorDetails: [],
                            RawJson: "{}")
                    ],
                    RawJson: "{}")
            ],
            RawJson: "{}",
            ResultUrl: new Uri("https://example.test/output/sync.json"));
    }
}