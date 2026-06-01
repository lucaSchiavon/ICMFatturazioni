-- =============================================================================
-- Migration 005 — Tabella ana.Anagrafica
-- =============================================================================
-- Scopo
--   Tabella principale del modulo Anagrafica clienti. Replica lo schema
--   del DB Access originale (vedi `Schema database.png`) con le seguenti
--   normalizzazioni:
--
--   1) Schema `ana` (ADR D2) anziché prefisso nel nome.
--   2) Refuso `PerFatturaEletronica` corretto in `PECFatturaElettronica`
--      (decisione in CLAUDE.md "Schema database").
--   3) Campi `IdCodiceMerce` e `ResaMerce` OMESSI (ADR D8 chiusa il
--      2026-05-20): ICM opera in Industrial Construction Management,
--      non in compravendita di merci. Eventuale reintroduzione futura
--      sarà una migration additiva.
--   4) `TipoAnagrafica` come CHAR(1) con CHECK su {'S','P','E'} (ADR D3):
--      enum a livello DB, non FK verso tabella lookup.
--   5) `SiglaPaese` default `IT` (D14 + descrizione funzionale: l'azienda
--      lavora prevalentemente con clienti italiani).
--   6) Audit timestamp `DataRecord` con DATETIME2(3) DEFAULT SYSUTCDATETIME()
--      (D9): qui ha senso perché Anagrafica è tabella volatile e auditabile.
--
-- Foreign Key
--   FK_Anagrafica_Paesi:    SiglaPaese → sta.Paesi(CodicePaese)
--   FK_Anagrafica_Province: Provincia  → sta.Province(Prov)
--
--   I campi IdPag / IdCodiciIVA / IdBancaAppoggio / IdTipologieClientela
--   restano come INT nullable **senza FK** in questa migration: le tabelle
--   parent verranno create in Fase 3 (Codici pagamento, Banche, Codici IVA,
--   Tipologie clientela). La FK sarà aggiunta da una migration successiva
--   con ALTER TABLE ADD CONSTRAINT — additiva, non distruttiva.
--
-- Rollback
--   DROP TABLE ana.Anagrafica;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Necessario per CREATE INDEX su indici filtrati (es. WHERE PIVA IS NOT NULL).
-- Senza questa opzione SQL Server rifiuta la creazione (errore 1934).
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'ana.Anagrafica', N'U') IS NULL
BEGIN
    CREATE TABLE ana.Anagrafica
    (
        IdAnagrafica            INT             IDENTITY(1,1) NOT NULL,
        TipoAnagrafica          CHAR(1)         NOT NULL,
        RagioneSociale          NVARCHAR(200)   NOT NULL,
        Indirizzo               NVARCHAR(200)   NULL,
        CAP                     NVARCHAR(10)    NULL,
        City                    NVARCHAR(100)   NULL,
        Provincia               NVARCHAR(2)     NULL,
        SiglaPaese              NVARCHAR(2)     NOT NULL CONSTRAINT DF_Anagrafica_SiglaPaese DEFAULT (N'IT'),
        Telefono                NVARCHAR(40)    NULL,
        Cellulare               NVARCHAR(40)    NULL,
        Fax                     NVARCHAR(40)    NULL,
        Email                   NVARCHAR(200)   NULL,
        PIVA                    NVARCHAR(20)    NULL,
        Contatto                NVARCHAR(200)   NULL,
        IdPag                   INT             NULL,
        IdBancaAppoggio         INT             NULL,
        IdCodiciIVA             INT             NULL,
        IdTipologieClientela    INT             NULL,
        CodiceDestinatario      NVARCHAR(10)    NULL,
        PECFatturaElettronica   NVARCHAR(200)   NULL,
        DataRecord              DATETIME2(3)    NOT NULL CONSTRAINT DF_Anagrafica_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Anagrafica                PRIMARY KEY CLUSTERED (IdAnagrafica),
        CONSTRAINT CK_Anagrafica_TipoAnagrafica CHECK (TipoAnagrafica IN ('S', 'P', 'E')),
        CONSTRAINT FK_Anagrafica_Paesi          FOREIGN KEY (SiglaPaese) REFERENCES sta.Paesi    (CodicePaese),
        CONSTRAINT FK_Anagrafica_Province       FOREIGN KEY (Provincia)  REFERENCES sta.Province (Prov)
    );

    -- Indici per le query frequenti:
    -- * Filtro tipologia + ricerca alfabetica per ragione sociale (caso
    --   tipico della maschera elenco: "tutti i privati ordinati per nome").
    CREATE INDEX IX_Anagrafica_TipoAnagrafica_RagioneSociale
        ON ana.Anagrafica (TipoAnagrafica, RagioneSociale);
END
GO

-- Indice filtrato per PIVA: separato dal blocco di CREATE TABLE per
-- restare idempotente anche se la tabella esisteva già (è il caso di una
-- prima esecuzione fallita per QUOTED_IDENTIFIER OFF).
-- Non è UNIQUE: la colonna PIVA è un campo unificato PIVA/CF e in fase di
-- importazione storica si possono trovare duplicati legittimi.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Anagrafica_PIVA'
      AND object_id = OBJECT_ID(N'ana.Anagrafica')
)
BEGIN
    CREATE INDEX IX_Anagrafica_PIVA
        ON ana.Anagrafica (PIVA)
        WHERE PIVA IS NOT NULL;
END
GO
