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
        public CoordinateKey(Coordinate snappedCoord, double tolerance)
        {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(snappedCoord.X * inv);
            _y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y)
        {
            _x = x;
            _y = y;
        }

        public CoordinateKey Offset(int dx, int dy) => new CoordinateKey(_x + dx, _y + dy);

        public bool Equals(CoordinateKey other) => _x == other._x && _y == other._y;
        public override bool Equals(object obj) => obj is CoordinateKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(_x, _y);

        public int CompareTo(CoordinateKey other)
        {
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

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId, bool reversed)
        {
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

        public SplitPoint(Coordinate coord, Coordinate segStart)
        {
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

        private readonly List<NetworkGeometry> _sources = new();
        private readonly List<NetworkEdge> _edges = new();
        private readonly Dictionary<CoordinateKey, NetworkNode> _nodes = new();
        private readonly Dictionary<int, List<NetworkEdge>> _edgesBySource = new();
        private STRtree<NetworkEdge> _edgeIndex;

        public IReadOnlyList<NetworkEdge> Edges => _edges;
        public IReadOnlyCollection<NetworkNode> Nodes => _nodes.Values;

        public IEnumerable<int> Sources => this._sources.Select(e => e.Id);

        public MixedTopologyNetwork(GeometryFactory factory = null, double snapTolerance = 0.000001)
        {
            _factory = factory ?? NtsGeometryServices.Instance.CreateGeometryFactory();
            _snapTolerance = snapTolerance;
        }

        // -------------------------------------------------------------------
        // Registration
        // -------------------------------------------------------------------

        public int AddPolygon(Polygon polygon) => Register(polygon, GeometryKind.Polygon);

        public int AddLineString(LineString lineString) => Register(lineString, GeometryKind.LineString);

        public void AddPolygons(IEnumerable<Polygon> polygons)
        {
            foreach (var p in polygons) AddPolygon(p);
        }

        public void AddLineStrings(IEnumerable<LineString> lines)
        {
            foreach (var l in lines) AddLineString(l);
        }

        private int Register(Geometry geom, GeometryKind kind)
        {
            int id = _sources.Count;
            _sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------

        public void Build()
        {
            // Step 1: Extract raw segments per source (bare coordinates)
            var rawSegments = ExtractRawSegments();

            // Step 2: Canonicalize coordinates (union-find over adjacent grid cells)
            //         and remap every segment's endpoints to canonical coordinates.
            //         Drop any segment that becomes degenerate (zero-length)
            //         AFTER canonicalization.
            var canonical = BuildCanonicalCoordinateMap(rawSegments);

            var cleanedSegments = new List<RawSegment>(rawSegments.Count);
            foreach (var seg in rawSegments)
            {
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

            foreach (var seg in segments)
            {
                var queryEnv = seg.Envelope.Copy();
                queryEnv.ExpandBy(_snapTolerance);

                var candidates = segIndex.Query(queryEnv);
                foreach (var other in candidates)
                {
                    if (other.SegId >= seg.SegId) continue; // process each pair once

                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;

                    for (int k = 0; k < li.IntersectionNum; k++)
                    {
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

            foreach (var seg in segments)
            {
                var splits = splitPoints[seg.SegId];

                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits)
                {
                    if (!sp.Coord.Equals2D(coords[^1], _snapTolerance))
                        coords.Add(sp.Coord);
                }
                if (!seg.P1.Equals2D(coords[^1], _snapTolerance))
                    coords.Add(seg.P1);

                for (int i = 0; i < coords.Count - 1; i++)
                {
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

        private List<RawSegment> ExtractRawSegments()
        {
            var result = new List<RawSegment>(EstimateSegmentCount());
            int segId = 0;

            foreach (var src in _sources)
            {
                if (src.Kind == GeometryKind.Polygon)
                {
                    var poly = (Polygon)src.Geometry;
                    segId = ExtractFromRing(poly.ExteriorRing.Coordinates, src.Id, result, segId, isRing: true);
                    foreach (var hole in poly.InteriorRings)
                        segId = ExtractFromRing(hole.Coordinates, src.Id, result, segId, isRing: true);
                }
                else
                {
                    segId = ExtractFromRing(src.Geometry.Coordinates, src.Id, result, segId, isRing: false);
                }
            }

            return result;
        }

        private int EstimateSegmentCount() => _sources.Sum(s => s.Geometry.NumPoints);

        /// <summary>
        /// Snaps + de-duplicates a coordinate sequence, re-closes rings if needed,
        /// and emits direction-canonicalized RawSegments (smaller coordinate first).
        /// </summary>
        private int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId, bool isRing = true)
        {
            // Snap + de-duplicate consecutive identical coordinates that
            // collapse together after snapping
            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords)
            {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1]))
                    snapped.Add(sc);
            }

            // Re-close the ring if snapping broke closure
            if (isRing && snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(snapped[0]);

            for (int i = 0; i < snapped.Count - 1; i++)
            {
                var p0 = snapped[i];
                var p1 = snapped[i + 1];

                if (p0.Equals2D(p1)) continue; // degenerate after snapping

                // Canonicalize direction: always store the "smaller" coordinate first
                bool reversed = ComparePoints(p0, p1) > 0;
                var (c0, c1) = reversed ? (p1, p0) : (p0, p1);

                result.Add(new RawSegment(segId++, c0, c1, sourceId, reversed));
            }

            return segId;
        }

        private static int ComparePoints(Coordinate a, Coordinate b)
        {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        // -------------------------------------------------------------------
        // Snapping / canonicalization helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Snaps a coordinate to a fixed grid defined by _snapTolerance.
        /// Floor-based with a small epsilon to avoid boundary-straddling issues.
        /// </summary>
        private Coordinate SnapToGrid(Coordinate c)
        {
            double inv = 1.0 / _snapTolerance;
            const double epsilon = 1e-9;

            double x = Math.Floor(c.X * inv + epsilon) * _snapTolerance;
            double y = Math.Floor(c.Y * inv + epsilon) * _snapTolerance;

            return double.IsNaN(c.Z)
                ? new Coordinate(x, y)
                : new CoordinateZ(x, y, c.Z);
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

        /// <summary>
        /// If the intersection point pt is within tolerance of an endpoint of
        /// either segment, snap to that endpoint exactly.
        /// </summary>
        private Coordinate SnapToEndpoint(Coordinate pt, RawSegment seg, RawSegment other)
        {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 })
            {
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
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap)
        {
            if (c0.Equals2D(c1, _snapTolerance)) return; // degenerate

            var k0 = new CoordinateKey(c0, _snapTolerance);
            var k1 = new CoordinateKey(c1, _snapTolerance);
            bool needsSwap = k0.CompareTo(k1) > 0;
            var key = needsSwap ? (k1, k0) : (k0, k1);

            if (!edgeMap.TryGetValue(key, out var edge))
            {
                var (geomC0, geomC1) = needsSwap ? (c1, c0) : (c0, c1);
                var ls = _factory.CreateLineString(new[] { geomC0, geomC1 });
                edge = new NetworkEdge
                {
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

            // Did THIS source traverse edge.StartNode -> edge.EndNode?
            bool sourceGoesStartToEnd = c0.Equals2D(edge.StartNode, _snapTolerance);
            edge.SourceOrientation[sourceId] = sourceGoesStartToEnd;

            if (!_edgesBySource.TryGetValue(sourceId, out var list))
                _edgesBySource[sourceId] = list = new List<NetworkEdge>();
            if (!list.Contains(edge))
                list.Add(edge);
        }

        private NetworkNode GetOrCreateNode(Coordinate coord)
        {
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
        public NetworkNode GetNode(Coordinate coord)
        {
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
        public NetworkDefinition GetNetworkDefinition(int sourceId)
        {
            var edges = GetEdgesFor(sourceId);
            var sharedEdges = edges.Where(e => e.IsShared).ToList();
            var privateEdges = edges.Where(e => !e.IsShared).ToList();

            var neighbourIds = sharedEdges
                .SelectMany(e => e.SourceGeometryIds)
                .Where(id => id != sourceId)
                .Distinct()
                .ToList();

            return new NetworkDefinition
            {
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

        private List<MergedEdge> MergeEdgeSet(IEnumerable<NetworkEdge> edgeSet)
        {
            var edges = edgeSet.ToHashSet();
            if (edges.Count == 0) return new List<MergedEdge>();

            // Build a local adjacency: coord -> edges in this set only
            var adjacency = new Dictionary<CoordinateKey, List<NetworkEdge>>();

            void Touch(Coordinate c, NetworkEdge e)
            {
                var key = new CoordinateKey(c, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var list))
                    adjacency[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }

            foreach (var edge in edges)
            {
                Touch(edge.StartNode, edge);
                Touch(edge.EndNode, edge);
            }

            var visited = new HashSet<int>();
            var result = new List<MergedEdge>();

            foreach (var startEdge in edges)
            {
                if (visited.Contains(startEdge.Id)) continue;

                var chain = WalkFullChain(startEdge, edges, adjacency, visited);
                var coords = ChainToCoordinates(chain);

                result.Add(new MergedEdge
                {
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
            HashSet<int> visited)
        {
            visited.Add(seed.Id);
            var forward = new List<NetworkEdge>();
            var backward = new List<NetworkEdge>();

            // ---- Forward walk from seed.EndNode ----
            var coord = seed.EndNode;
            var current = seed;
            while (true)
            {
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
            while (true)
            {
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
        private Coordinate[] ChainToCoordinates(List<NetworkEdge> chain)
        {
            if (chain.Count == 0) return Array.Empty<Coordinate>();
            if (chain.Count == 1)
                return chain[0].Geometry.Coordinates.ToArray();

            var coords = new List<Coordinate>();

            for (int i = 0; i < chain.Count; i++)
            {
                var edge = chain[i];
                var edgeCs = edge.Geometry.Coordinates;

                if (i == 0)
                {
                    var nextEdge = chain[1];
                    bool forward = ConnectsTo(edge, nextEdge, fromEnd: true);
                    coords.AddRange(forward ? edgeCs : edgeCs.Reverse());
                }
                else
                {
                    var last = coords[^1];
                    bool startMatches = edgeCs[0].Equals2D(last, _snapTolerance);
                    var oriented = startMatches ? edgeCs : edgeCs.Reverse();

                    // Skip the first coord — it's the same as the last one added
                    coords.AddRange(oriented.Skip(1));
                }
            }

            return coords.ToArray();
        }

        private bool ConnectsTo(NetworkEdge edge, NetworkEdge other, bool fromEnd)
        {
            var coord = fromEnd ? edge.EndNode : edge.StartNode;
            return coord.Equals2D(other.StartNode, _snapTolerance)
                || coord.Equals2D(other.EndNode, _snapTolerance);
        }

        // -------------------------------------------------------------------
        // Ring / LineString classification for merged edges
        // -------------------------------------------------------------------

        /// <summary>
        /// Classifies a merged edge against each of its contributing source
        /// geometries: exterior ring, interior ring (hole), or linestring.
        /// </summary>
        public List<MergedEdgeClassification> ClassifyMergedEdge(MergedEdge edge)
        {
            var result = new List<MergedEdgeClassification>();

            foreach (var sourceId in edge.SourceGeometryIds)
            {
                var src = _sources[sourceId];

                if (src.Kind == GeometryKind.LineString)
                {
                    result.Add(new MergedEdgeClassification
                    {
                        Kind = EdgeRingKind.LineString,
                        Source = src,
                    });
                    continue;
                }

                var poly = (Polygon)src.Geometry;
                var ringKind = ClassifyAgainstPolygon(edge.Geometry, poly);

                result.Add(new MergedEdgeClassification
                {
                    Kind = ringKind.Kind,
                    RingIndex = ringKind.RingIndex,
                    Source = src,
                });
            }

            // If contributions disagree, mark as Mixed
            if (result.Select(r => r.Kind).Distinct().Count() > 1)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    var r = result[i];
                    result[i] = new MergedEdgeClassification
                    {
                        Kind = EdgeRingKind.Mixed,
                        RingIndex = r.RingIndex,
                        Source = r.Source,
                    };
                }
            }

            return result;
        }

        private (EdgeRingKind Kind, int? RingIndex) ClassifyAgainstPolygon(LineString edgeGeom, Polygon poly)
        {
            var probe = MidPoint(edgeGeom);

            if (IsOnRing(probe, poly.ExteriorRing))
                return (EdgeRingKind.ExteriorRing, null);

            for (int i = 0; i < poly.NumInteriorRings; i++)
            {
                if (IsOnRing(probe, poly.GetInteriorRingN(i)))
                    return (EdgeRingKind.InteriorRing, i);
            }

            return FallbackClassify(edgeGeom, poly);
        }

        private bool IsOnRing(Point probe, LineString ring) => ring.Distance(probe) <= _snapTolerance;

        private (EdgeRingKind Kind, int? RingIndex) FallbackClassify(LineString edgeGeom, Polygon poly)
        {
            int exteriorVotes = 0;
            var interiorVotes = new int[poly.NumInteriorRings];

            int samples = Math.Min(edgeGeom.NumPoints, 5);
            for (int i = 0; i <= samples; i++)
            {
                int idx = (int)((double)i / samples * (edgeGeom.NumPoints - 1));
                var pt = edgeGeom.Factory.CreatePoint(edgeGeom.GetCoordinateN(idx));

                if (poly.ExteriorRing.Distance(pt) <= _snapTolerance)
                {
                    exteriorVotes++;
                    continue;
                }
                for (int j = 0; j < poly.NumInteriorRings; j++)
                {
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

        private static Point MidPoint(LineString line)
        {
            var coords = line.Coordinates;
            int mid = coords.Length / 2;

            if (coords.Length % 2 == 0)
            {
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
        public LineString OrientToSource(MergedEdge merged, int sourceId)
        {
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
