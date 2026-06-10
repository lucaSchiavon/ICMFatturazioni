-- =============================================================================
-- Migration 006 — Ruoli (tabella) + ricostruzione fatt.Utenti a GUID
-- =============================================================================
-- Scopo
--   Introduce l'autorizzazione DB-driven (vedi memoria 'menu-ruoli-dinamici'):
--     * fatt.Ruoli: i ruoli diventano RIGHE, non più un enum C#. L'admin ne
--       crea/configura di nuovi a runtime. 'Superadmin' e 'Admin' sono ruoli
--       di SISTEMA (IsSistema=1): seedati, non eliminabili né rinominabili.
--     * fatt.Utenti viene RICOSTRUITA: PK uniqueidentifier (GUID v7 app-side,
--       ADR D22) al posto di INT IDENTITY, FK IdRuolo, PasswordHash come
--       singola colonna nvarchar (formato PBKDF2 v3 di PasswordHasher<T>, salt
--       incluso → niente più colonna PasswordSalt separata), audit
--       CreatedAt/UpdatedAt al posto di DataRecord. Mantiene TemaPreferito.
--
-- Perché si DROPpa e ricrea fatt.Utenti
--   La migration 002 (immutabile) crea fatt.Utenti in versione INT. Qui la
--   convertiamo: il cambio di PK INT→GUID non è un ALTER praticabile. Non
--   esistono ancora dati di produzione, quindi il drop è sicuro. La guardia
--   "se manca la colonna IdRuolo" rende l'operazione idempotente: una volta
--   ricostruita la tabella, rieseguire la migration non la ridistrugge.
--
-- PasswordHash NULLABLE
--   Predisposizione al flusso "invito" (Tappa T4): un utente creato dall'admin
--   nasce SENZA password (PasswordHash NULL = "da attivare") e la imposta lui
--   tramite link. Fino ad allora il login fallisce (check applicativo).
--
-- Rollback
--   DROP TABLE fatt.Utenti; DROP TABLE fatt.Ruoli;
--   (poi rieseguire la 002 per tornare alla vecchia fatt.Utenti INT)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------------------
-- 1) fatt.Ruoli
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.Ruoli', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Ruoli
    (
        IdRuolo      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Ruoli PRIMARY KEY,
        -- Codice stabile per i ruoli di sistema ('SUPERADMIN','ADMIN'); NULL
        -- per i ruoli custom. Il codice — non il nome visualizzato — è ciò che
        -- il codice applicativo usa per riconoscere i ruoli fissi.
        Codice       NVARCHAR(20)     NULL,
        Ruolo        NVARCHAR(50)     NOT NULL,      -- nome visualizzato/modificabile (custom)
        Descrizione  NVARCHAR(200)    NULL,
        -- IsSistema=1: ruolo non eliminabile né rinominabile dalla UI.
        IsSistema    BIT              NOT NULL CONSTRAINT DF_Ruoli_IsSistema DEFAULT (0),
        IsAttivo     BIT              NOT NULL CONSTRAINT DF_Ruoli_IsAttivo  DEFAULT (1),
        CreatedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_Ruoli_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_Ruoli_UpdatedAt DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT UQ_Ruoli_Ruolo UNIQUE (Ruolo)
    );

    -- Un solo ruolo per ciascun codice di sistema (i custom hanno Codice NULL).
    CREATE UNIQUE INDEX UX_Ruoli_Codice ON fatt.Ruoli (Codice) WHERE Codice IS NOT NULL;
END
GO

-- Seed ruoli (idempotente su Codice per i fissi, su Ruolo per Operatore).
-- GUID fissi e leggibili per i ruoli di sistema: sono righe "ben note",
-- l'ordinamento temporale di UUIDv7 qui è irrilevante.
IF NOT EXISTS (SELECT 1 FROM fatt.Ruoli WHERE Codice = N'SUPERADMIN')
    INSERT INTO fatt.Ruoli (IdRuolo, Codice, Ruolo, Descrizione, IsSistema)
    VALUES (N'5a5a5a5a-0000-0000-0000-000000000001', N'SUPERADMIN', N'Superadmin',
            N'Account di servizio: vede tutto, incluso il log errori. Non configurabile.', 1);

IF NOT EXISTS (SELECT 1 FROM fatt.Ruoli WHERE Codice = N'ADMIN')
    INSERT INTO fatt.Ruoli (IdRuolo, Codice, Ruolo, Descrizione, IsSistema)
    VALUES (N'ad000000-0000-0000-0000-000000000001', N'ADMIN', N'Admin',
            N'Amministratore: accesso a tutte le funzionalità tranne il log errori. Non configurabile.', 1);

IF NOT EXISTS (SELECT 1 FROM fatt.Ruoli WHERE Ruolo = N'Operatore')
    INSERT INTO fatt.Ruoli (IdRuolo, Codice, Ruolo, Descrizione, IsSistema)
    VALUES (N'09e40000-0000-0000-0000-000000000001', NULL, N'Operatore',
            N'Ruolo operativo configurabile: vede solo i menu assegnati.', 0);
GO

-- ---------------------------------------------------------------------------
-- 2) fatt.Utenti — ricostruzione a GUID + FK ruolo
-- ---------------------------------------------------------------------------
-- Idempotenza: ricostruiamo solo se la tabella non ha ancora la colonna
-- IdRuolo (cioè è la vecchia versione INT della migration 002, o assente).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'fatt.Utenti') AND name = N'IdRuolo'
)
BEGIN
    IF OBJECT_ID(N'fatt.Utenti', N'U') IS NOT NULL
        DROP TABLE fatt.Utenti;

    CREATE TABLE fatt.Utenti
    (
        IdUtente        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Utenti PRIMARY KEY,
        Username        NVARCHAR(80)     NOT NULL,
        Email           NVARCHAR(256)    NULL,
        -- NULL = utente invitato non ancora attivato (T4). Altrimenti hash
        -- PBKDF2-SHA256 in formato v3 di PasswordHasher<T> (~130 char, salt incluso).
        PasswordHash    NVARCHAR(200)    NULL,
        IdRuolo         UNIQUEIDENTIFIER NOT NULL,
        NomeCompleto    NVARCHAR(128)    NULL,
        Attivo          BIT              NOT NULL CONSTRAINT DF_Utenti_Attivo        DEFAULT (1),
        TemaPreferito   NVARCHAR(8)      NOT NULL CONSTRAINT DF_Utenti_TemaPreferito DEFAULT (N'light'),
        UltimoLoginUtc  DATETIME2(3)     NULL,
        CreatedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_Utenti_CreatedAt     DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(3)     NOT NULL CONSTRAINT DF_Utenti_UpdatedAt     DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT UQ_Utenti_Username UNIQUE (Username),
        CONSTRAINT FK_Utenti_Ruoli FOREIGN KEY (IdRuolo) REFERENCES fatt.Ruoli (IdRuolo),
        CONSTRAINT CK_Utenti_TemaPreferito CHECK (TemaPreferito IN (N'light', N'dark', N'auto'))
    );

    -- Email unique solo quando valorizzata (indice filtrato): serve per il
    -- reset password via email (T4); più utenti possono non avere email.
    CREATE UNIQUE INDEX UX_Utenti_Email ON fatt.Utenti (Email) WHERE Email IS NOT NULL;
END
GO
