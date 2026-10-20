namespace AzMepCad.Routing;

using AzMepCad.Core.Interfaces;

/// <summary>
/// Stub routing engine: builds straight segments between points and suggests elbows at direction changes.
/// </summary>
public sealed class ParametricRoutingEngine : IRoutingEngine
{
    private const double DirectionChangeToleranceDegrees = 1.0;

    public RoutingResult Route(RoutingRequest request)
    {
        if (request.Points.Count < 2)
        {
            return new RoutingResult(
                false,
                Array.Empty<CenterlineSegment>(),
                Array.Empty<FittingSuggestion>(),
                "Marşrut üçün ən azı iki nöqtə lazımdır.");
        }

        var segments = new List<CenterlineSegment>();
        for (var i = 0; i < request.Points.Count - 1; i++)
        {
            var start = request.Points[i];
            var end = request.Points[i + 1];
            if (SegmentLength(start, end) < 0.001)
            {
                continue;
            }

            segments.Add(new CenterlineSegment(start, end, request.DiametrMm, request.Sistem));
        }

        if (segments.Count == 0)
        {
            return new RoutingResult(
                false,
                Array.Empty<CenterlineSegment>(),
                Array.Empty<FittingSuggestion>(),
                "Sıfır uzunluqlu seqmentlər.");
        }

        var fittings = SuggestFittings(segments);
        return new RoutingResult(true, segments, fittings);
    }

    public IReadOnlyList<FittingSuggestion> SuggestFittings(IReadOnlyList<CenterlineSegment> segments)
    {
        if (segments.Count < 2)
        {
            return Array.Empty<FittingSuggestion>();
        }

        var fittings = new List<FittingSuggestion>();
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var a = segments[i];
            var b = segments[i + 1];
            var angle = AngleBetweenDegrees(a, b);
            if (angle < DirectionChangeToleranceDegrees)
            {
                continue;
            }

            var prefer45 = false;
            var nov = prefer45 && Math.Abs(angle - 45) < Math.Abs(angle - 90)
                ? FittingNovu.Bucaq45
                : FittingNovu.Bucaq90;

            fittings.Add(new FittingSuggestion(
                nov,
                a.End,
                angle,
                a.DiametrMm));
        }

        return fittings;
    }

    private static double SegmentLength(CenterlinePoint start, CenterlinePoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var dz = end.Z - start.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static double AngleBetweenDegrees(CenterlineSegment first, CenterlineSegment second)
    {
        var v1 = Normalize(
            first.End.X - first.Start.X,
            first.End.Y - first.Start.Y,
            first.End.Z - first.Start.Z);
        var v2 = Normalize(
            second.End.X - second.Start.X,
            second.End.Y - second.Start.Y,
            second.End.Z - second.Start.Z);

        var dot = Clamp(v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z, -1, 1);
        return Math.Acos(dot) * (180.0 / Math.PI);
    }

    private static (double X, double Y, double Z) Normalize(double x, double y, double z)
    {
        var len = Math.Sqrt(x * x + y * y + z * z);
        if (len < 1e-9)
        {
            return (0, 0, 0);
        }

        return (x / len, y / len, z / len);
    }

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;
}
