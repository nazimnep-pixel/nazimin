namespace AzMepCad.AutoCAD.Plugin;

using AzMepCad.AutoCAD.Services;
using AzMepCad.Core.XData;

#if !AZMEP_NO_AUTOCAD_SDK
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
#endif

#if AZMEP_NO_AUTOCAD_SDK

/// <summary>
/// Stub plugin entry when AutoCAD SDK is not referenced (Linux / CI).
/// </summary>
public static class AzMepPlugin
{
    public static string DisplayName => "AzMEP CAD (SDK stub)";

    public static void Initialize() { }

    public static void Terminate() { }

    public static AutoCadXDataService XDataService { get; } = new();
}

#else

public class AzMepPlugin : IExtensionApplication
{
    public static AutoCadXDataService XDataService { get; } = new();

    public void Initialize()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is not null)
        {
            XDataService.RegisterApplication(doc.Database);
        }

        Application.DocumentManager.DocumentCreated += (_, e) =>
            XDataService.RegisterApplication(e.Document.Database);
    }

    public void Terminate()
    {
    }
}

#endif
