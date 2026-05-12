using System.Text.Json;
using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class ReplicaEditsTests
{
    [Fact]
    public void ToJson_WritesExpectedLayerEditPayload() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = """
                    [
                      {
                        "geometry": { "x": 10, "y": 20 },
                        "attributes": {
                          "globalID": "{11111111-1111-1111-1111-111111111111}",
                          "name": "New feature"
                        }
                      }
                    ]
                    """,
                    UpdatesJson = """
                    [
                      {
                        "attributes": {
                          "globalID": "{22222222-2222-2222-2222-222222222222}",
                          "name": "Updated feature"
                        }
                      }
                    ]
                    """,
                    DeletesJson = """
                    [
                      {
                        "globalID": "{33333333-3333-3333-3333-333333333333}"
                      }
                    ]
                    """,
                    AttachmentsJson = """
                    {
                      "adds": []
                    }
                    """
                }
            ]
        };

        var json = edits.ToJson();

        using var document = JsonDocument.Parse(json);
        var layer = document.RootElement[0];

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(0, layer.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Array, layer.GetProperty("adds").ValueKind);
        Assert.Equal(JsonValueKind.Array, layer.GetProperty("updates").ValueKind);
        Assert.Equal(JsonValueKind.Array, layer.GetProperty("deletes").ValueKind);
        Assert.Equal(JsonValueKind.Object, layer.GetProperty("attachments").ValueKind);
    }

    [Fact]
    public void ToJson_WritesMultipleLayers() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                },
                new ReplicaLayerEdits {
                    Id = 1,
                    DeletesJson = "[]"
                }
            ]
        };

        var json = edits.ToJson();

        using var document = JsonDocument.Parse(json);

        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal(0, document.RootElement[0].GetProperty("id").GetInt32());
        Assert.Equal(1, document.RootElement[1].GetProperty("id").GetInt32());
    }

    [Fact]
    public void Validate_Throws_WhenLayersAreMissing() {
        var edits = new ReplicaEdits();

        var exception = Assert.Throws<InvalidOperationException>(() => edits.Validate());

        Assert.Contains("at least one layer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayerIdsAreDuplicate() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                },
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "[]"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => edits.Validate());

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenLayerHasNoEditCollections() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => edits.Validate());

        Assert.Contains("at least one edit collection", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenAddsJsonIsNotArray() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "{}"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => edits.Validate());

        Assert.Contains("AddsJson", exception.Message);
        Assert.Contains("JSON array", exception.Message);
    }

    [Fact]
    public void Validate_Throws_WhenRawJsonIsInvalid() {
        var edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = "{"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => edits.Validate());

        Assert.Contains("AddsJson", exception.Message);
        Assert.Contains("valid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}