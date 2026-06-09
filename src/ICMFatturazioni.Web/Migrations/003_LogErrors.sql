-- =============================================================================
-- Migration 003 — Tabella fatt.LogErrors (Regola 6 di CLAUDE.md)
-- =============================================================================
-- Scopo
--   Persistere ogni eccezione di runtime sollevata dall'app (UI Blazor,
--   Manager, Repository, middleware, BackgroundService). Lo schema della
--   tabella è il contratto tra l'app e il pannello /admin/log-errors,
--   quindi va trattato come API: aggiungere campi è additivo, rimuoverli
--   non lo è e richiede una nuova migration.
--
-- Severity
--   TINYINT in mappatura con l'enum applicativo Severity:
--     0 = Info, 1 = Warning, 2 = Error, 3 = Critical.
--   Default 2 = Error (caso più frequente).
--
-- Handled
--   BIT (default 0). Vale 1 quando l'eccezione è stata catturata e gestita
--   senza rilancio (es. fallback su valore safe nel Manager). Aiuta a
--   distinguere bug subdoli (eccezione silenziata) da crash veri.
--
-- Indici
--   * IX_LogErrors_TimestampUtc DESC: per la query più frequente
--     "ultimi N errori".
--   * IX_LogErrors_ExceptionType: per il filtro per tipo nella pagina
--     admin e per le query di alerting che contano per tipologia.
--
-- Anti-mascheramento
--   La tabella esiste, ma il fallback testuale su file (regola CLAUDE.md)
--   resta abilitato per coprire i casi in cui INSERT su LogErrors fallisce
--   (DB irraggiungibile, deadlock). La policy è codice C# nel logger, non
--   schema.
--
-- Rollback
--   DROP TABLE fatt.LogErrors;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'fatt.LogErrors', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.LogErrors
    (
        Id                          BIGINT          IDENTITY(1,1) NOT NULL,
        TimestampUtc                DATETIME2(3)    NOT NULL CONSTRAINT DF_LogErrors_TimestampUtc DEFAULT (SYSUTCDATETIME()),
        ExceptionType               NVARCHAR(512)   NOT NULL,
        [Message]                   NVARCHAR(MAX)   NOT NULL,
        StackTrace                  NVARCHAR(MAX)   NULL,
        InnerExceptionType          NVARCHAR(512)   NULL,
        InnerExceptionMessage       NVARCHAR(MAX)   NULL,
        InnerExceptionStackTrace    NVARCHAR(MAX)   NULL,
        [Source]                    NVARCHAR(512)   NULL,
        DescrizioneEstesa           NVARCHAR(MAX)   NULL,
        Contesto                    NVARCHAR(512)   NULL,
        UserId                      INT             NULL,
        UserName                    NVARCHAR(256)   NULL,
        RequestPath                 NVARCHAR(2048)  NULL,
        MachineName                 NVARCHAR(256)   NULL,
        EnvironmentName             NVARCHAR(64)    NULL,
        CorrelationId               NVARCHAR(64)    NULL,
        Severity                    TINYINT         NOT NULL CONSTRAINT DF_LogErrors_Severity DEFAULT (2),
        Handled                     BIT             NOT NULL CONSTRAINT DF_LogErrors_Handled  DEFAULT (0),

        CONSTRAINT PK_LogErrors PRIMARY KEY CLUSTERED (Id)
    );

    CREATE INDEX IX_LogErrors_TimestampUtc  ON fatt.LogErrors (TimestampUtc DESC);
    CREATE INDEX IX_LogErrors_ExceptionType ON fatt.LogErrors (ExceptionType);
END
GO
