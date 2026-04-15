using System.Text.Json;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriEditGeometryWriter
{
    public static string WriteFeatures(IEnumerable<EditableFeature> features) {
        ArgumentNullException.ThrowIfNull(features);

        var payload = features.Select(WriteFeature).ToArray();
        return JsonSerializer.Serialize(payload);
    }

    internal static object? WriteGeometry(
        Geometry? geometry,
        bool includeSpatialReference = true) {
        if (geometry is null) {
            return null;
        }

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Empty geometries cannot be serialized as Esri JSON.");
        }

        return geometry switch {
            Point point => WritePoint(point, includeSpatialReference),
            MultiPoint multiPoint => WriteMultiPoint(multiPoint, includeSpatialReference),
            LineString lineString => WritePolyline([lineString], lineString.SRID, includeSpatialReference),
            MultiLineString multiLineString => WritePolyline(
                multiLineString.Geometries.Cast<LineString>(),
                multiLineString.SRID,
                includeSpatialReference),
            Polygon polygon => WritePolygon([polygon], polygon.SRID, includeSpatialReference),
            MultiPolygon multiPolygon => WritePolygon(
                multiPolygon.Geometries.Cast<Polygon>(),
                multiPolygon.SRID,
                includeSpatialReference),
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for Esri JSON serialization.")
        };
    }

    private static object WriteFeature(EditableFeature feature) {
        ArgumentNullException.ThrowIfNull(feature);

        return new Dictionary<string, object?> {
            ["geometry"] = WriteGeometry(feature.Geometry),
            ["attributes"] = feature.Attributes
        };
    }

    private static Dictionary<string, object?> WritePoint(
        Point point,
        bool includeSpatialReference) {
        var payload = new Dictionary<string, object?> {
            ["x"] = point.X,
            ["y"] = point.Y
        };

        if (!double.IsNaN(point.Coordinate.Z)) {
            payload["z"] = point.Coordinate.Z;
        }

        AddSpatialReference(payload, point.SRID, includeSpatialReference);

        return payload;
    }

    private static Dictionary<string, object?> WriteMultiPoint(
        MultiPoint multiPoint,
        bool includeSpatialReference) {
        var points = multiPoint.Geometries
            .Cast<Point>()
            .Select(point => ToCoordinateValues(point.Coordinate))
            .ToList();

        var payload = new Dictionary<string, object?> {
            ["points"] = points
        };

        if (points.Any(values => values.Length >= 3)) {
            payload["hasZ"] = true;
        }

        AddSpatialReference(payload, multiPoint.SRID, includeSpatialReference);

        return payload;
    }

    private static Dictionary<string, object?> WritePolyline(
        IEnumerable<LineString> lineStrings,
        int srid,
        bool includeSpatialReference) {
        var paths = lineStrings
            .Select(line => line.Coordinates.Select(ToCoordinateValues).ToList())
            .ToList();

        var payload = new Dictionary<string, object?> {
            ["paths"] = paths
        };

        if (paths.SelectMany(path => path).Any(values => values.Length >= 3)) {
            payload["hasZ"] = true;
        }

        AddSpatialReference(payload, srid, includeSpatialReference);

        return payload;
    }

    private static Dictionary<string, object?> WritePolygon(
        IEnumerable<Polygon> polygons,
        int srid,
        bool includeSpatialReference) {
        var rings = new List<List<double[]>>();

        foreach (var polygon in polygons) {
            rings.Add(ToRingValues(NormalizeRing((LinearRing)polygon.ExteriorRing, clockwise: true)));

            for (var index = 0; index < polygon.NumInteriorRings; index++) {
                rings.Add(ToRingValues(NormalizeRing((LinearRing)polygon.GetInteriorRingN(index), clockwise: false)));
            }
        }

        var payload = new Dictionary<string, object?> {
            ["rings"] = rings
        };

        if (rings.SelectMany(ring => ring).Any(values => values.Length >= 3)) {
            payload["hasZ"] = true;
        }

        AddSpatialReference(payload, srid, includeSpatialReference);

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

    private static void AddSpatialReference(
        IDictionary<string, object?> payload,
        int srid,
        bool includeSpatialReference) {
        if (!includeSpatialReference) {
            return;
        }

        if (srid > 0) {
            payload["spatialReference"] = new Dictionary<string, object?> {
                ["wkid"] = srid
            };
        }
    }
}