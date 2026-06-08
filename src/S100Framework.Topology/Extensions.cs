namespace GeoAPI.Geometries
{
    using NetTopologySuite.Geometries;

    public static class Extensions
    {
        public static LineString RemoveRepeatedVertices(this LineString lineString) {
            var coordinates = lineString.Coordinates.RemoveRepeatedVertices();
            if (coordinates.Length != lineString.Count)
                return lineString.Factory.CreateLineString(coordinates.ToArray());
            return lineString;
        }

        public static Coordinate[] RemoveRepeatedVertices(this Coordinate[] coordinates) {
            var _ = new List<Coordinate> { coordinates[0] };

            for (int i = 1; i < coordinates.Length; i++) {
                if (coordinates[i - 1].Equals(coordinates[i])) continue;
                _.Add(coordinates[i]);
            }
            return _.ToArray();
        }

        public static LineString RemoveCollinearVertices(this LineString line) {
            return line;
            var coords = line.Coordinates;
            var keep = new List<Coordinate> { coords[0] };

            for (int i = 1; i < coords.Length - 1; i++) {
                var prev = keep[^1];
                var curr = coords[i];
                var next = coords[i + 1];

                // Cross product of (curr-prev) x (next-prev); non-zero = not collinear
                double cross = (curr.X - prev.X) * (next.Y - prev.Y)
                             - (curr.Y - prev.Y) * (next.X - prev.X);

                if (Math.Abs(cross) > 1e-10)
                    keep.Add(curr);
            }

            keep.Add(coords[^1]); // always keep the last point
            return line.Factory.CreateLineString(keep.ToArray());
        }
    }
}
