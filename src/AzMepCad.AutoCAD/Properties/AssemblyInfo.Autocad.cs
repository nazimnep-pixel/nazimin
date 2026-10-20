#if !AZMEP_NO_AUTOCAD_SDK
using AzMepCad.AutoCAD.Plugin;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(AzMepPlugin))]
#endif
