using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Precision;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("TestTopology")]

namespace S100FC.Topology
{
    using NetTopologySuite.Operation.Linemerge;
    using NetTopologySuite.Precision;
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

        public static GeometryFactory? Factory { get; set; } = default; // new GeometryFactory(new PrecisionModel(10000000), srid: 4326); // Or PrecisionModels.Floating

        private Action<int, ICollection<(LineString lineString, string message)>>? _interceptor;

        private readonly MixedTopologyNetwork _mixedTopologyNetwork;

        private readonly Dictionary<string, PolygonSource> _featureMapperPolygons = [];
        private readonly Dictionary<string, int> _featureMapperLineStrings = [];

        private readonly Dictionary<string, string> _mapping = [];

        protected Reloaded() {
            //  Protected default constructor            

            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.000000001);
            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.000000005);
            //this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: 0.00000001);
            this._mixedTopologyNetwork = new MixedTopologyNetwork(Reloaded.Factory, snapTolerance: Reloaded.Factory.PrecisionModel.GridSize);
        }

        public static ITopologyBuilder CreateMatrix(Action<int, ICollection<(LineString lineString, string message)>>? interceptor = default) {
            return new Reloaded() {
                _interceptor = interceptor,
            };
        }

        GeometryFactory IMatrixReloaded.Factory => Reloaded.Factory;

        ITopologyBuilder ITopologyBuilder.AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, true);

        ITopologyBuilder ITopologyBuilder.AddNavigationalFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<Polyline> curves) => this.AddTopologyFeatures(surfaces, curves, false);

        public GeometryPrecisionReducer Reducer => this._mixedTopologyNetwork.Reducer;

        IMatrix ITopologyBuilder.BuildTopology() {
            this._mapping.Clear();
            this._mixedTopologyNetwork.Build();

            //this._interceptor?.Invoke(100, [.. this._mixedTopologyNetwork.Edges.Select(e => (e.Geometry, $"{e.Id}"))]);

            var featureRefs = new Dictionary<ulong, FeatureRef>();

            var source2featureRefs = new Dictionary<int, ulong>();

            var ids = new Dictionary<int, Func<string>>();

            //var xxFeature = this._featureMapperPolygons.Single(e => e.Key.Equals("F10400012235"));
            //int[] xxID = [xxFeature.Value.ExteriorRing, .. xxFeature.Value.InteriorRing];
            //var xxEdges = this._mixedTopologyNetwork.Edges.Where(e => e.SourceGeometryIds.Any(i => xxID.Contains(i))).ToArray();

            //this._interceptor?.Invoke(6000, [.. xxEdges.Select(e => (e.Geometry, $"{e.Id}"))!]);

            //  Create all curves and composite curves
            //  ------------------------------------------------------------------------------------------------
            foreach (var id in this._mixedTopologyNetwork.Sources) {
                foreach (var edge in this._mixedTopologyNetwork.MergeEdgesFor(id)) {
                    var hash1 = System.IO.Hashing.XxHash32.HashToUInt32(edge.Geometry.AsBinary());
                    var hash2 = System.IO.Hashing.XxHash32.HashToUInt32(edge.Geometry.Factory.CreateLineString([.. edge.Geometry.Coordinates.Reverse()]).AsBinary());

                    if (!featureRefs.ContainsKey(hash1) || !featureRefs.ContainsKey(hash2)) {
                        var featureRef1 = new FeatureRef {
                            Id = hash1,
                            Reverse = false,
                        };
                        featureRefs.Add(featureRef1.Id, featureRef1);

                        var featureRef2 = new FeatureRef {
                            Id = hash2,
                            Reverse = true,
                        };
                        featureRefs.Add(featureRef2.Id, featureRef2);

                        var curve = new CurveFeature(edge.Geometry, hash1);
                        this._curves.Add(featureRef1.Id, curve);
                        this._curves.Add(featureRef2.Id, curve);
                    }
                }
            }

            var curves = new Dictionary<FeatureRef, LineString>();

            var used = new List<FeatureRef>();

            foreach (var id in this._mixedTopologyNetwork.Sources) {
                var mergedEdges = this._mixedTopologyNetwork.MergeEdgesFor(id);                

                FeatureRef[] refs = [];

                var linemerger = new LineMerger();

                foreach (var edge in mergedEdges) {
                    var hash = System.IO.Hashing.XxHash32.HashToUInt32(edge.Geometry.ToBinary());
                    refs = [.. refs, featureRefs[hash]];

                    linemerger.Add(edge.Geometry);
                }

                var merged = linemerger.GetMergedLineStrings();
                if (merged.Count > 1) {
                    this._interceptor?.Invoke(100, [.. mergedEdges.Select(e => (e.Geometry, $"{string.Join(',',e.SourceGeometryIds)}"))]);

                    //this._interceptor?.Invoke(100, [.. merged.Select(e => ((LineString)e, ""))]);
                    System.Diagnostics.Debugger.Break();
                }
                Debug.Assert(merged.Count == 1);                

                bool skip = false;
                foreach (var c in curves) {
                    if (!RingsEqual(c.Value, (LineString)merged[0], out bool reverse)) continue;

                    if (!reverse) {
                        ids.Add(id, c.Key.Reverse ? () => $"RC{c.Key.Id}" : () => $"C{c.Key.Id}");
                    }
                    else {
                        ids.Add(id, !c.Key.Reverse ? () => $"RC{c.Key.Id}" : () => $"C{c.Key.Id}");
                    }
                    source2featureRefs.Add(id, c.Key.Id);
                    skip = true;
                    continue;
                }
                if (skip) continue;

                used.AddRange(refs);

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
                            sortedlist.Add(IndexOfSegment(mergedText, text), new FeatureRef {
                                Id = e.Id,
                                Reverse = !e.Reverse,
                            });
                        }
                    }

                    var compositecurve = new CompositeCurveFeature([.. sortedlist.Values]);
                    if (!this._compositecurves.ContainsKey(compositecurve.Id))
                        this._compositecurves.Add(compositecurve.Id, compositecurve);

                    ids.Add(id, () => $"C{compositecurve.Id}");
                    source2featureRefs.Add(id, compositecurve.Id);

                    if (!featureRefs.ContainsKey(compositecurve.Id)) {
                        var featureRef1 = new FeatureRef {
                            Id = compositecurve.Id,
                            Reverse = false,
                        };
                        featureRefs.Add(compositecurve.Id, featureRef1);

                        curves.Add(featureRef1, (LineString)merged[0]);
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
            }

            this._interceptor?.Invoke(100, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))]);

            foreach (var polygon in this._featureMapperPolygons) {
                var uid = polygon.Key;

                FeatureRef exteriorRing = featureRefs[source2featureRefs[polygon.Value.ExteriorRing]];
                {
                    var curve = curves[exteriorRing];
                    var ring = curve.Factory.CreateLinearRing(curve.Coordinates);
                    if (ring.IsCCW) {
                        exteriorRing = new FeatureRef {
                            Id = exteriorRing.Id,
                            Reverse = !exteriorRing.Reverse,
                        };
                    }
                }

                FeatureRef[] interior = [];
                if (polygon.Value.InteriorRing != default) {
                    for (int i = 0; i < polygon.Value.InteriorRing.Length; i++) {
                        var featureRef = featureRefs[source2featureRefs[polygon.Value.InteriorRing[i]]];
                        var curve = curves[featureRef];

                        var ring = curve.Factory.CreateLinearRing(curve.Coordinates);
                        if (!ring.IsCCW) {
                            featureRef = new FeatureRef {
                                Id = featureRef.Id,
                                Reverse = !featureRef.Reverse,
                            };
                        }
                        interior = [.. interior, featureRef];
                    }
                }

                var surface = new SurfaceFeature {
                    Id = ulong.Parse(uid.Substring(1)),
                    Exterior = exteriorRing,
                    Interior = interior,
                    Ref = uid,
                };

                if (!this._surfaces.ContainsKey(surface.Id))
                    this._surfaces.Add(surface.Id, surface);

                this._mapping.Add(uid, $"S{surface.Id}");
            }

            if (this._mapping.Any(e => e.Value.StartsWith("RC"))) System.Diagnostics.Debugger.Break();

            var _ = used.Select(e => e.Id).Distinct();
            this._curves = this._curves.Where(e => _.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value);


            //this._interceptor?.Invoke(6000, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))]);
            //this._interceptor?.Invoke(100, [.. this._curves.Select(e => (e.Value.LineString, $"{e.Value.Id}"))]);

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
                //Func<NetTopologySuite.Geometries.Polygon> createPolygon = surface.InteriorRings.Any() switch {
                //    true => () => {
                //        return Factory.CreatePolygon(Factory.CreateLinearRing(surface.ExteriorRing.Coordinates), [.. surface.InteriorRings.Select(e => Factory.CreateLinearRing(e.Coordinates))]);
                //    }
                //    ,
                //    false => () => {
                //        return Factory.CreatePolygon(Factory.CreateLinearRing(surface.ExteriorRing.Coordinates), []);
                //    }
                //    ,
                //};
                //var polygon = createPolygon();

                //if (!reducer.Reduce(polygon.ExteriorRing).Equals(polygon.ExteriorRing)) System.Diagnostics.Debugger.Break();
                
                var idExteriorRing = this._mixedTopologyNetwork.AddLineString(surface.ExteriorRing);
                var idInteriorRings = new int[0];
                foreach (var interior in surface.InteriorRings) {
                    var id = this._mixedTopologyNetwork.AddLineString((LineString)this.Reducer.Reduce(interior));
                    idInteriorRings = [.. idInteriorRings, id];
                }

                if (idExteriorRing == 78 || idInteriorRings.Contains(78)) {
                    //this._interceptor?.Invoke(100, [(surface.ExteriorRing, surface.UID)]);
                    System.Diagnostics.Debugger.Break();
                }

                var p = new PolygonSource(idExteriorRing, idInteriorRings);
                this._featureMapperPolygons.Add(surface.Name, p);
                //this._featureMapperPolygons.Add(surface.UID, p);

                //var id = this._mixedTopologyNetwork.AddPolygon(polygon);
                //this._featureMapper.Add(surface.UID, id);
            }

            foreach (var curve in curves) {
                //if (!this.Reducer.Reduce(curve.LineString).Equals(curve.LineString)) System.Diagnostics.Debugger.Break();

                var id = this._mixedTopologyNetwork.AddLineString((LineString)this.Reducer.Reduce(curve.LineString));
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

        // sourceId -> true if that source traverses StartNode -> EndNode (forward)
        public Dictionary<int, bool> SourceOrientation { get; } = new();

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

        public GeometryPrecisionReducer Reducer => new GeometryPrecisionReducer(new PrecisionModel(1.0 / this._snapTolerance)) { Pointwise = true, RemoveCollapsedComponents = true };

        public MixedTopologyNetwork(GeometryFactory factory, double snapTolerance) {
            this._factory = factory;
            this._snapTolerance = snapTolerance;
        }

        public IEnumerable<int> Sources => this._sources.Select(e => e.Id);

        public int AddPolygon(NetTopologySuite.Geometries.Polygon p) => this.Register(p, GeometryKind.Polygon);
        public int AddLineString(LineString l) => this.Register(l, GeometryKind.LineString);
        public void AddPolygons(IEnumerable<NetTopologySuite.Geometries.Polygon> ps) { foreach (var p in ps) this.AddPolygon(p); }
        public void AddLineStrings(IEnumerable<LineString> ls) { foreach (var l in ls) this.AddLineString(l); }

        private int Register(NetTopologySuite.Geometries.Geometry geom, GeometryKind kind) {
            //geom.Normalize();
            int id = this._sources.Count;
            this._sources.Add(new NetworkGeometry { Id = id, Geometry = geom, Kind = kind });
            return id;
        }

        // -------------------------------------------------------------------------
        // Build — spatially-local noding only
        // -------------------------------------------------------------------------
#if null
        public void Build() {
            // Step 1: Extract raw segments per source — no LineString allocation,
            //         just coordinate pairs + source id
            var rawSegments = this.ExtractRawSegments();

            // ---- SINGLE canonical coordinate pass ----
            // Build ONE dictionary: grid-cell-key -> canonical Coordinate
            // Merge adjacent cells (within 1 cell) into the SAME canonical coordinate.
            var canonical = BuildCanonicalCoordinateMap(rawSegments);

            var remapped = new List<RawSegment>(rawSegments.Count);
            // Remap every raw segment endpoint through the canonical map
            for (int i = 0; i < rawSegments.Count; i++) {
                var seg = rawSegments[i];
                var p0 = canonical[new CoordinateKey(SnapToGrid(seg.P0), _snapTolerance)];
                var p1 = canonical[new CoordinateKey(SnapToGrid(seg.P1), _snapTolerance)];

                // ✅ CRITICAL: skip degenerate segments AFTER canonicalization,
                // not just at original extraction time
                if (p0.Equals2D(p1)) continue;

                //rawSegments[i] = seg.WithCoordinates(p0, p1);
                remapped.Add(seg.WithCoordinates(p0, p1));
            }
            rawSegments = remapped;

            // Pass 1: collect all distinct snapped coordinates
            var existingByKey = new Dictionary<CoordinateKey, Coordinate>();
            var dedupeTolerance = _snapTolerance * 2; // 1-cell neighbor search

            foreach (var seg in rawSegments) {
                RegisterCoordinate(seg.P0, existingByKey, dedupeTolerance);
                RegisterCoordinate(seg.P1, existingByKey, dedupeTolerance);
            }

            // Pass 2: re-map every segment endpoint through the dedupe map
            for (int i = 0; i < rawSegments.Count; i++) {
                var seg = rawSegments[i];
                var p0 = SnapToNearestExisting(seg.P0, existingByKey, dedupeTolerance);
                var p1 = SnapToNearestExisting(seg.P1, existingByKey, dedupeTolerance);
                rawSegments[i] = seg.WithCoordinates(p0, p1);
            }





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
                for (int i = 0; i < coords.Count - 1; i++) {
                    //this.InsertEdge(coords[i], coords[i + 1], seg.SourceId, edgeMap);
                    this.InsertEdge(SnapToGrid(coords[i]), SnapToGrid(coords[i + 1]), seg.SourceId, edgeMap);
                }
            }

            // Step 5: Spatial index on final edges
            this._edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in this._edges)
                this._edgeIndex.Insert(edge.Geometry!.EnvelopeInternal, edge);
        }
#endif
        public void Build() {
            // -----------------------------------------------------------------
            // Step 1: Extract raw segments per source (bare coordinates)
            // -----------------------------------------------------------------
            var rawSegments = ExtractRawSegments();

            // -----------------------------------------------------------------
            // Step 2: Canonicalize coordinates (union-find over adjacent grid cells)
            //         and remap every segment's endpoints to canonical coordinates.
            //         Drop any segment that becomes degenerate (zero-length)
            //         AFTER canonicalization.
            // -----------------------------------------------------------------
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

            // -----------------------------------------------------------------
            // Step 3: Spatial index over cleaned segments
            // -----------------------------------------------------------------
            var segIndex = new STRtree<RawSegment>();
            foreach (var seg in segments)
                segIndex.Insert(seg.Envelope, seg);

            // -----------------------------------------------------------------
            // Step 4: Local intersection detection — collect split points per segment
            // -----------------------------------------------------------------
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

            // -----------------------------------------------------------------
            // Step 5: Split each segment at its intersection points and insert
            //         the resulting sub-segments into the graph
            // -----------------------------------------------------------------
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

            // -----------------------------------------------------------------
            // Step 6: Spatial index on final edges
            // -----------------------------------------------------------------
            _edgeIndex = new STRtree<NetworkEdge>();
            foreach (var edge in _edges)
                _edgeIndex.Insert(edge.Geometry.EnvelopeInternal, edge);
        }




        // -------------------------------------------------------------------------
        // Segment extraction — no geometry object allocation per segment
        // -------------------------------------------------------------------------
        private List<RawSegment> ExtractRawSegments() {
            var result = new List<RawSegment>(this.EstimateSegmentCount());
            int segId = 0;

            foreach (var src in this._sources) {
                if (src.Kind == GeometryKind.Polygon) {
                    var poly = (NetTopologySuite.Geometries.Polygon)src.Geometry!;
                    segId = ExtractFromRing(poly.ExteriorRing.Coordinates,
                        src.Id, result, segId);
                    foreach (var hole in poly.InteriorRings)
                        segId = ExtractFromRing(hole.Coordinates,
                            src.Id, result, segId);
                }
                else {
                    segId = ExtractFromRing(src.Geometry!.Coordinates,
                        src.Id, result, segId);
                }
            }

            return result;
        }

        private int ExtractFromRing(
            Coordinate[] coords, int sourceId,
            List<RawSegment> result, int segId) {
            // Snap + de-duplicate consecutive identical coordinates that
            // collapse together after snapping
            var snapped = new List<Coordinate>(coords.Length);
            foreach (var c in coords) {
                var sc = SnapToGrid(c);
                if (snapped.Count == 0 || !sc.Equals2D(snapped[^1]))
                    snapped.Add(sc);
            }

            // Re-close the ring if snapping broke closure
            if (snapped.Count > 2 && !snapped[0].Equals2D(snapped[^1]))
                snapped.Add(snapped[0]);

            for (int i = 0; i < snapped.Count - 1; i++) {
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

        /// <summary>
        /// Builds a map from EVERY grid cell touched by input coordinates to ONE
        /// canonical coordinate. Adjacent cells (within 1 cell of each other) that
        /// are also within snapTolerance in real distance collapse to the same
        /// canonical coordinate using a union-find–style merge.
        /// </summary>
        private Dictionary<CoordinateKey, Coordinate> BuildCanonicalCoordinateMap(
            List<RawSegment> rawSegments) {
            // Step 1: collect distinct snapped coordinates per cell (first-seen wins
            // as the "raw" value for that cell)
            var cellValue = new Dictionary<CoordinateKey, Coordinate>();
            foreach (var seg in rawSegments) {
                foreach (var c in new[] { seg.P0, seg.P1 }) {
                    var snapped = SnapToGrid(c);
                    var key = new CoordinateKey(snapped, _snapTolerance);
                    if (!cellValue.ContainsKey(key))
                        cellValue[key] = snapped;
                }
            }

            // Step 2: union-find over cells. Two cells merge if they are adjacent
            // (within 1 cell in x or y) AND their raw values are within tolerance.
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
            foreach (var key in keys) {
                var v = cellValue[key];
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++) {
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

            foreach (var key in keys) {
                var root = Find(key);
                if (!groupCanonical.TryGetValue(root, out var canon)) {
                    canon = cellValue[root]; // representative's own value
                    groupCanonical[root] = canon;
                }
                result[key] = canon;
            }

            return result;
        }

        private void RegisterCoordinate(Coordinate c, Dictionary<CoordinateKey, Coordinate> existingByKey, double dedupeTolerance) {
            var snapped = SnapToGrid(c);
            var key = new CoordinateKey(snapped, _snapTolerance);

            if (!existingByKey.ContainsKey(key)) {
                // Check if a neighboring cell already has a coordinate within
                // dedupe distance — if so, don't register a new one
                foreach (var dx in new[] { -1, 0, 1 })
                    foreach (var dy in new[] { -1, 0, 1 }) {
                        var nk = key.Offset(dx, dy);
                        if (existingByKey.TryGetValue(nk, out var existing)
                            && snapped.Distance(existing) <= dedupeTolerance)
                            return; // already covered by a neighbor
                    }

                existingByKey[key] = snapped;
            }
        }

        /// <summary>
        /// Snaps a coordinate to a fixed grid defined by tolerance.
        /// </summary>
        //private Coordinate SnapToGrid(Coordinate c) {
        //    return c;
        //    double inv = 1.0 / _snapTolerance;
        //    double x = Math.Round(c.X * inv) / inv;
        //    double y = Math.Round(c.Y * inv) / inv;

        //    // Preserve Z if present
        //    var coord = double.IsNaN(c.Z)
        //        ?  new Coordinate(x, y)
        //        : new CoordinateZ(x, y, c.Z);

        //    //_factory.PrecisionModel.MakePrecise(coord);
        //    return coord;
        //}

        private Coordinate SnapToGrid(Coordinate c) {
            double inv = 1.0 / _snapTolerance;

            // Nudge slightly before flooring to absorb floating-point noise
            // that places a "true" grid-line point just below the line
            const double epsilon = 1e-9;
            double x = Math.Floor(c.X * inv + epsilon) * _snapTolerance;
            double y = Math.Floor(c.Y * inv + epsilon) * _snapTolerance;

            return double.IsNaN(c.Z)
                ? new Coordinate(x, y)
                : new CoordinateZ(x, y, c.Z);
        }

        //private static int ExtractFromRing(
        //    Coordinate[] coords, int sourceId,
        //    List<RawSegment> result, int segId) {
        //    for (int i = 0; i < coords.Length - 1; i++) {
        //        var p0 = coords[i];
        //        var p1 = coords[i + 1];

        //        // Canonicalize: always store the "smaller" coordinate first
        //        bool reversed = ComparePoints(p0, p1) > 0;
        //        var (c0, c1) = reversed ? (p1, p0) : (p0, p1);

        //        result.Add(new RawSegment(segId++, c0, c1, sourceId, reversed));
        //    }
        //    return segId;
        //}

        /// <summary>
        /// Snaps a coordinate to the nearest coordinate in a reference set,
        /// if one exists within tolerance. Falls back to grid-snap otherwise.
        /// </summary>
        private Coordinate SnapToNearestExisting(
            Coordinate c,
            Dictionary<CoordinateKey, Coordinate> existingByKey,
            double tolerance) {
            var key = new CoordinateKey(SnapToGrid(c), tolerance);

            if (existingByKey.TryGetValue(key, out var existing))
                return existing;

            // Check neighboring cells too — handles the "1 cell apart" case
            foreach (var dx in new[] { -1, 0, 1 })
                foreach (var dy in new[] { -1, 0, 1 }) {
                    var neighborKey = key.Offset(dx, dy);
                    if (existingByKey.TryGetValue(neighborKey, out var neighbor)
                        && c.Distance(neighbor) <= tolerance) {
                        return neighbor;
                    }
                }

            return SnapToGrid(c);
        }

        private static int ComparePoints(Coordinate a, Coordinate b) {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        }

        private int EstimateSegmentCount() => this._sources.Sum(s => s.Geometry.NumPoints);

        // -------------------------------------------------------------------------
        // Graph insertion
        // -------------------------------------------------------------------------
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
            bool sourceGoesStartToEnd = c0.Equals2D(edge.StartNode, _snapTolerance);
            edge.SourceOrientation[sourceId] = sourceGoesStartToEnd;

            if (!_edgesBySource.TryGetValue(sourceId, out var list))
                _edgesBySource[sourceId] = list = new List<NetworkEdge>();
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
                Touch(edge.StartNode!, edge);
                Touch(edge.EndNode!, edge);
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
            var forward = Walk(seed.EndNode!, seed, edgeSet, adjacency, visited);
            var backward = Walk(seed.StartNode!, seed, edgeSet, adjacency, visited);

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
                var key = new CoordinateKey(currentCoord!, _snapTolerance);
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
                currentCoord = nextEdge.StartNode!.Equals2D(currentCoord, _snapTolerance)
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
                return chain[0].Geometry!.Coordinates.ToArray();

            var coords = new List<Coordinate>();

            for (int i = 0; i < chain.Count; i++) {
                var edge = chain[i];
                var edgeCs = edge.Geometry!.Coordinates;

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
            return coord!.Equals2D(other.StartNode, _snapTolerance)
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
        public readonly bool OriginallyReversed;
        public readonly Envelope Envelope;

        public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId, bool reversed) {
            SegId = segId; P0 = p0; P1 = p1; SourceId = sourceId;
            OriginallyReversed = reversed;
            Envelope = new Envelope(p0, p1);
        }

        public RawSegment WithCoordinates(Coordinate p0, Coordinate p1)
            => new RawSegment(SegId, p0, p1, SourceId, OriginallyReversed);

        public RawSegment WithSegId(int segId)
            => new RawSegment(segId, P0, P1, SourceId, OriginallyReversed);
    }

    //internal sealed class RawSegment
    //{
    //    public readonly int SegId;
    //    public readonly Coordinate P0, P1;
    //    public readonly int SourceId;
    //    public readonly bool OriginallyReversed; // true if source digitized P1->P0
    //    public readonly Envelope Envelope;

    //    public RawSegment(int segId, Coordinate p0, Coordinate p1, int sourceId, bool reversed) {
    //        SegId = segId; P0 = p0; P1 = p1; SourceId = sourceId;
    //        OriginallyReversed = reversed;
    //        Envelope = new Envelope(p0, p1);
    //    }
    //}


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
    //internal readonly struct CoordinateKey : IEquatable<CoordinateKey>,
    //                                       IComparable<CoordinateKey>
    //{
    //    private readonly long _x, _y;

    //    public CoordinateKey(Coordinate c, double tolerance) {
    //        //double inv = 1.0 / tolerance;
    //        //this._x = (long)Math.Round(c.X * inv);
    //        //this._y = (long)Math.Round(c.Y * inv);

    //        double inv = 1.0 / tolerance;
    //        const double epsilon = 1e-9;

    //        _x = (long)Math.Floor(c.X * inv + epsilon);
    //        _y = (long)Math.Floor(c.Y * inv + epsilon);
    //    }

    //    public bool Equals(CoordinateKey other) => this._x == other._x && this._y == other._y;
    //    public override bool Equals(object obj) => obj is CoordinateKey k && this.Equals(k);
    //    public override int GetHashCode() => HashCode.Combine(this._x, this._y);
    //    public int CompareTo(CoordinateKey other) {
    //        int c = this._x.CompareTo(other._x);
    //        return c != 0 ? c : this._y.CompareTo(other._y);
    //    }
    //}

    internal readonly struct CoordinateKey : IEquatable<CoordinateKey>, IComparable<CoordinateKey>
    {
        private readonly long _x, _y;

        public CoordinateKey(Coordinate snappedCoord, double tolerance) {
            double inv = 1.0 / tolerance;
            _x = (long)Math.Round(snappedCoord.X * inv);
            _y = (long)Math.Round(snappedCoord.Y * inv);
        }

        private CoordinateKey(long x, long y) { _x = x; _y = y; }

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
