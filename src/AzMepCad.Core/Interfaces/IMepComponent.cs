namespace AzMepCad.Core.Interfaces;

/// <summary>
/// Represents a single MEP catalog or drawing component with Azerbaijani attribute names.
/// </summary>
public interface IMepComponent
{
    string Id { get; }
    string Ad { get; }
    string? Kod { get; }
    MepDiscipline Sistem { get; }

    /// <summary>Outer / nominal diameter (mm).</summary>
    double? Diametr { get; }

    /// <summary>Length along centerline (mm).</summary>
    double? Uzunluq { get; }

    string? Material { get; }
    string? Istehsalci { get; }
    string? Teyinat { get; }

    IReadOnlyDictionary<string, string> ElaveAtributlar { get; }
}

public enum MepDiscipline
{
    Naməlum,
    Santexnika,
    Istilik,
    Ventilyasiya,
    Elektrik,
    Yangin,
}
