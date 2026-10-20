# Phase 1 — CADprofi installation discovery (Windows)

**Məqsəd:** Quraşdırılmış CADprofi (və ya oxşar MEP kitabxanası) qovluq **strukturunu** anlamaq — fayl növləri, modul adları, DB formatları — **məzmunu kopyalamadan**.

## Etik qeyd / Ethical note

- Yalnız **öz** lisenziyanız olan quraşdırma üzərində işləyin.
- Proprietary `.mdb`, `.db`, `.dll` kataloq məzmununu **repozitoriyaya commit etməyin**.
- Açıq standartlar (DIN, EN, ISO) və öz SQLite kataloqunuzdan istifadə edin (`data/catalog/`).

## Yoxlama siyahısı / Checklist

Run on a Windows machine with CADprofi installed (typical path):

`C:\Program Files\CADprofi`

### 1. Top-level inventory

- [ ] List all subfolders (e.g. modules, languages, databases, symbols)
- [ ] Note `.exe`, `.dll`, `.chm`, `.ini`, `.cfg` configuration files
- [ ] Record version info from `About` or file properties (no need to upload binaries)

### 2. File types to collect (metadata only)

| Extension | Purpose (your notes) | Copy to repo? |
|-----------|----------------------|---------------|
| `.mdb` / `.accdb` | Catalog? | **No** — note schema only |
| `.sqlite` / `.db` | Local cache? | **No** |
| `.dwg` / `.dxf` | Symbol libraries? | **No** — describe folder names only |
| `.xml` / `.json` | Config? | Only if license permits and is redistributable |
| `.lng` / `.res` | Localization? | Note locale codes (compare with our `az-AZ`) |

### 3. Structure mapping for AzMEP

- [ ] Which folders correspond to **pipes**, **ducts**, **fittings**, **BOQ**?
- [ ] How are **diameter** and **material** attributes named (compare: `Diametr`, `Material`, `Təyinat`)?
- [ ] Is BOQ exported to Excel/CSV? Column headers (map to `AzMepCad.Boq` az-AZ headers)

### 4. Deliverables back to the project

Paste into a private note or issue (not committed):

1. Folder tree (text, 2–3 levels deep)
2. Table of file extensions found
3. Screenshots of palette tabs (optional, blur proprietary part numbers if needed)

### 5. Align with AzMEP scaffold

- [ ] Confirm SQLite schema fields in `data/catalog/schema.sql` cover your needed attributes
- [ ] Set AutoCAD SDK path per `src/AzMepCad.AutoCAD/README.md`
- [ ] Run `dotnet test` on Windows after cloning

## Növbəti addım

Discovery bitəndən sonra koordinatora Phase 2 üçün prioritet verin: marşrut + XData + BOQ end-to-end AutoCAD-da.
