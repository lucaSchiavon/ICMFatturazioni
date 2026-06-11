-- =============================================================================
-- Migration 017 — fatt.Audit.Dati (dettaglio strutturato dell'operazione)
-- =============================================================================
-- Scopo
--   Aggiunge la colonna Dati: contiene, in JSON, COSA è stato scritto.
--     - Inserimento  → snapshot completo dei campi del nuovo record.
--     - Modifica     → diff dei soli campi cambiati ({ campo: { prima, dopo } }).
--     - Eliminazione → snapshot del record eliminato.
--   I campi sensibili (PasswordHash, Salt, TokenHash) sono esclusi a monte
--   (Regola 6): la colonna non deve mai contenere segreti.
--
--   La colonna Descrizione resta come etichetta breve per la griglia; il JSON
--   completo si consulta nel dettaglio espandibile della pagina Audit.
--
-- Rollback
--   ALTER TABLE fatt.Audit DROP COLUMN Dati;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'fatt.Audit', N'Dati') IS NULL
    ALTER TABLE fatt.Audit ADD Dati NVARCHAR(MAX) NULL;
GO
