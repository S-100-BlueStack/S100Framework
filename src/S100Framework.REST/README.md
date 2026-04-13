using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var query = new FeatureQuery
{
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    OutSrid = 25832,
    SpatialFilter = FeatureSpatialFilter.FromEnvelope(
        new Envelope(530000, 540000, 6150000, 6160000),
        inSrid: 25832)
};



var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 25832);

var polygon = geometryFactory.CreatePolygon(
    [
        new Coordinate(530000, 6150000),
        new Coordinate(540000, 6150000),
        new Coordinate(540000, 6160000),
        new Coordinate(530000, 6160000),
        new Coordinate(530000, 6150000)
    ]);

var query = new FeatureQuery
{
    SpatialFilter = FeatureSpatialFilter.FromGeometry(
        polygon,
        spatialRelation: EsriSpatialRelationships.Intersects)
};