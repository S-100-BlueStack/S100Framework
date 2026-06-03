using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace S100FC.Topology
{
    internal static class PolygonVertexInserter
    {
        /// <summary>
        /// Finds all points on the polyline that lie exactly on the exterior ring of the polygon,
        /// then inserts those points as new vertices into the polygon's exterior ring.
        /// </summary>
        public static LineString InsertIntersectingPoints(this LineString exteriorRing, LineString polyline) {
            var pointsToInsert = new List<Coordinate>();

            // Check each vertex of the polyline
            foreach (var coord in polyline.Coordinates) {
                var point = exteriorRing.Factory.CreatePoint(coord);

                // Check if the point lies ON the exterior ring (not just inside polygon)
                if (exteriorRing.Distance(point) < 1e-10) {
                    // Ensure it's not already a vertex
                    if (!IsExistingVertex(exteriorRing, coord)) {
                        pointsToInsert.Add(coord);
                    }
                }
            }

            if (pointsToInsert.Count == 0)
                return exteriorRing; // Nothing to insert

            // Insert the new vertices into the exterior ring
            return InsertVerticesIntoRing(exteriorRing, pointsToInsert);

            //// Preserve interior rings (holes) unchanged
            //var interiorRings = new LinearRing[polygon.NumInteriorRings];
            //for (int i = 0; i < polygon.NumInteriorRings; i++)
            //    interiorRings[i] = (LinearRing)polygon.GetInteriorRingN(i);

            //return _geometryFactory.CreatePolygon(newExteriorRing, interiorRings);
        }

        /// <summary>
        /// Inserts new coordinates into a ring at the correct position
        /// (between the two existing vertices that the point lies between).
        /// </summary>
        private static LinearRing InsertVerticesIntoRing(LineString ring, List<Coordinate> newCoords) {
            var ringCoords = ring.Coordinates.ToList();

            // For each segment in the ring, check if any new coord lies on it
            // Walk backwards through segments so index insertions don't shift positions
            for (int i = ringCoords.Count - 2; i >= 0; i--) {
                var segStart = ringCoords[i];
                var segEnd = ringCoords[i + 1];

                // Find all new coords that lie on this segment, sorted by distance from segStart
                var onSegment = newCoords
                    .Where(c => IsPointOnSegment(segStart, segEnd, c))
                    .OrderBy(c => segStart.Distance(c))
                    .ToList();

                // Insert them in reverse order so earlier points stay in correct position
                foreach (var coord in Enumerable.Reverse(onSegment)) {
                    ringCoords.Insert(i + 1, coord);
                }
            }

            return ring.Factory.CreateLinearRing(ringCoords.ToArray());
        }

        private static bool IsExistingVertex(LineString ring, Coordinate coord) {
            return ring.Coordinates.Any(c => c.Distance(coord) < 1e-10);
        }

        /// <summary>
        /// Returns true if point C lies on segment AB (collinear and within bounds).
        /// </summary>
        private static bool IsPointOnSegment(Coordinate a, Coordinate b, Coordinate c) {
            // Cross product to check collinearity
            double cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (Math.Abs(cross) > 1e-10)
                return false;

            // Dot product to check if C is between A and B
            double dot = (c.X - a.X) * (b.X - a.X) + (c.Y - a.Y) * (b.Y - a.Y);
            if (dot < 0)
                return false;

            double lenSq = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            return dot <= lenSq;
        }
    }

    internal static class LineStringOverlapHandler
    {
        private const double Tolerance = 1e-9;

        /// <summary>
        /// Finds contiguous runs of shared vertices between two LineStrings.
        /// Returns each run as a LineString. This works even when NTS Intersection()
        /// fails due to non-collinear or floating-point imprecise shared segments.
        /// </summary>
        public static List<LineString> FindSharedSegments(this LineString a, LineString b) {
            var bCoordSet = new HashSet<Coordinate>(
                b.Coordinates,
                new TolerantCoordinateComparer(Tolerance)
            );

            var results = new List<LineString>();
            var currentRun = new List<Coordinate>();

            foreach (var coord in a.Coordinates) {
                if (bCoordSet.Contains(coord)) {
                    currentRun.Add(coord);
                }
                else {
                    if (currentRun.Count >= 2)
                        results.Add(a.Factory.CreateLineString(currentRun.ToArray()));
                    currentRun.Clear();
                }
            }

            // Flush final run
            if (currentRun.Count >= 2)
                results.Add(a.Factory.CreateLineString(currentRun.ToArray()));

            return results;
        }

        /// <summary>
        /// Inserts into LineString 'target' any vertices from 'other' that lie
        /// exactly on a segment of 'target' but aren't already vertices.
        /// </summary>
        public static LineString InsertMissingVertices(this LineString target, LineString other) {
            var targetCoords = target.Coordinates.ToList();
            var otherSet = new HashSet<Coordinate>(
                other.Coordinates,
                new TolerantCoordinateComparer(Tolerance)
            );

            // For each coord in 'other' not already in 'target', find its segment
            var toInsert = other.Coordinates
                .Where(c => !otherSet.Contains(c) == false) // all of other's coords
                .Where(c => !targetCoords.Any(tc => tc.Distance(c) < Tolerance))
                .ToList();

            foreach (var newCoord in toInsert) {
                int segIdx = FindSegmentIndex(targetCoords, newCoord);
                if (segIdx >= 0)
                    targetCoords.Insert(segIdx + 1, newCoord);
            }

            return target.Factory.CreateLineString(targetCoords.ToArray());
        }

        private static int FindSegmentIndex(List<Coordinate> coords, Coordinate point) {
            for (int i = 0; i < coords.Count - 1; i++) {
                var seg = new LineSegment(coords[i], coords[i + 1]);
                if (seg.Distance(point) < Tolerance)
                    return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// Coordinate equality comparer with configurable tolerance.
    /// Required because NTS Coordinate.Equals() is exact.
    /// </summary>
    public class TolerantCoordinateComparer : IEqualityComparer<Coordinate>
    {
        private readonly double _tolerance;

        public TolerantCoordinateComparer(double tolerance = 1e-9) {
            _tolerance = tolerance;
        }

        public bool Equals(Coordinate x, Coordinate y) =>
            Math.Abs(x.X - y.X) < _tolerance &&
            Math.Abs(x.Y - y.Y) < _tolerance;

        // Bucket by rounded value — must be consistent with Equals
        public int GetHashCode(Coordinate c) =>
            HashCode.Combine(
                Math.Round(c.X / _tolerance) * _tolerance,
                Math.Round(c.Y / _tolerance) * _tolerance
            );
    }
}

