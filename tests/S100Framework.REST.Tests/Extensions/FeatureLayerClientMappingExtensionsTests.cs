using S100Framework.REST.Abstractions;
using S100Framework.REST.Extensions;
using S100Framework.REST.Models;
using System.Runtime.CompilerServices;
using Xunit;

namespace S100Framework.REST.Tests.Extensions;

public sealed class FeatureLayerClientMappingExtensionsTests
{
    [Fact]
    public async Task QueryAsync_MapsFeatureRecordsToTypedProjection() {
        var layerClient = new FakeFeatureLayerClient(
            [
                new FeatureRecord(
                    Geometry: null,
                    Attributes: new Dictionary<string, object?>
                    {
                        ["OBJECTID"] = 1L,
                        ["NAME"] = "Harbor A"
                    },
                    ObjectId: 1),
                new FeatureRecord(
                    Geometry: null,
                    Attributes: new Dictionary<string, object?>
                    {
                        ["OBJECTID"] = 2L,
                        ["NAME"] = "Harbor B"
                    },
                    ObjectId: 2)
            ]);

        var results = new List<HarborProjection>();

        await foreach (var item in layerClient.QueryAsync(
            new FeatureQuery(),
            feature => new HarborProjection(
                feature.GetRequiredInt64("OBJECTID"),
                feature.GetRequiredString("NAME")))) {
            results.Add(item);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(1L, results[0].ObjectId);
        Assert.Equal("Harbor A", results[0].Name);
        Assert.Equal(2L, results[1].ObjectId);
        Assert.Equal("Harbor B", results[1].Name);
    }

    private sealed record HarborProjection(long ObjectId, string Name);

    private sealed class FakeFeatureLayerClient : IFeatureLayerClient
    {
        private readonly IReadOnlyList<FeatureRecord> _records;

        public FakeFeatureLayerClient(IReadOnlyList<FeatureRecord> records) {
            _records = records;
        }

        public Task<FeatureLayerSchema> GetSchemaAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult(
                new FeatureLayerSchema(
                    LayerId: 0,
                    Name: "Test Layer",
                    GeometryType: null,
                    Srid: null,
                    HasZ: false,
                    HasM: false,
                    SupportsPagination: true,
                    MaxRecordCount: 1000,
                    ObjectIdFieldName: "OBJECTID",
                    Fields: []));
        }

        public async IAsyncEnumerable<FeatureRecord> QueryAsync(
            FeatureQuery query,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in _records) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
            }

            await Task.CompletedTask;
        }
    }
}