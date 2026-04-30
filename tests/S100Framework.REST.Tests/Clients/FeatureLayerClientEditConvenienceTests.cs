using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientEditConvenienceTests
{
    [Fact]
    public async Task AddFeaturesAsync_WrapsApplyEditsAndReturnsAddResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            requestBody = ReadRequestBody(request);

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/applyEdits",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [
                { "success": true, "objectId": 101 }
              ],
              "updateResults": [],
              "deleteResults": []
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var results = await client.GetLayerClient(0).AddFeaturesAsync(
            [
                new EditableFeature(
                    geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
                    new Dictionary<string, object?> {
                        ["NAME"] = "Added feature"
                    })
            ],
            cancellationToken: cancellationToken);

        var result = Assert.Single(results);

        Assert.True(result.Success);
        Assert.Equal(101, result.ObjectId);

        Assert.NotNull(requestBody);
        Assert.Contains("adds=", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("updates=", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("deletes=", requestBody, StringComparison.Ordinal);
        Assert.Contains("rollbackOnFailure=true", requestBody, StringComparison.Ordinal);
        Assert.Contains("useGlobalIds=false", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_WrapsApplyEditsAndReturnsUpdateResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            requestBody = ReadRequestBody(request);

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/applyEdits",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [],
              "updateResults": [
                { "success": true, "objectId": 202 }
              ],
              "deleteResults": []
            }
            """);
        });

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        var results = await client.GetLayerClient(0).UpdateFeaturesAsync(
            [
                new EditableFeature(
                    geometryFactory.CreatePoint(new Coordinate(12.35, 56.79)),
                    new Dictionary<string, object?> {
                        ["OBJECTID"] = 202,
                        ["NAME"] = "Updated feature"
                    })
            ],
            new FeatureEditOptions {
                RollbackOnFailure = false,
                UseGlobalIds = true
            },
            cancellationToken);

        var result = Assert.Single(results);

        Assert.True(result.Success);
        Assert.Equal(202, result.ObjectId);

        Assert.NotNull(requestBody);
        Assert.DoesNotContain("adds=", requestBody, StringComparison.Ordinal);
        Assert.Contains("updates=", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("deletes=", requestBody, StringComparison.Ordinal);
        Assert.Contains("rollbackOnFailure=false", requestBody, StringComparison.Ordinal);
        Assert.Contains("useGlobalIds=true", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_WrapsApplyEditsAndReturnsDeleteResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            requestBody = ReadRequestBody(request);

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/applyEdits",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "addResults": [],
              "updateResults": [],
              "deleteResults": [
                { "success": true, "objectId": 303 },
                { "success": true, "objectId": 304 }
              ]
            }
            """);
        });

        var results = await client.GetLayerClient(0).DeleteFeaturesAsync(
            [303, 304],
            cancellationToken: cancellationToken);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Success);
        Assert.Equal(303, results[0].ObjectId);
        Assert.True(results[1].Success);
        Assert.Equal(304, results[1].ObjectId);

        Assert.NotNull(requestBody);
        Assert.DoesNotContain("adds=", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("updates=", requestBody, StringComparison.Ordinal);
        Assert.Contains("deletes=303%2C304", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFeaturesAsync_Throws_WhenFeaturesAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).AddFeaturesAsync(
                [],
                cancellationToken: cancellationToken));

        Assert.Contains("Adds, Updates, or Deletes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFeaturesAsync_Throws_WhenFeaturesAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).UpdateFeaturesAsync(
                [],
                cancellationToken: cancellationToken));

        Assert.Contains("Adds, Updates, or Deletes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenObjectIdsAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                [],
                cancellationToken: cancellationToken));

        Assert.Contains("Adds, Updates, or Deletes", exception.Message, StringComparison.Ordinal);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static string ReadRequestBody(HttpRequestMessage request) {
        return request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()
               ?? string.Empty;
    }
}