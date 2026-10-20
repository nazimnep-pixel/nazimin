using AzMepCad.Core.Catalog;
using AzMepCad.Core.Interfaces;
using Xunit;

namespace AzMepCad.Core.Tests;

public class CatalogSeedTests
{
    [Fact]
    public async Task JsonSeedImporter_LoadsPipeSizesAndFittings()
    {
        var repoRoot = RepoPaths.FindRoot();
        var dbPath = Path.Combine(Path.GetTempPath(), $"azmep-seed-{Guid.NewGuid():N}.db");
        var schemaPath = Path.Combine(repoRoot, "data", "catalog", "schema.sql");
        var seedDir = Path.Combine(repoRoot, "data", "catalog", "seed");

        await SqliteMepCatalog.CreateFromSeedAsync(schemaPath, seedDir, dbPath);

        await using var catalog = new SqliteMepCatalog(dbPath);
        await catalog.InitializeAsync();

        var sizes = await catalog.GetPipeSizesAsync("DIN-EN10255");
        Assert.True(sizes.Count >= 3);

        var elbow = await catalog.GetFittingAsync(FittingNovu.Bucaq90, 110);
        Assert.NotNull(elbow);
        Assert.Equal("EL90-110", elbow!.Kod);
    }
}
