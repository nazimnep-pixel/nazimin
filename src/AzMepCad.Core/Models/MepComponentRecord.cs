namespace AzMepCad.Core.Models;

using AzMepCad.Core.Interfaces;

/// <summary>
/// Default in-memory MEP component implementation.
/// </summary>
public sealed class MepComponentRecord : IMepComponent
{
    public required string Id { get; init; }
    public required string Ad { get; init; }
    public string? Kod { get; init; }
    public MepDiscipline Sistem { get; init; } = MepDiscipline.Naməlum;
    public double? Diametr { get; init; }
    public double? Uzunluq { get; init; }
    public string? Material { get; init; }
    public string? Istehsalci { get; init; }
    public string? Teyinat { get; init; }
    public IReadOnlyDictionary<string, string> ElaveAtributlar { get; init; } =
        new Dictionary<string, string>();
}
