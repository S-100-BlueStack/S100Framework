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

        // Canonical coordinate map built during Build(); used by Canonicalize()
        // so every coordinate emitted anywhere (edges, merged chains, full
        // geometries) is forced through the SAME tolerance-based union-find
        // result, regardless of which source/edge/direction produced it.
        private Dictionary<CoordinateKey, Coordinate> _canonicalMap;

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
            _canonicalMap = canonical; // keep available for Canonicalize() after Build()

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
        private int ExtractFromRing(Coordinate[] coords, int sourceId, List<RawSegment> result, int segId, bool isRing = true) {
            // REMOVED: RemoveSpikes(coords, spikeTolerance: _snapTolerance);
            // RemoveSpikes ran BEFORE the canonical coordinate map existed, and its
            // "keep first, drop near-duplicates within tolerance" logic is direction-
            // dependent: which point survives depends on which order the source
            // traverses them. Two sources crossing the same near-duplicate vertex
            // pair in opposite winding directions would each keep a DIFFERENT one
            // of the two points, so they could never be reconciled later even by
            // the canonical map (since the map only sees whichever point survived
            // RemoveSpikes for each source — it can't union two points if one of
            // them was already deleted).
            //
            // The canonical-coordinate-map + degenerate-segment-removal in Build()
            // already handles near-duplicate vertices correctly and deterministically
            // (order-independent union-find), so RemoveSpikes is redundant and
            // actively harmful. Removing it lets every coordinate, including both
            // members of a near-duplicate pair, survive into raw segments, where
            // they get canonicalized consistently regardless of source direction.

            // For rings, strip the closing duplicate BEFORE snapping so the
            // re-close logic below is always the one that adds it back.
            if (isRing && coords.Length > 1) {
                var snapFirst = SnapToGrid(coords[0]);
                var snapLast = SnapToGrid(coords[^1]);
                if (snapFirst.Equals2D(snapLast))
                    coords = coords[..^1];
            }

            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1]))
                    snapped.Add(sc);
            }

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
        /// Routes a coordinate through grid-snapping AND the canonical
        /// coordinate map (union-find result from BuildCanonicalCoordinateMap),
        /// so that two distinct-but-near-duplicate source vertices (e.g. a
        /// digitizing pair 1 ULP apart at tolerance scale) always resolve to
        /// the exact same emitted coordinate, regardless of which source,
        /// edge, or traversal direction produced it. This is the function
        /// that should be used any time a coordinate is being EMITTED
        /// (chain stitching, full-geometry reconstruction) rather than the
        /// raw SnapToGrid, which only normalizes to the grid and does not
        /// resolve cross-source near-duplicate pairs.
        /// </summary>
        private Coordinate Canonicalize(Coordinate c) {
            var snapped = SnapToGrid(c);
            if (_canonicalMap == null) return snapped;

            var key = new CoordinateKey(snapped, _snapTolerance);
            return _canonicalMap.TryGetValue(key, out var canon) ? canon : snapped;
        }

        /// <summary>
        /// Builds a canonical coordinate map, but only merges a point with its
        /// IMMEDIATE NEIGHBORS in segment-adjacency terms — i.e. only collapses
        /// duplicate/near-duplicate vertices that are CONSECUTIVE in some source
        /// geometry's coordinate sequence, never just "spatially nearby."
        /// This avoids collapsing genuinely close-but-unrelated vertices in thin
        /// sliver polygons while still fixing digitizing dangles at shared edges.
        ///
        /// Union() always attaches the LARGER CoordinateKey under the SMALLER
        /// one (by CompareTo), so the resulting canonical coordinate for a
        /// given union-find group is deterministic and does NOT depend on
        /// which segment/source happened to be processed first. Without this,
        /// the same logical point could canonicalize differently depending on
        /// iteration order, producing seams where two sources disagree on the
        /// exact coordinate of a shared near-duplicate vertex pair.
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
                if (ra.Equals(rb)) return;
                // Deterministic tie-break: always attach the larger key to the
                // smaller one so the result never depends on segment/source
                // processing order.
                if (ra.CompareTo(rb) < 0)
                    parent[rb] = ra;
                else
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

            // Pick canonical value deterministically: use the ROOT key's own
            // snapped coordinate (not "whichever raw value happened to be
            // cellValue[root]" based on insertion order).
            var result = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var key in cellValue.Keys) {
                var root = Find(key);
                result[key] = cellValue[root];
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
            if (c0.Equals2D(c1, _snapTolerance)) return; // degenerate

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

            // Did THIS source traverse edge.StartNode -> edge.EndNode?
            //
            // c0/c1 are the coordinates passed in for THIS source's segment,
            // in the order the source actually traverses them (c0 -> c1).
            // sourceReversed indicates whether, BEFORE canonical direction
            // swap, the source had originally digitized P1->P0 (i.e. c0/c1
            // here are already the canonicalized RawSegment.P0/P1, which may
            // or may not match the source's true traversal direction).
            // needsSwap indicates whether the EDGE's own StartNode/EndNode
            // were swapped relative to c0/c1 (StartNode=c1,EndNode=c0 when
            // needsSwap is true).
            //
            // Combining these: the source goes StartNode->EndNode exactly
            // when sourceReversed and needsSwap agree (both true or both
            // false) — i.e. an XNOR, not a plain negation of either flag
            // alone.
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
        /// A reference to a network edge with a traversal direction.
        /// </summary>
        public class EdgeReference
        {
            public MergedEdge Edge { get; init; }
            public bool Forward { get; init; } // true = use Edge.Geometry as-is

            public LineString OrientedGeometry => Forward
                ? Edge.Geometry
                : (LineString)Edge.Geometry.Reverse();
        }

        /// <summary>
        /// Returns all unique merged edges in the network (equivalent to
        /// MergeSharedEdges but also including private edges for every source),
        /// plus for each source an ordered list of EdgeReferences that
        /// reconstructs its original geometry end-to-end.
        /// </summary>
        public (List<MergedEdge> AllEdges, Dictionary<int, List<EdgeReference>> SourceRefs)
            BuildEdgeIndex() {

            // Step 1: for every network edge, determine its canonical MergedEdge.
            // Two NetworkEdges belong to the same canonical MergedEdge iff they have
            // the same SourceGeometryIds AND are adjacent in EVERY source that
            // contains them. The only reliable way to determine this is: they must
            // be consecutive in ALL sources' full chains.
            // Simplification: group NetworkEdges by SourceGeometryIds signature.
            // Within each group, find maximal consecutive runs across any one source.

            // Map sourceId signature -> list of NetworkEdge ids with that signature
            var sigToEdges = new Dictionary<string, List<int>>();
            foreach (var edge in _edges) {
                var sig = string.Join(",", edge.SourceGeometryIds.OrderBy(x => x));
                if (!sigToEdges.TryGetValue(sig, out var list))
                    sigToEdges[sig] = list = new List<int>();
                list.Add(edge.Id);
            }

            // For each signature group, split into maximal runs that are consecutive
            // in some source's chain. Use the numerically smallest source as reference.
            var networkEdgeToMerged = new Dictionary<int, MergedEdge>();
            var allMergedEdges = new List<MergedEdge>();

            foreach (var (sig, edgeIds) in sigToEdges) {
                // Pick reference source = smallest id in this sig's source set
                var refSourceId = edgeIds[0] < _edges.Count
                    ? _edges[edgeIds[0]].SourceGeometryIds.Min()
                    : 0;

                // Get the full chain for this source and find positions of these edges
                var refChain = GetFullEdgeChainFor(refSourceId);
                var posInChain = new Dictionary<int, int>();
                for (int i = 0; i < refChain.Count; i++)
                    posInChain[refChain[i].Id] = i;

                // Sort this group's edges by position in the reference chain
                var edgeIdSet = edgeIds.ToHashSet();
                var sortedIds = edgeIds
                    .Where(id => posInChain.ContainsKey(id))
                    .OrderBy(id => posInChain[id])
                    .ToList();

                // Split into consecutive runs
                if (sortedIds.Count == 0) continue;
                var currentRun = new List<NetworkEdge> { _edges[sortedIds[0]] };

                for (int i = 1; i < sortedIds.Count; i++) {
                    int prevPos = posInChain[sortedIds[i - 1]];
                    int currPos = posInChain[sortedIds[i]];
                    if (currPos == prevPos + 1) {
                        currentRun.Add(_edges[sortedIds[i]]);
                    }
                    else {
                        // Flush run
                        var merged = BuildMergedEdgeFromChain(currentRun, refSourceId);
                        allMergedEdges.Add(merged);
                        foreach (var e in currentRun)
                            networkEdgeToMerged[e.Id] = merged;
                        currentRun = new List<NetworkEdge> { _edges[sortedIds[i]] };
                    }
                }
                // Flush last run
                var lastMerged = BuildMergedEdgeFromChain(currentRun, refSourceId);
                allMergedEdges.Add(lastMerged);
                foreach (var e in currentRun)
                    networkEdgeToMerged[e.Id] = lastMerged;
            }

            // Step 2: build EdgeReference list per source
            var sourceRefs = new Dictionary<int, List<EdgeReference>>();
            foreach (var src in _sources) {
                var chain = GetFullEdgeChainFor(src.Id);
                var refs = new List<EdgeReference>();
                MergedEdge lastMerged = null;

                foreach (var netEdge in chain) {
                    if (!networkEdgeToMerged.TryGetValue(netEdge.Id, out var merged)) continue;
                    if (ReferenceEquals(merged, lastMerged)) continue;

                    // Direction: does this source traverse the canonical merged edge forward?
                    var canonFirst = merged.Geometry.Coordinates[0];
                    var srcEntryNode = netEdge.SourceOrientation.TryGetValue(src.Id, out bool fwd)
                        ? (fwd ? netEdge.StartNode : netEdge.EndNode)
                        : netEdge.StartNode;
                    bool forward = srcEntryNode.Equals2D(canonFirst, _snapTolerance);

                    refs.Add(new EdgeReference { Edge = merged, Forward = forward });
                    lastMerged = merged;
                }

                sourceRefs[src.Id] = refs;
            }

            return (allMergedEdges, sourceRefs);
        }

        private MergedEdge BuildMergedEdgeFromChain(List<NetworkEdge> edges, int refSourceId) {
            // Orient using the reference source's traversal direction
            var dirs = new List<bool>();
            for (int i = 0; i < edges.Count; i++) {
                bool fwd;
                if (i == 0) {
                    fwd = edges.Count == 1 ||
                          edges[0].EndNode.Equals2D(edges[1].StartNode, _snapTolerance) ||
                          edges[0].EndNode.Equals2D(edges[1].EndNode, _snapTolerance);
                }
                else {
                    var prevFwd = dirs[i - 1];
                    var prevExit = prevFwd ? edges[i - 1].EndNode : edges[i - 1].StartNode;
                    fwd = edges[i].StartNode.Equals2D(prevExit, _snapTolerance);
                }
                dirs.Add(fwd);
            }
            var coords = StitchEdges(edges, dirs);
            return new MergedEdge {
                Geometry = _factory.CreateLineString(coords),
                SourceGeometryIds = edges[0].SourceGeometryIds,
                ConstituentEdges = edges,
            };
        }

        private Coordinate[] StitchEdges(List<NetworkEdge> edges, List<bool> dirs) {
            var coords = new List<Coordinate>();
            for (int i = 0; i < edges.Count; i++) {
                var ec = dirs[i] ? edges[i].Geometry.Coordinates
                                 : edges[i].Geometry.Coordinates.Reverse().ToArray();
                if (coords.Count == 0) coords.AddRange(ec.Select(Canonicalize));
                else coords.AddRange(ec.Skip(1).Select(Canonicalize));
            }
            return coords.ToArray();
        }

        /// <summary>
        /// Reconstructs the full geometry of a source from its EdgeReferences.
        /// </summary>
        public LineString ReconstructGeometry(List<EdgeReference> refs, int sourceId) {
            if (refs.Count == 0) return null;

            var coords = new List<Coordinate>();

            foreach (var r in refs) {
                var edgeCoords = r.OrientedGeometry.Coordinates;
                if (coords.Count == 0) {
                    coords.AddRange(edgeCoords.Select(Canonicalize));
                }
                else {
                    coords.AddRange(edgeCoords.Skip(1).Select(Canonicalize));
                }
            }

            var source = _sources[sourceId];
            bool isRing = source.Kind == GeometryKind.Polygon
                       || source.Geometry is LinearRing;

            if (isRing && coords.Count > 1)
                coords[^1] = new Coordinate(coords[0].X, coords[0].Y);

            return _factory.CreateLineString(coords.ToArray());
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
        /// Stitches an ordered chain of edges into a single coordinate array.
        /// Relies ONLY on connectivity between consecutive edges (StartNode/
        /// EndNode matching the previous edge's actual last emitted
        /// coordinate), NOT on SourceOrientation — the chain must already be
        /// in correct topological order (consecutive edges share a node),
        /// which GetFullEdgeChainFor / WalkFullChain guarantee. Every emitted
        /// coordinate is routed through Canonicalize() so that near-duplicate
        /// source vertices (e.g. two original points 1 grid-cell apart that
        /// the canonical map already unions) always resolve to the exact same
        /// output coordinate, regardless of which edge or traversal direction
        /// produced it.
        /// </summary>
        private Coordinate[] ChainToCoordinates(List<NetworkEdge> chain) {
            if (chain.Count == 0) return Array.Empty<Coordinate>();
            if (chain.Count == 1)
                return chain[0].Geometry.Coordinates.Select(Canonicalize).ToArray();

            var coords = new List<Coordinate>();

            // Determine the orientation of edge[0] by checking how it connects to edge[1]
            var e0 = chain[0];
            var e1 = chain[1];

            bool e0Forward; // true = traverse StartNode->EndNode
            if (e0.EndNode.Equals2D(e1.StartNode, _snapTolerance) ||
                e0.EndNode.Equals2D(e1.EndNode, _snapTolerance)) {
                e0Forward = true;   // e0's EndNode connects to e1 -> go forward
            }
            else if (e0.StartNode.Equals2D(e1.StartNode, _snapTolerance) ||
                     e0.StartNode.Equals2D(e1.EndNode, _snapTolerance)) {
                e0Forward = false;  // e0's StartNode connects to e1 -> go backward (reverse e0)
            }
            else {
                // Edges don't actually connect — chain is broken. Default forward.
                e0Forward = true;
            }

            var firstCoords = e0Forward
                ? e0.Geometry.Coordinates
                : e0.Geometry.Coordinates.Reverse().ToArray();

            foreach (var c in firstCoords)
                coords.Add(Canonicalize(c));

            // Walk the rest of the chain: each edge must start where the previous
            // one ended (after orienting it that way).
            for (int i = 1; i < chain.Count; i++) {
                var edge = chain[i];
                var prevEnd = coords[^1];

                bool forward;
                if (edge.StartNode.Equals2D(prevEnd, _snapTolerance)) {
                    forward = true;
                }
                else if (edge.EndNode.Equals2D(prevEnd, _snapTolerance)) {
                    forward = false;
                }
                else {
                    // Edge doesn't connect to previous end within tolerance.
                    // Fall back to whichever endpoint is closer (shouldn't normally happen
                    // if the chain was built correctly).
                    forward = edge.StartNode.Distance(prevEnd) <= edge.EndNode.Distance(prevEnd);
                }

                var edgeCoords = forward
                    ? edge.Geometry.Coordinates
                    : edge.Geometry.Coordinates.Reverse().ToArray();

                // Skip the first coordinate — it duplicates prevEnd
                foreach (var c in edgeCoords.Skip(1))
                    coords.Add(Canonicalize(c));
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
        /// edges and walks them end-to-end. Internally built from
        /// GetMergedEdgeChainFor so the two stay consistent.
        /// </summary>
        public LineString GetFullGeometryFor(int sourceId) {
            var mergedEdges = GetMergedEdgeChainFor(sourceId);
            if (mergedEdges.Count == 0) return null;

            var coords = new List<Coordinate>();

            foreach (var merged in mergedEdges) {
                var mergedCoords = merged.Geometry.Coordinates;
                if (coords.Count == 0) {
                    coords.AddRange(mergedCoords.Select(Canonicalize));
                }
                else {
                    // Skip first coord - it duplicates the last coord we already have
                    coords.AddRange(mergedCoords.Skip(1).Select(Canonicalize));
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

            // Find the seed edge by scanning the source's original coordinate
            // sequence for the first node that appears in this source's edge set.
            // This guarantees a consistent starting point regardless of insertion
            // order, so the chain always begins at the same place and consecutive
            // shared edges are never split across the seed boundary.
            var source = _sources[sourceId];
            NetworkEdge seed = null;
            foreach (var coord in source.Geometry.Coordinates) {
                var key = new CoordinateKey(Canonicalize(coord), _snapTolerance);
                if (!adjacency.TryGetValue(key, out var edgesAtNode)) continue;
                // Find an edge at this node whose SourceOrientation entry node
                // matches — i.e. this source ENTERS this node, not exits from it
                foreach (var candidate in edgesAtNode) {
                    if (!candidate.SourceOrientation.TryGetValue(sourceId, out bool fwd)) continue;
                    var entryNode = fwd ? candidate.StartNode : candidate.EndNode;
                    if (entryNode.Equals2D(Canonicalize(coord), _snapTolerance)) {
                        seed = candidate;
                        break;
                    }
                }
                if (seed != null) break;
            }
            seed ??= edges.First(); // fallback

            var visited = new HashSet<int>();
            return WalkFullChainIgnoringSourceSets(seed, edges, adjacency, visited);
        }

        /// <summary>
        /// Same traversal as WalkFullChain, but WITHOUT the SourceGeometryIds.SetEquals
        /// check — it walks purely on graph degree (within this source's own edge
        /// set), so a node that's also shared with other geometries doesn't break
        /// the chain, as long as within THIS source's edges the node still has
        /// degree 2. Detects ring closure during the forward walk and skips the
        /// backward walk entirely in that case, to avoid double-counting edges.
        /// </summary>
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
            var chain = GetFullEdgeChainFor(sourceId);
            if (chain.Count == 0) return new List<MergedEdge>();

            var result = new List<MergedEdge>();
            var currentGroup = new List<NetworkEdge> { chain[0] };

            for (int i = 1; i < chain.Count; i++) {
                var edge = chain[i];
                var prev = currentGroup[^1];

                if (edge.SourceGeometryIds.SetEquals(prev.SourceGeometryIds)) {
                    // Same source set — extend the current group
                    currentGroup.Add(edge);
                }
                else {
                    // Source set changed — flush current group as a MergedEdge
                    result.Add(BuildMergedEdge(currentGroup));
                    currentGroup = new List<NetworkEdge> { edge };
                }
            }

            // Flush the last group
            if (currentGroup.Count > 0)
                result.Add(BuildMergedEdge(currentGroup));

            return result;
        }

        private MergedEdge BuildMergedEdge(List<NetworkEdge> edges) {
            var coords = ChainToCoordinates(edges);
            return new MergedEdge {
                Geometry = _factory.CreateLineString(coords),
                SourceGeometryIds = edges.SelectMany(e => e.SourceGeometryIds)
                                         .Distinct()
                                         .ToHashSet(),
                ConstituentEdges = edges.ToList(),
            };
        }

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