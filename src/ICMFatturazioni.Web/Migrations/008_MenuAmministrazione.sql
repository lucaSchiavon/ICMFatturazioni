-- =============================================================================
-- Migration 008 — Voce di menu "Amministrazione" + "Gestione utenti"
-- =============================================================================
-- Scopo
--   Aggiunge al menu dinamico il gruppo "Amministrazione" con la sottovoce
--   "Gestione utenti" (pagina GestioneUtenti, T3a). Le altre voci admin
--   (Gestione ruoli, Permessi) verranno aggiunte quando le relative pagine
--   saranno implementate (T3b/T3c).
--
--   Admin e Superadmin vedono questa voce per via del bypass in codice
--   (nessun mapping necessario). Gli altri ruoli NON la vedono e la guardia
--   di rotta nega l'accesso diretto via URL.
--
-- Rollback
--   DELETE FROM fatt.SottoMenu WHERE IdMenu = '<Amministrazione>';
--   DELETE FROM fatt.Menu WHERE IdMenu = '<Amministrazione>';
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- Gruppo Amministrazione (Menu = NULL → contenitore).
IF NOT EXISTS (SELECT 1 FROM fatt.Menu WHERE IdMenu = N'b0000000-0000-0000-0000-000000000004')
    INSERT INTO fatt.Menu (IdMenu, DescrizioneMenu, Menu, Icona, Ordine, Attivo)
    VALUES (N'b0000000-0000-0000-0000-000000000004', N'Amministrazione', NULL, N'AdminPanelSettings', 90, 1);

-- Sottovoce Gestione utenti → pagina Razor "GestioneUtenti".
IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000021')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (N'5b000000-0000-0000-0000-000000000021', N'b0000000-0000-0000-0000-000000000004',
            N'Gestione utenti', N'GestioneUtenti', N'ManageAccounts', 10, 1);
GO
