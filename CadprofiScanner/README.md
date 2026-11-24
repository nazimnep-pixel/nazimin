# AzmCadprofiScanner

AutoCAD .NET class library that extracts CADprofi menu/config knowledge and optional geometry metadata.

## Command

- **`AZM_SCAN_ALL`** — full autonomous scan (no prompts). Writes `cadprofi_master_knowledge.json` beside the active drawing.

## Build

Requires AutoCAD-managed assemblies (`acmgd`, `acdbmgd`, `accoremgd`) on the build machine:

```bash
dotnet build CadprofiScanner.csproj -p:AcadDir="C:\Program Files\Autodesk\AutoCAD 2024"
```

## Load

1. `NETLOAD` → `AzmCadprofiScanner.dll`
2. `AZM_SCAN_ALL`

Path discovery and JSON layout are documented in the project store at `internal/cadprofi-scanner-notes.md`.