namespace AzMepCad.Core.Interfaces;

/// <summary>
/// Maps <see cref="IMepComponent"/> attributes to CAD entity extended data (implementation in AutoCAD layer).
/// </summary>
public interface IXDataBinder
{
    string ApplicationName { get; }

    IReadOnlyList<XDataPair> Serialize(IMepComponent component);

    IMepComponent? Deserialize(IReadOnlyList<XDataPair> pairs, MepDiscipline defaultSistem);
}

public sealed record XDataPair(string Acar, string Deyer);
