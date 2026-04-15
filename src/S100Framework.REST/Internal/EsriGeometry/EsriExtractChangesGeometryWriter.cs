using System.Text.Json;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriExtractChangesGeometryWriter
{
    public static string WriteEnvelopeJson(Envelope envelope) {
        var payload = new Dictionary<string, object?> {
            ["xmin"] = envelope.MinX,
            ["ymin"] = envelope.MinY,
            ["xmax"] = envelope.MaxX,
            ["ymax"] = envelope.MaxY
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string WriteGeometryJson(Geometry geometry) {
        ArgumentNullException.ThrowIfNull(geometry);

        if (geometry.IsEmpty) {
            throw new InvalidOperationException("Empty geometries are not supported for extractChanges filters.");
        }

        var payload = geometry switch {
            Point point => WritePoint(point),
            MultiPoint multiPoint => WriteMultiPoint(multiPoint),
            LineString lineString => WritePolyline([lineString]),
            MultiLineString multiLineString => WritePolyline(multiLineString.Geometries.Cast<LineString>()),
            Polygon polygon => WritePolygon([polygon]),
            MultiPolygon multiPolygon => WritePolygon(multiPolygon.Geometries.Cast<Polygon>()),
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for extractChanges filters.")
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string GetGeometryType(Geometry geometry) {
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch {
            Point => "esriGeometryPoint",
            MultiPoint => "esriGeometryMultipoint",
            LineString => "esriGeometryPolyline",
            MultiLineString => "esriGeometryPolyline",
            Polygon => "esriGeometryPolygon",
            MultiPolygon => "esriGeometryPolygon",
            _ => throw new NotSupportedException(
                $"Geometry type '{geometry.GetType().Name}' is not supported for extractChanges filters.")
        };
    }

    private static Dictionary<string, object?> WritePoint(Point point) {
        return new Dictionary<string, object?> {
            ["x"] = point.X,
            ["y"] = point.Y
        };
    }

    private static Dictionary<string, object?> WriteMultiPoint(MultiPoint multiPoint) {
        return new Dictionary<string, object?> {
            ["points"] = multiPoint.Geometries
                .Cast<Point>()
                .Select(point => ToCoordinateValues(point.Coordinate))
                .ToList()
        };
    }

    private static Dictionary<string, object?> WritePolyline(IEnumerable<LineString> lineStrings) {
        return new Dictionary<string, object?> {
            ["paths"] = lineStrings
                .Select(line => line.Coordinates.Select(ToCoordinateValues).ToList())
                .ToList()
        };
    }

    private static Dictionary<string, object?> WritePolygon(IEnumerable<Polygon> polygons) {
        var rings = new List<List<double[]>>();

        foreach (var polygon in polygons) {
            rings.Add(ToRingValues(NormalizeRing((LinearRing)polygon.ExteriorRing, clockwise: true)));

            for (var index = 0; index < polygon.NumInteriorRings; index++) {
                rings.Add(ToRingValues(NormalizeRing((LinearRing)polygon.GetInteriorRingN(index), clockwise: false)));
            }
        }

        return new Dictionary<string, object?> {
            ["rings"] = rings
        };
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
}