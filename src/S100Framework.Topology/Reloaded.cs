using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using S100FC.Topology;
using System;
using System.Collections.Generic;
using System.Text;

namespace S100Framework.Topology
{
    using S100Framework.Topology.Internal;
    using System.Net;

    public interface IMatrixReloaded : IMatrix {
        GeometryFactory Factory { get; }
    }

    public class Reloaded : ITopologyBuilder, IMatrixReloaded
    {
        public static GeometryFactory Factory { get; set; } = new GeometryFactory(new PrecisionModel(100000000), srid: 4326);

        private Action<(LineString lineString, string message)[]>? _interceptor;

        private MixedTopologyNetwork _mixedTopologyNetwork;

        private Dictionary<string, int> _featureMapper = new();

        protected Reloaded() {
            //  Default protected constructor            

            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.000000001);
        }

        public static IMatrixReloaded CreateMatrix(Action<(LineString lineString, string message)[]>? interceptor = default) {
            return new Reloaded() {
                _interceptor = interceptor,
            };
        }

        GeometryFactory IMatrixReloaded.Factory => Reloaded.Factory;

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, true);

        ITopologyBuilder ITopologyBuilder.AddNavigationalFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, false);

        IMatrix ITopologyBuilder.BuildTopology() {
            this._mixedTopologyNetwork.Build();

            return (IMatrix)this;
        }

        IEnumerable<CurveFeature> IMatrix.Curves => throw new NotImplementedException();

        IEnumerable<CompositeCurveFeature> IMatrix.CompositeCurves => throw new NotImplementedException();

        IEnumerable<SurfaceFeature> IMatrix.Surfaces => throw new NotImplementedException();

        IDictionary<string, string> IMatrix.MappingFOID => throw new NotImplementedException();



        private ITopologyBuilder AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves, bool isTopology) {
            foreach (var surface in surfaces) {
                Func<NetTopologySuite.Geometries.Polygon> createPolygon = surface.InteriorRings.Any() switch {
                    true => () => {
                        return Factory.CreatePolygon(Factory.CreateLinearRing(surface.ExteriorRing.Coordinates), [.. surface.InteriorRings.Select(e => Factory.CreateLinearRing(e.Coordinates))]);
                    }
                    ,
                    false => () => {
                        return Factory.CreatePolygon(Factory.CreateLinearRing(surface.ExteriorRing.Coordinates), []);
                    }
                    ,
                };
                var polygon = createPolygon();

                var id = this._mixedTopologyNetwork.AddPolygon(polygon);
                this._featureMapper.Add(surface.UID, id);
            }

            foreach (var curve in curves) {
                var id = this._mixedTopologyNetwork.AddLineString(curve.LineString);
                this._featureMapper.Add(curve.UID, id);
            }

            return this;
        }
    }
}

namespace S100Framework.Topology.Internal
{
    /// <summary>
    /// A registered geometry in the network — either a Polygon or a LineString.
    /// </summary>
    internal class NetworkGeometry
    {
        public int Id { get; init; }
        public NetTopologySuite.Geometries.Geometry? Geometry { get; init; } = default;       // Original geometry
        public GeometryKind Kind { get; init; }
    }

    internal enum GeometryKind { Polygon, LineString }

    /// <summary>
    /// A directed edge in the planar network.
    /// </summary>
    internal class NetworkEdge
    {
        public int Id { get; init; }
        public LineString? Geometry { get; init; } = default;
        public Coordinate? StartNode { get; init; } = default;
        public Coordinate? EndNode { get; init; } = default;

        // Which registered geometries contributed this edge
        public HashSet<int> SourceGeometryIds { get; } = new();

        // Is this edge shared between two or more geometries?
        public bool IsShared => SourceGeometryIds.Count > 1;
    }

    /// <summary>
    /// A node in the planar network.
    /// </summary>
    internal class NetworkNode
    {
        public Coordinate? Coordinate { get; init; } = default;

        // All edges incident to this node
        public List<NetworkEdge> Edges { get; } = new();

        public int Degree => Edges.Count;
    }

    internal class MixedTopologyNetwork
    {
        private readonly GeometryFactory _factory;
        private readonly double _snapTolerance;

        private readonly List<NetworkGeometry> _sources = new();
        private readonly List<NetworkEdge> _edges = new();
        private readonly Dictionary<CoordinateKey, NetworkNode> _nodes = new();
        private readonly Dictionary<int, List<NetworkEdge>> _edgesBySource = new();
        private STRtree<NetworkEdge>? _edgeIndex = default;

        public int Sources => _sources.Count;

        public IReadOnlyList<NetworkEdge> Edges => _edges;
        public IReadOnlyCollection<NetworkNode> Nodes => _nodes.Values;

        public MixedTopologyNetwork(GeometryFactory factory, double snapTolerance = 0.0001) {
            _factory = factory;
            _snapTolerance = snapTolerance;
        }

        public int AddPolygon(NetTopologySuite.Geometries.Polygon p) => Register(p, GeometryKind.Polygon);
        public int AddLineString(LineString l) => Register(l, GeometryKind.LineString);
        public void AddPolygons(IEnumerable<NetTopologySuite.Geometries.Polygon> ps) { foreach (var p in ps) AddPolygon(p); }
        public void AddLineStrings(IEnumerable<LineString> ls) { foreach (var l in ls) AddLineString(l); }

        private int Register(NetTopologySuite.Geometries.Geometry geom, GeometryKind kind) {
            int id = _sources.Count;
            _sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------------
        // Build — spatially-local noding only
        // -------------------------------------------------------------------------

        public void Build() {
            // Step 1: Extract raw segments per source — no LineString allocation,
            //         just coordinate pairs + source id
            var rawSegments = ExtractRawSegments();

            // Step 2: Build a spatial index over raw segments
            var segIndex = new STRtree<RawSegment>();
            foreach (var seg in rawSegments)
                segIndex.Insert(seg.Envelope, seg);

            // Step 3: For each segment, query only nearby candidates and compute
            //         intersection points — collect split coordinates per segment
            var splitPoints = new Dictionary<int, SortedSet<SplitPoint>>(rawSegments.Count);
            for (int i = 0; i < rawSegments.Count; i++)
                splitPoints[i] = new SortedSet<SplitPoint>(SplitPoint.Comparer);

            var li = new RobustLineIntersector();

            // Expand each query envelope by tolerance to catch near-misses
            foreach (var seg in rawSegments) {
                var queryEnv = seg.Envelope.Copy();
                queryEnv.ExpandBy(_snapTolerance);

                var candidates = segIndex.Query(queryEnv);
                foreach (var other in candidates) {
                    if (other.SegId >= seg.SegId) continue; // process each pair once

                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;

                    for (int k = 0; k < li.IntersectionNum; k++) {
                        var pt = li.GetIntersection(k);

                        // Snap to endpoint if within tolerance
                        pt = SnapToEndpoint(pt, seg, other);

                        splitPoints[seg.SegId].Add(new SplitPoint(pt, seg.P0));
                        splitPoints[other.SegId].Add(new SplitPoint(pt, other.P0));
                    }
                }
            }

            // Step 4: Split each raw segment at its intersection points,
            //         then insert into the graph
            var edgeMap = new Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge>();

            foreach (var seg in rawSegments) {
                var splits = splitPoints[seg.SegId];

                // Build split coordinate list: P0 → split points (ordered) → P1
                var coords = new List<Coordinate>(splits.Count + 2) { seg.P0 };
                foreach (var sp in splits) {
                    // Skip duplicates of endpoints
                    if (!sp.Coord.Equals2D(coords[^1], _snapTolerance))
                        coords.Add(sp.Coord);
                }
                if (!seg.P1.Equals2D(coords[^1], _snapTolerance))
                    coords.Add(seg.P1);

                // Insert each sub-segment as a network edge
                for (int i = 0; i < coords.Count - 1; i++)
                    InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
            }

            // Step 5: Spatial index on final edges
            _edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in _edges)
                _edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }

        // -------------------------------------------------------------------------
        // Segment extraction — no geometry object allocation per segment
        // -------------------------------------------------------------------------

        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(EstimateSegmentCount());
            int segId = 0;

            foreach (var src in _sources) {
                if (src.Kind == GeometryKind.Polygon) {
                    var poly = (NetTopologySuite.Geometries.Polygon)src.Geometry;
                    segId = ExtractFromRing(poly.ExteriorRing.Coordinates,
                        src.Id, result, segId);
                    foreach (var hole in poly.InteriorRings)
                        segId = ExtractFromRing(hole.Coordinates,
                            src.Id, result, segId);
                }
                else {
                    segId = ExtractFromRing(src.Geometry.Coordinates,
                        src.Id, result, segId);
                }
            }

            return result;
        }

        private static int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId) {
            for (int i = 0; i < coords.Length - 1; i++) {
                result.Add(new RawSegment(segId++, coords[i], coords[i + 1], sourceId));
            }
            return segId;
        }

        private int EstimateSegmentCount()
            => _sources.Sum(s => s.Geometry.NumPoints);

        // -------------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------------

        private void InsertEdge(
            Coordinate c0, Coordinate c1, int sourceId,
            Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {
            if (c0.Equals2D(c1, _snapTolerance)) return; // degenerate

            var k0 = new CoordinateKey(c0, _snapTolerance);
            var k1 = new CoordinateKey(c1, _snapTolerance);
            var key = k0.CompareTo(k1) <= 0 ? (k0, k1) : (k1, k0);

            if (!edgeMap.TryGetValue(key, out var edge)) {
                var ls = _factory.CreateLineString(new[] { c0, c1 });
                edge = new NetworkEdge {
                    Id = _edges.Count,
                    Geometry = ls,
                    StartNode = c0,
                    EndNode = c1,
                };
                _edges.Add(edge);
                edgeMap[key] = edge;

                GetOrCreateNode(c0).Edges.Add(edge);
                GetOrCreateNode(c1).Edges.Add(edge);
            }

            edge.SourceGeometryIds.Add(sourceId);

            if (!_edgesBySource.TryGetValue(sourceId, out var list))
                _edgesBySource[sourceId] = list = new List<NetworkEdge>();

            // Avoid duplicates if the same source produced the same sub-segment
            if (!list.Contains(edge))
                list.Add(edge);
        }

        private NetworkNode GetOrCreateNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            if (!_nodes.TryGetValue(key, out var node))
                _nodes[key] = node = new NetworkNode { Coordinate = coord };
            return node;
        }

        // -------------------------------------------------------------------------
        // Snapping helper
        // -------------------------------------------------------------------------

        private Coordinate SnapToEndpoint(
            Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 }) {
                if (pt.Distance(ep) <= _snapTolerance)
                    return ep;
            }
            return pt;
        }

        // -------------------------------------------------------------------------
        // Query API (unchanged from before)
        // -------------------------------------------------------------------------

        public IReadOnlyList<NetworkEdge> GetEdgesFor(int sourceId)
            => _edgesBySource.TryGetValue(sourceId, out var l)
                ? l : Array.Empty<NetworkEdge>();

        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => GetEdgesFor(sourceIdA)
                .Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        public IEnumerable<NetworkEdge> GetAllSharedEdges()
            => _edges.Where(e => e.IsShared);

        public NetworkNode GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, _snapTolerance);
            return _nodes.TryGetValue(key, out var n) ? n : null;
        }

        public IEnumerable<NetworkEdge> QueryEdges(NetTopologySuite.Geometries.Envelope envelope)
            => _edgeIndex.Query(envelope).Cast<NetworkEdge>();

        public NetworkDefinition GetNetworkDefinition(int sourceId) {
            var edges = GetEdgesFor(sourceId);
            var shared = edges.Where(e => e.IsShared).ToList();
            var priv = edges.Where(e => !e.IsShared).ToList();
            var neighbours = shared
                .SelectMany(e => e.SourceGeometryIds)
                .Where(id => id != sourceId)
                .Distinct()
                .Select(id => _sources[id])
                .ToList();

            return new NetworkDefinition {
                SourceId = sourceId,
                Source = _sources[sourceId],
                AllEdges = edges.ToList(),
                SharedEdges = shared,
                PrivateEdges = priv,
                Neighbours = neighbours,
            };
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
        public readonly NetTopologySuite.Geometries.Envelope Envelope;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId) {
            SegId = segId;
            P0 = p0;
            P1 = p1;
            SourceId = sourceId;
            Envelope = new NetTopologySuite.Geometries.Envelope(p0, p1);
        }
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
            Comparer<SplitPoint>.Create((a, b) =>
                a._distFromStart.CompareTo(b._distFromStart));
    }

    /// <summary>
    /// Bucketed coordinate key for tolerance-based node identity.
    /// </summary>
    internal readonly struct CoordinateKey : IEquatable<CoordinateKey>,
                                           IComparable<CoordinateKey>
    {
        private readonly long _x, _y;

        public CoordinateKey(Coordinate c, double tolerance) {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(c.X * inv);
            _y = (long)Math.Round(c.Y * inv);
        }

        public bool Equals(CoordinateKey other) => _x == other._x && _y == other._y;
        public override bool Equals(object obj) => obj is CoordinateKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(_x, _y);
        public int CompareTo(CoordinateKey other) {
            int c = _x.CompareTo(other._x);
            return c != 0 ? c : _y.CompareTo(other._y);
        }
    }

    /// <summary>
    /// The full topology definition of a single source geometry within the network.
    /// </summary>
    internal class NetworkDefinition
    {
        public int SourceId { get; init; }
        public NetworkGeometry Source { get; init; }
        public List<NetworkEdge> AllEdges { get; init; }
        public List<NetworkEdge> SharedEdges { get; init; }
        public List<NetworkEdge> PrivateEdges { get; init; }
        public List<NetworkGeometry> Neighbours { get; init; }

        public double TotalLength =>
            AllEdges.Sum(e => e.Geometry.Length);
        public double SharedLength =>
            SharedEdges.Sum(e => e.Geometry.Length);
    }

}
