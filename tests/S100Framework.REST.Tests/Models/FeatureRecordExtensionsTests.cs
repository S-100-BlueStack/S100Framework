using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class FeatureRecordExtensionsTests
{
    [Fact]
    public void GetString_ReturnsStringValue() {
        var feature = CreateFeature(
            new Dictionary<string, object?> {
                ["NAME"] = "Harbor A"
            });

        var value = feature.GetString("NAME");

        Assert.Equal("Harbor A", value);
    }

    [Fact]
    public void GetInt64_ConvertsStringValue() {
        var feature = CreateFeature(
            new Dictionary<string, object?> {
                ["OBJECTID"] = "42"
            });

        var value = feature.GetInt64("OBJECTID");

        Assert.Equal(42L, value);
    }

    [Fact]
    public void GetDecimal_ConvertsIntegerValue() {
        var feature = CreateFeature(
            new Dictionary<string, object?> {
                ["DEPTH"] = 12L
            });

        var value = feature.GetDecimal("DEPTH");

        Assert.Equal(12m, value);
    }

    [Fact]
    public void GetBoolean_ConvertsNumericFlag() {
        var feature = CreateFeature(
            new Dictionary<string, object?> {
                ["IS_ACTIVE"] = 1
            });

        var value = feature.GetBoolean("IS_ACTIVE");

        Assert.True(value);
    }

    [Fact]
    public void GetRequiredString_Throws_WhenAttributeIsMissing() {
        var feature = CreateFeature(new Dictionary<string, object?>());

        var exception = Assert.Throws<InvalidOperationException>(() => feature.GetRequiredString("NAME"));

        Assert.Contains("NAME", exception.Message);
    }

    [Fact]
    public void HasAttribute_ReturnsTrue_WhenAttributeExists() {
        var feature = CreateFeature(
            new Dictionary<string, object?> {
                ["NAME"] = null
            });

        var hasAttribute = feature.HasAttribute("NAME");

        Assert.True(hasAttribute);
    }

    private static FeatureRecord CreateFeature(IReadOnlyDictionary<string, object?> attributes) {
        return new FeatureRecord(
            Geometry: null,
            Attributes: new Dictionary<string, object?>(attributes, StringComparer.OrdinalIgnoreCase),
            ObjectId: null);
    }
}