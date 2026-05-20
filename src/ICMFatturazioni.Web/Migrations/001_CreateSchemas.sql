-- =============================================================================
-- Migration 001 — Creazione degli schemi applicativi
-- =============================================================================
-- Scopo
--   Predispone i tre schemi SQL Server su cui poggiano tutte le tabelle
--   dell'applicazione, in coerenza con la convenzione di porting D2:
--
--     sta.*  tabelle di stato/lookup (Paesi, Province, NatureIVA, ...)
--     ana.*  anagrafiche (clienti, fornitori, ...)
--     fat.*  fatturazione (testate, righe, scadenze, ...)
--
--   Le tabelle trasversali (Utenti, LogErrors) rimangono nello schema dbo
--   per riflettere la loro natura infrastrutturale.
--
-- Idempotenza
--   I tre CREATE SCHEMA sono protetti da IF NOT EXISTS sulla vista
--   sys.schemas: rieseguire la migration su un database già aggiornato
--   non produce errori né effetti collaterali.
--
-- Rollback
--   DROP SCHEMA sta;  DROP SCHEMA ana;  DROP SCHEMA fat;
--   (eseguire solo dopo aver rimosso tutti gli oggetti contenuti)
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Schema sta -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'sta')
BEGIN
    EXEC(N'CREATE SCHEMA [sta] AUTHORIZATION [dbo];');
END
GO

-- Schema ana -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'ana')
BEGIN
    EXEC(N'CREATE SCHEMA [ana] AUTHORIZATION [dbo];');
END
GO

-- Schema fat -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'fat')
BEGIN
    EXEC(N'CREATE SCHEMA [fat] AUTHORIZATION [dbo];');
END
GO
