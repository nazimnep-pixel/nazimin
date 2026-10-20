using AzMepCad.Core.Interfaces;
using AzMepCad.Routing;
using Xunit;

namespace AzMepCad.Core.Tests;

public class RoutingEngineTests
{
    private readonly ParametricRoutingEngine _engine = new();

    [Fact]
    public void Route_TwoPoints_ReturnsOneSegment()
    {
        var request = new RoutingRequest(
            MepDiscipline.Santexnika,
            110,
            new[]
            {
                new CenterlinePoint(0, 0, 0),
                new CenterlinePoint(1000, 0, 0),
            });

        var result = _engine.Route(request);

        Assert.True(result.Ugurlu);
        Assert.Single(result.Seqmentler);
        Assert.Empty(result.Fittingler);
    }

    [Fact]
    public void Route_LShape_SuggestsElbow()
    {
        var request = new RoutingRequest(
            MepDiscipline.Santexnika,
            110,
            new[]
            {
                new CenterlinePoint(0, 0, 0),
                new CenterlinePoint(1000, 0, 0),
                new CenterlinePoint(1000, 1000, 0),
            });

        var result = _engine.Route(request);

        Assert.True(result.Ugurlu);
        Assert.Equal(2, result.Seqmentler.Count);
        Assert.NotEmpty(result.Fittingler);
        Assert.Equal(FittingNovu.Bucaq90, result.Fittingler[0].Nov);
    }

    [Fact]
    public void Route_SinglePoint_Fails()
    {
        var result = _engine.Route(new RoutingRequest(
            MepDiscipline.Ventilyasiya,
            200,
            new[] { new CenterlinePoint(0, 0, 0) }));

        Assert.False(result.Ugurlu);
        Assert.NotNull(result.XetaMesaji);
    }
}
