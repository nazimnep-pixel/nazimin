namespace AzMepCad.Routing;

using AzMepCad.Core.Interfaces;

/// <summary>
/// Analyzes polyline vertices; emits <see cref="RoutingSegment"/> and <see cref="FittingPlacement"/>.
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
                Array.Empty<RoutingSegment>(),
                Array.Empty<FittingPlacement>(),
                "Marşrut üçün ən azı iki nöqtə lazımdır.");
        }

        var segments = new List<RoutingSegment>();
        for (var i = 0; i < request.Points.Count - 1; i++)
        {
            var start = request.Points[i];
            var end = request.Points[i + 1];
            if (SegmentLength(start, end) < 0.001)
            {
                continue;
            }

            var diametr = end.DiametrMm ?? start.DiametrMm ?? request.DiametrMm;
            segments.Add(new RoutingSegment(start, end, diametr, request.Sistem));
        }

        if (request.BranchStubs is { Count: > 0 })
        {
            foreach (var stub in request.BranchStubs)
            {
                if (stub.VertexIndex < 0 || stub.VertexIndex >= request.Points.Count)
                {
                    continue;
                }

                var junction = request.Points[stub.VertexIndex];
                if (SegmentLength(junction, stub.End) < 0.001)
                {
                    continue;
                }

                var diametr = junction.DiametrMm ?? request.DiametrMm;
                segments.Add(new RoutingSegment(junction, stub.End, diametr, request.Sistem));
            }
        }

        if (segments.Count == 0)
        {
            return new RoutingResult(
                false,
                Array.Empty<RoutingSegment>(),
                Array.Empty<FittingPlacement>(),
                "Sıfır uzunluqlu seqmentlər.");
        }

        var fittings = SuggestFittings(segments, request);
        return new RoutingResult(true, segments, fittings);
    }

    public IReadOnlyList<FittingSuggestion> SuggestFittings(IReadOnlyList<CenterlineSegment> segments) =>
        SuggestFittings(
            segments.Select(s => new RoutingSegment(s.Start, s.End, s.DiametrMm, s.Sistem)).ToList(),
            null)
            .Select(f => new FittingSuggestion(f.Nov, f.Yer, f.BucaqDerece, f.DiametrMm))
            .ToList();

    private IReadOnlyList<FittingPlacement> SuggestFittings(
        IReadOnlyList<RoutingSegment> segments,
        RoutingRequest? request)
    {
        var fittings = new List<FittingPlacement>();
        var prefer45 = request?.Options?.Prefer45DegreeElbows ?? false;

        for (var i = 0; i < segments.Count - 1; i++)
        {
            var a = segments[i];
            var b = segments[i + 1];
            if (!PointsEqual(a.End, b.Start))
            {
                continue;
            }

            if (Math.Abs(a.DiametrMm - b.DiametrMm) > 0.01)
            {
                fittings.Add(new FittingPlacement(
                    FittingNovu.Reduktor,
                    a.End,
                    0,
                    a.DiametrMm,
                    b.DiametrMm));
                continue;
            }

            var angle = AngleBetweenDegrees(a, b);
            if (angle < DirectionChangeToleranceDegrees)
            {
                continue;
            }

            var nov = prefer45 && Math.Abs(angle - 45) < Math.Abs(angle - 90)
                ? FittingNovu.Bucaq45
                : FittingNovu.Bucaq90;

            fittings.Add(new FittingPlacement(nov, a.End, angle, a.DiametrMm));
        }

        if (request?.BranchStubs is { Count: > 0 })
        {
            foreach (var stub in request.BranchStubs)
            {
                if (stub.VertexIndex < 0 || stub.VertexIndex >= request.Points.Count)
                {
                    continue;
                }

                var junction = request.Points[stub.VertexIndex];
                var diametr = junction.DiametrMm ?? request.DiametrMm;
                fittings.Add(new FittingPlacement(
                    FittingNovu.Te,
                    junction,
                    90,
                    diametr));
            }
        }

        return fittings;
    }

    private static bool PointsEqual(CenterlinePoint a, CenterlinePoint b) =>
        Math.Abs(a.X - b.X) < 0.001
        && Math.Abs(a.Y - b.Y) < 0.001
        && Math.Abs(a.Z - b.Z) < 0.001;

    private static double SegmentLength(CenterlinePoint start, CenterlinePoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var dz = end.Z - start.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static double AngleBetweenDegrees(RoutingSegment first, RoutingSegment second)
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
