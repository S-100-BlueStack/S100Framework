using System.Net.Http;
using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientSpatialDistanceQueryTests
{
    [Fact]
    public async Task QueryAsync_IncludesSpatialDistanceAndUnits_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery {
                ReturnGeometry = false,
                SpatialFilter = FeatureSpatialFilter.FromGeometry(
                    new Point(12.34, 56.78) {
                        SRID = 4326
                    },
                    distance: 100,
                    distanceUnit: FeatureSpatialDistanceUnit.Meter)
            },
            cancellationToken)) {
        }

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("esriGeometryPoint", query["geometryType"]);
        Assert.Equal("esriSpatialRelIntersects", query["spatialRel"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.Equal("100", query["distance"]);
        Assert.Equal("esriSRUnit_Meter", query["units"]);
        Assert.True(query.ContainsKey("geometry"));
    }

    [Fact]
    public async Task QueryCountAsync_IncludesSpatialDistanceWithoutUnits_WhenUnitIsOmitted() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "count": 7
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var count = await client.GetLayerClient(0).QueryCountAsync(
            new FeatureQuery {
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    inSrid: 4326,
                    distance: 25)
            },
            cancellationToken);

        Assert.Equal(7, count);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnCountOnly"]);
        Assert.Equal("esriGeometryEnvelope", query["geometryType"]);
        Assert.Equal("4326", query["inSR"]);
        Assert.Equal("25", query["distance"]);
        Assert.False(query.ContainsKey("units"));
    }

    [Theory]
    [InlineData(FeatureSpatialDistanceUnit.Meter, "esriSRUnit_Meter")]
    [InlineData(FeatureSpatialDistanceUnit.StatuteMile, "esriSRUnit_StatuteMile")]
    [InlineData(FeatureSpatialDistanceUnit.Foot, "esriSRUnit_Foot")]
    [InlineData(FeatureSpatialDistanceUnit.Kilometer, "esriSRUnit_Kilometer")]
    [InlineData(FeatureSpatialDistanceUnit.NauticalMile, "esriSRUnit_NauticalMile")]
    [InlineData(FeatureSpatialDistanceUnit.UsNauticalMile, "esriSRUnit_USNauticalMile")]
    public async Task QueryObjectIdsAsync_MapsSpatialDistanceUnits(
        FeatureSpatialDistanceUnit unit,
        string expectedUnitParameter) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "objectIds": [10]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var objectIds = await client.GetLayerClient(0).QueryObjectIdsAsync(
            new FeatureQuery {
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    inSrid: 4326,
                    distance: 5,
                    distanceUnit: unit)
            },
            cancellationToken);

        Assert.Equal([10], objectIds);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnIdsOnly"]);
        Assert.Equal("5", query["distance"]);
        Assert.Equal(expectedUnitParameter, query["units"]);
    }

    [Fact]
    public async Task QueryExtentAsync_IncludesSpatialDistanceAndUnits_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "extent": {
                    "xmin": 10.0,
                    "ymin": 55.0,
                    "xmax": 11.0,
                    "ymax": 56.0,
                    "spatialReference": {
                      "wkid": 25832,
                      "latestWkid": 25832
                    }
                  }
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var extent = await client.GetLayerClient(0).QueryExtentAsync(
            new FeatureQuery {
                SpatialFilter = FeatureSpatialFilter.FromGeometry(
                    new Point(12.34, 56.78) {
                        SRID = 4326
                    },
                    distance: 2.5,
                    distanceUnit: FeatureSpatialDistanceUnit.Kilometer)
            },
            cancellationToken);

        Assert.NotNull(extent);
        Assert.Equal(25832, extent.Srid);

        var requestUri = Assert.Single(requestUris);
        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("true", query["returnExtentOnly"]);
        Assert.Equal("2.5", query["distance"]);
        Assert.Equal("esriSRUnit_Kilometer", query["units"]);
    }

    [Fact]
    public void FromGeometry_Throws_WhenDistanceIsNegative() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromGeometry(
                new Point(12.34, 56.78),
                distance: -1));

        Assert.Contains("Distance", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromGeometry_Throws_WhenDistanceIsNotFinite(double distance) {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromGeometry(
                new Point(12.34, 56.78),
                distance: distance));

        Assert.Contains("Distance", exception.Message, StringComparison.Ordinal);
        Assert.Contains("finite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromEnvelope_Throws_WhenInputSridIsNotPositive() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 11, 55, 56),
                inSrid: 0));

        Assert.Contains("InSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromGeometry_Throws_WhenExplicitInputSridIsNotPositive() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromGeometry(
                new Point(12.34, 56.78),
                inSrid: -1));

        Assert.Contains("InSrid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromGeometry_Throws_WhenSpatialRelationshipIsInvalid() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromGeometry(
                new Point(12.34, 56.78),
                spatialRelationship: (SpatialRelationship)999));

        Assert.Contains("SpatialRelationship", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromEnvelope_Throws_WhenDistanceUnitIsInvalid() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 11, 55, 56),
                inSrid: 4326,
                distance: 10,
                distanceUnit: (FeatureSpatialDistanceUnit)999));

        Assert.Contains("DistanceUnit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromEnvelope_Throws_WhenDistanceUnitIsProvidedWithoutDistance() {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeatureSpatialFilter.FromEnvelope(
                new Envelope(10, 11, 55, 56),
                inSrid: 4326,
                distanceUnit: FeatureSpatialDistanceUnit.Meter));

        Assert.Contains("DistanceUnit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Distance", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_IncludesZeroSpatialDistance_WhenProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsLayerMetadataRequest(request)) {
                return CreateLayerMetadataResponse();
            }

            if (IsLayerQueryRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "objectIdFieldName": "OBJECTID",
                  "geometryType": "esriGeometryPoint",
                  "features": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        await foreach (var _ in client.GetLayerClient(0).QueryAsync(
            new FeatureQuery {
                SpatialFilter = FeatureSpatialFilter.FromGeometry(
                    new Point(12.34, 56.78) {
                        SRID = 4326
                    },
                    distance: 0,
                    distanceUnit: FeatureSpatialDistanceUnit.Meter)
            },
            cancellationToken)) {
        }

        var requestUri = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/0/query?", StringComparison.OrdinalIgnoreCase));

        var query = ParseQuery(new Uri(requestUri));

        Assert.Equal("0", query["distance"]);
        Assert.Equal("esriSRUnit_Meter", query["units"]);
    }

    [Fact]
    public void FromGeometry_UsesGeometrySrid_WhenInputSridIsOmitted() {
        var filter = FeatureSpatialFilter.FromGeometry(
            new Point(12.34, 56.78) {
                SRID = 25832
            },
            distance: 10,
            distanceUnit: FeatureSpatialDistanceUnit.Meter);

        Assert.Equal(25832, filter.InSrid);
        Assert.Equal(10, filter.Distance);
    }

    [Fact]
    public void FromGeometry_UsesExplicitInputSrid_WhenProvided() {
        var filter = FeatureSpatialFilter.FromGeometry(
            new Point(12.34, 56.78) {
                SRID = 25832
            },
            inSrid: 4326,
            distance: 10,
            distanceUnit: FeatureSpatialDistanceUnit.Meter);

        Assert.Equal(4326, filter.InSrid);
        Assert.Equal(10, filter.Distance);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer"),
                QueryRequestMethodPreference = QueryRequestMethodPreference.Get
            });
    }

    private static bool IsLayerMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsLayerQueryRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/0/query",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => DecodeFormValue(parts[0]),
                parts => parts.Length == 2 ? DecodeFormValue(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string DecodeFormValue(string value) {
        return Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));
    }

    private static HttpResponseMessage CreateLayerMetadataResponse() {
        return StubHttpMessageHandler.Json("""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true,
            "supportsQueryWithDistance": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": {
              "wkid": 4326,
              "latestWkid": 4326
            }
          }
        }
        """);
    }
}