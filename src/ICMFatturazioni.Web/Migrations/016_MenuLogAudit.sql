-- =============================================================================
-- Migration 016 — Menu: voci "Audit" e "Log errori" + flag SoloSuperadmin
-- =============================================================================
-- Scopo
--   Aggiunge al gruppo "Amministrazione" le due pagine di consultazione dello
--   step logging+audit:
--     - "Audit"      → pagina AuditPage (/admin/audit), accesso Admin+Superadmin
--                      (eredita la visibilità del gruppo SoloAdmin).
--     - "Log errori" → pagina LogPage  (/admin/log),   accesso SOLO Superadmin.
--
--   Per il "solo Superadmin" si introduce fatt.SottoMenu.SoloSuperadmin: una
--   sottovoce così marcata è invisibile nel menu agli Admin e ai ruoli custom
--   (MenuService la filtra), oltre a essere protetta dall'[Authorize] della
--   pagina. Distinta da fatt.Menu.SoloAdmin (gruppo per Admin+Superadmin).
--
--   Le pagine portano comunque [Authorize(Policy=RequireSuperadmin/RequireAdmin)]:
--   il menu governa la VISIBILITÀ, la policy governa l'ACCESSO. Doppia difesa.
--
-- Rollback
--   DELETE FROM fatt.SottoMenu WHERE IdSottoMenu IN
--     (N'5b000000-0000-0000-0000-000000000025', N'5b000000-0000-0000-0000-000000000026');
--   ALTER TABLE fatt.SottoMenu DROP CONSTRAINT DF_SottoMenu_SoloSuperadmin;
--   ALTER TABLE fatt.SottoMenu DROP COLUMN SoloSuperadmin;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- 1) Colonna SoloSuperadmin (idempotente).
IF COL_LENGTH(N'fatt.SottoMenu', N'SoloSuperadmin') IS NULL
    ALTER TABLE fatt.SottoMenu ADD SoloSuperadmin BIT NOT NULL
        CONSTRAINT DF_SottoMenu_SoloSuperadmin DEFAULT (0);
GO

-- 2) Sottovoce "Audit" (Admin + Superadmin) → pagina AuditPage.
IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000025')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo, SoloSuperadmin)
    VALUES (N'5b000000-0000-0000-0000-000000000025', N'b0000000-0000-0000-0000-000000000004',
            N'Audit', N'AuditPage', N'History', 50, 1, 0);

-- 3) Sottovoce "Log errori" (SOLO Superadmin) → pagina LogPage.
IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenu WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000026')
    INSERT INTO fatt.SottoMenu (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo, SoloSuperadmin)
    VALUES (N'5b000000-0000-0000-0000-000000000026', N'b0000000-0000-0000-0000-000000000004',
            N'Log errori', N'LogPage', N'ReportProblem', 60, 1, 1);
GO
