namespace AzMepCad.Routing;

using AzMepCad.Core.Interfaces;

/// <summary>
/// Resolves nearest catalog pipe size for a requested diameter.
/// </summary>
public static class CatalogSizeLookup
{
    public static PipeSizeStandard? FindNearest(
        IEnumerable<PipeSizeStandard> standards,
        double requestedDiameterMm,
        double toleranceMm = 0.5)
    {
        PipeSizeStandard? best = null;
        var bestDelta = double.MaxValue;

        foreach (var s in standards)
        {
            var delta = Math.Abs(s.XariciDiametrMm - requestedDiameterMm);
            if (delta <= toleranceMm && delta < bestDelta)
            {
                best = s;
                bestDelta = delta;
            }
        }

        return best;
    }

    public static async Task<PipeSizeStandard?> FindInCatalogAsync(
        IMepCatalog catalog,
        string standartKodu,
        double requestedDiameterMm,
        CancellationToken cancellationToken = default)
    {
        return await catalog.DiametrStandartiniTapAsync(standartKodu, requestedDiameterMm, cancellationToken)
            .ConfigureAwait(false);
    }
}
