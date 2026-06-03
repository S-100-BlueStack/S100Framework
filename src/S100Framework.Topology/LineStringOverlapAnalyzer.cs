using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace S100FC.Topology
{
    internal static partial class Extension
    {
        private const double Tolerance = 1e-9;

        /// <summary>
        /// Finds all contiguous runs of shared vertices between A and B.
        /// Returns the overlap as LineStrings, plus the entry/exit points
        /// where non-shared segments of B touch A (needing vertex insertion).
        /// </summary>
        public static OverlapResult LineStringOverlapAnalyzer(this LineString a, LineString b) {
            var aCoords = a.Coordinates;
            var bCoords = b.Coordinates;
            var aSet = new HashSet<Coordinate>(aCoords, new TolerantComparer(Tolerance));

            // 1. Find shared runs in B
            var sharedRuns = new List<SharedRun>();
            var currentRun = new List<(int bIdx, Coordinate coord)>();

            for (int i = 0; i < bCoords.Length; i++) {
                if (aSet.Contains(bCoords[i])) {
                    currentRun.Add((i, bCoords[i]));
                }
                else {
                    if (currentRun.Count >= 2)
                        sharedRuns.Add(new SharedRun(currentRun));
                    currentRun.Clear();
                }
            }
            if (currentRun.Count >= 2)
                sharedRuns.Add(new SharedRun(currentRun));

            // 2. Find vertices of B that lie ON a segment of A (but aren't already a vertex)
            //    These are the vertices that need inserting into A
            var aList = aCoords.ToList();
            var verticesToInsert = new List<InsertionPoint>();

            foreach (var bc in bCoords) {
                // Skip if already a vertex in A
                if (aSet.Contains(bc)) continue;

                for (int i = 0; i < aList.Count - 1; i++) {
                    if (LiesOnSegment(bc, aList[i], aList[i + 1], Tolerance)) {
                        verticesToInsert.Add(new InsertionPoint(bc, i));
                        break;
                    }
                }
            }

            // 3. Build updated A with insertions
            // Insert in reverse order so indices stay valid
            foreach (var ins in verticesToInsert.OrderByDescending(x => x.SegmentIndex))
                aList.Insert(ins.SegmentIndex + 1, ins.Coordinate);

            return new OverlapResult(sharedRuns, verticesToInsert, aList.ToArray());
        }

        private static bool LiesOnSegment(Coordinate p, Coordinate a, Coordinate b, double tol) {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double px = p.X - a.X, py = p.Y - a.Y;
            double cross = dx * py - dy * px;
            if (Math.Abs(cross) > tol) return false;   // not collinear
            double dot = px * dx + py * dy;
            double lenSq = dx * dx + dy * dy;
            return dot >= -tol && dot <= lenSq + tol;   // within segment bounds
        }
    }

    public class TolerantComparer : IEqualityComparer<Coordinate>
    {
        private readonly double _tolerance;

        public TolerantComparer(double tolerance = 1e-9) {
            _tolerance = tolerance;
        }

        public bool Equals(Coordinate x, Coordinate y) {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return Math.Abs(x.X - y.X) < _tolerance &&
                   Math.Abs(x.Y - y.Y) < _tolerance;
        }

        public int GetHashCode(Coordinate c) {
            // Bucket coordinates by rounding to the tolerance scale
            // Must be consistent with Equals — two coords that are Equal must produce the same hash
            double scale = 1.0 / _tolerance;
            long xBucket = (long)Math.Round(c.X * scale);
            long yBucket = (long)Math.Round(c.Y * scale);
            return HashCode.Combine(xBucket, yBucket);
        }
    }

    public record SharedRun(List<(int bIdx, Coordinate coord)> Vertices)
    {
        public Coordinate Start => Vertices[0].coord;
        public Coordinate End => Vertices[^1].coord;
        public int Count => Vertices.Count;
    }

    public record InsertionPoint(Coordinate Coordinate, int SegmentIndex);

    public record OverlapResult(
        List<SharedRun> SharedRuns,
        List<InsertionPoint> InsertedVertices,
        Coordinate[] UpdatedACoordinates);
}
