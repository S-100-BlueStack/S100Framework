# S100Framework.REST

`S100Framework.REST` is a .NET library for reading, editing, calculating, bulk-loading, synchronizing replicas, and analyzing Esri Feature Services through the ArcGIS REST API and exposing geometry data through NetTopologySuite.

It does **not** depend on ArcGIS SDKs, ArcGIS Runtime, or ArcGIS licensing. It talks directly to Feature Service REST endpoints.

## What this library is for

Use this library when you want to:

- read service metadata, layer metadata, and schema
- query features from an Esri Feature Service
- work with geometries as NetTopologySuite types, including optional feature centroids
- use advanced query options such as temporal filters, unknown time reference handling, geodatabase versions, request shaping, spatial distance filters, cache hints, full text search, unique IDs, datum transformations, quantization, and statistics
- query service domains, service relationships, data elements, field groups, and contingent values
- run service-level multi-layer feature, count, ID, unique-ID, complete-result, and extent queries
- query related records and related-record counts, attachments, attachment counts, top features, estimates, bins, date bins, and synchronous/asynchronous analytic rows
- validate SQL expressions before sending dynamic queries
- calculate field values server-side for records matched by a layer query
- edit features with layer-level or service-level `applyEdits`
- use convenience edit wrappers for add, update, and delete workflows
- call direct `addFeatures`, `updateFeatures`, and `deleteFeatures` endpoints when endpoint-specific behavior is needed
- upload temporary server-side files for append workflows
- delete temporary server-side upload items after append workflows
- bulk-load data with service-level or layer-level `append`
- get change sets from `extractChanges`
- create, synchronize, inspect, and unregister Feature Service replicas
- run download-only, upload-only, and bidirectional replica synchronization workflows
- persist replica synchronization state outside the package
- inspect replica JSON result files and fail fast on returned edit errors
- convert between NTS geometries / feature records and Esri JSON

## What most consumers need

Most consumers only need these steps:

1. Create a `FeatureServiceClient`
2. Get a layer client
3. Read schema if needed
4. Run queries or edits

If you do **not** need incremental sync or delta tracking, you can ignore the `extractChanges` section.
If you do **not** need offline or replica synchronization, you can ignore the `replicas and synchronization` section.
If you do **not** need bulk loading, you can ignore the `append` section.
If you do **not** need aggregated analysis, you can ignore `queryBins`, `queryDateBins`, and `queryAnalytic`.
If you do **not** build dynamic editing forms, you can ignore `queryDataElements`, `fieldGroups`, and `queryContingentValues`.

---

## Requirements

- .NET 10
- NetTopologySuite
- Access to an Esri Feature Service endpoint

## Installation

Add a project reference during development, or consume the package when published.

Example project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\S100Framework.REST\S100Framework.REST.csproj" />
</ItemGroup>
```

---

## Core concepts

The library has two main clients:

- `IFeatureServiceClient`
- `IFeatureLayerClient`

### `IFeatureServiceClient`

Use the service client for service-level operations:

- read service metadata
- get a layer client by ID
- get a layer client by layer name
- query service domains for a set of layer IDs
- query service-level relationships
- run service-level multi-layer feature, count, ID, unique-ID, complete-result, and extent queries
- query data elements for a set of layer IDs
- query contingent values for one or more layers or tables
- get service-level layer estimates
- upload files to the service uploads endpoint
- run multi-layer `applyEdits`
- run `extractChanges`
- poll async `extractChanges` jobs
- download async `extractChanges` result files
- create, list, synchronize, and unregister replicas
- run state-aware replica workflows for download-only, upload-only, and bidirectional synchronization
- submit and poll service-level `append` jobs

### `IFeatureLayerClient`

Use the layer client for layer-level operations:

- read layer schema
- query features
- query counts, object IDs, unique IDs, and extents
- query statistics
- query related records
- query attachments, query attachment counts, and download attachment content
- query top features
- query bins, date bins, and synchronous/asynchronous analytic rows
- query data elements for the current layer
- query contingent values and field groups
- get layer estimates
- validate SQL expressions
- calculate field values for matched records synchronously or asynchronously
- run layer-level `applyEdits`
- use layer-level edit convenience wrappers
- call direct layer-level `addFeatures`, `updateFeatures`, and `deleteFeatures` endpoints
- run layer-level `append`
- add, update, and delete attachments

---

## Create a service client

```csharp
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        DefaultPageSize = 100,
        RequestTimeout = TimeSpan.FromSeconds(30),
        FixInvalidGeometries = true,
        PreferLatestWkid = true,
        QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
        AutoPostQueryLengthThreshold = 1800
    });
```

---

## Dependency injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using S100Framework.REST.Configuration;
using S100Framework.REST.DependencyInjection;

services.AddFeatureServiceClient(options => {
    options.ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer");
    options.DefaultPageSize = 100;
});
```

Use direct construction when you need explicit control over `HttpClient` lifetime or authentication setup. Use DI when the consuming application already uses `IServiceCollection` and typed clients.

---

## Authentication

Authentication is intentionally handled through `HttpClient` and optional request authorizers.

That keeps authentication concerns outside the query and edit models. The library supports three practical patterns:

1. direct Windows / integrated authentication through `HttpClientHandler`
2. direct bearer tokens
3. token providers that fetch or exchange tokens for you

### When to use which authentication style

Use this as a rule of thumb:

- If the Feature Service itself is available through Integrated Windows Authentication, configure `HttpClientHandler.UseDefaultCredentials = true` in the consuming application.
- If you already have a token, use `BearerTokenFeatureServiceRequestAuthorizer` directly.
- If you need the library to fetch or refresh a token, use an `IFeatureServiceAccessTokenProvider`.
- If your Feature Service is federated behind Portal for ArcGIS, the common pattern is: **portal token -> server token exchange -> Feature Service requests**.

### Direct Windows SSO / integrated authentication

Use this only when the Feature Service endpoint itself accepts the current Windows identity through integrated authentication.

```csharp
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var handler = new HttpClientHandler {
    UseDefaultCredentials = true
};

using var httpClient = new HttpClient(handler);

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    });
```

This is **not** the same as token-based access through a federated portal. If your service expects a token, `UseDefaultCredentials = true` is not enough by itself.

### Direct bearer token

If your application already has a token, pass it through a request authorizer.

```csharp
using S100Framework.REST.Authorization;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(
    _ => ValueTask.FromResult("your-token"));

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

### Static access token provider

```csharp
using S100Framework.REST.Authorization;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var tokenProvider = new StaticFeatureServiceAccessTokenProvider(
    new FeatureServiceAccessToken(
        "your-token",
        DateTimeOffset.UtcNow.AddHours(1)));

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(tokenProvider);

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

### ArcGIS Server `generateToken`

Use `ArcGisServerGenerateTokenProvider` when you are calling an ArcGIS Server token endpoint directly and you want the library to fetch and cache the token for you.

```csharp
using S100Framework.REST.Authorization;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

using var serviceHttpClient = new HttpClient();
using var tokenHttpClient = new HttpClient();
using var tokenProvider = new ArcGisServerGenerateTokenProvider(
    tokenHttpClient,
    new ArcGisServerGenerateTokenOptions {
        TokenUri = new Uri("https://your-server/arcgis/tokens/generateToken"),
        Username = "your-username",
        Password = "your-password",
        ClientType = ArcGisServerTokenClientType.Referer,
        Referer = "https://your-app",
        ExpirationMinutes = 60
    });

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(tokenProvider);

var client = new FeatureServiceClient(
    serviceHttpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

### Federated services behind Portal for ArcGIS

If your Feature Service is federated behind Portal for ArcGIS, the typical flow is:

1. get a **portal token**
2. exchange it for a **server token**
3. use the server token against the Feature Service

That is what `PortalServerTokenExchangeProvider` is for.

```csharp
using S100Framework.REST.Authorization;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

using var serviceHttpClient = new HttpClient();
using var portalHttpClient = new HttpClient();

var portalTokenProvider = new StaticFeatureServiceAccessTokenProvider(
    new FeatureServiceAccessToken(
        "portal-token-from-your-app",
        DateTimeOffset.UtcNow.AddHours(1)));

using var featureServiceTokenProvider = new PortalServerTokenExchangeProvider(
    portalHttpClient,
    portalTokenProvider,
    new PortalServerTokenExchangeOptions {
        GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
        ServerUrl = new Uri("https://server.example.com/server")
    });

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(featureServiceTokenProvider);

var client = new FeatureServiceClient(
    serviceHttpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://server.example.com/server/rest/services/MyService/FeatureServer")
    },
    authorizer);
```

In this setup, the library does **not** acquire the portal token itself. It expects another part of the application to provide that token.

### What the library does not do

The library currently does **not** handle these parts for you:

- interactive portal login
- OAuth browser flows
- Portal for ArcGIS login through Windows / IWA
- turning the current Windows session into a portal token automatically

If you need one of those flows, handle it in the consuming application and then pass the resulting token into the library through a token provider.

Do not hardcode secrets in source code. Use a secret store, secure local development configuration, or a runtime prompt in sample applications.

---

## Get service metadata

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine($"Layers: {metadata.Layers.Count}");
Console.WriteLine($"Supports editing: {metadata.Capabilities.SupportsEditing}");
Console.WriteLine($"Supports uploads: {metadata.Capabilities.SupportsUploads}");
Console.WriteLine($"Supports change tracking: {metadata.Capabilities.SupportsChangeTracking}");
Console.WriteLine($"Supports append: {metadata.Capabilities.SupportsAppend}");
Console.WriteLine($"Supports replica resource: {metadata.SupportsReplicaResource()}");
Console.WriteLine($"Supports replica sync direction control: {metadata.SupportsReplicaSyncDirectionControl()}");
Console.WriteLine($"Supports replica rollback-on-failure: {metadata.SupportsReplicaRollbackOnFailure()}");
Console.WriteLine($"Supports query domains: {metadata.Capabilities.SupportsQueryDomains}");
Console.WriteLine($"Supports query data elements: {metadata.Capabilities.SupportsQueryDataElements}");
Console.WriteLine($"Supports query contingent values: {metadata.Capabilities.SupportsQueryContingentValues}");
Console.WriteLine($"Supports relationships resource: {metadata.Capabilities.SupportsRelationshipsResource}");
Console.WriteLine($"Append formats: {string.Join(", ", metadata.SupportedAppendFormats)}");
Console.WriteLine($"Export formats: {string.Join(", ", metadata.SupportedExportFormats)}");
```

This is mostly useful when you want to know what the service supports before doing advanced operations such as attachment edits, `extractChanges`, replica synchronization, `append`, uploads, `queryDomains`, service-level `relationships`, service-level `query`, `queryDataElements`, or `queryContingentValues`.

---

## Get a layer client

By layer ID:

```csharp
var layerClient = client.GetLayerClient(0);
```

By layer name:

```csharp
var layerClient = await client.GetLayerClientAsync("Harbors");
```

---

## Get layer schema

```csharp
var layerClient = client.GetLayerClient(0);
var schema = await layerClient.GetSchemaAsync();

Console.WriteLine($"Layer name: {schema.Name}");
Console.WriteLine($"Geometry type: {schema.GeometryType}");
Console.WriteLine($"SRID: {schema.Srid}");
Console.WriteLine($"Object ID field: {schema.ObjectIdFieldName}");
Console.WriteLine($"Has attachments: {schema.Capabilities.HasAttachments}");
Console.WriteLine($"Supports full text search: {schema.Capabilities.SupportsFullTextSearch}");
Console.WriteLine($"Supports geometry envelope: {schema.Capabilities.SupportsReturningGeometryEnvelope}");
Console.WriteLine($"Supports percentile statistics: {schema.Capabilities.SupportsPercentileStatistics}");
Console.WriteLine($"Supports append: {schema.Capabilities.SupportsAppend}");
Console.WriteLine($"Supports queryDateBins: {schema.Capabilities.SupportsQueryDateBins}");
Console.WriteLine($"Supports queryAnalytic: {schema.Capabilities.SupportsQueryAnalytic}");
Console.WriteLine($"Supports async queryAnalytic: {schema.Capabilities.SupportsAsyncQueryAnalytic}");
Console.WriteLine($"Supports calculate: {schema.Capabilities.SupportsCalculate}");
Console.WriteLine($"Supports async calculate: {schema.Capabilities.SupportsAsyncCalculate}");
Console.WriteLine($"Supports extent-only query: {schema.Capabilities.SupportsReturningQueryExtent}");
Console.WriteLine($"Supports centroid output: {schema.Capabilities.SupportsReturningGeometryCentroid}");
Console.WriteLine($"Supports default SR: {schema.Capabilities.SupportsDefaultSrid}");
Console.WriteLine($"Supports SQL expressions: {schema.Capabilities.SupportsSqlExpression}");
Console.WriteLine($"Supports out-field SQL expressions: {schema.Capabilities.SupportsOutFieldSqlExpression}");
Console.WriteLine($"Supports having clause: {schema.Capabilities.SupportsHavingClause}");
Console.WriteLine($"Supports spatial distance queries: {schema.Capabilities.SupportsQueryWithDistance}");
Console.WriteLine($"Supports result type: {schema.Capabilities.SupportsQueryWithResultType}");
Console.WriteLine($"Supports historic moment: {schema.Capabilities.SupportsQueryWithHistoricMoment}");
Console.WriteLine($"Supports datum transformation: {schema.Capabilities.SupportsQueryWithDatumTransformation}");
Console.WriteLine($"Supports coordinate quantization: {schema.Capabilities.SupportsCoordinatesQuantization}");
Console.WriteLine($"Supports current user queries: {schema.Capabilities.SupportsCurrentUserQueries}");
Console.WriteLine($"Supports query cache hint: {schema.Capabilities.SupportsQueryWithCacheHint}");
Console.WriteLine($"Has contingent values definition: {schema.HasContingentValuesDefinition}");
Console.WriteLine($"Supports unique IDs: {schema.SupportsUniqueIds}");

if (schema.UniqueIdInfo is not null) {
    Console.WriteLine($"Unique ID type: {schema.UniqueIdInfo.Type}");
    Console.WriteLine($"Unique ID fields: {string.Join(", ", schema.UniqueIdInfo.Fields)}");
}
```

This is the easiest way to discover what a layer looks like before querying, editing, appending, calculating values, validating input, or using advanced layer operations.

---

## Basic feature query

```csharp
using S100Framework.REST.Models;

var layerClient = client.GetLayerClient(0);

var query = new FeatureQuery {
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine($"ObjectId: {feature.ObjectId}");
    Console.WriteLine($"Name: {feature.Attributes["NAME"]}");
    Console.WriteLine($"Geometry type: {feature.Geometry?.GeometryType}");
}
```

Use this when you want actual features back.

### Query count only

```csharp
var count = await layerClient.QueryCountAsync(new FeatureQuery {
    Where = "STATUS = 'Active'"
});
```

### Query object IDs only

```csharp
var objectIds = await layerClient.QueryObjectIdsAsync(new FeatureQuery {
    Where = "STATUS = 'Active'"
});
```

### Query unique IDs only

```csharp
var result = await layerClient.QueryUniqueIdsAsync(new FeatureQuery {
    Where = "STATUS = 'Active'"
});

Console.WriteLine($"Unique ID fields: {string.Join(", ", result.UniqueIdFieldNames)}");

foreach (var uniqueId in result.UniqueIds) {
    Console.WriteLine(string.Join(" | ", uniqueId.Components));
}
```

### Query extent only

```csharp
var extent = await layerClient.QueryExtentAsync(new FeatureQuery {
    Where = "STATUS = 'Active'"
});
```

These are useful when you do not need full features.

---

## Advanced feature query

### Temporal query with `time` and `historicMoment`

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    TimeExtent = new FeatureTimeExtent(
        Start: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2025, 01, 31, 23, 59, 59, TimeSpan.Zero)),
    HistoricMoment = new DateTimeOffset(2025, 02, 01, 0, 0, 0, TimeSpan.Zero)
};

var count = await layerClient.QueryCountAsync(query);
```

Use `TimeInstant` for a single instant and `TimeExtent` for an interval or open-ended range.

### Versioned and unknown time reference queries

Use `GdbVersion` when querying a specific geodatabase version. Use `TimeReferenceUnknownClient` when the client can work with date fields whose time reference is unknown.

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "STATUS = 'Active'",
    GdbVersion = "SDE.DEFAULT",
    TimeReferenceUnknownClient = true
};

var objectIds = await layerClient.QueryObjectIdsAsync(query);
```

`GdbVersion` and `TimeReferenceUnknownClient` are also forwarded by service-client convenience methods such as `QueryAllAsync` and `QueryExtentsAsync` when they execute layer-level queries.

### Spatial distance filters

Use a spatial distance when you want ArcGIS to evaluate a spatial relationship against a buffered input geometry.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "1=1",
    SpatialFilter = FeatureSpatialFilter.FromGeometry(
        new Point(12.34, 56.78) {
            SRID = 4326
        },
        distance: 100,
        distanceUnit: FeatureSpatialDistanceUnit.Meter)
};

var count = await layerClient.QueryCountAsync(query);
```

You can also use distance with envelope filters:

```csharp
var query = new FeatureQuery {
    SpatialFilter = FeatureSpatialFilter.FromEnvelope(
        new Envelope(10, 11, 55, 56),
        inSrid: 4326,
        distance: 2.5,
        distanceUnit: FeatureSpatialDistanceUnit.Kilometer)
};
```

The supported units are `Meter`, `StatuteMile`, `Foot`, `Kilometer`, `NauticalMile`, and `UsNauticalMile`. Use `schema.Capabilities.SupportsQueryWithDistance` when you need a proactive capability check.

### Request shaping with result windows, result type, default SR, cache hint, and SQL format

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "1=1",
    ResultOffset = 100,
    ResultRecordCount = 50,
    ResultType = FeatureQueryResultType.Standard,
    ReturnExceededLimitFeatures = false,
    DefaultSrid = 4326,
    CacheHint = true,
    SqlFormat = FeatureQuerySqlFormat.Standard,
    PageSize = 25
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine(feature.ObjectId);
}
```

`ResultOffset` and `ResultRecordCount` define the requested result window. `PageSize` still controls the client-side batch size while streaming.

Use `ResultType = FeatureQueryResultType.Tile` when you are intentionally using tile-oriented server behavior.

Use `CacheHint = true` when the layer advertises query cache support and the query is suitable for server-side response caching. You can check `schema.Capabilities.SupportsQueryWithCacheHint` before enabling it.

### Datum transformation

Use `DatumTransformationWkid` when a known transformation ID is enough:

```csharp
var query = new FeatureQuery {
    Where = "1=1",
    OutSrid = 3857,
    DatumTransformationWkid = 108190
};
```

Use `DatumTransformationJson` for composite or WKT-based transformations:

```csharp
var query = new FeatureQuery {
    Where = "1=1",
    DatumTransformationJson = """
    {
      "geoTransforms": [
        {
          "wkid": 108190,
          "forward": true
        }
      ]
    }
    """
};
```

Only one of `DatumTransformationWkid` and `DatumTransformationJson` can be set.

### Quantization parameters

```csharp
var query = new FeatureQuery {
    Where = "1=1",
    ReturnGeometry = true,
    QuantizationParametersJson = """
    {
      "mode": "view",
      "originPosition": "upperLeft",
      "tolerance": 1,
      "extent": {
        "xmin": 0,
        "ymin": 0,
        "xmax": 10,
        "ymax": 10,
        "spatialReference": { "wkid": 4326 }
      }
    }
    """
};
```

Raw JSON is used intentionally here because ArcGIS supports multiple quantization payload shapes.

### Multipatch options

```csharp
var query = new FeatureQuery {
    Where = "1=1",
    ReturnGeometry = true,
    MultipatchOption = FeatureQueryMultipatchOption.Extent
};
```

`MultipatchOption` is only valid when `ReturnGeometry = true`.

### Return geometry as envelope

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    ReturnGeometry = true,
    ReturnEnvelope = true
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine(feature.Geometry?.EnvelopeInternal);
}
```

Use this when you only need spatial bounds instead of full geometry payloads.

### Return feature centroids

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = false,
    ReturnCentroid = true
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine($"ObjectId: {feature.ObjectId}");
    Console.WriteLine($"Centroid: {feature.Centroid?.X}, {feature.Centroid?.Y}");
}
```

Use `ReturnCentroid = true` when you need the service-returned centroid for polygon features without also requesting full feature geometry. The centroid is exposed as `FeatureRecord.Centroid` and is `null` when the service does not return a centroid for a feature.


### Full text search

```csharp
using S100Framework.REST.Models;

var count = await layerClient.QueryCountAsync(new FeatureQuery {
    FullText = [
        new FeatureQueryFullTextExpression {
            OnFields = ["NAME", "DESCRIPTION"],
            SearchTerm = "harbor basin",
            SearchType = FeatureQueryFullTextSearchType.Simple,
            Operator = FeatureQueryFullTextOperator.Or
        }
    ]
});
```

### Filter by unique IDs

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    UniqueIds = [
        new FeatureUniqueId(["DK", "CPH"]),
        new FeatureUniqueId(["SE", "GOT"])
    ]
};

var objectIds = await layerClient.QueryObjectIdsAsync(query);
```

For simple unique IDs, use a single component:

```csharp
new FeatureUniqueId(["alpha"])
```

---

## Query statistics

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery {
    Where = "1=1",
    GroupByFields = ["STATUS"],
    Statistics = [
        new StatisticDefinition(
            OnStatisticField: "OBJECTID",
            OutStatisticFieldName: "ROW_COUNT",
            StatisticType: StatisticType.Count)
    ]
});

foreach (var row in rows) {
    Console.WriteLine($"Status: {row.Attributes["STATUS"]}, Count: {row.Attributes["ROW_COUNT"]}");
}
```

Use this when you need grouped results or aggregates instead of feature rows.

### Statistics pagination for grouped results

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery {
    GroupByFields = ["STATUS"],
    ResultOffset = 0,
    ResultRecordCount = 25,
    Statistics = [
        new StatisticDefinition(
            OnStatisticField: "OBJECTID",
            OutStatisticFieldName: "ROW_COUNT",
            StatisticType: StatisticType.Count)
    ]
});
```

`ResultOffset` and `ResultRecordCount` are only valid for grouped statistics queries, and the target layer must support pagination on aggregated queries.

### Percentile statistics

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery {
    Statistics = [
        new StatisticDefinition(
            OnStatisticField: "DEPTH",
            OutStatisticFieldName: "P90_DEPTH",
            StatisticType: StatisticType.PercentileDiscrete,
            PercentileParameters: new StatisticPercentileParameters(
                Value: 0.9,
                OrderBy: StatisticPercentileOrder.Desc))
    ]
});

Console.WriteLine(rows[0].Attributes["P90_DEPTH"]);
```

Percentile statistics require percentile parameters and depend on layer support.

---

## Query domains

Use `queryDomains` when you want the service to return full domain information for the domains referenced by one or more layers.

```csharp
var domains = await client.QueryDomainsAsync([0, 1]);

foreach (var domain in domains) {
    Console.WriteLine($"Type: {domain.Type}");
    Console.WriteLine($"Name: {domain.Name}");

    if (domain.IsRange && domain.Range is not null) {
        Console.WriteLine($"Range: {domain.Range.MinimumValue} - {domain.Range.MaximumValue}");
    }

    if (domain.IsCodedValue) {
        foreach (var codedValue in domain.CodedValues) {
            Console.WriteLine($"{codedValue.Code} = {codedValue.Name}");
        }
    }
}
```

This is useful when you need to:

- build dropdowns from coded value domains
- validate values against domain ranges
- resolve code values to user-facing labels without hardcoding them
- inspect domain metadata for multiple layers in one call

---

## Service relationships

Use the service-level `relationships` resource when you need relationship class metadata for the whole feature service instead of inspecting each layer schema individually.

```csharp
var metadata = await client.GetMetadataAsync();

if (!metadata.Capabilities.SupportsRelationshipsResource) {
    throw new InvalidOperationException("The service does not support the relationships resource.");
}

var relationships = await client.GetRelationshipsAsync();

foreach (var relationship in relationships.Relationships) {
    Console.WriteLine(relationship.Id);
    Console.WriteLine(relationship.Name);
    Console.WriteLine($"{relationship.OriginLayerId} -> {relationship.DestinationLayerId}");
    Console.WriteLine(relationship.Cardinality);

    foreach (var rule in relationship.Rules) {
        Console.WriteLine(rule.RuleId);
    }
}
```

This is useful for dynamic tools that need to understand relationship classes across the whole service.

---

## Service-level query

Use service-level query APIs when you want to work with multiple layers or tables from the same Feature Service through one service client.

The service client exposes separate methods for the main response shapes:

- `QueryAsync` returns feature records grouped by layer from the service-level `query` endpoint.
- `QueryAllAsync` executes complete layer-level feature queries for each layer definition and groups the results by layer.
- `QueryCountAsync` returns counts grouped by layer from the service-level `query` endpoint.
- `QueryObjectIdsAsync` returns object IDs grouped by layer from the service-level `query` endpoint.
- `QueryUniqueIdsAsync` returns unique IDs grouped by layer from the service-level `query` endpoint.
- `QueryExtentsAsync` executes layer-level extent-only queries for each layer definition and groups the extents by layer.

`QueryAsync`, `QueryCountAsync`, `QueryObjectIdsAsync`, and `QueryUniqueIdsAsync` call the service-level `/FeatureServer/query` endpoint directly.

`QueryAllAsync` and `QueryExtentsAsync` are service-client convenience methods. They derive one layer-level query per `FeatureServiceLayerQueryDefinition`. This is useful when the consumer wants a multi-layer result shape but also wants to reuse the more complete layer-level query behavior.

### Service-level feature-set query

Use `QueryAsync` when you want one service-level request and the expected result set is small enough for the service-level feature-set response.

```csharp
using S100Framework.REST.Models;

var result = await client.QueryAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'",
            OutFields = ["OBJECTID", "NAME", "STATUS"]
        },
        new FeatureServiceLayerQueryDefinition {
            LayerId = 1,
            Where = "OBJECTID < 100",
            OutFields = ["OBJECTID", "INSPECTION_DATE"]
        }
    ],
    ReturnGeometry = true,
    OutSrid = 4326
});

foreach (var layerResult in result.Layers) {
    Console.WriteLine($"Layer: {layerResult.LayerId}");
    Console.WriteLine($"Exceeded transfer limit: {layerResult.ExceededTransferLimit}");

    foreach (var record in layerResult.Records) {
        Console.WriteLine(record.ObjectId);
        Console.WriteLine(record.Geometry?.GeometryType);
    }
}
```

### Complete multi-layer query

Use `QueryAllAsync` when you want complete feature records for multiple layers and want the existing layer-level query behavior, including paging and object-ID fallback where supported.

```csharp
using S100Framework.REST.Models;

var result = await client.QueryAllAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'",
            OutFields = ["OBJECTID", "NAME", "STATUS"]
        },
        new FeatureServiceLayerQueryDefinition {
            LayerId = 1,
            Where = "OBJECTID < 100",
            OutFields = ["OBJECTID", "INSPECTION_DATE"]
        }
    ],
    ReturnGeometry = true,
    OutSrid = 4326
});

foreach (var layerResult in result.Layers) {
    Console.WriteLine($"Layer: {layerResult.LayerId}");

    foreach (var record in layerResult.Records) {
        Console.WriteLine(record.ObjectId);
    }
}
```

`QueryAllAsync` executes layer-level queries. It forwards shared query options such as `GdbVersion`, `TimeReferenceUnknownClient`, temporal filters, spatial filters, output SR, SQL format, geometry precision, and geometry generalization to each layer-level query.

### Service-level counts

Use `QueryCountAsync` when you need row counts for multiple layers or tables without returning features.

```csharp
using S100Framework.REST.Models;

var result = await client.QueryCountAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'"
        },
        new FeatureServiceLayerQueryDefinition {
            LayerId = 1,
            Where = "INSPECTION_REQUIRED = 1"
        }
    ]
});

foreach (var layer in result.Layers) {
    Console.WriteLine($"Layer {layer.LayerId}: {layer.Count}");
}
```

### Service-level object IDs

Use `QueryObjectIdsAsync` when you need object IDs for multiple layers or tables.

```csharp
using S100Framework.REST.Models;

var result = await client.QueryObjectIdsAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'"
        },
        new FeatureServiceLayerQueryDefinition {
            LayerId = 1,
            Where = "INSPECTION_REQUIRED = 1"
        }
    ]
});

foreach (var layer in result.Layers) {
    Console.WriteLine($"Layer {layer.LayerId} object ID field: {layer.ObjectIdFieldName}");

    foreach (var objectId in layer.ObjectIds) {
        Console.WriteLine(objectId);
    }
}
```

### Service-level unique IDs

Use `QueryUniqueIdsAsync` when the service supports unique IDs and you need stable unique identifiers grouped by layer.

```csharp
using S100Framework.REST.Models;

var result = await client.QueryUniqueIdsAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'"
        }
    ]
});

foreach (var layer in result.Layers) {
    Console.WriteLine($"Layer {layer.LayerId}");
    Console.WriteLine($"Composite unique ID: {layer.IsComposite}");
    Console.WriteLine(string.Join(", ", layer.UniqueIdFieldNames));

    foreach (var uniqueId in layer.UniqueIds) {
        if (uniqueId.SingleValue is not null) {
            Console.WriteLine(uniqueId.SingleValue);
        }
        else {
            Console.WriteLine(string.Join(", ", uniqueId.Components));
        }
    }
}
```

### Multi-layer extents

Use `QueryExtentsAsync` when you need extents for multiple layers. ArcGIS REST exposes `returnExtentOnly` on layer-level query endpoints, so this method executes one layer-level extent query per layer definition and returns the results grouped by layer.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var result = await client.QueryExtentsAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "STATUS = 'Active'"
        },
        new FeatureServiceLayerQueryDefinition {
            LayerId = 1,
            Where = "INSPECTION_REQUIRED = 1"
        }
    ],
    SpatialFilter = FeatureSpatialFilter.FromEnvelope(
        new Envelope(10, 11, 55, 56),
        4326),
    OutSrid = 4326
});

foreach (var layer in result.Layers) {
    Console.WriteLine($"Layer {layer.LayerId}");

    if (layer.Extent is null) {
        Console.WriteLine("No extent returned.");
        continue;
    }

    Console.WriteLine(layer.Extent.Srid);
    Console.WriteLine(layer.Extent.Envelope);
}
```

`QueryExtentsAsync` executes layer-level queries. It forwards shared extent-query options such as `GdbVersion`, `TimeReferenceUnknownClient`, temporal filters, spatial filters, output SR, and SQL format to each layer-level extent query.

### Service-level query with time and spatial filters

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var result = await client.QueryAsync(new FeatureServiceQueryRequest {
    LayerDefinitions = [
        new FeatureServiceLayerQueryDefinition {
            LayerId = 0,
            Where = "1=1",
            OutFields = ["OBJECTID", "NAME"]
        }
    ],
    SpatialFilter = FeatureSpatialFilter.FromEnvelope(
        new Envelope(10, 11, 55, 56),
        4326),
    TimeExtent = new FeatureTimeExtent(
        Start: new DateTimeOffset(2026, 01, 01, 0, 0, 0, TimeSpan.Zero),
        End: null),
    GdbVersion = "SDE.DEFAULT",
    TimeReferenceUnknownClient = true,
    ReturnGeometry = false,
    SqlFormat = FeatureQuerySqlFormat.Standard
});
```

---

## Query related records

Use `queryRelatedRecords` when you need records connected through a relationship class from the current layer.

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();
var relationshipId = schema.Relationships[0].Id;

var related = await layerClient.QueryRelatedRecordsAsync(new RelatedRecordsQuery {
    ObjectIds = [1, 2, 3],
    RelationshipId = relationshipId,
    OutFields = ["OBJECTID", "NAME"],
    DefinitionExpression = "STATUS = 'Active'",
    ReturnGeometry = false
});

foreach (var group in related) {
    Console.WriteLine($"Source object ID: {group.SourceObjectId}");

    foreach (var record in group.Records) {
        Console.WriteLine(record.GetRequiredString("NAME"));
    }
}
```

If you are unsure which `RelationshipId` to use, inspect `schema.Relationships` first.

### Related-record counts

Use `QueryRelatedRecordCountsAsync` when you only need the number of related records per source object ID.

```csharp
var counts = await layerClient.QueryRelatedRecordCountsAsync(new RelatedRecordsQuery {
    ObjectIds = [1, 2, 3],
    RelationshipId = relationshipId
});

foreach (var group in counts) {
    Console.WriteLine($"{group.SourceObjectId}: {group.Count}");
}
```

Count-only related-record queries require the layer to support advanced related-record queries.

### Ordering and pagination

Related-record queries can request server-side ordering and pagination when the layer advertises the corresponding capabilities.

```csharp
var page = await layerClient.QueryRelatedRecordsAsync(new RelatedRecordsQuery {
    ObjectIds = [1, 2, 3],
    RelationshipId = relationshipId,
    OutFields = ["OBJECTID", "NAME", "CREATED_DATE"],
    OrderBy = "CREATED_DATE DESC",
    ResultOffset = 0,
    ResultRecordCount = 100
});
```

- `OrderBy` uses the REST `orderByFields` parameter and requires advanced related-record query support.
- `ResultOffset` and `ResultRecordCount` use related-record pagination and require related-record pagination support.

### Versioned, historic, and projected related records

Related-record queries also support geodatabase version, historic moment, unknown time zone handling, output spatial reference, geometry shaping, and datum transformations.

```csharp
var historic = await layerClient.QueryRelatedRecordsAsync(new RelatedRecordsQuery {
    ObjectIds = [1],
    RelationshipId = relationshipId,
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    OutSrid = 25832,
    GdbVersion = "SDE.DEFAULT",
    HistoricMoment = DateTimeOffset.UtcNow.AddDays(-7),
    TimeReferenceUnknownClient = true,
    GeometryPrecision = 2,
    MaxAllowableOffset = 0.5,
    DatumTransformationWkid = 108190
});
```

For composite or WKT-based transformations, use `DatumTransformationJson` instead of `DatumTransformationWkid`. Only one datum transformation option can be set at a time.

### Related-record request method selection

`queryRelatedRecords` uses the same GET/POST/Auto transport selection as layer-level `query`. This helps avoid overly long URLs when `ObjectIds`, `DefinitionExpression`, or datum transformation JSON become large. Configure this with `FeatureServiceClientOptions.QueryRequestMethodPreference` and `AutoPostQueryLengthThreshold`.

---

## Top features

Use top-features queries when the layer supports server-side top-N selection per group.

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryTopFeaturesAsync(new TopFeaturesQuery {
    Where = "1=1",
    OutFields = ["OBJECTID", "CATEGORY", "SCORE"],
    ReturnGeometry = false,
    TopFilter = new TopFeaturesFilter(
        GroupByFields: ["CATEGORY"],
        OrderByFields: ["SCORE DESC"],
        TopCount: 3)
});
```

You can also query top-feature object IDs or counts:

```csharp
var objectIds = await layerClient.QueryTopFeatureObjectIdsAsync(query);
var count = await layerClient.QueryTopFeatureCountAsync(query);
```

`queryTopFeatures` uses the same GET/POST/Auto transport selection as layer-level `query`. This helps avoid overly long URLs when `TopFilter`, `Where`, `ObjectIds`, or spatial filter parameters become large.

---

## Estimates

Use `getEstimates` when approximate count/extent information is good enough and you want a lightweight operation.

### Service-level estimates

```csharp
var estimates = await client.GetEstimatesAsync([0, 1]);

foreach (var estimate in estimates) {
    Console.WriteLine($"Layer: {estimate.LayerId}");
    Console.WriteLine($"Count: {estimate.Count}");
    Console.WriteLine($"Extent: {estimate.Extent?.Envelope}");
}
```

Call without layer IDs to ask the service for all returned layer estimates:

```csharp
var estimates = await client.GetEstimatesAsync();
```

### Layer-level estimates

```csharp
var estimate = await layerClient.GetEstimatesAsync();

Console.WriteLine(estimate.Count);
Console.WriteLine(estimate.Extent?.Envelope);
```

---

## Validate SQL

Use `validateSQL` before sending SQL entered or generated outside trusted static code.

```csharp
using S100Framework.REST.Models;

var result = await layerClient.ValidateSqlAsync(new ValidateSqlRequest {
    Sql = "STATUS = 'Active'",
    SqlType = ValidateSqlType.Where
});

if (!result.IsValidSql) {
    foreach (var error in result.ValidationErrors) {
        Console.WriteLine($"{error.ErrorCode}: {error.Description}");
    }
}
```

This validates the SQL against the layer, but it does not make untrusted SQL automatically safe. Prefer server-supported parameterization or strongly controlled query builders in consuming applications.

---

## Calculate field values

Use `calculate` when the layer supports server-side field updates for records matched by a `where` clause.

Always use an explicit `Where` expression. The .NET model defaults to `1=1` for consistency with other request models, but calculating against all rows is usually not what you want unless it is intentional.

### Synchronous `calculate`

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();

if (!schema.Capabilities.SupportsCalculate) {
    throw new InvalidOperationException("The layer does not support calculate.");
}

var result = await layerClient.CalculateAsync(new CalculateRequest {
    Where = "STATUS = 'Pending'",
    Expressions = [
        CalculateExpression.ForValue("STATUS", "Reviewed"),
        CalculateExpression.ForSqlExpression("SCORE", "BASE_SCORE * 2"),
        CalculateExpression.ForNull("REVIEWED_AT")
    ],
    SqlFormat = FeatureQuerySqlFormat.Standard,
    ReturnEditMoment = true
});

Console.WriteLine(result.Success);
Console.WriteLine(result.UpdatedFeatureCount);
Console.WriteLine(result.EditMoment);
```

`CalculateExpression.ForValue(...)` assigns a scalar value, `CalculateExpression.ForSqlExpression(...)` sends a SQL expression evaluated by the service, and `CalculateExpression.ForNull(...)` assigns a JSON `null` value.

### Submit async `calculate`

Use async `calculate` when the layer supports calculate jobs that may take longer than a normal request.

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();

if (!schema.Capabilities.SupportsAsyncCalculate) {
    throw new InvalidOperationException("The layer does not support async calculate.");
}

var submission = await layerClient.SubmitCalculateAsync(new CalculateRequest {
    Where = "STATUS = 'Pending'",
    Expressions = [
        CalculateExpression.ForValue("STATUS", "Reviewed")
    ],
    SqlFormat = FeatureQuerySqlFormat.Standard
});

if (submission.IsPending) {
    Console.WriteLine(submission.StatusUrl);
}

if (submission.Result is not null) {
    Console.WriteLine(submission.Result.UpdatedFeatureCount);
}
```

### Poll async `calculate` manually

```csharp
if (submission.StatusUrl is null) {
    throw new InvalidOperationException("The server did not return a status URL.");
}

while (true) {
    var status = await layerClient.GetCalculateStatusAsync(submission.StatusUrl);

    if (status.IsCompleted) {
        Console.WriteLine(status.RecordCount);
        break;
    }

    if (status.IsFailed || status.IsCancelled || status.IsTimedOut) {
        throw new InvalidOperationException(
            $"calculate ended with status '{status.Status}'.");
    }

    await Task.Delay(TimeSpan.FromSeconds(2));
}
```

### Wait for async `calculate`

```csharp
var result = await layerClient.WaitForCalculateCompletionAsync(
    new CalculateRequest {
        Where = "STATUS = 'Pending'",
        Expressions = [
            CalculateExpression.ForValue("STATUS", "Reviewed")
        ]
    },
    new CalculateWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(5)
    });

Console.WriteLine(result.Success);
Console.WriteLine(result.UpdatedFeatureCount);
```

`CalculateJobStatus` understands common Esri job values such as `esriJobSubmitted`, `esriJobExecuting`, `esriJobSucceeded`, `esriJobFailed`, `esriJobTimedOut`, and `esriJobCancelled`.


---

## Query bins

Use `queryBins` for histogram-like grouped analysis over numeric or date fields.

```csharp
using S100Framework.REST.Models;

var result = await layerClient.QueryBinsAsync(new QueryBinsRequest {
    Where = "STATUS = 'Active'",
    BinJson = """
    {
      "type": "fixedIntervalBin",
      "onField": "DEPTH",
      "parameters": {
        "interval": 100,
        "start": 0,
        "end": 1000
      }
    }
    """,
    Statistics = [
        new StatisticDefinition(
            OnStatisticField: "DEPTH",
            OutStatisticFieldName: "AVG_DEPTH",
            StatisticType: StatisticType.Average)
    ],
    BinOrder = QueryBinsOrder.Ascending
});

foreach (var row in result.Rows) {
    Console.WriteLine($"Bin: {row.Attributes["bin"]}");
    Console.WriteLine($"Frequency: {row.Attributes["frequency"]}");
    Console.WriteLine($"Average depth: {row.Attributes["AVG_DEPTH"]}");
}
```

`BinJson` is raw JSON by design because ArcGIS supports several bin shapes and this package should not force one model too early.

Stacked bins are returned through `QueryBinsResult.StackFieldNames` and `QueryBinRow.StackedAttributes`.

---

## Query date bins

Use `queryDateBins` for aggregated time-based histograms.

```csharp
using S100Framework.REST.Models;

var result = await layerClient.QueryDateBinsAsync(new QueryDateBinsRequest {
    BinField = "created_at",
    BinJson = """
    {
      "calendarBin": {
        "unit": "day",
        "timezone": "Europe/Copenhagen"
      }
    }
    """,
    Statistics = [
        new StatisticDefinition(
            OnStatisticField: "OBJECTID",
            OutStatisticFieldName: "item_count",
            StatisticType: StatisticType.Count)
    ],
    Where = "STATUS = 'Active'",
    TimeExtent = new FeatureTimeExtent(
        Start: new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero),
        End: new DateTimeOffset(2025, 02, 01, 0, 0, 0, TimeSpan.Zero)),
    BinOrder = QueryBinsOrder.Ascending,
    ReturnCentroid = false,
    ResultRecordCount = 100,
    BinBoundaryAlias = "boundary"
});

foreach (var row in result.Rows) {
    Console.WriteLine($"Boundary: {row.Attributes["boundary"]}");
    Console.WriteLine($"Count: {row.Attributes["item_count"]}");
}
```

Use `ReturnCentroid = true` if the service should return aggregate centroid information for each date bin.

---

## Query analytic rows

Use `queryAnalytic` when the service supports analytic queries such as windowed calculations, ranking, running totals, aggregate expressions, or other server-side analytic definitions.

```csharp
using S100Framework.REST.Models;

var result = await layerClient.QueryAnalyticAsync(new QueryAnalyticRequest {
    Where = "POP1990 > 0",
    AnalyticWhere = "RunningTotal > 100",
    OutAnalyticsJson = """
    [
      {
        "analyticType": "SUM",
        "onAnalyticField": "POP1990",
        "outAnalyticFieldName": "RunningTotal",
        "analyticParameters": {
          "partitionBy": "STATE_NAME",
          "orderBy": "POP1990 ASC"
        }
      }
    ]
    """,
    OutFields = ["OBJECTID", "STATE_NAME", "RunningTotal"],
    ReturnGeometry = false,
    ResultType = FeatureQueryResultType.Standard,
    ResultRecordCount = 100,
    SqlFormat = FeatureQuerySqlFormat.Standard
});

foreach (var row in result.Rows) {
    Console.WriteLine(row.Attributes["STATE_NAME"]);
    Console.WriteLine(row.Attributes["RunningTotal"]);
}
```

`OutAnalyticsJson` is raw JSON by design because ArcGIS supports many analytic definitions and this package should not lock consumers into one model too early.

Check `schema.Capabilities.SupportsQueryAnalytic` before using this operation when you need proactive capability checks.

### Async `queryAnalytic`

Use async `queryAnalytic` when the layer supports analytic jobs that may take longer than a normal request. Check `schema.Capabilities.SupportsAsyncQueryAnalytic` before submitting an async job.

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();

if (!schema.Capabilities.SupportsAsyncQueryAnalytic) {
    throw new InvalidOperationException("The layer does not support async queryAnalytic.");
}

var submission = await layerClient.SubmitQueryAnalyticAsync(new QueryAnalyticRequest {
    Where = "POP1990 > 0",
    OutAnalyticsJson = """
    [
      {
        "analyticType": "SUM",
        "onAnalyticField": "POP1990",
        "outAnalyticFieldName": "RunningTotal",
        "analyticParameters": {
          "partitionBy": "STATE_NAME",
          "orderBy": "POP1990 ASC"
        }
      }
    ]
    """,
    OutFields = ["OBJECTID", "STATE_NAME", "RunningTotal"],
    ReturnGeometry = false
});

if (submission.IsPending) {
    Console.WriteLine(submission.StatusUrl);
}
```

### Poll async `queryAnalytic` manually

```csharp
if (submission.StatusUrl is null) {
    throw new InvalidOperationException("The server did not return a status URL.");
}

while (true) {
    var status = await layerClient.GetQueryAnalyticStatusAsync(submission.StatusUrl);

    if (status.IsCompleted && status.ResultUrl is not null) {
        var result = await layerClient.GetQueryAnalyticResultAsync(status.ResultUrl);

        foreach (var row in result.Rows) {
            Console.WriteLine(row.Attributes["RunningTotal"]);
        }

        break;
    }

    if (status.IsFailed || status.IsCancelled || status.IsTimedOut) {
        throw new InvalidOperationException(
            $"queryAnalytic ended with status '{status.Status}'.");
    }

    await Task.Delay(TimeSpan.FromSeconds(2));
}
```

### Wait for async `queryAnalytic`

```csharp
var result = await layerClient.WaitForQueryAnalyticCompletionAsync(
    new QueryAnalyticRequest {
        Where = "POP1990 > 0",
        OutAnalyticsJson = """
        [
          {
            "analyticType": "RANK",
            "onAnalyticField": "POP1990",
            "outAnalyticFieldName": "PopulationRank"
          }
        ]
        """,
        OutFields = ["OBJECTID", "STATE_NAME", "PopulationRank"],
        ReturnGeometry = false
    },
    new QueryAnalyticWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(5)
    });

foreach (var row in result.Rows) {
    Console.WriteLine(row.Attributes["PopulationRank"]);
}
```

`QueryAnalyticJobStatus` understands common Esri job values such as `esriJobSubmitted`, `esriJobExecuting`, `esriJobSucceeded`, `esriJobFailed`, `esriJobTimedOut`, and `esriJobCancelled`.

---

## Query data elements

Use `queryDataElements` when you need the raw geodatabase data element metadata associated with layers or tables. This is useful for dynamic tools that need metadata beyond the normal layer schema.

### Service-level data elements

```csharp
var dataElements = await client.QueryDataElementsAsync([0, 1]);

foreach (var dataElement in dataElements) {
    Console.WriteLine($"Layer: {dataElement.LayerId}");
    Console.WriteLine(dataElement.DataElement.GetRawText());
}
```

### Layer-level data element

```csharp
var dataElement = await layerClient.QueryDataElementAsync();

Console.WriteLine(dataElement.LayerId);
Console.WriteLine(dataElement.DataElement.GetRawText());
```

The data element itself is exposed as raw JSON because ArcGIS can return different data element shapes depending on service type, dataset type, and server version.

Check `metadata.Capabilities.SupportsQueryDataElements` before using this operation when you need proactive capability checks.

---

## Contingent values and field groups

Use contingent values and field groups when building dynamic edit forms or validation logic where valid values in one field depend on values in another field.

### Query contingent values

Service-level request:

```csharp
using S100Framework.REST.Models;

var result = await client.QueryContingentValuesAsync(new QueryContingentValuesRequest {
    LayerIds = [0, 2],
    CompactFormat = false,
    DomainDictionaries = QueryContingentValuesDomainDictionaries.Trimmed
});

Console.WriteLine(result.Payload.GetRawText());
```

Layer-level convenience wrapper:

```csharp
var result = await layerClient.QueryContingentValuesAsync(new QueryContingentValuesOptions {
    CompactFormat = false,
    DomainDictionaries = QueryContingentValuesDomainDictionaries.Trimmed
});

Console.WriteLine(result.LayerId);
Console.WriteLine(result.Payload.GetRawText());
```

The contingent values payload is exposed as raw JSON because ArcGIS returns different shapes for hosted and non-hosted services.

Check `metadata.Capabilities.SupportsQueryContingentValues` before using this operation when you need proactive capability checks.

### Get field groups

```csharp
var fieldGroups = await layerClient.GetFieldGroupsAsync();

foreach (var group in fieldGroups.FieldGroups) {
    Console.WriteLine(group.Name);
    Console.WriteLine($"Restrictive: {group.Restrictive}");
    Console.WriteLine($"Fields: {string.Join(", ", group.Fields)}");
}
```

`schema.HasContingentValuesDefinition` tells you whether the layer advertises contingent value definitions.

---

## Attachments

Attachments are only available when the target layer and service support them.

You can check this up front:

```csharp
var schema = await layerClient.GetSchemaAsync();
var metadata = await client.GetMetadataAsync();

var canReadAttachments = schema.Capabilities.HasAttachments;
var canQueryAttachments = schema.Capabilities.HasAttachments && schema.Capabilities.SupportsQueryAttachments;
var canQueryAttachmentCounts = canQueryAttachments && schema.Capabilities.SupportsQueryAttachmentsCountOnly;
var canOrderAttachmentQueries = canQueryAttachments && schema.Capabilities.SupportsQueryAttachmentOrderByFields;
var canEditAttachments = schema.Capabilities.HasAttachments && metadata.Capabilities.SupportsEditing;
var canUploadAttachments = canEditAttachments && metadata.Capabilities.SupportsUploads;
```

The client also validates these capabilities at runtime and throws a clear exception if the operation is not supported.

### Query attachments

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryAttachmentsAsync(new AttachmentQuery {
    ObjectIds = [1, 2, 3]
});

foreach (var group in groups) {
    Console.WriteLine($"Parent object: {group.SourceObjectId}");

    foreach (var attachment in group.Attachments) {
        Console.WriteLine($"Attachment: {attachment.AttachmentId} - {attachment.Name}");
    }
}
```

You can select parent features by object IDs, global IDs, or a definition expression:

```csharp
var byGlobalId = await layerClient.QueryAttachmentsAsync(new AttachmentQuery {
    GlobalIds = ["{parent-global-id}"],
    ReturnMetadata = true
});

var byExpression = await layerClient.QueryAttachmentsAsync(new AttachmentQuery {
    DefinitionExpression = "STATUS = 'Active'",
    AttachmentTypes = ["image/jpeg", "application/pdf"],
    Keywords = ["inspection"],
    MinimumSizeBytes = 1024,
    MaximumSizeBytes = 10_000_000,
    ResultOffset = 0,
    ResultRecordCount = 100
});
```

`ObjectIds` and `GlobalIds` cannot be combined in the same `AttachmentQuery`. At least one of `ObjectIds`, `GlobalIds`, or `DefinitionExpression` must be provided.

### Query attachment counts

Use `QueryAttachmentCountsAsync` when you only need the number of attachments per parent feature.

```csharp
var counts = await layerClient.QueryAttachmentCountsAsync(new AttachmentQuery {
    DefinitionExpression = "STATUS = 'Active'"
});

foreach (var group in counts) {
    Console.WriteLine($"Parent object: {group.SourceObjectId}");
    Console.WriteLine($"Parent global ID: {group.SourceGlobalId}");
    Console.WriteLine($"Attachment count: {group.Count}");
}
```

Attachment count queries require `schema.Capabilities.SupportsQueryAttachmentsCountOnly`.

### Attachment ordering and pagination

Attachment queries can request server-side ordering and result windows when the service supports them.

```csharp
var page = await layerClient.QueryAttachmentsAsync(new AttachmentQuery {
    ObjectIds = [1, 2, 3],
    OrderByFields = ["size DESC", "name ASC"],
    ResultOffset = 0,
    ResultRecordCount = 50,
    ReturnMetadata = true
});
```

Use `schema.Capabilities.SupportsQueryAttachmentOrderByFields` before setting `OrderByFields` when you want proactive capability checks.

### Attachment request method selection

`queryAttachments` uses the same GET/POST/Auto transport selection as layer-level `query`. This helps avoid overly long URLs when object IDs, global IDs, definition expressions, filters, or pagination parameters become large.

### Download an attachment

```csharp
var content = await layerClient.DownloadAttachmentAsync(objectId: 1, attachmentId: 10);
File.WriteAllBytes(content.FileName ?? "attachment.bin", content.Content);
```

### Add an attachment

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("harbor-photo.jpg");

var result = await layerClient.AddAttachmentAsync(new AddAttachmentRequest {
    ObjectId = 1,
    Content = stream,
    FileName = "harbor-photo.jpg",
    ContentType = "image/jpeg"
});
```

### Update an attachment

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("harbor-photo-updated.jpg");

var result = await layerClient.UpdateAttachmentAsync(new UpdateAttachmentRequest {
    ObjectId = 1,
    AttachmentId = 10,
    Content = stream,
    FileName = "harbor-photo-updated.jpg",
    ContentType = "image/jpeg"
});
```

### Delete attachments

```csharp
using S100Framework.REST.Models;

var result = await layerClient.DeleteAttachmentsAsync(new DeleteAttachmentsRequest {
    ObjectId = 1,
    AttachmentIds = [10, 11]
});
```

---

## Editing features

### Layer-level `applyEdits`

Use this when you are editing one layer and want the server to complete the request in a single call.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.ApplyEditsAsync(new FeatureEdits {
    Adds = [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
            new Dictionary<string, object?> {
                ["NAME"] = "New harbor"
            })
    ]
});
```

### Layer-level edit convenience wrappers

Use these wrappers when you want a simpler API for normal add, update, or delete-by-object-ID workflows. These wrappers use layer-level `applyEdits` internally and return only the relevant result group.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var addResults = await layerClient.AddFeaturesAsync(
    [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
            new Dictionary<string, object?> {
                ["NAME"] = "New harbor"
            })
    ],
    new FeatureEditOptions {
        RollbackOnFailure = true
    });

var updateResults = await layerClient.UpdateFeaturesAsync(
    [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.35, 56.79)),
            new Dictionary<string, object?> {
                ["OBJECTID"] = 101,
                ["NAME"] = "Updated harbor"
            })
    ]);

var deleteResults = await layerClient.DeleteFeaturesAsync([101, 102]);
```

Use the direct endpoint request models below when you need endpoint-specific parameters such as `returnEditMoment`, `returnDeleteResults`, delete-by-where, or delete-by-geometry.

### Direct `addFeatures`

Use direct `addFeatures` when you explicitly want the ArcGIS REST `addFeatures` endpoint instead of the `applyEdits` wrapper.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.AddFeaturesAsync(new AddFeaturesRequest {
    Features = [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
            new Dictionary<string, object?> {
                ["NAME"] = "New harbor"
            })
    ],
    RollbackOnFailure = true,
    UseGlobalIds = false,
    ReturnEditMoment = true
});

foreach (var addResult in result.AddResults) {
    Console.WriteLine($"{addResult.ObjectId}: {addResult.Success}");
}

Console.WriteLine(result.EditMoment);
```

### Direct `updateFeatures`

Use direct `updateFeatures` when you explicitly want the ArcGIS REST `updateFeatures` endpoint instead of the `applyEdits` wrapper.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.UpdateFeaturesAsync(new UpdateFeaturesRequest {
    Features = [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.35, 56.79)),
            new Dictionary<string, object?> {
                ["OBJECTID"] = 101,
                ["NAME"] = "Updated harbor"
            })
    ],
    RollbackOnFailure = true,
    ReturnEditMoment = true
});

foreach (var updateResult in result.UpdateResults) {
    Console.WriteLine($"{updateResult.ObjectId}: {updateResult.Success}");
}
```

### Direct `deleteFeatures`

Use direct `deleteFeatures` when you need endpoint-specific delete behavior, including delete-by-where or delete-by-geometry.

```csharp
using S100Framework.REST.Models;

var result = await layerClient.DeleteFeaturesAsync(new DeleteFeaturesRequest {
    ObjectIds = [101, 102],
    RollbackOnFailure = true,
    ReturnDeleteResults = true,
    ReturnEditMoment = true
});

foreach (var deleteResult in result.DeleteResults) {
    Console.WriteLine($"{deleteResult.ObjectId}: {deleteResult.Success}");
}
```

Delete by where clause:

```csharp
var result = await layerClient.DeleteFeaturesAsync(DeleteFeaturesRequest.ForWhere(
    "STATUS = 'Obsolete'"));
```

Delete by spatial filter:

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var result = await layerClient.DeleteFeaturesAsync(new DeleteFeaturesRequest {
    Where = "STATUS = 'Obsolete'",
    SpatialFilter = FeatureSpatialFilter.FromEnvelope(
        new Envelope(10, 11, 55, 56),
        4326),
    ReturnDeleteResults = false
});
```

Direct `deleteFeatures` returns either per-feature `DeleteResults` or a top-level success value, depending on service behavior and `ReturnDeleteResults`.

### Layer-level async `applyEdits` with manual submit/status/result

Use this when you want explicit control over polling, logging, retry behavior, or timeout handling.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var submission = await layerClient.SubmitApplyEditsAsync(new FeatureEdits {
    Adds = [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
            new Dictionary<string, object?> {
                ["NAME"] = "New harbor"
            })
    ]
});

ApplyEditsResult result;

if (submission.Result is not null) {
    result = submission.Result;
}
else {
    if (submission.StatusUrl is null) {
        throw new InvalidOperationException("The server did not return a result or a status URL.");
    }

    while (true) {
        var status = await layerClient.GetApplyEditsStatusAsync(submission.StatusUrl);

        if (status.IsCompleted && status.ResultUrl is not null) {
            result = await layerClient.GetApplyEditsResultAsync(status.ResultUrl);
            break;
        }

        if (status.IsTerminal) {
            throw new InvalidOperationException(
                $"applyEdits ended with terminal status '{status.Status}'.");
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
```

### Layer-level async `applyEdits` with wait helper

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.WaitForApplyEditsCompletionAsync(
    new FeatureEdits {
        Adds = [
            new EditableFeature(
                geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
                new Dictionary<string, object?> {
                    ["NAME"] = "New harbor"
                })
        ]
    },
    new ApplyEditsWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(1)
    });
```

### Service-level `applyEdits`

Use this when you need edits across multiple layers and want the server to complete the request in a single call.

```csharp
using S100Framework.REST.Models;

var result = await client.ApplyEditsAsync(new FeatureServiceEdits {
    Layers = [
        new ServiceLayerEdits {
            LayerId = 0,
            DeleteObjectIds = [101]
        },
        new ServiceLayerEdits {
            LayerId = 1,
            DeleteObjectIds = [202]
        }
    ]
});
```

### Service-level async `applyEdits` with wait helper

```csharp
using S100Framework.REST.Models;

var result = await client.WaitForApplyEditsCompletionAsync(
    new FeatureServiceEdits {
        Layers = [
            new ServiceLayerEdits {
                LayerId = 0,
                DeleteObjectIds = [101]
            },
            new ServiceLayerEdits {
                LayerId = 1,
                DeleteObjectIds = [202]
            }
        ]
    },
    new ApplyEditsWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(1)
    });
```


---

## Uploads

Use uploads when you want the service to temporarily store a file that another operation can consume. Common use cases in this package are upload-backed `append` and upload-backed replica synchronization through `editsUploadId`.

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("source.gdb.zip");

var upload = await client.UploadItemAsync(new FeatureServiceUploadRequest {
    Content = stream,
    FileName = "source.gdb.zip",
    ContentType = "application/zip",
    Description = "Append source"
});

Console.WriteLine(upload.ItemId);
```

The returned `ItemId` can be passed as `AppendUploadId`.

### Delete an upload item

Use `DeleteUploadItemAsync` when you want to clean up a temporary server-side upload item after the workflow has finished.

```csharp
var deleteResult = await client.DeleteUploadItemAsync(upload.ItemId);

if (!deleteResult.Success) {
    Console.WriteLine($"Upload item {deleteResult.ItemId} was not deleted.");
}
```


---

## `append`

Use `append` when you want the service to ingest a larger payload asynchronously. This is usually the right tool for bulk loads and controlled upsert scenarios.

### Check append support first

Service-level append:

```csharp
var metadata = await client.GetMetadataAsync();

if (!metadata.Capabilities.SupportsAppend) {
    throw new InvalidOperationException("The service does not support append.");
}

Console.WriteLine(string.Join(", ", metadata.SupportedAppendFormats));
Console.WriteLine(string.Join(", ", metadata.SupportedExportFormats));
```

Layer-level append:

```csharp
var schema = await layerClient.GetSchemaAsync();

if (!schema.Capabilities.SupportsAppend) {
    throw new InvalidOperationException("The layer does not support append.");
}

Console.WriteLine(string.Join(", ", schema.SupportedAppendFormats));
```

### Important note about `upsert`

When `upsert = true`, the library validates an important Esri rule before the request is sent:

- append upsert is **not** supported when sync is enabled
- append upsert is **not** supported when change tracking is enabled

That is validated from service metadata before the append request is submitted.

### Service-level append using literal `edits`

Use this when you already have an append-compatible JSON payload and want to submit it directly.

```csharp
using S100Framework.REST.Models;

var submission = await client.SubmitAppendAsync(new FeatureServiceAppendEditsRequest {
    Layers = [0],
    EditsJson = """
    {
      "layers": [
        {
          "layerDefinition": { "id": 0 },
          "featureSet": {
            "features": [
              {
                "attributes": {
                  "NAME": "Harbor A"
                },
                "geometry": {
                  "x": 12.34,
                  "y": 56.78,
                  "spatialReference": { "wkid": 4326 }
                }
              }
            ]
          }
        }
      ]
    }
    """,
    RollbackOnFailure = true,
    ReturnEditMoment = true
});

Console.WriteLine(submission.Status);
Console.WriteLine(submission.JobId);
Console.WriteLine(submission.StatusUrl);
```

### Service-level append using `appendItemId`

Use this when the source data already exists as a portal item.

```csharp
using S100Framework.REST.Models;

var status = await client.WaitForAppendCompletionAsync(
    new FeatureServiceAppendItemRequest {
        Layers = [0],
        AppendItemId = "894d8c12438v4baaac164b636f7e1e2f",
        AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
        LayerMappings = [
            new FeatureServiceAppendLayerMapping {
                Id = 0,
                SourceTableName = "Countries",
                FieldMappings = [
                    new FeatureServiceAppendFieldMapping {
                        Name = "NAME",
                        Source = "Country"
                    }
                ]
            }
        ],
        RollbackOnFailure = true
    },
    new AppendWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });
```

### Service-level append using `appendUploadId`

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("source.gdb.zip");

var upload = await client.UploadItemAsync(new FeatureServiceUploadRequest {
    Content = stream,
    FileName = "source.gdb.zip",
    ContentType = "application/zip"
});

var status = await client.WaitForAppendCompletionAsync(
    new FeatureServiceAppendUploadRequest {
        Layers = [0],
        AppendUploadId = upload.ItemId,
        AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
        LayerMappings = [
            new FeatureServiceAppendLayerMapping {
                Id = 0,
                SourceTableName = "USA"
            }
        ],
        RollbackOnFailure = true
    },
    new AppendWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });
```

### Layer-level append using `appendUploadId`

Use layer-level append when your source maps directly into one target layer.

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("source.gdb.zip");

var upload = await client.UploadItemAsync(new FeatureServiceUploadRequest {
    Content = stream,
    FileName = "source.gdb.zip",
    ContentType = "application/zip"
});

var status = await layerClient.WaitForAppendCompletionAsync(
    new FeatureLayerAppendUploadRequest {
        AppendUploadId = upload.ItemId,
        AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
        SourceTableName = "USA",
        FieldMappings = [
            new FeatureServiceAppendFieldMapping {
                Name = "NAME",
                Source = "SOURCE_NAME"
            }
        ],
        Upsert = true,
        UpsertMatchingField = "GLOBALID",
        UpdateGeometry = true,
        RollbackOnFailure = true
    },
    new AppendWaitOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });
```

Layer-level append also supports `FeatureLayerAppendEditsRequest` and `FeatureLayerAppendItemRequest`.

### Upload-backed append with cleanup

Use `try`/`finally` when the upload item should be deleted even if append polling fails.

```csharp
await using var stream = File.OpenRead("source.gdb.zip");

var upload = await client.UploadItemAsync(new FeatureServiceUploadRequest {
    Content = stream,
    FileName = "source.gdb.zip",
    ContentType = "application/zip"
});

try {
    var status = await layerClient.WaitForAppendCompletionAsync(
        new FeatureLayerAppendUploadRequest {
            AppendUploadId = upload.ItemId,
            AppendUploadFormat = FeatureServiceAppendSourceFormat.FileGeodatabase,
            SourceTableName = "USA",
            RollbackOnFailure = true
        },
        new AppendWaitOptions {
            PollInterval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromMinutes(2)
        });
}
finally {
    await client.DeleteUploadItemAsync(upload.ItemId);
}
```

### Poll an append job manually

```csharp
var submission = await client.SubmitAppendAsync(new FeatureServiceAppendEditsRequest {
    Layers = [0],
    EditsJson = editsJson,
    RollbackOnFailure = true
});

if (submission.StatusUrl is not null) {
    while (true) {
        var status = await client.GetAppendStatusAsync(submission.StatusUrl);

        if (status.IsCompleted) {
            Console.WriteLine($"Records processed: {status.RecordCount}");
            break;
        }

        if (status.IsTerminal) {
            throw new InvalidOperationException($"append ended with status '{status.Status}'.");
        }

        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
```

---

## `extractChanges`

Use `extractChanges` when you want to ask the server:

> What has changed since my last sync?

This is **not** the first thing most consumers need. It is mainly useful for incremental synchronization, cache refresh, local replicas, or jobs that only want deltas.

### Important idea: what `LayerServerGens` means

`LayerServerGens` is a bookmark from the last successful change read.

A typical flow is:

1. Call `extractChanges`
2. Receive changes plus updated `layerServerGens`
3. Store those returned generation values somewhere
4. Send those values back next time to ask for changes since the last run

So this:

```csharp
LayerServerGens = [
    new ExtractChangesLayerServerGen(0, 1653608093000)
]
```

means roughly:

> Give me changes for layer `0` since server generation `1653608093000`.

### Basic `extractChanges` example

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Json
});
```

After a successful call, store the returned `layerServerGens` values if you want to continue incremental sync later.

### `extractChanges` with layer queries and geometry filter

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
        [0] = new() {
            QueryOption = ExtractChangesLayerQueryOption.UseFilter,
            Where = "STATUS = 'Active'"
        }
    },
    SpatialFilter = ExtractChangesSpatialFilter.FromEnvelope(
        new Envelope(10, 11, 55, 56),
        4326),
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Json
});
```

### `extractChanges` with IDs only

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnIdsOnly = true,
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Json
});
```

### `extractChanges` with extent only

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnExtentOnly = true,
    ChangesExtentGridCell = ExtractChangesExtentGridCell.Medium,
    DataFormat = ExtractChangesDataFormat.Json
});
```

### Async `extractChanges`

Some `extractChanges` requests are handled asynchronously. In that case the first response contains a `statusUrl` instead of the final data, and the job must be polled until completion.

```csharp
using S100Framework.REST.Models;

var submission = await client.SubmitExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Sqlite
});

if (submission.StatusUrl is not null) {
    var status = await client.GetExtractChangesStatusAsync(submission.StatusUrl);
}
```

### Poll until async `extractChanges` completes

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var submission = await client.SubmitExtractChangesAsync(new ExtractChangesRequest {
    Layers = [0],
    LayerServerGens = [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Sqlite
});

var status = await client.WaitForExtractChangesCompletionAsync(
    submission.StatusUrl!,
    new ExtractChangesPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });
```

### Submit, poll, and download a SQLite result file in one call

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var file = await client.SubmitAndDownloadExtractChangesFileAsync(
    new ExtractChangesRequest {
        Layers = [0],
        LayerServerGens = [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        ReturnInserts = true,
        ReturnUpdates = true,
        ReturnDeletes = true,
        DataFormat = ExtractChangesDataFormat.Sqlite
    },
    new ExtractChangesPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });

File.WriteAllBytes(file.FileName ?? "changes.sqlite", file.Content);
```

### Important note about `Sqlite`

In the .NET API you use:

```csharp
ExtractChangesDataFormat.Sqlite
```

But the ArcGIS REST parameter value is spelled:

```text
sqllite
```

That spelling comes from the REST API itself. The library handles that mapping internally, so callers should keep using the .NET enum value `Sqlite`.

---


## Replicas and synchronization

Use replica operations when you want a syncable offline or local workflow backed by ArcGIS Feature Service replica generations.

The package supports these service-level replica operations:

- `createReplica`
- `replicas`
- `synchronizeReplica`
- `unRegisterReplica`

It also provides state-aware helpers that keep the ArcGIS generation bookkeeping out of consumer code:

- `CreateReplicaStateAsync(...)`
- `SynchronizeReplicaStateAsync(...)` for download-only sync
- `SynchronizeReplicaStateUploadAsync(...)` for upload-only sync
- `SynchronizeReplicaStateBidirectionalAsync(...)` for upload + download sync
- `UnregisterReplicaStateAsync(...)`

### Important idea: persist `ReplicaSynchronizationState`

`ReplicaSynchronizationState` is the bookmark for future replica sync calls. Persist it in your application storage after `createReplica`, and replace it with the returned updated state after successful download-only or bidirectional sync.

The package intentionally does **not** include a database, file store, or application-specific persistence provider. Store the state in the consuming application, for example as JSON.

```csharp
using System.Text.Json;
using S100Framework.REST.Models;

var json = JsonSerializer.Serialize(state);
var restoredState = JsonSerializer.Deserialize<ReplicaSynchronizationState>(json);

restoredState!.Validate();
```

### Check replica capabilities

```csharp
using S100Framework.REST.Extensions;

var metadata = await client.GetMetadataAsync();

if (!metadata.SupportsReplicaResource()) {
    foreach (var issue in metadata.GetReplicaCapabilityIssues()) {
        Console.WriteLine(issue);
    }

    throw new InvalidOperationException("The service does not expose replica support.");
}

Console.WriteLine($"Supports async replica jobs: {metadata.SupportsAsyncReplicaJobs()}");
Console.WriteLine($"Supports upload/bidirectional sync direction control: {metadata.SupportsReplicaSyncDirectionControl()}");
Console.WriteLine($"Supports rollback on failure: {metadata.SupportsReplicaRollbackOnFailure()}");
Console.WriteLine($"Supported export formats: {string.Join(", ", metadata.SupportedExportFormats)}");
```

`GetReplicaCapabilityIssues()` returns user-readable capability issues. Some issues are hard blockers for core replica support, while others explain why upload-oriented options such as sync direction control or rollback-on-failure may not be available.

### Create a syncable replica and initial state

Use `CreateReplicaStateAsync(...)` when you want the package to submit `createReplica`, poll if needed, download the result file, and create the initial persisted state.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var createResult = await client.CreateReplicaStateAsync(
    new CreateReplicaRequest {
        ReplicaName = "my-offline-replica",
        Layers = [0],
        SpatialFilter = FeatureSpatialFilter.FromEnvelope(
            new Envelope(10, 20, 55, 56),
            inSrid: 4326),
        SyncModel = CreateReplicaSyncModel.PerReplica,
        DataFormat = CreateReplicaDataFormat.Json,
        TransportType = CreateReplicaTransportType.Url,
        IsAsync = true
    },
    new ReplicaPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });

ReplicaSynchronizationState state = createResult.InitialState;

// Persist this in your application storage before the next sync.
Console.WriteLine(state.ReplicaId);
Console.WriteLine(state.ReplicaServerGen);
```

`CreateReplicaStateAsync(...)` requires URL transport because it downloads the result file. It also requires a syncable replica, so `CreateReplicaSyncModel.None` is intentionally rejected by this helper.

### Download-only sync from state

Use `SynchronizeReplicaStateAsync(...)` when you only want server-side changes since the persisted generation.

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var syncResult = await client.SynchronizeReplicaStateAsync(
    state,
    dataFormat: SynchronizeReplicaDataFormat.Json,
    isAsync: true,
    pollingOptions: new ReplicaPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });

state = syncResult.UpdatedState;

// Persist the updated state after the result has been processed successfully.
Console.WriteLine(syncResult.File.FileName);
Console.WriteLine(syncResult.File.ContentType);
```

For JSON result files, the helper can update state from generation values returned in either the operation response, async status response, or downloaded JSON result file. For SQLite result files, the helper can only update state automatically when the generation values are returned outside the SQLite file.

`SynchronizeReplicaStateAsync(...)` does not allow `closeReplica = true`, because it returns state intended for future use. Use `SubmitSynchronizeReplicaAsync(...)` directly when closing a replica as part of a custom workflow.

### Build a download request without submitting it

Use `ToDownloadRequest(...)` when you want to inspect, log, or submit the low-level request yourself.

```csharp
var request = state.ToDownloadRequest(
    dataFormat: SynchronizeReplicaDataFormat.Json,
    transportType: SynchronizeReplicaTransportType.Url,
    isAsync: true);

request.Validate();
```

### Upload-only sync

Use upload-only sync when you want to send local edits to the service without downloading server-side changes in the same call. The returned state is intentionally unchanged.

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var uploadResult = await client.SynchronizeReplicaStateUploadAsync(
    state,
    new SynchronizeReplicaStateUploadRequest {
        Edits = new ReplicaEdits {
            Layers = [
                new ReplicaLayerEdits {
                    Id = 0,
                    AddsJson = """
                    [
                      {
                        "attributes": {
                          "globalID": "{11111111-1111-1111-1111-111111111111}",
                          "name": "New feature"
                        }
                      }
                    ]
                    """
                }
            ]
        },
        RollbackOnFailure = true,
        ReturnIdsForAdds = true,
        ThrowOnEditErrors = true
    });

state = uploadResult.State;
```

`ReplicaEdits` is schema-agnostic. The package validates that raw JSON is syntactically valid and has the expected high-level shape, but it does not validate your layer-specific field names, geometry shape, or business rules.

### Bidirectional sync

Use bidirectional sync when you want to upload local edits and download server-side changes in one operation. The helper downloads the JSON result file, parses upload edit results, and updates the persisted state from returned generation values.

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var bidirectionalResult = await client.SynchronizeReplicaStateBidirectionalAsync(
    state,
    new SynchronizeReplicaStateBidirectionalRequest {
        EditsJson = """
        {
          "layers": []
        }
        """,
        RollbackOnFailure = true,
        ReturnIdsForAdds = true,
        ThrowOnEditErrors = true,
        IsAsync = true,
        PollingOptions = new ReplicaPollingOptions {
            PollInterval = TimeSpan.FromSeconds(2),
            Timeout = TimeSpan.FromMinutes(2)
        }
    });

state = bidirectionalResult.UpdatedState;

Console.WriteLine($"Edit results: {bidirectionalResult.JsonResult.EditResultCount}");
Console.WriteLine($"Edit errors: {bidirectionalResult.JsonResult.FailedEditResultCount}");
```

### Upload large edit payloads by upload ID

For large edit payloads, upload a file first and pass the server-side upload item ID as `EditsUploadId`. The package sends `editsUploadFormat=sqlite` for uploaded edit payloads.

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("replica-edits.geodatabase");

var upload = await client.UploadItemAsync(new FeatureServiceUploadRequest {
    Content = stream,
    FileName = "replica-edits.geodatabase",
    ContentType = "application/octet-stream"
});

try {
    var result = await client.SynchronizeReplicaStateBidirectionalAsync(
        state,
        new SynchronizeReplicaStateBidirectionalRequest {
            EditsUploadId = upload.ItemId,
            RollbackOnFailure = true,
            ThrowOnEditErrors = true
        });

    state = result.UpdatedState;
}
finally {
    await client.DeleteUploadItemAsync(upload.ItemId);
}
```

### Build upload or bidirectional requests without submitting them

Use these helpers when you want custom orchestration but still want the package to build a validated REST request from persisted state.

```csharp
var uploadRequest = state.ToUploadRequest(
    new SynchronizeReplicaStateUploadRequest {
        EditsJson = """{"layers": []}"""
    });

var bidirectionalRequest = state.ToBidirectionalRequest(
    new SynchronizeReplicaStateBidirectionalRequest {
        EditsJson = """{"layers": []}"""
    });

uploadRequest.Validate();
bidirectionalRequest.Validate();
```

### Inspect replica JSON result files

Use `ReadJsonResultFile()` when you want to inspect a downloaded JSON result file from `createReplica` or `synchronizeReplica`.

```csharp
using S100Framework.REST.Extensions;

ReplicaJsonResultFile jsonResult = syncResult.File.ReadJsonResultFile();

foreach (var editError in jsonResult.GetLayerEditErrors()) {
    Console.WriteLine(
        $"Layer {editError.LayerId} {editError.Operation} failed for objectId {editError.ObjectId}: {editError.ErrorDescription}");
}

jsonResult.ThrowIfEditErrors();
```

`ThrowIfEditErrors()` throws `ReplicaEditResultsException`, which exposes the failed edit results through the `Errors` property.

### List and unregister replicas

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var replicas = await client.GetReplicasAsync(new FeatureServiceReplicasRequest {
    ReturnVersion = true,
    ReturnLastSyncDate = true
});

foreach (var replica in replicas.Replicas) {
    Console.WriteLine($"{replica.ReplicaId}: {replica.ReplicaName}");
}

var unregisterResult = await client.UnregisterReplicaStateAsync(state);

if (!unregisterResult.Success) {
    Console.WriteLine($"Replica {unregisterResult.ReplicaId} was not unregistered.");
}
```

`UnregisterReplicaStateAsync(...)` requires a concrete replica ID and rejects the wildcard `*`. Use `UnregisterReplicaAsync(...)` directly if you intentionally want to unregister all replicas supported by the service.

### Low-level replica APIs

The state-aware helpers are convenience APIs. Use the lower-level service methods when you need custom behavior:

```csharp
var submission = await client.SubmitSynchronizeReplicaAsync(new SynchronizeReplicaRequest {
    ReplicaId = state.ReplicaId,
    ReplicaServerGen = state.ReplicaServerGen,
    SyncDirection = SynchronizeReplicaSyncDirection.Download,
    DataFormat = SynchronizeReplicaDataFormat.Json,
    TransportType = SynchronizeReplicaTransportType.Url
});

if (submission.IsPending) {
    var status = await client.WaitForSynchronizeReplicaCompletionAsync(
        submission.StatusUrl!,
        new ReplicaPollingOptions());

    if (status.ResultUrl is not null) {
        var file = await client.DownloadSynchronizeReplicaFileAsync(status.ResultUrl);
    }
}
```

### Replica support boundaries

Supported:

- client target replicas
- `perReplica` and `perLayer` sync models
- `createReplica` with JSON or SQLite result files
- download-only `synchronizeReplica`
- upload-only `synchronizeReplica`
- bidirectional `synchronizeReplica`
- raw JSON edit payloads through `EditsJson`
- schema-agnostic edit payload building through `ReplicaEdits`
- uploaded SQLite edit payloads through `EditsUploadId`
- JSON result file inspection and edit result diagnostics
- state-aware helper workflows for create, download, upload, bidirectional sync, and unregister

Not currently provided by the package:

- internal persistence or offline database storage
- SQLite result file parsing
- strongly typed schema-specific offline feature models
- automatic conflict resolution
- interactive authentication or environment-specific login flows
- server-to-server replica workflows

---

## Public Esri JSON converters

The package includes public converters for Esri JSON geometry and feature payloads.

### Geometry to Esri JSON

```csharp
using S100Framework.REST.Serialization;

var esriGeometryJson = EsriJsonGeometryConverter.Serialize(feature.Geometry!);
```

### Esri JSON to NTS geometry

```csharp
using S100Framework.REST.Configuration;
using S100Framework.REST.Serialization;

var geometry = EsriJsonGeometryConverter.Deserialize(
    esriJson,
    geometryType: "esriGeometryPolyline",
    trueCurveHandling: TrueCurveHandling.LinearizeCircularArcs,
    circularArcSegmentCount: 16);
```

### `FeatureRecord` to Esri feature JSON

```csharp
using S100Framework.REST.Serialization;

var esriFeatureJson = EsriJsonFeatureConverter.SerializeFeature(
    feature,
    objectIdFieldName: "OBJECTID");
```

### Many `FeatureRecord` values to an Esri feature set

```csharp
using S100Framework.REST.Serialization;

var esriFeatureSetJson = EsriJsonFeatureConverter.SerializeFeatureSet(features, schema);
```

---

## Capability checks

Advanced operations are not supported by every Feature Service. The library exposes capability information and also validates important cases at runtime.

### Layer capabilities

```csharp
var schema = await layerClient.GetSchemaAsync();

Console.WriteLine(schema.Capabilities.HasAttachments);
Console.WriteLine(schema.Capabilities.SupportsQueryAttachments);
Console.WriteLine(schema.Capabilities.SupportsTopFeaturesQuery);
Console.WriteLine(schema.Capabilities.SupportsPagination);
Console.WriteLine(schema.Capabilities.SupportsPaginationOnAggregatedQueries);
Console.WriteLine(schema.Capabilities.SupportsQueryRelatedPagination);
Console.WriteLine(schema.Capabilities.SupportsAdvancedQueryRelated);
Console.WriteLine(schema.Capabilities.SupportsAsyncApplyEdits);
Console.WriteLine(schema.Capabilities.SupportsReturningGeometryEnvelope);
Console.WriteLine(schema.Capabilities.SupportsFullTextSearch);
Console.WriteLine(schema.Capabilities.SupportsPercentileStatistics);
Console.WriteLine(schema.Capabilities.SupportsAppend);
Console.WriteLine(schema.Capabilities.SupportsQueryDateBins);
Console.WriteLine(schema.Capabilities.SupportsQueryAnalytic);
Console.WriteLine(schema.Capabilities.SupportsAsyncQueryAnalytic);
Console.WriteLine(schema.Capabilities.SupportsCalculate);
Console.WriteLine(schema.Capabilities.SupportsAsyncCalculate);
Console.WriteLine(schema.Capabilities.SupportsReturningQueryExtent);
Console.WriteLine(schema.Capabilities.SupportsReturningGeometryCentroid);
Console.WriteLine(schema.Capabilities.SupportsDefaultSrid);
Console.WriteLine(schema.Capabilities.SupportsOutFieldSqlExpression);
Console.WriteLine(schema.Capabilities.SupportsSqlExpression);
Console.WriteLine(schema.Capabilities.SupportsHavingClause);
Console.WriteLine(schema.Capabilities.SupportsQueryWithDistance);
Console.WriteLine(schema.Capabilities.SupportsQueryWithResultType);
Console.WriteLine(schema.Capabilities.SupportsQueryWithHistoricMoment);
Console.WriteLine(schema.Capabilities.SupportsQueryWithDatumTransformation);
Console.WriteLine(schema.Capabilities.SupportsCoordinatesQuantization);
Console.WriteLine(schema.Capabilities.SupportsCurrentUserQueries);
Console.WriteLine(schema.Capabilities.SupportsQueryWithCacheHint);
Console.WriteLine(schema.Capabilities.SupportsQueryAttachmentsCountOnly);
Console.WriteLine(schema.Capabilities.SupportsQueryAttachmentOrderByFields);
Console.WriteLine(schema.HasContingentValuesDefinition);
Console.WriteLine(schema.SupportsUniqueIds);
```

### Service capabilities

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine(metadata.Capabilities.SupportsQuery);
Console.WriteLine(metadata.Capabilities.SupportsEditing);
Console.WriteLine(metadata.Capabilities.SupportsUploads);
Console.WriteLine(metadata.Capabilities.SupportsChangeTracking);
Console.WriteLine(metadata.Capabilities.SupportsAsyncApplyEdits);
Console.WriteLine(metadata.Capabilities.SupportsAppend);
Console.WriteLine(metadata.SupportsReplicaResource());
Console.WriteLine(metadata.SupportsAsyncReplicaJobs());
Console.WriteLine(metadata.SupportsReplicaSyncDirectionControl());
Console.WriteLine(metadata.SupportsReplicaRollbackOnFailure());
Console.WriteLine(metadata.Capabilities.SupportsQueryDomains);
Console.WriteLine(metadata.Capabilities.SupportsQueryDataElements);
Console.WriteLine(metadata.Capabilities.SupportsQueryContingentValues);
Console.WriteLine(metadata.Capabilities.SupportsRelationshipsResource);
Console.WriteLine(string.Join(", ", metadata.SupportedAppendFormats));
Console.WriteLine(string.Join(", ", metadata.SupportedExportFormats));
```

### `extractChanges` capabilities

```csharp
var metadata = await client.GetMetadataAsync();

if (metadata.ExtractChangesCapabilities is not null) {
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsLayerQueries);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsGeometry);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsReturnAttachments);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsReturnIdsOnly);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsReturnExtentOnly);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsFieldsToCompare);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsServerGens);
    Console.WriteLine(metadata.ExtractChangesCapabilities.SupportsReturnHasGeometryUpdates);
}
```

### Rule of thumb

- For ordinary queries, you usually do not need to think much about capabilities.
- For attachments, attachment count queries, attachment query ordering, top features, advanced related queries, service-level relationships, service-level query, statistics pagination, percentile statistics, `queryDomains`, `queryDataElements`, `queryContingentValues`, `queryAnalytic`, synchronous/asynchronous `calculate`, direct feature edit endpoints, `extractChanges`, replica workflows, `append`, uploads, and bin queries, check capabilities first.
- Even if you skip the manual check, the client will still fail fast for important unsupported operations.

---

## Query request method selection

Several query-style endpoints can use GET or POST. The client supports three modes through `FeatureServiceClientOptions.QueryRequestMethodPreference`:

- `Auto`
- `Get`
- `Post`

`Auto` uses GET for shorter requests and switches to POST when the generated query URL becomes too long. This applies to ordinary layer queries, related-record queries, top-features queries, and attachment queries.

```csharp
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
        AutoPostQueryLengthThreshold = 1800
    });
```

---

## Curve handling

The library returns NetTopologySuite geometries.

When `ReturnTrueCurves` is disabled, the server is expected to return densified linear geometries. When `ReturnTrueCurves` is enabled, the client can currently linearize circular-arc true curves for `curvePaths` and `curveRings`. Other true-curve segment types are not yet supported.

---

## Notes and limitations

- The library works directly against Esri Feature Service REST endpoints.
- It does not use ArcGIS SDK licensing.
- Advanced Feature Service capabilities vary between services.
- `extractChanges` is mainly for incremental sync scenarios.
- Replica workflows are for Feature Services that advertise sync support and expose sync capabilities.
- State-aware replica helpers require consumers to persist `ReplicaSynchronizationState` outside the package.
- Upload-only replica sync returns the original state unchanged; download-only and bidirectional sync return an updated state when generation values are available.
- JSON replica result files can be inspected for generation values and edit result errors. SQLite replica result files are downloaded as files but are not parsed by this package.
- Async `extractChanges` SQLite results are downloaded as files.
- The ArcGIS REST API uses the literal `sqllite` for the SQLite data format parameter. The library hides that detail behind `ExtractChangesDataFormat.Sqlite`.
- `append` support covers service-level append and layer-level append with `edits`, `appendItemId`, and `appendUploadId` sources.
- Upload creation is supported through `UploadItemAsync`, and temporary upload items can be deleted with `DeleteUploadItemAsync` when the server supports the upload delete endpoint.
- `queryDomains` depends on service-level support and only returns domains referenced by the requested layer IDs.
- Service-level `relationships` depends on `SupportsRelationshipsResource` and returns service-wide relationship class metadata.
- Service-level `query` supports feature-set, count, object-ID, and unique-ID result shapes. `QueryAllAsync` and `QueryExtentsAsync` execute layer-level queries so they can forward layer-supported options such as `GdbVersion` and `TimeReferenceUnknownClient`.
- `QueryAllAsync` and `QueryExtentsAsync` are service-client convenience methods that execute layer-level queries and group results by layer.
- Use layer-level query directly when you need streaming control or layer-specific query options that are not represented by `FeatureServiceQueryRequest`.
- Layer-level `ReturnCentroid` maps service-returned feature centroids to `FeatureRecord.Centroid`; missing or null centroid payloads are represented as `null`.
- Advanced related-record queries, related-record counts, and related-record pagination depend on layer-level capability support. Attachment count queries and attachment ordering also depend on layer-level capability support.
- Percentile statistics depend on layer-level support and cannot be combined with unsupported server-side options.
- `queryBins` and `queryDateBins` use raw JSON for bin configuration to avoid over-constraining ArcGIS-supported payload shapes.
- Token acquisition for Portal for ArcGIS login is intentionally outside this package.

---

## Recommended reading order

If this is your first time using the package, read the sections in this order:

1. Create a service client
2. Dependency injection
3. Authentication
4. Get service metadata
5. Get a layer client
6. Get layer schema
7. Basic feature query
8. Advanced feature query
9. Query statistics
10. Query domains
11. Service relationships
12. Service-level query
13. Query related records
14. Top features
15. Estimates
16. Validate SQL
17. Calculate field values
18. Query bins
19. Query date bins
20. Query analytic rows
21. Query data elements
22. Contingent values and field groups
23. Attachments
24. Editing features, edit convenience wrappers, and direct edit endpoints
25. Uploads
26. `append`
27. `extractChanges`
28. Replicas and synchronization
29. Public Esri JSON converters
30. Capability checks
31. Query request method selection
32. Curve handling
33. Notes and limitations

That gets the common read/query/edit cases out of the way before the more specialized bulk-load, sync, metadata, and analysis scenarios.
