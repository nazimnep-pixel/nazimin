namespace AzMepCad.AutoCAD.Commands;

using AzMepCad.AutoCAD.Plugin;
using AzMepCad.AutoCAD.Services;
using AzMepCad.Boq;
using AzMepCad.Core.Interfaces;
using AzMepCad.Core.Models;
using AzMepCad.Routing;

#if !AZMEP_NO_AUTOCAD_SDK
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Windows.Forms;
#endif

public static class AzmepCommands
{
    private static readonly ParametricRoutingEngine RoutingEngine = new();
    private static readonly ClosedXmlBoqExporter BoqExporter = new();

#if AZMEP_NO_AUTOCAD_SDK

    public const string CommandBoru = "AZMEP_BORU";
    public const string CommandSpesifikasiya = "AZMEP_SPESIFIKASIYA";

    public static string BoruPrompt => "\nMərkəzxətt polyline nöqtələrini seçin (SDK stub): ";

    public static string SpesifikasiyaPrompt => "\nBOQ ixrac (SDK stub — AutoCAD tələb olunur).";

    public static RoutingResult SimulateBoruRoute(IReadOnlyList<CenterlinePoint> points, double diametr) =>
        RoutingEngine.Route(new RoutingRequest(MepDiscipline.Santexnika, diametr, points));

#else

    [CommandMethod("AZMEP", "AZMEP_BORU", CommandFlags.Modal)]
    public static void AzmepBoru()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        ed.WriteMessage("\nAzMEP: boru mərkəzxətti — polyline nöqtələrini daxil edin (Enter — bitir).");

        var points = new List<CenterlinePoint>();
        while (true)
        {
            var ppr = ed.GetPoint(points.Count == 0
                ? "\nBaşlanğıc nöqtəsi: "
                : "\nNövbəti nöqtə (Enter — bitir): ");
            if (ppr.Status != PromptStatus.OK)
            {
                break;
            }

            var pt = ppr.Value;
            points.Add(new CenterlinePoint(pt.X, pt.Y, pt.Z));
        }

        if (points.Count < 2)
        {
            ed.WriteMessage("\nAzMEP: ən azı iki nöqtə lazımdır.");
            return;
        }

        var diametrOpts = new PromptDoubleOptions("\nDiametr (mm): ")
        {
            DefaultValue = 110,
            AllowNegative = false,
            AllowZero = false,
        };
        var diametrRes = ed.GetDouble(diametrOpts);
        if (diametrRes.Status != PromptStatus.OK)
        {
            return;
        }

        var result = RoutingEngine.Route(new RoutingRequest(
            MepDiscipline.Santexnika,
            diametrRes.Value,
            points));

        if (!result.Ugurlu)
        {
            ed.WriteMessage($"\nAzMEP: {result.XetaMesaji}");
            return;
        }

        var db = doc.Database;
        using var docLock = doc.LockDocument();
        using (var tr = db.TransactionManager.StartTransaction())
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            AzMepPlugin.XDataService.RegisterApplication(db);

            var segNo = 0;
            foreach (var seg in result.Seqmentler)
            {
                var line = new Line(
                    new Point3d(seg.Start.X, seg.Start.Y, seg.Start.Z),
                    new Point3d(seg.End.X, seg.End.Y, seg.End.Z));
                var uzunluq = line.Length;
                var component = new MepComponentRecord
                {
                    Id = $"boru-{Guid.NewGuid():N}",
                    Ad = $"Boru DN{seg.DiametrMm:F0}",
                    Kod = $"PIPE-{seg.DiametrMm:F0}",
                    Sistem = seg.Sistem,
                    Diametr = seg.DiametrMm,
                    Uzunluq = uzunluq,
                    Material = "PE100",
                    Teyinat = "Boru",
                    ElaveAtributlar = new Dictionary<string, string>
                    {
                        ["KomponentNovu"] = "Boru",
                    },
                };
                AzMepPlugin.XDataService.AttachXData(line, component);
                ms.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);
                segNo++;
            }

            foreach (var fit in result.Fittingler)
            {
                var marker = new Line(
                    new Point3d(fit.Yer.X, fit.Yer.Y, fit.Yer.Z),
                    new Point3d(fit.Yer.X + 1, fit.Yer.Y, fit.Yer.Z));
                var component = new MepComponentRecord
                {
                    Id = $"fit-{Guid.NewGuid():N}",
                    Ad = fit.Nov.ToString(),
                    Kod = fit.Nov.ToString(),
                    Sistem = MepDiscipline.Santexnika,
                    Diametr = fit.DiametrMm,
                    Teyinat = fit.Nov.ToString(),
                    ElaveAtributlar = new Dictionary<string, string>
                    {
                        ["KomponentNovu"] = fit.Nov.ToString(),
                    },
                };
                AzMepPlugin.XDataService.AttachXData(marker, component);
                ms.AppendEntity(marker);
                tr.AddNewlyCreatedDBObject(marker, true);
            }

            tr.Commit();
        }

        ed.WriteMessage($"\nAzMEP: {result.Seqmentler.Count} boru seqmenti, {result.Fittingler.Count} armatur yaradıldı.");
    }

    [CommandMethod("AZMEP", "AZMEP_SPESIFIKASIYA", CommandFlags.Modal)]
    public static void AzmepSpesifikasiya()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var ed = doc.Editor;
        ed.WriteMessage("\nAzMEP: smeta / BOQ — model fəzası skan edilir...");

        var scanner = new AutocadBoqScanner();
        var components = scanner.ScanModelSpace();
        if (components.Count == 0)
        {
            ed.WriteMessage("\nAzMEP: AZMEP XData ilə komponent tapılmadı.");
            return;
        }

        var request = BoqAggregator.ToExportRequest("AzMEP layihə smetası", components);

        using var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = "azmep-smeta.xlsx",
            Title = "Smeta faylını saxla",
        };

        if (dlg.ShowDialog() != DialogResult.OK)
        {
            ed.WriteMessage("\nAzMEP: ixrac ləğv edildi.");
            return;
        }

        using (var stream = File.Open(dlg.FileName, FileMode.Create, FileAccess.Write))
        {
            BoqExporter.ExportAsync(request, stream).GetAwaiter().GetResult();
        }

        ed.WriteMessage($"\nAzMEP: {components.Count} komponent, fayl saxlanıldı: {dlg.FileName}");
    }

#endif
}
