# AzMepCad.AutoCAD

AutoCAD 2025+ plugin layer (.NET 8). References **AcDbMgd**, **AcMgd**, and **AcCoreMgd** from your local ObjectARX / managed SDK install.

## Typical DLL locations (Windows)

| Product | Managed assemblies |
|---------|-------------------|
| AutoCAD 2025 | `C:\Program Files\Autodesk\AutoCAD 2025\` |
| AutoCAD 2026 | `C:\Program Files\Autodesk\AutoCAD 2026\` |

ObjectARX SDK copies (if installed):

`C:\ObjectARX\inc\`

## Build with SDK references

```powershell
$env:AUTOCAD_SDK_DIR = "C:\Program Files\Autodesk\AutoCAD 2025"
dotnet build src/AzMepCad.AutoCAD/AzMepCad.AutoCAD.csproj -p:EnableAutocadReferences=true
```

Without `AUTOCAD_SDK_DIR`, the project builds in **stub mode** (`AZMEP_NO_AUTOCAD_SDK`) so CI/Linux builds succeed.

## Other CAD hosts

- **BricsCAD**: BricsCAD .NET API (separate refs; Phase 2 adapter).
- **ZWCAD**: ZWCAD .NET API (separate refs; Phase 2 adapter).

See root `docs/architecture.md` for .NET Framework 4.8 fallback on older hosts.
