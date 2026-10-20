namespace AzMepCad.Core.Interfaces;

/// <summary>
/// Reads MEP components tagged in a CAD drawing (implementation in host layer).
/// </summary>
public interface IMepDrawingScanner
{
    IReadOnlyList<IMepComponent> ScanModelSpace();
}
