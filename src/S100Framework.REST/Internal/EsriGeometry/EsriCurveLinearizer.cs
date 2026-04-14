using System.Text.Json;
using NetTopologySuite.Geometries;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriCurveLinearizer
{
    public static List<Coordinate> ReadCurvePath(
        JsonElement pathElement,
        int circularArcSegmentCount) {
        var coordinates = new List<Coordinate>();

        foreach (var segmentElement in pathElement.EnumerateArray()) {
            AppendSegment(coordinates, segmentElement, circularArcSegmentCount);
        }

        return coordinates;
    }

    public static List<Coordinate> ReadCurveRing(
        JsonElement ringElement,
        int circularArcSegmentCount) {
        var coordinates = ReadCurvePath(ringElement, circularArcSegmentCount);

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

    private static void AppendSegment(
        ICollection<Coordinate> coordinates,
        JsonElement segmentElement,
        int circularArcSegmentCount) {
        switch (segmentElement.ValueKind) {
            case JsonValueKind.Array:
                coordinates.Add(ReadCoordinate(segmentElement));
                return;

            case JsonValueKind.Object:
                AppendCurveObject(coordinates, segmentElement, circularArcSegmentCount);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported curve segment token kind '{segmentElement.ValueKind}'.");
        }
    }

    private static void AppendCurveObject(
        ICollection<Coordinate> coordinates,
        JsonElement curveObjectElement,
        int circularArcSegmentCount) {
        if (coordinates.Count == 0) {
            throw new InvalidOperationException(
                "A curve object cannot be the first segment in a path or ring.");
        }

        var from = coordinates.Last();

        if (curveObjectElement.TryGetProperty("c", out var circularArcElement)) {
            foreach (var coordinate in EsriCircularArcLinearizer.Linearize(from, circularArcElement, circularArcSegmentCount)) {
                coordinates.Add(coordinate);
            }

            return;
        }

        if (curveObjectElement.TryGetProperty("a", out _)) {
            throw new NotSupportedException(
                "The true curve segment type 'a' is not supported yet. Current support is limited to circular arcs represented by 'c'.");
        }

        if (curveObjectElement.TryGetProperty("b", out _)) {
            throw new NotSupportedException(
                "The true curve segment type 'b' is not supported yet. Current support is limited to circular arcs represented by 'c'.");
        }

        throw new NotSupportedException(
            "The true curve segment type is not supported yet. Current support is limited to circular arcs represented by 'c'.");
    }

    private static Coordinate ReadCoordinate(JsonElement pointElement) {
        if (pointElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException("A curve point must be represented as a coordinate array.");
        }

        var values = pointElement.EnumerateArray().ToArray();

        if (values.Length < 2) {
            throw new InvalidOperationException("A curve point must contain at least x and y values.");
        }

        var x = values[0].GetDouble();
        var y = values[1].GetDouble();

        if (values.Length >= 3 && values[2].ValueKind != JsonValueKind.Null) {
            return new CoordinateZ(x, y, values[2].GetDouble());
        }

        return new Coordinate(x, y);
    }

    private static Coordinate CreateCoordinate(double x, double y, double z) {
        return double.IsNaN(z)
            ? new Coordinate(x, y)
            : new CoordinateZ(x, y, z);
    }
}