using AzMepCad.Boq;
using AzMepCad.Core.Interfaces;
using AzMepCad.Core.Models;
using Xunit;

namespace AzMepCad.Core.Tests;

public class BoqAggregatorTests
{
    [Fact]
    public void Aggregate_GroupsPipesAndFittings()
    {
        var components = new List<IMepComponent>
        {
            new MepComponentRecord
            {
                Id = "1",
                Ad = "Boru",
                Kod = "P110",
                Sistem = MepDiscipline.Santexnika,
                Diametr = 110,
                Uzunluq = 5000,
                Material = "PE100",
                Teyinat = "Boru",
            },
            new MepComponentRecord
            {
                Id = "2",
                Ad = "Boru",
                Kod = "P110",
                Sistem = MepDiscipline.Santexnika,
                Diametr = 110,
                Uzunluq = 3000,
                Material = "PE100",
                Teyinat = "Boru",
            },
            new MepComponentRecord
            {
                Id = "3",
                Ad = "Dirsek",
                Kod = "EL90",
                Sistem = MepDiscipline.Santexnika,
                Teyinat = "Bucaq90",
            },
        };

        var lines = BoqAggregator.Aggregate(components);

        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.OlcuVahidi == "m" && l.Miqdar == 8.0);
        Assert.Contains(lines, l => l.OlcuVahidi == "ədəd");
    }
}
