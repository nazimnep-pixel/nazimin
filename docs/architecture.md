# AzMEP CAD — Architecture / Arxitektura

Open-source MEP automation plugin for AutoCAD-class hosts. Inspired by general CAD+MEP workflows (e.g. CADprofi-style palettes, routing, BOQ); **does not ship or depend on proprietary CADprofi databases or assets**.

## Modules / Modullar

| Project | Role | CAD dependency |
|---------|------|----------------|
| `AzMepCad.Core` | Domain models, `IMepComponent`, catalog (SQLite), XData binding contracts | None |
| `AzMepCad.Routing` | `IRoutingEngine` — centerline segments, fitting hints | None |
| `AzMepCad.Boq` | `IBoqExporter` — Excel (ClosedXML), az-AZ headers | None |
| `AzMepCad.UI` | WPF palette shell, `az-AZ` `.resx` strings | WPF (Windows) |
| `AzMepCad.AutoCAD` | Plugin entry, drawing bridge, XData on entities | AutoCAD 2025+ managed API |

### Core interfaces (Phase 1)

- **IMepComponent** — `Diametr`, `Uzunluq`, `Material`, `İstehsalçı` (`Istehsalci` in code), `Təyinat` (`Teyinat`)
- **IRoutingEngine** — parametric route + fitting suggestions
- **IMepCatalog** — SQLite catalog search and DIN-style size lookup
- **IBoqExporter** — BOQ / smeta export
- **IXDataBinder** — serialize MEP attributes for CAD XData

## Data model / Məlumat modeli

- **SQLite** (`data/catalog/schema.sql`): `mep_components`, `mep_standards`, `pipe_size_standards`
- **JSON seed** (`data/catalog/seed/demo-pipe-sizes.json`): demo public DIN dimensions only
- Drawing persistence: XData application name `AZMEP` (see `DefaultXDataBinder`)

## Target platforms

| Host | .NET | Notes |
|------|------|-------|
| AutoCAD 2025+ | **.NET 8** (this repo default) | Set `AUTOCAD_SDK_DIR` to folder with `AcDbMgd.dll` |
| AutoCAD 2019–2024 | .NET Framework **4.8** (Phase 2 split) | Duplicate plugin projects targeting `net48`; share Core/Routing via multi-targeting |
| BricsCAD | .NET 8 / 4.x per version | Separate adapter assembly |
| ZWCAD | Vendor .NET API | Separate adapter assembly |

Build AutoCAD layer without SDK (CI/Linux):

```bash
dotnet build src/AzMepCad.AutoCAD/AzMepCad.AutoCAD.csproj
# defines AZMEP_NO_AUTOCAD_SDK
```

## Roadmap

### Phase 1 — Scaffold (current)

- Solution layout, interfaces, stub routing, BOQ export, WPF palette shell
- Catalog schema + demo seed
- Unit tests for routing and size lookup

### Phase 2 — Drawing integration

- Real `IExtensionApplication`, commands, palette dock
- XData read/write on polylines/blocks
- User catalog import (CSV/SQLite), no third-party DB redistribution

### Phase 3 — Production MEP

- Fitting libraries (elbows, tees, reducers) from open standards
- BOQ rules (length/ea by system), layer standards
- Optional BricsCAD/ZWCAD adapters

## Ethical / legal note

Use installed commercial tools only on your machine to **map folder layout and file types** for interoperability planning. Do **not** commit proprietary databases, symbols, or licensed content into this repository.
