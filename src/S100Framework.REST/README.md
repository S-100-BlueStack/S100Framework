# S100Framework.REST

`S100Framework.REST` is a .NET library for reading and editing Esri Feature Services through the ArcGIS REST API and exposing geometry data through NetTopologySuite.

It does **not** depend on ArcGIS SDKs or ArcGIS runtime components. It talks directly to Feature Service REST endpoints.

## What this library is for

Use this library when you want to:

- read layer metadata and schema
- query features from an Esri Feature Service
- work with geometries as NetTopologySuite types
- edit features with `applyEdits`
- work with attachments
- get change sets from `extractChanges`
- convert between NTS geometries / feature records and Esri JSON

## What most consumers need

Most internal consumers only need these steps:

1. Create a `FeatureServiceClient`
2. Get a layer client
3. Read schema if needed
4. Run queries or edits

If you do **not** need incremental sync or delta tracking, you can ignore the `extractChanges` section.

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
- run multi-layer `applyEdits`
- run `extractChanges`
- poll async `extractChanges` jobs
- download async `extractChanges` result files

### `IFeatureLayerClient`

Use the layer client for layer-level operations:

- read layer schema
- query features
- query counts, object IDs and extents
- query statistics
- query related records
- query attachments and download attachment content
- query top features
- run layer-level `applyEdits`
- add, update and delete attachments

---

## Create a service client

```csharp
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        DefaultPageSize = 100,
        RequestTimeout = TimeSpan.FromSeconds(30),
        FixInvalidGeometries = true,
        PreferLatestWkid = true
    });
```

---

## Authentication

Authentication is intentionally handled through `HttpClient` and optional request authorizers.
That keeps authentication concerns outside the query and edit models.

The library supports three practical patterns:

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

var handler = new HttpClientHandler
{
    UseDefaultCredentials = true
};

using var httpClient = new HttpClient(handler);

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    });
```

This is **not** the same as token-based access through a federated portal.
If your service expects a token, `UseDefaultCredentials = true` is not enough by itself.

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
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

This is the simplest token-based setup.
It is a good choice when another part of your application already handles login.

### Static access token provider

If you want to stay on the token-provider model even when you already have a token, use `StaticFeatureServiceAccessTokenProvider`.

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
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

This is useful when you want one code path for both static tokens and future refreshable providers.

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
    new ArcGisServerGenerateTokenOptions
    {
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
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

Use this pattern when the token source is ArcGIS Server itself.
This provider caches the token and refreshes it before it expires.

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
    new PortalServerTokenExchangeOptions
    {
        GenerateTokenUri = new Uri("https://portal.example.com/portal/sharing/rest/generateToken"),
        ServerUrl = new Uri("https://server.example.com/server")
    });

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(featureServiceTokenProvider);

var client = new FeatureServiceClient(
    serviceHttpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://server.example.com/server/rest/services/MyService/FeatureServer")
    },
    authorizer);
```

In this setup, the library does **not** acquire the portal token itself.
It expects another part of the application to provide that token.

### What the library does not do

The library currently does **not** handle these parts for you:

- interactive portal login
- OAuth browser flows
- Portal for ArcGIS login through Windows / IWA
- turning the current Windows session into a portal token automatically

If you need one of those flows, handle it in the consuming application and then pass the resulting token into the library through a token provider.

Do not hardcode secrets in source code.
Use a secret store, secure local development configuration, or a runtime prompt in sample applications.

---

## Get service metadata

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine($"Layers: {metadata.Layers.Count}");
Console.WriteLine($"Supports editing: {metadata.CapabilityInfo.SupportsEditing}");
Console.WriteLine($"Supports uploads: {metadata.CapabilityInfo.SupportsUploads}");
Console.WriteLine($"Supports change tracking: {metadata.CapabilityInfo.SupportsChangeTracking}");
```

This is mostly useful when you want to know what the service supports before doing advanced operations such as attachment edits or `extractChanges`.

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
```

This is the easiest way to discover what a layer looks like before querying or editing it.

---

## Basic feature query

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var layerClient = client.GetLayerClient(0);

var query = new FeatureQuery
{
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true
};

await foreach (var feature in layerClient.QueryAsync(query))
{
    Console.WriteLine($"ObjectId: {feature.ObjectId}");
    Console.WriteLine($"Name: {feature.Attributes["NAME"]}");
    Console.WriteLine($"Geometry type: {feature.Geometry?.GeometryType}");
}
```

Use this when you want actual features back.

### Query count only

```csharp
var count = await layerClient.QueryCountAsync(new FeatureQuery
{
    Where = "STATUS = 'Active'"
});
```

### Query object IDs only

```csharp
var objectIds = await layerClient.QueryObjectIdsAsync(new FeatureQuery
{
    Where = "STATUS = 'Active'"
});
```

### Query extent only

```csharp
var extent = await layerClient.QueryExtentAsync(new FeatureQuery
{
    Where = "STATUS = 'Active'"
});
```

These are useful when you do not need full features.

---

## Query statistics

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(new FeatureStatisticsQuery
{
    Where = "1=1",
    GroupByFields = ["STATUS"],
    Statistics =
    [
        new FeatureStatisticDefinition(
            StatisticType.Count,
            OnStatisticField: "OBJECTID",
            OutStatisticFieldName: "ROW_COUNT")
    ]
});

foreach (var row in rows)
{
    Console.WriteLine($"Status: {row.Values["STATUS"]}, Count: {row.Values["ROW_COUNT"]}");
}
```

Use this when you need grouped results or aggregates instead of feature rows.

---

## Query related records

```csharp
using S100Framework.REST.Models;

var schema = await layerClient.GetSchemaAsync();
var relationshipId = schema.Relationships[0].Id;

var related = await layerClient.QueryRelatedRecordsAsync(new RelatedRecordsQuery
{
    ObjectIds = [1, 2, 3],
    RelationshipId = relationshipId,
    OutFields = ["*"]
});
```

If you are unsure which `RelationshipId` to use, inspect `schema.Relationships` first.

---

## Attachments

Attachments are only available when the target layer and service support them.

You can check this up front:

```csharp
var schema = await layerClient.GetSchemaAsync();
var metadata = await client.GetMetadataAsync();

var canReadAttachments = schema.Capabilities.HasAttachments;
var canQueryAttachments = schema.Capabilities.HasAttachments && schema.Capabilities.SupportsQueryAttachments;
var canEditAttachments = schema.Capabilities.HasAttachments && metadata.CapabilityInfo.SupportsEditing;
var canUploadAttachments = canEditAttachments && metadata.CapabilityInfo.SupportsUploads;
```

The client also validates these capabilities at runtime and throws a clear exception if the operation is not supported.

### Query attachments

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryAttachmentsAsync(new AttachmentQuery
{
    ObjectIds = [1, 2, 3]
});

foreach (var group in groups)
{
    Console.WriteLine($"Parent object: {group.ParentObjectId}");

    foreach (var attachment in group.Attachments)
    {
        Console.WriteLine($"Attachment: {attachment.Id} - {attachment.Name}");
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

var result = await layerClient.AddAttachmentAsync(new AddAttachmentRequest
{
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

var result = await layerClient.UpdateAttachmentAsync(new UpdateAttachmentRequest
{
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

var result = await layerClient.DeleteAttachmentsAsync(new DeleteAttachmentsRequest
{
    ObjectId = 1,
    AttachmentIds = [10, 11]
});
```

---

## Editing features

### Layer-level `applyEdits`

Use this when you are editing one layer.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.ApplyEditsAsync(new FeatureEdits
{
    Adds =
    [
        new EditableFeature(
            geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
            new Dictionary<string, object?>
            {
                ["NAME"] = "New harbor"
            })
    ]
});
```

### Service-level `applyEdits`

Use this when you need one request that touches multiple layers.

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await client.ApplyEditsAsync(new FeatureServiceEdits
{
    LayerEdits =
    [
        new FeatureServiceLayerEdits(
            LayerId: 0,
            Edits: new FeatureEdits
            {
                Adds =
                [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(12.34, 56.78)),
                        new Dictionary<string, object?>
                        {
                            ["NAME"] = "New harbor"
                        })
                ]
            })
    ]
});
```

---

## `extractChanges`

Use `extractChanges` when you want to ask the server:

> What has changed since my last sync?

This is **not** the first thing most consumers need.
It is mainly useful for incremental synchronization, cache refresh, local replicas, or jobs that only want deltas.

### Important idea: what `LayerServerGens` means

`LayerServerGens` is the part that often looks strange the first time.

Think of it as a **bookmark from the last successful change read**.

A typical flow is:

1. Call `extractChanges`
2. Receive changes plus updated `layerServerGens`
3. Store those returned generation values somewhere
4. Send those values back next time to ask for changes since the last run

So this:

```csharp
LayerServerGens =
[
    new ExtractChangesLayerServerGen(0, 1653608093000)
]
```

means roughly:

> Give me changes for layer `0` since server generation `1653608093000`.

### Basic `extractChanges` example

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Json
});
```

What to remember here:

- `Layers` says which layers you want changes from
- `LayerServerGens` says where your last sync left off
- `ReturnInserts`, `ReturnUpdates` and `ReturnDeletes` decide which change types you want
- `DataFormat = Json` asks for a JSON result

After a successful call, store the returned `layerServerGens` values if you want to continue incremental sync later.

### `extractChanges` with layer queries and geometry filter

Use this only if you want to narrow the result.

Examples:

- only changes for one part of the map
- only changes that match a `where` clause
- only changes for one layer with different rules than another layer

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await client.ExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    LayerQueries = new Dictionary<int, ExtractChangesLayerQuery>
    {
        [0] = new()
        {
            QueryOption = ExtractChangesLayerQueryOption.UseFilter,
            Where = "STATUS = 'Active'"
        }
    },
    SpatialFilter = geometryFactory.ToGeometry(new Envelope(10, 11, 55, 56)),
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Json
});
```

What the extra fields mean:

- `LayerQueries` lets you apply a per-layer filter
- `Where = "STATUS = 'Active'"` means only changes matching that layer filter are returned
- `SpatialFilter` means only changes inside the given area are returned

These options are capability-dependent and should only be used when the service advertises support for them.

### `extractChanges` with IDs only

Use this when you only need object IDs or delete IDs, not full feature payloads.

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
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

Use this when you do **not** need the changed features themselves.
You only want the area where changes happened.

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnExtentOnly = true,
    ChangesExtentGridCell = ExtractChangesExtentGridCell.Medium,
    DataFormat = ExtractChangesDataFormat.Json
});
```

### Async `extractChanges`

Some `extractChanges` requests are handled asynchronously.
In that case the first response contains a `statusUrl` instead of the final data, and the job must be polled until completion.

You can still use the low-level API directly:

```csharp
using S100Framework.REST.Models;

var submission = await client.SubmitExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Sqlite
});

if (submission.StatusUrl is not null)
{
    var status = await client.GetExtractChangesStatusAsync(submission.StatusUrl);
}
```

But most consumers should use the convenience helpers instead.

### Poll until async `extractChanges` completes

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var submission = await client.SubmitExtractChangesAsync(new ExtractChangesRequest
{
    Layers = [0],
    LayerServerGens =
    [
        new ExtractChangesLayerServerGen(0, 1653608093000)
    ],
    ReturnInserts = true,
    ReturnUpdates = true,
    ReturnDeletes = true,
    DataFormat = ExtractChangesDataFormat.Sqlite
});

var status = await client.WaitForExtractChangesCompletionAsync(
    submission.StatusUrl!,
    new ExtractChangesPollingOptions
    {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });
```

### Submit, poll and download a SQLite result file in one call

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var file = await client.SubmitAndDownloadExtractChangesFileAsync(
    new ExtractChangesRequest
    {
        Layers = [0],
        LayerServerGens =
        [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        ReturnInserts = true,
        ReturnUpdates = true,
        ReturnDeletes = true,
        DataFormat = ExtractChangesDataFormat.Sqlite
    },
    new ExtractChangesPollingOptions
    {
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

That spelling comes from the REST API itself.
The library handles that mapping internally, so callers should keep using the .NET enum value `Sqlite`.

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

Advanced operations are not supported by every Feature Service.

The library exposes capability information and also validates important cases at runtime.

### Layer capabilities

```csharp
var schema = await layerClient.GetSchemaAsync();

Console.WriteLine(schema.Capabilities.HasAttachments);
Console.WriteLine(schema.Capabilities.SupportsQueryAttachments);
Console.WriteLine(schema.Capabilities.SupportsTopFeaturesQuery);
Console.WriteLine(schema.Capabilities.SupportsPagination);
Console.WriteLine(schema.Capabilities.SupportsAsyncApplyEdits);
```

### Service capabilities

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine(metadata.CapabilityInfo.SupportsQuery);
Console.WriteLine(metadata.CapabilityInfo.SupportsEditing);
Console.WriteLine(metadata.CapabilityInfo.SupportsUploads);
Console.WriteLine(metadata.CapabilityInfo.SupportsChangeTracking);
Console.WriteLine(metadata.CapabilityInfo.SupportsAsyncApplyEdits);
```

### `extractChanges` capabilities

```csharp
var metadata = await client.GetMetadataAsync();

if (metadata.ExtractChangesCapabilities is not null)
{
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
- For attachments, top features, advanced related queries and `extractChanges`, check capabilities first.
- Even if you skip the manual check, the client will still fail fast for important unsupported operations.

---

## Query request method selection

Standard layer `query` requests can use GET or POST.
The client supports three modes through `FeatureServiceClientOptions.QueryRequestMethodPreference`:

- `Auto`
- `Get`
- `Post`

`Auto` uses GET for shorter requests and switches to POST when the generated query URL becomes too long.

```csharp
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
        AutoPostQueryLengthThreshold = 1800
    });
```

---

## Curve handling

The library returns NetTopologySuite geometries.

When `ReturnTrueCurves` is disabled, the server is expected to return densified linear geometries.
When `ReturnTrueCurves` is enabled, the client can currently linearize circular-arc true curves for `curvePaths` and `curveRings`.
Other true-curve segment types are not yet supported.

---

## Notes and limitations

- The library works directly against Esri Feature Service REST endpoints.
- It does not use ArcGIS SDK licensing.
- Advanced Feature Service capabilities vary between services.
- `extractChanges` is mainly for incremental sync scenarios.
- Async `extractChanges` SQLite results are downloaded as files.
- The ArcGIS REST API uses the literal `sqllite` for the SQLite data format parameter. The library hides that detail behind `ExtractChangesDataFormat.Sqlite`.
- Token acquisition for Portal for ArcGIS login is intentionally outside this package.

---

## Recommended reading order

If this is your first time using the package, read the sections in this order:

1. Create a service client
2. Authentication
3. Get a layer client
4. Get layer schema
5. Basic feature query
6. Editing features
7. Attachments
8. `extractChanges`
9. Public Esri JSON converters

That gets the common cases out of the way before the more specialized sync scenarios.
