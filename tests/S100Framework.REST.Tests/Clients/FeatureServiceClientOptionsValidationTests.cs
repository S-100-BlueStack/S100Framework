using S100Framework.REST.Configuration;
using Xunit;

namespace S100Framework.REST.Tests.Configuration;

public sealed class FeatureServiceClientOptionsValidationTests
{
    [Fact]
    public void Validate_Throws_WhenQueryRequestMethodPreferenceIsInvalid() {
        var options = CreateValidOptions();
        options.QueryRequestMethodPreference = (QueryRequestMethodPreference)999;

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("QueryRequestMethodPreference", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Throws_WhenTrueCurveHandlingIsInvalid() {
        var options = CreateValidOptions();
        options.TrueCurveHandling = (TrueCurveHandling)999;

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("TrueCurveHandling", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllowsSupportedEnumValues() {
        var options = CreateValidOptions();

        options.QueryRequestMethodPreference = QueryRequestMethodPreference.Auto;
        options.TrueCurveHandling = TrueCurveHandling.Throw;
        options.Validate();

        options.QueryRequestMethodPreference = QueryRequestMethodPreference.Get;
        options.TrueCurveHandling = TrueCurveHandling.LinearizeCircularArcs;
        options.Validate();

        options.QueryRequestMethodPreference = QueryRequestMethodPreference.Post;
        options.TrueCurveHandling = TrueCurveHandling.Throw;
        options.Validate();
    }

    private static FeatureServiceClientOptions CreateValidOptions() {
        return new FeatureServiceClientOptions {
            ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
        };
    }
}