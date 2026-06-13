-- =============================================================================
-- Migration 021 — Tabella fatt.TipiPagamento + voce di menu "Tipi di pagamento"
-- =============================================================================
-- Scopo
--   Catalogo dei TIPI di pagamento (livello "padre" della gerarchia
--   tipo→codice, dispensa cap. 3). Ogni tipo porta il FLAG BANCA che decide,
--   in fattura, di chi sono i dati bancari mostrati:
--     * 'A' = dati banca dell'AZIENDA (bonifico: il cliente versa sul nostro IBAN);
--     * 'C' = dati banca del CLIENTE (ricevuta bancaria: servono ABI/CAB cliente).
--   Qui è sola persistenza del flag; il filtro banche per flag scatta alla
--   Tappa 6 (integrazione anagrafica), che userà BancaAppoggioManager.Selezionabili.
--
--   Fonte autorevole: docs/piano-sviluppo-fase1-attivita.md (Tappa 2) e
--   dispensa cap. 3. Riferimento legacy (struttura): `AltreTabelle.sql`
--   tabella `STA-TipiPagamento`.
--
-- Convenzioni (allineate alle verticali già fatte, NON al piano vecchio):
--   PK UNIQUEIDENTIFIER (GUID UUIDv7 app-side, ADR D22), soft-delete IsAttivo;
--   FlagBanca CHAR(1) con CHECK (come TipoAnagrafica S/P/E). Nomi colonna fedeli
--   al legacy IT (D1): TipoPagamento (descrizione), SiglaPag.
--
-- Indici
--   * UX_TipiPagamento_Descr: descrizione univoca tra gli attivi (chiave umana, combo).
--   * UX_TipiPagamento_Sigla: sigla univoca tra gli attivi (quando valorizzata).
--
-- Rollback
--   DROP TABLE fatt.TipiPagamento;
--   DELETE FROM fatt.SottoMenu WHERE IdSottoMenu = '5b000000-0000-0000-0000-000000000007';
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'fatt.TipiPagamento', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.TipiPagamento
    (
        IdTipoPagamento UNIQUEIDENTIFIER NOT NULL,
        -- Descrizione del tipo (es. "Bonifico", "Ricevute bancarie").
        TipoPagamento   NVARCHAR(50)     NOT NULL,
        -- Sigla breve (es. "BO", "RB"). Facoltativa.
        SiglaPag        NVARCHAR(2)      NULL,
        -- Flag banca: 'A' = dati azienda, 'C' = dati cliente.
        FlagBanca       CHAR(1)          NOT NULL,
        IsAttivo        BIT              NOT NULL CONSTRAINT DF_TipiPagamento_IsAttivo DEFAULT (1),

        CONSTRAINT PK_TipiPagamento          PRIMARY KEY CLUSTERED (IdTipoPagamento),
        CONSTRAINT CK_TipiPagamento_FlagBanca CHECK (FlagBanca IN ('A', 'C'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TipiPagamento_Descr' AND object_id = OBJECT_ID(N'fatt.TipiPagamento'))
BEGIN
    CREATE UNIQUE INDEX UX_TipiPagamento_Descr ON fatt.TipiPagamento (TipoPagamento) WHERE IsAttivo = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TipiPagamento_Sigla' AND object_id = OBJECT_ID(N'fatt.TipiPagamento'))
BEGIN
    CREATE UNIQUE INDEX UX_TipiPagamento_Sigla ON fatt.TipiPagamento (SiglaPag) WHERE IsAttivo = 1 AND SiglaPag IS NOT NULL;
END
GO

-- ---------------------------------------------------------------------------
-- Seed di partenza (idempotente per IdTipoPagamento). GUID fissi: come per il
-- seed del menu, i dati di partenza usano id deterministici.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM fatt.TipiPagamento WHERE IdTipoPagamento = N'7a000000-0000-0000-0000-000000000001')
    INSERT INTO fatt.TipiPagamento (IdTipoPagamento, TipoPagamento, SiglaPag, FlagBanca)
    VALUES (N'7a000000-0000-0000-0000-000000000001', N'Bonifico', N'BO', 'A');

IF NOT EXISTS (SELECT 1 FROM fatt.TipiPagamento WHERE IdTipoPagamento = N'7a000000-0000-0000-0000-000000000002')
    INSERT INTO fatt.TipiPagamento (IdTipoPagamento, TipoPagamento, SiglaPag, FlagBanca)
    VALUES (N'7a000000-0000-0000-0000-000000000002', N'Ricevute bancarie', N'RB', 'C');
GO

-- ---------------------------------------------------------------------------
-- Voce di menu "Tipi di pagamento" (nuova, sotto il gruppo Tabelle Amm.ve).
-- La pagina /tipi-pagamento (classe TipiPagamento) esiste ora: Attivo = 1.
-- ---------------------------------------------------------------------------
-- NB: la colonna che contiene il nome-classe della pagina si chiama `SottoMenu`
-- (non `Pagina`); `Descrizione` è l'etichetta mostrata.
IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000007')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (N'5b000000-0000-0000-0000-000000000007', N'b0000000-0000-0000-0000-000000000001',
            N'Tipi di pagamento', N'TipiPagamento', N'Paid', 45, 1);
GO
