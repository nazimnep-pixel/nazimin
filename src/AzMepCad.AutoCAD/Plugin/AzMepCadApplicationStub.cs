namespace AzMepCad.AutoCAD.Plugin;

using AzMepCad.Core.Interfaces;
using AzMepCad.Core.XData;
// DefaultXDataBinder retained for compatibility; plugin uses MepXDataBinder.

#if AZMEP_NO_AUTOCAD_SDK

/// <summary>
/// Placeholder application entry when AutoCAD managed DLLs are not referenced.
/// </summary>
public static class AzMepCadApplicationStub
{
    public static string PluginDisplayName => "AzMEP CAD (SDK stub)";

    public static IXDataBinder CreateDefaultBinder() => new MepXDataBinder();
}

#else

// Real IExtensionApplication implementation will live here when SDK is available.
public static class AzMepCadApplicationStub
{
    public static string PluginDisplayName => "AzMEP CAD";

    public static IXDataBinder CreateDefaultBinder() => new MepXDataBinder();
}

#endif
