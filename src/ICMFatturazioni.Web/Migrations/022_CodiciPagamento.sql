-- =============================================================================
-- Migration 022 — Tabella fatt.CodiciPagamento + abilitazione voce di menu
-- =============================================================================
-- Scopo
--   Catalogo dei CODICI di pagamento (livello "figlio" della gerarchia
--   tipo→codice, dispensa cap. 3-4-5). Ogni codice appartiene a un TIPO
--   (fatt.TipiPagamento, da cui eredita il flag banca A/C) e descrive una
--   regola di scadenza: numero di rate (1..3), giorni alla scadenza per rata,
--   eventuale spostamento a fine mese e giorni aggiuntivi. Referenzia inoltre
--   la Condizione e la Modalità di pagamento (lookup ministeriali, per la
--   fattura elettronica).
--
--   Il CALCOLO delle date di scadenza non è in tabella: è il servizio
--   applicativo ScadenzaCalculator (algoritmo dispensa §5.1).
--
--   Fonte: docs/piano-sviluppo-fase1-attivita.md (Tappa 3) e dispensa cap. 4/5.
--   Riferimento legacy: `AltreTabelle.sql` tabella `STA-CodiciPagamento`.
--
-- Convenzioni (allineate alle verticali già fatte): PK UNIQUEIDENTIFIER (GUID
--   UUIDv7 app-side, ADR D22), soft-delete IsAttivo. FK alle lookup
--   Condizione/Modalità per CODICE NATURALE (NCHAR(4), come CodiciIVA→NatureIVA),
--   non per Id INT.
--
-- Regole di dominio (cap. 4) — doppia difesa CHECK + manager:
--   * NumScadenze ∈ [1,3];
--   * GGpiu (giorni aggiuntivi) ammessi SOLO se FineMese = 1.
--   (La coerenza GGScadN ↔ NumScadenze è validata nel manager.)
--
-- Rollback
--   DROP TABLE fatt.CodiciPagamento;
--   UPDATE fatt.SottoMenu SET Attivo = 0 WHERE IdSottoMenu = '5b000000-0000-0000-0000-000000000005';
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'fatt.CodiciPagamento', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.CodiciPagamento
    (
        IdCodicePagamento   UNIQUEIDENTIFIER NOT NULL,
        -- Tipo di appartenenza (eredita il flag banca A/C).
        IdTipoPagamento     UNIQUEIDENTIFIER NOT NULL,
        -- Descrizione del codice (es. "BONIFICO 60 GG F.M.").
        DescrPag            NVARCHAR(70)     NOT NULL,
        -- Numero di rate (1..3).
        NumScadenze         INT              NOT NULL,
        -- Giorni alla scadenza per rata (GGScad1 obbligatorio).
        GGScad1             INT              NOT NULL,
        GGScad2             INT              NULL,
        GGScad3             INT              NULL,
        -- Giorni aggiuntivi dopo il fine mese (solo se FineMese = 1).
        GGpiu               INT              NULL,
        -- Spostamento a fine mese (Sì/No).
        FineMese            BIT              NOT NULL CONSTRAINT DF_CodiciPagamento_FineMese DEFAULT (0),
        -- Lookup ministeriali (per codice naturale): Condizione/Modalità FE.
        CondizionePagamento NCHAR(4)         NULL,
        ModalitaPagamento   NCHAR(4)         NULL,
        IsAttivo            BIT              NOT NULL CONSTRAINT DF_CodiciPagamento_IsAttivo DEFAULT (1),

        CONSTRAINT PK_CodiciPagamento            PRIMARY KEY CLUSTERED (IdCodicePagamento),
        CONSTRAINT FK_CodiciPagamento_Tipo       FOREIGN KEY (IdTipoPagamento)     REFERENCES fatt.TipiPagamento (IdTipoPagamento),
        CONSTRAINT FK_CodiciPagamento_Condizione FOREIGN KEY (CondizionePagamento) REFERENCES fatt.CondizioniPagamento (Codice),
        CONSTRAINT FK_CodiciPagamento_Modalita   FOREIGN KEY (ModalitaPagamento)   REFERENCES fatt.ModalitaPagamento (Codice),
        CONSTRAINT CK_CodiciPagamento_NumScadenze CHECK (NumScadenze BETWEEN 1 AND 3),
        -- I giorni aggiuntivi presuppongono il fine mese (cap. 4).
        CONSTRAINT CK_CodiciPagamento_GGpiu       CHECK (GGpiu IS NULL OR FineMese = 1)
    );
END
GO

-- Descrizione del codice univoca tra gli attivi (chiave umana, combo).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CodiciPagamento_Descr' AND object_id = OBJECT_ID(N'fatt.CodiciPagamento'))
BEGIN
    CREATE UNIQUE INDEX UX_CodiciPagamento_Descr ON fatt.CodiciPagamento (DescrPag) WHERE IsAttivo = 1;
END
GO

-- ---------------------------------------------------------------------------
-- Seed di partenza (idempotente per Id). Riferiti ai tipi seedati in mig. 021:
--   Bonifico = '7a...001', Ricevute bancarie = '7a...002'.
--   * A VISTA              → Bonifico, 1 rata, 0 giorni, no fine mese.
--   * BONIFICO 60 GG F.M.  → Bonifico, 1 rata, 60 giorni, fine mese.
--   * RIBA 30/60/90 F.M.   → Ricevute bancarie, 3 rate, 30/60/90, fine mese.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM fatt.CodiciPagamento WHERE IdCodicePagamento = N'7b000000-0000-0000-0000-000000000001')
    INSERT INTO fatt.CodiciPagamento (IdCodicePagamento, IdTipoPagamento, DescrPag, NumScadenze, GGScad1, GGScad2, GGScad3, GGpiu, FineMese)
    VALUES (N'7b000000-0000-0000-0000-000000000001', N'7a000000-0000-0000-0000-000000000001', N'A VISTA', 1, 0, NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM fatt.CodiciPagamento WHERE IdCodicePagamento = N'7b000000-0000-0000-0000-000000000002')
    INSERT INTO fatt.CodiciPagamento (IdCodicePagamento, IdTipoPagamento, DescrPag, NumScadenze, GGScad1, GGScad2, GGScad3, GGpiu, FineMese)
    VALUES (N'7b000000-0000-0000-0000-000000000002', N'7a000000-0000-0000-0000-000000000001', N'BONIFICO 60 GG F.M.', 1, 60, NULL, NULL, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM fatt.CodiciPagamento WHERE IdCodicePagamento = N'7b000000-0000-0000-0000-000000000003')
    INSERT INTO fatt.CodiciPagamento (IdCodicePagamento, IdTipoPagamento, DescrPag, NumScadenze, GGScad1, GGScad2, GGScad3, GGpiu, FineMese)
    VALUES (N'7b000000-0000-0000-0000-000000000003', N'7a000000-0000-0000-0000-000000000002', N'RIBA 30/60/90 F.M.', 3, 30, 60, 90, NULL, 1);
GO

-- ---------------------------------------------------------------------------
-- Abilitazione voce di menu "Codici pagamento" (finora Attivo = 0).
-- ---------------------------------------------------------------------------
UPDATE fatt.SottoMenu SET Attivo = 1 WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000005';
GO
