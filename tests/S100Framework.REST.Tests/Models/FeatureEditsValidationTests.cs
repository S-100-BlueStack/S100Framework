using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class FeatureEditsValidationTests
{
    [Fact]
    public void Validate_Throws_WhenAddsContainNullFeature() {
        var edits = new FeatureEdits {
            Adds = [null!]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Adds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenUpdatesContainNullAttributes() {
        var edits = new FeatureEdits {
            Updates = [
                new EditableFeature(
                    Geometry: null,
                    Attributes: null!)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Updates.Attributes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenAddsContainBlankAttributeName() {
        var edits = new FeatureEdits {
            Adds = [
                new EditableFeature(
                    Geometry: null,
                    new Dictionary<string, object?> {
                        [" "] = "invalid"
                    })
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Adds.Attributes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenDeletesContainNonPositiveValue() {
        var edits = new FeatureEdits {
            Deletes = [10, 0]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Deletes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Throws_WhenDeletesContainDuplicateValues() {
        var edits = new FeatureEdits {
            Deletes = [10, 10]
        };

        var exception = Assert.Throws<InvalidOperationException>(edits.Validate);

        Assert.Contains("Deletes", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}