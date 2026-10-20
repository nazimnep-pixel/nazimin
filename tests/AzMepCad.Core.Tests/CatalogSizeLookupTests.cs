using AzMepCad.Core.Catalog;
using AzMepCad.Core.Interfaces;
using AzMepCad.Routing;
using Xunit;

namespace AzMepCad.Core.Tests;

public class CatalogSizeLookupTests
{
    [Fact]
    public void FindNearest_MatchesExactDiameter()
    {
        var standards = new[]
        {
            new PipeSizeStandard("DIN-EN10255", "DN110", 110, 104.6, 2.7),
            new PipeSizeStandard("DIN-EN10255", "DN160", 160, 152.4, 3.8),
        };

        var match = CatalogSizeLookup.FindNearest(standards, 110);

        Assert.NotNull(match);
        Assert.Equal("DN110", match!.NominalAd);
    }

    [Fact]
    public async Task SqliteCatalog_FindsDin110_FromSchema()
    {
        var repoRoot = RepoPaths.FindRoot();
        var dbPath = Path.Combine(Path.GetTempPath(), $"azmep-test-{Guid.NewGuid():N}.db");
        var schemaPath = Path.Combine(repoRoot, "data", "catalog", "schema.sql");

        Assert.True(File.Exists(schemaPath), $"Schema not found: {schemaPath}");

        await CatalogSchemaBootstrap.ApplySchemaAsync(schemaPath, dbPath);

        await using var catalog = new SqliteMepCatalog(dbPath);
        await catalog.InitializeAsync();

        var size = await catalog.DiametrStandartiniTapAsync("DIN-EN10255", 110);
        Assert.NotNull(size);
        Assert.Equal(110, size!.XariciDiametrMm, 3);
    }
}
