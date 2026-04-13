using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;
using System.Text.Json;
using Xunit;

namespace S100Framework.REST.Tests.Internal.Geometry;

public sealed class EsriGeometryReaderTests
{
    [Fact]
    public void Read_PolygonWithHole_ReturnsPolygonWithInteriorRing() {
        using var document = JsonDocument.Parse("""
        {
          "rings": [
            [[0,0],[10,0],[10,10],[0,10],[0,0]],
            [[2,2],[2,8],[8,8],[8,2],[2,2]]
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
            fixInvalidGeometries: false);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.Equal(1, polygon.NumInteriorRings);
        Assert.Equal(4326, polygon.SRID);
    }
}