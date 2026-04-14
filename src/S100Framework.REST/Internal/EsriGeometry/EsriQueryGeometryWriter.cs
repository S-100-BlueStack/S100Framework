using System.Text.Json;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriQueryGeometryWriter
{
    public static string WriteEnvelope(Envelope envelope, int? srid) {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.IsNull) {
            throw new InvalidOperationException("Envelope must not be null or empty.");
        }

        var payload = new Dictionary<string, object?> {
            ["xmin"] = envelope.MinX,
            ["ymin"] = envelope.MinY,
            ["xmax"] = envelope.MaxX,
            ["ymax"] = envelope.MaxY
        };

        AddSpatialReference(payload, srid);

        return JsonSerializer.Serialize(payload);
    }

    public static string WriteGeometry(Geometry geometry, int? srid) {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Geometry must not be empty.");
        }

        return geometry switch {
            Point point => JsonSerializer.Serialize(WritePoint(point, srid)),
            MultiPoint multiPoint => JsonSerializer.Serialize(WriteMultiPoint(multiPoint, srid)),
            LineString lineString => JsonSerializer.Serialize(WritePolyline([lineString], srid)),
            MultiLineString multiLineString => JsonSerializer.Serialize(
                WritePolyline(multiLineString.Geometries.Cast<LineString>(), srid)),
            Polygon polygon => JsonSerializer.Serialize(WritePolygon([polygon], srid)),
            MultiPolygon multiPolygon => JsonSerializer.Serialize(
                WritePolygon(multiPolygon.Geometries.Cast<Polygon>(), srid)),
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for Esri query serialization.")
        };
    }

    private static Dictionary<string, object?> WritePoint(Point point, int? srid) {
        var payload = new Dictionary<string, object?> {
            ["x"] = point.X,
            ["y"] = point.Y
        };

        if (!double.IsNaN(point.Coordinate.Z)) {
            payload["z"] = point.Coordinate.Z;
        }

        AddSpatialReference(payload, srid);

        return payload;
    }

    private static Dictionary<string, object?> WriteMultiPoint(MultiPoint multiPoint, int? srid) {
        var points = multiPoint.Geometries
            .Cast<Point>()
            .Select(point => ToCoordinateValues(point.Coordinate))
            .ToList();

        var payload = new Dictionary<string, object?> {
            ["points"] = points
        };

        AddSpatialReference(payload, srid);

        return payload;
    }

    private static Dictionary<string, object?> WritePolyline(
        IEnumerable<LineString> lineStrings,
        int? srid) {
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
        int? srid) {
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

    private static void AddSpatialReference(IDictionary<string, object?> payload, int? srid) {
        if (srid is > 0) {
            payload["spatialReference"] = new Dictionary<string, object?> {
                ["wkid"] = srid.Value
            };
        }
    }
}