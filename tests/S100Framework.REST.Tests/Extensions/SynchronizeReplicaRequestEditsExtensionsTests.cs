using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class SynchronizeReplicaRequestEditsExtensionsTests
{
    [Fact]
    public void WithEdits_AssignsSerializedEditsJson() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsJson = null
        };

        var updatedRequest = request.WithEdits(
            new ReplicaEdits {
                Layers = [
                    new ReplicaLayerEdits {
                        Id = 0,
                        AddsJson = """
                        [
                          {
                            "attributes": {
                              "globalID": "{11111111-1111-1111-1111-111111111111}"
                            }
                          }
                        ]
                        """
                    }
                ]
            });

        Assert.NotNull(updatedRequest.EditsJson);
        Assert.Contains("\"id\":0", updatedRequest.EditsJson);
        Assert.Contains("\"adds\"", updatedRequest.EditsJson);
        Assert.Null(updatedRequest.EditsUploadId);

        updatedRequest.Validate();
    }

    [Fact]
    public void WithEdits_Throws_WhenRequestAlreadyUsesEditsUploadId() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload,
            EditsUploadId = "upload-1"
        };

        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            request.WithEdits(edits));

        Assert.Contains("EditsUploadId", exception.Message);
    }

    [Fact]
    public void WithEdits_UsesIndentedOutputWhenRequested() {
        var request = new SynchronizeReplicaRequest {
            ReplicaId = "replica-1",
            SyncDirection = SynchronizeReplicaSyncDirection.Upload
        };

        var updatedRequest = request.WithEdits(
            new ReplicaEdits {
                Layers = [
                    new ReplicaLayerEdits {
                        Id = 0,
                        AddsJson = "[]"
                    }
                ]
            },
            new ReplicaEditsJsonOptions {
                WriteIndented = true
            });

        Assert.Contains(Environment.NewLine, updatedRequest.EditsJson);
    }
}