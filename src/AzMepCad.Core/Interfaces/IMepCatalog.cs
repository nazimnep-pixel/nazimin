namespace AzMepCad.Core.Interfaces;

/// <summary>
/// SQLite / JSON backed component and standards catalog.
/// </summary>
public interface IMepCatalog
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogComponent>> AxtarAsync(CatalogSearchFilter filter, CancellationToken cancellationToken = default);

    Task<CatalogComponent?> IdIleGetirAsync(string id, CancellationToken cancellationToken = default);

    Task<PipeSizeStandard?> DiametrStandartiniTapAsync(string standartKodu, double diametrMm, CancellationToken cancellationToken = default);
}

public sealed record CatalogSearchFilter(
    MepDiscipline? Sistem = null,
    string? Material = null,
    double? MinDiametr = null,
    double? MaxDiametr = null,
    string? AxtarisMetni = null);

public sealed class CatalogComponent : IMepComponent
{
    public required string Id { get; init; }
    public required string Ad { get; init; }
    public string? Kod { get; init; }
    public MepDiscipline Sistem { get; init; }
    public double? Diametr { get; init; }
    public double? Uzunluq { get; init; }
    public string? Material { get; init; }
    public string? Istehsalci { get; init; }
    public string? Teyinat { get; init; }
    public IReadOnlyDictionary<string, string> ElaveAtributlar { get; init; } =
        new Dictionary<string, string>();
}

public sealed record PipeSizeStandard(
    string StandartKodu,
    string NominalAd,
    double XariciDiametrMm,
    double DaxiliDiametrMm,
    double DivarQalinligiMm);
