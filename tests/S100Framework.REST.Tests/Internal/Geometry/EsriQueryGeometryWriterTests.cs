using NetTopologySuite.Geometries;
using S100Framework.REST.Internal.EsriGeometry;
using System.Text.Json;
using Xunit;

namespace S100Framework.REST.Tests.Internal.Geometry;

public sealed class EsriQueryGeometryWriterTests
{
    [Fact]
    public void WriteEnvelope_WritesExpectedEsriEnvelopeJson() {
        var envelope = new Envelope(10, 30, 20, 40);

        var json = EsriQueryGeometryWriter.WriteEnvelope(envelope, 4326);

        using var document = JsonDocument.Parse(json);

        Assert.Equal(10, document.RootElement.GetProperty("xmin").GetDouble());
        Assert.Equal(30, document.RootElement.GetProperty("xmax").GetDouble());
        Assert.Equal(20, document.RootElement.GetProperty("ymin").GetDouble());
        Assert.Equal(40, document.RootElement.GetProperty("ymax").GetDouble());
        Assert.Equal(4326, document.RootElement
            .GetProperty("spatialReference")
            .GetProperty("wkid")
            .GetInt32());
    }

    [Fact]
    public void WriteGeometry_WritesEsriPolygonJson() {
        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 25832);

        var polygon = geometryFactory.CreatePolygon(
            [
                new Coordinate(0, 0),
                new Coordinate(10, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10),
                new Coordinate(0, 0)
            ]);

        var json = EsriQueryGeometryWriter.WriteGeometry(polygon, polygon.SRID);

        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("rings", out var rings));
        Assert.Equal(JsonValueKind.Array, rings.ValueKind);
        Assert.Equal(25832, document.RootElement
            .GetProperty("spatialReference")
            .GetProperty("wkid")
            .GetInt32());
    }
}