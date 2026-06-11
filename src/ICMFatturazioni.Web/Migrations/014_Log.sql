-- =============================================================================
-- Migration 014 — fatt.Log (logging errori persistente, mirror di ICMVerbali)
-- =============================================================================
-- Scopo
--   Step "mirror logging+audit": sostituisce il vecchio fatt.LogErrors
--   (BIGINT IDENTITY, scrittura sincrona via IErrorLogger) con fatt.Log in
--   stile ICMVerbali (GUID v7, scrittura asincrona disaccoppiata via coda +
--   BackgroundService, cattura automatica dei Warning+ del framework tramite
--   DbLoggerProvider). Tabella IMMUTABILE by design: solo INSERT, mai UPDATE.
--
--   Differenze rispetto al pari-pari di Verbali (scelta utente, Regola 6 di
--   CLAUDE.md): la sanitizzazione dei segreti e il fallback su file restano
--   responsabilità del layer applicativo (LogSanitizer / LogFallbackWriter),
--   non dello schema. La tabella è identica a dbo.Log di Verbali ma sotto lo
--   schema applicativo fatt (ADR D21).
--
-- Livello (TINYINT) — allineato a Microsoft.Extensions.Logging.LogLevel:
--   3 = Warning, 4 = Error, 5 = Critical. Persistiamo solo Warning+.
--
-- Rollback
--   DROP TABLE fatt.Log;
--   (il vecchio fatt.LogErrors NON viene ricreato: era già a DB vuoto in dev)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Rimozione del vecchio canale di logging (sostituito da fatt.Log). In dev la
-- tabella nasce vuota: nessun dato storico da preservare.
IF OBJECT_ID(N'fatt.LogErrors', N'U') IS NOT NULL
    DROP TABLE fatt.LogErrors;
GO

IF OBJECT_ID(N'fatt.Log', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Log
    (
        Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Log PRIMARY KEY,
        TimestampUtc      DATETIME2(3)     NOT NULL CONSTRAINT DF_Log_TimestampUtc DEFAULT (SYSUTCDATETIME()),
        -- 3 = Warning, 4 = Error, 5 = Critical (cast diretto da LogLevel).
        Livello           TINYINT          NOT NULL,
        -- Categoria del logger (es. "ICMFatturazioni.Web.Managers.UtenteManager")
        -- oppure la sorgente esplicita passata a LogErroreAsync.
        Sorgente          NVARCHAR(256)    NOT NULL,
        Messaggio         NVARCHAR(MAX)    NOT NULL,
        EccezioneTipo     NVARCHAR(512)    NULL,
        StackTrace        NVARCHAR(MAX)    NULL,
        -- Spiegazione user-friendly: valorizzata SOLO dal path esplicito
        -- ILogManager.LogErroreAsync, mai dalla rete automatica del provider.
        SpiegazioneUtente NVARCHAR(MAX)    NULL,
        -- Correlazione con la request/attività (Activity.Current?.Id).
        RequestId         NVARCHAR(128)    NULL,
        UtenteId          UNIQUEIDENTIFIER NULL,
        -- Entità di dominio coinvolta (facoltativa), per diagnostica mirata.
        EntityId          UNIQUEIDENTIFIER NULL,
        EntityType        NVARCHAR(128)    NULL
    );

    -- "Ultimi N errori" è la query diagnostica più frequente.
    CREATE INDEX IX_Log_TimestampUtc ON fatt.Log (TimestampUtc DESC);
    CREATE INDEX IX_Log_Livello      ON fatt.Log (Livello);
    -- Filtrato: indicizza solo le righe legate a un'entità (la maggioranza è NULL).
    CREATE INDEX IX_Log_EntityId     ON fatt.Log (EntityId) WHERE EntityId IS NOT NULL;
END
GO
