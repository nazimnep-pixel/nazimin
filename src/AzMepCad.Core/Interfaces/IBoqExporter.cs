namespace AzMepCad.Core.Interfaces;

/// <summary>
/// Bill of quantities export (Excel, CSV, etc.).
/// </summary>
public interface IBoqExporter
{
    Task ExportAsync(BoqExportRequest request, Stream output, CancellationToken cancellationToken = default);
}

public sealed record BoqExportRequest(
    string LayiheAdi,
    IReadOnlyList<BoqLineItem> Setrler,
    BoqExportFormat Format = BoqExportFormat.Excel);

public enum BoqExportFormat
{
    Excel,
    Csv,
}

/// <summary>
/// BOQ row with az-AZ column semantics.
/// </summary>
public sealed record BoqLineItem(
    int Sira,
    string Kod,
    string Teyinat,
    string Material,
    string OlcuVahidi,
    double Miqdar,
    string? Qeyd = null);
