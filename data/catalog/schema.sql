-- AzMEP CAD catalog schema (SQLite)
-- Attribute names align with az-AZ UI: Diametr, Uzunluq, Material, İstehsalçı, Təyinat

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS mep_standards (
    id TEXT PRIMARY KEY NOT NULL,
    kod TEXT NOT NULL UNIQUE,
    ad TEXT NOT NULL,
    tesvir TEXT
);

CREATE TABLE IF NOT EXISTS mep_components (
    id TEXT PRIMARY KEY NOT NULL,
    ad TEXT NOT NULL,
    kod TEXT,
    sistem TEXT NOT NULL,
    diametr_mm REAL,
    uzunluq_mm REAL,
    material TEXT,
    istehsalci TEXT,
    teyinat TEXT,
    elave_json TEXT,
    standard_id TEXT REFERENCES mep_standards(id)
);

CREATE INDEX IF NOT EXISTS idx_mep_components_sistem ON mep_components(sistem);
CREATE INDEX IF NOT EXISTS idx_mep_components_diametr ON mep_components(diametr_mm);

CREATE TABLE IF NOT EXISTS pipe_size_standards (
    id TEXT PRIMARY KEY NOT NULL,
    standart_kodu TEXT NOT NULL,
    nominal_ad TEXT NOT NULL,
    xarici_diametr_mm REAL NOT NULL,
    daxili_diametr_mm REAL NOT NULL,
    divar_qalinligi_mm REAL NOT NULL,
    UNIQUE (standart_kodu, nominal_ad)
);

CREATE INDEX IF NOT EXISTS idx_pipe_size_standards_kod ON pipe_size_standards(standart_kodu);

CREATE TABLE IF NOT EXISTS mep_fittings (
    id TEXT PRIMARY KEY NOT NULL,
    nov TEXT NOT NULL,
    dn_mm REAL NOT NULL,
    ad TEXT NOT NULL,
    kod TEXT,
    material TEXT
);

CREATE INDEX IF NOT EXISTS idx_mep_fittings_nov_dn ON mep_fittings(nov, dn_mm);

-- Demo seed: public DIN-style dimensions only (not proprietary catalog data)
INSERT OR IGNORE INTO mep_standards (id, kod, ad, tesvir) VALUES
    ('std-din-en10255', 'DIN-EN10255', 'DIN EN 10255 (demo)', 'Public reference dimensions for scaffold tests');

INSERT OR IGNORE INTO pipe_size_standards
    (id, standart_kodu, nominal_ad, xarici_diametr_mm, daxili_diametr_mm, divar_qalinligi_mm) VALUES
    ('din-110', 'DIN-EN10255', 'DN110', 110.0, 104.6, 2.7),
    ('din-160', 'DIN-EN10255', 'DN160', 160.0, 152.4, 3.8),
    ('din-200', 'DIN-EN10255', 'DN200', 200.0, 190.8, 4.6);

INSERT OR IGNORE INTO mep_components
    (id, ad, kod, sistem, diametr_mm, uzunluq_mm, material, istehsalci, teyinat, elave_json, standard_id) VALUES
    ('pipe-dn110-demo', 'Polietilen boru DN110', 'PE110', 'Santexnika', 110.0, NULL, 'PE100', NULL, 'İçmə suyu', '{}', 'std-din-en10255');
