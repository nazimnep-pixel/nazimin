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
    RoutingOptions? Options = null);

public sealed record RoutingOptions(
    double MinBendRadiusMm = 150,
    double MaxSegmentLengthMm = 6000,
    bool Prefer45DegreeElbows = false);

public sealed record CenterlinePoint(double X, double Y, double Z);

public sealed record CenterlineSegment(
    CenterlinePoint Start,
    CenterlinePoint End,
    double DiametrMm,
    MepDiscipline Sistem);

public sealed record RoutingResult(
    bool Ugurlu,
    IReadOnlyList<CenterlineSegment> Seqmentler,
    IReadOnlyList<FittingSuggestion> Fittingler,
    string? XetaMesaji = null);

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
