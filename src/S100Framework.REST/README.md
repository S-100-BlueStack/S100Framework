# S100Framework.REST

`S100Framework.REST` is a .NET library for reading and editing Esri Feature Services through the ArcGIS REST API and exposing geometry data through NetTopologySuite.

The library is designed to work without ArcGIS SDK or ArcGIS runtime dependencies. It communicates directly with Feature Service REST endpoints.

## Goals

- Read Feature Service metadata and layer schema
- Query features and convert Esri geometry JSON to NetTopologySuite geometries
- Support typed spatial filtering using NetTopologySuite `Envelope` and `Geometry`
- Support common query result shapes:
  - features
  - count
  - object IDs
  - extent
- Support advanced query families:
  - statistics
  - related records
  - attachments
  - top features
  - extract changes
- Support feature editing through layer-level and service-level `applyEdits`
- Support attachment lifecycle operations
- Provide public converters for Esri JSON geometry and feature payloads
- Provide convenience helpers for async `extractChanges` polling and file downloads
- Keep the public API .NET-friendly while hiding Esri-specific request details where possible

## Requirements

- .NET 10
- NetTopologySuite
- Access to an Esri Feature Service endpoint

## Installation

Add a project reference during development, or consume the package when published.

Example project reference:

```xml
<ProjectReference Include="..\..\src\S100Framework.REST\S100Framework.REST.csproj" />
```

## Core concepts

The library is built around two client abstractions:

- `IFeatureServiceClient`
- `IFeatureLayerClient`

A service client reads service-level metadata and gives access to layer clients.

A layer client performs layer-specific operations such as:

- schema retrieval
- feature queries
- counts
- object ID queries
- extent queries
- statistics queries
- related record queries
- attachment queries and downloads
- top feature queries
- layer-level apply-edits operations
- attachment add, update, and delete operations

The service client also performs service-level operations such as:

- service metadata retrieval
- multi-layer `applyEdits`
- `extractChanges`
- layer lookup by name

## Authentication

Authentication is intentionally handled through `HttpClient` and optional request authorizers instead of being hardcoded into the query model.

### Windows SSO / Integrated authentication

If your ArcGIS environment is available through Integrated Windows Authentication, configure `HttpClientHandler.UseDefaultCredentials = true` in the consuming application:

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
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer"),
        DefaultPageSize = 100,
        RequestTimeout = TimeSpan.FromSeconds(30),
        FixInvalidGeometries = true,
        PreferLatestWkid = true
    });
```

### Token-based authentication

If your ArcGIS environment requires tokens, provide an `IFeatureServiceRequestAuthorizer`.

Example with bearer token:

```csharp
using S100Framework.REST.Authorization;
using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var authorizer = new BearerTokenFeatureServiceRequestAuthorizer(
    _ => ValueTask.FromResult(""));

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

> Do not hardcode secrets in source code.
> Use a secret store, secure local development configuration, or a runtime prompt in sample applications.

## Query request method selection

Standard layer `query` requests can use GET or POST.
The client supports three modes through `FeatureServiceClientOptions.QueryRequestMethodPreference`:

- `Auto`
- `Get`
- `Post`

`Auto` uses GET for shorter requests and switches to POST when the generated query URL becomes too long.

```csharp
var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        QueryRequestMethodPreference = QueryRequestMethodPreference.Auto,
        AutoPostQueryLengthThreshold = 1800
    });
```

This transport selection currently applies to standard layer `query` operations such as:

- feature queries
- count queries
- object ID queries
- extent queries
- statistics queries

## Curve handling

The library returns NetTopologySuite geometries.

When `ReturnTrueCurves` is disabled, the server is expected to return densified linear geometries.
When `ReturnTrueCurves` is enabled, the client can currently linearize circular-arc true curves for `curvePaths` and `curveRings`.
Other true-curve segment types are not yet supported.

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
        PreferLatestWkid = true
    });
```

## Read service metadata

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine($"Layers: {metadata.Layers.Count}");

foreach (var layer in metadata.Layers) {
    Console.WriteLine($"{layer.Id}: {layer.Name}");
}
```

## Get a layer client

By layer ID:

```csharp
var layerClient = client.GetLayerClient(0);
```

By layer name:

```csharp
var layerClient = await client.GetLayerClientAsync("Harbors");
```

## Get a layer client and schema

```csharp
var layerClient = client.GetLayerClient(0);
var schema = await layerClient.GetSchemaAsync();

Console.WriteLine($"Layer name: {schema.Name}");
Console.WriteLine($"Geometry type: {schema.GeometryType}");
Console.WriteLine($"SRID: {schema.Srid}");
Console.WriteLine($"Object ID field: {schema.ObjectIdFieldName}");
```

## Inspect layer capabilities

Some query families are only available when the target layer advertises support for them.
Use `GetSchemaAsync()` to inspect layer capabilities before calling advanced query operations.

```csharp
var layerClient = client.GetLayerClient(0);
var schema = await layerClient.GetSchemaAsync();

Console.WriteLine($"Supports pagination: {schema.Capabilities.SupportsPagination}");
Console.WriteLine($"Supports query attachments: {schema.Capabilities.SupportsQueryAttachments}");
Console.WriteLine($"Has attachments: {schema.Capabilities.HasAttachments}");
Console.WriteLine($"Supports top features query: {schema.Capabilities.SupportsTopFeaturesQuery}");
Console.WriteLine($"Supports aggregated query pagination: {schema.Capabilities.SupportsPaginationOnAggregatedQueries}");
Console.WriteLine($"Supports related query pagination: {schema.Capabilities.SupportsQueryRelatedPagination}");
Console.WriteLine($"Supports advanced related queries: {schema.Capabilities.SupportsAdvancedQueryRelated}");
Console.WriteLine($"Supports order by: {schema.Capabilities.SupportsOrderBy}");
Console.WriteLine($"Supports distinct: {schema.Capabilities.SupportsDistinct}");
Console.WriteLine($"Supports async applyEdits: {schema.Capabilities.SupportsAsyncApplyEdits}");
```

## Inspect service capabilities

```csharp
var metadata = await client.GetMetadataAsync();

Console.WriteLine($"Supports query: {metadata.CapabilityInfo.SupportsQuery}");
Console.WriteLine($"Supports editing: {metadata.CapabilityInfo.SupportsEditing}");
Console.WriteLine($"Supports uploads: {metadata.CapabilityInfo.SupportsUploads}");
Console.WriteLine($"Supports change tracking: {metadata.CapabilityInfo.SupportsChangeTracking}");
Console.WriteLine($"Supports async applyEdits: {metadata.CapabilityInfo.SupportsAsyncApplyEdits}");

if (metadata.ExtractChangesCapabilities is not null) {
    Console.WriteLine($"ExtractChanges supports layerQueries: {metadata.ExtractChangesCapabilities.SupportsLayerQueries}");
    Console.WriteLine($"ExtractChanges supports geometry filters: {metadata.ExtractChangesCapabilities.SupportsGeometry}");
    Console.WriteLine($"ExtractChanges supports returnAttachments: {metadata.ExtractChangesCapabilities.SupportsReturnAttachments}");
    Console.WriteLine($"ExtractChanges supports returnIdsOnly: {metadata.ExtractChangesCapabilities.SupportsReturnIdsOnly}");
    Console.WriteLine($"ExtractChanges supports returnExtentOnly: {metadata.ExtractChangesCapabilities.SupportsReturnExtentOnly}");
    Console.WriteLine($"ExtractChanges supports fieldsToCompare: {metadata.ExtractChangesCapabilities.SupportsFieldsToCompare}");
    Console.WriteLine($"ExtractChanges supports serverGens: {metadata.ExtractChangesCapabilities.SupportsServerGens}");
    Console.WriteLine($"ExtractChanges supports returnHasGeometryUpdates: {metadata.ExtractChangesCapabilities.SupportsReturnHasGeometryUpdates}");
}
```

## Capability-dependent operations

Some operations should be capability-checked before they are called.
The client can also enforce these checks at runtime and fail fast with clear exceptions.

| Operation | Recommended capability check |
|---|---|
| `QueryAttachmentsAsync` | `schema.Capabilities.HasAttachments && schema.Capabilities.SupportsQueryAttachments` |
| `DownloadAttachmentAsync` | `schema.Capabilities.HasAttachments` |
| `AddAttachmentAsync` | `schema.Capabilities.HasAttachments && metadata.CapabilityInfo.SupportsEditing && metadata.CapabilityInfo.SupportsUploads` |
| `UpdateAttachmentAsync` | `schema.Capabilities.HasAttachments && metadata.CapabilityInfo.SupportsEditing && metadata.CapabilityInfo.SupportsUploads` |
| `DeleteAttachmentsAsync` | `schema.Capabilities.HasAttachments && metadata.CapabilityInfo.SupportsEditing` |
| `ExtractChangesAsync` / `SubmitExtractChangesAsync` | `metadata.CapabilityInfo.SupportsChangeTracking` |
| `ExtractChangesRequest.LayerQueries` | `metadata.ExtractChangesCapabilities?.SupportsLayerQueries == true` |
| `ExtractChangesRequest.SpatialFilter` | `metadata.ExtractChangesCapabilities?.SupportsGeometry == true` |
| `ExtractChangesRequest.ReturnAttachments` / `ReturnAttachmentsDataByUrl` | `metadata.ExtractChangesCapabilities?.SupportsReturnAttachments == true` |
| `ExtractChangesRequest.ReturnIdsOnly` | `metadata.ExtractChangesCapabilities?.SupportsReturnIdsOnly == true` |
| `ExtractChangesRequest.ReturnExtentOnly` / `ChangesExtentGridCell` | `metadata.ExtractChangesCapabilities?.SupportsReturnExtentOnly == true` |
| `ExtractChangesRequest.FieldsToCompare` | `metadata.ExtractChangesCapabilities?.SupportsFieldsToCompare == true` |
| `ExtractChangesRequest.ServerGens` / `LayerServerGens` | `metadata.ExtractChangesCapabilities?.SupportsServerGens == true` |
| `ExtractChangesRequest.ReturnHasGeometryUpdates` | `metadata.ExtractChangesCapabilities?.SupportsReturnHasGeometryUpdates == true` |

## Inspect layer relationships

Layer metadata also exposes relationship definitions so consumers can discover valid `RelationshipId` values programmatically.

```csharp
var schema = await layerClient.GetSchemaAsync();

foreach (var relationship in schema.Relationships) {
    Console.WriteLine(
        $"{relationship.Id} | " +
        $"{relationship.Name} | " +
        $"{relationship.RelatedTableId} | " +
        $"{relationship.Cardinality} | " +
        $"{relationship.Role}");
}
```

## Query features

### Basic query

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery {
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    Limit = 10
};

await foreach (var feature in layerClient.QueryAsync(query)) {
    Console.WriteLine($"{feature.ObjectId}: {feature.GetString("NAME")}");
}
```

### Query with spatial filter

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var spatialFilter = FeatureSpatialFilter.FromEnvelope(
    new Envelope(530000, 540000, 6150000, 6160000),
    inSrid: 25832,
    spatialRelationship: SpatialRelationship.Intersects);

var query = new FeatureQuery {
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    OutSrid = 25832,
    SpatialFilter = spatialFilter
};
```

## Count, object IDs, and extent

```csharp
var count = await layerClient.QueryCountAsync(
    new FeatureQuery {
        Where = "NAME LIKE '%Harbor%'"
    });

var objectIds = await layerClient.QueryObjectIdsAsync(
    new FeatureQuery {
        Where = "NAME LIKE '%Harbor%'"
    });

var extent = await layerClient.QueryExtentAsync(
    new FeatureQuery {
        Where = "NAME LIKE '%Harbor%'",
        OutSrid = 4326
    });
```

## Statistics queries

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(
    new FeatureStatisticsQuery {
        Where = "1=1",
        GroupByFields = ["PLANNAME"],
        HavingClause = "COUNT(OBJECTID) > 1",
        OrderBy = "PLANNAME",
        Statistics = [
            new StatisticDefinition("OBJECTID", "FEATURE_COUNT", StatisticType.Count),
            new StatisticDefinition("AOIID", "MAX_AOIID", StatisticType.Max)
        ]
    });
```

## Related records queries

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryRelatedRecordsAsync(
    new RelatedRecordsQuery {
        ObjectIds = [123, 456],
        RelationshipId = 0,
        OutFields = ["OBJECTID", "NAME", "TYPE"],
        ReturnGeometry = false,
        OrderBy = "NAME"
    });
```

## Attachments

### Query attachment metadata

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryAttachmentsAsync(
    new AttachmentQuery {
        ObjectIds = [123],
        ReturnUrl = true
    });
```

### Download attachment content

```csharp
var attachment = await layerClient.DownloadAttachmentAsync(
    objectId: 123,
    attachmentId: 1);

await File.WriteAllBytesAsync(
    attachment.FileName ?? "attachment.bin",
    attachment.Content);
```

### Add attachment

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("photo.jpg");

var result = await layerClient.AddAttachmentAsync(
    new AddAttachmentRequest {
        ObjectId = 818654,
        Content = stream,
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Keywords = "harbor,photo",
        ReturnEditMoment = true
    });
```

### Update attachment

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("photo-updated.jpg");

var result = await layerClient.UpdateAttachmentAsync(
    new UpdateAttachmentRequest {
        ObjectId = 818654,
        AttachmentId = 58,
        Content = stream,
        FileName = "photo-updated.jpg",
        ContentType = "image/jpeg",
        Keywords = "harbor,photo,updated",
        ReturnEditMoment = true
    });
```

### Delete attachments

```csharp
using S100Framework.REST.Models;

var result = await layerClient.DeleteAttachmentsAsync(
    new DeleteAttachmentsRequest {
        ObjectId = 818654,
        AttachmentIds = [58, 4],
        RollbackOnFailure = false,
        ReturnEditMoment = true
    });
```

## Top features queries

```csharp
using S100Framework.REST.Models;

var features = await layerClient.QueryTopFeaturesAsync(
    new TopFeaturesQuery {
        Where = "1=1",
        OutFields = ["OBJECTID", "PLANNAME", "SCORE", "REGION"],
        ReturnGeometry = true,
        TopFilter = new TopFeaturesFilter {
            GroupByFields = ["REGION"],
            OrderByFields = ["SCORE DESC"],
            TopCount = 3
        }
    });
```

## Apply edits

### Layer-level apply edits

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await layerClient.ApplyEditsAsync(
    new FeatureEdits {
        Adds = [
            new EditableFeature(
                geometryFactory.CreatePoint(new Coordinate(10, 20)),
                new Dictionary<string, object?> {
                    ["NAME"] = "New feature"
                })
        ],
        Updates = [
            new EditableFeature(
                geometryFactory.CreatePoint(new Coordinate(11, 21)),
                new Dictionary<string, object?> {
                    ["OBJECTID"] = 42,
                    ["NAME"] = "Updated feature"
                })
        ],
        Deletes = [99]
    });
```

### Service-level multi-layer apply edits

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

var result = await client.ApplyEditsAsync(
    new FeatureServiceEdits {
        Layers = [
            new ServiceLayerEdits {
                LayerId = 0,
                Adds = [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(10, 20)),
                        new Dictionary<string, object?> {
                            ["NAME"] = "New feature"
                        })
                ]
            },
            new ServiceLayerEdits {
                LayerId = 1,
                DeleteObjectIds = [42, 43]
            }
        ]
    });
```

## Extract changes

### Basic extract changes

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(
    new ExtractChangesRequest {
        Layers = [0],
        ServerGens = new ExtractChangesServerGens {
            SinceServerGen = 1653608093000
        },
        ReturnIdsOnly = false
    });
```

### Extract changes with layer queries and geometry filter

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var spatialFilter = ExtractChangesSpatialFilter.FromEnvelope(
    new Envelope(-104, -94.32, 35.6, 41),
    inSrid: 4326);

var result = await client.ExtractChangesAsync(
    new ExtractChangesRequest {
        Layers = [0],
        LayerServerGens = [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        LayerQueries = new Dictionary<int, ExtractChangesLayerQuery> {
            [0] = new() {
                QueryOption = ExtractChangesLayerQueryOption.UseFilter,
                Where = "requires_inspection = 'yes'",
                UseGeometry = true,
                IncludeRelated = true
            }
        },
        SpatialFilter = spatialFilter,
        ReturnIdsOnly = true,
        ReturnAttachments = true,
        ReturnAttachmentsDataByUrl = true,
        FieldsToCompare = ["type"]
    });
```

### Extract changes with extent-only response

```csharp
using S100Framework.REST.Models;

var result = await client.ExtractChangesAsync(
    new ExtractChangesRequest {
        Layers = [0],
        LayerServerGens = [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        ReturnExtentOnly = true,
        ChangesExtentGridCell = ExtractChangesExtentGridCell.Medium
    });
```

### Submit async extractChanges job

```csharp
using S100Framework.REST.Models;

var submission = await client.SubmitExtractChangesAsync(
    new ExtractChangesRequest {
        Layers = [0],
        LayerServerGens = [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        DataFormat = ExtractChangesDataFormat.Sqlite
    });

if (submission.IsPending) {
    Console.WriteLine(submission.StatusUrl);
}
```

### Poll extractChanges job status

```csharp
var status = await client.GetExtractChangesStatusAsync(submission.StatusUrl!);

Console.WriteLine(status.Status);
Console.WriteLine(status.ResultUrl);
```

### Wait for extractChanges completion with helper

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var status = await client.WaitForExtractChangesCompletionAsync(
    submission.StatusUrl!,
    new ExtractChangesPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });

Console.WriteLine(status.Status);
Console.WriteLine(status.ResultUrl);
```

### Submit, poll, and download SQLite extractChanges result

```csharp
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var file = await client.SubmitAndDownloadExtractChangesFileAsync(
    new ExtractChangesRequest {
        Layers = [0],
        LayerServerGens = [
            new ExtractChangesLayerServerGen(0, 1653608093000)
        ],
        DataFormat = ExtractChangesDataFormat.Sqlite
    },
    new ExtractChangesPollingOptions {
        PollInterval = TimeSpan.FromSeconds(2),
        Timeout = TimeSpan.FromMinutes(2)
    });

await File.WriteAllBytesAsync(
    file.FileName ?? "changes.sqlite",
    file.Content);
```

### Download SQLite extractChanges result from a result URL

```csharp
var file = await client.DownloadExtractChangesFileAsync(status.ResultUrl!);

await File.WriteAllBytesAsync(
    file.FileName ?? "changes.sqlite",
    file.Content);
```

### What `dataFormat=sqlite` does

In the .NET API, use `ExtractChangesDataFormat.Sqlite`.

On the wire, ArcGIS expects the literal value `sqllite` for this request parameter.
The library handles that mapping internally.

`dataFormat=json` returns changes as JSON payloads that can be mapped into `ExtractChangesResult`.
`dataFormat=sqllite` asks the service to return the changes as a SQLite artifact instead of embedded JSON.
In practice this is handled as an asynchronous job plus a downloadable file/result URL flow in this library.

## Esri JSON converters

The package includes public converters for Esri JSON geometry and feature payloads.

### Geometry to Esri JSON

```csharp
using S100Framework.REST.Serialization;

var esriGeometryJson = EsriJsonGeometryConverter.Serialize(feature.Geometry!);
```

### Esri JSON to NetTopologySuite geometry

```csharp
using S100Framework.REST.Serialization;

var geometry = EsriJsonGeometryConverter.Deserialize(esriGeometryJson);
```

### FeatureRecord to Esri feature JSON

```csharp
using S100Framework.REST.Serialization;

var esriFeatureJson = EsriJsonFeatureConverter.SerializeFeature(
    feature,
    objectIdFieldName: "OBJECTID");
```

### Feature set to Esri JSON

```csharp
using S100Framework.REST.Serialization;

var esriFeatureSetJson = EsriJsonFeatureConverter.SerializeFeatureSet(features, schema);
```

## Notes on querying

- `Where` uses ArcGIS SQL-style where clauses.
- Some operations are capability-dependent and should be checked against metadata before use.
- Relationship-based queries should use `schema.Relationships` to discover valid relationship IDs when possible.
- Date values in Esri feature attributes should be handled as epoch milliseconds in UTC when writing raw Esri JSON payloads.

## Current scope

The library currently supports:

- read/query scenarios for Feature Services
- layer-level feature adds, updates, and deletes through `applyEdits`
- service-level multi-layer feature adds, updates, and deletes through `applyEdits`
- layer-level attachment add, update, and delete operations
- runtime capability-gating for attachment and `extractChanges` workflows
- `extractChanges` JSON responses
- `extractChanges` extent-only responses
- `extractChanges` async status/result flows
- `extractChanges` SQLite file downloads
- convenience polling helpers for async `extractChanges` jobs
- public Esri JSON converters for geometry and feature payloads

The library does not currently include:

- service-level attachment edit payloads inside multi-layer `applyEdits`
- direct parsing of SQLite `extractChanges` content
- a high-level JSON result parser for async `extractChanges` result URLs
- full true-curve support for all Esri segment types

## Design notes

- Esri request details are hidden behind typed .NET models where practical.
- Spatial filters are built from NetTopologySuite `Envelope` and `Geometry`.
- Public Esri JSON converters are included so consuming solutions do not need to reimplement geometry or feature serialization.
- Convenience helpers live in extensions so the main client abstractions stay focused on the core REST surface.

## ArcGIS REST alignment

The library maps several ArcGIS Feature Service operations to typed .NET APIs, including:

- standard feature queries
- count, object ID, and extent queries
- statistics queries
- related records queries
- attachment queries and downloads
- top features queries
- layer-level apply-edits
- service-level multi-layer apply-edits
- layer-level attachment add, update, and delete operations
- extractChanges
- public Esri JSON conversion helpers

## Testing

The project includes unit tests for:

- service metadata
- layer schema
- feature queries
- spatial filters
- counts, object IDs, and extents
- statistics
- related records
- attachments
- top features
- layer-level apply-edits
- service-level multi-layer apply-edits
- layer-level attachment add, update, and delete operations
- extractChanges
- Esri JSON converters

Run tests with:

```bash
dotnet test
```

## Limitations

- Curve geometries are not supported in full in the current version.
- Authentication setup depends on the consuming application environment.
- SQLite `extractChanges` results are currently downloaded as files and are not parsed into higher-level models by the library.

## Roadmap ideas

- service-level attachment edit payloads
- JSON parsing helpers for async `extractChanges` result URLs
- SQLite `extractChanges` parsing helpers
- additional query capabilities and capability inspection helpers
- stronger date/time field handling
- optional convenience APIs for common field mapping patterns
