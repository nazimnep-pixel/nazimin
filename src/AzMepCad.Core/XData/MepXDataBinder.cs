namespace AzMepCad.Core.XData;

using System.Text;
using System.Text.Json;
using AzMepCad.Core.Interfaces;
using AzMepCad.Core.Models;

/// <summary>
/// Serializes MEP components to XData pairs and byte[] / dictionary payloads.
/// </summary>
public sealed class MepXDataBinder : IXDataBinder
{
    public string ApplicationName { get; init; } = MepXDataSchema.RegAppName;

    public IReadOnlyList<XDataPair> Serialize(IMepComponent component)
    {
        var pairs = new List<XDataPair>
        {
            new(MepXDataSchema.KeyId, component.Id),
            new(MepXDataSchema.KeyAd, component.Ad),
            new(MepXDataSchema.KeySistem, component.Sistem.ToString()),
        };

        if (component.Kod is not null) pairs.Add(new(MepXDataSchema.KeyKod, component.Kod));
        if (component.Diametr is not null)
        {
            pairs.Add(new(MepXDataSchema.KeyDiametr, component.Diametr.Value.ToString("F3")));
        }

        if (component.Uzunluq is not null)
        {
            pairs.Add(new(MepXDataSchema.KeyUzunluq, component.Uzunluq.Value.ToString("F3")));
        }

        if (component.Material is not null) pairs.Add(new(MepXDataSchema.KeyMaterial, component.Material));
        if (component.Istehsalci is not null) pairs.Add(new(MepXDataSchema.KeyIstehsalci, component.Istehsalci));
        if (component.Teyinat is not null) pairs.Add(new(MepXDataSchema.KeyTeyinat, component.Teyinat));

        foreach (var kv in component.ElaveAtributlar)
        {
            pairs.Add(new($"{MepXDataSchema.ExtraKeyPrefix}{kv.Key}", kv.Value));
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
        if (!map.TryGetValue(MepXDataSchema.KeyId, out var id)
            || !map.TryGetValue(MepXDataSchema.KeyAd, out var ad))
        {
            return null;
        }

        Enum.TryParse(map.GetValueOrDefault(MepXDataSchema.KeySistem), out MepDiscipline sistem);
        if (sistem == MepDiscipline.Naməlum)
        {
            sistem = defaultSistem;
        }

        return new MepComponentRecord
        {
            Id = id,
            Ad = ad,
            Kod = map.GetValueOrDefault(MepXDataSchema.KeyKod),
            Sistem = sistem,
            Diametr = ParseDouble(map.GetValueOrDefault(MepXDataSchema.KeyDiametr)),
            Uzunluq = ParseDouble(map.GetValueOrDefault(MepXDataSchema.KeyUzunluq)),
            Material = map.GetValueOrDefault(MepXDataSchema.KeyMaterial),
            Istehsalci = map.GetValueOrDefault(MepXDataSchema.KeyIstehsalci),
            Teyinat = map.GetValueOrDefault(MepXDataSchema.KeyTeyinat),
            ElaveAtributlar = map
                .Where(kv => kv.Key.StartsWith(MepXDataSchema.ExtraKeyPrefix, StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key[MepXDataSchema.ExtraKeyPrefix.Length..], kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    public byte[] SerializeToBytes(IMepComponent component) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Serialize(component)));

    public IReadOnlyDictionary<string, string> SerializeToDictionary(IMepComponent component) =>
        Serialize(component).ToDictionary(p => p.Acar, p => p.Deyer, StringComparer.OrdinalIgnoreCase);

    public IMepComponent? DeserializeFromDictionary(
        IReadOnlyDictionary<string, string> map,
        MepDiscipline defaultSistem) =>
        Deserialize(map.Select(kv => new XDataPair(kv.Key, kv.Value)).ToList(), defaultSistem);

    public IMepComponent? DeserializeFromBytes(byte[] data, MepDiscipline defaultSistem)
    {
        if (data.Length == 0)
        {
            return null;
        }

        var pairs = JsonSerializer.Deserialize<List<XDataPair>>(data.AsSpan());
        return pairs is null ? null : Deserialize(pairs, defaultSistem);
    }

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, out var d) ? d : null;
}
