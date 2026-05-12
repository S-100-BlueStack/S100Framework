using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaJsonResultSummaryTests
{
    [Fact]
    public void SummaryProperties_CountEditResultsAcrossLayers() {
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
                        CreateSuccessfulResult(101)
                    ],
                    UpdateResults: [
                        CreateFailedResult(102, "Update failed.")
                    ],
                    DeleteResults: [],
                    RawJson: "{}"),
                new ReplicaJsonResultLayer(
                    Id: 1,
                    AddResults: [],
                    UpdateResults: [],
                    DeleteResults: [
                        CreateSuccessfulResult(201)
                    ],
                    RawJson: "{}")
            ],
            RawJson: "{}",
            ResultUrl: new Uri("https://example.test/output/sync.json"));

        Assert.True(result.HasEditResults);
        Assert.True(result.HasEditErrors);
        Assert.Equal(3, result.EditResultCount);
        Assert.Equal(2, result.SuccessfulEditResultCount);
        Assert.Equal(1, result.FailedEditResultCount);

        var editResults = result.GetEditResults().ToArray();
        Assert.Equal(3, editResults.Length);

        var error = Assert.Single(result.GetEditErrors());
        Assert.Equal(102, error.ObjectId);
        Assert.Equal("Update failed.", error.ErrorDescription);
    }

    [Fact]
    public void LayerSummaryProperties_CountEditResultsOnLayer() {
        var layer = new ReplicaJsonResultLayer(
            Id: 0,
            AddResults: [
                CreateSuccessfulResult(101)
            ],
            UpdateResults: [
                CreateFailedResult(102, "Update failed.")
            ],
            DeleteResults: [
                CreateSuccessfulResult(103)
            ],
            RawJson: "{}");

        Assert.True(layer.HasEditResults);
        Assert.True(layer.HasEditErrors);
        Assert.Equal(3, layer.EditResultCount);
        Assert.Equal(2, layer.SuccessfulEditResultCount);
        Assert.Equal(1, layer.FailedEditResultCount);

        Assert.Equal(
            [101, 102, 103],
            layer.GetEditResults().Select(static result => result.ObjectId).ToArray());
    }

    [Fact]
    public void EditResult_IsSuccessful_ReturnsTrueOnlyWhenSuccessIsTrueAndNoErrorExists() {
        var result = CreateSuccessfulResult(101);

        Assert.True(result.IsSuccessful);
        Assert.False(result.IsFailed);
        Assert.False(result.HasError);
    }

    [Fact]
    public void EditResult_IsFailed_ReturnsTrueWhenSuccessIsFalse() {
        var result = new ReplicaEditResult(
            ObjectId: 101,
            GlobalId: null,
            Success: false,
            ErrorCode: null,
            ErrorDescription: null,
            ErrorDetails: [],
            RawJson: "{}");

        Assert.False(result.IsSuccessful);
        Assert.True(result.IsFailed);
        Assert.False(result.HasError);
    }

    [Fact]
    public void EditResult_IsFailed_ReturnsTrueWhenErrorExistsWithoutSuccessValue() {
        var result = new ReplicaEditResult(
            ObjectId: 101,
            GlobalId: null,
            Success: null,
            ErrorCode: 400,
            ErrorDescription: "Edit failed.",
            ErrorDetails: [],
            RawJson: "{}");

        Assert.False(result.IsSuccessful);
        Assert.True(result.IsFailed);
        Assert.True(result.HasError);
    }

    private static ReplicaEditResult CreateSuccessfulResult(long objectId) {
        return new ReplicaEditResult(
            ObjectId: objectId,
            GlobalId: null,
            Success: true,
            ErrorCode: null,
            ErrorDescription: null,
            ErrorDetails: [],
            RawJson: "{}");
    }

    private static ReplicaEditResult CreateFailedResult(
        long objectId,
        string errorDescription) {
        return new ReplicaEditResult(
            ObjectId: objectId,
            GlobalId: null,
            Success: false,
            ErrorCode: 400,
            ErrorDescription: errorDescription,
            ErrorDetails: [],
            RawJson: "{}");
    }
}