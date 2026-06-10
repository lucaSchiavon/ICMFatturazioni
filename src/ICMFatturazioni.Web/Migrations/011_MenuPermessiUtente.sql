-- =============================================================================
-- Migration 011 — Voce di menu "Permessi utente" (T3d, override per-utente)
-- =============================================================================
-- Aggiunge la sottovoce "Permessi utente" (pagina PermessiUtente) sotto il
-- gruppo "Amministrazione": consente di configurare i permessi del SINGOLO
-- utente, che SOSTITUISCONO quelli del suo ruolo quando presenti.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000024')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (N'5b000000-0000-0000-0000-000000000024', N'b0000000-0000-0000-0000-000000000004',
            N'Permessi utente', N'PermessiUtente', N'ManageAccounts', 40, 1);
GO
