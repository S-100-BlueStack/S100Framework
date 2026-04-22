using System.Net.Http;

namespace S100Framework.REST.Tests.TestDoubles;

internal static class FeatureServiceTestHandlers
{
    private const string ServiceMetadataUri =
        "https://example.test/arcgis/rest/services/Test/FeatureServer?f=json";

    public static HttpMessageHandler WithExtractChangesMetadata(
        Func<HttpRequestMessage, HttpResponseMessage> innerHandler) {
        ArgumentNullException.ThrowIfNull(innerHandler);

        return new StubHttpMessageHandler(request => {
            var requestUri = request.RequestUri!.AbsoluteUri;

            if (requestUri == ServiceMetadataUri) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateExtractChangesSupportedMetadataResponse());
            }

            return innerHandler(request);
        });
    }

    public static HttpMessageHandler WithAttachmentCapabilities(
        int layerId,
        Func<HttpRequestMessage, HttpResponseMessage> innerHandler) {
        ArgumentNullException.ThrowIfNull(innerHandler);

        var layerSchemaUri =
            $"https://example.test/arcgis/rest/services/Test/FeatureServer/{layerId}?f=json";

        return new StubHttpMessageHandler(request => {
            var requestUri = request.RequestUri!.AbsoluteUri;

            if (requestUri == ServiceMetadataUri) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateAttachmentEditingSupportedMetadataResponse());
            }

            if (requestUri == layerSchemaUri) {
                return StubHttpMessageHandler.Json(
                    FeatureServiceTestResponses.CreateLayerSchemaResponse(
                        layerId: layerId,
                        name: $"Layer {layerId}",
                        supportsAsyncApplyEdits: false,
                        hasAttachments: true,
                        supportsQueryAttachments: true,
                        supportsAttachmentsResizing: false));
            }

            return innerHandler(request);
        });
    }
}