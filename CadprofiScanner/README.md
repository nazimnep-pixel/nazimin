# AzmCadprofiScanner (four-engine autonomous)

AutoCAD .NET class library for unattended CADprofi knowledge extraction.

## Four engines

| Engine | Source | Role |
|--------|--------|------|
| 1 — Leaf discovery | `LeafDiscoveryEngine.cs`, `CadprofiCuixMenuParser.cs` | Install roots, TSV/INI/CUIX/CUI/MNL registry, leaf enumeration |
| 2+3 — Geometry x-ray | `GeometryXRayEngine.cs`, `ScanCommandInteraction.cs` | Macro execution, model-space diff, functional verification v1.1 |
| 4 — UI watchdog + checkpoints | `UiWatchdogHelpers.cs`, `DesktopJsonCheckpoints.cs` | UIAutomation modal probe, ESC cancel, master/full JSON on Desktop + DWG folder |

## Commands

- `AZM_SCAN_ALL` — full runnable queue (cap 500)
- `AZM_SCAN_FAST` — smoke (`AZM_FAST_COUNT`, default 2)
- `AZM_SCAN_ONE` — single leaf at `AZM_BATCH_OFFSET`
- `AZM_SCAN_BATCH` — chunk (`AZM_BATCH_SIZE` / `AZM_BATCH_OFFSET`)

## Build

```bash
dotnet build CadprofiScanner.csproj -p:AcadDir="C:\Program Files\Autodesk\AutoCAD 2025"
```

## Load

1. `NETLOAD` → `AzmCadprofiScanner.dll`
2. `AZM_SCAN_ALL` (or FAST / ONE / BATCH)

Outputs: `cadprofi_master_knowledge.json` (v1.1), `cadprofi_master_knowledge_full.json` (v2), plus progress `cadprofi_scan_checkpoint.json` on Desktop/Documents.