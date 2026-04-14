using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using System.Text.Json;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriGeometryReader
{
    public static Geometry? Read(
        JsonElement geometryElement,
        string? geometryType,
        int? defaultSrid,
        bool preferLatestWkid,
        bool fixInvalidGeometries) {
        if (geometryElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) {
            return null;
        }

        var srid = ReadSrid(geometryElement, defaultSrid, preferLatestWkid);
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: srid ?? 0);

        Geometry geometry = geometryType switch {
            "esriGeometryPoint" => ReadPoint(geometryElement, factory),
            "esriGeometryMultipoint" => ReadMultipoint(geometryElement, factory),
            "esriGeometryPolyline" => ReadPolyline(geometryElement, factory),
            "esriGeometryPolygon" => ReadPolygon(geometryElement, factory),
            "esriGeometryEnvelope" => ReadEnvelope(geometryElement, factory),
            null => InferGeometry(geometryElement, factory),
            _ => throw new NotSupportedException($"Unsupported geometry type '{geometryType}'.")
        };

        if (fixInvalidGeometries && !geometry.IsValid) {
            geometry = GeometryFixer.Fix(geometry);
            geometry.SRID = factory.SRID;
        }

        return geometry;
    }

    private static Geometry InferGeometry(JsonElement geometryElement, GeometryFactory factory) {
        if (geometryElement.TryGetProperty("x", out _)) {
            return ReadPoint(geometryElement, factory);
        }

        if (geometryElement.TryGetProperty("points", out _)) {
            return ReadMultipoint(geometryElement, factory);
        }

        if (geometryElement.TryGetProperty("paths", out _)) {
            return ReadPolyline(geometryElement, factory);
        }

        if (geometryElement.TryGetProperty("rings", out _)) {
            return ReadPolygon(geometryElement, factory);
        }

        if (geometryElement.TryGetProperty("xmin", out _)) {
            return ReadEnvelope(geometryElement, factory);
        }

        throw new NotSupportedException("Could not infer Esri geometry type from JSON payload.");
    }

    private static Geometry ReadPoint(JsonElement element, GeometryFactory factory) {
        if (!TryGetDouble(element, "x", out var x) || !TryGetDouble(element, "y", out var y)) {
            return factory.CreatePoint();
        }

        if (TryGetDouble(element, "z", out var z)) {
            return factory.CreatePoint(new CoordinateZ(x, y, z));
        }

        return factory.CreatePoint(new Coordinate(x, y));
    }

    private static Geometry ReadMultipoint(JsonElement element, GeometryFactory factory) {
        if (!element.TryGetProperty("points", out var pointsElement) || pointsElement.GetArrayLength() == 0) {
            return factory.CreateMultiPoint();
        }

        var points = new List<Point>();

        foreach (var pointElement in pointsElement.EnumerateArray()) {
            points.Add(factory.CreatePoint(ReadCoordinate(pointElement)));
        }

        return factory.CreateMultiPoint(points.ToArray());
    }

    private static Geometry ReadPolyline(JsonElement element, GeometryFactory factory) {
        if (element.TryGetProperty("curvePaths", out _)) {
            throw new NotSupportedException(
    "True curve geometries are not supported for direct parsing. Configure the client to request densified geometries by keeping ReturnTrueCurves disabled.");
        }

        if (!element.TryGetProperty("paths", out var pathsElement) || pathsElement.GetArrayLength() == 0) {
            return factory.CreateMultiLineString();
        }

        var lineStrings = new List<LineString>();

        foreach (var pathElement in pathsElement.EnumerateArray()) {
            var coordinates = ReadCoordinateArray(pathElement);

            if (coordinates.Count < 2) {
                continue;
            }

            lineStrings.Add(factory.CreateLineString(coordinates.ToArray()));
        }

        return lineStrings.Count switch {
            0 => factory.CreateMultiLineString(),
            1 => lineStrings[0],
            _ => factory.CreateMultiLineString(lineStrings.ToArray())
        };
    }

    private static Geometry ReadPolygon(JsonElement element, GeometryFactory factory) {
        if (element.TryGetProperty("curveRings", out _)) {
            throw new NotSupportedException(
    "True curve geometries are not supported for direct parsing. Configure the client to request densified geometries by keeping ReturnTrueCurves disabled.");
        }

        if (!element.TryGetProperty("rings", out var ringsElement) || ringsElement.GetArrayLength() == 0) {
            return factory.CreatePolygon();
        }

        var rings = new List<LinearRing>();

        foreach (var ringElement in ringsElement.EnumerateArray()) {
            var coordinates = NormalizeRing(ReadCoordinateArray(ringElement));

            if (coordinates.Count < 4) {
                continue;
            }

            rings.Add(factory.CreateLinearRing(coordinates.ToArray()));
        }

        if (rings.Count == 0) {
            return factory.CreatePolygon();
        }

        var shells = new List<PolygonShell>();
        var holes = new List<LinearRing>();

        foreach (var ring in rings) {
            if (Orientation.IsCCW(ring.Coordinates)) {
                holes.Add(ring);
            }
            else {
                shells.Add(new PolygonShell(ring));
            }
        }

        if (shells.Count == 0) {
            shells.Add(new PolygonShell(rings[0]));
            holes = rings.Skip(1).ToList();
        }

        foreach (var hole in holes) {
            var assigned = false;

            foreach (var shell in shells.OrderBy(GetApproximateArea)) {
                var shellPolygon = factory.CreatePolygon(shell.Shell);

                if (shellPolygon.Covers(factory.CreatePoint(hole.Coordinate))) {
                    shell.Holes.Add(hole);
                    assigned = true;
                    break;
                }
            }

            if (!assigned) {
                shells[0].Holes.Add(hole);
            }
        }

        var polygons = shells
            .Select(shell => factory.CreatePolygon(shell.Shell, shell.Holes.ToArray()))
            .ToArray();

        return polygons.Length == 1
            ? polygons[0]
            : factory.CreateMultiPolygon(polygons);
    }

    private static Geometry ReadEnvelope(JsonElement element, GeometryFactory factory) {
        if (!TryGetDouble(element, "xmin", out var xmin) ||
            !TryGetDouble(element, "ymin", out var ymin) ||
            !TryGetDouble(element, "xmax", out var xmax) ||
            !TryGetDouble(element, "ymax", out var ymax)) {
            return factory.CreatePolygon();
        }

        var coordinates = new[]
        {
            new Coordinate(xmin, ymin),
            new Coordinate(xmax, ymin),
            new Coordinate(xmax, ymax),
            new Coordinate(xmin, ymax),
            new Coordinate(xmin, ymin)
        };

        return factory.CreatePolygon(coordinates);
    }

    private static List<Coordinate> ReadCoordinateArray(JsonElement arrayElement) {
        var coordinates = new List<Coordinate>();

        foreach (var pointElement in arrayElement.EnumerateArray()) {
            coordinates.Add(ReadCoordinate(pointElement));
        }

        return coordinates;
    }

    private static Coordinate ReadCoordinate(JsonElement pointElement) {
        if (pointElement.ValueKind == JsonValueKind.Array) {
            var values = pointElement.EnumerateArray().ToArray();

            if (values.Length < 2) {
                throw new InvalidOperationException("Esri coordinate array must contain at least x and y values.");
            }

            var x = values[0].GetDouble();
            var y = values[1].GetDouble();

            if (values.Length >= 3 && values[2].ValueKind != JsonValueKind.Null) {
                return new CoordinateZ(x, y, values[2].GetDouble());
            }

            return new Coordinate(x, y);
        }

        if (TryGetDouble(pointElement, "x", out var objectX) &&
            TryGetDouble(pointElement, "y", out var objectY)) {
            if (TryGetDouble(pointElement, "z", out var objectZ)) {
                return new CoordinateZ(objectX, objectY, objectZ);
            }

            return new Coordinate(objectX, objectY);
        }

        throw new InvalidOperationException("Unsupported point representation in Esri geometry payload.");
    }

    private static List<Coordinate> NormalizeRing(List<Coordinate> coordinates) {
        if (coordinates.Count == 0) {
            return coordinates;
        }

        var first = coordinates[0];
        var last = coordinates[^1];

        if (!first.Equals2D(last)) {
            coordinates.Add(CreateCoordinate(first.X, first.Y, first.Z));
        }

        return coordinates;
    }

    private static Coordinate CreateCoordinate(double x, double y, double z) {
        return double.IsNaN(z)
            ? new Coordinate(x, y)
            : new CoordinateZ(x, y, z);
    }

    private static int? ReadSrid(JsonElement geometryElement, int? defaultSrid, bool preferLatestWkid) {
        if (!geometryElement.TryGetProperty("spatialReference", out var spatialReference)) {
            return defaultSrid;
        }

        if (preferLatestWkid &&
            spatialReference.TryGetProperty("latestWkid", out var latestWkid) &&
            latestWkid.ValueKind == JsonValueKind.Number &&
            latestWkid.TryGetInt32(out var latest)) {
            return latest;
        }

        if (spatialReference.TryGetProperty("wkid", out var wkid) &&
            wkid.ValueKind == JsonValueKind.Number &&
            wkid.TryGetInt32(out var current)) {
            return current;
        }

        return defaultSrid;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value) {
        value = default;

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number) {
            return false;
        }

        return property.TryGetDouble(out value);
    }

    private static double GetApproximateArea(PolygonShell shell) {
        return Math.Abs(Area.OfRing(shell.Shell.Coordinates));
    }

    private sealed class PolygonShell
    {
        public PolygonShell(LinearRing shell) {
            Shell = shell;
        }

        public LinearRing Shell { get; }

        public List<LinearRing> Holes { get; } = [];
    }
}