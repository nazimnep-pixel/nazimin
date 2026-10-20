namespace AzMepCad.AutoCAD.Services;

using AzMepCad.Core.Interfaces;
using AzMepCad.Core.XData;

#if !AZMEP_NO_AUTOCAD_SDK
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
#endif

/// <summary>
/// Attaches and reads AZMEP XData on drawing entities.
/// </summary>
public sealed class AutoCadXDataService
{
    private readonly MepXDataBinder _binder = new();

    public string ApplicationName => _binder.ApplicationName;

#if AZMEP_NO_AUTOCAD_SDK

    public void RegisterApplication(object database) { }

    public void AttachXData(object entity, IMepComponent component) { }

    public IMepComponent? ReadXData(object entity, MepDiscipline defaultSistem = MepDiscipline.Santexnika) =>
        null;

#else

    public void RegisterApplication(Database database)
    {
        using var reg = database.RegAppTableId.GetObject(OpenMode.ForRead) as RegAppTable
            ?? throw new InvalidOperationException("RegAppTable oxunmadı.");
        if (reg.Has(ApplicationName))
        {
            return;
        }

        reg.UpgradeOpen();
        using var record = new RegAppTableRecord { Name = ApplicationName };
        reg.Add(record);
        database.TransactionManager.AddNewlyCreatedDBObject(record, true);
    }

    public void AttachXData(Entity entity, IMepComponent component)
    {
        var pairs = _binder.Serialize(component);
        var values = new ResultBuffer { new TypedValue((int)DxfCode.ExtendedDataRegAppName, ApplicationName) };
        foreach (var pair in pairs)
        {
            values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"{pair.Acar}={pair.Deyer}"));
        }

        entity.XData = values;
    }

    public IMepComponent? ReadXData(Entity entity, MepDiscipline defaultSistem = MepDiscipline.Santexnika)
    {
        var rb = entity.GetXDataForApplication(ApplicationName);
        if (rb is null)
        {
            return null;
        }

        var pairs = new List<XDataPair>();
        foreach (TypedValue tv in rb)
        {
            if (tv.TypeCode != (int)DxfCode.ExtendedDataAsciiString || tv.Value is not string s)
            {
                continue;
            }

            var eq = s.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            pairs.Add(new XDataPair(s[..eq], s[(eq + 1)..]));
        }

        return _binder.Deserialize(pairs, defaultSistem);
    }

#endif
}
