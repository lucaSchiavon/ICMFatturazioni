-- =============================================================================
-- Migration 007 — Menu/Sottomenu dinamici + mapping per ruolo e per utente
-- =============================================================================
-- Scopo
--   Rende menu/permessi configurabili a runtime (memoria menu-ruoli-dinamici):
--     * fatt.Menu        voci di primo livello (gruppi o link diretti)
--     * fatt.SottoMenu   voci figlie (puntano sempre a una pagina)
--     * fatt.MenuRuolo / fatt.SottoMenuRuolo       visibilità per RUOLO (primaria)
--     * fatt.MenuUtente / fatt.SottoMenuUtente     override per UTENTE (UI in T3)
--
-- Convenzioni
--   * Il campo "Menu"/"SottoMenu" contiene il NOME DELLA CLASSE/PAGINA Razor a
--     cui la voce punta (es. 'Anagrafiche'): è la chiave su cui la guardia di
--     rotta confronta routeData.PageType.Name. Su fatt.Menu è NULLABLE: NULL =
--     voce-gruppo contenitore (solo espandibile, senza pagina propria).
--   * DescrizioneMenu/Descrizione = etichetta mostrata in UI.
--   * Icona = nome icona Material (es. 'PeopleAlt'); Ordine = ordinamento.
--   * Attivo = funzionalità implementata/cliccabile (0 = mostrata in grigio).
--     La VISIBILITÀ per ruolo è cosa distinta, governata da MenuRuolo.
--
-- Regola di accesso (in codice, non in tabella): Superadmin vede tutto incluso
--   il log errori; Admin vede tutto tranne il log errori; gli altri ruoli solo
--   ciò che MenuRuolo/SottoMenuRuolo (+ override utente) concede.
--
-- Rollback
--   DROP TABLE fatt.SottoMenuUtente, fatt.MenuUtente, fatt.SottoMenuRuolo,
--              fatt.MenuRuolo, fatt.SottoMenu, fatt.Menu;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------------------
-- Tabelle struttura menu
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.Menu', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.Menu
    (
        IdMenu          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Menu PRIMARY KEY,
        DescrizioneMenu NVARCHAR(50)     NOT NULL,        -- etichetta UI
        Menu            NVARCHAR(80)     NULL,            -- nome pagina Razor; NULL = gruppo
        Icona           NVARCHAR(64)     NULL,
        Ordine          INT              NOT NULL CONSTRAINT DF_Menu_Ordine DEFAULT (0),
        Attivo          BIT              NOT NULL CONSTRAINT DF_Menu_Attivo DEFAULT (1)
    );
END
GO

IF OBJECT_ID(N'fatt.SottoMenu', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.SottoMenu
    (
        IdSottoMenu  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SottoMenu PRIMARY KEY,
        IdMenu       UNIQUEIDENTIFIER NOT NULL,
        Descrizione  NVARCHAR(50)     NOT NULL,           -- etichetta UI
        SottoMenu    NVARCHAR(80)     NOT NULL,           -- nome pagina Razor
        Icona        NVARCHAR(64)     NULL,
        Ordine       INT              NOT NULL CONSTRAINT DF_SottoMenu_Ordine DEFAULT (0),
        Attivo       BIT              NOT NULL CONSTRAINT DF_SottoMenu_Attivo DEFAULT (1),

        CONSTRAINT FK_SottoMenu_Menu FOREIGN KEY (IdMenu) REFERENCES fatt.Menu (IdMenu)
    );
    CREATE INDEX IX_SottoMenu_IdMenu ON fatt.SottoMenu (IdMenu);
END
GO

-- ---------------------------------------------------------------------------
-- Mapping visibilità per RUOLO (primario)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.MenuRuolo', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.MenuRuolo
    (
        IdMenuRuolo UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MenuRuolo PRIMARY KEY,
        IdMenu      UNIQUEIDENTIFIER NOT NULL,
        IdRuolo     UNIQUEIDENTIFIER NOT NULL,

        CONSTRAINT FK_MenuRuolo_Menu  FOREIGN KEY (IdMenu)  REFERENCES fatt.Menu (IdMenu),
        CONSTRAINT FK_MenuRuolo_Ruolo FOREIGN KEY (IdRuolo) REFERENCES fatt.Ruoli (IdRuolo),
        CONSTRAINT UQ_MenuRuolo UNIQUE (IdMenu, IdRuolo)
    );
END
GO

IF OBJECT_ID(N'fatt.SottoMenuRuolo', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.SottoMenuRuolo
    (
        IdSottoMenuRuolo UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SottoMenuRuolo PRIMARY KEY,
        IdSottoMenu      UNIQUEIDENTIFIER NOT NULL,
        IdRuolo          UNIQUEIDENTIFIER NOT NULL,

        CONSTRAINT FK_SottoMenuRuolo_Sotto FOREIGN KEY (IdSottoMenu) REFERENCES fatt.SottoMenu (IdSottoMenu),
        CONSTRAINT FK_SottoMenuRuolo_Ruolo FOREIGN KEY (IdRuolo)     REFERENCES fatt.Ruoli (IdRuolo),
        CONSTRAINT UQ_SottoMenuRuolo UNIQUE (IdSottoMenu, IdRuolo)
    );
END
GO

-- ---------------------------------------------------------------------------
-- Override visibilità per UTENTE (tabelle pronte; UI di gestione in T3)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.MenuUtente', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.MenuUtente
    (
        IdMenuUtente UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MenuUtente PRIMARY KEY,
        IdMenu       UNIQUEIDENTIFIER NOT NULL,
        IdUtente     UNIQUEIDENTIFIER NOT NULL,

        CONSTRAINT FK_MenuUtente_Menu   FOREIGN KEY (IdMenu)   REFERENCES fatt.Menu (IdMenu),
        CONSTRAINT FK_MenuUtente_Utente FOREIGN KEY (IdUtente) REFERENCES fatt.Utenti (IdUtente),
        CONSTRAINT UQ_MenuUtente UNIQUE (IdMenu, IdUtente)
    );
END
GO

IF OBJECT_ID(N'fatt.SottoMenuUtente', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.SottoMenuUtente
    (
        IdSottoMenuUtente UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SottoMenuUtente PRIMARY KEY,
        IdSottoMenu       UNIQUEIDENTIFIER NOT NULL,
        IdUtente          UNIQUEIDENTIFIER NOT NULL,

        CONSTRAINT FK_SottoMenuUtente_Sotto  FOREIGN KEY (IdSottoMenu) REFERENCES fatt.SottoMenu (IdSottoMenu),
        CONSTRAINT FK_SottoMenuUtente_Utente FOREIGN KEY (IdUtente)    REFERENCES fatt.Utenti (IdUtente),
        CONSTRAINT UQ_SottoMenuUtente UNIQUE (IdSottoMenu, IdUtente)
    );
END
GO

-- ---------------------------------------------------------------------------
-- Seed: struttura menu attuale (3 gruppi). Solo "Anagrafiche" è Attivo=1
-- (le altre pagine arriveranno: restano visibili ma in grigio). GUID fissi
-- per poter referenziare le righe nei mapping di seed.
-- ---------------------------------------------------------------------------
-- Gruppi di primo livello (Menu = NULL → contenitori espandibili).
MERGE fatt.Menu AS t
USING (VALUES
    (N'b0000000-0000-0000-0000-000000000001', N'Tabelle Amm.ve',  N'TableChart',  10),
    (N'b0000000-0000-0000-0000-000000000002', N'Attività studio', N'WorkOutline', 20),
    (N'b0000000-0000-0000-0000-000000000003', N'Reports',         N'Assessment',  30)
) AS s (IdMenu, DescrizioneMenu, Icona, Ordine)
ON t.IdMenu = s.IdMenu
WHEN NOT MATCHED THEN
    INSERT (IdMenu, DescrizioneMenu, Menu, Icona, Ordine, Attivo)
    VALUES (s.IdMenu, s.DescrizioneMenu, NULL, s.Icona, s.Ordine, 1);
GO

-- Sottovoci. Attivo=1 solo per Anagrafiche (unica pagina già implementata).
MERGE fatt.SottoMenu AS t
USING (VALUES
    -- Tabelle Amm.ve
    (N'5b000000-0000-0000-0000-000000000001', N'b0000000-0000-0000-0000-000000000001', N'Anagrafiche',       N'Anagrafiche',            N'PeopleAlt',       10, 1),
    (N'5b000000-0000-0000-0000-000000000002', N'b0000000-0000-0000-0000-000000000001', N'Desc. attività',    N'DescrizioniAttivita',    N'ListAlt',         20, 0),
    (N'5b000000-0000-0000-0000-000000000003', N'b0000000-0000-0000-0000-000000000001', N'Banche appoggio',   N'BancheAppoggio',         N'AccountBalance',  30, 0),
    (N'5b000000-0000-0000-0000-000000000004', N'b0000000-0000-0000-0000-000000000001', N'Codici IVA',        N'CodiciIVA',              N'Percent',         40, 0),
    (N'5b000000-0000-0000-0000-000000000005', N'b0000000-0000-0000-0000-000000000001', N'Codici pagamento',  N'CodiciPagamento',        N'Payments',        50, 0),
    (N'5b000000-0000-0000-0000-000000000006', N'b0000000-0000-0000-0000-000000000001', N'Aliquote vigenti',  N'AliquoteVigenti',        N'PriceChange',     60, 0),
    -- Attività studio
    (N'5b000000-0000-0000-0000-000000000011', N'b0000000-0000-0000-0000-000000000002', N'Gest. attività studio',      N'GestAttivitaStudio',     N'Assignment',  10, 0),
    (N'5b000000-0000-0000-0000-000000000012', N'b0000000-0000-0000-0000-000000000002', N'Tipi attività studio',       N'TipiAttivitaStudio',     N'Category',    20, 0),
    (N'5b000000-0000-0000-0000-000000000013', N'b0000000-0000-0000-0000-000000000002', N'Tipi dett. attività studio', N'TipiDettAttivitaStudio', N'Segment',     30, 0),
    (N'5b000000-0000-0000-0000-000000000014', N'b0000000-0000-0000-0000-000000000002', N'Schede attività clienti',    N'SchedeAttivitaClienti',  N'FactCheck',   40, 0)
) AS s (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
ON t.IdSottoMenu = s.IdSottoMenu
WHEN NOT MATCHED THEN
    INSERT (IdSottoMenu, IdMenu, Descrizione, SottoMenu, Icona, Ordine, Attivo)
    VALUES (s.IdSottoMenu, s.IdMenu, s.Descrizione, s.SottoMenu, s.Icona, s.Ordine, s.Attivo);
GO

-- ---------------------------------------------------------------------------
-- Seed mapping per il ruolo Operatore (GUID da migration 006): vede il gruppo
-- "Tabelle Amm.ve" e la sottovoce "Anagrafiche". Admin/Superadmin NON hanno
-- bisogno di righe: la regola di bypass in codice concede loro tutto.
-- ---------------------------------------------------------------------------
DECLARE @Operatore UNIQUEIDENTIFIER = N'09e40000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM fatt.MenuRuolo WHERE IdMenu = N'b0000000-0000-0000-0000-000000000001' AND IdRuolo = @Operatore)
    INSERT INTO fatt.MenuRuolo (IdMenuRuolo, IdMenu, IdRuolo)
    VALUES (NEWID(), N'b0000000-0000-0000-0000-000000000001', @Operatore);

IF NOT EXISTS (SELECT 1 FROM fatt.SottoMenuRuolo WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000001' AND IdRuolo = @Operatore)
    INSERT INTO fatt.SottoMenuRuolo (IdSottoMenuRuolo, IdSottoMenu, IdRuolo)
    VALUES (NEWID(), N'5b000000-0000-0000-0000-000000000001', @Operatore);
GO
