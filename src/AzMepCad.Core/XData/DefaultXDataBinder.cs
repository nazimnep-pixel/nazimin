namespace AzMepCad.Core.XData;

using AzMepCad.Core.Interfaces;
using AzMepCad.Core.Models;

public sealed class DefaultXDataBinder : IXDataBinder
{
    public const string DefaultAppName = "AZMEP";

    public string ApplicationName { get; init; } = DefaultAppName;

    public IReadOnlyList<XDataPair> Serialize(IMepComponent component)
    {
        var pairs = new List<XDataPair>
        {
            new("Id", component.Id),
            new("Ad", component.Ad),
            new("Sistem", component.Sistem.ToString()),
        };

        if (component.Kod is not null) pairs.Add(new("Kod", component.Kod));
        if (component.Diametr is not null) pairs.Add(new("Diametr", component.Diametr.Value.ToString("F3")));
        if (component.Uzunluq is not null) pairs.Add(new("Uzunluq", component.Uzunluq.Value.ToString("F3")));
        if (component.Material is not null) pairs.Add(new("Material", component.Material));
        if (component.Istehsalci is not null) pairs.Add(new("Istehsalci", component.Istehsalci));
        if (component.Teyinat is not null) pairs.Add(new("Teyinat", component.Teyinat));

        foreach (var kv in component.ElaveAtributlar)
        {
            pairs.Add(new($"X_{kv.Key}", kv.Value));
        }

        return pairs;
    }

    public IMepComponent? Deserialize(IReadOnlyList<XDataPair> pairs, MepDiscipline defaultSistem)
    {
        if (pairs.Count == 0)
        {
            return null;
        }

        var map = pairs.ToDictionary(p => p.Acar, p => p.Deyer, StringComparer.OrdinalIgnoreCase);
        if (!map.TryGetValue("Id", out var id) || !map.TryGetValue("Ad", out var ad))
        {
            return null;
        }

        Enum.TryParse(map.GetValueOrDefault("Sistem"), out MepDiscipline sistem);
        if (sistem == MepDiscipline.Naməlum)
        {
            sistem = defaultSistem;
        }

        return new MepComponentRecord
        {
            Id = id,
            Ad = ad,
            Kod = map.GetValueOrDefault("Kod"),
            Sistem = sistem,
            Diametr = ParseDouble(map.GetValueOrDefault("Diametr")),
            Uzunluq = ParseDouble(map.GetValueOrDefault("Uzunluq")),
            Material = map.GetValueOrDefault("Material"),
            Istehsalci = map.GetValueOrDefault("Istehsalci"),
            Teyinat = map.GetValueOrDefault("Teyinat"),
            ElaveAtributlar = map
                .Where(kv => kv.Key.StartsWith("X_", StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key[2..], kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, out var d) ? d : null;
}
