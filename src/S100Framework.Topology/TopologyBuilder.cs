using System;
using System.Collections.Generic;
using System.Text;

namespace S100Framework.Topology
{
    using NetTopologySuite.Geometries;
    using NetTopologySuite.Noding;
    using NetTopologySuite.Noding.Snapround;
    using NetTopologySuite.Operation.Linemerge;
    using NetTopologySuite.Operation.Overlay.Snap;
    using NetTopologySuite.Operation.Union;
    using NetTopologySuite.Planargraph;
    using NetTopologySuite.Precision;
    using S100Framework.Topology.Internal;
    using System;
    using System.Collections.Generic;
    using System.Linq;


    public class TopologyGraph : PlanarGraph
    {
        public TopologyGraph() : base() {
        }

        public void AddEdge(Edge edge) => base.Add(edge);

        public void AddNode(Node node) => base.Add(node);
    }

    public class TopologyBuilder
    {
        private readonly PrecisionModel _pm;
        private readonly GeometryFactory _gf;

        public List<LineString> MergedEdges { get; private set; } = new();
        public TopologyGraph Graph { get; private set; } = new();

        private Dictionary<Coordinate, List<LineString>> _nodeEdges = new();

        public TopologyBuilder(int srid = 4326) {
            _pm = new PrecisionModel(1_000_000); // ✅ required precision
            _gf = new GeometryFactory(_pm, srid);
        }

        // ============================================================
        // BUILD TOPOLOGY (handles near-intersections robustly)
        // ============================================================
        public void Build(IEnumerable<Geometry> inputGeometries) {
            var inputs = inputGeometries.ToList();

            if (!inputs.Any())
                return;

            // --------------------------------------------------------
            // 1. SNAP NEAR-INTERSECTIONS (CRITICAL)
            // --------------------------------------------------------
            double snapTolerance = 5.0 / _pm.Scale;

            Geometry unioned = UnaryUnionOp.Union(inputs);

            // Snap to self to fix almost-touching segments
            var snapped = GeometrySnapper.SnapToSelf(
                unioned,
                snapTolerance,
                true
            );

            // --------------------------------------------------------
            // 2. REDUCE TO PRECISION GRID
            // --------------------------------------------------------
            var reducer = new GeometryPrecisionReducer(_pm);
            var reduced = reducer.Reduce(snapped);

            // --------------------------------------------------------
            // 3. NODE USING SNAP-ROUNDING
            // --------------------------------------------------------
            var noder = new SnapRoundingNoder(_pm);

            var segStrings = new List<ISegmentString>();
            int id = 0;

            foreach (var line in ExtractLines(reduced)) {
                segStrings.Add(new NodedSegmentString(line.Coordinates, id++));
            }

            noder.ComputeNodes(segStrings);

            var noded = noder.GetNodedSubstrings();

            var nodedLines = noded
                .Select(s => _gf.CreateLineString(s.Coordinates))
                .ToList();

            // --------------------------------------------------------
            // 4. MERGE INTO MAXIMAL EDGES
            // --------------------------------------------------------
            var merger = new LineMerger();
            merger.Add(nodedLines);

            MergedEdges = merger.GetMergedLineStrings()
                .Cast<LineString>()
                .ToList();

            // --------------------------------------------------------
            // 5. BUILD PLANAR GRAPH
            // --------------------------------------------------------
            BuildGraph(MergedEdges);

            foreach (var edge in this.MergedEdges) {
                var start = edge.GetCoordinateN(0);
                var end = edge.GetCoordinateN(edge.NumPoints - 1);

                if (!_nodeEdges.ContainsKey(start))
                    _nodeEdges[start] = new List<LineString>();

                if (!_nodeEdges.ContainsKey(end))
                    _nodeEdges[end] = new List<LineString>();

                _nodeEdges[start].Add(edge);
                _nodeEdges[end].Add(edge);
            }
        }


        public List<(LineString edge, bool forward)> GetEdgesForInput(LineString input) {

            input = NormalizeInput(input);            


            var result = new List<(LineString, bool)>();

            var coords = input.Coordinates;
            int i = 0;

            while (i < coords.Length - 1) {
                var current = coords[i];
                var next = coords[i + 1];

                // find candidate edges from current node
                if (!_nodeEdges.TryGetValue(current, out var candidates))
                    throw new Exception("Node not found");

                LineString matchedEdge = null;
                bool forward = true;

                foreach (var edge in candidates) {
                    var ec = edge.Coordinates;

                    // Check if edge starts or ends at current
                    if (ec[0].Equals2D(current)) {
                        if (ec.Length > 1 && ec[1].Equals2D(next)) {
                            matchedEdge = edge;
                            forward = true;
                            break;
                        }
                    }
                    else if (ec[^1].Equals2D(current)) {
                        if (ec.Length > 1 && ec[^2].Equals2D(next)) {
                            matchedEdge = edge;
                            forward = false;
                            break;
                        }
                    }
                }

                if (matchedEdge == null)
                    throw new Exception("No matching edge found");

                // --------------------------------------------------
                // ✅ CRITICAL PART:
                // Consume as much of input as this edge covers
                // --------------------------------------------------

                var edgeCoords = matchedEdge.Coordinates;
                var edgeSeq = forward ? edgeCoords : edgeCoords.Reverse().ToArray();

                int j = i;
                int consumed = 0;

                while (j < coords.Length && consumed < edgeSeq.Length) {
                    if (!coords[j].Equals2D(edgeSeq[consumed]))
                        break;

                    j++;
                    consumed++;
                }

                if (consumed < 2)
                    throw new Exception("Edge match failed");

                result.Add((matchedEdge, forward));

                // Advance input index
                i += consumed - 1;
            }

            return result;
        }



        private LineString NormalizeInput(LineString input) {
            double snapTolerance = 5.0 / _pm.Scale;
            var snapped = GeometrySnapper.SnapToSelf(input, snapTolerance, true);
            return (LineString)new GeometryPrecisionReducer(_pm).Reduce(snapped);
        }


        // ============================================================
        // GRAPH BUILDING
        // ============================================================
        private void BuildGraph(IEnumerable<LineString> edges) {
            Graph = new TopologyGraph();

            foreach (var ls in edges) {
                var coords = ls.Coordinates;

                var startNode = GetOrCreateNode(coords.First());
                var endNode = GetOrCreateNode(coords.Last());

                var edge = new Edge();

                var de1 = new DirectedEdge(
                    startNode,
                    endNode,
                    coords[1],
                    true
                );

                var de2 = new DirectedEdge(
                    endNode,
                    startNode,
                    coords[^2],
                    false
                );

                edge.SetDirectedEdges(de1, de2);
                edge.Data = ls; // store geometry

                Graph.AddEdge(edge);
            }
        }

        private Node GetOrCreateNode(Coordinate c) {
            var existing = Graph.FindNode(c);

            if (existing != null)
                return existing;

            var node = new Node(c);
            Graph.AddNode(node);
            return node;
        }

        // ============================================================
        // RECONSTRUCT LINESTRING
        // ============================================================
        public LineString ReconstructLine(Coordinate start, Coordinate end) {
            var coords = Traverse(start, end, false);
            return _gf.CreateLineString(coords.ToArray());
        }

        // ============================================================
        // RECONSTRUCT LINEARRING
        // ============================================================
        public LinearRing ReconstructRing(Coordinate start) {
            var coords = Traverse(start, start, true);

            if (!coords.First().Equals2D(coords.Last()))
                coords.Add(coords.First());

            return _gf.CreateLinearRing(coords.ToArray());
        }

        // ============================================================
        // CORE GRAPH TRAVERSAL
        // ============================================================
        private List<Coordinate> Traverse(
            Coordinate start,
            Coordinate end,
            bool isRing) {
            var result = new List<Coordinate> { start };

            var current = Graph.FindNode(start);
            DirectedEdge previous = null;

            while (true) {
                var next = GetNextEdge(current, previous);

                if (next == null)
                    break;

                var edge = (LineString)next.Edge.Data;
                var coords = edge.Coordinates;

                // Ensure correct direction
                if (!coords.First().Equals2D(current.Coordinate))
                    coords = coords.Reverse().ToArray();

                // append (skip duplicate)
                for (int i = 1; i < coords.Length; i++)
                    result.Add(coords[i]);

                var nextNode = next.ToNode;

                if (!isRing && nextNode.Coordinate.Equals2D(end))
                    break;

                if (isRing && nextNode.Coordinate.Equals2D(start))
                    break;

                previous = next;
                current = nextNode;
            }

            return result;
        }

        private DirectedEdge GetNextEdge(Node node, DirectedEdge previous) {
            foreach (DirectedEdge de in node.OutEdges) {
                if (previous != null && de == previous.Sym)
                    continue;

                return de;
            }
            return null;
        }

        // ============================================================
        // HELPER: Extract lines from any geometry
        // ============================================================
        private List<LineString> ExtractLines(Geometry geom) {
            var result = new List<LineString>();

            switch (geom) {
                case LineString ls:
                    result.Add(ls);
                    break;

                case MultiLineString mls:
                    for (int i = 0; i < mls.NumGeometries; i++)
                        result.Add((LineString)mls.GetGeometryN(i));
                    break;

                case Polygon poly:
                    result.Add(poly.ExteriorRing);
                    for (int i = 0; i < poly.NumInteriorRings; i++)
                        result.Add(poly.GetInteriorRingN(i));
                    break;

                case GeometryCollection gc:
                    foreach (var g in gc.Geometries)
                        result.AddRange(ExtractLines(g));
                    break;
            }

            return result;
        }
    }
}
