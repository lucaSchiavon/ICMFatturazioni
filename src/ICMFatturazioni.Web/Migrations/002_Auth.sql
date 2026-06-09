-- =============================================================================
-- Migration 002 — Tabella fatt.Utenti per autenticazione cookie
-- =============================================================================
-- Scopo
--   Crea la tabella che custodisce le credenziali applicative.
--   La scelta dell'algoritmo di hashing (ADR D4) è PBKDF2 via
--   Microsoft.AspNetCore.Cryptography.KeyDerivation:
--     - 100.000 iterazioni HMAC-SHA256
--     - salt da 16 byte casuali per utente
--     - derived key da 32 byte
--   Salt e hash sono memorizzati in due colonne distinte come byte sequence:
--   non concateniamo nulla in stringa per evitare ambiguità di parsing nel
--   verify, ed è esplicitato chi è chi a livello di schema.
--
-- Note sulle scelte di colonna
--   * Username unique e case-insensitive: il collation della colonna è
--     SQL_Latin1_General_CP1_CI_AS (default del DB) — l'indice unique
--     blocca duplicati indipendentemente dalla case.
--   * TemaPreferito NVARCHAR(8) NOT NULL DEFAULT 'light' (ADR D17, D18):
--     valori ammessi {'light','dark','auto'}, vincolati da CHECK.
--   * Attivo BIT NOT NULL DEFAULT 1: gli utenti revocati restano in tabella
--     per audit (FK ancora valide), ma vengono filtrati al login.
--   * DataRecord come audit timestamp di creazione/modifica riga: in linea
--     con la decisione D9 (audit caso per caso) — qui ha senso perché la
--     tabella è governata da operazioni manuali di amministratore.
--
-- Rollback
--   DROP TABLE fatt.Utenti;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'fatt.Utenti', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Utenti
    (
        IdUtente         INT             IDENTITY(1,1) NOT NULL,
        Username         NVARCHAR(64)    NOT NULL,
        PasswordHash     VARBINARY(64)   NOT NULL,
        PasswordSalt     VARBINARY(16)   NOT NULL,
        NomeCompleto     NVARCHAR(128)   NULL,
        Email            NVARCHAR(256)   NULL,
        Attivo           BIT             NOT NULL CONSTRAINT DF_Utenti_Attivo         DEFAULT (1),
        TemaPreferito    NVARCHAR(8)     NOT NULL CONSTRAINT DF_Utenti_TemaPreferito  DEFAULT (N'light'),
        DataRecord       DATETIME2(3)    NOT NULL CONSTRAINT DF_Utenti_DataRecord     DEFAULT (SYSUTCDATETIME()),
        UltimoLoginUtc   DATETIME2(3)    NULL,

        CONSTRAINT PK_Utenti PRIMARY KEY CLUSTERED (IdUtente),
        CONSTRAINT CK_Utenti_TemaPreferito CHECK (TemaPreferito IN (N'light', N'dark', N'auto'))
    );

    -- Indice unique sullo Username: il login lo usa come chiave logica.
    CREATE UNIQUE INDEX UX_Utenti_Username ON fatt.Utenti (Username);
END
GO
