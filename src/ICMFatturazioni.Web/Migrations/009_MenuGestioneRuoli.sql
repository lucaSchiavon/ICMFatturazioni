-- =============================================================================
-- Migration 009 — Voce di menu "Gestione ruoli" (T3b)
-- =============================================================================
-- Aggiunge la sottovoce "Gestione ruoli" (pagina GestioneRuoli) sotto il
-- gruppo "Amministrazione" già creato dalla migration 008.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000022')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (N'5b000000-0000-0000-0000-000000000022', N'b0000000-0000-0000-0000-000000000004',
            N'Gestione ruoli', N'GestioneRuoli', N'Badge', 20, 1);
GO
