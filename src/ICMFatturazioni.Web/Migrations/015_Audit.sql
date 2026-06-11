-- =============================================================================
-- Migration 015 — fatt.Audit (tracciamento "chi-ha-fatto-cosa", mirror Verbali)
-- =============================================================================
-- Scopo
--   Tabella GENERICA di audit delle operazioni CRUD sui dati master (Utenti,
--   Anagrafiche, Cataloghi, token di accesso). Mirror di dbo.Audit di
--   ICMVerbali, sotto schema applicativo fatt (ADR D21). IMMUTABILE: solo
--   INSERT.
--
--   Nessuna FK: i dati referenziati possono essere eliminati (es. utente
--   cancellato), ma la riga di audit deve restare leggibile a fini storici.
--   Per questo UtenteNome è uno SNAPSHOT del nome al momento dell'azione: resta
--   consultabile anche se l'utente viene rinominato o rimosso.
--
-- Operazione (TINYINT): 0 = Creazione, 1 = Modifica, 2 = Eliminazione.
--
-- Rollback
--   DROP TABLE fatt.Audit;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'fatt.Audit', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Audit
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Audit PRIMARY KEY,
        TimestampUtc DATETIME2(3)     NOT NULL CONSTRAINT DF_Audit_TimestampUtc DEFAULT (SYSUTCDATETIME()),
        -- Chi: id dell'utente + snapshot del nome (entrambi NULL se l'azione
        -- avviene fuori da un contesto autenticato, es. seed o flusso anonimo).
        UtenteId     UNIQUEIDENTIFIER NULL,
        UtenteNome   NVARCHAR(256)    NULL,
        -- Cosa: tipo di operazione + tipo/id dell'entità + descrizione leggibile.
        Operazione   TINYINT          NOT NULL,
        EntityType   NVARCHAR(128)    NOT NULL,
        EntityId     UNIQUEIDENTIFIER NULL,
        Descrizione  NVARCHAR(512)    NULL
    );

    CREATE INDEX IX_Audit_TimestampUtc ON fatt.Audit (TimestampUtc DESC);
    CREATE INDEX IX_Audit_EntityType   ON fatt.Audit (EntityType);
    CREATE INDEX IX_Audit_UtenteId     ON fatt.Audit (UtenteId) WHERE UtenteId IS NOT NULL;
END
GO
