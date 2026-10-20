namespace AzMepCad.Core.Interfaces;

/// <summary>
/// Parametric pipe/duct centerline routing and fitting selection (CAD-agnostic).
/// </summary>
public interface IRoutingEngine
{
    RoutingResult Route(RoutingRequest request);

    IReadOnlyList<FittingSuggestion> SuggestFittings(IReadOnlyList<CenterlineSegment> segments);
}

public sealed record RoutingRequest(
    MepDiscipline Sistem,
    double DiametrMm,
    IReadOnlyList<CenterlinePoint> Points,
    RoutingOptions? Options = null,
    IReadOnlyList<BranchStub>? BranchStubs = null);

public sealed record RoutingOptions(
    double MinBendRadiusMm = 150,
    double MaxSegmentLengthMm = 6000,
    bool Prefer45DegreeElbows = false);

public sealed record CenterlinePoint(double X, double Y, double Z, double? DiametrMm = null);

/// <summary>Parametric centerline segment emitted by the routing engine.</summary>
public sealed record RoutingSegment(
    CenterlinePoint Start,
    CenterlinePoint End,
    double DiametrMm,
    MepDiscipline Sistem);

/// <summary>Backward-compatible alias for <see cref="RoutingSegment"/>.</summary>
public sealed record CenterlineSegment(
    CenterlinePoint Start,
    CenterlinePoint End,
    double DiametrMm,
    MepDiscipline Sistem);

public sealed record BranchStub(int VertexIndex, CenterlinePoint End);

public sealed record RoutingResult(
    bool Ugurlu,
    IReadOnlyList<RoutingSegment> Seqmentler,
    IReadOnlyList<FittingPlacement> Fittingler,
    string? XetaMesaji = null);

/// <summary>Suggested fitting at a vertex or junction.</summary>
public sealed record FittingPlacement(
    FittingNovu Nov,
    CenterlinePoint Yer,
    double BucaqDerece,
    double DiametrMm,
    double? IkinciDiametrMm = null);

/// <summary>Backward-compatible alias for <see cref="FittingPlacement"/>.</summary>
public sealed record FittingSuggestion(
    FittingNovu Nov,
    CenterlinePoint Yer,
    double BucaqDerece,
    double DiametrMm);

public enum FittingNovu
{
    DuzBorus,
    Bucaq90,
    Bucaq45,
    Te,
    Reduktor,
    Flanec,
}
