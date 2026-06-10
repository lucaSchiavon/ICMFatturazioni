-- =============================================================================
-- Migration 012 — Menu admin-only + rinomina "Permessi" → "Permessi ruolo"
-- =============================================================================
-- Scopo
--   1) Aggiunge fatt.Menu.SoloAdmin: marca i gruppi accessibili SOLO ad
--      Admin/Superadmin (es. "Amministrazione"). Questi NON sono configurabili
--      nelle pagine dei permessi (un ruolo/utente custom non può riceverne
--      l'accesso) e i non-admin non li vedono mai.
--   2) Rinomina la sottovoce "Permessi" in "Permessi ruolo" (è la pagina di
--      configurazione permessi PER RUOLO, da distinguere da "Permessi utente").
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- 1) Colonna SoloAdmin (idempotente).
IF COL_LENGTH(N'fatt.Menu', N'SoloAdmin') IS NULL
    ALTER TABLE fatt.Menu ADD SoloAdmin BIT NOT NULL CONSTRAINT DF_Menu_SoloAdmin DEFAULT (0);
GO

-- Il gruppo "Amministrazione" è admin-only.
UPDATE fatt.Menu SET SoloAdmin = 1 WHERE IdMenu = N'b0000000-0000-0000-0000-000000000004';
GO

-- 2) Rinomina la voce "Permessi" → "Permessi ruolo" (pagina PermessiMenu).
UPDATE fatt.SottoMenu SET Descrizione = N'Permessi ruolo'
WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000023';
GO
