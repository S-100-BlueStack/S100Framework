using System.Text.Json;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Models;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureLayerClientCalculateTests
{
    [Fact]
    public async Task CalculateAsync_PostsCalculateRequestAndMapsResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                Assert.Equal(HttpMethod.Post, request.Method);

                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "updatedFeatureCount": 51,
                  "editMoment": 1457994488000
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetLayerClient(0).CalculateAsync(
            new CalculateRequest {
                Where = "STATUS = 'Active'",
                Expressions = [
                    CalculateExpression.ForValue("QUALITY", 3),
                    CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2"),
                    CalculateExpression.ForNull("REVIEWED_AT")
                ],
                SqlFormat = FeatureQuerySqlFormat.Standard,
                GdbVersion = "SDE.DEFAULT",
                SessionId = "{E81C2E2D-C6A7-40CB-BF61-FB499E53DD1D}",
                ReturnEditMoment = true
            },
            cancellationToken);

        Assert.True(result.Success);
        Assert.Equal(51, result.UpdatedFeatureCount);
        Assert.Equal(1457994488000, result.EditMoment);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("json", form["f"]);
        Assert.Equal("STATUS = 'Active'", form["where"]);
        Assert.Equal("standard", form["sqlFormat"]);
        Assert.Equal("SDE.DEFAULT", form["gdbVersion"]);
        Assert.Equal("{E81C2E2D-C6A7-40CB-BF61-FB499E53DD1D}", form["sessionID"]);
        Assert.Equal("true", form["returnEditMoment"]);

        using var expressionDocument = JsonDocument.Parse(form["calcExpression"]);
        var expressions = expressionDocument.RootElement;

        Assert.Equal(3, expressions.GetArrayLength());
        Assert.Equal("QUALITY", expressions[0].GetProperty("field").GetString());
        Assert.Equal(3, expressions[0].GetProperty("value").GetInt32());

        Assert.Equal("SCORE", expressions[1].GetProperty("field").GetString());
        Assert.Equal("BASE_SCORE * 2", expressions[1].GetProperty("sqlExpression").GetString());

        Assert.Equal("REVIEWED_AT", expressions[2].GetProperty("field").GetString());
        Assert.Equal(JsonValueKind.Null, expressions[2].GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task CalculateAsync_DoesNotSendSqlFormat_WhenSqlFormatIsNone() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "updatedFeatureCount": 1
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.GetLayerClient(0).CalculateAsync(
            new CalculateRequest {
                Where = "OBJECTID = 1",
                Expressions = [
                    CalculateExpression.ForValue("QUALITY", 4)
                ],
                SqlFormat = FeatureQuerySqlFormat.None
            },
            cancellationToken);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.False(form.ContainsKey("sqlFormat"));
    }

    [Fact]
    public async Task CalculateAsync_SendsNativeSqlFormat_WhenRequested() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "updatedFeatureCount": 1
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.GetLayerClient(0).CalculateAsync(
            new CalculateRequest {
                Where = "OBJECTID = 1",
                Expressions = [
                    CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2")
                ],
                SqlFormat = FeatureQuerySqlFormat.Native
            },
            cancellationToken);

        var form = ParseFormBody(Assert.Single(requestBodies));

        Assert.Equal("native", form["sqlFormat"]);
    }

    [Fact]
    public async Task CalculateAsync_SerializesNullScalarValue() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestBodies = new List<string>();

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: true);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0/calculate",
                StringComparison.OrdinalIgnoreCase)) {
                requestBodies.Add(ReadRequestBody(request));

                return StubHttpMessageHandler.Json("""
                {
                  "success": true,
                  "updatedFeatureCount": 1
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        await client.GetLayerClient(0).CalculateAsync(
            new CalculateRequest {
                Where = "OBJECTID = 1",
                Expressions = [
                    CalculateExpression.ForNull("REVIEWED_AT")
                ]
            },
            cancellationToken);

        var form = ParseFormBody(Assert.Single(requestBodies));

        using var expressionDocument = JsonDocument.Parse(form["calcExpression"]);
        var expression = expressionDocument.RootElement[0];

        Assert.Equal("REVIEWED_AT", expression.GetProperty("field").GetString());
        Assert.Equal(JsonValueKind.Null, expression.GetProperty("value").ValueKind);
        Assert.False(expression.TryGetProperty("sqlExpression", out _));
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenValueExpressionAlsoHasSqlExpression() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Expressions = [
                        new CalculateExpression {
                            Field = "QUALITY",
                            Kind = CalculateExpressionKind.Value,
                            Value = 3,
                            SqlExpression = "BASE_SCORE * 2"
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("SqlExpression", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenSqlExpressionAlsoHasValue() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Expressions = [
                        new CalculateExpression {
                            Field = "SCORE",
                            Kind = CalculateExpressionKind.SqlExpression,
                            SqlExpression = "BASE_SCORE * 2",
                            Value = 3
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("Value", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenSqlExpressionIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Expressions = [
                        new CalculateExpression {
                            Field = "SCORE",
                            Kind = CalculateExpressionKind.SqlExpression
                        }
                    ]
                },
                cancellationToken));

        Assert.Contains("SqlExpression", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenLayerDoesNotAdvertiseCalculateSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: false);
            }

            throw new InvalidOperationException("HTTP should not be called after schema lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest {
                    Expressions = [
                        CalculateExpression.ForValue("QUALITY", 3)
                    ]
                },
                cancellationToken));

        Assert.Contains("calculate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateAsync_Throws_WhenExpressionsAreMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ =>
            throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetLayerClient(0).CalculateAsync(
                new CalculateRequest(),
                cancellationToken));

        Assert.Contains("expression", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSchemaAsync_MapsCalculateCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (request.RequestUri!.AbsolutePath.EndsWith(
                "/FeatureServer/0",
                StringComparison.OrdinalIgnoreCase)) {
                return CreateLayerMetadataResponse(supportsCalculate: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var schema = await client.GetLayerClient(0).GetSchemaAsync(cancellationToken);

        Assert.True(schema.Capabilities.SupportsCalculate);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static HttpResponseMessage CreateLayerMetadataResponse(bool supportsCalculate) {
        return StubHttpMessageHandler.Json($$"""
        {
          "id": 0,
          "name": "Layer 0",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "supportsCalculate": {{supportsCalculate.ToString().ToLowerInvariant()}},
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
            { "name": "QUALITY", "type": "esriFieldTypeInteger", "nullable": true },
            { "name": "SCORE", "type": "esriFieldTypeDouble", "nullable": true },
            { "name": "REVIEWED_AT", "type": "esriFieldTypeDate", "nullable": true }
          ],
          "relationships": [],
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """);
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