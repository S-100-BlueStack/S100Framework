using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace S100Framework.Topology.Internal
{
    public enum GeometryKind
    {
        Polygon,
        LineString,
    }

    /// <summary>
    /// A registered geometry in the network — either a Polygon or a LineString.
    /// </summary>
    public class NetworkGeometry
    {
        public int Id { get; init; }
        public Geometry Geometry { get; init; }
        public GeometryKind Kind { get; init; }
    }

    /// <summary>
    /// A node in the planar network.
    /// </summary>
    public class NetworkNode
    {
        public Coordinate Coordinate { get; init; }

        // All edges incident to this node
        public List<NetworkEdge> Edges { get; } = new();

        public int Degree => Edges.Count;
    }

    /// <summary>
    /// An edge in the planar network.
    /// </summary>
    public class NetworkEdge
    {
        public int Id { get; init; }
        public LineString Geometry { get; init; }
        public Coordinate StartNode { get; init; }
        public Coordinate EndNode { get; init; }

        // Which registered geometries contributed this edge
        public HashSet<int> SourceGeometryIds { get; } = new();

        // sourceId -> true if that source traverses StartNode -> EndNode (forward)
        public Dictionary<int, bool> SourceOrientation { get; } = new();

        public bool IsShared => SourceGeometryIds.Count > 1;
    }

    /// <summary>
    /// The merged result of one or more network edges stitched into a single
    /// LineString, respecting source-geometry boundaries.
    /// </summary>
    public class MergedEdge
    {
        public LineString Geometry { get; init; }
        public HashSet<int> SourceGeometryIds { get; init; }
        public List<NetworkEdge> ConstituentEdges { get; init; }

        public bool IsShared => SourceGeometryIds.Count > 1;
        public double Length => Geometry.Length;
        public int SegmentCount => ConstituentEdges.Count;
    }

    /// <summary>
    /// The full topology definition of a single source geometry within the network.
    /// </summary>
    public class NetworkDefinition
    {
        public int SourceId { get; init; }
        public NetworkGeometry Source { get; init; }
        public List<NetworkEdge> AllEdges { get; init; }
        public List<NetworkEdge> SharedEdges { get; init; }
        public List<NetworkEdge> PrivateEdges { get; init; }
        public List<NetworkGeometry> Neighbours { get; init; }

        public double TotalLength => AllEdges.Sum(e => e.Geometry.Length);
        public double SharedLength => SharedEdges.Sum(e => e.Geometry.Length);
    }

    public enum EdgeRingKind
    {
        ExteriorRing,
        InteriorRing,
        LineString,
        Mixed,
    }

    public class MergedEdgeClassification
    {
        public EdgeRingKind Kind { get; init; }
        public int? RingIndex { get; init; }
        public NetworkGeometry Source { get; init; }
    }

    /// <summary>
    /// Bucketed coordinate key for tolerance-based node identity.
    /// Two coordinates that snap to the same grid cell produce the same key.
    /// </summary>
    public readonly struct CoordinateKey : IEquatable<CoordinateKey>, IComparable<CoordinateKey>
    {
        private readonly long _x, _y;

        /// <summary>
        /// Construct from an ALREADY-SNAPPED coordinate + the tolerance used to snap it.
        /// </summary>
        public CoordinateKey(Coordinate snappedCoord, double tolerance) {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(snappedCoord.X * inv);
            _y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y) {
            _x = x;
            _y = y;
        }

        public CoordinateKey Offset(int dx, int dy) => new CoordinateKey(_x + dx, _y + dy);

        public bool Equals(CoordinateKey other) => _x == other._x && _y == other._y;
        public override bool Equals(object obj) => obj is CoordinateKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(_x, _y);

        public int CompareTo(CoordinateKey other) {
            int c = _x.CompareTo(other._x);
            return c != 0 ? c : _y.CompareTo(other._y);
        }
    }

    /// <summary>
    /// A raw segment: just two coordinates + metadata. No Geometry object allocated.
    /// </summary>
    internal sealed class RawSegment
    {
        public readonly int SegId;
        public readonly Coordinate P0, P1;
        public readonly int SourceId;
        public readonly bool OriginallyReversed; // true if source digitized P1->P0
        public readonly Envelope Envelope;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId, bool reversed) {
            SegId = segId;
            P0 = p0;
            P1 = p1;
            SourceId = sourceId;
            OriginallyReversed = reversed;
            Envelope = new Envelope(p0, p1);
        }

        public RawSegment WithCoordinates(Coordinate p0, Coordinate p1)
            => new RawSegment(SegId, p0, p1, SourceId, OriginallyReversed);

        public RawSegment WithSegId(int segId)
            => new RawSegment(segId, P0, P1, SourceId, OriginallyReversed);
    }

    /// <summary>
    /// An intersection point on a segment, ordered by distance from segment start.
    /// </summary>
    internal readonly struct SplitPoint
    {
        public readonly Coordinate Coord;
        private readonly double _distFromStart;

        public SplitPoint(Coordinate coord, Coordinate segStart) {
            Coord = coord;
            _distFromStart = coord.Distance(segStart);
        }

        public static readonly IComparer<SplitPoint> Comparer =
            Comparer<SplitPoint>.Create((a, b) => a._distFromStart.CompareTo(b._distFromStart));
    }

    /// <summary>
    /// Builds a planar topology network from a mix of Polygons and LineStrings.
    /// Shared boundary segments between geometries are detected and represented
    /// as single edges tagged with all contributing source geometry ids.
    /// </summary>
    public class MixedTopologyNetwork
    {
        private readonly GeometryFactory _factory;
        private readonly double _snapTolerance;
        private readonly double _dedupeRadius;

        private readonly List<NetworkGeometry> _sources = new();
        private readonly List<NetworkEdge> _edges = new();
        private readonly Dictionary<CoordinateKey, NetworkNode> _nodes = new();
        private readonly Dictionary<int, List<NetworkEdge>> _edgesBySource = new();
        private STRtree<NetworkEdge> _edgeIndex;

        public IReadOnlyList<NetworkEdge> Edges => _edges;
        public IReadOnlyCollection<NetworkNode> Nodes => _nodes.Values;

        public IEnumerable<int> Sources => this._sources.Select(e => e.Id);

        public MixedTopologyNetwork(GeometryFactory? factory = default, double snapTolerance = 0.000001, double dedupeRadius = -1) {
            _factory = factory ?? NtsGeometryServices.Instance.CreateGeometryFactory();
            _snapTolerance = snapTolerance;

            // Default dedupe radius: a generous multiple of snapTolerance to absorb
            // typical digitizing noise (duplicate vertices a few ULPs apart), while
            // staying well below any real feature size.
            _dedupeRadius = dedupeRadius > 0 ? dedupeRadius : snapTolerance * 5;            
        }

        // -------------------------------------------------------------------
        // Registration
        // -------------------------------------------------------------------

        public int AddPolygon(Polygon polygon) => Register(polygon, GeometryKind.Polygon);

        public int AddLineString(LineString lineString) => Register(lineString, GeometryKind.LineString);

        public void AddPolygons(IEnumerable<Polygon> polygons) {
            foreach (var p in polygons) AddPolygon(p);
        }

        public void AddLineStrings(IEnumerable<LineString> lines) {
            foreach (var l in lines) AddLineString(l);
        }

        private int Register(Geometry geom, GeometryKind kind) {
            int id = _sources.Count;
            _sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        /// <summary>
        /// Returns true if a LineString's extent is smaller than the snap tolerance,
        /// meaning every coordinate would collapse to the same canonical point and
        /// the geometry would become degenerate (zero-length / null) after Build().
        /// </summary>
        private bool IsBelowSnapTolerance(LineString line) {
            var env = line.EnvelopeInternal;

            // Diagonal of the bounding box — if this is <= tolerance, every
            // coordinate is within tolerance of every other coordinate.
            double diagonal = Math.Sqrt(
                env.Width * env.Width + env.Height * env.Height);

            return diagonal <= _snapTolerance;
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------

        public void Build() {
            // Step 1: Extract raw segments per source (bare coordinates)
            var rawSegments = ExtractRawSegments();

            // Step 2: Canonicalize coordinates (union-find over adjacent grid cells)
            //         and remap every segment's endpoints to canonical coordinates.
            //         Drop any segment that becomes degenerate (zero-length)
            //         AFTER canonicalization.
            var canonical = BuildCanonicalCoordinateMap(rawSegments);

            var cleanedSegments = new List<RawSegment>(rawSegments.Count);
            foreach (var seg in rawSegments) {
                var p0 = canonical[new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance)];
                var p1 = canonical[new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance)];

                if (p0.Equals2D(p1)) continue; // degenerate after canonicalization

                cleanedSegments.Add(seg.WithCoordinates(p0, p1));
            }

            // Re-assign sequential SegIds — the intersection loop below relies on
            // "other.SegId >= seg.SegId" to process each pair once, so ids must
            // be dense/contiguous after we dropped some segments.
            var segments = new List<RawSegment>(cleanedSegments.Count);
            for (int i = 0; i < cleanedSegments.Count; i++)
                segments.Add(cleanedSegments[i].WithSegId(i));

            // Step 3: Spatial index over cleaned segments
            var segIndex = new STRtree<RawSegment>();
            foreach (var seg in segments)
                segIndex.Insert(seg.Envelope, seg);

            // Step 4: Local intersection detection — collect split points per segment
            var splitPoints = new Dictionary<int, SortedSet<SplitPoint>>(segments.Count);
            for (int i = 0; i < segments.Count; i++)
                splitPoints[i] = new SortedSet<SplitPoint>(SplitPoint.Comparer);

            var li = new RobustLineIntersector();

            foreach (var seg in segments) {
                var queryEnv = seg.Envelope.Copy();
                queryEnv.ExpandBy(_snapTolerance);

                var candidates = segIndex.Query(queryEnv);
                foreach (var other in candidates) {
                    if (other.SegId >= seg.SegId) continue; // process each pair once

                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;

                    for (int k = 0; k < li.IntersectionNum; k++) {
                        var pt = li.GetIntersection(k);

                        // Canonicalize the computed intersection point too —
                        // it's a brand-new floating-point coordinate that must
                        // land on the same grid as everything else.
                        pt = SnapToGrid(pt);
                        var ptKey = new CoordinateKey(pt, _snapTolerance);
                        if (canonical.TryGetValue(ptKey, out var canonPt))
                            pt = canonPt;

                        // Snap to endpoint if within tolerance
                        pt = SnapToEndpoint(pt, seg, other);

                        splitPoints[seg.SegId].Add(new SplitPoint(pt, seg.P0));
                        splitPoints[other.SegId].Add(new SplitPoint(pt, other.P0));
                    }
                }
            }

            // Step 5: Split each segment at its intersection points and insert
            //         the resulting sub-segments into the graph
            _nodes.Clear();
            _edges.Clear();
            _edgesBySource.Clear();

            var edgeMap = new Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge>();

            foreach (var seg in segments) {
                var splits = splitPoints[seg.SegId];

                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits) {
                    if (!sp.Coord.Equals2D(coords[^1], _snapTolerance))
                        coords.Add(sp.Coord);
                }
                if (!seg.P1.Equals2D(coords[^1], _snapTolerance))
                    coords.Add(seg.P1);

                for (int i = 0; i < coords.Count - 1; i++) {
                    if (coords[i].Equals2D(coords[i + 1], _snapTolerance))
                        continue; // skip degenerate sub-segments from split noise

                    InsertEdge(coords[i], coords[i + 1], seg.SourceId, seg.OriginallyReversed, edgeMap);
                }
            }

            // Step 6: Spatial index on final edges
            _edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in _edges)
                _edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }

        // -------------------------------------------------------------------
        // Step 1: Extract line segments tagged with their source id
        // -------------------------------------------------------------------

        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(EstimateSegmentCount());
            int segId = 0;

            foreach (var src in _sources) {
                if (src.Kind == GeometryKind.Polygon) {
                    var poly = (Polygon)src.Geometry;
                    segId = ExtractFromRing(poly.ExteriorRing.Coordinates, src.Id, result, segId, isRing: true);
                    foreach (var hole in poly.InteriorRings)
                        segId = ExtractFromRing(hole.Coordinates, src.Id, result, segId, isRing: true);
                }
                else {
                    segId = ExtractFromRing(src.Geometry.Coordinates, src.Id, result, segId, isRing: src.Geometry is LinearRing);
                }
            }

            return result;
        }



        private int EstimateSegmentCount() => _sources.Sum(s => s.Geometry.NumPoints);

        /// <summary>
        /// Snaps + de-duplicates a coordinate sequence, re-closes rings if needed,
        /// and emits direction-canonicalized RawSegments (smaller coordinate first).
        /// </summary>
        //private int ExtractFromRing(Coordinate[] coords, int sourceId, List<RawSegment> result, int segId, bool isRing = true) {
        //    //if (sourceId == 104) System.Diagnostics.Debugger.Break();

        //    // Pre-clean: remove near-duplicate "spike" vertices using a tolerance
        //    // matched to source data precision, separate from the fine _snapTolerance
        //    // used for the rest of the network build.
        //    coords = RemoveSpikes(coords, spikeTolerance: _snapTolerance);

        //    // Snap + de-duplicate consecutive identical coordinates that
        //    // collapse together after snapping
        //    var snapped = new List<Coordinate>(coords.Length);
        //    foreach (var c in coords) {
        //        var sc = SnapToGrid(c);
        //        if (snapped.Count == 0 || !sc.Equals2D(snapped[^1]))
        //            snapped.Add(sc);
        //    }

        //    // Re-close the ring if snapping broke closure
        //    if (isRing && snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
        //        snapped.Add(snapped[0]);

        //    for (int i = 0; i < snapped.Count - 1; i++) {
        //        var p0 = snapped[i];
        //        var p1 = snapped[i + 1];

        //        if (p0.Equals2D(p1)) continue; // degenerate after snapping

        //        // Canonicalize direction: always store the "smaller" coordinate first
        //        bool reversed = ComparePoints(p0, p1) > 0;
        //        var (c0, c1) = reversed ? (p1, p0) : (p0, p1);

        //        result.Add(new RawSegment(segId++, c0, c1, sourceId, reversed));
        //    }

        //    return segId;
        //}
        private int ExtractFromRing(Coordinate[] coords, int sourceId, List<RawSegment> result, int segId, bool isRing = true) {
            coords = RemoveSpikes(coords, spikeTolerance: _snapTolerance);

            // For rings, strip the closing duplicate BEFORE snapping so the
            // re-close logic below is always the one that adds it back.
            // This prevents the dedup loop from silently eating the last point
            // when snapped-last == snapped-first.
            if (isRing && coords.Length > 1) {
                var snapFirst = SnapToGrid(coords[0]);
                var snapLast = SnapToGrid(coords[^1]);
                if (snapFirst.Equals2D(snapLast))
                    coords = coords[..^1]; // strip closing duplicate
            }

            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1]))
                    snapped.Add(sc);
            }

            // Re-close the ring
            if (isRing && snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(snapped[0]);

            for (int i = 0; i < snapped.Count - 1; i++) {
                var p0 = snapped[i];
                var p1 = snapped[i + 1];
                if (p0.Equals2D(p1)) continue;

                bool reversed = ComparePoints(p0, p1) > 0;
                var (c0, c1) = reversed ? (p1, p0) : (p0, p1);
                result.Add(new RawSegment(segId++, c0, c1, sourceId, reversed));
            }

            return segId;
        }

        /// <summary>
        /// Removes "spike" vertices — a vertex whose neighbors on both sides are
        /// within distSnapTolerance, indicating a near-duplicate point that
        /// creates a degenerate zero-area excursion.
        /// </summary>
        private Coordinate[] RemoveSpikes(Coordinate[] coords, double spikeTolerance) {
            if (coords.Length < 4) return coords;

            var result = new List<Coordinate> { coords[0] };
            for (int i = 1; i < coords.Length - 1; i++) {
                // Skip this vertex if it's within spikeTolerance of either
                // the previous kept vertex or the next vertex
                if (result[^1].Distance(coords[i]) <= spikeTolerance)
                    continue; // collapses into previous point — skip

                result.Add(coords[i]);
            }
            result.Add(coords[^1]);
            return result.ToArray();
        }

        private static int ComparePoints(Coordinate a, Coordinate b) {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        // -------------------------------------------------------------------
        // Snapping / canonicalization helpers
        // -------------------------------------------------------------------
        private Coordinate SnapToGrid(Coordinate c) => SnapToGridRound(c);

        /// <summary>
        /// Snaps a coordinate to a fixed grid defined by _snapTolerance.
        /// Floor-based with a small epsilon to avoid boundary-straddling issues.
        /// </summary>
        private Coordinate SnapToGridFloor(Coordinate c) {
            double inv = 1.0 / _snapTolerance;
            const double epsilon = 1e-9;

            double x = Math.Floor(c.X * inv + epsilon) * _snapTolerance;
            double y = Math.Floor(c.Y * inv + epsilon) * _snapTolerance;

            return double.IsNaN(c.Z)
                ? new Coordinate(x, y)
                : new CoordinateZ(x, y, c.Z);
        }

        /// <summary>
        /// Snaps a coordinate to a fixed grid defined by tolerance.
        /// </summary>
        private Coordinate SnapToGridRound(Coordinate c) {
            double inv = 1.0 / _snapTolerance;
            double x = Math.Round(c.X * inv) / inv;
            double y = Math.Round(c.Y * inv) / inv;

            // Preserve Z if present
            var coord = double.IsNaN(c.Z)
                ? new Coordinate(x, y)
                : new CoordinateZ(x, y, c.Z);
            _factory.PrecisionModel.MakePrecise(coord);
            return coord;
        }

        /// <summary>
        /// Builds a map from EVERY grid cell touched by input coordinates to ONE
        /// canonical coordinate. Adjacent cells (within 1 cell of each other) that
        /// are also within snapTolerance in real distance collapse to the same
        /// canonical coordinate using a union-find–style merge.
        /// </summary>
#if null
        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments)
        {
            // Step 1: collect distinct snapped coordinates per cell (first-seen wins
            // as the "raw" value for that cell)
            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments)
            {
                foreach (var c in new[] { seg.P0, seg.P1 })
                {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key))
                        cellValue[key] = snapped;
                }
            }

            // Step 2: union-find over cells. Two cells merge if they are adjacent
            // (within 1 cell in x or y) AND their raw values are within tolerance.
            var parent = new Dictionary<CoordinateKey, CoordinateKey>();
            CoordinateKey Find(CoordinateKey k)
            {
                while (parent.TryGetValue(k, out var p) && !p.Equals(k))
                    k = p;
                return k;
            }
            void Union(CoordinateKey a, CoordinateKey b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (!ra.Equals(rb))
                    parent[ra] = rb;
            }

            foreach (var key in cellValue.Keys)
                parent[key] = key;

            var keys = cellValue.Keys.ToList();
            foreach (var key in keys)
            {
                var v = cellValue[key];
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nk = key.Offset(dx, dy);
                    if (cellValue.TryGetValue(nk, out var nv)
                        && v.Distance(nv) <= _snapTolerance * 1.0001) // tiny epsilon for fp safety
                    {
                        Union(key, nk);
                    }
                }
            }

            // Step 3: pick ONE canonical coordinate per union-find group
            // (use the group's representative cell's value)
            var groupCanonical = new Dictionary<CoordinateKey, Coordinate>();
            var result = new Dictionary<CoordinateKey, Coordinate>();

            foreach (var key in keys)
            {
                var root = Find(key);
                if (!groupCanonical.TryGetValue(root, out var canon))
                {
                    canon = cellValue[root]; // representative's own value
                    groupCanonical[root] = canon;
                }
                result[key] = canon;
            }

            return result;
        }
#endif

#if null
        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {
            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments) {
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key))
                        cellValue[key] = snapped;
                }
            }

            var parent = new Dictionary<CoordinateKey, CoordinateKey>();
            CoordinateKey Find(CoordinateKey k) {
                while (parent.TryGetValue(k, out var p) && !p.Equals(k))
                    k = p;
                return k;
            }
            void Union(CoordinateKey a, CoordinateKey b) {
                var ra = Find(a);
                var rb = Find(b);
                if (!ra.Equals(rb))
                    parent[ra] = rb;
            }

            foreach (var key in cellValue.Keys)
                parent[key] = key;

            var keys = cellValue.Keys.ToList();

            // Search radius widened to ±2 cells to catch 2-cell gaps (e.g. x differs
            // by exactly 2 grid units). Distance threshold widened to tol*1.5 to
            // catch diagonal 1-cell neighbors (sqrt(2)*tol ≈ 1.414*tol).
            const int searchRadius = 2;
            double distThreshold = _snapTolerance * 2.5;    // covers both 2-cell-axis-aligned (2.0) and diagonal (1.414) gaps with margin

            foreach (var key in keys) {
                var v = cellValue[key];
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                    for (int dy = -searchRadius; dy <= searchRadius; dy++) {
                        if (dx == 0 && dy == 0) continue;
                        var nk = key.Offset(dx, dy);
                        if (cellValue.TryGetValue(nk, out var nv)
                            && v.Distance(nv) <= distThreshold) {
                            Union(key, nk);
                        }
                    }
            }

            var groupCanonical = new Dictionary<CoordinateKey, Coordinate>();
            var result = new Dictionary<CoordinateKey, Coordinate>();

            foreach (var key in keys) {
                var root = Find(key);
                if (!groupCanonical.TryGetValue(root, out var canon)) {
                    canon = cellValue[root];
                    groupCanonical[root] = canon;
                }
                result[key] = canon;
            }

            return result;
        }
#endif

#if null
        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {
            // Collect all distinct snapped coordinates
            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments) {
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key))
                        cellValue[key] = snapped;
                }
            }

            // Build a spatial index over the distinct points for proximity search
            var pointIndex = new STRtree<CoordinateKey>();
            foreach (var kvp in cellValue) {
                var env = new Envelope(kvp.Value);
                pointIndex.Insert(env, kvp.Key);
            }

            // Union-find over points within DEDUPE RADIUS of each other (real distance,
            // not grid-cell offset). This catches dangles of any size up to the radius,
            // regardless of how many grid cells they happen to span.
            var parent = new Dictionary<CoordinateKey, CoordinateKey>();
            CoordinateKey Find(CoordinateKey k) {
                while (parent.TryGetValue(k, out var p) && !p.Equals(k))
                    k = p;
                return k;
            }
            void Union(CoordinateKey a, CoordinateKey b) {
                var ra = Find(a);
                var rb = Find(b);
                if (!ra.Equals(rb))
                    parent[ra] = rb;
            }

            foreach (var key in cellValue.Keys)
                parent[key] = key;

            foreach (var kvp in cellValue) {
                var key = kvp.Key;
                var v = kvp.Value;

                var queryEnv = new Envelope(v);
                queryEnv.ExpandBy(_dedupeRadius);

                var candidates = pointIndex.Query(queryEnv);
                foreach (var candidateKey in candidates) {
                    if (candidateKey.Equals(key)) continue;
                    var candidateValue = cellValue[candidateKey];
                    if (v.Distance(candidateValue) <= _dedupeRadius)
                        Union(key, candidateKey);
                }
            }

            // Pick one canonical coordinate per group
            var groupCanonical = new Dictionary<CoordinateKey, Coordinate>();
            var result = new Dictionary<CoordinateKey, Coordinate>();

            foreach (var key in cellValue.Keys) {
                var root = Find(key);
                if (!groupCanonical.TryGetValue(root, out var canon)) {
                    canon = cellValue[root];
                    groupCanonical[root] = canon;
                }
                result[key] = canon;
            }

            return result;
        }
#endif

        /// <summary>
        /// Builds a canonical coordinate map, but only merges a point with its
        /// IMMEDIATE NEIGHBORS in segment-adjacency terms — i.e. only collapses
        /// duplicate/near-duplicate vertices that are CONSECUTIVE in some source
        /// geometry's coordinate sequence, never just "spatially nearby."
        /// This avoids collapsing genuinely close-but-unrelated vertices in thin
        /// sliver polygons while still fixing digitizing dangles at shared edges.
        /// </summary>
        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {
            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments) {
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key))
                        cellValue[key] = snapped;
                }
            }

            var parent = new Dictionary<CoordinateKey, CoordinateKey>();
            CoordinateKey Find(CoordinateKey k) {
                while (parent.TryGetValue(k, out var p) && !p.Equals(k))
                    k = p;
                return k;
            }
            void Union(CoordinateKey a, CoordinateKey b) {
                var ra = Find(a);
                var rb = Find(b);
                if (!ra.Equals(rb))
                    parent[ra] = rb;
            }

            foreach (var key in cellValue.Keys)
                parent[key] = key;

            // ONLY merge endpoints of segments that are THEMSELVES short (i.e. the
            // segment connecting them is, by itself, shorter than dedupeRadius).
            // This catches: (a) genuine zero/near-zero-length digitizing artifacts
            // -- the dangle case -- because the dangle IS a short connecting hop.
            // It will NOT catch two unrelated nearby vertices in a thin sliver,
            // because those vertices are typically NOT directly joined by a raw
            // segment in the source data (they're connected via several hops
            // around the sliver tip, not by a single short edge).
            foreach (var seg in rawSegments) {
                if (seg.P0.Distance(seg.P1) <= _dedupeRadius) {
                    var k0 = new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance);
                    var k1 = new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance);
                    Union(k0, k1);
                }
            }

            var groupCanonical = new Dictionary<CoordinateKey, Coordinate>();
            var result = new Dictionary<CoordinateKey, Coordinate>();

            foreach (var key in cellValue.Keys) {
                var root = Find(key);
                if (!groupCanonical.TryGetValue(root, out var canon)) {
                    canon = cellValue[root];
                    groupCanonical[root] = canon;
                }
                result[key] = canon;
            }

            return result;
        }



        /// <summary>
        /// If the intersection point pt is within tolerance of an endpoint of
        /// either segment, snap to that endpoint exactly.
        /// </summary>
        private Coordinate SnapToEndpoint(Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 }) {
                if (pt.Distance(ep) <= _snapTolerance)
                    return ep;
            }
            return pt;
        }

        // -------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------

        private void InsertEdge(
            Coordinate c0, Coordinate c1, int sourceId, bool sourceReversed,
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {
            if (c0.Equals2D(c1, _snapTolerance)) return;

            var k0 = new CoordinateKey(c0, _snapTolerance);
            var k1 = new CoordinateKey(c1, _snapTolerance);
            bool needsSwap = k0.CompareTo(k1) > 0;
            var key = needsSwap ? (k1, k0) : (k0, k1);

            if (!edgeMap.TryGetValue(key, out var edge)) {
                var (geomC0, geomC1) = needsSwap ? (c1, c0) : (c0, c1);
                var ls = _factory.CreateLineString(new[] { geomC0, geomC1 });
                edge = new NetworkEdge {
                    Id = _edges.Count,
                    Geometry = ls,
                    StartNode = geomC0,
                    EndNode = geomC1,
                };
                _edges.Add(edge);
                edgeMap[key] = edge;

                GetOrCreateNode(geomC0).Edges.Add(edge);
                GetOrCreateNode(geomC1).Edges.Add(edge);
            }

            edge.SourceGeometryIds.Add(sourceId);

            // c0→c1 is the source's traversal direction.
            // edge.StartNode is geomC0 = (needsSwap ? c1 : c0)
            // So source goes StartNode→EndNode iff NOT needsSwap
            // (because if needsSwap, StartNode=c1 and source goes c0→c1 = EndNode→StartNode)
            bool sourceGoesStartToEnd = (sourceReversed == needsSwap);
            edge.SourceOrientation[sourceId] = sourceGoesStartToEnd;

            if (!_edgesBySource.TryGetValue(sourceId, out var list))
                _edgesBySource[sourceId] = list = new List<NetworkEdge>();
            if (!list.Contains(edge))
                list.Add(edge);
        }

        private NetworkNode GetOrCreateNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            if (!_nodes.TryGetValue(key, out var node))
                _nodes[key] = node = new NetworkNode { Coordinate = coord };
            return node;
        }

        // -------------------------------------------------------------------
        // Query API
        // -------------------------------------------------------------------

        /// <summary>
        /// Get all network edges that belong to a registered geometry by id.
        /// </summary>
        public IReadOnlyList<NetworkEdge> GetEdgesFor(int sourceId)
            => _edgesBySource.TryGetValue(sourceId, out var list)
                ? list
                : Array.Empty<NetworkEdge>();

        /// <summary>
        /// Get all edges shared between exactly these two geometries.
        /// </summary>
        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => GetEdgesFor(sourceIdA).Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        /// <summary>
        /// Get all edges shared between any two or more geometries.
        /// </summary>
        public IEnumerable<NetworkEdge> GetAllSharedEdges()
            => _edges.Where(e => e.IsShared);

        /// <summary>
        /// Find which source geometries contain a given edge.
        /// </summary>
        public IEnumerable<NetworkGeometry> GetSourceGeometriesForEdge(NetworkEdge edge)
            => edge.SourceGeometryIds.Select(id => _sources[id]);

        /// <summary>
        /// Get the node at a coordinate (within snap tolerance).
        /// </summary>
        public NetworkNode GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            return _nodes.TryGetValue(key, out var node) ? node : null;
        }

        /// <summary>
        /// Find all edges within a search envelope.
        /// </summary>
        public IEnumerable<NetworkEdge> QueryEdges(Envelope envelope)
            => _edgeIndex.Query(envelope).Cast<NetworkEdge>();

        /// <summary>
        /// Describe the full network definition for a source geometry.
        /// </summary>
        public NetworkDefinition GetNetworkDefinition(int sourceId) {
            var edges = GetEdgesFor(sourceId);
            var sharedEdges = edges.Where(e => e.IsShared).ToList();
            var privateEdges = edges.Where(e => !e.IsShared).ToList();

            var neighbourIds = sharedEdges
                .SelectMany(e => e.SourceGeometryIds)
                .Where(id => id != sourceId)
                .Distinct()
                .ToList();

            return new NetworkDefinition {
                SourceId = sourceId,
                Source = _sources[sourceId],
                AllEdges = edges.ToList(),
                SharedEdges = sharedEdges,
                PrivateEdges = privateEdges,
                Neighbours = neighbourIds.Select(id => _sources[id]).ToList(),
            };
        }

        // -------------------------------------------------------------------
        // Merging edges into longest possible LineStrings
        // -------------------------------------------------------------------

        /// <summary>
        /// Merges all shared edges into the longest possible LineStrings,
        /// respecting source-geometry boundaries.
        /// </summary>
        public List<MergedEdge> MergeSharedEdges() => MergeEdgeSet(GetAllSharedEdges());

        /// <summary>
        /// Merges all edges belonging to a specific source geometry into the
        /// longest possible LineStrings.
        /// </summary>
        public List<MergedEdge> MergeEdgesFor(int sourceId) => MergeEdgeSet(GetEdgesFor(sourceId));

        private List<MergedEdge> MergeEdgeSet(IEnumerable<NetworkEdge> edgeSet) {
            var edges = edgeSet.ToHashSet();
            if (edges.Count == 0) return new List<MergedEdge>();

            // Build a local adjacency: coord -> edges in this set only
            var adjacency = new Dictionary<CoordinateKey, List<NetworkEdge>>();

            void Touch(Coordinate c, NetworkEdge e) {
                var key = new CoordinateKey(c, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var list))
                    adjacency[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }

            foreach (var edge in edges) {
                Touch(edge.StartNode, edge);
                Touch(edge.EndNode, edge);
            }

            var visited = new HashSet<int>();
            var result = new List<MergedEdge>();

            foreach (var startEdge in edges) {
                if (visited.Contains(startEdge.Id)) continue;

                var chain = WalkFullChain(startEdge, edges, adjacency, visited);
                var coords = ChainToCoordinates(chain);

                result.Add(new MergedEdge {
                    Geometry = _factory.CreateLineString(coords),
                    SourceGeometryIds = chain.SelectMany(e => e.SourceGeometryIds).Distinct().ToHashSet(),
                    ConstituentEdges = chain,
                });
            }

            return result;
        }

        /// <summary>
        /// Walks a chain in both directions from seed, correctly handling
        /// closed loops (stops cleanly when arriving back at the seed).
        /// </summary>
        private List<NetworkEdge> WalkFullChain(
            NetworkEdge seed,
            HashSet<NetworkEdge> edgeSet,
            Dictionary<CoordinateKey, List<NetworkEdge>> adjacency,
            HashSet<int> visited) {
            visited.Add(seed.Id);
            var forward = new List<NetworkEdge>();
            var backward = new List<NetworkEdge>();

            // ---- Forward walk from seed.EndNode ----
            var coord = seed.EndNode;
            var current = seed;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var neighbours)) break;

                var candidates = neighbours.Where(e => edgeSet.Contains(e) && e.Id != current.Id).ToList();
                if (candidates.Count != 1) break;

                var next = candidates[0];

                // Stop if we've looped back to the seed (closed ring) or any visited edge
                if (next.Id == seed.Id || visited.Contains(next.Id)) break;
                if (!next.SourceGeometryIds.SetEquals(current.SourceGeometryIds)) break;

                visited.Add(next.Id);
                forward.Add(next);
                coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
                current = next;
            }

            // ---- Backward walk from seed.StartNode ----
            coord = seed.StartNode;
            current = seed;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var neighbours)) break;

                var candidates = neighbours.Where(e => edgeSet.Contains(e) && e.Id != current.Id).ToList();
                if (candidates.Count != 1) break;

                var prev = candidates[0];

                if (prev.Id == seed.Id || visited.Contains(prev.Id)) break;
                if (!prev.SourceGeometryIds.SetEquals(current.SourceGeometryIds)) break;

                visited.Add(prev.Id);
                backward.Add(prev);
                coord = prev.StartNode.Equals2D(coord, _snapTolerance) ? prev.EndNode : prev.StartNode;
                current = prev;
            }

            backward.Reverse();
            var chain = new List<NetworkEdge>(backward.Count + 1 + forward.Count);
            chain.AddRange(backward);
            chain.Add(seed);
            chain.AddRange(forward);
            return chain;
        }

        /// <summary>
        /// Stitches an ordered chain of edges into a single coordinate array,
        /// orienting each edge so consecutive coordinates connect.
        /// </summary>
        //private Coordinate[] ChainToCoordinates(List<NetworkEdge> chain) {
        //    if (chain.Count == 0) return Array.Empty<Coordinate>();
        //    if (chain.Count == 1)
        //        return chain[0].Geometry.Coordinates.ToArray();

        //    var coords = new List<Coordinate>();

        //    for (int i = 0; i < chain.Count; i++) {
        //        var edge = chain[i];
        //        var edgeCs = edge.Geometry.Coordinates;

        //        if (i == 0) {
        //            var nextEdge = chain[1];
        //            bool forward = ConnectsTo(edge, nextEdge, fromEnd: true);
        //            coords.AddRange(forward ? edgeCs : edgeCs.Reverse());
        //        }
        //        else {
        //            var last = coords[^1];
        //            bool startMatches = edgeCs[0].Equals2D(last, _snapTolerance);
        //            var oriented = startMatches ? edgeCs : edgeCs.Reverse();

        //            // Skip the first coord — it's the same as the last one added
        //            coords.AddRange(oriented.Skip(1));
        //        }
        //    }

        //    return coords.ToArray();
        //}
        private Coordinate[] ChainToCoordinates(List<NetworkEdge> chain) {
            if (chain.Count == 0) return Array.Empty<Coordinate>();

            var coords = new List<Coordinate>();

            // The second coord in the chain tells us which end of edge[0] connects forward
            Coordinate nextStart = chain.Count > 1
                ? (chain[1].StartNode.Equals2D(chain[0].StartNode, _snapTolerance) ||
                   chain[1].StartNode.Equals2D(chain[0].EndNode, _snapTolerance)
                       ? chain[1].StartNode
                       : chain[1].EndNode)
                : chain[0].EndNode;

            // Determine traversal direction for the first edge
            bool firstReversed = nextStart.Equals2D(chain[0].StartNode, _snapTolerance);

            var firstEdge = chain[0];
            var firstCoords = firstReversed
                ? firstEdge.Geometry.Coordinates.Reverse().ToArray()
                : firstEdge.Geometry.Coordinates;

            foreach (var c in firstCoords)
                coords.Add(SnapToGrid(c));  // <-- snap

            for (int i = 1; i < chain.Count; i++) {
                var edge = chain[i];
                var prevEnd = coords[^1];

                bool reversed = !edge.StartNode.Equals2D(prevEnd, _snapTolerance);
                var edgeCoords = reversed
                    ? edge.Geometry.Coordinates.Reverse().ToArray()
                    : edge.Geometry.Coordinates;

                // Skip first coord (it duplicates prevEnd), snap the rest
                foreach (var c in edgeCoords.Skip(1))
                    coords.Add(SnapToGrid(c));  // <-- snap
            }

            return coords.ToArray();
        }

        private bool ConnectsTo(NetworkEdge edge, NetworkEdge other, bool fromEnd) {
            var coord = fromEnd ? edge.EndNode : edge.StartNode;
            return coord.Equals2D(other.StartNode, _snapTolerance)
                || coord.Equals2D(other.EndNode, _snapTolerance);
        }


        /// <summary>
        /// Returns the full geometry for a single source, stitched back into one
        /// LineString (or closed ring), ignoring source-set boundaries entirely.
        /// Unlike MergeEdgesFor, this never splits at points where the boundary
        /// is shared with other geometries — it only considers this source's own
        /// edges and walks them end-to-end.
        /// </summary>
#if null
        public LineString GetFullGeometryFor(int sourceId) {
            var edges = GetEdgesFor(sourceId).ToHashSet();
            if (edges.Count == 0) return null;

            // Build adjacency using ONLY this source's own edges
            var adjacency = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            void Touch(Coordinate c, NetworkEdge e) {
                var key = new CoordinateKey(c, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var list))
                    adjacency[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }
            foreach (var edge in edges) {
                Touch(edge.StartNode, edge);
                Touch(edge.EndNode, edge);
            }

            var visited = new HashSet<int>();
            var seed = edges.First();
            var chain = WalkFullChainIgnoringSourceSets(seed, edges, adjacency, visited);

            var coords = ChainToCoordinates(chain);
            return _factory.CreateLineString(coords);
        }
#endif

        //public LineString? GetFullGeometryFor(int sourceId) {
        //    var chain = GetFullEdgeChainFor(sourceId);
        //    if (chain.Count == 0) return null;

        //    var coords = ChainToCoordinates(chain);
        //    return _factory.CreateLineString(coords);
        //}

        public LineString GetFullGeometryFor(int sourceId) {
            var mergedEdges = GetMergedEdgeChainFor(sourceId);
            if (mergedEdges.Count == 0) return null;

            var coords = new List<Coordinate>();

            foreach (var merged in mergedEdges) {
                var mergedCoords = merged.Geometry.Coordinates;
                if (coords.Count == 0) {
                    coords.AddRange(mergedCoords.Select(c => SnapToGrid(c)));
                }
                else {
                    // Skip first coord - it duplicates the last coord we already have
                    coords.AddRange(mergedCoords.Skip(1).Select(c => SnapToGrid(c)));
                }
            }

            var source = _sources.First(s => s.Id == sourceId);
            bool isRing = source.Kind == GeometryKind.Polygon || source.Geometry is LinearRing;

            if (isRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);

            return _factory.CreateLineString(coords.ToArray());
        }


        /// <summary>
        /// Returns the full ordered list of NetworkEdges belonging to a single
        /// source, walked end-to-end, ignoring source-set boundaries entirely
        /// (i.e. it does not stop at nodes shared with other geometries).
        /// Unlike MergeEdgesFor, every edge stays a distinct NetworkEdge in the
        /// result — nothing is merged or flattened into one LineString.
        /// </summary>
        public List<NetworkEdge> GetFullEdgeChainFor(int sourceId) {
            var edges = GetEdgesFor(sourceId).ToHashSet();
            if (edges.Count == 0) return new List<NetworkEdge>();

            var adjacency = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            void Touch(Coordinate c, NetworkEdge e) {
                var key = new CoordinateKey(c, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var list))
                    adjacency[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }
            foreach (var edge in edges) {
                Touch(edge.StartNode, edge);
                Touch(edge.EndNode, edge);
            }

            var visited = new HashSet<int>();
            var seed = edges.First();
            return WalkFullChainIgnoringSourceSets(seed, edges, adjacency, visited);
        }



        /// <summary>
        /// Same traversal as WalkFullChain, but WITHOUT the SourceGeometryIds.SetEquals
        /// check — it walks purely on graph degree (within this source's own edge
        /// set), so a node that's also shared with other geometries doesn't break
        /// the chain, as long as within THIS source's edges the node still has
        /// degree 2.
        /// </summary>
        //private List<NetworkEdge> WalkFullChainIgnoringSourceSets(
        //    NetworkEdge seed,
        //    HashSet<NetworkEdge> edgeSet,
        //    Dictionary<CoordinateKey, List<NetworkEdge>> adjacency,
        //    HashSet<int> visited) {
        //    visited.Add(seed.Id);
        //    var forward = new List<NetworkEdge>();
        //    var backward = new List<NetworkEdge>();

        //    var coord = seed.EndNode;
        //    var current = seed;
        //    while (true) {
        //        var key = new CoordinateKey(coord, _snapTolerance);
        //        if (!adjacency.TryGetValue(key, out var neighbours)) break;

        //        var candidates = neighbours.Where(e => edgeSet.Contains(e) && e.Id != current.Id).ToList();
        //        if (candidates.Count != 1) break;

        //        var next = candidates[0];
        //        if (next.Id == seed.Id || visited.Contains(next.Id)) break;
        //        // NOTE: no SourceGeometryIds check here — that's the whole point

        //        visited.Add(next.Id);
        //        forward.Add(next);
        //        coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
        //        current = next;
        //    }

        //    coord = seed.StartNode;
        //    current = seed;
        //    while (true) {
        //        var key = new CoordinateKey(coord, _snapTolerance);
        //        if (!adjacency.TryGetValue(key, out var neighbours)) break;

        //        var candidates = neighbours.Where(e => edgeSet.Contains(e) && e.Id != current.Id).ToList();
        //        if (candidates.Count != 1) break;

        //        var prev = candidates[0];
        //        if (prev.Id == seed.Id || visited.Contains(prev.Id)) break;

        //        visited.Add(prev.Id);
        //        backward.Add(prev);
        //        coord = prev.StartNode.Equals2D(coord, _snapTolerance) ? prev.EndNode : prev.StartNode;
        //        current = prev;
        //    }

        //    backward.Reverse();
        //    var chain = new List<NetworkEdge>(backward.Count + 1 + forward.Count);
        //    chain.AddRange(backward);
        //    chain.Add(seed);
        //    chain.AddRange(forward);
        //    return chain;
        //}
        private List<NetworkEdge> WalkFullChainIgnoringSourceSets(
            NetworkEdge seed,
            HashSet<NetworkEdge> edgeSet,
            Dictionary<CoordinateKey, List<NetworkEdge>> adjacency,
            HashSet<int> visited) {
            visited.Add(seed.Id);
            var forward = new List<NetworkEdge>();
            var backward = new List<NetworkEdge>();

            // --- Forward walk ---
            var coord = seed.EndNode;
            var current = seed;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var neighbours)) break;
                var candidates = neighbours.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (candidates.Count != 1) break;
                var next = candidates[0];

                // Closed ring: forward walk has reached back to seed's start
                bool closedRing = next.StartNode.Equals2D(seed.StartNode, _snapTolerance)
                               || next.EndNode.Equals2D(seed.StartNode, _snapTolerance);
                if (closedRing) {
                    visited.Add(next.Id);
                    forward.Add(next);
                    break; // ring is closed, don't continue
                }

                visited.Add(next.Id);
                forward.Add(next);
                coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
                current = next;
            }

            // --- Backward walk (only if ring isn't already closed) ---
            bool ringClosed = forward.Count > 0 &&
                              (forward[^1].StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                               forward[^1].EndNode.Equals2D(seed.StartNode, _snapTolerance));

            if (!ringClosed) {
                coord = seed.StartNode;
                current = seed;
                while (true) {
                    var key = new CoordinateKey(coord, _snapTolerance);
                    if (!adjacency.TryGetValue(key, out var neighbours)) break;
                    var candidates = neighbours.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                    if (candidates.Count != 1) break;
                    var prev = candidates[0];

                    visited.Add(prev.Id);
                    backward.Add(prev);
                    coord = prev.StartNode.Equals2D(coord, _snapTolerance) ? prev.EndNode : prev.StartNode;
                    current = prev;
                }
            }

            backward.Reverse();
            var chain = new List<NetworkEdge>(backward.Count + 1 + forward.Count);
            chain.AddRange(backward);
            chain.Add(seed);
            chain.AddRange(forward);
            return chain;
        }




        /// <summary>
        /// Returns the full boundary of a source as an ordered list of MergedEdges,
        /// where consecutive edges with the same SourceGeometryIds are stitched into
        /// the longest possible LineString. Break points occur only where the shared-
        /// source-set changes (i.e. where the boundary switches from "private" to
        /// "shared with geometry X" to "shared with geometries X+Y", etc).
        ///
        /// This gives you the largest reusable line segments: each MergedEdge in the
        /// result has a single, consistent topological meaning.
        /// </summary>
        public List<MergedEdge> GetMergedEdgeChainFor(int sourceId) {
            var sourceEdges = GetEdgesFor(sourceId).ToHashSet();
            if (sourceEdges.Count == 0) return new List<MergedEdge>();

            // Walk edges in the order the SOURCE originally traversed them,
            // using SourceOrientation to determine direction.
            var orderedEdges = OrderEdgesBySourceTraversal(sourceId, sourceEdges);
            if (orderedEdges.Count == 0) return new List<MergedEdge>();

            var directions = orderedEdges.Select(e =>
                e.SourceOrientation.TryGetValue(sourceId, out bool fwd) ? fwd : true
            ).ToList();

            // Group consecutive edges with the same SourceGeometryIds
            var result = new List<MergedEdge>();
            var currentGroup = new List<NetworkEdge> { orderedEdges[0] };
            var currentDirs = new List<bool> { directions[0] };

            for (int i = 1; i < orderedEdges.Count; i++) {
                if (orderedEdges[i].SourceGeometryIds.SetEquals(currentGroup[^1].SourceGeometryIds)) {
                    currentGroup.Add(orderedEdges[i]);
                    currentDirs.Add(directions[i]);
                }
                else {
                    result.Add(BuildMergedEdge(currentGroup, currentDirs));
                    currentGroup = new List<NetworkEdge> { orderedEdges[i] };
                    currentDirs = new List<bool> { directions[i] };
                }
            }
            if (currentGroup.Count > 0)
                result.Add(BuildMergedEdge(currentGroup, currentDirs));

            return result;
        }

        private List<NetworkEdge> OrderEdgesBySourceTraversal(
    int sourceId, HashSet<NetworkEdge> sourceEdges) {

            var source = _sources[sourceId];
            var originalCoords = source.Geometry.Coordinates;

            // Build a lookup: network node coordinate -> edges in this source
            var nodeToEdges = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            foreach (var edge in sourceEdges) {
                void Touch(Coordinate c) {
                    var key = new CoordinateKey(c, _snapTolerance);
                    if (!nodeToEdges.TryGetValue(key, out var list))
                        nodeToEdges[key] = list = new List<NetworkEdge>();
                    if (!list.Contains(edge)) list.Add(edge);
                }
                Touch(edge.StartNode);
                Touch(edge.EndNode);
            }

            // Walk original coords; each time we hit a network node that starts
            // an unvisited edge, emit that edge.
            var result = new List<NetworkEdge>();
            var visited = new HashSet<int>();

            for (int ci = 0; ci < originalCoords.Length; ci++) {
                var snapped = SnapToGrid(originalCoords[ci]);
                var key = new CoordinateKey(snapped, _snapTolerance);

                if (!nodeToEdges.TryGetValue(key, out var edgesHere)) continue;

                foreach (var edge in edgesHere) {
                    if (visited.Contains(edge.Id)) continue;

                    // Only take this edge if snapped coord is the ENTRY node
                    // (the node we arrive at from the previous coord in the sequence)
                    // Entry = StartNode if we're going forward, EndNode if reversed.
                    // We determine direction by which endpoint matches snapped.
                    bool entryIsStart = edge.StartNode.Equals2D(snapped, _snapTolerance);
                    bool entryIsEnd = edge.EndNode.Equals2D(snapped, _snapTolerance);

                    if (!entryIsStart && !entryIsEnd) continue;

                    // Verify the OTHER endpoint appears later in originalCoords
                    // to confirm we're traversing in the right direction.
                    var exitNode = entryIsStart ? edge.EndNode : edge.StartNode;
                    bool exitFoundLater = false;
                    for (int cj = ci + 1; cj < originalCoords.Length; cj++) {
                        if (SnapToGrid(originalCoords[cj]).Equals2D(exitNode, _snapTolerance)) {
                            exitFoundLater = true;
                            break;
                        }
                        // Stop searching if we hit another network node first
                        // (that would mean this edge goes backwards)
                        var ck = new CoordinateKey(SnapToGrid(originalCoords[cj]), _snapTolerance);
                        if (nodeToEdges.ContainsKey(ck)) break;
                    }

                    if (exitFoundLater) {
                        visited.Add(edge.Id);
                        result.Add(edge);
                        break;
                    }
                }
            }

            return result;
        }

        private List<NetworkEdge> RotateChainToSourceStart(
            List<NetworkEdge> chain, int sourceId, NetworkGeometry source) {

            var firstCoord = SnapToGrid(source.Geometry.Coordinates[0]);

            // First try: find edge where source enters at firstCoord (it's a node)
            for (int i = 0; i < chain.Count; i++) {
                var edge = chain[i];
                if (!edge.SourceOrientation.TryGetValue(sourceId, out bool forward)) continue;
                var entryNode = forward ? edge.StartNode : edge.EndNode;
                if (entryNode.Equals2D(firstCoord, _snapTolerance)) {
                    if (i == 0) return chain;
                    var rotated = new List<NetworkEdge>(chain.Count);
                    rotated.AddRange(chain.Skip(i));
                    rotated.AddRange(chain.Take(i));
                    return rotated;
                }
            }

            // Second try: find the edge whose geometry CONTAINS firstCoord
            // (it's interior to an edge — source start was not a network node)
            for (int i = 0; i < chain.Count; i++) {
                var edge = chain[i];
                var edgeCoords = edge.Geometry.Coordinates;
                foreach (var c in edgeCoords) {
                    if (SnapToGrid(c).Equals2D(firstCoord, _snapTolerance)) {
                        if (i == 0) return chain;
                        var rotated = new List<NetworkEdge>(chain.Count);
                        rotated.AddRange(chain.Skip(i));
                        rotated.AddRange(chain.Take(i));
                        return rotated;
                    }
                }
                // Also check if firstCoord is within snap tolerance of any coord
                if (edge.Geometry.Coordinates.Any(c => c.Distance(firstCoord) <= _snapTolerance)) {
                    if (i == 0) return chain;
                    var rotated = new List<NetworkEdge>(chain.Count);
                    rotated.AddRange(chain.Skip(i));
                    rotated.AddRange(chain.Take(i));
                    return rotated;
                }
            }

            return chain; // can't find it, return as-is
        }

        // Returns true = traverse StartNode->EndNode, false = traverse EndNode->StartNode
        private List<bool> ComputeChainDirections(List<NetworkEdge> chain, int sourceId) {
            var directions = new List<bool>(chain.Count);
            if (chain.Count == 0) return directions;

            // Use SourceOrientation as ground truth for direction
            for (int i = 0; i < chain.Count; i++) {
                var edge = chain[i];
                if (edge.SourceOrientation.TryGetValue(sourceId, out bool sourceForward)) {
                    directions.Add(sourceForward);
                }
                else {
                    // Fallback: infer from connectivity to previous edge
                    if (i == 0) {
                        directions.Add(true);
                    }
                    else {
                        var prevExit = directions[i - 1]
                            ? chain[i - 1].EndNode
                            : chain[i - 1].StartNode;
                        directions.Add(chain[i].StartNode.Equals2D(prevExit, _snapTolerance));
                    }
                }
            }

            return directions;
        }

        private MergedEdge BuildMergedEdge(List<NetworkEdge> edges, List<bool> directions) {
            var coords = new List<Coordinate>();

            for (int i = 0; i < edges.Count; i++) {
                var edgeCoords = edges[i].Geometry.Coordinates;
                var ordered = directions[i] ? edgeCoords : edgeCoords.Reverse().ToArray();

                if (coords.Count == 0)
                    coords.AddRange(ordered.Select(c => SnapToGrid(c)));
                else
                    coords.AddRange(ordered.Skip(1).Select(c => SnapToGrid(c)));
            }

            return new MergedEdge {
                Geometry = _factory.CreateLineString(coords.ToArray()),
                SourceGeometryIds = edges.SelectMany(e => e.SourceGeometryIds).Distinct().ToHashSet(),
                ConstituentEdges = edges.ToList(),
            };
        }

        //private MergedEdge BuildMergedEdge(List<NetworkEdge> edges) {
        //    var coords = ChainToCoordinates(edges);
        //    return new MergedEdge {
        //        Geometry = _factory.CreateLineString(coords),
        //        SourceGeometryIds = edges.SelectMany(e => e.SourceGeometryIds)
        //                                 .Distinct()
        //                                 .ToHashSet(),
        //        ConstituentEdges = edges.ToList(),
        //    };
        //}








        // -------------------------------------------------------------------
        // Ring / LineString classification for merged edges
        // -------------------------------------------------------------------

        /// <summary>
        /// Classifies a merged edge against each of its contributing source
        /// geometries: exterior ring, interior ring (hole), or linestring.
        /// </summary>
        public List<MergedEdgeClassification> ClassifyMergedEdge(MergedEdge edge) {
            var result = new List<MergedEdgeClassification>();

            foreach (var sourceId in edge.SourceGeometryIds) {
                var src = _sources[sourceId];

                if (src.Kind == GeometryKind.LineString) {
                    result.Add(new MergedEdgeClassification {
                        Kind = EdgeRingKind.LineString,
                        Source = src,
                    });
                    continue;
                }

                var poly = (Polygon)src.Geometry;
                var ringKind = ClassifyAgainstPolygon(edge.Geometry, poly);

                result.Add(new MergedEdgeClassification {
                    Kind = ringKind.Kind,
                    RingIndex = ringKind.RingIndex,
                    Source = src,
                });
            }

            // If contributions disagree, mark as Mixed
            if (result.Select(r => r.Kind).Distinct().Count() > 1) {
                for (int i = 0; i < result.Count; i++) {
                    var r = result[i];
                    result[i] = new MergedEdgeClassification {
                        Kind = EdgeRingKind.Mixed,
                        RingIndex = r.RingIndex,
                        Source = r.Source,
                    };
                }
            }

            return result;
        }

        private (EdgeRingKind Kind, int? RingIndex) ClassifyAgainstPolygon(LineString edgeGeom, Polygon poly) {
            var probe = MidPoint(edgeGeom);

            if (IsOnRing(probe, poly.ExteriorRing))
                return (EdgeRingKind.ExteriorRing, null);

            for (int i = 0; i < poly.NumInteriorRings; i++) {
                if (IsOnRing(probe, poly.GetInteriorRingN(i)))
                    return (EdgeRingKind.InteriorRing, i);
            }

            return FallbackClassify(edgeGeom, poly);
        }

        private bool IsOnRing(Point probe, LineString ring) => ring.Distance(probe) <= _snapTolerance;

        private (EdgeRingKind Kind, int? RingIndex) FallbackClassify(LineString edgeGeom, Polygon poly) {
            int exteriorVotes = 0;
            var interiorVotes = new int[poly.NumInteriorRings];

            int samples = Math.Min(edgeGeom.NumPoints, 5);
            for (int i = 0; i <= samples; i++) {
                int idx = (int)((double)i / samples * (edgeGeom.NumPoints - 1));
                var pt = edgeGeom.Factory.CreatePoint(edgeGeom.GetCoordinateN(idx));

                if (poly.ExteriorRing.Distance(pt) <= _snapTolerance) {
                    exteriorVotes++;
                    continue;
                }
                for (int j = 0; j < poly.NumInteriorRings; j++) {
                    if (poly.GetInteriorRingN(j).Distance(pt) <= _snapTolerance)
                        interiorVotes[j]++;
                }
            }

            if (exteriorVotes > 0)
                return (EdgeRingKind.ExteriorRing, null);

            for (int j = 0; j < interiorVotes.Length; j++)
                if (interiorVotes[j] > 0)
                    return (EdgeRingKind.InteriorRing, j);

            return (EdgeRingKind.ExteriorRing, null);
        }

        private static Point MidPoint(LineString line) {
            var coords = line.Coordinates;
            int mid = coords.Length / 2;

            if (coords.Length % 2 == 0) {
                var c0 = coords[mid - 1];
                var c1 = coords[mid];
                return line.Factory.CreatePoint(new Coordinate((c0.X + c1.X) / 2.0, (c0.Y + c1.Y) / 2.0));
            }

            return line.Factory.CreatePoint(coords[mid]);
        }

        // -------------------------------------------------------------------
        // Orientation helper
        // -------------------------------------------------------------------

        /// <summary>
        /// Orients the merged geometry to match the traversal direction of a
        /// specific source geometry.
        /// </summary>
        public LineString OrientToSource(MergedEdge merged, int sourceId) {
            var coords = merged.Geometry.Coordinates;

            var refEdge = merged.ConstituentEdges.FirstOrDefault(e => e.SourceGeometryIds.Contains(sourceId));
            if (refEdge == null) return merged.Geometry; // source not part of this edge

            bool sourceForward = refEdge.SourceOrientation[sourceId];

            bool mergedMatchesEdgeStart = coords[0].Equals2D(refEdge.StartNode, _snapTolerance);
            bool mergedIsForwardForSource = mergedMatchesEdgeStart == sourceForward;

            return mergedIsForwardForSource
                ? merged.Geometry
                : (LineString)merged.Geometry.Reverse();
        }
    }
}
