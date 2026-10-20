# AzMEP CAD (`AzMepCad`)

Open-source MEP plugin scaffold for **AutoCAD-class** CAD (AutoCAD, BricsCAD, ZWCAD). Modular **.NET 8** solution with Azerbaijani (`az-AZ`) UI strings and BOQ column headers.

> CADprofi-inspired workflow (palette, catalog, routing, BOQ) — **not** a fork and **no** proprietary CADprofi assets.

## Solution layout

```
AzMepCad.sln
src/
  AzMepCad.Core/       Domain + IMepCatalog, IXDataBinder, SQLite catalog
  AzMepCad.Routing/    IRoutingEngine (CAD-agnostic)
  AzMepCad.Boq/        IBoqExporter (ClosedXML Excel)
  AzMepCad.UI/         WPF palette (az-AZ resx)
  AzMepCad.AutoCAD/    AutoCAD 2025+ plugin layer (optional SDK refs)
tests/
  AzMepCad.Core.Tests/ xUnit
data/catalog/          schema.sql + demo JSON seed
docs/                  architecture, Phase 1 discovery checklist
```

## Build (Linux / macOS / CI)

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
export PATH="$PATH:$HOME/.dotnet"
dotnet restore AzMepCad.sln
dotnet build src/AzMepCad.Core/AzMepCad.Core.csproj
dotnet build src/AzMepCad.Routing/AzMepCad.Routing.csproj
dotnet build src/AzMepCad.Boq/AzMepCad.Boq.csproj
dotnet test tests/AzMepCad.Core.Tests/AzMepCad.Core.Tests.csproj
```

`AzMepCad.AutoCAD` builds in **stub mode** without `AUTOCAD_SDK_DIR` (`AZMEP_NO_AUTOCAD_SDK`).

`AzMepCad.UI` (WPF) requires **Windows** and `net8.0-windows`.

## Build (Windows + AutoCAD)

1. Install AutoCAD **2025 or newer** (target; 2026 supported when SDK paths match).
2. Point to managed assemblies:

```powershell
$env:AUTOCAD_SDK_DIR = "C:\Program Files\Autodesk\AutoCAD 2025"
dotnet build src/AzMepCad.AutoCAD/AzMepCad.AutoCAD.csproj -p:EnableAutocadReferences=true
dotnet build src/AzMepCad.UI/AzMepCad.UI.csproj
```

See [src/AzMepCad.AutoCAD/README.md](src/AzMepCad.AutoCAD/README.md).

### Other CAD hosts

| Host | Status |
|------|--------|
| **AutoCAD** 2025+ | Primary; .NET 8 plugin project |
| **AutoCAD** 2019–2024 | Plan: .NET Framework 4.8 sibling project ([docs/architecture.md](docs/architecture.md)) |
| **BricsCAD** | Phase 2 adapter |
| **ZWCAD** | Phase 2 adapter |

## UI language

All user-visible strings in `AzMepCad.UI` use **`az-AZ`** satellite resources (`Resources/Strings.az-AZ.resx`). BOQ Excel headers are Azerbaijani in `ClosedXmlBoqExporter`.

## Catalog database

```bash
sqlite3 data/catalog/azmep.db < data/catalog/schema.sql
```

Demo pipe sizes: [data/catalog/seed/demo-pipe-sizes.json](data/catalog/seed/demo-pipe-sizes.json) (public DIN-style dimensions only).

## AutoCAD commands (az-AZ)

| Command | Description |
|---------|-------------|
| `AZMEP_BORU` | Draw pipe centerline segments with routing + AZMEP XData |
| `AZMEP_SPESIFIKASIYA` | Scan model space, export BOQ Excel (Borular / Armatur sheets) |

Load with `NETLOAD` → `AzMepCad.AutoCAD.dll` (Windows + SDK build). See [docs/istifade-qaydasi.md](docs/istifade-qaydasi.md).

## Documentation

- [docs/architecture.md](docs/architecture.md) — modules, usage flow, roadmap (EN + AZ)
- [docs/istifade-qaydasi.md](docs/istifade-qaydasi.md) — quraşdırma, NETLOAD, əmrlər (az-AZ)
- [docs/phase1-discovery.md](docs/phase1-discovery.md) — Windows checklist for mapping `C:\Program Files\CADprofi` structure (**do not commit proprietary DB content**)

## License

Add your chosen open-source license before public release.
