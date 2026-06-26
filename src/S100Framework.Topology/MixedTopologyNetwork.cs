using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace S100Framework.Topology.Internal
{
    /// <summary>
    /// Indicates whether a registered source geometry represents a polygon boundary or an open linestring.
    /// </summary>
    public enum GeometryKind { Polygon, LineString }

    /// <summary>
    /// A source geometry registered with <see cref="MixedTopologyNetwork"/>.
    /// Wraps the original NTS geometry together with its assigned network id and kind.
    /// </summary>
    public class NetworkGeometry
    {
        /// <summary>Zero-based index assigned when the geometry was registered.</summary>
        public int Id { get; init; }
        /// <summary>The original NTS geometry (Polygon, LinearRing, or LineString).</summary>
        public Geometry Geometry { get; init; }
        /// <summary>Whether this source is treated as a polygon boundary or an open linestring.</summary>
        public GeometryKind Kind { get; init; }

        /// <summary>
        /// True if this source geometry is a closed ring — a Polygon, a LinearRing,
        /// or a LineString whose first and last coordinates are equal.
        /// Use this everywhere ring vs open behaviour is needed instead of checking
        /// Kind or geometry type directly.
        /// </summary>
        public bool IsRing =>
            Kind == GeometryKind.Polygon
            || Geometry is LinearRing
            || (Kind == GeometryKind.LineString
                && Geometry.NumPoints >= 4
                && Geometry.Coordinates[0].Equals2D(
                   Geometry.Coordinates[Geometry.NumPoints - 1]));
    }

    /// <summary>
    /// A topological node in the planar network — a coordinate where two or more edges meet.
    /// </summary>
    public class NetworkNode
    {
        /// <summary>The snapped, canonical coordinate of this node.</summary>
        public Coordinate Coordinate { get; init; }
        /// <summary>All edges incident to this node.</summary>
        public List<NetworkEdge> Edges { get; } = new();
        /// <summary>Number of edges incident to this node.</summary>
        public int Degree => Edges.Count;
    }

    /// <summary>
    /// An undirected edge in the planar network representing a single straight-line segment
    /// between two nodes. <see cref="StartNode"/> and <see cref="EndNode"/> are stored in
    /// canonical lexicographic order and do not encode any source geometry's traversal direction.
    /// Use <see cref="EdgeReference.Forward"/> to determine direction per source.
    /// </summary>
    public class NetworkEdge
    {
        /// <summary>Zero-based edge index, unique within the network.</summary>
        public int Id { get; init; }
        /// <summary>Two-point LineString geometry in canonical (StartNode → EndNode) order.</summary>
        public LineString Geometry { get; init; }
        /// <summary>Lexicographically smaller endpoint (canonical start).</summary>
        public Coordinate StartNode { get; init; }
        /// <summary>Lexicographically larger endpoint (canonical end).</summary>
        public Coordinate EndNode { get; init; }
        /// <summary>Ids of all source geometries that contain this edge.</summary>
        public HashSet<int> SourceGeometryIds { get; } = new();
        /// <summary>True when more than one source geometry contributes this edge.</summary>
        public bool IsShared => SourceGeometryIds.Count > 1;
    }

    /// <summary>
    /// A maximal run of consecutive <see cref="NetworkEdge"/> objects that all share the same
    /// <see cref="SourceGeometryIds"/> set, stitched into a single LineString.
    /// The geometry direction is determined by connectivity, not by any source's traversal direction.
    /// Use <see cref="EdgeReference.OrientedGeometry"/> to retrieve the geometry oriented for a
    /// specific source.
    /// </summary>
    public class MergedEdge
    {
        /// <summary>The stitched LineString covering all constituent edges in connectivity order.</summary>
        public LineString Geometry { get; init; }
        /// <summary>The set of source geometry ids shared by every constituent edge.</summary>
        public HashSet<int> SourceGeometryIds { get; init; }
        /// <summary>The individual network edges that were merged to produce this geometry.</summary>
        public List<NetworkEdge> ConstituentEdges { get; init; }
        /// <summary>True when more than one source geometry contributes this merged edge.</summary>
        public bool IsShared => SourceGeometryIds.Count > 1;
        /// <summary>Total length of the merged geometry.</summary>
        public double Length => Geometry.Length;
    }

    /// <summary>
    /// A directed reference from a specific source geometry to a canonical <see cref="MergedEdge"/>.
    /// Produced by <see cref="MixedTopologyNetwork.BuildEdgeIndex"/> and used to reconstruct the
    /// original source geometry without duplicating shared edge coordinates.
    /// </summary>
    public class EdgeReference
    {
        /// <summary>The canonical merged edge shared across all sources that contain it.</summary>
        public MergedEdge Edge { get; init; }
        /// <summary>
        /// True if this source traverses <see cref="Edge"/> in the same direction as
        /// <see cref="MergedEdge.Geometry"/>. False means the geometry must be reversed
        /// before concatenation.
        /// </summary>
        public bool Forward { get; init; }
        /// <summary>
        /// The edge geometry oriented for this source's traversal direction.
        /// Equivalent to <see cref="MergedEdge.Geometry"/> when <see cref="Forward"/> is true,
        /// or its reverse when false.
        /// </summary>
        public LineString OrientedGeometry => Forward
            ? Edge.Geometry
            : (LineString)Edge.Geometry.Reverse();
    }

    /// <summary>
    /// A full topological description of a single source geometry within the network,
    /// including its shared and private edges and its immediate neighbours.
    /// </summary>
    public class NetworkDefinition
    {
        /// <summary>Id of the source geometry this definition describes.</summary>
        public int SourceId { get; init; }
        /// <summary>The source geometry record.</summary>
        public NetworkGeometry Source { get; init; }
        /// <summary>Every network edge that belongs to this source.</summary>
        public List<NetworkEdge> AllEdges { get; init; }
        /// <summary>Edges shared with at least one other source geometry.</summary>
        public List<NetworkEdge> SharedEdges { get; init; }
        /// <summary>Edges belonging exclusively to this source.</summary>
        public List<NetworkEdge> PrivateEdges { get; init; }
        /// <summary>All source geometries that share at least one edge with this source.</summary>
        public List<NetworkGeometry> Neighbours { get; init; }
        /// <summary>Sum of lengths of all edges belonging to this source.</summary>
        public double TotalLength => AllEdges.Sum(e => e.Geometry.Length);
        /// <summary>Sum of lengths of edges shared with other sources.</summary>
        public double SharedLength => SharedEdges.Sum(e => e.Geometry.Length);
    }

    /// <summary>
    /// Topological role of a <see cref="MergedEdge"/> relative to a specific source polygon.
    /// </summary>
    public enum EdgeRingKind
    {
        /// <summary>The edge lies on the exterior ring of the source polygon.</summary>
        ExteriorRing,
        /// <summary>The edge lies on an interior ring (hole) of the source polygon.</summary>
        InteriorRing,
        /// <summary>The source is a LineString, not a polygon.</summary>
        LineString,
        /// <summary>Contributing sources disagree on ring kind (e.g. exterior for one, interior for another).</summary>
        Mixed
    }

    /// <summary>
    /// Classifies a <see cref="MergedEdge"/> relative to one of its contributing source geometries.
    /// </summary>
    public class MergedEdgeClassification
    {
        /// <summary>Whether the edge is an exterior ring, interior ring, linestring, or mixed.</summary>
        public EdgeRingKind Kind { get; init; }
        /// <summary>Zero-based interior ring index when <see cref="Kind"/> is <see cref="EdgeRingKind.InteriorRing"/>; otherwise null.</summary>
        public int? RingIndex { get; init; }
        /// <summary>The source geometry this classification applies to.</summary>
        public NetworkGeometry Source { get; init; }
    }

    /// <summary>
    /// An integer grid key derived from a snapped coordinate, used for O(1) node lookup and
    /// deterministic union-find tie-breaking in the canonical coordinate map.
    /// Two coordinates that snap to the same grid cell produce equal keys.
    /// </summary>
    public readonly struct CoordinateKey : IEquatable<CoordinateKey>, IComparable<CoordinateKey>
    {
        private readonly long _x, _y;

        /// <summary>
        /// Constructs a key from a coordinate that has already been snapped to the tolerance grid.
        /// </summary>
        public CoordinateKey(Coordinate snappedCoord, double tolerance) {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(snappedCoord.X * inv);
            _y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y) { _x = x; _y = y; }

        /// <summary>Returns a key offset by the given number of grid cells in each axis.</summary>
        public CoordinateKey Offset(int dx, int dy) => new(_x + dx, _y + dy);
        /// <inheritdoc/>
        public bool Equals(CoordinateKey o) => _x == o._x && _y == o._y;
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is CoordinateKey k && Equals(k);
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(_x, _y);
        /// <summary>Lexicographic comparison by X then Y, used for deterministic union-find root selection.</summary>
        public int CompareTo(CoordinateKey o) {
            int c = _x.CompareTo(o._x);
            return c != 0 ? c : _y.CompareTo(o._y);
        }
    }

    internal sealed class RawSegment
    {
        public readonly int SegId;
        public readonly Coordinate P0, P1;
        public readonly int SourceId;
        public readonly Envelope Envelope;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId) {
            SegId = segId; P0 = p0; P1 = p1; SourceId = sourceId;
            Envelope = new Envelope(p0, p1);
        }
        public RawSegment WithCoordinates(Coordinate p0, Coordinate p1) => new(SegId, p0, p1, SourceId);
        public RawSegment WithSegId(int segId) => new(segId, P0, P1, SourceId);
    }

    internal readonly struct SplitPoint
    {
        public readonly Coordinate Coord;
        private readonly double _dist;
        public SplitPoint(Coordinate coord, Coordinate segStart) {
            Coord = coord; _dist = coord.Distance(segStart);
        }
        public static readonly IComparer<SplitPoint> Comparer =
            Comparer<SplitPoint>.Create((a, b) => a._dist.CompareTo(b._dist));
    }

    /// <summary>
    /// Builds a planar topology network from an arbitrary mix of Polygon and LineString geometries.
    /// Shared boundary segments are detected and stored as single <see cref="NetworkEdge"/> objects
    /// tagged with every contributing source id. The network can then be queried for shared/private
    /// edges, merged edge chains, and a compact edge index that reconstructs every source geometry
    /// from a deduplicated set of directed edge references.
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
        private STRtree<NetworkEdge> _edgeIndex = new();
        private Dictionary<CoordinateKey, Coordinate>? _canonicalMap;

        /// <summary>All network edges produced by the most recent <see cref="Build"/> call.</summary>
        public IReadOnlyList<NetworkEdge> Edges => _edges;
        /// <summary>All network nodes produced by the most recent <see cref="Build"/> call.</summary>
        public IReadOnlyCollection<NetworkNode> Nodes => _nodes.Values;
        /// <summary>Zero-based ids of all registered source geometries.</summary>
        public IEnumerable<int> Sources => _sources.Select(s => s.Id);

        /// <summary>
        /// Creates a new network.
        /// </summary>
        /// <param name="factory">NTS geometry factory to use; defaults to the NTS singleton.</param>
        /// <param name="snapTolerance">
        /// Grid size used to snap coordinates and identify coincident nodes. Default 1e-6 (suitable
        /// for geographic coordinates in degrees).
        /// </param>
        /// <param name="dedupeRadius">
        /// Maximum distance between two source vertices that will be merged into a single canonical
        /// coordinate. Defaults to five times <paramref name="snapTolerance"/>. Only applies to
        /// vertices connected by a raw segment shorter than this value, preventing accidental
        /// collapse of genuinely close-but-distinct vertices in thin sliver polygons.
        /// </param>
        public MixedTopologyNetwork(
            GeometryFactory? factory = null,
            double snapTolerance = 0.000001,
            double dedupeRadius = -1) {
            _factory = factory ?? NtsGeometryServices.Instance.CreateGeometryFactory();
            _snapTolerance = snapTolerance;
            _dedupeRadius = dedupeRadius > 0 ? dedupeRadius : snapTolerance * 5;
        }

        // -------------------------------------------------------------------
        // Registration
        // -------------------------------------------------------------------

        /// <summary>
        /// Registers a polygon and returns its source id.
        /// The exterior ring and all interior rings (holes) are added to the network.
        /// </summary>
        public int AddPolygon(Polygon polygon) => Register(polygon, GeometryKind.Polygon);

        /// <summary>
        /// Registers an open or closed LineString and returns its source id.
        /// If the geometry is a <see cref="LinearRing"/> it is treated as a closed ring.
        /// </summary>
        public int AddLineString(LineString lineString) => Register(lineString, GeometryKind.LineString);

        /// <summary>Registers multiple polygons in order, returning nothing (ids are sequential).</summary>
        public void AddPolygons(IEnumerable<Polygon> polygons) { foreach (var p in polygons) AddPolygon(p); }

        /// <summary>Registers multiple linestrings in order, returning nothing (ids are sequential).</summary>
        public void AddLineStrings(IEnumerable<LineString> lines) { foreach (var l in lines) AddLineString(l); }

        private int Register(Geometry geom, GeometryKind kind) {
            int id = _sources.Count;
            _sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------

        /// <summary>
        /// Constructs the planar network from all registered source geometries.
        /// Must be called before any query or edge-index methods. Steps:
        /// (1) extract raw directed segments from every source;
        /// (2) build a canonical coordinate map that collapses near-duplicate vertices;
        /// (3) detect all pairwise segment intersections and collect split points;
        /// (4) split segments at intersection points and insert the resulting sub-segments
        ///     as undirected <see cref="NetworkEdge"/> objects tagged with their source ids;
        /// (5) build a spatial index over the final edges.
        /// </summary>
        public void Build() {
            var rawSegments = ExtractRawSegments();

            var canonical = BuildCanonicalCoordinateMap(rawSegments);
            _canonicalMap = canonical;

            var cleaned = new List<RawSegment>(rawSegments.Count);
            foreach (var seg in rawSegments) {
                // Use CanonicalizeWithProximity so that source endpoints that are
                // within _dedupeRadius of an existing canonical entry (from another
                // source's short-segment union) snap to it — without creating new
                // entries for isolated coordinates that have no nearby canonical peer.
                // This handles the dangle case (two sources use slightly different
                // coords for the same node) without collapsing genuinely distinct
                // nearby vertices in thin slivers.
                var p0 = CanonicalizeWithProximity(SnapToGrid(seg.P0), canonical, registerIfNew: false);
                var p1 = CanonicalizeWithProximity(SnapToGrid(seg.P1), canonical, registerIfNew: false);
                if (!p0.Equals2D(p1)) cleaned.Add(seg.WithCoordinates(p0, p1));
            }

            var segments = new List<RawSegment>(cleaned.Count);
            for (int i = 0; i < cleaned.Count; i++) segments.Add(cleaned[i].WithSegId(i));

            var segIndex = new STRtree<RawSegment>();
            foreach (var seg in segments) segIndex.Insert(seg.Envelope, seg);

            var splitPoints = new Dictionary<int, SortedSet<SplitPoint>>(segments.Count);
            for (int i = 0; i < segments.Count; i++)
                splitPoints[i] = new SortedSet<SplitPoint>(SplitPoint.Comparer);

            var li = new RobustLineIntersector();
            foreach (var seg in segments) {
                var qe = seg.Envelope.Copy(); qe.ExpandBy(_snapTolerance);
                foreach (var other in segIndex.Query(qe)) {
                    if (other.SegId >= seg.SegId) continue;
                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;
                    for (int k = 0; k < li.IntersectionNum; k++) {
                        var pt = SnapToGrid(li.GetIntersection(k));
                        pt = CanonicalizeWithProximity(pt, canonical);
                        pt = SnapToEndpoint(pt, seg, other);
                        splitPoints[seg.SegId].Add(new SplitPoint(pt, seg.P0));
                        splitPoints[other.SegId].Add(new SplitPoint(pt, other.P0));
                    }
                }
            }

            // Step 4b: Interior-node splitting.
            // The pairwise intersection pass only finds proper crossings and
            // endpoint-touching. It misses collinear partial overlaps where
            // source A has segment p0→p2 and source B has p0→p1→p2 (p1 lies
            // on the interior of A's segment). Collect every candidate node
            // coordinate (all segment endpoints + all intersection split points)
            // into a spatial index, then for each segment check whether any of
            // those nodes lies on its interior and add it as an additional split.
            var nodeCoordIndex = new STRtree<Coordinate>();
            var nodeCoordSet = new Dictionary<CoordinateKey, Coordinate>();
            void RegisterNode(Coordinate pt) {
                var k = new CoordinateKey(pt, _snapTolerance);
                if (nodeCoordSet.TryAdd(k, pt))
                    nodeCoordIndex.Insert(new Envelope(pt), pt);
            }
            foreach (var seg in segments) { RegisterNode(seg.P0); RegisterNode(seg.P1); }
            foreach (var kv in splitPoints)
                foreach (var sp in kv.Value) RegisterNode(sp.Coord);

            foreach (var seg in segments) {
                var env = seg.Envelope.Copy(); env.ExpandBy(_snapTolerance);
                var segP0Key = new CoordinateKey(seg.P0, _snapTolerance);
                var segP1Key = new CoordinateKey(seg.P1, _snapTolerance);
                foreach (var nodePt in nodeCoordIndex.Query(env)) {
                    var k = new CoordinateKey(nodePt, _snapTolerance);
                    if (k.Equals(segP0Key) || k.Equals(segP1Key)) continue;
                    if (IsInteriorPoint(nodePt, seg.P0, seg.P1)) {
                        // Canonicalize the interior split point before adding
                        var canonPt = CanonicalizeWithProximity(nodePt, canonical);
                        splitPoints[seg.SegId].Add(new SplitPoint(canonPt, seg.P0));
                    }
                }
            }

            _nodes.Clear(); _edges.Clear(); _edgesBySource.Clear();
            var edgeMap = new Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge>();

            foreach (var seg in segments) {
                var splits = splitPoints[seg.SegId];
                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits)
                    if (!sp.Coord.Equals2D(coords[^1], _snapTolerance)) coords.Add(sp.Coord);
                if (!seg.P1.Equals2D(coords[^1], _snapTolerance)) coords.Add(seg.P1);

                for (int i = 0; i < coords.Count - 1; i++) {
                    if (!coords[i].Equals2D(coords[i + 1], _snapTolerance))
                        InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
                }
            }

            _edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in _edges) _edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }

        // -------------------------------------------------------------------
        // Segment extraction
        // -------------------------------------------------------------------

        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(EstimateSegmentCount());
            int segId = 0;
            foreach (var src in _sources) {
                if (src.Kind == GeometryKind.Polygon) {
                    var poly = (Polygon)src.Geometry;
                    segId = ExtractFromRing(poly.ExteriorRing.Coordinates, src.Id, result, segId, true);
                    foreach (var hole in poly.InteriorRings)
                        segId = ExtractFromRing(hole.Coordinates, src.Id, result, segId, true);
                }
                else {
                    segId = ExtractFromRing(src.Geometry.Coordinates, src.Id, result, segId,
                        src.IsRing);
                }
            }
            return result;
        }

        private int EstimateSegmentCount() => _sources.Sum(s => s.Geometry.NumPoints);

        private int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId, bool isRing) {

            // Strip closing duplicate before snapping so re-close is deterministic
            if (isRing && coords.Length > 1 &&
                SnapToGrid(coords[0]).Equals2D(SnapToGrid(coords[^1])))
                coords = coords[..^1];

            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1])) snapped.Add(sc);
            }

            // For rings: if the last snapped coordinate is within _dedupeRadius of the
            // first (a self-closing dangle), snap the last to the first.
            if (isRing && snapped.Count > 1 &&
                !snapped[0].Equals2D(snapped[^1]) &&
                snapped[0].Distance(snapped[^1]) <= _dedupeRadius)
                snapped[^1] = snapped[0];

            if (isRing && snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(snapped[0]);

            for (int i = 0; i < snapped.Count - 1; i++) {
                var p0 = snapped[i]; var p1 = snapped[i + 1];
                if (p0.Equals2D(p1)) continue;
                // Canonical direction: smaller coordinate first
                if (CompareCoords(p0, p1) > 0) (p0, p1) = (p1, p0);
                result.Add(new RawSegment(segId++, p0, p1, sourceId));
            }
            return segId;
        }

        private static int CompareCoords(Coordinate a, Coordinate b) {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        // -------------------------------------------------------------------
        // Snapping / canonicalization
        // -------------------------------------------------------------------

        private Coordinate SnapToGrid(Coordinate c) {
            double inv = 1.0 / _snapTolerance;
            double x = Math.Round(c.X * inv) / inv;
            double y = Math.Round(c.Y * inv) / inv;
            var coord = double.IsNaN(c.Z) ? new Coordinate(x, y) : new CoordinateZ(x, y, c.Z);
            _factory.PrecisionModel.MakePrecise(coord);
            return coord;
        }

        private Coordinate Canonicalize(Coordinate c) {
            var snapped = SnapToGrid(c);
            if (_canonicalMap == null) return snapped;
            var key = new CoordinateKey(snapped, _snapTolerance);
            return _canonicalMap.TryGetValue(key, out var canon) ? canon : snapped;
        }

        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {

            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments)
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key)) cellValue[key] = snapped;
                }

            var parent = new Dictionary<CoordinateKey, CoordinateKey>();
            foreach (var key in cellValue.Keys) parent[key] = key;

            CoordinateKey Find(CoordinateKey k) {
                while (parent.TryGetValue(k, out var p) && !p.Equals(k)) k = p;
                return k;
            }
            void Union(CoordinateKey a, CoordinateKey b) {
                var ra = Find(a); var rb = Find(b);
                if (ra.Equals(rb)) return;
                if (ra.CompareTo(rb) < 0) parent[rb] = ra; else parent[ra] = rb;
            }

            // Only union endpoints of short segments that are shared by MORE THAN
            // ONE source. A short segment private to a single source is a legitimate
            // tiny feature (e.g. a small ring closing segment) and should not be
            // collapsed. A short segment shared by multiple sources is a cross-source
            // digitizing dangle where two sources use slightly different coordinates
            // for the same logical node — those should be unified.
            var segSourceCount = new Dictionary<(CoordinateKey, CoordinateKey), int>();
            foreach (var seg in rawSegments) {
                var k0 = new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance);
                var k1 = new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance);
                var segKey = k0.CompareTo(k1) < 0 ? (k0, k1) : (k1, k0);
                segSourceCount.TryGetValue(segKey, out int cnt);
                segSourceCount[segKey] = cnt + 1;
            }

            // Extended radius for shared segments: two sources digitizing the same node
            // slightly differently can produce a short connecting segment up to ~6e-6
            // long. Private segments only get the standard _dedupeRadius.
            double sharedRadius = _dedupeRadius * 1.04; // 5.2e-6: catches 5.099e-6 dangle, stays below 5.385e-6

            foreach (var seg in rawSegments) {
                double d = seg.P0.Distance(seg.P1);
                if (d > sharedRadius) continue;
                var k0 = new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance);
                var k1 = new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance);
                var segKey = k0.CompareTo(k1) < 0 ? (k0, k1) : (k1, k0);
                if (!segSourceCount.TryGetValue(segKey, out int cnt)) continue;
                // Shared (cnt > 1): union up to sharedRadius.
                // Private (cnt == 1): union up to _dedupeRadius. All genuinely
                // private short segments in real data are digitizing artefacts on
                // data boundaries (back-and-forth micro-steps). Legitimate tiny ring
                // features that need to remain distinct are always shared (cnt > 1)
                // and are handled by the shared rule above.
                bool shouldUnion = cnt > 1
                    ? d <= sharedRadius
                    : d <= _dedupeRadius;
                if (shouldUnion) Union(k0, k1);
            }

            var result = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var key in cellValue.Keys) result[key] = cellValue[Find(key)];
            return result;
        }

        /// <summary>
        /// Looks up pt in the canonical map. If not found exactly, searches nearby cells
        /// within _dedupeRadius for an existing canonical entry and snaps to the closest one.
        /// If still not found, registers pt as a new canonical entry so future nearby points
        /// resolve to the same value. This handles computed intersection points that may land
        /// 1 ULP apart from the same logical point depending on which segment pair computed them.
        /// </summary>
        /// <param name="registerIfNew">
        /// When true (used for computed intersection points), registers pt as a new
        /// canonical entry if no nearby peer exists, so future nearby intersection
        /// points resolve to the same value. When false (used for source segment
        /// endpoints), returns pt unchanged if no nearby canonical entry is found,
        /// preventing genuinely distinct-but-close source vertices from being merged.
        /// </param>
        private Coordinate CanonicalizeWithProximity(
            Coordinate pt, Dictionary<CoordinateKey, Coordinate> canonical,
            bool registerIfNew = true) {

            var key = new CoordinateKey(pt, _snapTolerance);
            if (canonical.TryGetValue(key, out var exact)) return exact;

            int cellRadius = (int)Math.Ceiling(_dedupeRadius / _snapTolerance);
            Coordinate? best = null;
            double bestDist = double.MaxValue;

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
                for (int dy = -cellRadius; dy <= cellRadius; dy++) {
                    var nk = key.Offset(dx, dy);
                    if (canonical.TryGetValue(nk, out var nv)) {
                        double d = pt.Distance(nv);
                        if (d <= _dedupeRadius && d < bestDist) { bestDist = d; best = nv; }
                    }
                }

            if (best != null) { canonical[key] = best; return best; }

            if (registerIfNew) canonical[key] = pt;
            return pt;
        }

        private Coordinate SnapToEndpoint(Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 })
                if (pt.Distance(ep) <= _snapTolerance) return ep;
            return pt;
        }

        /// <summary>
        /// Returns true if <paramref name="pt"/> lies strictly on the interior of the
        /// segment <paramref name="segStart"/>→<paramref name="segEnd"/> (i.e. not within
        /// <see cref="_snapTolerance"/> of either endpoint) and within
        /// <see cref="_snapTolerance"/> of the segment's infinite line.
        /// Used by the interior-node splitting pass in <see cref="Build"/>.
        /// </summary>
        private bool IsInteriorPoint(Coordinate pt, Coordinate segStart, Coordinate segEnd) {
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < _snapTolerance * _snapTolerance) return false;

            // Parameter t of the projection of pt onto the segment line
            double t = ((pt.X - segStart.X) * dx + (pt.Y - segStart.Y) * dy) / lenSq;

            // Must be strictly interior — not within snapTolerance of either endpoint
            double len = Math.Sqrt(lenSq);
            if (t * len <= _snapTolerance || (1.0 - t) * len <= _snapTolerance) return false;

            // Distance from pt to the closest point on the infinite line
            double cx = segStart.X + t * dx;
            double cy = segStart.Y + t * dy;
            double distToLine = Math.Sqrt((pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy));

            return distToLine <= _snapTolerance;
        }

        // -------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------

        private void InsertEdge(
            Coordinate c0, Coordinate c1, int sourceId,
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {

            if (c0.Equals2D(c1, _snapTolerance)) return;

            var k0 = new CoordinateKey(c0, _snapTolerance);
            var k1 = new CoordinateKey(c1, _snapTolerance);
            bool swap = k0.CompareTo(k1) > 0;
            var key = swap ? (k1, k0) : (k0, k1);

            if (!edgeMap.TryGetValue(key, out var edge)) {
                var (gc0, gc1) = swap ? (c1, c0) : (c0, c1);
                var ls = _factory.CreateLineString(new[] { gc0, gc1 });
                edge = new NetworkEdge { Id = _edges.Count, Geometry = ls, StartNode = gc0, EndNode = gc1 };
                _edges.Add(edge);
                edgeMap[key] = edge;
                GetOrCreateNode(gc0).Edges.Add(edge);
                GetOrCreateNode(gc1).Edges.Add(edge);
            }

            edge.SourceGeometryIds.Add(sourceId);
            if (!_edgesBySource.TryGetValue(sourceId, out var list))
                _edgesBySource[sourceId] = list = new List<NetworkEdge>();
            if (!list.Contains(edge)) list.Add(edge);
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
        /// Returns all network edges that belong to the given source geometry, in the order
        /// they were first inserted during <see cref="Build"/>.
        /// </summary>
        public IReadOnlyList<NetworkEdge> GetEdgesFor(int sourceId)
            => _edgesBySource.TryGetValue(sourceId, out var list) ? list : Array.Empty<NetworkEdge>();

        /// <summary>
        /// Returns all edges shared between exactly the two given source geometry ids.
        /// </summary>
        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => GetEdgesFor(sourceIdA).Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        /// <summary>
        /// Returns every edge in the network that is shared by two or more source geometries.
        /// </summary>
        public IEnumerable<NetworkEdge> GetAllSharedEdges() => _edges.Where(e => e.IsShared);

        /// <summary>
        /// Returns the source geometry records that contribute the given edge.
        /// </summary>
        public IEnumerable<NetworkGeometry> GetSourceGeometriesForEdge(NetworkEdge edge)
            => edge.SourceGeometryIds.Select(id => _sources[id]);

        /// <summary>
        /// Returns the network node at the given coordinate (within snap tolerance), or null
        /// if no node exists there.
        /// </summary>
        public NetworkNode? GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            return _nodes.TryGetValue(key, out var node) ? node : null;
        }

        /// <summary>
        /// Returns all edges whose bounding box intersects the given envelope.
        /// </summary>
        public IEnumerable<NetworkEdge> QueryEdges(Envelope envelope)
            => _edgeIndex.Query(envelope).Cast<NetworkEdge>();

        /// <summary>
        /// Returns a full topological description of a source geometry, including its shared
        /// edges, private edges, and neighbouring source geometries.
        /// </summary>
        public NetworkDefinition GetNetworkDefinition(int sourceId) {
            var edges = GetEdgesFor(sourceId);
            var shared = edges.Where(e => e.IsShared).ToList();
            var priv = edges.Where(e => !e.IsShared).ToList();
            var nbrIds = shared.SelectMany(e => e.SourceGeometryIds)
                               .Where(id => id != sourceId).Distinct().ToList();
            return new NetworkDefinition {
                SourceId = sourceId,
                Source = _sources[sourceId],
                AllEdges = edges.ToList(),
                SharedEdges = shared,
                PrivateEdges = priv,
                Neighbours = nbrIds.Select(id => _sources[id]).ToList(),
            };
        }

        // -------------------------------------------------------------------
        // Full edge chain
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns all network edges belonging to the given source geometry in topological
        /// traversal order — i.e. consecutive edges share a node. The seed edge is chosen
        /// by scanning the source's original coordinate sequence for the first consecutive
        /// pair that forms a network edge, ensuring a consistent and reproducible starting
        /// position independent of edge insertion order. For closed rings the list forms a
        /// complete loop; for open linestrings both ends are included via forward and backward
        /// walks from the seed.
        /// </summary>
        public List<NetworkEdge> GetFullEdgeChainFor(int sourceId) {
            var edges = GetEdgesFor(sourceId).ToHashSet();
            if (edges.Count == 0) return new List<NetworkEdge>();

            var adj = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            void Touch(Coordinate c, NetworkEdge e) {
                var key = new CoordinateKey(c, _snapTolerance);
                if (!adj.TryGetValue(key, out var list)) adj[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }
            foreach (var edge in edges) { Touch(edge.StartNode, edge); Touch(edge.EndNode, edge); }

            var srcCoords = _sources[sourceId].Geometry.Coordinates;
            NetworkEdge? seed = null;
            for (int i = 0; i < srcCoords.Length - 1 && seed == null; i++) {
                var a = new CoordinateKey(Canonicalize(srcCoords[i]), _snapTolerance);
                var b = new CoordinateKey(Canonicalize(srcCoords[i + 1]), _snapTolerance);
                if (!adj.TryGetValue(a, out var atA)) continue;
                foreach (var candidate in atA) {
                    var ks = new CoordinateKey(candidate.StartNode, _snapTolerance);
                    var ke = new CoordinateKey(candidate.EndNode, _snapTolerance);
                    if ((ks.Equals(a) && ke.Equals(b)) || (ke.Equals(a) && ks.Equals(b))) {
                        seed = candidate; break;
                    }
                }
            }
            seed ??= edges.First();

            return WalkChain(seed, edges, adj);
        }

        private List<NetworkEdge> WalkChain(
            NetworkEdge seed,
            HashSet<NetworkEdge> edgeSet,
            Dictionary<CoordinateKey, List<NetworkEdge>> adj) {

            var visited = new HashSet<int> { seed.Id };
            var forward = new List<NetworkEdge>();
            var backward = new List<NetworkEdge>();

            // Forward walk from seed.EndNode
            var coord = seed.EndNode;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adj.TryGetValue(key, out var nbrs)) break;
                var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (cands.Count != 1) break;
                var next = cands[0];

                visited.Add(next.Id);
                forward.Add(next);

                // Ring closed geometrically at seed's start node
                bool closedAtSeedStart =
                    next.StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                    next.EndNode.Equals2D(seed.StartNode, _snapTolerance);

                // Ring closed because all edges in this source are consumed
                // (handles rings where the seed was not at a natural closure node)
                bool allVisited = visited.Count == edgeSet.Count;

                if (closedAtSeedStart || allVisited) break;

                coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
            }

            // Determine if a ring was completed — either geometrically or by exhaustion
            bool ringClosed =
                visited.Count == edgeSet.Count ||
                (forward.Count > 0 && (
                    forward[^1].StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                    forward[^1].EndNode.Equals2D(seed.StartNode, _snapTolerance)));

            // Only do backward walk for open linestrings
            if (!ringClosed) {
                coord = seed.StartNode;
                while (true) {
                    var key = new CoordinateKey(coord, _snapTolerance);
                    if (!adj.TryGetValue(key, out var nbrs)) break;
                    var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                    if (cands.Count != 1) break;
                    var prev = cands[0];
                    visited.Add(prev.Id);
                    backward.Add(prev);
                    coord = prev.StartNode.Equals2D(coord, _snapTolerance) ? prev.EndNode : prev.StartNode;
                }
            }

            backward.Reverse();
            var chain = new List<NetworkEdge>(backward.Count + 1 + forward.Count);
            chain.AddRange(backward); chain.Add(seed); chain.AddRange(forward);
            return chain;
        }

        // -------------------------------------------------------------------
        // Stitching
        // -------------------------------------------------------------------

        private Coordinate[] StitchChain(List<NetworkEdge> chain) {
            if (chain.Count == 0) return Array.Empty<Coordinate>();
            if (chain.Count == 1)
                return chain[0].Geometry.Coordinates.Select(Canonicalize).ToArray();

            var coords = new List<Coordinate>();
            var e0 = chain[0]; var e1 = chain[1];
            bool e0Fwd = e0.EndNode.Equals2D(e1.StartNode, _snapTolerance)
                      || e0.EndNode.Equals2D(e1.EndNode, _snapTolerance);
            var first = e0Fwd ? e0.Geometry.Coordinates : e0.Geometry.Coordinates.Reverse().ToArray();
            foreach (var c in first) coords.Add(Canonicalize(c));

            for (int i = 1; i < chain.Count; i++) {
                var edge = chain[i]; var prevEnd = coords[^1];
                bool fwd;
                if (edge.StartNode.Equals2D(prevEnd, _snapTolerance)) fwd = true;
                else if (edge.EndNode.Equals2D(prevEnd, _snapTolerance)) fwd = false;
                else fwd = edge.StartNode.Distance(prevEnd) <= edge.EndNode.Distance(prevEnd);
                var ec = fwd ? edge.Geometry.Coordinates : edge.Geometry.Coordinates.Reverse().ToArray();
                foreach (var c in ec.Skip(1)) coords.Add(Canonicalize(c));
            }
            return coords.ToArray();
        }

        // -------------------------------------------------------------------
        // GetMergedEdgeChainFor
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns the boundary of a source geometry as an ordered list of <see cref="MergedEdge"/>
        /// objects. Consecutive network edges that share the same <see cref="NetworkEdge.SourceGeometryIds"/>
        /// set are stitched into a single merged edge. Break points occur wherever the shared-source
        /// set changes — e.g. where a private boundary section meets a section shared with another
        /// geometry. The geometry of each merged edge is oriented by graph connectivity, not by the
        /// source's original traversal direction; use <see cref="BuildEdgeIndex"/> to obtain
        /// per-source directed <see cref="EdgeReference"/> lists.
        /// </summary>
        public List<MergedEdge> GetMergedEdgeChainFor(int sourceId) {
            var chain = GetFullEdgeChainFor(sourceId);
            if (chain.Count == 0) return new List<MergedEdge>();

            var result = new List<MergedEdge>();
            var current = new List<NetworkEdge> { chain[0] };

            for (int i = 1; i < chain.Count; i++) {
                if (chain[i].SourceGeometryIds.SetEquals(current[^1].SourceGeometryIds))
                    current.Add(chain[i]);
                else { result.Add(BuildMergedEdge(current)); current = new List<NetworkEdge> { chain[i] }; }
            }
            if (current.Count > 0) result.Add(BuildMergedEdge(current));
            return result;
        }

        private MergedEdge BuildMergedEdge(List<NetworkEdge> edges) => new MergedEdge {
            Geometry = _factory.CreateLineString(StitchChain(edges)),
            SourceGeometryIds = edges.SelectMany(e => e.SourceGeometryIds).Distinct().ToHashSet(),
            ConstituentEdges = edges.ToList(),
        };

        // -------------------------------------------------------------------
        // BuildEdgeIndex
        // -------------------------------------------------------------------

        /// <summary>
        /// Constructs a compact, deduplicated edge index for the entire network and returns:
        /// <list type="bullet">
        ///   <item><description>
        ///     <c>AllEdges</c> — every unique <see cref="MergedEdge"/> in the network exactly once.
        ///     Two merged edges are considered identical when they cover the same set of constituent
        ///     <see cref="NetworkEdge"/> ids, regardless of direction. The first source to produce a
        ///     given group defines its canonical geometry direction.
        ///   </description></item>
        ///   <item><description>
        ///     <c>SourceRefs</c> — for each source id, an ordered list of <see cref="EdgeReference"/>
        ///     objects that, when concatenated using <see cref="EdgeReference.OrientedGeometry"/>,
        ///     exactly reconstruct the source's original boundary. Each reference points to a
        ///     canonical edge from <c>AllEdges</c> and carries a <c>Forward</c> flag indicating
        ///     whether the canonical geometry must be reversed for this source.
        ///   </description></item>
        /// </list>
        /// Ring source chains are rotated to a group boundary before reference list construction
        /// to prevent the same canonical edge from appearing at both the start and end of the list.
        /// </summary>
        /// <summary>
        /// Builds a deduplicated edge index for the network.
        /// Returns:
        /// <list type="bullet">
        ///   <item><c>AllEdges</c> — every unique <see cref="MergedEdge"/> exactly once.
        ///   Edges with the same constituent <see cref="NetworkEdge"/> ids are the same edge.
        ///   Overlapping sub-edges (caused by ring seam splits) are merged into the larger group.</item>
        ///   <item><c>SourceEdges</c> — for each source id, the ordered list of
        ///   canonical <see cref="MergedEdge"/> objects that together define its boundary.
        ///   No direction flag — each edge's geometry is as produced by <see cref="StitchChain"/>
        ///   from the first source that registered it.</item>
        /// </list>
        /// </summary>
        public (List<MergedEdge> AllEdges, Dictionary<int, List<MergedEdge>> SourceEdges)
            BuildEdgeIndex() {

            // Pass 1: collect canonical MergedEdges from all sources.
            // Key = sorted NetworkEdge ids (direction-independent).
            // When a ring-seam split produces a small piece (subset) and a large piece
            // (superset) with the same SourceGeometryIds, keep only the larger one.
            // We maintain a per-NetworkEdge-id map to detect these subset relationships.
            var canonicalByKey = new Dictionary<string, MergedEdge>();
            var allMergedEdges = new List<MergedEdge>();
            // Maps each NetworkEdge.Id -> the current largest canonical that contains it
            // (with a given SourceGeometryIds signature).
            // Key: (sorted-source-ids-string, network-edge-id)
            // Key: (source-sig, producing-source-id, network-edge-id)
            // Including producingSrcId ensures we only ever replace a canonical with a larger
            // one from THE SAME producing source (ring-seam-split case). Cross-source
            // replacement is blocked entirely.
            var edgeIdToLargest = new Dictionary<(string srcSig, int producingSrcId, int edgeId), MergedEdge>();

            foreach (var src in _sources) {
                foreach (var merged in GetMergedEdgeChainFor(src.Id)) {
                    var key = string.Join(",", merged.ConstituentEdges.Select(e => e.Id).OrderBy(id => id));
                    var srcSig = string.Join(",", merged.SourceGeometryIds.OrderBy(id => id));

                    if (canonicalByKey.ContainsKey(key)) continue; // exact duplicate

                    // Check if any constituent edge of this merged group already belongs
                    // to a LARGER canonical with the same source signature, AND that larger
                    // canonical contains ALL of this group's edges (true superset).
                    // If so, this merged group is a subset — skip it.
                    // We require ALL edges to be present to avoid false matches where a
                    // large canonical from a different source spans across a degree-4 node
                    // that splits our source's path.
                    bool isSubset = false;
                    foreach (var e in merged.ConstituentEdges) {
                        if (edgeIdToLargest.TryGetValue((srcSig, src.Id, e.Id), out var larger) &&
                            larger.ConstituentEdges.Count > merged.ConstituentEdges.Count) {
                            // Verify true superset: all our edges are in the larger canonical
                            var largerIds = larger.ConstituentEdges.Select(x => x.Id).ToHashSet();
                            if (merged.ConstituentEdges.All(x => largerIds.Contains(x.Id))) {
                                isSubset = true;
                                break;
                            }
                        }
                    }
                    if (isSubset) continue;

                    // Check if any constituent edge belongs to a SMALLER canonical with
                    // the same source signature — if so, replace it, but only if the
                    // candidate smaller canonicals together account for ALL edges of this
                    // larger canonical. If they only cover PART of it, the larger canonical
                    // bridges across two separate runs from another source (which visited
                    // those segs in two disconnected passes), so absorbing would destroy
                    // that source's connectivity.
                    var toRemove = new HashSet<string>();
                    var thisIds = merged.ConstituentEdges.Select(e => e.Id).ToHashSet();
                    var candidatesToAbsorb = new Dictionary<string, MergedEdge>();
                    var coveredBySmaller = new HashSet<int>();
                    foreach (var e in merged.ConstituentEdges) {
                        if (edgeIdToLargest.TryGetValue((srcSig, src.Id, e.Id), out var smaller) &&
                            smaller.ConstituentEdges.Count < merged.ConstituentEdges.Count) {
                            if (smaller.ConstituentEdges.All(x => thisIds.Contains(x.Id))) {
                                var smallerKey = string.Join(",",
                                    smaller.ConstituentEdges.Select(x => x.Id).OrderBy(id => id));
                                if (candidatesToAbsorb.TryAdd(smallerKey, smaller))
                                    foreach (var se in smaller.ConstituentEdges)
                                        coveredBySmaller.Add(se.Id);
                            }
                        }
                    }
                    // Only absorb when the candidates together tile the larger exactly:
                    //   GOOD: src A makes one big canonical that fully covers segs from
                    //         src B's many small consecutive pieces => coveredBySmaller == thisIds
                    //   BAD:  src A's big canonical bridges a gap in src B's traversal,
                    //         spanning two disconnected smaller pieces => partial coverage only
                    if (candidatesToAbsorb.Count > 0 && coveredBySmaller.SetEquals(thisIds)) {
                        toRemove.UnionWith(candidatesToAbsorb.Keys);
                    }
                    foreach (var rk in toRemove) {
                        if (canonicalByKey.TryGetValue(rk, out var removed)) {
                            canonicalByKey.Remove(rk);
                            allMergedEdges.Remove(removed);
                        }
                    }

                    // Register this merged group as canonical
                    canonicalByKey[key] = merged;
                    allMergedEdges.Add(merged);
                    foreach (var e in merged.ConstituentEdges)
                        edgeIdToLargest[(srcSig, src.Id, e.Id)] = merged;
                }
            }

            // Build NetworkEdge.Id -> canonical lookup from the clean set.
            var edgeToCanonical = new Dictionary<int, MergedEdge>();
            foreach (var canonical in canonicalByKey.Values)
                foreach (var e in canonical.ConstituentEdges)
                    edgeToCanonical[e.Id] = canonical;

            // Ensure every NetworkEdge in the network is represented.
            // Any edge not reached by GetMergedEdgeChainFor for any source
            // (e.g. due to a walk gap) gets a single-edge fallback canonical.
            foreach (var netEdge in _edges) {
                if (edgeToCanonical.ContainsKey(netEdge.Id)) continue;
                var fallback = new MergedEdge {
                    Geometry = _factory.CreateLineString(
                                            new[] { netEdge.StartNode, netEdge.EndNode }),
                    SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                    ConstituentEdges = new List<NetworkEdge> { netEdge },
                };
                edgeToCanonical[netEdge.Id] = fallback;
                allMergedEdges.Add(fallback);
            }

            // Pass 2: build per-source ordered lists of canonical MergedEdges.
            // Walk the raw NetworkEdge chain for each source (via GetFullEdgeChainFor),
            // rotate rings to a canonical-group boundary, then map each NetworkEdge to
            // its canonical MergedEdge and deduplicate consecutive identical canonicals.
            var sourceEdges = new Dictionary<int, List<MergedEdge>>();

            foreach (var src in _sources) {
                var chain = GetFullEdgeChainFor(src.Id);
                if (chain.Count == 0) { sourceEdges[src.Id] = new List<MergedEdge>(); continue; }

                // Build a source-local edgeToCanonical from this source's own merged groups.
                // This ensures that large foreign canonicals (from other sources sharing some
                // edges) do not displace this source's natural group boundaries — both for
                // rotation and for the result list itself. Fall back to the global map for
                // any edge not covered by this source's own walk.
                var localEdgeToCanonical = new Dictionary<int, MergedEdge>();
                foreach (var merged in GetMergedEdgeChainFor(src.Id))
                    foreach (var e in merged.ConstituentEdges)
                        if (!localEdgeToCanonical.ContainsKey(e.Id))
                            localEdgeToCanonical[e.Id] = merged;

                if (src.IsRing && chain.Count > 1)
                    chain = RotateToGroupBoundary(chain,
                        localEdgeToCanonical.Count > 0 ? localEdgeToCanonical : edgeToCanonical);

                var result = new List<MergedEdge>();
                MergedEdge? last = null;

                foreach (var netEdge in chain) {
                    // Map to canonical: prefer this source's own canonical (local), falling
                    // back to the global map so no NetworkEdge is silently dropped.
                    if (!localEdgeToCanonical.TryGetValue(netEdge.Id, out var canonical) &&
                        !edgeToCanonical.TryGetValue(netEdge.Id, out canonical)) {
                        canonical = new MergedEdge {
                            Geometry = _factory.CreateLineString(
                                                    new[] { netEdge.StartNode, netEdge.EndNode }),
                            SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                            ConstituentEdges = new List<NetworkEdge> { netEdge },
                        };
                        edgeToCanonical[netEdge.Id] = canonical;
                        allMergedEdges.Add(canonical);
                    }
                    // Safety check: the canonical must actually contain this NetworkEdge.
                    // If edgeIdToLargest replacement produced a canonical from a different
                    // part of the network (e.g. due to a shared split node), fall back to
                    // a single-edge canonical scoped to this edge only.
                    else if (!canonical.ConstituentEdges.Any(e => e.Id == netEdge.Id)) {
                        canonical = new MergedEdge {
                            Geometry = _factory.CreateLineString(
                                                    new[] { netEdge.StartNode, netEdge.EndNode }),
                            SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                            ConstituentEdges = new List<NetworkEdge> { netEdge },
                        };
                        // Don't update edgeToCanonical — the existing canonical may be
                        // correct for other sources; only this source gets the fallback.
                        allMergedEdges.Add(canonical);
                    }
                    if (ReferenceEquals(canonical, last)) continue;
                    result.Add(canonical);
                    last = canonical;
                }

                // For rings: if seam split caused first==last canonical, remove the duplicate.
                if (src.IsRing && result.Count > 1 &&
                    ReferenceEquals(result[0], result[^1]))
                    result.RemoveAt(result.Count - 1);

                sourceEdges[src.Id] = result;
            }

            return (allMergedEdges, sourceEdges);
        }

        /// <summary>
        /// Resolves a <see cref="LineString"/> against the already-built network
        /// (from <see cref="BuildEdgeIndex"/>) and returns an ordered list of
        /// <see cref="MergedEdge"/> objects that together cover the full path.
        /// <para>
        /// Edges that already exist in the network (<paramref name="allEdges"/>) are
        /// returned as-is. Portions of the linestring that fall between two existing
        /// network nodes but have no canonical edge are returned as new
        /// <see cref="MergedEdge"/> objects that are <b>not</b> added to the network —
        /// they are ephemeral, for this linestring only.
        /// </para>
        /// <para>
        /// Call <see cref="Build"/> and <see cref="BuildEdgeIndex"/> before calling
        /// this method.
        /// </para>
        /// </summary>
        /// <param name="lineString">The open (non-ring) linestring to resolve.</param>
        /// <param name="allEdges">
        /// The canonical edge list returned by <see cref="BuildEdgeIndex"/>.
        /// Used to look up existing canonical edges by their constituent node pair.
        /// </param>
        public List<MergedEdge> ResolveLineString(
            LineString lineString,
            List<MergedEdge> allEdges) {

            // Build a lookup: canonical (StartNode, EndNode) pair -> MergedEdge.
            // Both orderings are stored so direction doesn't matter for lookup.
            if (_nodeEdgeLookup == null)
                _nodeEdgeLookup = BuildNodeEdgeLookup(allEdges);

            // Snap all linestring coordinates to the network's canonical grid.
            // Any coordinate within _snapTolerance of an existing network node
            // is mapped to that node; unknown coordinates stay as-is.
            var raw = lineString.Coordinates;
            var snapped = new List<Coordinate>(raw.Length);
            foreach (var c in raw) {
                var key = new CoordinateKey(SnapToGrid(c), _snapTolerance);
                if (_nodes.TryGetValue(key, out var node))
                    snapped.Add(node.Coordinate);
                else
                    snapped.Add(SnapToGrid(c));
            }

            // Remove consecutive duplicates produced by snapping.
            var pts = new List<Coordinate> { snapped[0] };
            for (int i = 1; i < snapped.Count; i++)
                if (!snapped[i].Equals2D(pts[^1]))
                    pts.Add(snapped[i]);

            // Walk pts, grouping consecutive coordinates by whether they form
            // a known network edge (StartNode, EndNode) pair.
            var result = new List<MergedEdge>();

            int start = 0;
            while (start < pts.Count - 1) {
                // Try extending from pts[start] to find the longest run that
                // matches a single canonical MergedEdge in the network.
                MergedEdge? matched = null;
                int matchedEnd = -1;

                for (int end = pts.Count - 1; end > start; end--) {
                    var key = MakeNodePairKey(pts[start], pts[end]);
                    if (_nodeEdgeLookup.TryGetValue(key, out var edge)) {
                        matched = edge;
                        matchedEnd = end;
                        break;
                    }
                }

                if (matched != null) {
                    result.Add(matched);
                    start = matchedEnd;
                }
                else {
                    // No existing edge covers pts[start]→pts[start+1].
                    // Advance one step and build an ephemeral single-segment edge.
                    var ephemeral = new MergedEdge {
                        Geometry = _factory.CreateLineString(
                                                new[] { pts[start], pts[start + 1] }),
                        SourceGeometryIds = new HashSet<int>(),
                        ConstituentEdges = new List<NetworkEdge>(),
                    };
                    result.Add(ephemeral);
                    start++;
                }
            }

            return result;
        }

        // Cached node-pair -> MergedEdge lookup built on first ResolveLineString call.
        private Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>? _nodeEdgeLookup;

        private Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>
            BuildNodeEdgeLookup(List<MergedEdge> allEdges) {

            var lookup = new Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>();
            foreach (var edge in allEdges) {
                var coords = edge.Geometry.Coordinates;
                var k0 = new CoordinateKey(coords[0], _snapTolerance);
                var k1 = new CoordinateKey(coords[^1], _snapTolerance);
                // Store both orderings so direction-agnostic lookup works.
                lookup.TryAdd((k0, k1), edge);
                lookup.TryAdd((k1, k0), edge);
                // Also index every constituent NetworkEdge endpoint pair so
                // sub-edges of a longer merged edge can be resolved individually.
                foreach (var ne in edge.ConstituentEdges) {
                    var nk0 = new CoordinateKey(ne.StartNode, _snapTolerance);
                    var nk1 = new CoordinateKey(ne.EndNode, _snapTolerance);
                    lookup.TryAdd((nk0, nk1), edge);
                    lookup.TryAdd((nk1, nk0), edge);
                }
            }
            return lookup;
        }

        private (CoordinateKey, CoordinateKey) MakeNodePairKey(
            Coordinate a, Coordinate b) =>
            (new CoordinateKey(a, _snapTolerance), new CoordinateKey(b, _snapTolerance));

        private List<NetworkEdge> RotateToGroupBoundary(
            List<NetworkEdge> chain,
            Dictionary<int, MergedEdge> edgeToCanonical) {

            for (int i = 1; i < chain.Count; i++) {
                if (!edgeToCanonical.TryGetValue(chain[i - 1].Id, out var prev)) continue;
                if (!edgeToCanonical.TryGetValue(chain[i].Id, out var curr)) continue;
                if (!ReferenceEquals(prev, curr)) {
                    var rotated = new List<NetworkEdge>(chain.Count);
                    rotated.AddRange(chain.Skip(i));
                    rotated.AddRange(chain.Take(i));
                    return rotated;
                }
            }
            return chain;
        }

        // -------------------------------------------------------------------
        // Reconstruct geometry from EdgeReferences
        // -------------------------------------------------------------------

        /// <summary>
        /// Reconstructs the full boundary geometry of a source from its <see cref="EdgeReference"/>
        /// list as returned by <see cref="BuildEdgeIndex"/>. Concatenates the oriented geometry of
        /// each reference in order, skipping the duplicate junction coordinate between consecutive
        /// edges. For ring sources the last coordinate is forced to exactly equal the first to
        /// guarantee closure.
        /// </summary>
        public LineString ReconstructGeometry(List<EdgeReference> refs, int sourceId) {
            if (refs.Count == 0) return null!;
            var coords = new List<Coordinate>();
            foreach (var r in refs) {
                var ec = r.OrientedGeometry.Coordinates;
                if (coords.Count == 0) coords.AddRange(ec.Select(Canonicalize));
                else coords.AddRange(ec.Skip(1).Select(Canonicalize));
            }
            var source = _sources[sourceId];
            bool isRing = source.IsRing;
            if (isRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
            return _factory.CreateLineString(coords.ToArray());
        }

        // -------------------------------------------------------------------
        // Orientation
        // -------------------------------------------------------------------

        /// <summary>
        /// Returns true if the ring described by the given <see cref="EdgeReference"/> list is
        /// counter-clockwise (CCW), false if clockwise (CW). Uses the NTS shoelace-based
        /// <see cref="NetTopologySuite.Algorithm.Orientation.IsCCW"/> test. In standard geographic
        /// coordinates (longitude/latitude), exterior polygon rings are CCW and holes are CW.
        /// Returns false for degenerate inputs with fewer than three distinct coordinates.
        /// </summary>
        public bool IsCCW(List<EdgeReference> refs) {
            var coords = new List<Coordinate>();
            foreach (var r in refs) {
                var ec = r.OrientedGeometry.Coordinates;
                if (coords.Count == 0) coords.AddRange(ec); else coords.AddRange(ec.Skip(1));
            }
            if (coords.Count < 3) return false;
            if (!coords[0].Equals2D(coords[^1]))
                coords.Add(new Coordinate(coords[0].X, coords[0].Y));
            return NetTopologySuite.Algorithm.Orientation.IsCCW(coords.ToArray());
        }

        // -------------------------------------------------------------------
        // Classification
        // -------------------------------------------------------------------

        /// <summary>
        /// Classifies a <see cref="MergedEdge"/> against each of its contributing source geometries,
        /// indicating whether the edge lies on an exterior ring, an interior ring (hole), or is
        /// part of a plain linestring source. If contributing sources disagree — for example one
        /// polygon treats the edge as its exterior ring while another treats it as an interior ring
        /// — all classifications are overridden with <see cref="EdgeRingKind.Mixed"/>.
        /// </summary>
        public List<MergedEdgeClassification> ClassifyMergedEdge(MergedEdge edge) {
            var result = new List<MergedEdgeClassification>();
            foreach (var sourceId in edge.SourceGeometryIds) {
                var src = _sources[sourceId];
                if (src.Kind == GeometryKind.LineString) {
                    result.Add(new MergedEdgeClassification { Kind = EdgeRingKind.LineString, Source = src });
                    continue;
                }
                var rk = ClassifyAgainstPolygon(edge.Geometry, (Polygon)src.Geometry);
                result.Add(new MergedEdgeClassification { Kind = rk.Kind, RingIndex = rk.RingIndex, Source = src });
            }
            if (result.Select(r => r.Kind).Distinct().Count() > 1)
                result = result.Select(r => new MergedEdgeClassification {
                    Kind = EdgeRingKind.Mixed,
                    RingIndex = r.RingIndex,
                    Source = r.Source
                }).ToList();
            return result;
        }

        private (EdgeRingKind Kind, int? RingIndex) ClassifyAgainstPolygon(LineString geom, Polygon poly) {
            var probe = MidPoint(geom);
            if (poly.ExteriorRing.Distance(probe) <= _snapTolerance) return (EdgeRingKind.ExteriorRing, null);
            for (int i = 0; i < poly.NumInteriorRings; i++)
                if (poly.GetInteriorRingN(i).Distance(probe) <= _snapTolerance)
                    return (EdgeRingKind.InteriorRing, i);
            return FallbackClassify(geom, poly);
        }

        private (EdgeRingKind Kind, int? RingIndex) FallbackClassify(LineString geom, Polygon poly) {
            int extV = 0; var intV = new int[poly.NumInteriorRings];
            int n = Math.Min(geom.NumPoints, 5);
            for (int i = 0; i <= n; i++) {
                int idx = (int)((double)i / n * (geom.NumPoints - 1));
                var pt = geom.Factory.CreatePoint(geom.GetCoordinateN(idx));
                if (poly.ExteriorRing.Distance(pt) <= _snapTolerance) { extV++; continue; }
                for (int j = 0; j < poly.NumInteriorRings; j++)
                    if (poly.GetInteriorRingN(j).Distance(pt) <= _snapTolerance) intV[j]++;
            }
            if (extV > 0) return (EdgeRingKind.ExteriorRing, null);
            for (int j = 0; j < intV.Length; j++) if (intV[j] > 0) return (EdgeRingKind.InteriorRing, j);
            return (EdgeRingKind.ExteriorRing, null);
        }

        private static Point MidPoint(LineString line) {
            var cs = line.Coordinates; int mid = cs.Length / 2;
            if (cs.Length % 2 == 0)
                return line.Factory.CreatePoint(
                    new Coordinate((cs[mid - 1].X + cs[mid].X) / 2, (cs[mid - 1].Y + cs[mid].Y) / 2));
            return line.Factory.CreatePoint(cs[mid]);
        }
    }
}