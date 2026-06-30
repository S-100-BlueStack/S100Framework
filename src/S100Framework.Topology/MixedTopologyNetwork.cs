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
    /// </summary>
    public class EdgeReference
    {
        /// <summary>The canonical merged edge shared across all sources that contain it.</summary>
        public MergedEdge Edge { get; init; }
        /// <summary>True if this source traverses the edge in its canonical direction.</summary>
        public bool Forward { get; init; }
        /// <summary>The edge geometry oriented for this source's traversal direction.</summary>
        public LineString OrientedGeometry => Forward
            ? Edge.Geometry
            : (LineString)Edge.Geometry.Reverse();
    }

    /// <summary>
    /// A full topological description of a single source geometry within the network.
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

    /// <summary>Topological role of a <see cref="MergedEdge"/> relative to a specific source polygon.</summary>
    public enum EdgeRingKind { ExteriorRing, InteriorRing, LineString, Mixed }

    /// <summary>Classifies a <see cref="MergedEdge"/> relative to one of its contributing source geometries.</summary>
    public class MergedEdgeClassification
    {
        public EdgeRingKind Kind { get; init; }
        public int? RingIndex { get; init; }
        public NetworkGeometry Source { get; init; }
    }

    /// <summary>
    /// An integer grid key derived from a snapped coordinate, used for O(1) node lookup and
    /// deterministic union-find tie-breaking in the canonical coordinate map.
    /// </summary>
    public readonly struct CoordinateKey : IEquatable<CoordinateKey>, IComparable<CoordinateKey>
    {
        private readonly long _x, _y;

        public CoordinateKey(Coordinate snappedCoord, double tolerance) {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(snappedCoord.X * inv);
            _y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y) { _x = x; _y = y; }

        public CoordinateKey Offset(int dx, int dy) => new(_x + dx, _y + dy);
        public bool Equals(CoordinateKey o) => _x == o._x && _y == o._y;
        public override bool Equals(object? obj) => obj is CoordinateKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(_x, _y);
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

        public IReadOnlyList<NetworkEdge> Edges => _edges;
        public IReadOnlyCollection<NetworkNode> Nodes => _nodes.Values;
        public IEnumerable<int> Sources => _sources.Select(s => s.Id);

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

        public int AddPolygon(Polygon polygon) => Register(polygon, GeometryKind.Polygon);
        public int AddLineString(LineString lineString) => Register(lineString, GeometryKind.LineString);
        public void AddPolygons(IEnumerable<Polygon> polygons) { foreach (var p in polygons) AddPolygon(p); }
        public void AddLineStrings(IEnumerable<LineString> lines) { foreach (var l in lines) AddLineString(l); }

        private int Register(Geometry geom, GeometryKind kind) {
            int id = _sources.Count;
            _sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------

        public void Build() {
            var rawSegments = ExtractRawSegments();

            var canonical = BuildCanonicalCoordinateMap(rawSegments);
            _canonicalMap = canonical;

            var cleaned = new List<RawSegment>(rawSegments.Count);
            foreach (var seg in rawSegments) {
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
                    segId = ExtractFromRing(src.Geometry.Coordinates, src.Id, result, segId, src.IsRing);
                }
            }
            return result;
        }

        private int EstimateSegmentCount() => _sources.Sum(s => s.Geometry.NumPoints);

        private int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId, bool isRing) {

            if (isRing && coords.Length > 1 &&
                SnapToGrid(coords[0]).Equals2D(SnapToGrid(coords[^1])))
                coords = coords[..^1];

            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1])) snapped.Add(sc);
            }

            if (isRing && snapped.Count > 1 &&
                !snapped[0].Equals2D(snapped[^1]) &&
                snapped[0].Distance(snapped[^1]) <= _dedupeRadius)
                snapped[^1] = snapped[0];

            if (isRing && snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(snapped[0]);

            for (int i = 0; i < snapped.Count - 1; i++) {
                var p0 = snapped[i]; var p1 = snapped[i + 1];
                if (p0.Equals2D(p1)) continue;
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

            var segSourceCount = new Dictionary<(CoordinateKey, CoordinateKey), int>();
            foreach (var seg in rawSegments) {
                var k0 = new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance);
                var k1 = new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance);
                var segKey = k0.CompareTo(k1) < 0 ? (k0, k1) : (k1, k0);
                segSourceCount.TryGetValue(segKey, out int cnt);
                segSourceCount[segKey] = cnt + 1;
            }

            double sharedRadius = _dedupeRadius * 1.04;

            foreach (var seg in rawSegments) {
                double d = seg.P0.Distance(seg.P1);
                if (d > sharedRadius) continue;
                var k0 = new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance);
                var k1 = new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance);
                var segKey = k0.CompareTo(k1) < 0 ? (k0, k1) : (k1, k0);
                if (!segSourceCount.TryGetValue(segKey, out int cnt)) continue;
                bool shouldUnion = cnt > 1 ? d <= sharedRadius : d <= _dedupeRadius;
                if (shouldUnion) Union(k0, k1);
            }

            var result = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var key in cellValue.Keys) result[key] = cellValue[Find(key)];
            return result;
        }

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

        private bool IsInteriorPoint(Coordinate pt, Coordinate segStart, Coordinate segEnd) {
            double dx = segEnd.X - segStart.X;
            double dy = segEnd.Y - segStart.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < _snapTolerance * _snapTolerance) return false;
            double t = ((pt.X - segStart.X) * dx + (pt.Y - segStart.Y) * dy) / lenSq;
            double len = Math.Sqrt(lenSq);
            if (t * len <= _snapTolerance || (1.0 - t) * len <= _snapTolerance) return false;
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

        public IReadOnlyList<NetworkEdge> GetEdgesFor(int sourceId)
            => _edgesBySource.TryGetValue(sourceId, out var list) ? list : Array.Empty<NetworkEdge>();

        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => GetEdgesFor(sourceIdA).Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        public IEnumerable<NetworkEdge> GetAllSharedEdges() => _edges.Where(e => e.IsShared);

        public IEnumerable<NetworkGeometry> GetSourceGeometriesForEdge(NetworkEdge edge)
            => edge.SourceGeometryIds.Select(id => _sources[id]);

        public NetworkNode? GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            return _nodes.TryGetValue(key, out var node) ? node : null;
        }

        public IEnumerable<NetworkEdge> QueryEdges(Envelope envelope)
            => _edgeIndex.Query(envelope).Cast<NetworkEdge>();

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

            var coord = seed.EndNode;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adj.TryGetValue(key, out var nbrs)) break;
                var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (cands.Count != 1) break;
                var next = cands[0];

                visited.Add(next.Id);
                forward.Add(next);

                bool closedAtSeedStart =
                    next.StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                    next.EndNode.Equals2D(seed.StartNode, _snapTolerance);
                bool allVisited = visited.Count == edgeSet.Count;

                if (closedAtSeedStart || allVisited) break;

                coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
            }

            bool ringClosed =
                visited.Count == edgeSet.Count ||
                (forward.Count > 0 && (
                    forward[^1].StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                    forward[^1].EndNode.Equals2D(seed.StartNode, _snapTolerance)));

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

        public (List<MergedEdge> AllEdges, Dictionary<int, List<MergedEdge>> SourceEdges)
            BuildEdgeIndex() {

            var canonicalByKey = new Dictionary<string, MergedEdge>();
            var allMergedEdges = new List<MergedEdge>();
            var edgeIdToLargest = new Dictionary<(string srcSig, int producingSrcId, int edgeId), MergedEdge>();

            foreach (var src in _sources) {
                foreach (var merged in GetMergedEdgeChainFor(src.Id)) {
                    var key = string.Join(",", merged.ConstituentEdges.Select(e => e.Id).OrderBy(id => id));
                    var srcSig = string.Join(",", merged.SourceGeometryIds.OrderBy(id => id));

                    if (canonicalByKey.ContainsKey(key)) continue;

                    bool isSubset = false;
                    foreach (var e in merged.ConstituentEdges) {
                        if (edgeIdToLargest.TryGetValue((srcSig, src.Id, e.Id), out var larger) &&
                            larger.ConstituentEdges.Count > merged.ConstituentEdges.Count) {
                            var largerIds = larger.ConstituentEdges.Select(x => x.Id).ToHashSet();
                            if (merged.ConstituentEdges.All(x => largerIds.Contains(x.Id))) {
                                isSubset = true;
                                break;
                            }
                        }
                    }
                    if (isSubset) continue;

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
                    if (candidatesToAbsorb.Count > 0 && coveredBySmaller.SetEquals(thisIds))
                        toRemove.UnionWith(candidatesToAbsorb.Keys);

                    foreach (var rk in toRemove) {
                        if (canonicalByKey.TryGetValue(rk, out var removed)) {
                            canonicalByKey.Remove(rk);
                            allMergedEdges.Remove(removed);
                        }
                    }

                    canonicalByKey[key] = merged;
                    allMergedEdges.Add(merged);
                    foreach (var e in merged.ConstituentEdges)
                        edgeIdToLargest[(srcSig, src.Id, e.Id)] = merged;
                }
            }

            var edgeToCanonical = new Dictionary<int, MergedEdge>();
            foreach (var canonical in canonicalByKey.Values)
                foreach (var e in canonical.ConstituentEdges)
                    edgeToCanonical[e.Id] = canonical;

            foreach (var netEdge in _edges) {
                if (edgeToCanonical.ContainsKey(netEdge.Id)) continue;
                var fallback = new MergedEdge {
                    Geometry = _factory.CreateLineString(new[] { netEdge.StartNode, netEdge.EndNode }),
                    SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                    ConstituentEdges = new List<NetworkEdge> { netEdge },
                };
                edgeToCanonical[netEdge.Id] = fallback;
                allMergedEdges.Add(fallback);
            }

            var sourceEdges = new Dictionary<int, List<MergedEdge>>();

            foreach (var src in _sources) {
                var chain = GetFullEdgeChainFor(src.Id);
                if (chain.Count == 0) { sourceEdges[src.Id] = new List<MergedEdge>(); continue; }

                var localEdgeToCanonical = new Dictionary<int, MergedEdge>();
                foreach (var merged in GetMergedEdgeChainFor(src.Id))
                    foreach (var e in merged.ConstituentEdges)
                        if (!localEdgeToCanonical.ContainsKey(e.Id))
                            localEdgeToCanonical[e.Id] = merged;

                if (src.IsRing && chain.Count > 1) {
                    Coordinate? rotationNode = null;
                    var mergedChain = GetMergedEdgeChainFor(src.Id).ToList();
                    if (mergedChain.Count > 1) {
                        var firstEdge = mergedChain[0].ConstituentEdges.Last();
                        rotationNode = firstEdge.EndNode.Equals2D(mergedChain[1].ConstituentEdges.First().StartNode)
                            ? firstEdge.EndNode : firstEdge.StartNode;
                    }
                    chain = RotateToNode(chain, rotationNode);
                }

                var result = new List<MergedEdge>();
                MergedEdge? last = null;

                foreach (var netEdge in chain) {
                    if (!localEdgeToCanonical.TryGetValue(netEdge.Id, out var canonical) &&
                        !edgeToCanonical.TryGetValue(netEdge.Id, out canonical)) {
                        canonical = new MergedEdge {
                            Geometry = _factory.CreateLineString(new[] { netEdge.StartNode, netEdge.EndNode }),
                            SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                            ConstituentEdges = new List<NetworkEdge> { netEdge },
                        };
                        edgeToCanonical[netEdge.Id] = canonical;
                        allMergedEdges.Add(canonical);
                    }
                    else if (!canonical.ConstituentEdges.Any(e => e.Id == netEdge.Id)) {
                        canonical = new MergedEdge {
                            Geometry = _factory.CreateLineString(new[] { netEdge.StartNode, netEdge.EndNode }),
                            SourceGeometryIds = new HashSet<int>(netEdge.SourceGeometryIds),
                            ConstituentEdges = new List<NetworkEdge> { netEdge },
                        };
                        allMergedEdges.Add(canonical);
                    }
                    if (ReferenceEquals(canonical, last)) continue;
                    result.Add(canonical);
                    last = canonical;
                }

                if (src.IsRing && result.Count > 1 &&
                    ReferenceEquals(result[0], result[^1]))
                    result.RemoveAt(result.Count - 1);

                sourceEdges[src.Id] = result;
            }

            return (allMergedEdges, sourceEdges);
        }

        // -------------------------------------------------------------------
        // ResolveLineString
        // -------------------------------------------------------------------

        public List<MergedEdge> ResolveLineString(LineString lineString, List<MergedEdge> allEdges) {
            if (_nodeEdgeLookup == null)
                _nodeEdgeLookup = BuildNodeEdgeLookup(allEdges);

            var raw = lineString.Coordinates;
            var snapped = new List<Coordinate>(raw.Length);
            foreach (var c in raw) {
                var key = new CoordinateKey(SnapToGrid(c), _snapTolerance);
                if (_nodes.TryGetValue(key, out var node))
                    snapped.Add(node.Coordinate);
                else
                    snapped.Add(SnapToGrid(c));
            }

            var pts = new List<Coordinate> { snapped[0] };
            for (int i = 1; i < snapped.Count; i++)
                if (!snapped[i].Equals2D(pts[^1]))
                    pts.Add(snapped[i]);

            var result = new List<MergedEdge>();
            int start = 0;
            while (start < pts.Count - 1) {
                MergedEdge? matched = null;
                int matchedEnd = -1;
                for (int end = pts.Count - 1; end > start; end--) {
                    var key = MakeNodePairKey(pts[start], pts[end]);
                    if (_nodeEdgeLookup.TryGetValue(key, out var edge)) {
                        matched = edge; matchedEnd = end; break;
                    }
                }
                if (matched != null) {
                    result.Add(matched); start = matchedEnd;
                }
                else {
                    result.Add(new MergedEdge {
                        Geometry = _factory.CreateLineString(new[] { pts[start], pts[start + 1] }),
                        SourceGeometryIds = new HashSet<int>(),
                        ConstituentEdges = new List<NetworkEdge>(),
                    });
                    start++;
                }
            }
            return result;
        }

        private Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>? _nodeEdgeLookup;

        private Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>
            BuildNodeEdgeLookup(List<MergedEdge> allEdges) {
            var lookup = new Dictionary<(CoordinateKey, CoordinateKey), MergedEdge>();
            foreach (var edge in allEdges) {
                var coords = edge.Geometry.Coordinates;
                var k0 = new CoordinateKey(coords[0], _snapTolerance);
                var k1 = new CoordinateKey(coords[^1], _snapTolerance);
                lookup.TryAdd((k0, k1), edge);
                lookup.TryAdd((k1, k0), edge);
                foreach (var ne in edge.ConstituentEdges) {
                    var nk0 = new CoordinateKey(ne.StartNode, _snapTolerance);
                    var nk1 = new CoordinateKey(ne.EndNode, _snapTolerance);
                    lookup.TryAdd((nk0, nk1), edge);
                    lookup.TryAdd((nk1, nk0), edge);
                }
            }
            return lookup;
        }

        private (CoordinateKey, CoordinateKey) MakeNodePairKey(Coordinate a, Coordinate b) =>
            (new CoordinateKey(a, _snapTolerance), new CoordinateKey(b, _snapTolerance));

        private List<NetworkEdge> RotateToNode(List<NetworkEdge> chain, Coordinate? targetNode) {
            if (targetNode == null) return chain;
            for (int i = 1; i < chain.Count; i++) {
                if (chain[i].StartNode.Equals2D(targetNode) || chain[i].EndNode.Equals2D(targetNode)) {
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

        public LineString ReconstructGeometry(List<EdgeReference> refs, int sourceId) {
            if (refs.Count == 0) return null!;
            var coords = new List<Coordinate>();
            foreach (var r in refs) {
                var ec = r.OrientedGeometry.Coordinates;
                if (coords.Count == 0) coords.AddRange(ec.Select(Canonicalize));
                else coords.AddRange(ec.Skip(1).Select(Canonicalize));
            }
            var source = _sources[sourceId];
            if (source.IsRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
            return _factory.CreateLineString(coords.ToArray());
        }

        // -------------------------------------------------------------------
        // Orientation
        // -------------------------------------------------------------------

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
        // Robust assembly — Hierholzer's Eulerian path/circuit algorithm
        // -------------------------------------------------------------------

        /// <summary>
        /// Assembles a <see cref="LinearRing"/> from an unordered list of <see cref="MergedEdge"/>
        /// objects using Hierholzer's Eulerian-circuit algorithm. Correctly handles all topologies
        /// including cases where greedy traversal would get stuck (e.g. a "figure-8" junction or
        /// a node shared by edges traversed in opposite directions).
        /// </summary>
        public LinearRing AssembleLinearRing(List<MergedEdge> edges) {
            var (coords, _) = AssembleOrderedEdges(edges, isRing: true);
            if (coords.Length == 0) return _factory.CreateLinearRing(Array.Empty<Coordinate>());
            return _factory.CreateLinearRing(coords);
        }

        /// <summary>
        /// Assembles a <see cref="LineString"/> from an unordered list of <see cref="MergedEdge"/>
        /// objects using Hierholzer's Eulerian-path algorithm. Starts from a degree-1 endpoint when
        /// one exists (open path), otherwise from any node (closed path).
        /// </summary>
        public LineString AssembleLineString(List<MergedEdge> edges) {
            var (coords, _) = AssembleOrderedEdges(edges, isRing: false);
            if (coords.Length == 0) return _factory.CreateLineString(Array.Empty<Coordinate>());
            return _factory.CreateLineString(coords);
        }

        /// <summary>
        /// Returns the same ordered, correctly-oriented edge list that
        /// <see cref="AssembleLineString"/> uses internally to build its geometry — as a list of
        /// <see cref="EdgeReference"/>, each carrying the <see cref="MergedEdge"/> plus a
        /// <see cref="EdgeReference.Forward"/> flag indicating whether it is traversed in its
        /// canonical direction or reversed. Concatenating
        /// <see cref="EdgeReference.OrientedGeometry"/> for each entry in order (skipping the
        /// duplicate junction coordinate between consecutive entries) reproduces exactly the
        /// coordinates returned by <see cref="AssembleLineString"/>.
        /// </summary>
        public List<EdgeReference> AssembleEdgeOrderForLineString(List<MergedEdge> edges) {
            var (_, refs) = AssembleOrderedEdges(edges, isRing: false);
            return refs;
        }

        /// <summary>
        /// Returns the same ordered, correctly-oriented edge list that
        /// <see cref="AssembleLinearRing"/> uses internally to build its geometry. See
        /// <see cref="AssembleEdgeOrderForLineString"/> for details on the returned references.
        /// </summary>
        public List<EdgeReference> AssembleEdgeOrderForLinearRing(List<MergedEdge> edges) {
            var (_, refs) = AssembleOrderedEdges(edges, isRing: true);
            return refs;
        }

        /// <summary>
        /// Core Hierholzer implementation shared by <see cref="AssembleLinearRing"/>,
        /// <see cref="AssembleLineString"/>, and their edge-order counterparts. Builds a
        /// half-edge adjacency map keyed by snapped <see cref="CoordinateKey"/>, selects the
        /// best start node, then performs the stack-based Hierholzer walk that backtracks
        /// whenever the current node has no more unused edges, guaranteeing all edges are
        /// visited regardless of local topology. Returns both the concatenated coordinate
        /// array and the ordered/oriented <see cref="EdgeReference"/> list in one pass, so the
        /// two outputs can never disagree with each other.
        /// </summary>
        private (Coordinate[] Coords, List<EdgeReference> Refs) AssembleOrderedEdges(
            List<MergedEdge> edges, bool isRing) {

            if (edges.Count == 0) return (Array.Empty<Coordinate>(), new List<EdgeReference>());

            double tol = _snapTolerance;
            // Use Canonicalize (not just raw snapping) so that endpoints which have
            // drifted slightly across many StitchChain concatenations still resolve
            // to the SAME node. Without this, two genuinely-identical network nodes
            // can end up with different CoordinateKeys after repeated coordinate
            // copying through canonical-edge construction, splitting the adjacency
            // graph into disconnected pieces and causing the Hierholzer walk to
            // jump to an unrelated component when it runs out of edges at a
            // falsely-isolated node.
            CoordinateKey Snap(Coordinate c) => new CoordinateKey(Canonicalize(c), tol);

            // Build a linked-list adjacency map of directed half-edges.
            // Each undirected edge contributes two half-edges (forward and backward).
            var adj = new Dictionary<CoordinateKey,
                          LinkedList<(MergedEdge edge, bool forward)>>();

            void AddHalf(CoordinateKey k, MergedEdge e, bool fwd) {
                if (!adj.TryGetValue(k, out var lst))
                    adj[k] = lst = new LinkedList<(MergedEdge, bool)>();
                lst.AddLast((e, fwd));
            }

            foreach (var e in edges) {
                var cs = e.Geometry.Coordinates;
                var kS = Snap(cs[0]);
                var kE = Snap(cs[^1]);
                if (kS.Equals(kE)) {        // self-loop
                    AddHalf(kS, e, true);
                }
                else {
                    AddHalf(kS, e, true);   // forward  traversal starting from kS
                    AddHalf(kE, e, false);  // backward traversal starting from kE
                }
            }

            // For an open linestring prefer a degree-1 node (odd half-edge count) as start.
            // For a ring any node works; pick the first one found.
            CoordinateKey startKey = adj.Keys.First();
            if (!isRing) {
                foreach (var kv in adj) {
                    if (kv.Value.Count % 2 == 1) { startKey = kv.Key; break; }
                }
            }

            // Hierholzer's algorithm:
            //   Push (startNode, null) onto the stack.
            //   While the stack is non-empty:
            //     If the top node has an unused outgoing half-edge, consume it and push the next node.
            //     Otherwise pop and prepend the incoming step to the result path.
            // Each "step" carries both the oriented coordinate array AND the (edge, forward)
            // pair that produced it, so the coordinate path and the edge-reference list are
            // built from exactly the same walk and can never diverge from each other.
            var used = new HashSet<MergedEdge>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<(CoordinateKey node, (Coordinate[] cs, MergedEdge edge, bool fwd)? step)>();
            var path = new LinkedList<(Coordinate[] cs, MergedEdge edge, bool fwd)>();

            stack.Push((startKey, null));

            while (stack.Count > 0) {
                var (cur, _) = stack.Peek();

                if (adj.TryGetValue(cur, out var halfEdges)) {
                    LinkedListNode<(MergedEdge edge, bool forward)>? next = null;
                    for (var n = halfEdges.First; n != null; n = n.Next) {
                        if (!used.Contains(n.Value.edge)) { next = n; break; }
                    }

                    if (next != null) {
                        var (edge, fwd) = next.Value;
                        used.Add(edge);

                        var cs = edge.Geometry.Coordinates;
                        if (!fwd) cs = cs.Reverse().ToArray();

                        var kE = fwd ? Snap(cs[^1]) : Snap(cs[0]);
                        stack.Push((kE, (cs, edge, fwd)));
                        continue;
                    }
                }

                // No unused edges from cur — pop and record the incoming step
                var (_, inStep) = stack.Pop();
                if (inStep != null) path.AddFirst(inStep.Value);
            }

            // If any edges were never visited, the graph has more than one connected
            // component under the current node-key equivalence. This happens when an
            // edge's endpoint, even after Canonicalize, does not match any other
            // edge's endpoint at that physical location — almost always because the
            // two MergedEdges were built from canonicals whose StitchChain geometry
            // was independently snapped/rounded and drifted apart by a tiny amount
            // beyond _snapTolerance. Rather than silently producing a corrupted single
            // path (which is what caused edges to appear stitched together when they
            // are not topologically adjacent), append any leftover components as
            // SEPARATE trailing segments in their original list order, traversed
            // forward. This keeps the output deterministic and makes any remaining
            // disconnection visible (the seam will show as a coordinate jump) instead
            // of hiding it inside a single LineString that silently cuts across the ring.
            if (used.Count < edges.Count) {
                foreach (var e in edges) {
                    if (used.Contains(e)) continue;
                    used.Add(e);
                    path.AddLast((e.Geometry.Coordinates, e, true));
                }
            }

            if (path.Count == 0) return (Array.Empty<Coordinate>(), new List<EdgeReference>());

            // Concatenate segments, dropping duplicate junction coordinates, and build the
            // parallel EdgeReference list from the exact same walk.
            var result = new List<Coordinate>();
            var refs = new List<EdgeReference>(path.Count);
            foreach (var (cs, edge, fwd) in path) {
                int skip = result.Count > 0 && result[^1].Equals2D(cs[0]) ? 1 : 0;
                result.AddRange(cs.Skip(skip));
                refs.Add(new EdgeReference { Edge = edge, Forward = fwd });
            }

            // Force-close for rings
            if (isRing && result.Count > 1 && !result[0].Equals2D(result[^1]))
                result.Add(new Coordinate(result[0].X, result[0].Y));

            return (result.ToArray(), refs);
        }

        // -------------------------------------------------------------------
        // Classification
        // -------------------------------------------------------------------

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