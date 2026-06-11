-- =============================================================================
-- Migration 018 — Tabella fatt.CodiciIVA + abilitazione voce di menu "Codici IVA"
-- =============================================================================
-- Scopo
--   Prima entità di dominio della Fase 3 (Replica del pattern) dopo Anagrafica.
--   Catalogo dei codici IVA usati in fattura. Modella la coppia
--   aliquota/natura prevista dalla normativa: o c'è un'aliquota imponibile
--   (> 0) oppure l'operazione è a 0 e va qualificata con una "Natura IVA"
--   (codice Agenzia Entrate N1..N7).
--
--   Fonte autorevole: dispensa `1-Dispensa_Analisi_Gestionale_Fatturazione.pdf`
--   cap. 6 (§6.1/6.2/6.3). Riferimento legacy (solo struttura): `AltreTabelle.sql`
--   tabella `STA-CodiciIVA`.
--
-- Normalizzazioni rispetto al legacy (`STA-CodiciIVA`)
--   1) Schema applicativo unico `fatt` (ADR D21).
--   2) PK `UNIQUEIDENTIFIER` (GUID UUIDv7 generato app-side, ADR D22) anziché
--      `idCodiceIVA INT IDENTITY`.
--   3) Soft-delete `IsAttivo BIT DEFAULT 1` (ADR D22): i codici IVA non si
--      cancellano fisicamente, si disattivano.
--   4) `idNaturaIVA INT` (FK numerica legacy) → `Natura NVARCHAR(2)` che
--      referenzia `fatt.NatureIVA(Natura)` per **codice naturale** (come
--      Anagrafica→Paesi via SiglaPaese). NatureIVA ha UNIQUE su Natura.
--   5) `Bollo INT` → `ObbligoBollo BIT` (Sì/No: serve a marcare il bollo
--      nell'XML della fattura elettronica).
--
-- Regola di dominio (dispensa §6.2): la **Natura** è condizionale ⟺ Aliquota = 0.
--   * Aliquota > 0  → operazione imponibile, Natura NON ammessa (deve essere NULL).
--   * Aliquota = 0  → operazione non imponibile/esente/ecc., Natura OBBLIGATORIA.
--   Difesa a DB con il CHECK CK_CodiciIVA_NaturaAliquota (l'altra metà della
--   "doppia difesa" è il pre-check nel manager, con messaggi user-friendly).
--   NB: l'ObbligoBollo NON è legato all'aliquota (scelta libera dell'utente).
--
-- Indici
--   * UX_CodiciIVA_Codice: il `Codice` (sigla, di norma = aliquota) è unico
--     **tra i soli attivi** → indice UNIQUE filtrato WHERE IsAttivo = 1. Così
--     un codice disattivato non blocca il riuso della stessa sigla.
--
-- Rollback
--   DROP TABLE fatt.CodiciIVA;
--   (e, per il menu, UPDATE fatt.SottoMenu SET Attivo = 0 WHERE IdSottoMenu = '5b000000-0000-0000-0000-000000000004';)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Necessario per la CREATE INDEX filtrato (WHERE IsAttivo = 1): senza
-- QUOTED_IDENTIFIER ON SQL Server rifiuta gli indici filtrati (errore 1934).
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'fatt.CodiciIVA', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.CodiciIVA
    (
        IdCodiceIVA   UNIQUEIDENTIFIER NOT NULL,
        -- Sigla del codice IVA. Di norma coincide con l'aliquota ('21', '22')
        -- ma può essere alfanumerica libera ('A', '8a'): NVARCHAR(10).
        Codice        NVARCHAR(10)     NOT NULL,
        -- Descrizione che "appare in fattura" (dispensa §6.1): obbligatoria.
        Descrizione   NVARCHAR(50)     NOT NULL,
        -- Aliquota percentuale. DECIMAL(5,2) → fino a 999.99. 0 = non imponibile.
        Aliquota      DECIMAL(5,2)     NOT NULL,
        -- Natura IVA (codice AdE N1..N7), valorizzata SOLO se Aliquota = 0.
        Natura        NVARCHAR(2)      NULL,
        -- Obbligo di bollo (Sì/No), indipendente dall'aliquota. Serve all'XML.
        ObbligoBollo  BIT              NOT NULL CONSTRAINT DF_CodiciIVA_ObbligoBollo DEFAULT (0),
        -- Soft-delete (ADR D22).
        IsAttivo      BIT              NOT NULL CONSTRAINT DF_CodiciIVA_IsAttivo DEFAULT (1),

        CONSTRAINT PK_CodiciIVA        PRIMARY KEY CLUSTERED (IdCodiceIVA),
        CONSTRAINT FK_CodiciIVA_Natura FOREIGN KEY (Natura) REFERENCES fatt.NatureIVA (Natura),
        -- Doppia difesa sulla regola Natura ⟺ Aliquota = 0 (l'altra metà è nel manager).
        CONSTRAINT CK_CodiciIVA_NaturaAliquota CHECK
            ((Aliquota = 0 AND Natura IS NOT NULL) OR (Aliquota > 0 AND Natura IS NULL))
    );
END
GO

-- Indice UNIQUE filtrato sul Codice tra i soli attivi: separato dalla CREATE
-- TABLE per restare idempotente anche dopo una prima esecuzione interrotta.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CodiciIVA_Codice'
      AND object_id = OBJECT_ID(N'fatt.CodiciIVA')
)
BEGIN
    CREATE UNIQUE INDEX UX_CodiciIVA_Codice
        ON fatt.CodiciIVA (Codice)
        WHERE IsAttivo = 1;
END
GO

-- ---------------------------------------------------------------------------
-- Abilitazione della voce di menu "Codici IVA" (finora Attivo = 0, mostrata
-- in grigio). Ora che la pagina /codici-iva esiste, la rendiamo cliccabile.
-- La VISIBILITÀ per ruolo resta governata da MenuRuolo (Admin/Superadmin
-- vedono tutto via bypass in codice).
-- ---------------------------------------------------------------------------
UPDATE fatt.SottoMenu
SET Attivo = 1
WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000004';
GO
