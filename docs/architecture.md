# CadProfi AZ — Architecture sketch (clean-room)

This repository is a **greenfield** reimplementation inspired by the *public* product surface of commercial CAD add-ons (multi-domain modules, symbol libraries, DWG/DXF exchange). It does **not** use CADprofi source code, binaries, or symbol libraries.

## Goals

- Azerbaijani UI and technical terminology.
- Module-oriented design: Architecture → HVAC/Piping → Electrical → Mechanical (order may change with product decisions).
- Clean separation: **core project model**, **exchange** (DXF first), **UI**, optional **host CAD plugin**.

## Recommended layering

```
┌─────────────────────────────────────────┐
│  UI (web/desktop) or Host CAD plugin    │
├─────────────────────────────────────────┤
│  Domain modules (arch, hvac, elec, mech)│
├─────────────────────────────────────────┤
│  Core: geometry, rooms, params, BOM     │
├─────────────────────────────────────────┤
│  Exchange: DXF (DWG later if licensed)  │
└─────────────────────────────────────────┘
```

## MVP default (until user confirms)

- **First module slice:** Architecture — walls, rooms, parametric doors/windows, basic dimensions, DXF export.
- **First stack:** TypeScript web/desktop prototype; evaluate BricsCAD BRX plugin after MVP-1.

Full phased plan and Azerbaijani i18n strategy: see Project store document  
`/cursor/stores/self/docs/cadprofi-az-plan.md` (CadProfi AZ — Clean-Room Layihə Planı).

## Decisions to record

Use Architecture Decision Records under `docs/decisions/` as choices are made (host CAD, DWG vs DXF-only, monorepo tooling).