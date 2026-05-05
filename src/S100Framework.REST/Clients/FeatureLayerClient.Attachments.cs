using System.Text.Json;
using S100Framework.REST.Exceptions;
using S100Framework.REST.Internal.Dto;
using S100Framework.REST.Models;

namespace S100Framework.REST.Clients;

/// <summary>
/// Provides attachment operations for <see cref="FeatureLayerClient" />.
/// </summary>
public sealed partial class FeatureLayerClient
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentGroup>> QueryAttachmentsAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureAttachmentQuerySupported(schema, query, requireCountOnly: false);

        var response = await _serviceClient.QueryAttachmentsAsync(
            _layerId,
            query,
            returnCountOnly: false,
            cancellationToken);

        return EnumerateAttachmentGroups(response.AttachmentGroups)
            .Select(group => new AttachmentGroup(
                group.ParentObjectId,
                group.ParentGlobalId,
                (group.AttachmentInfos ?? Enumerable.Empty<JsonElement?>())
                    .Where(static info => info.HasValue && info.Value.ValueKind == JsonValueKind.Object)
                    .Select(info => MapAttachmentInfo(
                        info!.Value,
                        group.ParentObjectId,
                        group.ParentGlobalId))
                    .ToArray()))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttachmentCountGroup>> QueryAttachmentCountsAsync(
        AttachmentQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        var schema = await GetSchemaAsync(cancellationToken);
        EnsureAttachmentQuerySupported(schema, query, requireCountOnly: true);

        var response = await _serviceClient.QueryAttachmentsAsync(
            _layerId,
            query,
            returnCountOnly: true,
            cancellationToken);

        return EnumerateAttachmentGroups(response.AttachmentGroups)
            .Select(static group => new AttachmentCountGroup(
                group.ParentObjectId,
                group.ParentGlobalId,
                group.Count ?? 0))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<AttachmentContent> DownloadAttachmentAsync(
        long objectId,
        long attachmentId,
        CancellationToken cancellationToken = default) {
        var schema = await GetSchemaAsync(cancellationToken);
        EnsureAttachmentReadSupported(schema);

        return await _serviceClient.DownloadAttachmentAsync(
            _layerId,
            objectId,
            attachmentId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeleteAttachmentsResult> DeleteAttachmentsAsync(
        DeleteAttachmentsRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await EnsureAttachmentEditingSupportedAsync(
            requireUploadSupport: false,
            cancellationToken);

        return await _serviceClient.DeleteAttachmentsAsync(
            _layerId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AddAttachmentResult> AddAttachmentAsync(
        AddAttachmentRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await EnsureAttachmentEditingSupportedAsync(
            requireUploadSupport: true,
            cancellationToken);

        return await _serviceClient.AddAttachmentAsync(
            _layerId,
            request,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpdateAttachmentResult> UpdateAttachmentAsync(
        UpdateAttachmentRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await EnsureAttachmentEditingSupportedAsync(
            requireUploadSupport: true,
            cancellationToken);

        return await _serviceClient.UpdateAttachmentAsync(
            _layerId,
            request,
            cancellationToken);
    }

    private static IEnumerable<EsriAttachmentGroupDto> EnumerateAttachmentGroups(
    IEnumerable<EsriAttachmentGroupDto?>? groups) {
        return groups?
            .Where(static group => group is not null)
            .Select(static group => group!) ?? Enumerable.Empty<EsriAttachmentGroupDto>();
    }

    private static void EnsureAttachmentReadSupported(FeatureLayerSchema schema) {
        if (!schema.Capabilities.HasAttachments) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support attachments.");
        }
    }

    private static void EnsureAttachmentQuerySupported(
        FeatureLayerSchema schema,
        AttachmentQuery query,
        bool requireCountOnly) {
        EnsureAttachmentReadSupported(schema);

        if (!schema.Capabilities.SupportsQueryAttachments) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support attachment queries.");
        }

        if (requireCountOnly && !schema.Capabilities.SupportsQueryAttachmentsCountOnly) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support attachment count queries.");
        }

        if (query.OrderByFields is { Count: > 0 } &&
            !schema.Capabilities.SupportsQueryAttachmentOrderByFields) {
            throw new FeatureServiceCapabilityException(
                $"Layer '{schema.Name}' ({schema.LayerId}) does not support attachment order-by fields.");
        }
    }

    private async Task EnsureAttachmentEditingSupportedAsync(
        bool requireUploadSupport,
        CancellationToken cancellationToken) {
        var schema = await GetSchemaAsync(cancellationToken);
        EnsureAttachmentReadSupported(schema);

        var metadata = await _serviceClient.GetMetadataAsync(cancellationToken);

        if (!metadata.Capabilities.SupportsEditing) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support editing, so attachment edits are not available.");
        }

        if (requireUploadSupport && !metadata.Capabilities.SupportsUploads) {
            throw new FeatureServiceCapabilityException(
                "The feature service does not support uploads, so attachment add/update operations are not available.");
        }
    }
}