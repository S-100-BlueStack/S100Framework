using S100Framework.REST.Models;
using Xunit;

namespace S100Framework.REST.Tests.Models;

public sealed class FeatureServiceMetadataTests
{
    [Fact]
    public void Constructor_Throws_WhenServiceUriIsNull() {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new FeatureServiceMetadata(
                serviceUri: null!,
                layers: Array.Empty<FeatureServiceDatasetInfo>(),
                tables: Array.Empty<FeatureServiceDatasetInfo>(),
                capabilityText: "Query",
                maxRecordCount: 1000,
                capabilities: new FeatureServiceCapabilities(
                    SupportsQuery: true,
                    SupportsCreate: false,
                    SupportsUpdate: false,
                    SupportsDelete: false,
                    SupportsEditing: false,
                    SupportsUploads: false,
                    SupportsSync: false,
                    SupportsChangeTracking: false,
                    SyncEnabled: false,
                    SupportsAsyncApplyEdits: false),
                extractChangesCapabilities: null));

        Assert.Equal("serviceUri", exception.ParamName);
    }

    [Fact]
    public void Constructor_Throws_WhenLayersIsNull() {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new FeatureServiceMetadata(
                serviceUri: new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                layers: null!,
                tables: Array.Empty<FeatureServiceDatasetInfo>(),
                capabilityText: "Query",
                maxRecordCount: 1000,
                capabilities: new FeatureServiceCapabilities(
                    SupportsQuery: true,
                    SupportsCreate: false,
                    SupportsUpdate: false,
                    SupportsDelete: false,
                    SupportsEditing: false,
                    SupportsUploads: false,
                    SupportsSync: false,
                    SupportsChangeTracking: false,
                    SyncEnabled: false,
                    SupportsAsyncApplyEdits: false),
                extractChangesCapabilities: null));

        Assert.Equal("layers", exception.ParamName);
    }

    [Fact]
    public void DatasetInfo_Throws_WhenNameIsNull() {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new FeatureServiceDatasetInfo(
                id: 0,
                name: null!));

        Assert.Equal("name", exception.ParamName);
    }
}