-- =============================================================================
-- Migration 010 — Voce di menu "Permessi" (matrice ruolo×menu) (T3c)
-- =============================================================================
-- Aggiunge la sottovoce "Permessi" (pagina PermessiMenu) sotto il gruppo
-- "Amministrazione": consente di configurare quali menu/sottomenu vede
-- ciascun ruolo custom.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000023')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (N'5b000000-0000-0000-0000-000000000023', N'b0000000-0000-0000-0000-000000000004',
            N'Permessi', N'PermessiMenu', N'Tune', 30, 1);
GO
