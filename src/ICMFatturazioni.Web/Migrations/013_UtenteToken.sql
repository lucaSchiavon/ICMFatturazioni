-- =============================================================================
-- Migration 013 — fatt.UtenteToken (magic-link attivazione / reset password)
-- =============================================================================
-- Scopo
--   Tappa T4 (mirror di ICMVerbali, migration 010): tabella dei token monouso
--   per l'INVITO utente (primo accesso, imposta password) e il RESET password.
--   Il token in CHIARO non è mai salvato: si memorizza solo il suo hash
--   SHA-256 (binary(32)). L'URL inviato per email contiene il token in chiaro;
--   il server ne ricalcola l'hash e lo cerca per match. Un leak della tabella
--   è quindi inutile (nessun link riutilizzabile).
--
-- Stato derivato (pattern CLAUDE.md "stato logico da colonne nullable")
--   Token "Attivo" = UsatoUtc IS NULL AND RevocatoUtc IS NULL
--                    AND ScadenzaUtc > SYSUTCDATETIME().
--   UsatoUtc   → consumato (uso singolo).
--   RevocatoUtc→ sostituito da un reinvio più recente.
--
-- FK ON DELETE CASCADE
--   Se l'utente viene eliminato, i suoi token spariscono con lui.
--
-- Rollback
--   DROP TABLE fatt.UtenteToken;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'fatt.UtenteToken', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.UtenteToken
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UtenteToken PRIMARY KEY,
        UtenteId     UNIQUEIDENTIFIER NOT NULL,
        -- SHA-256 del token in chiaro (32 byte). Mai il token stesso.
        TokenHash    BINARY(32)       NOT NULL,
        -- 0 = Attivazione (primo accesso), 1 = Reset password.
        Tipo         TINYINT          NOT NULL,
        ScadenzaUtc  DATETIME2(0)     NOT NULL,
        UsatoUtc     DATETIME2(0)     NULL,           -- consumato (uso singolo)
        RevocatoUtc  DATETIME2(0)     NULL,           -- sostituito da un reinvio
        CreatedAt    DATETIME2(3)     NOT NULL CONSTRAINT DF_UtenteToken_CreatedAt DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT FK_UtenteToken_Utenti
            FOREIGN KEY (UtenteId) REFERENCES fatt.Utenti (IdUtente) ON DELETE CASCADE,
        -- L'hash è univoco: la generazione è 256 bit casuali, collisione trascurabile.
        CONSTRAINT UQ_UtenteToken_TokenHash UNIQUE (TokenHash)
    );

    -- Lookup frequente "ultimi token dell'utente per tipo" (revoca/diagnostica).
    CREATE INDEX IX_UtenteToken_UtenteId_Tipo ON fatt.UtenteToken (UtenteId, Tipo);
END
GO
