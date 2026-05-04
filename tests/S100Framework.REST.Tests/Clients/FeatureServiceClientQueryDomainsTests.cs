using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Tests.TestDoubles;
using Xunit;

namespace S100Framework.REST.Tests.Clients;

public sealed class FeatureServiceClientQueryDomainsTests
{
    [Fact]
    public async Task GetMetadataAsync_MapsSupportsQueryDomains_WhenServiceAdvertisesIt() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(_ => StubHttpMessageHandler.Json("""
        {
          "layers": [],
          "tables": [],
          "capabilities": "Query",
          "syncEnabled": false,
          "supportsQueryDomains": true
        }
        """));

        var metadata = await client.GetMetadataAsync(cancellationToken);

        Assert.True(metadata.Capabilities.SupportsQueryDomains);
    }

    [Fact]
    public async Task QueryDomainsAsync_MapsRangeCodedValueAndInheritedDomains_WhenSupported() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            var uri = request.RequestUri!.AbsoluteUri;
            requestUris.Add(uri);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "domains": [
                    {
                      "type": "range",
                      "name": "DepthRange",
                      "fieldType": "esriFieldTypeDouble",
                      "range": [1, 100.5],
                      "mergePolicy": "esriMPTDefaultValue",
                      "splitPolicy": "esriSPTDefaultValue"
                    },
                    {
                      "type": "codedValue",
                      "name": "StatusDomain",
                      "fieldType": "esriFieldTypeString",
                      "codedValues": [
                        { "name": "Open", "code": "O" },
                        { "name": "Closed", "code": "C" }
                      ],
                      "mergePolicy": "esriMPTDefaultValue",
                      "splitPolicy": "esriSPTDefaultValue"
                    },
                    {
                      "type": "inherited"
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });

        var domains = await client.QueryDomainsAsync([0, 1], cancellationToken);

        Assert.Equal(3, domains.Count);

        var rangeDomain = domains[0];
        Assert.True(rangeDomain.IsRange);
        Assert.Equal("DepthRange", rangeDomain.Name);
        Assert.NotNull(rangeDomain.Range);
        Assert.Equal(1L, rangeDomain.Range!.MinimumValue);
        Assert.Equal(100.5d, rangeDomain.Range.MaximumValue);
        Assert.Empty(rangeDomain.CodedValues);

        var codedValueDomain = domains[1];
        Assert.True(codedValueDomain.IsCodedValue);
        Assert.Equal("StatusDomain", codedValueDomain.Name);
        Assert.Equal(2, codedValueDomain.CodedValues.Count);
        Assert.Equal("Open", codedValueDomain.CodedValues[0].Name);
        Assert.Equal("O", codedValueDomain.CodedValues[0].Code);

        var inheritedDomain = domains[2];
        Assert.True(inheritedDomain.IsInherited);

        var queryRequest = Assert.Single(
            requestUris,
            uri => uri.Contains("/FeatureServer/queryDomains?", StringComparison.OrdinalIgnoreCase));

        var decodedQueryRequest = Uri.UnescapeDataString(queryRequest);

        Assert.Contains("layers=[0,1]", decodedQueryRequest, StringComparison.Ordinal);
        Assert.Contains("f=json", decodedQueryRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryDomainsAsync_ReturnsEmptyList_WhenDomainsPropertyIsMissing() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("{}");
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var domains = await client.QueryDomainsAsync([0], cancellationToken);

        Assert.Empty(domains);
    }

    [Fact]
    public async Task QueryDomainsAsync_ReturnsEmptyList_WhenDomainsPropertyIsNull() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "domains": null
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var domains = await client.QueryDomainsAsync([0], cancellationToken);

        Assert.Empty(domains);
    }

    [Fact]
    public async Task QueryDomainsAsync_IgnoresNullDomainAndCodedValueItems() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "domains": [
                    null,
                    {
                      "type": "codedValue",
                      "name": "NullableDomain",
                      "fieldType": "esriFieldTypeInteger",
                      "codedValues": [
                        null,
                        { "name": "Active", "code": 1 }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var domain = Assert.Single(await client.QueryDomainsAsync([0], cancellationToken));

        var codedValue = Assert.Single(domain.CodedValues);
        Assert.Equal("Active", codedValue.Name);
        Assert.Equal(1L, codedValue.Code);
    }

    [Fact]
    public async Task QueryDomainsAsync_MapsScalarCodeShapes() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "domains": [
                    {
                      "type": "codedValue",
                      "name": "MixedCodeDomain",
                      "codedValues": [
                        { "name": "Boolean", "code": true },
                        { "name": "Null", "code": null },
                        { "name": "Object", "code": { "value": 1 } },
                        { "name": "Array", "code": [1, 2] }
                      ]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var domain = Assert.Single(await client.QueryDomainsAsync([0], cancellationToken));

        Assert.Equal(true, domain.CodedValues[0].Code);
        Assert.Null(domain.CodedValues[1].Code);
        Assert.Equal("""{"value":1}""", domain.CodedValues[2].Code);
        Assert.Equal("[1,2]", domain.CodedValues[3].Code);
    }

    [Fact]
    public async Task QueryDomainsAsync_ReturnsNullRange_WhenRangeHasFewerThanTwoValues() {
        var cancellationToken = TestContext.Current.CancellationToken;

        var client = CreateClient(request => {
            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: true);
            }

            if (IsQueryDomainsRequest(request)) {
                return StubHttpMessageHandler.Json("""
                {
                  "domains": [
                    {
                      "type": "range",
                      "name": "MalformedRange",
                      "range": [1]
                    }
                  ]
                }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var domain = Assert.Single(await client.QueryDomainsAsync([0], cancellationToken));

        Assert.True(domain.IsRange);
        Assert.Null(domain.Range);
    }

    [Fact]
    public async Task QueryDomainsAsync_Throws_WhenServiceDoesNotAdvertiseSupport() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestUris = new List<string>();

        var client = CreateClient(request => {
            requestUris.Add(request.RequestUri!.AbsoluteUri);

            if (IsServiceMetadataRequest(request)) {
                return CreateServiceMetadataResponse(supportsQueryDomains: false);
            }

            throw new InvalidOperationException("HTTP should not be called after metadata lookup.");
        });

        var exception = await Assert.ThrowsAsync<FeatureServiceCapabilityException>(() =>
            client.QueryDomainsAsync([0], cancellationToken));

        Assert.Contains("queryDomains", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(requestUris);
    }

    [Fact]
    public async Task QueryDomainsAsync_Throws_WhenLayerIdsContainDuplicates() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.QueryDomainsAsync([0, 0], cancellationToken));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
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

    private static bool IsQueryDomainsRequest(HttpRequestMessage request) {
        return request.RequestUri?.AbsolutePath.EndsWith(
            "/FeatureServer/queryDomains",
            StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpResponseMessage CreateServiceMetadataResponse(bool supportsQueryDomains) {
        return StubHttpMessageHandler.Json($$"""
        {
          "layers": [],
          "tables": [],
          "capabilities": "Query",
          "syncEnabled": false,
          "supportsQueryDomains": {{supportsQueryDomains.ToString().ToLowerInvariant()}}
        }
        """);
    }
}