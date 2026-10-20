# AzMEP CAD — istifadə qaydası

AzMEP CAD açıq mənbəli MEP avtomatlaşdırma plaginidir (AutoCAD 2025+). İstifadəçi interfeysi və smeta sütunları **az-AZ** dilindədir.

## Quraşdırma (Windows)

1. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) quraşdırın.
2. AutoCAD **2025 və ya daha yeni** versiyasını quraşdırın.
3. Repozitoriyanı klonlayın və layihəni SDK ilə yığın:

```powershell
$env:AUTOCAD_SDK_DIR = "C:\Program Files\Autodesk\AutoCAD 2025"
dotnet build src/AzMepCad.AutoCAD/AzMepCad.AutoCAD.csproj -p:EnableAutocadReferences=true
```

4. Kataloq bazası (istəyə görə):

```powershell
dotnet run --project tools  # və ya schema.sql + seed JSON ilə sqlite3
```

`data/catalog/schema.sql` və `data/catalog/seed/` qovluğundakı demo fayllardan istifadə edin.

## AutoCAD-də yükləmə (NETLOAD)

1. AutoCAD-i açın.
2. `NETLOAD` əmrini yazın.
3. `AzMepCad.AutoCAD.dll` faylını seçin (`bin\Debug\net8.0-windows\` altında).
4. Palitra üçün (Phase 2): WPF `AzMepCad.UI.dll` — `PaletteSet` ilə inteqrasiya ([src/AzMepCad.AutoCAD/README.md](../src/AzMepCad.AutoCAD/README.md)).

## Əmrlər

| Əmr | Təsvir |
|-----|--------|
| `AZMEP_BORU` | Mərkəzxətt nöqtələrini daxil edin; marşrut mühərriki boru seqmentləri və AZMEP XData yaradır. |
| `AZMEP_SPESIFIKASIYA` | Model fəzasında AZMEP XData olan obyektləri skan edir; **Borular** və **Armatur** vərəqləri olan Excel smeta ixrac edir. |

### AZMEP_BORU addımları

1. Başlanğıc və növbəti nöqtələri klikləyin; **Enter** — bitir.
2. Diametri (mm) daxil edin (məs. `110`).
3. Nəticə: `Line` obyektləri AZMEP genişləndirilmiş məlumatı ilə.

### AZMEP_SPESIFIKASIYA

1. Əmr işə düşəndə smeta avtomatik toplanır.
2. Saxlama dialoqunda `.xlsx` yolunu seçin.
3. Faylda **CƏMİ** sətirləri az-AZ başlıqlarla göstərilir.

## Kataloq

- Standart boru ölçüləri: `DIN-EN10255` (demo, açıq ölçülər).
- CADprofi və ya digər qapalı kataloqlardan məlumat **daxil edilmir**.

## Dəstək

Problem olduqda `docs/architecture.md` və GitHub Issues istifadə edin.
