using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class FeatureServiceEditsValidationTests
{
    [Fact]
    public void Validate_Throws_WhenLayersIsNull() {
        var edits = new FeatureServiceEdits {
            Layers = null!
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("layer edit block", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayersContainNullValue() {
        var edits = new FeatureServiceEdits {
            Layers = [null!]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Layers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayersContainDuplicateLayerIds() {
        var edits = new FeatureServiceEdits {
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteObjectIds = [1]
                },
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteObjectIds = [2]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Duplicate layer edit block", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenLayerAddsContainNullFeature() {
        var edits = new FeatureServiceEdits {
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    Adds = [null!]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Adds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayerUpdatesContainBlankAttributeName() {
        var edits = new FeatureServiceEdits {
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    Updates = [
                        new EditableFeature(
                            Geometry: null,
                            new Dictionary<string, object?> {
                                [" "] = "invalid"
                            })
                    ]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Updates.Attributes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenDeleteObjectIdsContainNonPositiveValue() {
        var edits = new FeatureServiceEdits {
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteObjectIds = [1, 0]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("DeleteObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenDeleteObjectIdsContainDuplicateValues() {
        var edits = new FeatureServiceEdits {
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteObjectIds = [1, 1]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("DeleteObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenDeleteGlobalIdsContainBlankValue() {
        var edits = new FeatureServiceEdits {
            UseGlobalIds = true,
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteGlobalIds = ["{ABC}", " "]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("DeleteGlobalIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenDeleteGlobalIdsContainDuplicateValues() {
        var edits = new FeatureServiceEdits {
            UseGlobalIds = true,
            Layers = [
                new ServiceLayerEdits {
                    LayerId = 0,
                    DeleteGlobalIds = ["{ABC}", "{abc}"]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("DeleteGlobalIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}