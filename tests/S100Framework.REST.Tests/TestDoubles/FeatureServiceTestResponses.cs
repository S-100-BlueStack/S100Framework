namespace S100Framework.REST.Tests.TestDoubles;

internal static class FeatureServiceTestResponses
{
    public static string CreateExtractChangesSupportedMetadataResponse(
        bool supportsAsyncApplyEdits = false) {
        return $$"""
        {
          "layers": [],
          "tables": [],
          "capabilities": "Create,Update,Delete,Editing,Query,ChangeTracking,Sync",
          "syncEnabled": true,
          "advancedEditingCapabilities": {
            "supportsAsyncApplyEdits": {{supportsAsyncApplyEdits.ToString().ToLowerInvariant()}}
          },
          "extractChangesCapabilities": {
            "supportsReturnIdsOnly": true,
            "supportsReturnExtentOnly": true,
            "supportsReturnAttachments": true,
            "supportsLayerQueries": true,
            "supportsGeometry": true,
            "supportsReturnFeature": true,
            "supportsFieldsToCompare": true,
            "supportsServerGens": true,
            "supportsReturnHasGeometryUpdates": true
          }
        }
        """;
    }

    public static string CreateLayerSchemaResponse(
        int layerId = 0,
        string name = "Layer 0",
        bool supportsAsyncApplyEdits = false,
        bool hasAttachments = true,
        bool supportsQueryAttachments = true,
        bool supportsAttachmentsResizing = false) {
        return $$"""
        {
          "id": {{layerId}},
          "name": "{{name}}",
          "geometryType": "esriGeometryPoint",
          "objectIdField": "OBJECTID",
          "maxRecordCount": 1000,
          "advancedQueryCapabilities": {
            "supportsPagination": true
          },
          "advancedEditingCapabilities": {
            "supportsAsyncApplyEdits": {{supportsAsyncApplyEdits.ToString().ToLowerInvariant()}}
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false }
          ],
          "relationships": [],
          "hasAttachments": {{hasAttachments.ToString().ToLowerInvariant()}},
          "supportsQueryAttachments": {{supportsQueryAttachments.ToString().ToLowerInvariant()}},
          "supportsAttachmentsResizing": {{supportsAttachmentsResizing.ToString().ToLowerInvariant()}},
          "supportsTopFeaturesQuery": false,
          "extent": {
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          }
        }
        """;
    }

    public static string CreateAttachmentEditingSupportedMetadataResponse() {
        return """
    {
      "layers": [],
      "tables": [],
      "capabilities": "Query,Editing,Uploads"
    }
    """;
    }

}