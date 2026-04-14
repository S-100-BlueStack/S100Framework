using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;
using System.Text.Json;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriEditGeometryWriter
{
    public static object? Write(Geometry? geometry) {
        if (geometry is null) {
            return null;
        }

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Empty geometries cannot be serialized for applyEdits.");
        }

        return geometry switch {
            Point point => WritePoint(point),
            MultiPoint multiPoint => WriteMultiPoint(multiPoint),
            LineString lineString => WritePolyline([lineString], lineString.SRID),
            MultiLineString multiLineString => WritePolyline(multiLineString.Geometries.Cast<LineString>(), multiLineString.SRID),
            Polygon polygon => WritePolygon([polygon], polygon.SRID),
            MultiPolygon multiPolygon => WritePolygon(multiPolygon.Geometries.Cast<Polygon>(), multiPolygon.SRID),
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for applyEdits serialization.")
        };
    }

    public static string WriteFeatures(IEnumerable<EditableFeature> features) {
        var payload = features.Select(WriteFeature).ToArray();
        return JsonSerializer.Serialize(payload);
    }

    private static object WriteFeature(EditableFeature feature) {
        ArgumentNullException.ThrowIfNull(feature);

        return new Dictionary<string, object?> {
            ["geometry"] = Write(feature.Geometry),
            ["attributes"] = feature.Attributes
        };
    }

    private static Dictionary<string, object?> WritePoint(Point point) {
        var payload = new Dictionary<string, object?> {
            ["x"] = point.X,
            ["y"] = point.Y
        };

        if (!double.IsNaN(point.Coordinate.Z)) {
            payload["z"] = point.Coordinate.Z;
        }

        AddSpatialReference(payload, point.SRID);

        return payload;
    }

    private static Dictionary<string, object?> WriteMultiPoint(MultiPoint multiPoint) {
        var points = multiPoint.Geometries
            .Cast<Point>()
            .Select(point => ToCoordinateValues(point.Coordinate))
            .ToList();

        var payload = new Dictionary<string, object?> {
            ["points"] = points
        };

        AddSpatialReference(payload, multiPoint.SRID);

        return payload;
    }

    private static Dictionary<string, object?> WritePolyline(
        IEnumerable<LineString> lineStrings,
        int srid) {
        var paths = lineStrings
            .Select(line => line.Coordinates.Select(ToCoordinateValues).ToList())
            .ToList();

        var payload = new Dictionary<string, object?> {
            ["paths"] = paths
        };

        AddSpatialReference(payload, srid);

        return payload;
    }

    private static Dictionary<string, object?> WritePolygon(
        IEnumerable<Polygon> polygons,
        int srid) {
        var rings = new List<List<double[]>>();

        foreach (var polygon in polygons) {
            rings.Add(ToRingValues(NormalizeRing(polygon.Shell, clockwise: true)));

            foreach (var hole in polygon.Holes) {
                rings.Add(ToRingValues(NormalizeRing(hole, clockwise: false)));
            }
        }

        var payload = new Dictionary<string, object?> {
            ["rings"] = rings
        };

        AddSpatialReference(payload, srid);

        return payload;
    }

    private static Coordinate[] NormalizeRing(LinearRing ring, bool clockwise) {
        var coordinates = ring.Coordinates
            .Select(coordinate => CreateCoordinate(coordinate.X, coordinate.Y, coordinate.Z))
            .ToList();

        if (coordinates.Count == 0) {
            return [];
        }

        if (!coordinates[0].Equals2D(coordinates[^1])) {
            coordinates.Add(CreateCoordinate(
                coordinates[0].X,
                coordinates[0].Y,
                coordinates[0].Z));
        }

        var isClockwise = !Orientation.IsCCW(coordinates.ToArray());

        if (clockwise != isClockwise) {
            coordinates.Reverse();
        }

        return coordinates.ToArray();
    }

    private static List<double[]> ToRingValues(IEnumerable<Coordinate> coordinates) {
        return coordinates.Select(ToCoordinateValues).ToList();
    }

    private static double[] ToCoordinateValues(Coordinate coordinate) {
        return double.IsNaN(coordinate.Z)
            ? [coordinate.X, coordinate.Y]
            : [coordinate.X, coordinate.Y, coordinate.Z];
    }

    private static Coordinate CreateCoordinate(double x, double y, double z) {
        return double.IsNaN(z)
            ? new Coordinate(x, y)
            : new CoordinateZ(x, y, z);
    }

    private static void AddSpatialReference(IDictionary<string, object?> payload, int srid) {
        if (srid > 0) {
            payload["spatialReference"] = new Dictionary<string, object?> {
                ["wkid"] = srid
            };
        }
    }
}