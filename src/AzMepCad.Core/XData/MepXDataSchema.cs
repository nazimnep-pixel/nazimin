namespace AzMepCad.Core.XData;

/// <summary>
/// AZMEP extended-data keys aligned with az-AZ component attributes.
/// </summary>
public static class MepXDataSchema
{
    public const string RegAppName = "AZMEP";

    public const string KeyId = "Id";
    public const string KeyAd = "Ad";
    public const string KeyKod = "Kod";
    public const string KeySistem = "Sistem";
    public const string KeyDiametr = "Diametr";
    public const string KeyUzunluq = "Uzunluq";
    public const string KeyMaterial = "Material";
    public const string KeyIstehsalci = "Istehsalci";
    public const string KeyTeyinat = "Teyinat";
    public const string KeyKomponentNovu = "KomponentNovu";

    public const string ExtraKeyPrefix = "X_";

    public static IReadOnlyList<string> CoreKeys { get; } =
    [
        KeyId,
        KeyAd,
        KeyKod,
        KeySistem,
        KeyDiametr,
        KeyUzunluq,
        KeyMaterial,
        KeyIstehsalci,
        KeyTeyinat,
        KeyKomponentNovu,
    ];
}
