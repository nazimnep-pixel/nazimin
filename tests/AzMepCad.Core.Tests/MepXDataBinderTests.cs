using AzMepCad.Core.Interfaces;
using AzMepCad.Core.Models;
using AzMepCad.Core.XData;
using Xunit;

namespace AzMepCad.Core.Tests;

public class MepXDataBinderTests
{
    [Fact]
    public void RoundTrip_PreservesAttributes()
    {
        var binder = new MepXDataBinder();
        var source = new MepComponentRecord
        {
            Id = "test-1",
            Ad = "Polietilen boru",
            Kod = "PE110",
            Sistem = MepDiscipline.Santexnika,
            Diametr = 110,
            Uzunluq = 2500,
            Material = "PE100",
            Teyinat = "Boru",
        };

        var dict = binder.SerializeToDictionary(source);
        var restored = binder.DeserializeFromDictionary(dict, MepDiscipline.Naməlum);

        Assert.NotNull(restored);
        Assert.Equal(source.Id, restored!.Id);
        Assert.Equal(source.Diametr, restored.Diametr);
    }

    [Fact]
    public void Schema_UsesAzmepRegApp()
    {
        Assert.Equal("AZMEP", MepXDataSchema.RegAppName);
        Assert.Contains(MepXDataSchema.KeyDiametr, MepXDataSchema.CoreKeys);
    }
}
