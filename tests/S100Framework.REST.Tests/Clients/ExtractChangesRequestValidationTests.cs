using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class ExtractChangesRequestValidationTests
{
    [Fact]
    public void Validate_Throws_WhenLayersAreMissing() {
        var request = new ExtractChangesRequest {
            ServerGens = new ExtractChangesServerGens {
                SinceServerGen = 10
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("At least one layer ID must be provided", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenNeitherServerGensNorLayerServerGensIsProvided() {
        var request = new ExtractChangesRequest {
            Layers = [0]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Exactly one of ServerGens or LayerServerGens must be provided", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenBothServerGensAndLayerServerGensAreProvided() {
        var request = new ExtractChangesRequest {
            Layers = [0],
            ServerGens = new ExtractChangesServerGens {
                SinceServerGen = 10
            },
            LayerServerGens =
            [
                new ExtractChangesLayerServerGen(0, 20)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Exactly one of ServerGens or LayerServerGens must be provided", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenLayerQueryKeyIsNotPresentInLayers() {
        var request = new ExtractChangesRequest {
            Layers = [0],
            LayerServerGens =
            [
                new ExtractChangesLayerServerGen(0, 20)
            ],
            LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
                [7] = new ExtractChangesLayerQuery {
                    Where = "OBJECTID > 0"
                }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("Layer query key '7' must also be present in Layers", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenReturnDeletedFeaturesRequiresReturnDeletes() {
        var request = CreateValidRequest() with {
            ReturnDeletes = false,
            ReturnDeletedFeatures = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReturnDeletedFeatures requires ReturnDeletes to be true", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenReturnDeletedFeaturesRequiresReturnIdsOnlyFalse() {
        var request = CreateValidRequest() with {
            ReturnIdsOnly = true,
            ReturnDeletedFeatures = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReturnDeletedFeatures requires ReturnIdsOnly to be false", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenReturnAttachmentsDataByUrlRequiresReturnAttachments() {
        var request = CreateValidRequest() with {
            ReturnAttachments = false,
            ReturnAttachmentsDataByUrl = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ReturnAttachmentsDataByUrl requires ReturnAttachments to be true", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenChangesExtentGridCellRequiresReturnExtentOnly() {
        var request = CreateValidRequest() with {
            ReturnExtentOnly = false,
            ChangesExtentGridCell = ExtractChangesExtentGridCell.Medium
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("ChangesExtentGridCell requires ReturnExtentOnly to be true", exception.Message);
    }

    [Fact]
    public void Validate_DoesNotThrow_ForValidLayerServerGensRequest() {
        var request = CreateValidRequest();

        request.Validate();
    }

    [Fact]
    public void Validate_Throws_WhenLayerQueryOptionIsInvalid() {
        var request = CreateValidRequest() with {
            LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
                [0] = new ExtractChangesLayerQuery {
                    QueryOption = (ExtractChangesLayerQueryOption)999
                }
            }
        };

        var exception = Assert.Throws<InvalidOperationException>(() => request.Validate());

        Assert.Contains("QueryOption", exception.Message, StringComparison.Ordinal);
        Assert.Contains("layer query option", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ExtractChangesRequest CreateValidRequest() {
        return new ExtractChangesRequest {
            Layers = [0],
            LayerServerGens =
            [
                new ExtractChangesLayerServerGen(0, 20)
            ],
            ReturnInserts = true,
            ReturnUpdates = true,
            ReturnDeletes = true
        };
    }
}