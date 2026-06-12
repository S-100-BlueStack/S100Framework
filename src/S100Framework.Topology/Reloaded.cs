using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace S100FC.Topology
{
    using NetTopologySuite.Operation.Linemerge;
    using S100Framework.Topology.Internal;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net;

    public interface IMatrixReloaded : IMatrix
    {
        GeometryFactory Factory { get; }
    }

    public class Reloaded : ITopologyBuilder, IMatrixReloaded
    {
        private class Surface
        {
            public int Id { get; init; }

            public required string Exterior { get; init; }

            public string[]? Interior { get; set; } = default;

            public string Name => $"S{this.Id}";
        }

        public static GeometryFactory Factory { get; set; } = new GeometryFactory(new PrecisionModel(10000000), srid: 4326); // Or PrecisionModels.Floating

        private Action<int, ICollection<(LineString lineString, string message)>>? _interceptor;

        private readonly MixedTopologyNetwork _mixedTopologyNetwork;

        private readonly Dictionary<string, PolygonSource> _featureMapperPolygons = [];
        private readonly Dictionary<string, int> _featureMapperLineStrings = [];

        private readonly Dictionary<string, string> _mapping = [];

        protected Reloaded() {
            //  Default protected constructor            

            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.000000001);
        }

        public static ITopologyBuilder CreateMatrix(Action<int, ICollection<(LineString lineString, string message)>>? interceptor = default) {
            return new Reloaded() {
                _interceptor = interceptor,
            };
        }

        GeometryFactory IMatrixReloaded.Factory => Reloaded.Factory;

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, true);

        ITopologyBuilder ITopologyBuilder.AddNavigationalFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, false);

        IMatrix ITopologyBuilder.BuildTopology() {
            this._mapping.Clear();
            this._mixedTopologyNetwork.Build();

            var featureRefs = new Dictionary<ulong, FeatureRef>();
            var featureRefsRevers = new Dictionary<ulong, ulong>();

            var source2featureRefs = new Dictionary<int, ulong>();

            var ids = new Dictionary<int, Func<string>>();


            //var depthArea = this._featureMapperPolygons["F10800045543"];
            //var landArea = this._featureMapperPolygons["F10800023692"];

            var curves = new Dictionary<FeatureRef, LineString>();

            //  Create all curves and composite curves
            //  ------------------------------------------------------------------------------------------------
            foreach (var id in this._mixedTopologyNetwork.Sources) {
                var mergedEdges = this._mixedTopologyNetwork.MergeEdgesFor(id);

                FeatureRef[] refs = [];

                var linemerger = new LineMerger();

                foreach (var edge in mergedEdges) {
                    var hash = System.IO.Hashing.XxHash32.HashToUInt32(edge.Geometry.ToBinary());

                    if (!featureRefs.ContainsKey(hash)) {
                        var curve = new CurveFeature(edge.Geometry);

                        featureRefs.Add(curve.Id, new FeatureRef {
                            Id = curve.Id,
                            Reverse = false,
                        });

                        var reverse = System.IO.Hashing.XxHash3.HashToUInt64(curve.LineStringReverse.AsBinary());
                        featureRefs.Add(reverse, new FeatureRef {
                            Id = curve.Id,
                            Reverse = true,
                        });
                        featureRefsRevers.Add(curve.Id, reverse);

                        this._curves.Add(curve.Id, curve);
                    }

                    refs = [.. refs, featureRefs[hash]];

                    linemerger.Add(edge.Geometry);
                }

                //if (id == landArea.ExteriorRing) System.Diagnostics.Debugger.Break();
                //if (id == depthArea.InteriorRing[1]) System.Diagnostics.Debugger.Break();

                var merged = linemerger.GetMergedLineStrings();
                Debug.Assert(merged.Count == 1);
                
                if (curves.Any(e => RingsEqual(e.Value, (LineString)merged[0], out bool reverse))) {
                    var _ = curves.Single(e => RingsEqual(e.Value, (LineString)merged[0], out bool reverse));

                    REVERSE

                    ids.Add(id, () => $"C{_.Key.Id}");
                    source2featureRefs.Add(id, _.Key.Id);
                    continue;
                }

                if (refs.Length > 1) {
                    var mergedText = merged[0].ToText();

                    var sortedlist = new SortedList<int, FeatureRef>();

                    foreach (var e in refs) {
                        var curve = this._curves[e.Id];

                        var text = curve.LineStringText.Substring("LINESTRING (".Length).TrimEnd(')');

                        if (ContainsSegment(mergedText, text))
                            sortedlist.Add(IndexOfSegment(mergedText, text), e);
                        else {
                            text = curve.LineStringReverseText.Substring("LINESTRING (".Length).TrimEnd(')');
                            sortedlist.Add(IndexOfSegment(mergedText, text), featureRefs[featureRefsRevers[e.Id]]);
                        }
                    }

                    var compositecurve = new CompositeCurveFeature([.. sortedlist.Values]);
                    if (!this._compositecurves.ContainsKey(compositecurve.Id))
                        this._compositecurves.Add(compositecurve.Id, compositecurve);

                    ids.Add(id, () => $"C{compositecurve.Id}");
                    source2featureRefs.Add(id, compositecurve.Id);

                    if (!featureRefs.ContainsKey(compositecurve.Id)) {
                        var _ = new FeatureRef {
                            Id = compositecurve.Id,
                            Reverse = false,
                        };
                        featureRefs.Add(compositecurve.Id, _);

                        curves.Add(_, (LineString)merged[0]);
                    }                    
                }
                else {
                    ids.Add(id, refs[0].Reverse ? () => $"RC{refs[0].Id}" : () => $"C{refs[0].Id}");

                    source2featureRefs.Add(id, refs[0].Id);

                    curves.Add(refs[0], (LineString)merged[0]);
                }

            }

            //  Create mapping
            //  ------------------------------------------------------------------------------------------------
            foreach (var id in this._mixedTopologyNetwork.Sources) {
                if (this._featureMapperLineStrings.ContainsValue(id)) {
                    var linestring = this._featureMapperLineStrings.Single(e => e.Value == id);

                    var uid = linestring.Key;

                    this._mapping.Add(uid, ids[id]());
                }
                //else {
                //    var polygon = this._featureMapperPolygons.Single(e => e.Value.ExteriorRing == id || e.Value.InteriorRing.Contains(id));

                //    var surface = new SurfaceFeature() {
                //        Ref = polygon.Key,
                //        Exterior = ids[polygon.Value.ExteriorRing](),
                //    };

                //}

            }

            foreach (var polygon in this._featureMapperPolygons) {
                var uid = polygon.Key;

                var surface = new SurfaceFeature {
                    Id = ulong.Parse(uid.Substring(1)),
                    Exterior = featureRefs[source2featureRefs[polygon.Value.ExteriorRing]],
                    Interior = [.. polygon.Value.InteriorRing.Select(e => featureRefs[source2featureRefs[e]])],
                    Ref = uid,
                };

                if (!this._surfaces.ContainsKey(surface.Id))
                    this._surfaces.Add(surface.Id, surface);

                this._mapping.Add(uid, $"S{surface.Id}");
            }

            if (this._mapping.Any(e => e.Value.StartsWith("RC"))) System.Diagnostics.Debugger.Break();

            return this;
        }

        private IDictionary<ulong, CurveFeature> _curves = new Dictionary<ulong, CurveFeature>();
        private IDictionary<ulong, CompositeCurveFeature> _compositecurves = new Dictionary<ulong, CompositeCurveFeature>();
        private IDictionary<ulong, SurfaceFeature> _surfaces = new Dictionary<ulong, SurfaceFeature>();

        IEnumerable<CurveFeature> IMatrix.Curves => this._curves.Values;

        IEnumerable<CompositeCurveFeature> IMatrix.CompositeCurves => this._compositecurves.Values;

        IEnumerable<SurfaceFeature> IMatrix.Surfaces => this._surfaces.Values;

        IDictionary<string, string> IMatrix.MappingFOID => this._mapping;

        public record PolygonSource(int ExteriorRing, int[] InteriorRing);

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

                var idExteriorRing = this._mixedTopologyNetwork.AddLineString(polygon.ExteriorRing);
                var idInteriorRings = new int[0];
                foreach (var interior in polygon.InteriorRings) {
                    var id = this._mixedTopologyNetwork.AddLineString(interior);
                    idInteriorRings = [.. idInteriorRings, id];
                }

                var p = new PolygonSource(idExteriorRing, idInteriorRings);
                this._featureMapperPolygons.Add(surface.Name, p);
                //this._featureMapperPolygons.Add(surface.UID, p);

                //var id = this._mixedTopologyNetwork.AddPolygon(polygon);
                //this._featureMapper.Add(surface.UID, id);
            }

            foreach (var curve in curves) {
                var id = this._mixedTopologyNetwork.AddLineString(curve.LineString);
                this._featureMapperLineStrings.Add(curve.Name, id);
                //this._featureMapperLineStrings.Add(curve.UID, id);
            }

            return this;
        }

        private static bool RingsEqual(LineString a, LineString b, out bool reverse) {
            reverse = false;

            var coordsA = a.Coordinates.Take(a.Coordinates.Length - 1).ToArray();
            var coordsB = b.Coordinates.Take(b.Coordinates.Length - 1).ToArray();

            if (coordsA.Length != coordsB.Length) return false;

            int n = coordsA.Length;

            for (int dir = 0; dir < 2; dir++) {
                var seqB = dir == 0 ? coordsB : coordsB.Reverse().ToArray();

                for (int offset = 0; offset < n; offset++) {
                    bool match = true;
                    for (int i = 0; i < n; i++) {
                        var ca = coordsA[i];
                        var cb = seqB[(i + offset) % n];
                        if (ca.X != cb.X || ca.Y != cb.Y) {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
                reverse = true;
            }

            return false;
        }

        private static bool ContainsSegment(string lineString, string segment) {
            if (lineString.Equals(segment)) return true;

            if (lineString.Contains(segment + ",")) return true;
            if (lineString.Contains(", " + segment)) return true;

            //  MultiLineString
            if (lineString.Contains(segment + ")")) return true;
            if (lineString.Contains(segment + "),")) return true;
            if (lineString.Contains("), " + segment)) return true;

            return false;
        }

        private static int IndexOfSegment(string lineString, string segment) {
            if (lineString.Equals(segment)) return 0;

            if (lineString.Contains(segment + ",")) return lineString.IndexOf(segment + ",");
            if (lineString.Contains(", " + segment)) return lineString.IndexOf(", " + segment);

            //  MultiLineString
            if (lineString.Contains(segment + ")")) return lineString.IndexOf(segment + ")");
            if (lineString.Contains(segment + "),")) return lineString.IndexOf(segment + "),");
            if (lineString.Contains("), " + segment)) return lineString.IndexOf("), " + segment);

            throw new IndexOutOfRangeException();
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
        public HashSet<int> SourceGeometryIds { get; } = [];

        // Is this edge shared between two or more geometries?
        public bool IsShared => this.SourceGeometryIds.Count > 1;
    }

    /// <summary>
    /// A node in the planar network.
    /// </summary>
    internal class NetworkNode
    {
        public Coordinate? Coordinate { get; init; } = default;

        // All edges incident to this node
        public List<NetworkEdge> Edges { get; } = [];

        public int Degree => this.Edges.Count;
    }

    internal class MixedTopologyNetwork
    {
        private readonly GeometryFactory _factory;
        private readonly double _snapTolerance;

        private readonly List<NetworkGeometry> _sources = [];
        private readonly List<NetworkEdge> _edges = [];
        private readonly Dictionary<CoordinateKey, NetworkNode> _nodes = [];
        private readonly Dictionary<int, List<NetworkEdge>> _edgesBySource = [];
        private STRtree<NetworkEdge>? _edgeIndex = default;

        public IReadOnlyList<NetworkEdge> Edges => this._edges;
        public IReadOnlyCollection<NetworkNode> Nodes => this._nodes.Values;

        public MixedTopologyNetwork(GeometryFactory factory, double snapTolerance = 0.0001) {
            this._factory = factory;
            this._snapTolerance = snapTolerance;
        }

        public IEnumerable<int> Sources => this._sources.Select(e => e.Id);

        public int AddPolygon(NetTopologySuite.Geometries.Polygon p) => this.Register(p, GeometryKind.Polygon);
        public int AddLineString(LineString l) => this.Register(l, GeometryKind.LineString);
        public void AddPolygons(IEnumerable<NetTopologySuite.Geometries.Polygon> ps) { foreach (var p in ps) this.AddPolygon(p); }
        public void AddLineStrings(IEnumerable<LineString> ls) { foreach (var l in ls) this.AddLineString(l); }

        private int Register(NetTopologySuite.Geometries.Geometry geom, GeometryKind kind) {
            int id = this._sources.Count;
            this._sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------------
        // Build — spatially-local noding only
        // -------------------------------------------------------------------------
        public void Build() {
            // Step 1: Extract raw segments per source — no LineString allocation,
            //         just coordinate pairs + source id
            var rawSegments = this.ExtractRawSegments();

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
                queryEnv.ExpandBy(this._snapTolerance);

                var candidates = segIndex.Query(queryEnv);
                foreach (var other in candidates) {
                    if (other.SegId >= seg.SegId) continue; // process each pair once

                    li.ComputeIntersection(seg.P0, seg.P1, other.P0, other.P1);
                    if (!li.HasIntersection) continue;

                    for (int k = 0; k < li.IntersectionNum; k++) {
                        var pt = li.GetIntersection(k);

                        // Snap to endpoint if within tolerance
                        pt = this.SnapToEndpoint(pt, seg, other);

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
                    if (!sp.Coord.Equals2D(coords[^1], this._snapTolerance))
                        coords.Add(sp.Coord);
                }
                if (!seg.P1.Equals2D(coords[^1], this._snapTolerance))
                    coords.Add(seg.P1);

                // Insert each sub-segment as a network edge
                for (int i = 0; i < coords.Count - 1; i++)
                    this.InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
            }

            // Step 5: Spatial index on final edges
            this._edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in this._edges)
                this._edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }

        // -------------------------------------------------------------------------
        // Segment extraction — no geometry object allocation per segment
        // -------------------------------------------------------------------------
        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(this.EstimateSegmentCount());
            int segId = 0;

            foreach (var src in this._sources) {
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

        private static int ExtractFromRing(Coordinate[] coords, int sourceId, List<RawSegment> result, int segId) {
            for (int i = 0; i < coords.Length - 1; i++) {
                result.Add(new RawSegment(segId++, coords[i], coords[i + 1], sourceId));
            }
            return segId;
        }

        private int EstimateSegmentCount() => this._sources.Sum(s => s.Geometry.NumPoints);

        // -------------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------------
        private void InsertEdge(Coordinate c0, Coordinate c1, int sourceId, Dictionary<(CoordinateKey, CoordinateKey), NetworkEdge> edgeMap) {
            if (c0.Equals2D(c1, this._snapTolerance)) return; // degenerate

            var k0 = new CoordinateKey(c0, this._snapTolerance);
            var k1 = new CoordinateKey(c1, this._snapTolerance);
            var key = k0.CompareTo(k1) <= 0 ? (k0, k1) : (k1, k0);

            if (!edgeMap.TryGetValue(key, out var edge)) {
                var ls = this._factory.CreateLineString(new[] { c0, c1 });
                edge = new NetworkEdge {
                    Id = this._edges.Count,
                    Geometry = ls,
                    StartNode = c0,
                    EndNode = c1,
                };
                this._edges.Add(edge);
                edgeMap[key] = edge;

                this.GetOrCreateNode(c0).Edges.Add(edge);
                this.GetOrCreateNode(c1).Edges.Add(edge);
            }

            edge.SourceGeometryIds.Add(sourceId);

            if (!this._edgesBySource.TryGetValue(sourceId, out var list))
                this._edgesBySource[sourceId] = list = [];

            // Avoid duplicates if the same source produced the same sub-segment
            if (!list.Contains(edge))
                list.Add(edge);
        }

        private NetworkNode GetOrCreateNode(Coordinate coord) {
            var key = new CoordinateKey(coord, this._snapTolerance);
            if (!this._nodes.TryGetValue(key, out var node))
                this._nodes[key] = node = new NetworkNode { Coordinate = coord };
            return node;
        }

        // -------------------------------------------------------------------------
        // Snapping helper
        // -------------------------------------------------------------------------
        private Coordinate SnapToEndpoint(
            Coordinate pt, RawSegment seg, RawSegment other) {
            foreach (var ep in new[] { seg.P0, seg.P1, other.P0, other.P1 }) {
                if (pt.Distance(ep) <= this._snapTolerance)
                    return ep;
            }
            return pt;
        }

        // -------------------------------------------------------------------------
        // Query API (unchanged from before)
        // -------------------------------------------------------------------------
        public IReadOnlyList<NetworkEdge> GetEdgesFor(int sourceId)
            => this._edgesBySource.TryGetValue(sourceId, out var l)
                ? l : Array.Empty<NetworkEdge>();

        public IEnumerable<NetworkEdge> GetSharedEdges(int sourceIdA, int sourceIdB)
            => this.GetEdgesFor(sourceIdA).Where(e => e.SourceGeometryIds.Contains(sourceIdB));

        public IEnumerable<NetworkEdge> GetAllSharedEdges() => this._edges.Where(e => e.IsShared);

        public NetworkNode GetNode(Coordinate coord) {
            var key = new CoordinateKey(coord, this._snapTolerance);
            return this._nodes.TryGetValue(key, out var n) ? n : null;
        }

        public IEnumerable<NetworkEdge> QueryEdges(NetTopologySuite.Geometries.Envelope envelope) => this._edgeIndex.Query(envelope).Cast<NetworkEdge>();

        public NetworkDefinition GetNetworkDefinition(int sourceId) {
            var edges = this.GetEdgesFor(sourceId);
            var shared = edges.Where(e => e.IsShared).ToList();
            var priv = edges.Where(e => !e.IsShared).ToList();
            var neighbours = shared
                .SelectMany(e => e.SourceGeometryIds)
                .Where(id => id != sourceId)
                .Distinct()
                .Select(id => this._sources[id])
                .ToList();

            return new NetworkDefinition {
                SourceId = sourceId,
                Source = this._sources[sourceId],
                AllEdges = edges.ToList(),
                SharedEdges = shared,
                PrivateEdges = priv,
                Neighbours = neighbours,
            };
        }


        /// <summary>
        /// Merges collinear/sequential shared edges into the longest possible
        /// LineStrings, respecting source-geometry boundaries.
        /// </summary>
        public List<MergedEdge> MergeSharedEdges() {
            return MergeEdgeSet(GetAllSharedEdges());
        }

        /// <summary>
        /// Merge edges for a specific source geometry.
        /// </summary>
        public List<MergedEdge> MergeEdgesFor(int sourceId) {
            return MergeEdgeSet(GetEdgesFor(sourceId));
        }

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

            var visited = new HashSet<int>();   // edge ids already consumed
            var result = new List<MergedEdge>();

            foreach (var startEdge in edges) {
                if (visited.Contains(startEdge.Id)) continue;

                // Walk in both directions from this edge, collecting a chain
                var chain = WalkChain(startEdge, edges, adjacency, visited);

                // Order the chain coordinates into a single LineString
                var coords = ChainToCoordinates(chain);
                var sourceSets = chain.Select(e => e.SourceGeometryIds).ToList();

                result.Add(new MergedEdge {
                    Geometry = _factory.CreateLineString(coords),
                    SourceGeometryIds = chain
                        .SelectMany(e => e.SourceGeometryIds)
                        .Distinct()
                        .ToHashSet(),
                    ConstituentEdges = chain,
                });
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // Chain walker
        // -------------------------------------------------------------------------
        private List<NetworkEdge> WalkChain(
            NetworkEdge seed,
            HashSet<NetworkEdge> edgeSet,
            Dictionary<CoordinateKey, List<NetworkEdge>> adjacency,
            HashSet<int> visited) {
            visited.Add(seed.Id);

            // Walk forward from seed.EndNode, backward from seed.StartNode
            var forward = Walk(seed.EndNode, seed, edgeSet, adjacency, visited);
            var backward = Walk(seed.StartNode, seed, edgeSet, adjacency, visited);

            // Chain = reversed-backward + seed + forward
            backward.Reverse();
            var chain = new List<NetworkEdge>(backward.Count + 1 + forward.Count);
            chain.AddRange(backward);
            chain.Add(seed);
            chain.AddRange(forward);
            return chain;
        }

        private List<NetworkEdge> Walk(Coordinate fromCoord, NetworkEdge incoming, HashSet<NetworkEdge> edgeSet, Dictionary<CoordinateKey, List<NetworkEdge>> adjacency, HashSet<int> visited) {
            var result = new List<NetworkEdge>();
            var current = incoming;
            var currentCoord = fromCoord;

            while (true) {
                var key = new CoordinateKey(currentCoord, _snapTolerance);
                if (!adjacency.TryGetValue(key, out var neighbours)) break;

                // Candidates: edges in the set, not already visited, not the current one
                var next = neighbours
                    .Where(e => !visited.Contains(e.Id)
                             && e.Id != current.Id
                             && edgeSet.Contains(e))
                    .ToList();

                // Stop if junction (more than one continuation) or dead end
                if (next.Count != 1) break;

                var nextEdge = next[0];

                // Stop if source-geometry set changes — different topology boundary
                if (!nextEdge.SourceGeometryIds.SetEquals(current.SourceGeometryIds))
                    break;

                visited.Add(nextEdge.Id);
                result.Add(nextEdge);

                // Advance: move to the other end of nextEdge
                currentCoord = nextEdge.StartNode.Equals2D(currentCoord, _snapTolerance)
                    ? nextEdge.EndNode
                    : nextEdge.StartNode;
                current = nextEdge;
            }

            return result;
        }

        // -------------------------------------------------------------------------
        // Stitch coordinates from an ordered edge chain into one coordinate array
        // -------------------------------------------------------------------------
        private Coordinate[] ChainToCoordinates(List<NetworkEdge> chain) {
            if (chain.Count == 0) return Array.Empty<Coordinate>();
            if (chain.Count == 1)
                return chain[0].Geometry.Coordinates.ToArray();

            var coords = new List<Coordinate>();

            for (int i = 0; i < chain.Count; i++) {
                var edge = chain[i];
                var edgeCs = edge.Geometry.Coordinates;

                if (i == 0) {
                    // Determine orientation vs next edge
                    var nextEdge = chain[1];
                    bool forward = ConnectsTo(edge, nextEdge, fromEnd: true);
                    coords.AddRange(forward ? edgeCs : edgeCs.Reverse());
                }
                else {
                    // Orient so first coord == last coord already added
                    var last = coords[^1];
                    bool startMatches = edgeCs[0].Equals2D(last, _snapTolerance);
                    var oriented = startMatches ? edgeCs : edgeCs.Reverse();

                    // Skip the first coord — it's the same as the last one added
                    coords.AddRange(oriented.Skip(1));
                }
            }

            return coords.ToArray();
        }

        private bool ConnectsTo(NetworkEdge edge, NetworkEdge other, bool fromEnd) {
            var coord = fromEnd ? edge.EndNode : edge.StartNode;
            return coord.Equals2D(other.StartNode, _snapTolerance)
                || coord.Equals2D(other.EndNode, _snapTolerance);
        }


    }

    internal class MergedEdge
    {
        public LineString Geometry { get; init; }
        public HashSet<int> SourceGeometryIds { get; init; }
        public List<NetworkEdge> ConstituentEdges { get; init; }

        public bool IsShared => SourceGeometryIds.Count > 1;
        public double Length => Geometry.Length;
        public int SegmentCount => ConstituentEdges.Count;
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
            this.SegId = segId;
            this.P0 = p0;
            this.P1 = p1;
            this.SourceId = sourceId;
            this.Envelope = new NetTopologySuite.Geometries.Envelope(p0, p1);
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
            this.Coord = coord;
            this._distFromStart = coord.Distance(segStart);
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
            this._x = (long)Math.Round(c.X * inv);
            this._y = (long)Math.Round(c.Y * inv);
        }

        public bool Equals(CoordinateKey other) => this._x == other._x && this._y == other._y;
        public override bool Equals(object obj) => obj is CoordinateKey k && this.Equals(k);
        public override int GetHashCode() => HashCode.Combine(this._x, this._y);
        public int CompareTo(CoordinateKey other) {
            int c = this._x.CompareTo(other._x);
            return c != 0 ? c : this._y.CompareTo(other._y);
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
            this.AllEdges.Sum(e => e.Geometry.Length);
        public double SharedLength =>
            this.SharedEdges.Sum(e => e.Geometry.Length);
    }

}
