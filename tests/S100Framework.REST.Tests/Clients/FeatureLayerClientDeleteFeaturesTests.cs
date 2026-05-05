using NetTopologySuite.Geometries;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientDeleteFeaturesTests
{
    [Fact]
    public async Task DeleteFeaturesAsync_PostsObjectIdsAndMapsDeleteResults() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/deleteFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "deleteResults": [
                { "success": true, "objectId": 10 },
                {
                  "success": false,
                  "objectId": 11,
                  "error": {
                    "code": 1005,
                    "description": "The specified feature could not be deleted or does not exist."
                  }
                }
              ],
              "editMoment": 1457994488000
            }
            """);
        });

        var result = await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                ObjectIds = [10, 11],
                GdbVersion = "SDE.DEFAULT",
                RollbackOnFailure = false,
                ReturnDeleteResults = true,
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1457994488000, result.EditMoment);
        Assert.Equal(2, result.DeleteResults.Count);

        Assert.True(result.DeleteResults[0].Success);
        Assert.Equal(10, result.DeleteResults[0].ObjectId);

        Assert.False(result.DeleteResults[1].Success);
        Assert.Equal(11, result.DeleteResults[1].ObjectId);
        Assert.Equal(1005, result.DeleteResults[1].ErrorCode);
        Assert.Equal(
            "The specified feature could not be deleted or does not exist.",
            result.DeleteResults[1].ErrorDescription);

        var form = ParseFormBody(requestBody!);

        Assert.Equal("json", form["f"]);
        Assert.Equal("10,11", form["objectIds"]);
        Assert.Equal("SDE.DEFAULT", form["gdbVersion"]);
        Assert.Equal("false", form["rollbackOnFailure"]);
        Assert.Equal("true", form["returnDeleteResults"]);
        Assert.Equal("true", form["returnEditMoment"]);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_PostsWhereAndSpatialFilterAndMapsSuccessResponse() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(
                "/FeatureServer/0/deleteFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "success": true,
              "editMoment": 1457994489000
            }
            """);
        });

        var result = await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                Where = "STATUS = 'Obsolete'",
                SpatialFilter = FeatureSpatialFilter.FromEnvelope(
                    new Envelope(10, 11, 55, 56),
                    4326),
                ReturnDeleteResults = false,
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Empty(result.DeleteResults);
        Assert.Equal(1457994489000, result.EditMoment);

        var form = ParseFormBody(requestBody!);

        Assert.Equal("json", form["f"]);
        Assert.Equal("STATUS = 'Obsolete'", form["where"]);
        Assert.Equal("esriGeometryEnvelope", form["geometryType"]);
        Assert.Equal("4326", form["inSR"]);
        Assert.Equal("esriSpatialRelIntersects", form["spatialRel"]);
        Assert.Contains("\"xmin\":10", form["geometry"], StringComparison.Ordinal);
        Assert.Equal("false", form["returnDeleteResults"]);
        Assert.Equal("true", form["returnEditMoment"]);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_DoesNotSendReturnEditMoment_WhenFalse() {
        var cancellationToken = TestContext.Current.CancellationToken;
        string? requestBody = null;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/deleteFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            requestBody = ReadRequestBody(request);

            return StubHttpMessageHandler.Json("""
            {
              "success": true
            }
            """);
        });

        await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                ObjectIds = [10],
                ReturnEditMoment = false
            },
            cancellationToken);

        var form = ParseFormBody(requestBody!);

        Assert.False(form.ContainsKey("returnEditMoment"));
    }

    [Fact]
    public async Task DeleteFeaturesAsync_ReturnsFalse_WhenServerReportsFailureWithoutDeleteResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/deleteFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
              "success": false
            }
            """);
        });

        var result = await client.GetLayerClient(0).DeleteFeaturesAsync(
            new DeleteFeaturesRequest {
                ObjectIds = [10],
                ReturnDeleteResults = false
            },
            cancellationToken);

        Assert.False(result.Success);
        Assert.Empty(result.DeleteResults);
    }

    [Fact]
    public void ForObjectIds_CreatesRequestWithObjectIds() {
        var request = DeleteFeaturesRequest.ForObjectIds([10, 11]);

        Assert.Equal([10, 11], request.ObjectIds);
        Assert.Null(request.Where);
        Assert.Null(request.SpatialFilter);
        request.Validate();
    }

    [Fact]
    public void ForWhere_CreatesRequestWithWhereClause() {
        var request = DeleteFeaturesRequest.ForWhere("STATUS = 'Obsolete'");

        Assert.Null(request.ObjectIds);
        Assert.Equal("STATUS = 'Obsolete'", request.Where);
        Assert.Null(request.SpatialFilter);
        request.Validate();
    }

    [Fact]
    public void ForSpatialFilter_CreatesRequestWithSpatialFilter() {
        var spatialFilter = FeatureSpatialFilter.FromEnvelope(
            new Envelope(10, 11, 55, 56),
            4326);

        var request = DeleteFeaturesRequest.ForSpatialFilter(spatialFilter);

        Assert.Null(request.ObjectIds);
        Assert.Null(request.Where);
        Assert.Same(spatialFilter, request.SpatialFilter);
        request.Validate();
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenNoDeleteSelectorIsProvided() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest(),
                cancellationToken));

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Where", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SpatialFilter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenObjectIdsAreEmpty() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = []
                },
                cancellationToken));

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenObjectIdsContainNonPositiveValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = [10, 0]
                },
                cancellationToken));

        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenWhereIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    Where = " "
                },
                cancellationToken));

        Assert.Contains("Where", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenGdbVersionIsWhitespace() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = [10],
                    GdbVersion = " "
                },
                cancellationToken));

        Assert.Contains("GdbVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenResponseDoesNotContainSuccessOrDeleteResults() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            Assert.EndsWith(
                "/FeatureServer/0/deleteFeatures",
                request.RequestUri!.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);

            return StubHttpMessageHandler.Json("""
            {
            }
            """);
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = [10]
                },
                cancellationToken));

        Assert.Contains("deleteFeatures", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteFeaturesAsync_Throws_WhenObjectIdsContainDuplicateValues() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).DeleteFeaturesAsync(
                new DeleteFeaturesRequest {
                    ObjectIds = [10, 10]
                },
                cancellationToken));

        Assert.Contains("ObjectIds", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static Dictionary<string, string> ParseFormBody(string body) {
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0].Replace("+", " ", StringComparison.Ordinal)),
                parts => parts.Length > 1
                    ? Uri.UnescapeDataString(parts[1].Replace("+", " ", StringComparison.Ordinal))
                    : string.Empty,
                StringComparer.Ordinal);
    }
}