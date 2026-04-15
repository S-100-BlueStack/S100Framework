using NetTopologySuite.Geometries;
using S100Framework.REST.Configuration;
using S100Framework.REST.Serialization;
using Xunit;

namespace S100Framework.REST.Tests.Serialization;

public sealed class EsriJsonGeometryConverterTests
{
    [Fact]
    public void Serialize_Point_IncludesSpatialReference() {
        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var point = geometryFactory.CreatePoint(new Coordinate(10, 20));

        var json = EsriJsonGeometryConverter.Serialize(point);

        Assert.Contains("\"x\":10", json);
        Assert.Contains("\"y\":20", json);
        Assert.Contains("\"spatialReference\"", json);
        Assert.Contains("\"wkid\":4326", json);
    }

    [Fact]
    public void Deserialize_Point_ReturnsPointGeometry() {
        const string json = """
        {
          "x": 10,
          "y": 20,
          "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
        }
        """;

        var geometry = EsriJsonGeometryConverter.Deserialize(json);

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(10, point.X);
        Assert.Equal(20, point.Y);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public void Deserialize_CurvePath_UsesConfiguredCurveHandling() {
        const string json = """
        {
          "curvePaths": [
            [
              [0, 0],
              { "c": [[3, 3], [1, 4]] }
            ]
          ]
        }
        """;

        var geometry = EsriJsonGeometryConverter.Deserialize(
            json,
            geometryType: "esriGeometryPolyline",
            trueCurveHandling: TrueCurveHandling.LinearizeCircularArcs,
            circularArcSegmentCount: 8);

        var line = Assert.IsType<LineString>(geometry);
        Assert.True(line.NumPoints > 2);
    }
}