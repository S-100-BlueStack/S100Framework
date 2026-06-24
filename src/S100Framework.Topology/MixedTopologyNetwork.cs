using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace S100Framework.Topology.Internal
{
    public enum GeometryKind { Polygon, LineString }

    public class NetworkGeometry
    {
        public int Id { get; init; }
        public Geometry Geometry { get; init; }
        public GeometryKind Kind { get; init; }
    }

    public class NetworkNode
    {
        public Coordinate Coordinate { get; init; }
        public List<NetworkEdge> Edges { get; } = new();
        public int Degree => Edges.Count;
    }

    /// <summary>
    /// An undirected edge. StartNode/EndNode are in canonical (lexicographic)
    /// order — they do NOT encode any source's traversal direction.
    /// </summary>
    public class NetworkEdge
    {
        public int Id { get; init; }
        public LineString Geometry { get; init; }
        public Coordinate StartNode { get; init; }
        public Coordinate EndNode { get; init; }
        public HashSet<int> SourceGeometryIds { get; } = new();
        public bool IsShared => SourceGeometryIds.Count > 1;
    }

    /// <summary>
    /// One or more consecutive NetworkEdges with the same SourceGeometryIds,
    /// stitched into a single LineString via connectivity (no source direction).
    /// </summary>
    public class MergedEdge
    {
        public LineString Geometry { get; init; }
        public HashSet<int> SourceGeometryIds { get; init; }
        public List<NetworkEdge> ConstituentEdges { get; init; }
        public bool IsShared => SourceGeometryIds.Count > 1;
        public double Length => Geometry.Length;
    }

    /// <summary>
    /// A directed reference to a canonical MergedEdge from a specific source.
    /// Forward=true  → use MergedEdge.Geometry as-is.
    /// Forward=false → use MergedEdge.Geometry reversed.
    /// </summary>
    public class EdgeReference
    {
        public MergedEdge Edge { get; init; }
        public bool Forward { get; init; }
        public LineString OrientedGeometry => Forward
            ? Edge.Geometry
            : (LineString)Edge.Geometry.Reverse();
    }

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

    public enum EdgeRingKind { ExteriorRing, InteriorRing, LineString, Mixed }

    public class MergedEdgeClassification
    {
        public EdgeRingKind Kind { get; init; }
        public int? RingIndex { get; init; }
        public NetworkGeometry Source { get; init; }
    }

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
                var p0 = canonical[new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance)];
                var p1 = canonical[new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance)];
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
                        if (canonical.TryGetValue(new CoordinateKey(pt, _snapTolerance), out var cp)) pt = cp;
                        pt = SnapToEndpoint(pt, seg, other);
                        splitPoints[seg.SegId].Add(new SplitPoint(pt, seg.P0));
                        splitPoints[other.SegId].Add(new SplitPoint(pt, other.P0));
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
                        src.Geometry is LinearRing);
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

            foreach (var seg in rawSegments)
                if (seg.P0.Distance(seg.P1) <= _dedupeRadius) {
                    Union(new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance),
                          new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance));
                }

            var result = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var key in cellValue.Keys) result[key] = cellValue[Find(key)];
            return result;
        }

        private Coordinate SnapToEndpoint(Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 })
                if (pt.Distance(ep) <= _snapTolerance) return ep;
            return pt;
        }

        // -------------------------------------------------------------------
        // Graph insertion — no SourceOrientation
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

        public IEnumerable<NetworkEdge> GetSharedEdges(int a, int b)
            => GetEdgesFor(a).Where(e => e.SourceGeometryIds.Contains(b));

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

            // Seed: first edge found by scanning source coordinate pairs
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

            // Forward from seed.EndNode
            var coord = seed.EndNode;
            while (true) {
                var key = new CoordinateKey(coord, _snapTolerance);
                if (!adj.TryGetValue(key, out var nbrs)) break;
                var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (cands.Count != 1) break;
                var next = cands[0];
                bool closed = next.StartNode.Equals2D(seed.StartNode, _snapTolerance)
                           || next.EndNode.Equals2D(seed.StartNode, _snapTolerance);
                visited.Add(next.Id);
                forward.Add(next);
                if (closed) break;
                coord = next.StartNode.Equals2D(coord, _snapTolerance) ? next.EndNode : next.StartNode;
            }

            // Backward from seed.StartNode (open linestrings only)
            bool ringClosed = forward.Count > 0 && (
                forward[^1].StartNode.Equals2D(seed.StartNode, _snapTolerance) ||
                forward[^1].EndNode.Equals2D(seed.StartNode, _snapTolerance));

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
        // Stitching — connectivity only, no source direction
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

        public (List<MergedEdge> AllEdges, Dictionary<int, List<EdgeReference>> SourceRefs)
            BuildEdgeIndex() {

            // Pass 1: collect canonical MergedEdge per unique constituent-edge-id set.
            // First source to produce a group wins as canonical; others reuse it.
            var canonicalByKey = new Dictionary<string, MergedEdge>();
            var edgeToCanonical = new Dictionary<int, MergedEdge>();
            var allMergedEdges = new List<MergedEdge>();

            foreach (var src in _sources) {
                foreach (var merged in GetMergedEdgeChainFor(src.Id)) {
                    var key = string.Join(",",
                        merged.ConstituentEdges.Select(e => e.Id).OrderBy(id => id));

                    if (!canonicalByKey.TryGetValue(key, out var canonical)) {
                        canonical = merged;
                        canonicalByKey[key] = canonical;
                        allMergedEdges.Add(canonical);
                    }
                    foreach (var e in merged.ConstituentEdges)
                        edgeToCanonical.TryAdd(e.Id, canonical);
                }
            }

            // Pass 2: per-source EdgeReference lists.
            var sourceRefs = new Dictionary<int, List<EdgeReference>>();

            foreach (var src in _sources) {
                var chain = GetFullEdgeChainFor(src.Id);
                if (chain.Count == 0) { sourceRefs[src.Id] = new List<EdgeReference>(); continue; }

                // Rotate ring chains to a group boundary to avoid split groups
                var source = _sources[src.Id];
                bool isRing = source.Kind == GeometryKind.Polygon || source.Geometry is LinearRing;
                if (isRing && chain.Count > 1)
                    chain = RotateToGroupBoundary(chain, edgeToCanonical);

                var refs = new List<EdgeReference>();
                MergedEdge? lastCanonical = null;

                for (int ci = 0; ci < chain.Count; ci++) {
                    var netEdge = chain[ci];
                    if (!edgeToCanonical.TryGetValue(netEdge.Id, out var canonical)) continue;
                    if (ReferenceEquals(canonical, lastCanonical)) continue;

                    // Determine forward/backward by comparing the source's
                    // entry node for this group vs the canonical first coord.
                    // Entry node = node shared with the previous chain edge,
                    // or (for the first edge) the end NOT connected to next edge.
                    Coordinate srcEntry;
                    if (ci == 0) {
                        // Check which end of netEdge connects to chain[1]
                        if (chain.Count > 1) {
                            var next = chain[1];
                            srcEntry = netEdge.EndNode.Equals2D(next.StartNode, _snapTolerance) ||
                                       netEdge.EndNode.Equals2D(next.EndNode, _snapTolerance)
                                ? netEdge.StartNode   // entry is StartNode (exit is EndNode)
                                : netEdge.EndNode;
                        }
                        else {
                            srcEntry = netEdge.StartNode;
                        }
                    }
                    else {
                        var prev = chain[ci - 1];
                        srcEntry = prev.StartNode.Equals2D(netEdge.StartNode, _snapTolerance) ||
                                   prev.EndNode.Equals2D(netEdge.StartNode, _snapTolerance)
                            ? netEdge.StartNode
                            : netEdge.EndNode;
                    }

                    var canonFirst = canonical.Geometry.Coordinates[0];
                    bool forward = srcEntry.Equals2D(canonFirst, _snapTolerance);

                    refs.Add(new EdgeReference { Edge = canonical, Forward = forward });
                    lastCanonical = canonical;
                }

                sourceRefs[src.Id] = refs;
            }

            return (allMergedEdges, sourceRefs);
        }

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

        public LineString ReconstructGeometry(List<EdgeReference> refs, int sourceId) {
            if (refs.Count == 0) return null!;
            var coords = new List<Coordinate>();
            foreach (var r in refs) {
                var ec = r.OrientedGeometry.Coordinates;
                if (coords.Count == 0) coords.AddRange(ec.Select(Canonicalize));
                else coords.AddRange(ec.Skip(1).Select(Canonicalize));
            }
            var source = _sources[sourceId];
            bool isRing = source.Kind == GeometryKind.Polygon || source.Geometry is LinearRing;
            if (isRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
            return _factory.CreateLineString(coords.ToArray());
        }

        // -------------------------------------------------------------------
        // Orientation helper
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