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
    }

    /// <summary>
    /// A topological node in the planar network — a coordinate where two or more edges meet.
    /// </summary>
    public class NetworkNode
    {
        /// <summary>The snapped, canonical coordinate of this node.</summary>
        public Coordinate Coordinate { get; init; }
        /// <summary>All edges incident to this node.</summary>
        public List<NetworkEdge> Edges { get; } = [];
        /// <summary>Number of edges incident to this node.</summary>
        public int Degree => this.Edges.Count;
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
        public HashSet<int> SourceGeometryIds { get; } = [];
        /// <summary>True when more than one source geometry contributes this edge.</summary>
        public bool IsShared => this.SourceGeometryIds.Count > 1;
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
        public bool IsShared => this.SourceGeometryIds.Count > 1;
        /// <summary>Total length of the merged geometry.</summary>
        public double Length => this.Geometry.Length;
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
        public LineString OrientedGeometry => this.Forward
            ? this.Edge.Geometry
            : (LineString)this.Edge.Geometry.Reverse();
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
        public double TotalLength => this.AllEdges.Sum(e => e.Geometry.Length);
        /// <summary>Sum of lengths of edges shared with other sources.</summary>
        public double SharedLength => this.SharedEdges.Sum(e => e.Geometry.Length);
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
            this._x = (long)Math.Round(snappedCoord.X * inv);
            this._y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y) { this._x = x; this._y = y; }

        /// <summary>Returns a key offset by the given number of grid cells in each axis.</summary>
        public CoordinateKey Offset(int dx, int dy) => new(this._x + dx, this._y + dy);
        /// <inheritdoc/>
        public bool Equals(CoordinateKey o) => this._x == o._x && this._y == o._y;
        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is CoordinateKey k && this.Equals(k);
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this._x, this._y);
        /// <summary>Lexicographic comparison by X then Y, used for deterministic union-find root selection.</summary>
        public int CompareTo(CoordinateKey o) {
            int c = this._x.CompareTo(o._x);
            return c != 0 ? c : this._y.CompareTo(o._y);
        }
    }

    internal sealed class RawSegment
    {
        public readonly int SegId;
        public readonly Coordinate P0, P1;
        public readonly int SourceId;
        public readonly Envelope Envelope;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId) {
            this.SegId = segId; this.P0 = p0; this.P1 = p1; this.SourceId = sourceId;
            this.Envelope = new Envelope(p0, p1);
        }
        public RawSegment WithCoordinates(Coordinate p0, Coordinate p1) => new(this.SegId, p0, p1, this.SourceId);
        public RawSegment WithSegId(int segId) => new(segId, this.P0, this.P1, this.SourceId);
    }

    internal readonly struct SplitPoint
    {
        public readonly Coordinate Coord;
        private readonly double _dist;
        public SplitPoint(Coordinate coord, Coordinate segStart) {
            this.Coord = coord; this._dist = coord.Distance(segStart);
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

        private readonly List<NetworkGeometry> _sources = [];
        private readonly List<NetworkEdge> _edges = [];
        private readonly Dictionary<CoordinateKey, NetworkNode> _nodes = [];
        private readonly Dictionary<int, List<NetworkEdge>> _edgesBySource = [];
        private STRtree<NetworkEdge> _edgeIndex = new();
        private Dictionary<CoordinateKey, Coordinate>? _canonicalMap;

        /// <summary>All network edges produced by the most recent <see cref="Build"/> call.</summary>
        public IReadOnlyList<NetworkEdge> Edges => this._edges;
        /// <summary>All network nodes produced by the most recent <see cref="Build"/> call.</summary>
        public IReadOnlyCollection<NetworkNode> Nodes => this._nodes.Values;
        /// <summary>Zero-based ids of all registered source geometries.</summary>
        public IEnumerable<int> Sources => this._sources.Select(s => s.Id);

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
            this._factory = factory ?? NtsGeometryServices.Instance.CreateGeometryFactory();
            this._snapTolerance = snapTolerance;
            this._dedupeRadius = dedupeRadius > 0 ? dedupeRadius : snapTolerance * 5;
        }

        // -------------------------------------------------------------------
        // Registration
        // -------------------------------------------------------------------

        /// <summary>
        /// Registers a polygon and returns its source id.
        /// The exterior ring and all interior rings (holes) are added to the network.
        /// </summary>
        public int AddPolygon(Polygon polygon) => this.Register(polygon, GeometryKind.Polygon);

        /// <summary>
        /// Registers an open or closed LineString and returns its source id.
        /// If the geometry is a <see cref="LinearRing"/> it is treated as a closed ring.
        /// </summary>
        public int AddLineString(LineString lineString) => this.Register(lineString, GeometryKind.LineString);

        /// <summary>Registers multiple polygons in order, returning nothing (ids are sequential).</summary>
        public void AddPolygons(IEnumerable<Polygon> polygons) { foreach (var p in polygons) this.AddPolygon(p); }

        /// <summary>Registers multiple linestrings in order, returning nothing (ids are sequential).</summary>
        public void AddLineStrings(IEnumerable<LineString> lines) { foreach (var l in lines) this.AddLineString(l); }

        private int Register(Geometry geom, GeometryKind kind) {
            int id = this._sources.Count;
            this._sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
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
            var rawSegments = this.ExtractRawSegments();

            var canonical = this.BuildCanonicalCoordinateMap(rawSegments);
            this._canonicalMap = canonical;

            var cleaned = new List<RawSegment>(rawSegments.Count);
            foreach (var seg in rawSegments) {
                var p0 = canonical[new CoordinateKey(this.SnapToGrid(seg.P0), this._snapTolerance)];
                var p1 = canonical[new CoordinateKey(this.SnapToGrid(seg.P1), this._snapTolerance)];
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
                var qe = seg.Envelope.Copy(); qe.ExpandBy(this._snapTolerance);
                foreach (var other in segIndex.Query(qe)) {
                    if (other.SegId >= seg.SegId) continue;
                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;
                    for (int k = 0; k < li.IntersectionNum; k++) {
                        var pt = this.SnapToGrid(li.GetIntersection(k));
                        if (canonical.TryGetValue(new CoordinateKey(pt, this._snapTolerance), out var cp)) pt = cp;
                        pt = this.SnapToEndpoint(pt, seg, other);
                        splitPoints[seg.SegId].Add(new SplitPoint(pt, seg.P0));
                        splitPoints[other.SegId].Add(new SplitPoint(pt, other.P0));
                    }
                }
            }

            this._nodes.Clear(); this._edges.Clear(); this._edgesBySource.Clear();
            var edgeMap = new Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge>();

            foreach (var seg in segments) {
                var splits = splitPoints[seg.SegId];
                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits)
                    if (!sp.Coord.Equals2D(coords[^1], this._snapTolerance)) coords.Add(sp.Coord);
                if (!seg.P1.Equals2D(coords[^1], this._snapTolerance)) coords.Add(seg.P1);

                for (int i = 0; i < coords.Count - 1; i++) {
                    if (!coords[i].Equals2D(coords[i + 1], this._snapTolerance))
                        this.InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
                }
            }

            this._edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in this._edges) this._edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }

        // -------------------------------------------------------------------
        // Segment extraction
        // -------------------------------------------------------------------

        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(this.EstimateSegmentCount());
            int segId = 0;
            foreach (var src in this._sources) {
                if (src.Kind == GeometryKind.Polygon) {
                    var poly = (Polygon)src.Geometry;
                    segId = this.ExtractFromRing(poly.ExteriorRing.Coordinates, src.Id, result, segId, true);
                    foreach (var hole in poly.InteriorRings)
                        segId = this.ExtractFromRing(hole.Coordinates, src.Id, result, segId, true);
                }
                else {
                    segId = this.ExtractFromRing(src.Geometry.Coordinates, src.Id, result, segId,
                        src.Geometry is LinearRing);
                }
            }
            return result;
        }

        private int EstimateSegmentCount() => this._sources.Sum(s => s.Geometry.NumPoints);

        private int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId, bool isRing) {

            // Strip closing duplicate before snapping so re-close is deterministic
            if (isRing && coords.Length > 1 &&
                this.SnapToGrid(coords[0]).Equals2D(this.SnapToGrid(coords[^1])))
                coords = coords[..^1];

            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = this.SnapToGrid(c);
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
            double inv = 1.0 / this._snapTolerance;
            double x = Math.Round(c.X * inv) / inv;
            double y = Math.Round(c.Y * inv) / inv;
            var coord = double.IsNaN(c.Z) ? new Coordinate(x, y) : new CoordinateZ(x, y, c.Z);
            this._factory.PrecisionModel.MakePrecise(coord);
            return coord;
        }

        private Coordinate Canonicalize(Coordinate c) {
            var snapped = this.SnapToGrid(c);
            if (this._canonicalMap == null) return snapped;
            var key = new CoordinateKey(snapped, this._snapTolerance);
            return this._canonicalMap.TryGetValue(key, out var canon) ? canon : snapped;
        }

        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {

            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments)
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = this.SnapToGrid(c);
                    var key = new CoordinateKey(snapped, this._snapTolerance);
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
                if (seg.P0.Distance(seg.P1) <= this._dedupeRadius) {
                    Union(new CoordinateKey(this.SnapToGrid(seg.P0), this._snapTolerance),
                          new CoordinateKey(this.SnapToGrid(seg.P1), this._snapTolerance));
                }

            var result = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var key in cellValue.Keys) result[key] = cellValue[Find(key)];
            return result;
        }

        private Coordinate SnapToEndpoint(Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 })
                if (pt.Distance(ep) <= this._snapTolerance) return ep;
            return pt;
        }

        // -------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------

        private void InsertEdge(
            Coordinate c0, Coordinate c1, int sourceId,
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {

            if (c0.Equals2D(c1, this._snapTolerance)) return;
            var k0 = new CoordinateKey(c0, this._snapTolerance);
            var k1 = new CoordinateKey(c1, this._snapTolerance);
            bool swap = k0.CompareTo(k1) > 0;
            var key = swap ? (k1, k0) : (k0, k1);

            if (!edgeMap.TryGetValue(key, out var edge)) {
                var (gc0, gc1) = swap ? (c1, c0) : (c0, c1);
                var ls = this._factory.CreateLineString(new[] { gc0, gc1 });
                edge = new NetworkEdge { Id = this._edges.Count, Geometry = ls, StartNode = gc0, EndNode = gc1 };
                this._edges.Add(edge);
                edgeMap[key] = edge;
                this.GetOrCreateNode(gc0).Edges.Add(edge);
                this.GetOrCreateNode(gc1).Edges.Add(edge);
            }

            edge.SourceGeometryIds.Add(sourceId);
            if (!this._edgesBySource.TryGetValue(sourceId, out var list))
                this._edgesBySource[sourceId] = list = [];
            if (!list.Contains(edge)) list.Add(edge);
        }

        private NetworkNode GetOrCreateNode(Coordinate coord) {
            var key = new CoordinateKey(coord, this._snapTolerance);
            if (!this._nodes.TryGetValue(key, out var node))
                this._nodes[key] = node = new NetworkNode { Coordinate = coord };
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
            => this._edgesBySource.TryGetValue(sourceId, out var list) ? list : Array.Empty<NetworkEdge>();

        /// <summary>
        /// Returns all edges shared between exactly the two given source geometry ids.
        /// </summary>
        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => this.GetEdgesFor(sourceIdA).Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        /// <summary>
        /// Returns every edge in the network that is shared by two or more source geometries.
        /// </summary>
        public IEnumerable<NetworkEdge> GetAllSharedEdges() => this._edges.Where(e => e.IsShared);

        /// <summary>
        /// Returns the source geometry records that contribute the given edge.
        /// </summary>
        public IEnumerable<NetworkGeometry> GetSourceGeometriesForEdge(NetworkEdge edge)
            => edge.SourceGeometryIds.Select(id => this._sources[id]);

        /// <summary>
        /// Returns the network node at the given coordinate (within snap tolerance), or null
        /// if no node exists there.
        /// </summary>
        public NetworkNode? GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, this._snapTolerance);
            return this._nodes.TryGetValue(key, out var node) ? node : null;
        }

        /// <summary>
        /// Returns all edges whose bounding box intersects the given envelope.
        /// </summary>
        public IEnumerable<NetworkEdge> QueryEdges(Envelope envelope)
            => this._edgeIndex.Query(envelope).Cast<NetworkEdge>();

        /// <summary>
        /// Returns a full topological description of a source geometry, including its shared
        /// edges, private edges, and neighbouring source geometries.
        /// </summary>
        public NetworkDefinition GetNetworkDefinition(int sourceId) {
            var edges = this.GetEdgesFor(sourceId);
            var shared = edges.Where(e => e.IsShared).ToList();
            var priv = edges.Where(e => !e.IsShared).ToList();
            var nbrIds = shared.SelectMany(e => e.SourceGeometryIds)
                               .Where(id => id != sourceId).Distinct().ToList();
            return new NetworkDefinition {
                SourceId = sourceId,
                Source = this._sources[sourceId],
                AllEdges = edges.ToList(),
                SharedEdges = shared,
                PrivateEdges = priv,
                Neighbours = nbrIds.Select(id => this._sources[id]).ToList(),
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
            var edges = this.GetEdgesFor(sourceId).ToHashSet();
            if (edges.Count == 0) return [];

            var adj = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            void Touch(Coordinate c, NetworkEdge e) {
                var key = new CoordinateKey(c, this._snapTolerance);
                if (!adj.TryGetValue(key, out var list)) adj[key] = list = [];
                list.Add(e);
            }
            foreach (var edge in edges) { Touch(edge.StartNode, edge); Touch(edge.EndNode, edge); }

            var srcCoords = this._sources[sourceId].Geometry.Coordinates;
            NetworkEdge? seed = null;
            for (int i = 0; i < srcCoords.Length - 1 && seed == null; i++) {
                var a = new CoordinateKey(this.Canonicalize(srcCoords[i]), this._snapTolerance);
                var b = new CoordinateKey(this.Canonicalize(srcCoords[i + 1]), this._snapTolerance);
                if (!adj.TryGetValue(a, out var atA)) continue;
                foreach (var candidate in atA) {
                    var ks = new CoordinateKey(candidate.StartNode, this._snapTolerance);
                    var ke = new CoordinateKey(candidate.EndNode, this._snapTolerance);
                    if ((ks.Equals(a) && ke.Equals(b)) || (ke.Equals(a) && ks.Equals(b))) {
                        seed = candidate; break;
                    }
                }
            }
            seed ??= edges.First();

            return this.WalkChain(seed, edges, adj);
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
                var key = new CoordinateKey(coord, this._snapTolerance);
                if (!adj.TryGetValue(key, out var nbrs)) break;
                var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (cands.Count != 1) break;
                var next = cands[0];
                bool closed = next.StartNode.Equals2D(seed.StartNode, this._snapTolerance)
                           || next.EndNode.Equals2D(seed.StartNode, this._snapTolerance);
                visited.Add(next.Id);
                forward.Add(next);
                if (closed) break;
                coord = next.StartNode.Equals2D(coord, this._snapTolerance) ? next.EndNode : next.StartNode;
            }

            bool ringClosed = forward.Count > 0 && (
                forward[^1].StartNode.Equals2D(seed.StartNode, this._snapTolerance) ||
                forward[^1].EndNode.Equals2D(seed.StartNode, this._snapTolerance));

            if (!ringClosed) {
                coord = seed.StartNode;
                while (true) {
                    var key = new CoordinateKey(coord, this._snapTolerance);
                    if (!adj.TryGetValue(key, out var nbrs)) break;
                    var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                    if (cands.Count != 1) break;
                    var prev = cands[0];
                    visited.Add(prev.Id);
                    backward.Add(prev);
                    coord = prev.StartNode.Equals2D(coord, this._snapTolerance) ? prev.EndNode : prev.StartNode;
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
                return chain[0].Geometry.Coordinates.Select(this.Canonicalize).ToArray();

            var coords = new List<Coordinate>();
            var e0 = chain[0]; var e1 = chain[1];
            bool e0Fwd = e0.EndNode.Equals2D(e1.StartNode, this._snapTolerance)
                      || e0.EndNode.Equals2D(e1.EndNode, this._snapTolerance);
            var first = e0Fwd ? e0.Geometry.Coordinates : e0.Geometry.Coordinates.Reverse().ToArray();
            foreach (var c in first) coords.Add(this.Canonicalize(c));

            for (int i = 1; i < chain.Count; i++) {
                var edge = chain[i]; var prevEnd = coords[^1];
                bool fwd;
                if (edge.StartNode.Equals2D(prevEnd, this._snapTolerance)) fwd = true;
                else if (edge.EndNode.Equals2D(prevEnd, this._snapTolerance)) fwd = false;
                else fwd = edge.StartNode.Distance(prevEnd) <= edge.EndNode.Distance(prevEnd);
                var ec = fwd ? edge.Geometry.Coordinates : edge.Geometry.Coordinates.Reverse().ToArray();
                foreach (var c in ec.Skip(1)) coords.Add(this.Canonicalize(c));
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
            var chain = this.GetFullEdgeChainFor(sourceId);
            if (chain.Count == 0) return [];

            var result = new List<MergedEdge>();
            var current = new List<NetworkEdge> { chain[0] };

            for (int i = 1; i < chain.Count; i++) {
                if (chain[i].SourceGeometryIds.SetEquals(current[^1].SourceGeometryIds))
                    current.Add(chain[i]);
                else { result.Add(this.BuildMergedEdge(current)); current = [chain[i]]; }
            }
            if (current.Count > 0) result.Add(this.BuildMergedEdge(current));
            return result;
        }

        private MergedEdge BuildMergedEdge(List<NetworkEdge> edges) => new MergedEdge {
            Geometry = this._factory.CreateLineString(this.StitchChain(edges)),
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
        public (List<MergedEdge> AllEdges, Dictionary<int, List<EdgeReference>> SourceRefs)
            BuildEdgeIndex() {

            var canonicalByKey = new Dictionary<string, MergedEdge>();
            var edgeToCanonical = new Dictionary<int, MergedEdge>();
            var allMergedEdges = new List<MergedEdge>();

            foreach (var src in this._sources) {
                foreach (var merged in this.GetMergedEdgeChainFor(src.Id)) {
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

            var sourceRefs = new Dictionary<int, List<EdgeReference>>();

            foreach (var src in this._sources) {
                var chain = this.GetFullEdgeChainFor(src.Id);
                if (chain.Count == 0) { sourceRefs[src.Id] = []; continue; }

                var source = this._sources[src.Id];
                bool isRing = source.Kind == GeometryKind.Polygon || source.Geometry is LinearRing;
                if (isRing && chain.Count > 1)
                    chain = this.RotateToGroupBoundary(chain, edgeToCanonical);

                var refs = new List<EdgeReference>();
                MergedEdge? lastCanonical = null;

                for (int ci = 0; ci < chain.Count; ci++) {
                    var netEdge = chain[ci];
                    if (!edgeToCanonical.TryGetValue(netEdge.Id, out var canonical)) continue;
                    if (ReferenceEquals(canonical, lastCanonical)) continue;

                    Coordinate srcEntry;
                    if (ci == 0) {
                        if (chain.Count > 1) {
                            var next = chain[1];
                            srcEntry = netEdge.EndNode.Equals2D(next.StartNode, this._snapTolerance) ||
                                       netEdge.EndNode.Equals2D(next.EndNode, this._snapTolerance)
                                ? netEdge.StartNode
                                : netEdge.EndNode;
                        }
                        else {
                            srcEntry = netEdge.StartNode;
                        }
                    }
                    else {
                        var prev = chain[ci - 1];
                        srcEntry = prev.StartNode.Equals2D(netEdge.StartNode, this._snapTolerance) ||
                                   prev.EndNode.Equals2D(netEdge.StartNode, this._snapTolerance)
                            ? netEdge.StartNode
                            : netEdge.EndNode;
                    }

                    var canonFirst = canonical.Geometry.Coordinates[0];
                    bool forward = srcEntry.Equals2D(canonFirst, this._snapTolerance);

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
                if (coords.Count == 0) coords.AddRange(ec.Select(this.Canonicalize));
                else coords.AddRange(ec.Skip(1).Select(this.Canonicalize));
            }
            var source = this._sources[sourceId];
            bool isRing = source.Kind == GeometryKind.Polygon || source.Geometry is LinearRing;
            if (isRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);
            return this._factory.CreateLineString(coords.ToArray());
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
                var src = this._sources[sourceId];
                if (src.Kind == GeometryKind.LineString) {
                    result.Add(new MergedEdgeClassification { Kind = EdgeRingKind.LineString, Source = src });
                    continue;
                }
                var rk = this.ClassifyAgainstPolygon(edge.Geometry, (Polygon)src.Geometry);
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
            if (poly.ExteriorRing.Distance(probe) <= this._snapTolerance) return (EdgeRingKind.ExteriorRing, null);
            for (int i = 0; i < poly.NumInteriorRings; i++)
                if (poly.GetInteriorRingN(i).Distance(probe) <= this._snapTolerance)
                    return (EdgeRingKind.InteriorRing, i);
            return this.FallbackClassify(geom, poly);
        }

        private (EdgeRingKind Kind, int? RingIndex) FallbackClassify(LineString geom, Polygon poly) {
            int extV = 0; var intV = new int[poly.NumInteriorRings];
            int n = Math.Min(geom.NumPoints, 5);
            for (int i = 0; i <= n; i++) {
                int idx = (int)((double)i / n * (geom.NumPoints - 1));
                var pt = geom.Factory.CreatePoint(geom.GetCoordinateN(idx));
                if (poly.ExteriorRing.Distance(pt) <= this._snapTolerance) { extV++; continue; }
                for (int j = 0; j < poly.NumInteriorRings; j++)
                    if (poly.GetInteriorRingN(j).Distance(pt) <= this._snapTolerance) intV[j]++;
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