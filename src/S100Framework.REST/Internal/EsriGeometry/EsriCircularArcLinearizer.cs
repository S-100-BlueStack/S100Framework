using System.Text.Json;
using NetTopologySuite.Geometries;

namespace S100Framework.REST.Internal.EsriGeometry;

internal static class EsriCircularArcLinearizer
{
    private const double Epsilon = 1e-12;
    private const double TwoPi = Math.PI * 2.0;

    public static IReadOnlyList<Coordinate> Linearize(
        Coordinate from,
        JsonElement circularArcElement,
        int segmentCount) {
        if (circularArcElement.ValueKind != JsonValueKind.Array || circularArcElement.GetArrayLength() != 2) {
            throw new InvalidOperationException(
                "A circular arc segment must contain an end point and an interior point.");
        }

        var elements = circularArcElement.EnumerateArray().ToArray();

        var to = ReadCoordinate(elements[0]);
        var interior = ReadInteriorPoint(elements[1]);

        if (!TryComputeCircle(from, interior, to, out var centerX, out var centerY, out var radius)) {
            return [to];
        }

        var startAngle = Math.Atan2(from.Y - centerY, from.X - centerX);
        var midAngle = Math.Atan2(interior.Y - centerY, interior.X - centerX);
        var endAngle = Math.Atan2(to.Y - centerY, to.X - centerX);

        var ccwToMid = NormalizeAngle(midAngle - startAngle);
        var ccwToEnd = NormalizeAngle(endAngle - startAngle);

        var useCounterClockwise = ccwToMid <= ccwToEnd + Epsilon;
        var sweep = useCounterClockwise
            ? ccwToEnd
            : ccwToEnd - TwoPi;

        var effectiveSegmentCount = Math.Max(2, segmentCount);
        var coordinates = new List<Coordinate>(effectiveSegmentCount);

        for (var index = 1; index <= effectiveSegmentCount; index++) {
            var fraction = (double)index / effectiveSegmentCount;

            if (index == effectiveSegmentCount) {
                coordinates.Add(to);
                continue;
            }

            var angle = startAngle + (sweep * fraction);
            var x = centerX + (radius * Math.Cos(angle));
            var y = centerY + (radius * Math.Sin(angle));

            var z = double.IsNaN(from.Z) || double.IsNaN(to.Z)
                ? double.NaN
                : from.Z + ((to.Z - from.Z) * fraction);

            coordinates.Add(CreateCoordinate(x, y, z));
        }

        return coordinates;
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

    private static Coordinate ReadInteriorPoint(JsonElement pointElement) {
        if (pointElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException("An arc interior point must be represented as a coordinate array.");
        }

        var values = pointElement.EnumerateArray().ToArray();

        if (values.Length < 2) {
            throw new InvalidOperationException("An arc interior point must contain at least x and y values.");
        }

        return new Coordinate(values[0].GetDouble(), values[1].GetDouble());
    }

    private static bool TryComputeCircle(
        Coordinate from,
        Coordinate interior,
        Coordinate to,
        out double centerX,
        out double centerY,
        out double radius) {
        centerX = default;
        centerY = default;
        radius = default;

        var x1 = from.X;
        var y1 = from.Y;
        var x2 = interior.X;
        var y2 = interior.Y;
        var x3 = to.X;
        var y3 = to.Y;

        var determinant = 2.0 *
                          ((x1 * (y2 - y3)) +
                           (x2 * (y3 - y1)) +
                           (x3 * (y1 - y2)));

        if (Math.Abs(determinant) < Epsilon) {
            return false;
        }

        var x1SqPlusY1Sq = (x1 * x1) + (y1 * y1);
        var x2SqPlusY2Sq = (x2 * x2) + (y2 * y2);
        var x3SqPlusY3Sq = (x3 * x3) + (y3 * y3);

        centerX =
            ((x1SqPlusY1Sq * (y2 - y3)) +
             (x2SqPlusY2Sq * (y3 - y1)) +
             (x3SqPlusY3Sq * (y1 - y2))) / determinant;

        centerY =
            ((x1SqPlusY1Sq * (x3 - x2)) +
             (x2SqPlusY2Sq * (x1 - x3)) +
             (x3SqPlusY3Sq * (x2 - x1))) / determinant;

        radius = Math.Sqrt(Math.Pow(x1 - centerX, 2) + Math.Pow(y1 - centerY, 2));
        return true;
    }

    private static double NormalizeAngle(double angle) {
        var normalized = angle % TwoPi;

        if (normalized < 0) {
            normalized += TwoPi;
        }

        return normalized;
    }

    private static Coordinate CreateCoordinate(double x, double y, double z) {
        return double.IsNaN(z)
            ? new Coordinate(x, y)
            : new CoordinateZ(x, y, z);
    }
}