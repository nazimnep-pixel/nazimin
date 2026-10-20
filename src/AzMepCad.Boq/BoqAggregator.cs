namespace AzMepCad.Boq;

using System.Globalization;
using AzMepCad.Core.Interfaces;

/// <summary>
/// Aggregates scanned <see cref="IMepComponent"/> readings into BOQ line items (az-AZ).
/// </summary>
public static class BoqAggregator
{
    public static IReadOnlyList<BoqLineItem> Aggregate(IReadOnlyList<IMepComponent> components)
    {
        var pipes = new Dictionary<string, (string Kod, string Material, double Mm)>();
        var fittings = new Dictionary<string, (string Kod, string Ad, int Say)>();

        foreach (var c in components)
        {
            var nov = c.ElaveAtributlar.GetValueOrDefault("KomponentNovu")
                ?? c.Teyinat
                ?? "Boru";

            if (IsPipeLike(nov))
            {
                var key = $"{c.Kod ?? c.Ad}|{c.Material ?? "-"}|{c.Diametr ?? 0:F0}";
                var uzunluq = c.Uzunluq ?? 0;
                if (pipes.TryGetValue(key, out var existing))
                {
                    pipes[key] = (existing.Kod, existing.Material, existing.Mm + uzunluq);
                }
                else
                {
                    pipes[key] = (c.Kod ?? c.Ad, c.Material ?? "-", uzunluq);
                }
            }
            else
            {
                var key = $"{c.Kod ?? c.Ad}|{nov}";
                if (fittings.TryGetValue(key, out var existing))
                {
                    fittings[key] = (existing.Kod, existing.Ad, existing.Say + 1);
                }
                else
                {
                    fittings[key] = (c.Kod ?? c.Ad, c.Ad, 1);
                }
            }
        }

        var lines = new List<BoqLineItem>();
        var sira = 1;

        foreach (var (_, row) in pipes.OrderBy(p => p.Key))
        {
            lines.Add(new BoqLineItem(
                sira++,
                row.Kod,
                "Boru seqmenti",
                row.Material,
                "m",
                row.Mm / 1000.0,
                "Mərkəzxətt uzunluğu"));
        }

        foreach (var (_, row) in fittings.OrderBy(p => p.Key))
        {
            lines.Add(new BoqLineItem(
                sira++,
                row.Kod,
                row.Ad,
                "-",
                "ədəd",
                row.Say,
                "Armatur"));
        }

        return lines;
    }

    public static BoqExportRequest ToExportRequest(string layiheAdi, IReadOnlyList<IMepComponent> components) =>
        new(layiheAdi, Aggregate(components));

    private static bool IsPipeLike(string nov) =>
        nov.Contains("boru", StringComparison.OrdinalIgnoreCase)
        || nov.Equals("Boru", StringComparison.OrdinalIgnoreCase)
        || nov.Equals("DuzBorus", StringComparison.OrdinalIgnoreCase);
}
