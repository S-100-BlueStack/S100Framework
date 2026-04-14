using NetTopologySuite.Geometries;
using S100Framework.REST.Configuration;
using S100Framework.REST.Internal.EsriGeometry;
using System.Text.Json;
using Xunit;

namespace S100Framework.REST.Tests.Internal.Geometry;

public sealed class EsriGeometryReaderCurveTests
{
    [Fact]
    public void Read_CurvePathWithCircularArc_LinearizesToLineString() {
        using var document = JsonDocument.Parse("""
        {
          "curvePaths": [
            [
              [0, 0],
              { "c": [[3, 3], [1, 4]] }
            ]
          ],
          "spatialReference": {
            "wkid": 4326,
            "latestWkid": 4326
          }
        }
        """);

        var geometry = EsriGeometryReader.Read(
            document.RootElement,
            "esriGeometryPolyline",
            defaultSrid: null,
            preferLatestWkid: true,
            fixInvalidGeometries: false,
            trueCurveHandling: TrueCurveHandling.LinearizeCircularArcs,
            circularArcSegmentCount: 8);

        var lineString = Assert.IsType<LineString>(geometry);
        Assert.True(lineString.NumPoints > 2);
        Assert.Equal(4326, lineString.SRID);

        var start = lineString.GetCoordinateN(0);
        var end = lineString.GetCoordinateN(lineString.NumPoints - 1);

        Assert.Equal(0, start.X, 8);
        Assert.Equal(0, start.Y, 8);
        Assert.Equal(3, end.X, 8);
        Assert.Equal(3, end.Y, 8);
    }

    [Fact]
    public void Read_CurveRingWithCircularArc_LinearizesToPolygon() {
        using var document = JsonDocument.Parse("""
        {
          "curveRings": [
            [
              [0, 0],
              [4, 0],
              { "c": [[4, 4], [6, 2]] },
              [0, 4],
              [0, 0]
            ]
          ],
          "spatialReference": {
            "wkid": 4326,
            "latestWkid": 4326
          }
        }
        """);

        var geometry = EsriGeometryReader.Read(
            document.RootElement,
            "esriGeometryPolygon",
            defaultSrid: null,
            preferLatestWkid: true,
            fixInvalidGeometries: false,
            trueCurveHandling: TrueCurveHandling.LinearizeCircularArcs,
            circularArcSegmentCount: 8);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.True(polygon.ExteriorRing.NumPoints > 5);
        Assert.Equal(4326, polygon.SRID);
    }

    [Fact]
    public void Read_CurvePath_ThrowsForUnsupportedArcType() {
        using var document = JsonDocument.Parse("""
        {
          "curvePaths": [
            [
              [3.5, 0],
              [3.5, 1],
              { "a": [[3.5, 1], [3, 2], 0, 1] }
            ]
          ]
        }
        """);

        var exception = Assert.Throws<NotSupportedException>(() =>
            EsriGeometryReader.Read(
                document.RootElement,
                "esriGeometryPolyline",
                defaultSrid: null,
                preferLatestWkid: true,
                fixInvalidGeometries: false,
                trueCurveHandling: TrueCurveHandling.LinearizeCircularArcs,
                circularArcSegmentCount: 8));

        Assert.Contains("limited to circular arcs", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_CurvePath_ThrowsWhenCurveHandlingIsDisabled() {
        using var document = JsonDocument.Parse("""
        {
          "curvePaths": [
            [
              [0, 0],
              { "c": [[3, 3], [1, 4]] }
            ]
          ]
        }
        """);

        var exception = Assert.Throws<NotSupportedException>(() =>
            EsriGeometryReader.Read(
                document.RootElement,
                "esriGeometryPolyline",
                defaultSrid: null,
                preferLatestWkid: true,
                fixInvalidGeometries: false,
                trueCurveHandling: TrueCurveHandling.Throw,
                circularArcSegmentCount: 8));

        Assert.Contains("ReturnTrueCurves", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}