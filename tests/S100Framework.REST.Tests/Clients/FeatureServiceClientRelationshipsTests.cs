using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientRelationshipsTests
{
    [Fact]
    public async Task GetRelationshipsAsync_GetsRelationshipsResourceAndMapsResult() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<Uri>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                Assert.Equal(HttpMethod.Get, request.Method);

                return StubHttpMessageHandler.Json("""
                {
                  "relationships": [
                    {
                      "id": 7,
                      "name": "county_division",
                      "catalogID": "{A14D41FC-ABB5-4E9C-86BC-BB0290D5B331}",
                      "backwardPathLabel": "belongs",
                      "originLayerId": 0,
                      "originPrimaryKey": "GlobalID",
                      "forwardPathLabel": "has",
                      "destinationLayerId": 2,
                      "originForeignKey": "GlobalID_sor",
                      "relationshipTableId": 3,
                      "destinationPrimaryKey": "GlobalID",
                      "destinationForeignKey": "GlobalID_des",
                      "rules": [
                        {
                          "ruleID": 1,
                          "originSubtypeCode": 10,
                          "originMinimumCardinality": 0,
                          "originMaximumCardinality": 2,
                          "destinationSubtypeCode": 20,
                          "destinationMinimumCardinality": 0,
                          "destinationMaximumCardinality": 1
                        }
                      ],
                      "cardinality": "esriRelCardinalityOneToMany",
                      "attributed": true,
                      "composite": false
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        var relationship = Assert.Single(result.Relationships);

        Assert.Equal(7, relationship.Id);
        Assert.Equal("county_division", relationship.Name);
        Assert.Equal("{A14D41FC-ABB5-4E9C-86BC-BB0290D5B331}", relationship.CatalogId);
        Assert.Equal("belongs", relationship.BackwardPathLabel);
        Assert.Equal(0, relationship.OriginLayerId);
        Assert.Equal("GlobalID", relationship.OriginPrimaryKey);
        Assert.Equal("has", relationship.ForwardPathLabel);
        Assert.Equal(2, relationship.DestinationLayerId);
        Assert.Equal("GlobalID_sor", relationship.OriginForeignKey);
        Assert.Equal(3, relationship.RelationshipTableId);
        Assert.Equal("GlobalID", relationship.DestinationPrimaryKey);
        Assert.Equal("GlobalID_des", relationship.DestinationForeignKey);
        Assert.Equal("esriRelCardinalityOneToMany", relationship.Cardinality);
        Assert.True(relationship.Attributed);
        Assert.False(relationship.Composite);

        var rule = Assert.Single(relationship.Rules);

        Assert.Equal(1, rule.RuleId);
        Assert.Equal(10, rule.OriginSubtypeCode);
        Assert.Equal(0, rule.OriginMinimumCardinality);
        Assert.Equal(2, rule.OriginMaximumCardinality);
        Assert.Equal(20, rule.DestinationSubtypeCode);
        Assert.Equal(0, rule.DestinationMinimumCardinality);
        Assert.Equal(1, rule.DestinationMaximumCardinality);

        var relationshipsRequest = Assert.Single(
            requestUris,
            uri => uri.AbsolutePath.EndsWith(
                "/FeatureServer/relationships",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal("json", ParseQuery(relationshipsRequest)["f"]);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ReturnsEmptyList_WhenNoRelationshipsAreReturned() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": []
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        Assert.Empty(result.Relationships);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ReturnsEmptyList_WhenRelationshipsPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        Assert.Empty(result.Relationships);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ReturnsEmptyList_WhenRelationshipsPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        Assert.Empty(result.Relationships);
    }

    [Fact]
    public async Task GetRelationshipsAsync_IgnoresNullRelationshipItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": [
                    null,
                    {
                      "id": 7,
                      "name": "county_division"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        var relationship = Assert.Single(result.Relationships);

        Assert.Equal(7, relationship.Id);
        Assert.Equal("county_division", relationship.Name);
        Assert.Empty(relationship.Rules);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ReturnsEmptyRules_WhenRulesPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": [
                    {
                      "id": 7,
                      "name": "county_division"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        var relationship = Assert.Single(result.Relationships);

        Assert.Equal(7, relationship.Id);
        Assert.Empty(relationship.Rules);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ReturnsEmptyRules_WhenRulesPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": [
                    {
                      "id": 7,
                      "name": "county_division",
                      "rules": null
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        var relationship = Assert.Single(result.Relationships);

        Assert.Equal(7, relationship.Id);
        Assert.Empty(relationship.Rules);
    }

    [Fact]
    public async Task GetRelationshipsAsync_IgnoresNullRuleItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "relationships": [
                    {
                      "id": 7,
                      "name": "county_division",
                      "rules": [
                        null,
                        {
                          "ruleID": 1,
                          "originSubtypeCode": 10,
                          "originMinimumCardinality": 0,
                          "originMaximumCardinality": 2
                        }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var result = await client.GetRelationshipsAsync(cancellationToken);

        var relationship = Assert.Single(result.Relationships);
        var rule = Assert.Single(relationship.Rules);

        Assert.Equal(1, rule.RuleId);
        Assert.Equal(10, rule.OriginSubtypeCode);
        Assert.Equal(0, rule.OriginMinimumCardinality);
        Assert.Equal(2, rule.OriginMaximumCardinality);
    }

    [Fact]
    public async Task GetRelationshipsAsync_Throws_WhenServiceDoesNotAdvertiseSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.GetRelationshipsAsync(cancellationToken));

        Assert.Contains("relationships", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMetadataAsync_MapsRelationshipsResourceCapability() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsRelationshipsResource);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ThrowsFeatureServiceException_WhenRelationshipIdIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "relationships": [
                {
                  "name": "county_division"
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetRelationshipsAsync(cancellationToken));

        Assert.Contains("relationship", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRelationshipsAsync_ThrowsFeatureServiceException_WhenRelationshipIdIsNegative() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsRelationshipsResource: true);
            }

            if (IsRelationshipsRequest(request)) {
                return StubHttpMessageHandler.Json("""
            {
              "relationships": [
                {
                  "id": -1,
                  "name": "county_division"
                }
              ]
            }
            """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceException>(() =>
            client.GetRelationshipsAsync(cancellationToken));

        Assert.Contains("relationship", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static FeatureServiceClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler) {
        return new FeatureServiceClient(
            new HttpClient(new StubHttpMessageHandler(handler)),
            new FeatureServiceClientOptions {
                ServiceUri = new Uri("https://example.test/arcgis/rest/services/Test/FeatureServer")
            });
    }

    private static bool IsServiceMetadataRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsRelationshipsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/relationships",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsRelationshipsResource) {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [
            { "id": 0, "name": "Counties" },
            { "id": 2, "name": "Divisions" }
          ],
          "tables": [
            { "id": 3, "name": "CountyDivisionRelationships" }
          ],
          "capabilities": "Query",
          "supportsRelationshipsResource": {{supportsRelationshipsResource.ToString().ToLowerInvariant()}}
        }
        """);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.Ordinal);
    }
}