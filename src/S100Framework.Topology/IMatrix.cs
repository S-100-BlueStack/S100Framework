using NetTopologySuite.Geometries;
using NetTopologySuite.Precision;
using System.Text;

namespace S100FC.Topology
{
    public interface ITopologyBuilder
    {
        ITopologyBuilder AddTopologyFeatures(IList<S100FC.Topology.Polygon> surfaces, IList<S100FC.Topology.Polyline> curves);
        ITopologyBuilder AddGroup2Features(IList<S100FC.Topology.Polygon> surfaces, IList<S100FC.Topology.Polyline> curves);

#if Singletons
        ITopologyBuilder AddSingletonFeatures(ICollection<S100FC.Topology.Polyline> curves);
#endif

        GeometryPrecisionReducer Reducer { get; }

        IMatrix BuildTopology();
    }

    public interface IMatrix
    {
        IEnumerable<CurveFeature> Curves { get; }

        IEnumerable<CompositeCurveFeature> CompositeCurves { get; }

        IEnumerable<SurfaceFeature> Surfaces { get; }

        IDictionary<string, string> MappingFOID { get; }

        ICollection<string> Collapse { get; }

        string[] NetworkTopology { get; }

        string[] Geometries { get; }
    }
}

namespace S100FC.Topology
{
    public class FeatureRef
    {
        public UInt64 Id { get; init; }
        public bool Reverse { get; init; } = false;
    }

    public abstract class FeatureType
    {
        private static UInt64 counter = 1;

        public UInt64 Id { get; init; } = Interlocked.Increment(ref FeatureType.counter);
    }

    public class CurveFeature : FeatureType
    {
        public CurveFeature(LineString lineString, ulong hash) {
            this.LineString = lineString;
            this.LineStringReverse = lineString.Factory.CreateLineString([.. lineString.Coordinates.Reverse()]);

            this.LineStringText = lineString.ToString();
            this.LineStringReverseText = this.LineStringReverse.ToString();
            base.Id = hash;

            //if (base.Id == 52672787 || base.Id == 3587297466) System.Diagnostics.Debugger.Break();
            //if (base.Id == 2933884953) System.Diagnostics.Debugger.Break();
        }

        public CurveFeature(LineString lineString) {
            this.LineString = lineString;
            this.LineStringReverse = lineString.Factory.CreateLineString([.. lineString.Coordinates.Reverse()]);

            this.LineStringText = lineString.ToString();
            this.LineStringReverseText = this.LineStringReverse.ToString();

            base.Id = System.IO.Hashing.XxHash64.HashToUInt64(LineString.ToBinary());
            //base.Id = System.IO.Hashing.XxHash32.HashToUInt32(this.LineString.ToBinary());

            //if (base.Id == 11123348237682635517 || base.Id == 8819474955002669271) System.Diagnostics.Debugger.Break();
        }

        public LineString LineString { get; set; }

        public LineString LineStringReverse { get; set; }

        public string LineStringText { get; init; }
        public string LineStringReverseText { get; init; }

        public bool Equals(CurveFeature lineString) {
            if (lineString.LineStringText.Equals(this.LineStringText))
                return true;
            return false;
        }

        public bool Equals(LineString lineString) {
            if (lineString.ToString().Equals(this.LineStringText))
                return true;
            return false;
        }

        public override bool Equals(object? obj) {
            if (obj is CurveFeature curve)
                return (this.Equals(curve));
            if (obj is LineString lineString)
                return (this.Equals(lineString));
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return (int)System.IO.Hashing.XxHash64.HashToUInt64(this.LineString.ToBinary());
        }
    }

    public class CompositeCurveFeature : FeatureType
    {
        public CompositeCurveFeature(FeatureRef[] curves) {
            this.Curves = curves;

            base.Id = System.IO.Hashing.XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(string.Join(',', curves.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"))));
            //base.Id = System.IO.Hashing.XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(string.Join(',', curves.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"))));

            this.Reverse = System.IO.Hashing.XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(string.Join(',', curves.Reverse().Select(e => !e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"))));

            //if (base.Id == 46947589) System.Diagnostics.Debugger.Break();
        }

        public ulong Reverse { get; init; }

        public FeatureRef[] Curves { get; init; } = [];
    }

    public class SurfaceFeature : FeatureType
    {
        public required FeatureRef Exterior { get; init; }

        public FeatureRef[]? Interior { get; set; } = default;

        public string? Ref { get; init; } = default;

        public LineString? LineString { get; set; } = default;

        public ICollection<UInt64>? Masks1 { get; set; } = default;
        public ICollection<UInt64>? Masks2 { get; set; } = default;
    }

    public record Polyline(long ObjectId, string Name, string Code, LineString LineString, string UID);

    public record Polygon(long ObjectId, string Name, string Code, LineString ExteriorRing, LineString[] InteriorRings) : Polyline(ObjectId, Name, Code, ExteriorRing, Name);

    public class CompositeCurveContainer
    {
        private readonly Dictionary<UInt64, CompositeCurveFeature> _feature = [];
        private readonly Dictionary<string, (UInt64 Id, bool Reverse)> _keys = [];

        public ICollection<CompositeCurveFeature> CompositeCurveFeatures => this._feature.Values;

        public (UInt64 Id, bool Reverse) AddOrGet(IList<FeatureRef> sortedList) {
            var keyStraight = string.Join(',', sortedList.Select(e => e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"));
            var keyReverse = string.Join(',', sortedList.Reverse().Select(e => !e.Reverse ? $"RC{e.Id}" : $"C{e.Id}"));

            var compositeCurve = new CompositeCurveFeature([.. sortedList]) {
            };

            lock (this) {
                if (this._keys.ContainsKey(keyStraight)) {
                    var value = this._keys[keyStraight];
                    return (value.Id, value.Reverse);
                }
                this._feature.Add(compositeCurve.Id, compositeCurve);
                this._keys.Add(keyStraight, (compositeCurve.Id, false));
                this._keys.Add(keyReverse, (compositeCurve.Id, true));

                return (compositeCurve.Id, false);
            }
        }
    }

}