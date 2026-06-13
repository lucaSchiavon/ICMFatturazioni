-- =============================================================================
-- Migration 020 — Anagrafiche bancarie (fatt.Banche, fatt.Agenzie) +
--                 fatt.BancheAppoggio + abilitazione voce di menu
-- =============================================================================
-- Scopo
--   Catalogo delle banche di appoggio usate in fattura, in un modello
--   NORMALIZZATO (decisione utente 2026-06-13, dopo aver riscontrato i doppioni
--   del modello a testo libero):
--
--     fatt.Banche   — l'ISTITUTO. Nome univoco, un ABI per banca.
--     fatt.Agenzie  — la FILIALE. Appartiene a una banca; Nome+CAB per agenzia.
--     fatt.BancheAppoggio — il LEGAME: associa una banca/agenzia a una banca
--                    dell'Azienda (IdCliente NULL) o di un cliente (IdCliente
--                    valorizzato), con l'eventuale IBAN (solo azienda).
--
--   Semantica bancaria (maschera Access, Evidenze/AppoggiBancariAzienda.png):
--   l'ABI identifica la BANCA (istituto, 5 cifre), il CAB la FILIALE (5 cifre).
--   Normalizzando, un'agenzia ha UN solo CAB: modificarlo aggiorna l'agenzia
--   ovunque, senza creare doppioni con CAB divergenti.
--
--   Banche e Agenzie sono CONDIVISE tra appoggi azienda e cliente: "Unicredit"
--   è un solo istituto, la sua filiale "Piazza Erbe" un solo sportello, a
--   prescindere da chi vi si appoggia.
--
--   Discriminante azienda/cliente su fatt.BancheAppoggio:
--     * IdCliente IS NULL      → banca dell'AZIENDA (+ IBAN per i bonifici).
--     * IdCliente valorizzato  → banca DI QUEL CLIENTE (per le ricevute banc.).
--
-- Convenzioni (ADR D21/D22): schema unico `fatt`, PK UNIQUEIDENTIFIER (GUID
--   UUIDv7 app-side), soft-delete IsAttivo. Nomi/colonne fedeli al legacy IT.
--
-- Rollback
--   DROP TABLE fatt.BancheAppoggio; DROP TABLE fatt.Agenzie; DROP TABLE fatt.Banche;
--   (e, per il menu, UPDATE fatt.SottoMenu SET Attivo = 0 WHERE IdSottoMenu = '5b000000-0000-0000-0000-000000000003';)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Necessario per le CREATE INDEX filtrate (WHERE ...).
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- fatt.Banche — istituti bancari (Nome univoco, un ABI per banca)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.Banche', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Banche
    (
        IdBanca  UNIQUEIDENTIFIER NOT NULL,
        Nome     NVARCHAR(50)     NOT NULL,
        -- ABI: codice istituto (5 cifre). Nullable: può essere ignoto in inserimento.
        ABI      NVARCHAR(5)      NULL,
        IsAttivo BIT              NOT NULL CONSTRAINT DF_Banche_IsAttivo DEFAULT (1),

        CONSTRAINT PK_Banche PRIMARY KEY CLUSTERED (IdBanca)
    );
END
GO

-- Nome banca univoco tra gli attivi (è la "chiave umana" usata dalla combo).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Banche_Nome' AND object_id = OBJECT_ID(N'fatt.Banche'))
BEGIN
    CREATE UNIQUE INDEX UX_Banche_Nome ON fatt.Banche (Nome) WHERE IsAttivo = 1;
END
GO

-- ---------------------------------------------------------------------------
-- fatt.Agenzie — filiali (appartengono a una banca; Nome+CAB per agenzia)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.Agenzie', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Agenzie
    (
        IdAgenzia UNIQUEIDENTIFIER NOT NULL,
        IdBanca   UNIQUEIDENTIFIER NOT NULL,
        Nome      NVARCHAR(50)     NOT NULL,
        -- CAB: codice filiale (5 cifre). Nullable.
        CAB       NVARCHAR(5)      NULL,
        IsAttivo  BIT              NOT NULL CONSTRAINT DF_Agenzie_IsAttivo DEFAULT (1),

        CONSTRAINT PK_Agenzie        PRIMARY KEY CLUSTERED (IdAgenzia),
        CONSTRAINT FK_Agenzie_Banca  FOREIGN KEY (IdBanca) REFERENCES fatt.Banche (IdBanca)
    );
END
GO

-- Nome agenzia univoco per banca tra gli attivi.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Agenzie_BancaNome' AND object_id = OBJECT_ID(N'fatt.Agenzie'))
BEGIN
    CREATE UNIQUE INDEX UX_Agenzie_BancaNome ON fatt.Agenzie (IdBanca, Nome) WHERE IsAttivo = 1;
END
GO

-- ---------------------------------------------------------------------------
-- fatt.BancheAppoggio — legame banca/agenzia ↔ azienda o cliente
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.BancheAppoggio', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.BancheAppoggio
    (
        IdBancaAppoggio UNIQUEIDENTIFIER NOT NULL,
        -- Discriminante: NULL = banca dell'Azienda; valorizzato = banca del cliente.
        IdCliente       UNIQUEIDENTIFIER NULL,
        -- Istituto (obbligatorio) e filiale (facoltativa).
        IdBanca         UNIQUEIDENTIFIER NOT NULL,
        IdAgenzia       UNIQUEIDENTIFIER NULL,
        -- IBAN: presente sulle sole banche azienda (conto per i bonifici).
        IBAN            NVARCHAR(27)     NULL,
        IsAttivo        BIT              NOT NULL CONSTRAINT DF_BancheAppoggio_IsAttivo DEFAULT (1),

        CONSTRAINT PK_BancheAppoggio          PRIMARY KEY CLUSTERED (IdBancaAppoggio),
        CONSTRAINT FK_BancheAppoggio_Cliente  FOREIGN KEY (IdCliente) REFERENCES fatt.Anagrafica (IdAnagrafica),
        CONSTRAINT FK_BancheAppoggio_Banca    FOREIGN KEY (IdBanca)   REFERENCES fatt.Banche (IdBanca),
        CONSTRAINT FK_BancheAppoggio_Agenzia  FOREIGN KEY (IdAgenzia) REFERENCES fatt.Agenzie (IdAgenzia)
    );
END
GO

-- Anti-duplicato: lo stesso intestatario (azienda o un cliente) non ha due
-- volte la stessa filiale. Filtrato sui soli attivi e sulle righe con agenzia.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BancheAppoggio_ClienteBancaAgenzia' AND object_id = OBJECT_ID(N'fatt.BancheAppoggio'))
BEGIN
    CREATE UNIQUE INDEX UX_BancheAppoggio_ClienteBancaAgenzia
        ON fatt.BancheAppoggio (IdCliente, IdBanca, IdAgenzia)
        WHERE IsAttivo = 1 AND IdAgenzia IS NOT NULL;
END
GO

-- ---------------------------------------------------------------------------
-- Abilitazione della voce di menu "Banche appoggio".
-- ---------------------------------------------------------------------------
UPDATE fatt.SottoMenu
SET Attivo = 1
WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000003';
GO
