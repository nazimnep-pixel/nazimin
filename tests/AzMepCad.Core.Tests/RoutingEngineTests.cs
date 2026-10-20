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
    public void Route_TJunction_SuggestsTee()
    {
        var request = new RoutingRequest(
            MepDiscipline.Santexnika,
            110,
            new[]
            {
                new CenterlinePoint(0, 0, 0),
                new CenterlinePoint(1000, 0, 0),
                new CenterlinePoint(2000, 0, 0),
            },
            BranchStubs:
            [
                new BranchStub(1, new CenterlinePoint(1000, 500, 0)),
            ]);

        var result = _engine.Route(request);

        Assert.True(result.Ugurlu);
        Assert.Equal(3, result.Seqmentler.Count);
        Assert.Contains(result.Fittingler, f => f.Nov == FittingNovu.Te);
    }

    [Fact]
    public void Route_DiameterChange_SuggestsReducer()
    {
        var request = new RoutingRequest(
            MepDiscipline.Santexnika,
            110,
            new[]
            {
                new CenterlinePoint(0, 0, 0, 110),
                new CenterlinePoint(1000, 0, 0, 110),
                new CenterlinePoint(2000, 0, 0, 160),
            });

        var result = _engine.Route(request);

        Assert.True(result.Ugurlu);
        Assert.Equal(2, result.Seqmentler.Count);
        var reducer = Assert.Single(result.Fittingler, f => f.Nov == FittingNovu.Reduktor);
        Assert.Equal(110, reducer.DiametrMm);
        Assert.Equal(160, reducer.IkinciDiametrMm);
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
