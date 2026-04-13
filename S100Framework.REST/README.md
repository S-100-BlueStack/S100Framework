using S100Framework.REST.Clients;
using S100Framework.REST.Configuration;

var httpClient = new HttpClient();

var client = new FeatureServiceClient(
    httpClient,
    new FeatureServiceClientOptions
    {
        ServiceUri = new Uri("https://example.com/arcgis/rest/services/MyService/FeatureServer"),
        DefaultPageSize = 500,
        FixInvalidGeometries = true
    });

var layer = client.GetLayerClient(0);
var schema = await layer.GetSchemaAsync();

await foreach (var feature in layer.QueryAsync(
    new()
    {
        Where = "1=1",
        OutFields = ["OBJECTID", "NAME"],
        ReturnGeometry = true
    }))
{
    Console.WriteLine($"{feature.ObjectId}: {feature.Geometry?.GeometryType}");
}