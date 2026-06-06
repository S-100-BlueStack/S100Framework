using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace S100FC.Topology
{
    internal static partial class Extension
    {
        /// <summary>
        /// Finds all points on the polyline that lie exactly on the exterior ring of the polygon,
        /// then inserts those points as new vertices into the polygon's exterior ring.
        /// </summary>
        public static LineString InsertMissingVertices(this LineString target, LineString other) {
            var targetCoords = target.Coordinates.ToList();
            var targetSet = new HashSet<Coordinate>(targetCoords, new TolerantComparer(Tolerance));

            // Find B vertices that lie ON a segment of A but aren't already a vertex
            var insertions = new List<(int segmentIndex, double t, Coordinate coord)>();

            foreach (var coord in other.Coordinates) {
                if (targetSet.Contains(coord)) continue;

                for (int i = 0; i < targetCoords.Count - 1; i++) {
                    var seg = new LineSegment(targetCoords[i], targetCoords[i + 1]);
                    if (seg.Distance(coord) < Tolerance) {
                        // t is the fractional position along the segment [0..1]
                        // used to order multiple insertions on the same segment
                        double t = seg.ProjectionFactor(coord);
                        insertions.Add((i, t, coord));
                        break;
                    }
                }
            }

            // Sort: by segment index first, then by position along that segment
            // Insert in reverse so earlier indices stay valid as we insert
            foreach (var ins in insertions.OrderByDescending(x => x.segmentIndex)
                                          .ThenByDescending(x => x.t)) {
                targetCoords.Insert(ins.segmentIndex + 1, ins.coord);
            }

            return target.Factory.CreateLineString(targetCoords.ToArray());
        }

    }
}

