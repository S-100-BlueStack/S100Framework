using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System.Text;

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
            _x = (long)Math.Floor(snappedCoord.X * inv);
            _y = (long)Math.Floor(snappedCoord.Y * inv);
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
        public override string ToString() => $"({_x},{_y})";
    }

    internal sealed class RawSegment
    {
        public readonly int SegId;
        public readonly Coordinate P0, P1;
        public readonly int SourceId;
        public readonly Envelope Envelope;
        /// <summary>True when P0/P1 were swapped into canonical order during extraction,
        /// i.e. the source traverses this segment P1 -> P0.</summary>
        public readonly bool Swapped;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId, bool swapped = false) {
            SegId = segId; P0 = p0; P1 = p1; SourceId = sourceId; Swapped = swapped;
            Envelope = new Envelope(p0, p1);
        }
        public RawSegment WithCoordinates(Coordinate p0, Coordinate p1) => new(SegId, p0, p1, SourceId, Swapped);
        public RawSegment WithSegId(int segId) => new(segId, P0, P1, SourceId, Swapped);
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
        /// <summary>Per-source NetworkEdge chains in exact raw traversal order,
        /// recorded during <see cref="Build"/>. Immune to graph-walk ambiguity at
        /// nodes the source passes through more than once (e.g. narrow features
        /// where interior splitting makes a ring touch itself).</summary>
        private readonly Dictionary<int, List<NetworkEdge>> _orderedEdgesBySource = new();

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
        private ILogger? _logger = default;

        internal ILogger Logger {
            set => _logger = value;
        }

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
            _orderedEdgesBySource.Clear();
            var edgeMap = new Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge>();

            foreach (var seg in segments) {
                var splits = splitPoints[seg.SegId];
                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits)
                    if (!sp.Coord.Equals2D(coords[^1], _snapTolerance)) coords.Add(sp.Coord);
                if (!seg.P1.Equals2D(coords[^1], _snapTolerance)) coords.Add(seg.P1);

                // Collect this segment's sub-edges, then append them to the source's
                // ordered chain in RAW TRAVERSAL order. Segments were canonicalized
                // (endpoints swapped) during extraction; the Swapped flag restores
                // the original direction here.
                var segEdges = new List<NetworkEdge>(coords.Count - 1);
                for (int i = 0; i < coords.Count - 1; i++) {
                    if (!coords[i].Equals2D(coords[i + 1], _snapTolerance)) {
                        var e = InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
                        if (e != null) segEdges.Add(e);
                    }
                }
                if (seg.Swapped) segEdges.Reverse();
                if (!_orderedEdgesBySource.TryGetValue(seg.SourceId, out var chainList))
                    _orderedEdgesBySource[seg.SourceId] = chainList = new List<NetworkEdge>();
                chainList.AddRange(segEdges);
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
                bool swapped = CompareCoords(p0, p1) > 0;
                if (swapped) (p0, p1) = (p1, p0);
                result.Add(new RawSegment(segId++, p0, p1, sourceId, swapped));
            }
            return segId;
        }

        private static int CompareCoords(Coordinate a, Coordinate b) {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        // -------------------------------------------------------------------
        // Ring degeneracy pre-flight check
        // -------------------------------------------------------------------

        public enum RingCollapseReason
        {
            /// <summary>The ring survives snapping and will bound an area.</summary>
            None,
            /// <summary>Fewer than three distinct vertices remain after snapping to the grid.</summary>
            TooFewDistinctVertices,
            /// <summary>The ring is a sliver: its mean width is below the snap tolerance, so
            /// noding will fold its vertices onto its own segments and the interior vanishes.</summary>
            Sliver,
        }

        public readonly struct RingCollapseCheck
        {
            /// <summary>True if the ring will degenerate to a line rather than bound an area.</summary>
            public bool WillCollapse { get; init; }
            public RingCollapseReason Reason { get; init; }
            /// <summary>Distinct grid cells occupied by the ring's vertices after snapping.</summary>
            public int DistinctVertices { get; init; }
            /// <summary>Absolute area of the snapped ring, in squared coordinate units.</summary>
            public double Area { get; init; }
            /// <summary>2 * Area / Perimeter — for a long thin ring, its effective thickness.</summary>
            public double MeanWidth { get; init; }
            /// <summary><see cref="MeanWidth"/> expressed in snap-tolerance units. Below 1.0 collapses.</summary>
            public double MeanWidthInTolerances { get; init; }

            public override string ToString() => WillCollapse
                ? $"COLLAPSES ({Reason}): vertices={DistinctVertices} area={Area:E3} " +
                  $"meanWidth={MeanWidth:E3} ({MeanWidthInTolerances:F3} x tol)"
                : $"OK: vertices={DistinctVertices} area={Area:E3} " +
                  $"meanWidth={MeanWidth:E3} ({MeanWidthInTolerances:F3} x tol)";
        }

        /// <summary>
        /// Tests whether <paramref name="ring"/> will degenerate into a line rather than bound an
        /// area once it is snapped into this network's tolerance grid. Call before adding the ring
        /// as a source.
        ///
        /// Two ways a ring collapses. It may lose vertices outright, leaving fewer than three
        /// distinct grid cells. More insidiously it may keep all its vertices — each landing in its
        /// own cell — yet be so thin that every vertex falls within the snap tolerance of one of the
        /// ring's own segments, at which point noding folds it flat. A 4.7 km ring only 2 cm wide
        /// does exactly this. Vertex count and area alone both miss it; mean width (2 * area /
        /// perimeter, i.e. the ring's effective thickness) catches it, and does not punish rings
        /// that are small but properly proportioned.
        ///
        /// Note this considers the ring in isolation. It cannot predict collapse caused by
        /// proximity clustering against OTHER sources, which is only knowable once every source
        /// is present.
        /// </summary>
        public RingCollapseCheck CheckRingCollapse(LinearRing ring) {
            if (ring == null) throw new ArgumentNullException(nameof(ring));

            // Snap, then drop consecutive duplicates — the same reduction the network performs.
            var snapped = new List<Coordinate>();
            var cells = new HashSet<CoordinateKey>();
            CoordinateKey? prev = null;
            foreach (var c in ring.Coordinates) {
                var s = SnapToGrid(c);
                var k = new CoordinateKey(s, _snapTolerance);
                if (prev.HasValue && prev.Value.Equals(k)) continue;
                snapped.Add(s);
                prev = k;
            }
            // Distinct cells, excluding the closing vertex.
            for (int i = 0; i < snapped.Count - 1; i++)
                cells.Add(new CoordinateKey(snapped[i], _snapTolerance));

            if (cells.Count < 3) {
                return new RingCollapseCheck {
                    WillCollapse = true,
                    Reason = RingCollapseReason.TooFewDistinctVertices,
                    DistinctVertices = cells.Count,
                    Area = 0,
                    MeanWidth = 0,
                    MeanWidthInTolerances = 0,
                };
            }

            if (snapped.Count > 1 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(new Coordinate(snapped[0].X, snapped[0].Y));

            double area = 0, perimeter = 0;
            for (int i = 0; i < snapped.Count - 1; i++) {
                var a = snapped[i]; var b = snapped[i + 1];
                area += a.X * b.Y - b.X * a.Y;
                perimeter += a.Distance(b);
            }
            area = Math.Abs(area) / 2.0;

            double meanWidth = perimeter > 0 ? 2.0 * area / perimeter : 0.0;
            double inTol = meanWidth / _snapTolerance;
            bool collapses = meanWidth < _snapTolerance;

            return new RingCollapseCheck {
                WillCollapse = collapses,
                Reason = collapses ? RingCollapseReason.Sliver : RingCollapseReason.None,
                DistinctVertices = cells.Count,
                Area = area,
                MeanWidth = meanWidth,
                MeanWidthInTolerances = inTol,
            };
        }

        /// <summary>Convenience overload: true if the ring will degenerate to a line.</summary>
        public bool WillRingCollapseToLine(LinearRing ring) => CheckRingCollapse(ring).WillCollapse;

        // -------------------------------------------------------------------
        // Snapping / canonicalization
        // -------------------------------------------------------------------

        private Coordinate SnapToGrid(Coordinate c) {
            double inv = 1.0 / _snapTolerance;
            double x = Math.Floor(c.X * inv) / inv;
            double y = Math.Floor(c.Y * inv) / inv;
            var coord = double.IsNaN(c.Z) ? new Coordinate(x, y) : new CoordinateZ(x, y, c.Z);
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
            var cellSources = new Dictionary<CoordinateKey, HashSet<int>>();
            foreach (var seg in rawSegments)
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key)) cellValue[key] = snapped;
                    if (!cellSources.TryGetValue(key, out var srcs))
                        cellSources[key] = srcs = new HashSet<int>();
                    srcs.Add(seg.SourceId);
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

            // General cross-source proximity clustering.
            //
            // The pass above only ever unions the two endpoints OF A SINGLE SHORT SEGMENT.
            // It cannot see the case where two different sources describe the SAME node with
            // slightly different coordinates (e.g. 9.963051 vs 9.96305 — a full _snapTolerance
            // cell apart). No segment joins those two vertices, so no union is attempted, they
            // keep distinct CoordinateKeys, and any ring passing through that node is severed.
            //
            // So: union any two occupied cells whose representative coordinates lie within
            // _dedupeRadius of each other, PROVIDED their source-id sets are disjoint.
            //
            // The disjointness guard is the important part. If any single source contributes a
            // vertex to both cells, then that source's own geometry passes close to itself here,
            // and fusing the cells welds its ring to itself — producing a degree-4 node and a
            // figure-eight that no single walk can traverse (sources 1211, 231). Testing merely
            // that the two source SETS differ is not enough: a cell {231} adjacent to a cell
            // {231, 87} has differing sets yet still fuses ring 231 to itself. Disjointness is
            // the correct condition, and it still admits the case we want, where one source
            // writes 9.963051 and a disjoint group of others write 9.96305.
            int proxCellRadius = (int)Math.Ceiling(_dedupeRadius / _snapTolerance);
            foreach (var key in cellValue.Keys.OrderBy(k => k).ToList()) {
                var c0 = cellValue[key];
                var s0 = cellSources[key];
                for (int dx = -proxCellRadius; dx <= proxCellRadius; dx++)
                    for (int dy = -proxCellRadius; dy <= proxCellRadius; dy++) {
                        if (dx == 0 && dy == 0) continue;
                        var nk = key.Offset(dx, dy);
                        if (!cellValue.TryGetValue(nk, out var c1)) continue;
                        if (c0.Distance(c1) > _dedupeRadius) continue;
                        // Any shared source => that source approaches itself here. Never fuse.
                        if (s0.Overlaps(cellSources[nk])) continue;
                        Union(key, nk);
                    }
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

        private NetworkEdge? InsertEdge(
            Coordinate c0, Coordinate c1, int sourceId,
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {

            if (c0.Equals2D(c1, _snapTolerance)) return null;

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
            return edge;
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

        /// <summary>
        /// Node identity for graph traversal. Must go through <see cref="Canonicalize"/>:
        /// BuildCanonicalCoordinateMap union-finds cells within _dedupeRadius, which spans
        /// several _snapTolerance cells. Keying adjacency on the raw snapped coordinate
        /// therefore files two edges that meet at the same canonical node under different
        /// keys, silently disconnecting the graph at that node.
        /// </summary>
        private CoordinateKey NodeKey(Coordinate c) =>
            new CoordinateKey(Canonicalize(c), _snapTolerance);

        /// <summary>
        /// Removes degenerate out-and-back spurs from a recorded traversal chain.
        ///
        /// A source can traverse the SAME network edge twice in immediate succession:
        /// its raw geometry spikes out to a vertex and comes straight back, and the
        /// return segment is then split at the spike's base (because the base vertex
        /// lies on it), producing edge X followed by edge X again. Such a spur has
        /// zero width and contributes nothing to the source's shape.
        ///
        /// Both traversals must be dropped together. Keeping one - as a plain
        /// first-occurrence dedupe does - leaves that edge attached to the chain by
        /// one end only: a dangle. That breaks chain continuity, which corrupts the
        /// stitched canonical geometry in GetMergedEdgeChainFor and, downstream, the
        /// assembled ring.
        ///
        /// For rings the chain is cyclic, so a spur straddling the seam appears as
        /// first==last rather than as an adjacent pair; that case is collapsed too.
        /// Passes repeat until stable, which unwinds nested spurs.
        /// </summary>
        private static List<NetworkEdge> CollapseSpurs(List<NetworkEdge> recorded, bool isRing) {
            var work = recorded;
            bool changed = true;
            while (changed) {
                changed = false;

                var next = new List<NetworkEdge>(work.Count);
                for (int i = 0; i < work.Count; i++) {
                    if (i + 1 < work.Count && work[i].Id == work[i + 1].Id) {
                        i++;                 // skip BOTH members of the pair
                        changed = true;
                        continue;
                    }
                    next.Add(work[i]);
                }
                work = next;

                // Ring seam straddle: the two traversals sit at opposite ends of the
                // chain because the source's raw start vertex fell inside the spur.
                if (isRing && work.Count >= 2 && work[0].Id == work[^1].Id) {
                    work = work.GetRange(1, work.Count - 2);
                    changed = true;
                }
            }
            return work;
        }

        public List<NetworkEdge> GetFullEdgeChainFor(int sourceId) {
            // Preferred path: the exact traversal-order chain recorded during Build.
            // This is deterministic and immune to walk ambiguity at nodes the source
            // passes through more than once (e.g. src605-style figure-eight touches
            // where a ring's closing segment is split at a node the ring also visits
            // as a vertex). Degenerate out-and-back spurs are collapsed first (see
            // CollapseSpurs); any remaining repeat - an edge the source genuinely
            // reuses in two separate places - is kept once, first occurrence,
            // matching the visited-once semantics of the graph walk below, which
            // remains only as a fallback.
            if (_orderedEdgesBySource.TryGetValue(sourceId, out var recorded)) {
                var collapsed = CollapseSpurs(recorded, _sources[sourceId].IsRing);
                var seen = new HashSet<int>();
                var chain0 = new List<NetworkEdge>(collapsed.Count);
                foreach (var e in collapsed)
                    if (seen.Add(e.Id)) chain0.Add(e);
                if (chain0.Count > 0) return chain0;
            }

            var edges = GetEdgesFor(sourceId).ToHashSet();
            if (edges.Count == 0) return new List<NetworkEdge>();

            var adj = new Dictionary<CoordinateKey, List<NetworkEdge>>();
            void Touch(Coordinate c, NetworkEdge e) {
                var key = NodeKey(c);
                if (!adj.TryGetValue(key, out var list)) adj[key] = list = new List<NetworkEdge>();
                list.Add(e);
            }
            foreach (var edge in edges) { Touch(edge.StartNode, edge); Touch(edge.EndNode, edge); }

            var srcCoords = _sources[sourceId].Geometry.Coordinates;
            NetworkEdge? seed = null;
            for (int i = 0; i < srcCoords.Length - 1 && seed == null; i++) {
                var a = NodeKey(srcCoords[i]);
                var b = NodeKey(srcCoords[i + 1]);
                if (!adj.TryGetValue(a, out var atA)) continue;
                foreach (var candidate in atA) {
                    var ks = NodeKey(candidate.StartNode);
                    var ke = NodeKey(candidate.EndNode);
                    if ((ks.Equals(a) && ke.Equals(b)) || (ke.Equals(a) && ks.Equals(b))) {
                        seed = candidate; break;
                    }
                }
            }
            seed ??= edges.First();

            var chain = WalkChain(seed, edges, adj);

            // Invariant: the walk must reach every edge belonging to this source. If it
            // doesn't, the source's edge set is not a single connected path/cycle — some
            // node that should be shared has been split into two CoordinateKey cells (or
            // fused into a spurious junction). Either way the assembled geometry will have
            // a gap. Break here so the broken adjacency can be inspected in situ rather
            // than being discovered downstream as a missing edge.
            if (chain.Count != edges.Count && System.Diagnostics.Debugger.IsAttached) {
                var console = new StringBuilder();
                var degree1 = adj.Where(kv => kv.Value.Count == 1).ToList();
                var degree3 = adj.Where(kv => kv.Value.Count > 2).ToList();
                console.AppendLine(
                    $"[MixedTopologyNetwork] source {sourceId}: walked {chain.Count} of " +
                    $"{edges.Count} edges; cells={adj.Count} deg1={degree1.Count} " +
                    $"deg3plus={degree3.Count}");
                foreach (var kv in degree1)
                    console.AppendLine(
                        $"    deg1 cell={kv.Key} edge={kv.Value[0].Id} " +
                        $"[{kv.Value[0].StartNode.X:F7} {kv.Value[0].StartNode.Y:F7}] -> " +
                        $"[{kv.Value[0].EndNode.X:F7} {kv.Value[0].EndNode.Y:F7}] " +
                        $"srcs={string.Join("/", kv.Value[0].SourceGeometryIds)}");
                foreach (var kv in degree3) {
                    console.AppendLine(
                        $"    FUSED cell={kv.Key} degree={kv.Value.Count}");
                    foreach (var e in kv.Value)
                        console.AppendLine(
                            $"        e{e.Id} [{e.StartNode.X:F7} {e.StartNode.Y:F7}] -> " +
                            $"[{e.EndNode.X:F7} {e.EndNode.Y:F7}] " +
                            $"srcs={string.Join("/", e.SourceGeometryIds)}");
                }
                // Inspect: degree1 (split node) / degree3 (fused node) / chain / edges.
                var txt = console.ToString();

                _logger?.LogWarning($"src{sourceId}" + Environment.NewLine + txt);

                //if (System.Diagnostics.Debugger.IsAttached)
                //    System.Diagnostics.Debugger.Break();
            }

            return chain;
        }

        private List<NetworkEdge> WalkChain(
            NetworkEdge seed,
            HashSet<NetworkEdge> edgeSet,
            Dictionary<CoordinateKey, List<NetworkEdge>> adj) {

            var visited = new HashSet<int> { seed.Id };
            var forward = new List<NetworkEdge>();
            var backward = new List<NetworkEdge>();

            var seedStartKey = NodeKey(seed.StartNode);

            var coord = seed.EndNode;
            while (true) {
                var key = NodeKey(coord);
                if (!adj.TryGetValue(key, out var nbrs)) break;
                var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                if (cands.Count != 1) break;
                var next = cands[0];

                visited.Add(next.Id);
                forward.Add(next);

                bool closedAtSeedStart =
                    NodeKey(next.StartNode).Equals(seedStartKey) ||
                    NodeKey(next.EndNode).Equals(seedStartKey);
                bool allVisited = visited.Count == edgeSet.Count;

                if (closedAtSeedStart || allVisited) break;

                coord = NodeKey(next.StartNode).Equals(key) ? next.EndNode : next.StartNode;
            }

            bool ringClosed =
                visited.Count == edgeSet.Count ||
                (forward.Count > 0 && (
                    NodeKey(forward[^1].StartNode).Equals(seedStartKey) ||
                    NodeKey(forward[^1].EndNode).Equals(seedStartKey)));

            if (!ringClosed) {
                coord = seed.StartNode;
                while (true) {
                    var key = NodeKey(coord);
                    if (!adj.TryGetValue(key, out var nbrs)) break;
                    var cands = nbrs.Where(e => edgeSet.Contains(e) && !visited.Contains(e.Id)).ToList();
                    if (cands.Count != 1) break;
                    var prev = cands[0];
                    visited.Add(prev.Id);
                    backward.Add(prev);
                    coord = NodeKey(prev.StartNode).Equals(key) ? prev.EndNode : prev.StartNode;
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
                bool sameSig = chain[i].SourceGeometryIds.SetEquals(current[^1].SourceGeometryIds);

                // A canonical group must also break at any junction node with network
                // degree >= 3, even when the source signature is unchanged. A degree-3+
                // node is a genuine topological junction: some other path departs there,
                // and canonicals from sources using that third branch reference the
                // junction as an edge endpoint. If we let a group span through it, this
                // source's canonical overlaps the junction and cannot be aligned with
                // the canonicals of the other sources meeting at that node.
                bool junctionOk = true;
                if (sameSig) {
                    var prev = current[^1];
                    var curr = chain[i];
                    Coordinate junction;
                    if (curr.StartNode.Equals2D(prev.StartNode) || curr.EndNode.Equals2D(prev.StartNode))
                        junction = prev.StartNode;
                    else
                        junction = prev.EndNode;
                    var node = GetNode(junction);
                    if (node != null && node.Degree > 2) junctionOk = false;
                }

                if (sameSig && junctionOk)
                    current.Add(chain[i]);
                else { result.Add(BuildMergedEdge(current)); current = new List<NetworkEdge> { chain[i] }; }
            }
            if (current.Count > 0) result.Add(BuildMergedEdge(current));

            // For ring sources, WalkChain seeds from a raw vertex which may sit in the
            // middle of a canonical group. This causes the first and last groups in the
            // result to be fragments of the same canonical run (same SourceGeometryIds)
            // separated by the ring wrap-around. Detect and merge them so each canonical
            // group appears as one continuous MergedEdge, regardless of where the seed fell.
            // Conditions: same signature, the fragments actually share an endpoint, and
            // the junction between them is NOT a degree-3+ node (same rule as above).
            var src = _sources[sourceId];
            if (src.IsRing && result.Count >= 2 &&
                result[0].SourceGeometryIds.SetEquals(result[^1].SourceGeometryIds)) {
                var eL = result[^1].ConstituentEdges.Last();
                var eF = result[0].ConstituentEdges.First();
                Coordinate? junction = null;
                if (eL.EndNode.Equals2D(eF.StartNode) || eL.EndNode.Equals2D(eF.EndNode))
                    junction = eL.EndNode;
                else if (eL.StartNode.Equals2D(eF.StartNode) || eL.StartNode.Equals2D(eF.EndNode))
                    junction = eL.StartNode;

                bool degreeOk = true;
                if (junction != null) {
                    var node = GetNode(junction);
                    if (node != null && node.Degree > 2) degreeOk = false;
                }

                if (junction != null && degreeOk) {
                    var merged = result[^1].ConstituentEdges.Concat(result[0].ConstituentEdges).ToList();
                    var stitched = BuildMergedEdge(merged);
                    result[^1] = stitched;
                    result.RemoveAt(0);
                }
            }

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
                        // Find the shared endpoint between the last NetworkEdge of mergedChain[0]
                        // and the first NetworkEdge of mergedChain[1]. We cannot assume canonical
                        // edge direction matches the source traversal direction, so we compare all
                        // four endpoint combinations to find the one coordinate in common.
                        var e0 = mergedChain[0].ConstituentEdges.Last();
                        var e1 = mergedChain[1].ConstituentEdges.First();
                        if (e0.EndNode.Equals2D(e1.StartNode) || e0.EndNode.Equals2D(e1.EndNode))
                            rotationNode = e0.EndNode;
                        else if (e0.StartNode.Equals2D(e1.StartNode) || e0.StartNode.Equals2D(e1.EndNode))
                            rotationNode = e0.StartNode;
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
            // We want the first position i where targetNode is the NODE BETWEEN chain[i-1]
            // and chain[i] — i.e. where the path arrives at targetNode from chain[i-1] and
            // then DEPARTS via chain[i]. We detect this by checking that targetNode matches
            // whichever endpoint of chain[i] faces the incoming direction from chain[i-1].
            for (int i = 1; i < chain.Count; i++) {
                var prev = chain[i - 1];
                var curr = chain[i];
                // Determine which endpoint of prev is the "exit" node
                // (the node shared with curr)
                Coordinate prevExit;
                if (curr.StartNode.Equals2D(prev.StartNode) || curr.EndNode.Equals2D(prev.StartNode))
                    prevExit = prev.StartNode;
                else
                    prevExit = prev.EndNode;

                if (prevExit.Equals2D(targetNode)) {
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
        // Robust assembly — sequential ordered walk
        // -------------------------------------------------------------------

        /// <summary>
        /// Assembles a <see cref="LinearRing"/> from an unordered list of <see cref="MergedEdge"/>
        /// objects. The edge list from BuildEdgeIndex is already in traversal order;
        /// this method orients each edge (forward or backward) by matching endpoints.
        /// </summary>
        public LinearRing AssembleLinearRing(List<MergedEdge> edges) {
            var start = edges.Count > 0 ? edges[0].Geometry.Coordinates[0] : null;
            var (coords, _) = AssembleOrderedEdges(edges, isRing: true, startCoord: start);
            if (coords.Length == 0) return _factory.CreateLinearRing(Array.Empty<Coordinate>());
            return _factory.CreateLinearRing(coords);
        }

        /// <summary>
        /// Assembles a <see cref="LineString"/> from an unordered list of <see cref="MergedEdge"/>
        /// objects. The edge list from BuildEdgeIndex is already in traversal order;
        /// this method orients each edge (forward or backward) by matching endpoints.
        /// </summary>
        public LineString AssembleLineString(List<MergedEdge> edges) {
            var start = edges.Count > 0 ? edges[0].Geometry.Coordinates[0] : null;
            var (coords, _) = AssembleOrderedEdges(edges, isRing: false, startCoord: start);
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
            var start = edges.Count > 0 ? edges[0].Geometry.Coordinates[0] : null;
            var (_, refs) = AssembleOrderedEdges(edges, isRing: false, startCoord: start);
            return refs;
        }

        /// <summary>
        /// Returns the same ordered, correctly-oriented edge list that
        /// <see cref="AssembleLinearRing"/> uses internally to build its geometry. See
        /// <see cref="AssembleEdgeOrderForLineString"/> for details on the returned references.
        /// </summary>
        public List<EdgeReference> AssembleEdgeOrderForLinearRing(List<MergedEdge> edges) {
            var start = edges.Count > 0 ? edges[0].Geometry.Coordinates[0] : null;
            var (_, refs) = AssembleOrderedEdges(edges, isRing: true, startCoord: start);
            return refs;
        }

        /// <summary>
        /// <summary>
        /// Assembles an ordered, oriented sequence of <see cref="EdgeReference"/> objects and
        /// their concatenated coordinates from a list of <see cref="MergedEdge"/> objects.
        ///
        /// The <paramref name="edges"/> list from <see cref="BuildEdgeIndex"/> is already in
        /// the correct traversal order for the source geometry. This method walks that list
        /// sequentially: for each edge it determines whether to traverse forward or backward
        /// by matching the current path endpoint against the edge's two geometry endpoints.
        /// This is O(n) and always produces the correct order regardless of graph topology.
        /// </summary>
        private (Coordinate[] Coords, List<EdgeReference> Refs) AssembleOrderedEdges(
            List<MergedEdge> edges, bool isRing, Coordinate? startCoord = null) {

            if (edges.Count == 0) return (Array.Empty<Coordinate>(), new List<EdgeReference>());

            double tol = _snapTolerance;
            CoordinateKey Snap(Coordinate c) => new CoordinateKey(Canonicalize(c), tol);

            // For rings only: prune dangles. A dangle is an edge chain that sticks out
            // of the ring and comes straight back (e.g. a source doubling back over a
            // tiny spur). In a valid ring every node has degree 2 within the edge set;
            // a dangle tip has degree 1. Iteratively remove edges with a degree-1
            // endpoint until none remain — this peels multi-edge dangle chains too.
            // Open linestrings are NOT pruned: their two path endpoints are degree-1
            // by definition.
            if (isRing && edges.Count > 1) {
                var degree = new Dictionary<CoordinateKey, int>();
                void Bump(CoordinateKey k, int d) {
                    degree.TryGetValue(k, out var v);
                    degree[k] = v + d;
                }
                foreach (var e in edges) {
                    var cs = e.Geometry.Coordinates;
                    Bump(Snap(cs[0]), 1);
                    Bump(Snap(cs[^1]), 1);
                }

                // A real dangle spur is short — a source doubling back on itself for a
                // few segments. A genuine break in ring continuity (the chain fails to
                // close) also produces degree-1 tips, but the "dangle" there can be the
                // entire chain. Without a length guard the loop can't tell these apart
                // and will happily eat a long, legitimately-connected chain down to
                // nothing. Only edges short enough to plausibly be a spur are eligible
                // for removal; anything longer is left in place even if it ends up
                // degree-1, so a real gap shows up as a visible open path instead of
                // silently vanishing.
                double dangleLengthGuard = 10 * _dedupeRadius;

                var pruned = new List<MergedEdge>(edges);
                bool removedAny = true;
                while (removedAny && pruned.Count > 1) {
                    removedAny = false;
                    for (int i = pruned.Count - 1; i >= 0; i--) {
                        var cs = pruned[i].Geometry.Coordinates;
                        var kS = Snap(cs[0]);
                        var kE = Snap(cs[^1]);
                        // Self-loops (kS==kE) are never dangles.
                        if (kS.Equals(kE)) continue;
                        if ((degree[kS] == 1 || degree[kE] == 1) &&
                            pruned[i].Geometry.Length <= dangleLengthGuard) {
                            Bump(kS, -1);
                            Bump(kE, -1);
                            pruned.RemoveAt(i);
                            removedAny = true;
                        }
                    }
                }
                edges = pruned;
                if (edges.Count == 0)
                    return (Array.Empty<Coordinate>(), new List<EdgeReference>());
            }

            var result = new List<Coordinate>();
            var refs = new List<EdgeReference>(edges.Count);
            Coordinate? pathEnd = null;

            for (int ei = 0; ei < edges.Count; ei++) {
                var edge = edges[ei];
                var cs = edge.Geometry.Coordinates;
                var kS = Snap(cs[0]);
                var kE = Snap(cs[^1]);

                bool fwd;
                if (pathEnd == null) {
                    // First edge: use startCoord hint, or look ahead to the next edge
                    // to determine which end connects forward.
                    if (startCoord != null && Snap(startCoord).Equals(kE) && !Snap(startCoord).Equals(kS)) {
                        fwd = false;
                    }
                    else if (ei + 1 < edges.Count) {
                        var nextCs = edges[ei + 1].Geometry.Coordinates;
                        var nkS = Snap(nextCs[0]);
                        var nkE = Snap(nextCs[^1]);
                        // If our EndNode connects to either endpoint of the next edge, go forward.
                        // If our StartNode connects instead, go backward.
                        if (nkS.Equals(kE) || nkE.Equals(kE)) fwd = true;
                        else if (nkS.Equals(kS) || nkE.Equals(kS)) fwd = false;
                        else fwd = true;
                    }
                    else {
                        fwd = true;
                    }
                }
                else {
                    var kPrev = Snap(pathEnd);
                    if (kPrev.Equals(kS)) fwd = true;
                    else if (kPrev.Equals(kE)) fwd = false;
                    else {
                        // pathEnd matches neither endpoint — the MergedEdge geometry was
                        // built in canonical (lexicographic) direction which may differ from
                        // the source traversal direction. Look ahead to the next edge to
                        // determine which end of this edge faces forward.
                        if (ei + 1 < edges.Count) {
                            var nextCs = edges[ei + 1].Geometry.Coordinates;
                            var nkS = Snap(nextCs[0]);
                            var nkE = Snap(nextCs[^1]);
                            if (nkS.Equals(kE) || nkE.Equals(kE)) fwd = true;
                            else if (nkS.Equals(kS) || nkE.Equals(kS)) fwd = false;
                            else fwd = true;
                        }
                        else {
                            fwd = true;
                        }
                    }
                }

                var oriented = fwd ? cs : cs.Reverse().ToArray();
                int skip = result.Count > 0 && result[^1].Equals2D(oriented[0]) ? 1 : 0;
                result.AddRange(oriented.Skip(skip));
                refs.Add(new EdgeReference { Edge = edge, Forward = fwd });
                pathEnd = oriented[^1];
            }

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