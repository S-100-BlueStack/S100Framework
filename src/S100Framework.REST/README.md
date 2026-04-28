# S100Framework.REST

`S100Framework.REST` is a .NET library for reading, editing, and analyzing Esri Feature Services through the ArcGIS REST API and exposing geometry data through NetTopologySuite.

It does **not** depend on ArcGIS SDKs, ArcGIS Runtime, or ArcGIS licensing. It talks directly to Feature Service REST endpoints.

## What this library is for

Use this library when you want to:

- read service metadata, layer metadata, and schema
- query features from an Esri Feature Service
- work with geometries as NetTopologySuite types
- use advanced query options such as temporal filters, request shaping, full text search, unique IDs, datum transformations, quantization, and statistics
- query service domains for one or more layers
- query related records, attachments, top features, estimates, bins, and date bins
- validate SQL expressions before sending dynamic queries
- edit features with layer-level or service-level `applyEdits`
- upload temporary server-side files for append workflows
- bulk-load data with service-level or layer-level `append`
- get change sets from `extractChanges`
- convert between NTS geometries / feature records and Esri JSON

## What most consumers need

Most consumers only need these steps:

1. Create a `FeatureServiceClient`
2. Get a layer client
3. Read schema if needed
4. Run queries or edits

If you do **not** need incremental sync or delta tracking, you can ignore the `extractChanges` section.
If you do **not** need bulk loading, you can ignore the `append` section.
If you do **not** need aggregated analysis, you can ignore `queryBins` and `queryDateBins`.

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
- get service-level layer estimates
- upload files to the service uploads endpoint
- run multi-layer `applyEdits`
- run `extractChanges`
- poll async `extractChanges` jobs
- download async `extractChanges` result files
- submit and poll service-level `append` jobs

### `IFeatureLayerClient`

Use the layer client for layer-level operations:

- read layer schema
- query features
- query counts, object IDs, unique IDs, and extents
- query statistics
- query related records
- query attachments and download attachment content
- query top features
- query bins and date bins
- get layer estimates
- validate SQL expressions
- run layer-level `applyEdits`
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
Console.WriteLine($"Supports query domains: {metadata.Capabilities.SupportsQueryDomains}");
Console.WriteLine($"Append formats: {string.Join(", ", metadata.SupportedAppendFormats)}");
```

This is mostly useful when you want to know what the service supports before doing advanced operations such as attachment edits, `extractChanges`, `append`, uploads, or `queryDomains`.

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
Console.WriteLine($"Supports unique IDs: {schema.SupportsUniqueIds}");

if (schema.UniqueIdInfo is not null) {
    Console.WriteLine($"Unique ID type: {schema.UniqueIdInfo.Type}");
    Console.WriteLine($"Unique ID fields: {string.Join(", ", schema.UniqueIdInfo.Fields)}");
}
```

This is the easiest way to discover what a layer looks like before querying, editing, appending, or using advanced layer operations.

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

### Request shaping with result windows, result type, default SR, and SQL format

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "1=1",
    ResultOffset = 100,
    ResultRecordCount = 50,
    ResultType = FeatureQueryResultType.Standard,
    ReturnExceededLimitFeatures = false,
    DefaultSrid = 4326,
    SqlFormat = FeatureQuerySqlFormat.Standard,
    PageSize = 25
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine(feature.ObjectId);
}
```

`ResultOffset` and `ResultRecordCount` define the requested result window. `PageSize` still controls the client-side batch size while streaming.

Use `ResultType = FeatureQueryResultType.Tile` when you are intentionally using tile-oriented server behavior.

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

## Query related records

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();
var relationshipId = schema.Relationships[0].Id;

var related = await layerClient.QueryRelatedRecordsAsync(new RelatedRecordsQuery {
    ObjectIds = [1, 2, 3],
    RelationshipId = relationshipId,
    OutFields = ["*"]
});
```

If you are unsure which `RelationshipId` to use, inspect `schema.Relationships` first.

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

## Attachments

Attachments are only available when the target layer and service support them.

You can check this up front:

```csharp
var schema = await layerClient.GetSchemaAsync();
var metadata = await client.GetMetadataAsync();

var canReadAttachments = schema.Capabilities.HasAttachments;
var canQueryAttachments = schema.Capabilities.HasAttachments && schema.Capabilities.SupportsQueryAttachments;
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

Use this when you need one request that touches multiple layers and want the server to complete the request in a single call.

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

Use uploads when you want the service to temporarily store a file that another operation can consume. The common use case in this package is upload-backed `append`.

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
Console.WriteLine(metadata.Capabilities.SupportsQueryDomains);
Console.WriteLine(string.Join(", ", metadata.SupportedAppendFormats));
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
- For attachments, top features, advanced related queries, statistics pagination, percentile statistics, `queryDomains`, `extractChanges`, `append`, uploads, and bin queries, check capabilities first.
- Even if you skip the manual check, the client will still fail fast for important unsupported operations.

---

## Query request method selection

Standard layer `query` requests can use GET or POST. The client supports three modes through `FeatureServiceClientOptions.QueryRequestMethodPreference`:

- `Auto`
- `Get`
- `Post`

`Auto` uses GET for shorter requests and switches to POST when the generated query URL becomes too long.

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
- Async `extractChanges` SQLite results are downloaded as files.
- The ArcGIS REST API uses the literal `sqllite` for the SQLite data format parameter. The library hides that detail behind `ExtractChangesDataFormat.Sqlite`.
- `append` support covers service-level append and layer-level append with `edits`, `appendItemId`, and `appendUploadId` sources.
- Upload creation is supported through `UploadItemAsync`, but upload lifecycle cleanup depends on the server and consuming workflow.
- `queryDomains` depends on service-level support and only returns domains referenced by the requested layer IDs.
- Percentile statistics depend on layer-level support and cannot be combined with unsupported server-side options.
- `queryBins` and `queryDateBins` use raw JSON for bin configuration to avoid over-constraining ArcGIS-supported payload shapes.
- Token acquisition for Portal for ArcGIS login is intentionally outside this package.

---

## Recommended reading order

If this is your first time using the package, read the sections in this order:

1. Create a service client
2. Authentication
3. Get a layer client
4. Get layer schema
5. Basic feature query
6. Advanced feature query
7. Editing features
8. Attachments
9. Uploads
10. `append`
11. `extractChanges`
12. Estimates and SQL validation
13. `queryBins` and `queryDateBins`
14. Public Esri JSON converters

That gets the common cases out of the way before the more specialized bulk-load, sync, and analysis scenarios.
