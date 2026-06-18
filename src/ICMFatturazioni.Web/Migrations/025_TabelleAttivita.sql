-- =====================================================================
-- 025_TabelleAttivita.sql
--
-- Tabelle di supporto per la Gestione Attività Studio (cap. 9 dispensa):
--   fatt.TipiAttivita          — catalogo tipi (CONSULENZE, PROGETTAZIONI, ALTRO)
--   fatt.TipiDettaglioAttivita — catalogo tipi dettaglio (DISCIPLINARE, ecc.)
--   fatt.DescrizioniAttivita   — elenco ordinato di descrizioni standard (cap. 9.3)
--
-- Normalizzazioni rispetto al legacy (AltreTabelle.sql):
--   PK INT IDENTITY → UNIQUEIDENTIFIER (GUID v7 app-side, ADR D22)
--   DataRecord       → rimosso (audit centralizzato in fatt.Audit)
--   INT/NVARCHAR(1)  → BIT dove usato come flag (StudiSettore)
--   GestisciCome     → NVARCHAR(20) con CHECK sui valori ammessi
--   Soft-delete      → IsAttivo BIT NOT NULL DEFAULT 1
-- =====================================================================

PRINT '025_TabelleAttivita in corso...';
GO

-- -----------------------------------------------------------------------
-- 1. fatt.TipiAttivita
-- -----------------------------------------------------------------------
IF OBJECT_ID(N'fatt.TipiAttivita', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.TipiAttivita
    (
        IdTipoAttivita  UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_TipiAttivita PRIMARY KEY,

        TipoAttivita    NVARCHAR(100)    NOT NULL,

        -- 'Consulenza' o 'Progetto': governa il comportamento del gestionale
        -- (cap. 9.1, Fig. 10). Persistito come stringa per leggibilità nel DB.
        GestisciCome    NVARCHAR(20)     NOT NULL
            CONSTRAINT CK_TipiAttivita_GestisciCome
                CHECK (GestisciCome IN ('Consulenza', 'Progetto')),

        -- Flag Studi di Settore (cap. 9, §rinviato). Presente per compatibilità legacy;
        -- la funzionalità vera è rinviata a dopo le fatture.
        StudiSettore    BIT              NOT NULL CONSTRAINT DF_TipiAttivita_StudiSettore DEFAULT 1,

        IsAttivo        BIT              NOT NULL CONSTRAINT DF_TipiAttivita_IsAttivo     DEFAULT 1
    );
    PRINT '  fatt.TipiAttivita creata.';
END
GO

-- Descrizione univoca tra gli attivi.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_TipiAttivita_TipoAttivita' AND object_id = OBJECT_ID(N'fatt.TipiAttivita')
)
    CREATE UNIQUE INDEX UX_TipiAttivita_TipoAttivita
        ON fatt.TipiAttivita (TipoAttivita)
        WHERE IsAttivo = 1;
GO

-- Seed dal legacy (AltreTabelle.sql STA-TipiAttivita).
MERGE fatt.TipiAttivita AS t
USING (VALUES
    (N'10000000-0000-7000-a000-000000000001', N'CONSULENZE',   N'Consulenza', 1),
    (N'10000000-0000-7000-a000-000000000002', N'PROGETTAZIONI',N'Progetto',   1),
    (N'10000000-0000-7000-a000-000000000003', N'ALTRO',        N'Consulenza', 0)
) AS s (IdTipoAttivita, TipoAttivita, GestisciCome, StudiSettore)
ON t.IdTipoAttivita = s.IdTipoAttivita
WHEN NOT MATCHED THEN
    INSERT (IdTipoAttivita, TipoAttivita, GestisciCome, StudiSettore, IsAttivo)
    VALUES (s.IdTipoAttivita, s.TipoAttivita, s.GestisciCome, s.StudiSettore, 1);
GO
PRINT '  Seed fatt.TipiAttivita applicato.';
GO

-- -----------------------------------------------------------------------
-- 2. fatt.TipiDettaglioAttivita
-- -----------------------------------------------------------------------
IF OBJECT_ID(N'fatt.TipiDettaglioAttivita', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.TipiDettaglioAttivita
    (
        IdTipoDettaglioAttivita UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_TipiDettaglioAttivita PRIMARY KEY,

        TipoDettaglioAttivita   NVARCHAR(100)    NOT NULL,

        IsAttivo                BIT              NOT NULL CONSTRAINT DF_TipiDettaglioAttivita_IsAttivo DEFAULT 1
    );
    PRINT '  fatt.TipiDettaglioAttivita creata.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_TipiDettaglioAttivita_Tipo' AND object_id = OBJECT_ID(N'fatt.TipiDettaglioAttivita')
)
    CREATE UNIQUE INDEX UX_TipiDettaglioAttivita_Tipo
        ON fatt.TipiDettaglioAttivita (TipoDettaglioAttivita)
        WHERE IsAttivo = 1;
GO

MERGE fatt.TipiDettaglioAttivita AS t
USING (VALUES
    (N'20000000-0000-7000-a000-000000000001', N'DISCIPLINARE'),
    (N'20000000-0000-7000-a000-000000000002', N'EXTRA DISCIPLINARE'),
    (N'20000000-0000-7000-a000-000000000003', N'VENDITA CESPITE')
) AS s (IdTipoDettaglioAttivita, TipoDettaglioAttivita)
ON t.IdTipoDettaglioAttivita = s.IdTipoDettaglioAttivita
WHEN NOT MATCHED THEN
    INSERT (IdTipoDettaglioAttivita, TipoDettaglioAttivita, IsAttivo)
    VALUES (s.IdTipoDettaglioAttivita, s.TipoDettaglioAttivita, 1);
GO
PRINT '  Seed fatt.TipiDettaglioAttivita applicato.';
GO

-- -----------------------------------------------------------------------
-- 3. fatt.DescrizioniAttivita  (nuova, cap. 9.3)
--    Elenco ordinato di descrizioni standard richiamabili in inserimento.
--    Non è vincolata via FK da AttivitaDettaglio: è solo un catalogo
--    di suggerimenti editabile dall'utente.
-- -----------------------------------------------------------------------
IF OBJECT_ID(N'fatt.DescrizioniAttivita', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.DescrizioniAttivita
    (
        IdDescrizioneAttivita UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_DescrizioniAttivita PRIMARY KEY,

        Descrizione           NVARCHAR(200)    NOT NULL,

        -- Ordine di visualizzazione nel selettore (ordinamento «naturale» lavoro).
        Ordine                INT              NOT NULL CONSTRAINT DF_DescrizioniAttivita_Ordine DEFAULT 0,

        IsAttivo              BIT              NOT NULL CONSTRAINT DF_DescrizioniAttivita_IsAttivo DEFAULT 1
    );
    PRINT '  fatt.DescrizioniAttivita creata.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_DescrizioniAttivita_Descrizione' AND object_id = OBJECT_ID(N'fatt.DescrizioniAttivita')
)
    CREATE UNIQUE INDEX UX_DescrizioniAttivita_Descrizione
        ON fatt.DescrizioniAttivita (Descrizione)
        WHERE IsAttivo = 1;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DescrizioniAttivita_Ordine' AND object_id = OBJECT_ID(N'fatt.DescrizioniAttivita')
)
    CREATE INDEX IX_DescrizioniAttivita_Ordine ON fatt.DescrizioniAttivita (Ordine);
GO

-- -----------------------------------------------------------------------
-- 4. Attiva voci menu:
--    5b...002 = Desc. attività (Tabelle Amm.ve)
--    5b...012 = Tipi attività studio (Attività studio)
--    5b...013 = Tipi dett. attività studio (Attività studio)
-- -----------------------------------------------------------------------
UPDATE fatt.SottoMenu SET Attivo = 1
WHERE IdSottoMenu IN (
    N'5b000000-0000-0000-0000-000000000002',
    N'5b000000-0000-0000-0000-000000000012',
    N'5b000000-0000-0000-0000-000000000013'
);
GO
PRINT '  Voci menu Tipi/DescrizioniAttivita attivate.';
GO

PRINT '025_TabelleAttivita completata.';
GO
