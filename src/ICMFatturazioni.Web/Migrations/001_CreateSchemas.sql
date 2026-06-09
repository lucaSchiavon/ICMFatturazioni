-- =============================================================================
-- Migration 001 — Creazione dello schema applicativo
-- =============================================================================
-- Scopo
--   Predispone l'UNICO schema SQL Server su cui poggiano TUTTE le tabelle
--   di ICMFatturazioni:
--
--     fatt.*   namespace applicativo dell'intero gestionale di fatturazione
--              (anagrafiche, lookup di stato, codici IVA/pagamento, banche,
--              attività, utenti, log errori, ...).
--
-- Perché uno schema unico (supera la vecchia convenzione sta/ana/fat — ADR D2)
--   Le due applicazioni ICMVerbali e ICMFatturazioni convergeranno su un
--   UNICO database condiviso (alcune entità si fonderanno: Anagrafica↔Committente,
--   Attivita↔Progetto). In quel DB le tabelle di Verbali vivono sotto `dbo.*`.
--   Mettendo TUTTE le tabelle di Fatturazioni sotto `fatt.*` si ottiene:
--     * ownership esplicita ("queste sono di ICMFatturazioni");
--     * zero collisioni con `dbo.*` di Verbali (incluse Utenti/LogErrors, che
--       altrimenti si mescolerebbero nel calderone dbo);
--     * fusione futura localizzata a poche tabelle, non un riordino globale.
--   La motivazione estesa è in docs/decisioni-architetturali.md.
--
-- Idempotenza
--   Il CREATE SCHEMA è protetto da IF NOT EXISTS sulla vista sys.schemas:
--   rieseguire la migration su un database già aggiornato non produce errori.
--
-- Rollback
--   DROP SCHEMA fatt;  (eseguire solo dopo aver rimosso tutti gli oggetti)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Schema fatt ----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'fatt')
BEGIN
    EXEC(N'CREATE SCHEMA [fatt] AUTHORIZATION [dbo];');
END
GO
