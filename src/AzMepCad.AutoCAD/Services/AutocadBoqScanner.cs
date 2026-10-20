namespace AzMepCad.AutoCAD.Services;

using AzMepCad.Core.Interfaces;
using AzMepCad.Core.XData;

#if !AZMEP_NO_AUTOCAD_SDK
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
#endif

/// <summary>
/// Scans model space for entities carrying AZMEP extended data.
/// </summary>
public sealed class AutocadBoqScanner : IMepDrawingScanner
{
    private readonly AutoCadXDataService _xdata = new();

#if AZMEP_NO_AUTOCAD_SDK

    public IReadOnlyList<IMepComponent> ScanModelSpace() => Array.Empty<IMepComponent>();

#else

    public IReadOnlyList<IMepComponent> ScanModelSpace()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<IMepComponent>();
        }

        var results = new List<IMepComponent>();
        using var docLock = doc.LockDocument();
        var db = doc.Database;

        using (var tr = db.TransactionManager.StartTransaction())
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent)
                {
                    continue;
                }

                var component = _xdata.ReadXData(ent);
                if (component is not null)
                {
                    results.Add(component);
                }
            }

            tr.Commit();
        }

        return results;
    }

#endif
}
