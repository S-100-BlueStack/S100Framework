using NetTopologySuite.Geometries;
using S100Framework.REST.Models;
using S100Framework.REST.Serialization;
using Xunit;

namespace S100Framework.REST.Tests.Serialization;

public sealed class EsriJsonFeatureConverterTests
{
    [Fact]
    public void SerializeFeature_IncludesGeometryAndAttributes() {
        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var feature = new FeatureRecord(
            geometryFactory.CreatePoint(new Coordinate(10, 20)),
            new Dictionary<string, object?> {
                ["NAME"] = "Harbor A"
            },
            42);

        var json = EsriJsonFeatureConverter.SerializeFeature(
            feature,
            objectIdFieldName: "OBJECTID");

        Assert.Contains("\"geometry\"", json);
        Assert.Contains("\"attributes\"", json);
        Assert.Contains("\"NAME\":\"Harbor A\"", json);
        Assert.Contains("\"OBJECTID\":42", json);
    }

    [Fact]
    public void SerializeFeatureSet_IncludesSchemaMetadataAndFeatures() {
        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var schema = new FeatureLayerSchema(
            0,
            "Harbors",
            "esriGeometryPoint",
            4326,
            false,
            false,
            true,
            1000,
            "OBJECTID",
            Array.Empty<FeatureField>(),
            new FeatureLayerCapabilities(
                HasAttachments: false,
                SupportsQueryAttachments: false,
                SupportsAttachmentsResizing: false,
                SupportsTopFeaturesQuery: false,
                SupportsPagination: true,
                SupportsPaginationOnAggregatedQueries: false,
                SupportsQueryRelatedPagination: false,
                SupportsAdvancedQueryRelated: false,
                SupportsOrderBy: true,
                SupportsDistinct: true,
                SupportsAsyncApplyEdits: false),
            Array.Empty<FeatureRelationshipInfo>());

        var features = new[]
        {
            new FeatureRecord(
                geometryFactory.CreatePoint(new Coordinate(10, 20)),
                new Dictionary<string, object?>
                {
                    ["NAME"] = "Harbor A"
                },
                1),
            new FeatureRecord(
                geometryFactory.CreatePoint(new Coordinate(11, 21)),
                new Dictionary<string, object?>
                {
                    ["NAME"] = "Harbor B"
                },
                2)
        };

        var json = EsriJsonFeatureConverter.SerializeFeatureSet(features, schema);

        Assert.Contains("\"geometryType\":\"esriGeometryPoint\"", json);
        Assert.Contains("\"objectIdFieldName\":\"OBJECTID\"", json);
        Assert.Contains("\"features\"", json);
        Assert.Contains("\"Harbor A\"", json);
        Assert.Contains("\"Harbor B\"", json);
    }
}