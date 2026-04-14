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
- Support feature editing through layer-level and service-level `applyEdits`
- Support attachment lifecycle operations
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

## Authentication

Authentication is intentionally handled through `HttpClient` and optional request authorizers instead of being hardcoded into the query model.

### Windows SSO / Integrated authentication

If your ArcGIS environment is available through Integrated Windows Authentication, configure `HttpClientHandler.UseDefaultCredentials = true` in the consuming application:

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
    _ => ValueTask.FromResult("<token>"));

using var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://your-server/arcgis/rest/services/YourService/FeatureServer")
    },
    authorizer);
```

> Do not hardcode secrets in source code. Use a secret store, secure local development configuration, or a runtime prompt in sample applications.

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
    new FeatureServiceClientOptions
    {
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
    new FeatureServiceClientOptions
    {
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

foreach (var layer in metadata.Layers)
{
    Console.WriteLine($"{layer.Id}: {layer.Name}");
}
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
```

This is especially useful before calling:

- `QueryAttachmentsAsync`
- `DownloadAttachmentAsync`
- `QueryTopFeaturesAsync`
- `QueryTopFeatureObjectIdsAsync`
- `QueryTopFeatureCountAsync`
- `QueryRelatedRecordsAsync`
- `QueryStatisticsAsync`

## Inspect layer relationships

Layer metadata also exposes relationship definitions so consumers can discover valid `RelationshipId` values programmatically.

```csharp
var schema = await layerClient.GetSchemaAsync();

foreach (var relationship in schema.Relationships)
{
    Console.WriteLine(
        $"{relationship.Id} | " +
        $"{relationship.Name} | " +
        $"{relationship.RelatedTableId} | " +
        $"{relationship.Cardinality} | " +
        $"{relationship.Role}");
}
```

Example output:

```text
0 | inspections | 3 | esriRelCardinalityOneToMany | esriRelRoleOrigin
```

This makes it easier to call related record queries without manually inspecting the ArcGIS REST browser first.

## Example: guard advanced queries with schema capabilities

```csharp
var schema = await layerClient.GetSchemaAsync();

if (schema.Capabilities.HasAttachments && schema.Capabilities.SupportsQueryAttachments)
{
    var attachments = await layerClient.QueryAttachmentsAsync(
        new AttachmentQuery
        {
            ObjectIds = [123]
        });

    Console.WriteLine($"Attachment groups: {attachments.Count}");
}

if (schema.Capabilities.SupportsTopFeaturesQuery)
{
    var topFeatures = await layerClient.QueryTopFeaturesAsync(
        new TopFeaturesQuery
        {
            OutFields = ["OBJECTID", "NAME", "SCORE"],
            TopFilter = new TopFeaturesFilter
            {
                GroupByFields = ["REGION"],
                OrderByFields = ["SCORE DESC"],
                TopCount = 3
            }
        });

    Console.WriteLine($"Top features returned: {topFeatures.Count}");
}

if (schema.Relationships.Count > 0)
{
    var relationshipId = schema.Relationships[0].Id;

    var related = await layerClient.QueryRelatedRecordsAsync(
        new RelatedRecordsQuery
        {
            ObjectIds = [123],
            RelationshipId = relationshipId,
            OutFields = ["OBJECTID", "NAME"],
            ReturnGeometry = false
        });

    Console.WriteLine($"Related record groups returned: {related.Count}");
}
```

## Query features

### Basic query

```csharp
using S100Framework.REST.Models;

var query = new FeatureQuery
{
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    Limit = 10
};

await foreach (var feature in layerClient.QueryAsync(query))
{
    Console.WriteLine($"{feature.ObjectId}: {feature.GetString("NAME")}");
}
```

### Query with spatial filter

Use typed NetTopologySuite spatial filters instead of raw Esri JSON.

#### Envelope filter

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var spatialFilter = FeatureSpatialFilter.FromEnvelope(
    new Envelope(530000, 540000, 6150000, 6160000),
    inSrid: 25832,
    spatialRelationship: SpatialRelationship.Intersects);

var query = new FeatureQuery
{
    Where = "1=1",
    OutFields = ["OBJECTID", "NAME"],
    ReturnGeometry = true,
    OutSrid = 25832,
    SpatialFilter = spatialFilter
};
```

#### Geometry filter

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Models;

var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 25832);

var polygon = geometryFactory.CreatePolygon(
[
    new Coordinate(530000, 6150000),
    new Coordinate(540000, 6150000),
    new Coordinate(540000, 6160000),
    new Coordinate(530000, 6160000),
    new Coordinate(530000, 6150000)
]);

var query = new FeatureQuery
{
    SpatialFilter = FeatureSpatialFilter.FromGeometry(
        polygon,
        spatialRelationship: SpatialRelationship.Intersects)
};
```

## Access attributes

Returned attributes are available in `FeatureRecord.Attributes`.

Use typed helper methods for safer access:

```csharp
await foreach (var feature in layerClient.QueryAsync(query))
{
    var objectId = feature.GetRequiredInt64("OBJECTID");
    var name = feature.GetString("NAME");
    var isActive = feature.GetBoolean("IS_ACTIVE");
    var depth = feature.GetDecimal("DEPTH");

    Console.WriteLine($"{objectId} | {name} | {depth}");
}
```

## Map to your own model

Use the mapping extension to project `FeatureRecord` into your own domain model:

```csharp
using NetTopologySuite.Geometries;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;

var rows = new List<HarborFeature>();

await foreach (var harbor in layerClient.QueryAsync(
    new FeatureQuery
    {
        Where = "1=1",
        OutFields = ["OBJECTID", "NAME", "DEPTH"],
        ReturnGeometry = true
    },
    feature => new HarborFeature
    {
        ObjectId = feature.GetRequiredInt64("OBJECTID"),
        Name = feature.GetRequiredString("NAME"),
        Depth = feature.GetDecimal("DEPTH"),
        Geometry = feature.Geometry
    }))
{
    rows.Add(harbor);
}

public sealed class HarborFeature
{
    public long ObjectId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal? Depth { get; init; }

    public Geometry? Geometry { get; init; }
}
```

## Count, object IDs, and extent

These are exposed as separate methods because the ArcGIS REST API returns different response shapes for each of them.

### Count

```csharp
var count = await layerClient.QueryCountAsync(
    new FeatureQuery
    {
        Where = "NAME LIKE '%Harbor%'"
    });
```

### Object IDs

```csharp
var objectIds = await layerClient.QueryObjectIdsAsync(
    new FeatureQuery
    {
        Where = "NAME LIKE '%Harbor%'"
    });
```

### Extent

```csharp
var extent = await layerClient.QueryExtentAsync(
    new FeatureQuery
    {
        Where = "NAME LIKE '%Harbor%'",
        OutSrid = 4326
    });

if (extent is not null)
{
    Console.WriteLine(
        $"{extent.Envelope.MinX}, {extent.Envelope.MinY}, {extent.Envelope.MaxX}, {extent.Envelope.MaxY}");
}
```

## Statistics queries

Use `FeatureStatisticsQuery` for grouped or aggregate queries.

```csharp
using S100Framework.REST.Models;

var rows = await layerClient.QueryStatisticsAsync(
    new FeatureStatisticsQuery
    {
        Where = "1=1",
        GroupByFields = ["PLANNAME"],
        HavingClause = "COUNT(OBJECTID) > 1",
        OrderBy = "PLANNAME",
        Statistics =
        [
            new StatisticDefinition("OBJECTID", "FEATURE_COUNT", StatisticType.Count),
            new StatisticDefinition("AOIID", "MAX_AOIID", StatisticType.Max)
        ]
    });

foreach (var row in rows)
{
    Console.WriteLine(
        $"{row.GetString("PLANNAME")} | " +
        $"{row.GetInt64("FEATURE_COUNT")} | " +
        $"{row.GetInt64("MAX_AOIID")}");
}
```

## Related records queries

Use `RelatedRecordsQuery` to retrieve records from related layers or tables.

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryRelatedRecordsAsync(
    new RelatedRecordsQuery
    {
        ObjectIds = [123, 456],
        RelationshipId = 0,
        OutFields = ["OBJECTID", "NAME", "TYPE"],
        ReturnGeometry = false,
        OrderBy = "NAME"
    });

foreach (var group in groups)
{
    Console.WriteLine($"Source object ID: {group.SourceObjectId}");

    foreach (var record in group.Records)
    {
        Console.WriteLine(
            $"{record.ObjectId} | " +
            $"{record.GetString("NAME")} | " +
            $"{record.GetString("TYPE")}");
    }
}
```

## Attachments

### Query attachment metadata

```csharp
using S100Framework.REST.Models;

var groups = await layerClient.QueryAttachmentsAsync(
    new AttachmentQuery
    {
        ObjectIds = [123],
        ReturnUrl = true
    });

foreach (var group in groups)
{
    Console.WriteLine($"Source object ID: {group.SourceObjectId}");

    foreach (var attachment in group.Attachments)
    {
        Console.WriteLine(
            $"{attachment.AttachmentId} | " +
            $"{attachment.Name} | " +
            $"{attachment.ContentType} | " +
            $"{attachment.Size}");
    }
}
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
    new AddAttachmentRequest
    {
        ObjectId = 818654,
        Content = stream,
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
        Keywords = "harbor,photo",
        ReturnEditMoment = true
    });

Console.WriteLine(result.Result.AttachmentId);
Console.WriteLine(result.Result.Success);
Console.WriteLine(result.EditMoment);
```

### Update attachment

```csharp
using S100Framework.REST.Models;

await using var stream = File.OpenRead("photo-updated.jpg");

var result = await layerClient.UpdateAttachmentAsync(
    new UpdateAttachmentRequest
    {
        ObjectId = 818654,
        AttachmentId = 58,
        Content = stream,
        FileName = "photo-updated.jpg",
        ContentType = "image/jpeg",
        Keywords = "harbor,photo,updated",
        ReturnEditMoment = true
    });

Console.WriteLine(result.Result.AttachmentId);
Console.WriteLine(result.Result.Success);
Console.WriteLine(result.EditMoment);
```

### Delete attachments

```csharp
using S100Framework.REST.Models;

var result = await layerClient.DeleteAttachmentsAsync(
    new DeleteAttachmentsRequest
    {
        ObjectId = 818654,
        AttachmentIds = [58, 4],
        RollbackOnFailure = false,
        ReturnEditMoment = true
    });

foreach (var edit in result.Results)
{
    Console.WriteLine(
        $"{edit.AttachmentId} | {edit.Success} | {edit.ErrorCode} | {edit.ErrorDescription}");
}
```

## Top features queries

Use `queryTopFeatures` when you need the top N records within groups, based on one or more order-by fields.

```csharp
using S100Framework.REST.Models;

var features = await layerClient.QueryTopFeaturesAsync(
    new TopFeaturesQuery
    {
        Where = "1=1",
        OutFields = ["OBJECTID", "PLANNAME", "SCORE", "REGION"],
        ReturnGeometry = true,
        TopFilter = new TopFeaturesFilter
        {
            GroupByFields = ["REGION"],
            OrderByFields = ["SCORE DESC"],
            TopCount = 3
        }
    });

foreach (var feature in features)
{
    Console.WriteLine(
        $"{feature.ObjectId} | " +
        $"{feature.GetString("REGION")} | " +
        $"{feature.GetString("PLANNAME")} | " +
        $"{feature.GetDouble("SCORE")}");
}
```

Object IDs only:

```csharp
var ids = await layerClient.QueryTopFeatureObjectIdsAsync(
    new TopFeaturesQuery
    {
        TopFilter = new TopFeaturesFilter
        {
            GroupByFields = ["REGION"],
            OrderByFields = ["SCORE DESC"],
            TopCount = 1
        }
    });
```

Count and extent:

```csharp
var countResult = await layerClient.QueryTopFeatureCountAsync(
    new TopFeaturesQuery
    {
        OutSrid = 4326,
        TopFilter = new TopFeaturesFilter
        {
            GroupByFields = ["REGION"],
            OrderByFields = ["SCORE DESC"],
            TopCount = 5
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
    new FeatureEdits
    {
        Adds =
        [
            new EditableFeature(
                geometryFactory.CreatePoint(new Coordinate(10, 20)),
                new Dictionary<string, object?>
                {
                    ["NAME"] = "New feature"
                })
        ],
        Updates =
        [
            new EditableFeature(
                geometryFactory.CreatePoint(new Coordinate(11, 21)),
                new Dictionary<string, object?>
                {
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
    new FeatureServiceEdits
    {
        Layers =
        [
            new ServiceLayerEdits
            {
                LayerId = 0,
                Adds =
                [
                    new EditableFeature(
                        geometryFactory.CreatePoint(new Coordinate(10, 20)),
                        new Dictionary<string, object?>
                        {
                            ["NAME"] = "New feature"
                        })
                ]
            },
            new ServiceLayerEdits
            {
                LayerId = 1,
                DeleteObjectIds = [42, 43]
            }
        ]
    });
```

## Notes on querying

- `Where` uses ArcGIS SQL-style where clauses.
- For text search, use expressions like:
  - `NAME = 'Harbor A'`
  - `NAME LIKE '%Harbor%'`
- `Envelope` in NetTopologySuite is constructed as:
  - `new Envelope(minX, maxX, minY, maxY)`
- Layers return geometry in feature queries.
- Tables do not return geometry in normal feature queries.
- Related tables may return only attributes, depending on the relationship target.
- Some operations are capability-dependent and should be checked against `schema.Capabilities` before use.
- Relationship-based queries should use `schema.Relationships` to discover valid relationship IDs when possible.

## Current scope

The library currently supports:

- read/query scenarios for Feature Services
- layer-level feature adds, updates, and deletes through `applyEdits`
- service-level multi-layer feature adds, updates, and deletes through `applyEdits`
- layer-level attachment add, update, and delete operations

The library does not currently include:

- service-level attachment edit payloads inside multi-layer `applyEdits`
- asynchronous edit workflows
- advanced upload-ID based attachment flows

## Design notes

- Esri request details are hidden behind typed .NET models where practical.
- Spatial filters are built from NetTopologySuite `Envelope` and `Geometry`.
- Attribute values are preserved dynamically and can be accessed through typed helpers.
- Query families with different response shapes are modeled as separate methods instead of flags on one large request object.
- Initial editing support is intentionally limited to explicit layer-level and service-level surfaces.

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

Where ArcGIS uses separate REST operations with different response shapes, this library also exposes them as separate methods instead of overloading one large request model.

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

Run tests with:

```bash
dotnet test
```

## Limitations

- Curve geometries are not supported in full in the current version.
- Authentication setup depends on the consuming application environment.
- Some ArcGIS query capabilities are server-version or datasource dependent.
- Support for specific query features still depends on what the target layer advertises through its capabilities.
- Editing support is currently limited to the initial layer-level and service-level surfaces described above.

## Roadmap ideas

- service-level attachment edit payloads
- attachment upload-ID flows
- additional query capabilities and capability inspection helpers
- stronger date/time field handling
- optional convenience APIs for common field mapping patterns
- package publishing and versioned consumer guidance
