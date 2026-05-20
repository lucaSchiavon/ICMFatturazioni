-- =============================================================================
-- Migration 004 — Lookup di stato (schema sta.*) + seed iniziale
-- =============================================================================
-- Scopo
--   Importa le 5 tabelle di lookup dal sorgente legacy
--   `TabelleLookupMancanti.sql` (UTF-16, schema dbo originale) nel nuovo
--   schema `sta` con le seguenti normalizzazioni rispetto al sorgente:
--
--     1) Nomi tabella senza prefisso `STA-` né `STA-FE_` (lo schema sta
--        sostituisce il prefisso, ADR D2).
--     2) Colonna `DataRecord` ridichiarata come
--        `DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()` (ADR D5 / D9):
--        evitiamo il tipo user-defined `dbo.DataRecord` del DB legacy.
--     3) PK/IDENTITY mantenuti come da originale per non rompere riferimenti
--        esterni futuri (es. seed di Anagrafiche che cita FK numeriche).
--     4) Aggiunto vincolo UNIQUE sui codici naturali (`Codice`, `Natura`,
--        `CodicePaese`) — assente nel DB legacy ma necessario in dominio.
--
-- Seed
--   Idempotente all'inserimento (anti-duplicazione): per ogni tabella la
--   popolazione avviene solo se la tabella è vuota. Aggiornamenti
--   successivi del catalogo (es. nuove Nature IVA) saranno migration
--   incrementali, non modifiche a questo file.
--
-- Rollback
--   DROP TABLE sta.Province; DROP TABLE sta.Paesi;
--   DROP TABLE sta.NatureIVA; DROP TABLE sta.ModalitaPagamento;
--   DROP TABLE sta.CondizioniPagamento;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- ---------------------------------------------------------------------------
-- sta.CondizioniPagamento (codici Agenzia Entrate TP01..TP03 al 2019;
-- catalogo "fisso" ma estendibile per nuove condizioni future)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'sta.CondizioniPagamento', N'U') IS NULL
BEGIN
    CREATE TABLE sta.CondizioniPagamento
    (
        IdCondizionePagamento INT             IDENTITY(1,1) NOT NULL,
        Codice                NCHAR(4)        NOT NULL,
        Descrizione           NVARCHAR(50)    NOT NULL,
        DataRecord            DATETIME2(3)    NOT NULL CONSTRAINT DF_sta_CondizioniPagamento_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_sta_CondizioniPagamento        PRIMARY KEY CLUSTERED (IdCondizionePagamento),
        CONSTRAINT UX_sta_CondizioniPagamento_Codice UNIQUE (Codice)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sta.CondizioniPagamento)
BEGIN
    SET IDENTITY_INSERT sta.CondizioniPagamento ON;

    INSERT INTO sta.CondizioniPagamento (IdCondizionePagamento, Codice, Descrizione) VALUES
    (1, N'TP01', N'Pagamento a rate'),
    (2, N'TP02', N'Pagamento completo'),
    (3, N'TP03', N'Anticipo');

    SET IDENTITY_INSERT sta.CondizioniPagamento OFF;
END
GO

-- ---------------------------------------------------------------------------
-- sta.ModalitaPagamento (codici Agenzia Entrate MP01..MP22 al 2019)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'sta.ModalitaPagamento', N'U') IS NULL
BEGIN
    CREATE TABLE sta.ModalitaPagamento
    (
        IdModalitaPagamento INT             IDENTITY(1,1) NOT NULL,
        Codice              NCHAR(4)        NOT NULL,
        Descrizione         NVARCHAR(50)    NOT NULL,
        DataRecord          DATETIME2(3)    NOT NULL CONSTRAINT DF_sta_ModalitaPagamento_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_sta_ModalitaPagamento        PRIMARY KEY CLUSTERED (IdModalitaPagamento),
        CONSTRAINT UX_sta_ModalitaPagamento_Codice UNIQUE (Codice)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sta.ModalitaPagamento)
BEGIN
    SET IDENTITY_INSERT sta.ModalitaPagamento ON;

    INSERT INTO sta.ModalitaPagamento (IdModalitaPagamento, Codice, Descrizione) VALUES
    (1, N'MP01', N'Contanti'),
    (2, N'MP02', N'Assegno'),
    (3, N'MP03', N'Assegno circolare'),
    (4, N'MP04', N'Contanti presso Tesoreria'),
    (5, N'MP05', N'Bonifico'),
    (6, N'MP06', N'Vaglia cambiario'),
    (7, N'MP07', N'Bollettino bancario'),
    (8, N'MP08', N'Carta di pagamento'),
    (9, N'MP09', N'RID'),
    (10, N'MP10', N'RID utenze'),
    (11, N'MP11', N'RID veloce'),
    (12, N'MP12', N'Riba'),
    (13, N'MP13', N'MAV'),
    (14, N'MP14', N'Quietanza Erario Stato'),
    (15, N'MP15', N'Giroconto su conti di contabilità speciale'),
    (16, N'MP16', N'Domiciliazione bancaria'),
    (17, N'MP17', N'Domiciliazione postale'),
    (18, N'MP18', N'Bollettino di conto corrente postale'),
    (19, N'MP19', N'SEPA Direct Debit'),
    (20, N'MP20', N'SEPA Direct Debit CORE'),
    (21, N'MP21', N'SEPA Direct Debit B2B'),
    (22, N'MP22', N'Trattenuta su somme già riscosse');

    SET IDENTITY_INSERT sta.ModalitaPagamento OFF;
END
GO

-- ---------------------------------------------------------------------------
-- sta.NatureIVA (codici Agenzia Entrate N1..N7 al 2019. Le sotto-nature
-- introdotte dal 2021 — N2.1, N3.1, N6.1, ecc. — non sono nel seed legacy
-- e dovranno essere aggiunte con una migration successiva, eventualmente
-- ampliando il tipo della colonna Natura.)
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'sta.NatureIVA', N'U') IS NULL
BEGIN
    CREATE TABLE sta.NatureIVA
    (
        IdNaturaIVA         INT             IDENTITY(1,1) NOT NULL,
        Natura              NVARCHAR(2)     NOT NULL,
        DescrizioneNatura   NVARCHAR(100)   NOT NULL,
        DataRecord          DATETIME2(3)    NOT NULL CONSTRAINT DF_sta_NatureIVA_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_sta_NatureIVA        PRIMARY KEY CLUSTERED (IdNaturaIVA),
        CONSTRAINT UX_sta_NatureIVA_Natura UNIQUE (Natura)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sta.NatureIVA)
BEGIN
    SET IDENTITY_INSERT sta.NatureIVA ON;

    INSERT INTO sta.NatureIVA (IdNaturaIVA, Natura, DescrizioneNatura) VALUES
    (1, N'N1', N'Escluse ex art. 15'),
    (2, N'N2', N'Non soggette'),
    (3, N'N3', N'Non imponibili'),
    (4, N'N4', N'Esenti'),
    (5, N'N5', N'Regime del margine'),
    (6, N'N6', N'Inversione contabile'),
    (7, N'N7', N'IVA assolta in altro stato UE');

    SET IDENTITY_INSERT sta.NatureIVA OFF;
END
GO

-- ---------------------------------------------------------------------------
-- sta.Paesi (catalogo ISO-3166 + flag CE, allineato al sorgente legacy).
-- Pk: Id (identità). Codice ISO non è chiave PK ma deve restare unique.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'sta.Paesi', N'U') IS NULL
BEGIN
    CREATE TABLE sta.Paesi
    (
        Id              INT             IDENTITY(1,1) NOT NULL,
        CodicePaese     NVARCHAR(2)     NOT NULL,
        Paese           NVARCHAR(50)    NOT NULL,
        CE              INT             NOT NULL,
        CodicePaese2    NVARCHAR(50)    NULL,
        ISO             NVARCHAR(2)     NULL,
        Cittadinanza    NVARCHAR(50)    NOT NULL,
        Lingua          NVARCHAR(2)     NOT NULL,
        DataRecord      DATETIME2(3)    NOT NULL CONSTRAINT DF_sta_Paesi_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_sta_Paesi             PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UX_sta_Paesi_CodicePaese UNIQUE (CodicePaese)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sta.Paesi)
BEGIN
    SET IDENTITY_INSERT sta.Paesi ON;

    INSERT INTO sta.Paesi (Id, CodicePaese, Paese, CE, CodicePaese2, ISO, Cittadinanza, Lingua) VALUES
    (1, N'AD', N'ANDORRA', 0, NULL, NULL, N'ANDORRA', N'EN'),
    (2, N'AE', N'EMIRATI ARABI UNITI', 0, NULL, NULL, N'EMIRATI ARABI UNITI', N'EN'),
    (3, N'AF', N'AFGHANISTAN', 0, NULL, NULL, N'AFGHANISTAN', N'EN'),
    (4, N'AG', N'ANTIGUA E BARBUDA', 0, NULL, NULL, N'ANTIGUA E BARBUDA', N'EN'),
    (5, N'AI', N'ANGUILLA', 0, NULL, NULL, N'ANGUILLA', N'EN'),
    (6, N'AL', N'ALBANIA', 0, NULL, NULL, N'ALBANESE', N'EN'),
    (7, N'AM', N'ARMENIA', 0, NULL, NULL, N'ARMENA', N'EN'),
    (8, N'AN', N'ANTILLE OLANDESI', 0, NULL, NULL, N'ANTILLE OLANDESI', N'EN'),
    (9, N'AO', N'ANGOLA (COMPRESA CABINDA)', 0, NULL, NULL, N'ANGOLA (COMPRESA CABINDA)', N'EN'),
    (10, N'AQ', N'ANTARTIDE', 0, NULL, NULL, N'ANTARTIDE', N'EN'),
    (11, N'AR', N'ARGENTINA', 0, NULL, NULL, N'ARGENTINA', N'EN'),
    (12, N'AS', N'SAMOA AMERICANE', 0, NULL, NULL, N'SAMOA AMERICANE', N'EN'),
    (13, N'AT', N'AUSTRIA', 1, N'038', N'AT', N'AUSTRIACA', N'DE'),
    (14, N'AU', N'AUSTRALIA', 0, NULL, NULL, N'AUSTRALIANA', N'EN'),
    (15, N'AW', N'ARUBA', 0, NULL, NULL, N'ARUBA', N'EN'),
    (16, N'AZ', N'AZERBAIGIAN', 0, NULL, NULL, N'AZERBAIGIAN', N'EN'),
    (17, N'BA', N'BOSNIA-ERZEGOVINA', 0, NULL, NULL, N'BOSNIACA', N'EN'),
    (18, N'BB', N'BARBADOS', 0, NULL, NULL, N'BARBADOS', N'EN'),
    (19, N'BD', N'BANGLADESH', 0, NULL, NULL, N'BANGLADESH', N'EN'),
    (20, N'BE', N'BELGIO', 1, N'017', N'BE', N'BELGA', N'EN'),
    (21, N'BF', N'BURKINA FASO', 0, NULL, NULL, N'BURKINA FASO', N'EN'),
    (22, N'BG', N'BULGARIA', 1, NULL, N'BG', N'BULGARA', N'EN'),
    (23, N'BH', N'BAHREIN', 0, NULL, NULL, N'BAHREIN', N'EN'),
    (24, N'BI', N'BURUNDI', 0, NULL, NULL, N'BURUNDI', N'EN'),
    (25, N'BJ', N'BENIN', 0, NULL, NULL, N'BENIN', N'EN'),
    (26, N'BM', N'BERMUDA', 0, NULL, NULL, N'BERMUDA', N'EN'),
    (27, N'BN', N'BRUNEI DARUSSALAM', 0, NULL, NULL, N'BRUNEI DARUSSALAM', N'EN'),
    (28, N'BO', N'BOLIVIA', 0, NULL, NULL, N'BOLIVIANA', N'EN'),
    (29, N'BR', N'BRASILE', 0, NULL, NULL, N'BRASILIANA', N'ES'),
    (30, N'BS', N'BAHAMA', 0, NULL, NULL, N'BAHAMA', N'EN'),
    (31, N'BT', N'BHUTAN', 0, NULL, NULL, N'BHUTAN', N'EN'),
    (32, N'BU', N'MYANMAR (EX BIRMANIA)', 0, NULL, NULL, N'MYANMAR (EX BIRMANIA)', N'EN'),
    (33, N'BV', N'BOUVET, ISOLA', 0, NULL, NULL, N'BOUVET, ISOLA', N'EN'),
    (34, N'BW', N'BOTSWANA', 0, NULL, NULL, N'BOTSWANA', N'EN'),
    (35, N'BY', N'BIELORUSSIA', 0, NULL, NULL, N'BIELORUSSA', N'EN'),
    (36, N'BZ', N'BELIZE', 0, NULL, NULL, N'CANADESE', N'EN'),
    (37, N'CA', N'CANADA', 0, NULL, NULL, N'CANADESE', N'FR'),
    (38, N'CC', N'COCOS (KEELING), ISOLA', 0, NULL, NULL, N'COCOS (KEELING), ISOLA', N'EN'),
    (39, N'CD', N'CONGO, REPUBBLICA DEMOCRATICA DEL', 0, NULL, NULL, N'CONGO, REPUBBLICA DEMOCRATICA DEL', N'EN'),
    (40, N'CF', N'REPUBBLICA CENTRAFRICANA', 0, NULL, NULL, N'REPUBBLICA CENTRAFRICANA', N'EN'),
    (41, N'CG', N'CONGO', 0, NULL, NULL, N'CONGOLESE', N'EN'),
    (42, N'CH', N'SVIZZERA', 0, NULL, NULL, N'SVIZZERA', N'FR'),
    (43, N'CI', N'COSTA D''AVORIO', 0, NULL, NULL, N'IVORIANA', N'EN'),
    (44, N'CK', N'COOK, ISOLE', 0, NULL, NULL, N'COOK, ISOLE', N'EN'),
    (45, N'CL', N'CILE', 0, NULL, NULL, N'CILENA', N'ES'),
    (46, N'CM', N'CAMERUN', 0, NULL, NULL, N'CAMERUN', N'EN'),
    (47, N'CN', N'CINESE, REPUBBLICA POPOLARE (CINA)', 0, NULL, NULL, N'CINESE', N'EN'),
    (48, N'CO', N'COLOMBIA', 0, NULL, NULL, N'COLOMBIANA', N'ES'),
    (49, N'CR', N'COSTA RICA', 0, NULL, NULL, N'COSTA RICA', N'ES'),
    (51, N'CU', N'CUBA', 0, NULL, NULL, N'CUBANA', N'ES'),
    (52, N'CV', N'CAPO VERDE', 0, NULL, NULL, N'CAPO VERDE', N'EN'),
    (53, N'CX', N'CHRISTMAS, ISOLA', 0, NULL, NULL, N'CHRISTMAS, ISOLA', N'EN'),
    (54, N'CY', N'CIPRO', 1, NULL, N'CY', N'CIPRIOTA', N'EN'),
    (55, N'CZ', N'REPUBBLICA CECA', 1, NULL, N'CZ', N'CECA', N'EN'),
    (56, N'DD', N'GERMANIA (REPUBBLICA DEMOCRATICA)', 0, NULL, NULL, N'GERMANIA (REPUBBLICA DEMOCRATICA)', N'DE'),
    (57, N'DE', N'GERMANIA', 1, N'004', N'DE', N'TEDESCA', N'DE'),
    (58, N'DJ', N'GIBUTI', 0, NULL, NULL, N'GIBUTI', N'EN'),
    (59, N'DK', N'DANIMARCA', 1, N'008', N'DK', N'DANESE', N'EN'),
    (60, N'DM', N'DOMINICA', 0, NULL, NULL, N'DOMINICA', N'ES'),
    (61, N'DO', N'REPUBBLICA DOMINICANA', 0, NULL, NULL, N'REPUBBLICA DOMINICANA', N'ES'),
    (62, N'DZ', N'ALGERIA', 0, NULL, NULL, N'ALGERINA', N'EN'),
    (63, N'EC', N'ECUADOR (COMPRESE GALAPAGOS)', 0, NULL, NULL, N'ECUADOR (COMPRESE GALAPAGOS)', N'ES'),
    (64, N'EE', N'ESTONIA', 1, NULL, N'EE', N'ESTONE', N'EN'),
    (65, N'EG', N'EGITTO', 0, NULL, NULL, N'EGIZIANA', N'EN'),
    (67, N'ER', N'ERITREA', 0, NULL, NULL, N'ERITREA', N'EN'),
    (68, N'ES', N'SPAGNA', 1, N'011', N'ES', N'SPAGNOLA', N'ES'),
    (69, N'ET', N'ETIOPIA', 0, NULL, NULL, N'ETIOPE', N'EN'),
    (70, N'EU', N'EURO', 0, NULL, NULL, N'EURO', N'EN'),
    (71, N'FI', N'FINLANDIA', 1, N'032', N'FI', N'FINLANDESE', N'EN'),
    (72, N'FJ', N'FIGI', 0, NULL, NULL, N'FIGI', N'EN'),
    (73, N'FK', N'FALKLAND, ISOLE (MALVINE)', 0, NULL, NULL, N'FALKLAND, ISOLE (MALVINE)', N'EN'),
    (74, N'FM', N'MICRONESIA, STATI FEDERATI DI', 0, NULL, NULL, N'MICRONESIA, STATI FEDERATI DI', N'EN'),
    (75, N'FO', N'FAEROER, ISOLE', 0, NULL, NULL, N'FAEROER, ISOLE', N'EN'),
    (76, N'FR', N'FRANCIA', 1, N'001', N'FR', N'FRANCESE', N'FR'),
    (77, N'GA', N'GABON', 0, NULL, NULL, N'GABON', N'EN'),
    (78, N'GB', N'REGNO UNITO', 1, N'006', N'GB', N'REGNO UNITO', N'EN'),
    (79, N'GD', N'GRENADA (COMPRESE ISOLE GRENADINE MERIDIONALI)', 0, NULL, NULL, N'GRENADA (COMPRESE ISOLE GRENADINE MERIDIONALI)', N'EN'),
    (80, N'GE', N'GEORGIA', 0, NULL, NULL, N'GEORGIANA', N'EN'),
    (81, N'GF', N'GUYANA FRANCESE', 0, NULL, NULL, N'GUYANA FRANCESE', N'FR'),
    (82, N'GH', N'GHANA', 0, NULL, NULL, N'GHANA', N'EN'),
    (83, N'GI', N'GIBILTERRA', 0, NULL, NULL, N'GIBILTERRA', N'EN'),
    (84, N'GL', N'GROENLANDIA', 0, NULL, NULL, N'GROENLANDIA', N'EN'),
    (85, N'GM', N'GAMBIA', 0, NULL, NULL, N'GAMBIA', N'EN'),
    (86, N'GN', N'GUINEA', 0, NULL, NULL, N'GUINEA', N'EN'),
    (87, N'GP', N'GUADALUPA', 0, NULL, NULL, N'GUADALUPA', N'EN'),
    (88, N'GQ', N'GUINEA EQUATORIALE', 0, NULL, NULL, N'GUINEA EQUATORIALE', N'EN'),
    (89, N'GR', N'GRECIA', 1, N'009', N'EL', N'GRECA', N'EN'),
    (90, N'GS', N'GEORGIA DEL SUD E ISOLE SANDWICH DEL SUD', 0, NULL, NULL, N'GEORGIA DEL SUD E ISOLE SANDWICH DEL SUD', N'EN'),
    (91, N'GT', N'GUATEMALA', 0, NULL, NULL, N'GUATEMALA', N'ES'),
    (92, N'GU', N'GUAM', 0, NULL, NULL, N'GUAM', N'EN'),
    (93, N'GW', N'GUINEA-BISSAU', 0, NULL, NULL, N'GUINEA-BISSAU', N'EN'),
    (94, N'GY', N'GUYANA', 0, NULL, NULL, N'GUYANA', N'EN'),
    (95, N'HK', N'HONG KONG', 0, NULL, NULL, N'HONG KONG', N'EN'),
    (96, N'HM', N'HEARD, ISOLA E MACDONALD ISOLE', 0, NULL, NULL, N'HEARD, ISOLA E MACDONALD ISOLE', N'EN'),
    (97, N'HN', N'HONDURAS (COMPRESO ISOLE SWAN)', 0, NULL, NULL, N'HONDURAS (COMPRESO ISOLE SWAN)', N'ES'),
    (98, N'HR', N'CROAZIA', 0, NULL, NULL, N'CROATA', N'EN'),
    (99, N'HT', N'HAITI', 0, NULL, NULL, N'HAITI', N'ES'),
    (100, N'HU', N'UNGHERIA', 1, NULL, N'HU', N'UNGHERESE', N'EN'),
    (101, N'ID', N'INDONESIA', 0, NULL, NULL, N'INDONESIANA', N'EN'),
    (102, N'IE', N'IRLANDA', 1, N'007', N'IE', N'IRLANDESE', N'EN'),
    (103, N'IL', N'ISRAELE', 0, NULL, NULL, N'ISRAELIANA', N'EN'),
    (104, N'IN', N'INDIA', 0, NULL, NULL, N'INDIANA', N'EN'),
    (105, N'IO', N'TERRITORIO BRITANNICO DELL''OCEANO INDIANO', 0, NULL, NULL, N'TERRITORIO BRITANNICO DELL''OCEANO INDIANO', N'EN'),
    (106, N'IQ', N'IRAQ', 0, NULL, NULL, N'IRAQ', N'EN'),
    (107, N'IR', N'IRAN', 0, NULL, NULL, N'IRANIANA', N'EN'),
    (108, N'IS', N'ISLANDA', 0, NULL, NULL, N'ISLANDESE', N'EN'),
    (109, N'IT', N'ITALIA', 2, N'005', N'IT', N'ITALIANA', N'EN'),
    (110, N'JM', N'GIAMAICA', 0, NULL, NULL, N'GIAMAICA', N'EN'),
    (111, N'JO', N'GIORDANIA', 0, NULL, NULL, N'GIORDANA', N'EN'),
    (112, N'JP', N'GIAPPONE', 0, NULL, NULL, N'GIAPPONESE', N'EN'),
    (113, N'KE', N'KENYA', 0, NULL, NULL, N'KENYA', N'EN'),
    (114, N'KG', N'KIRGHIZISTAN', 0, NULL, NULL, N'KIRGHIZISTAN', N'EN'),
    (115, N'KH', N'CAMBOGIA', 0, NULL, NULL, N'CAMBOGIA', N'EN'),
    (116, N'KI', N'KIRIBATI', 0, NULL, NULL, N'KIRIBATI', N'EN'),
    (117, N'KM', N'COMORE', 0, NULL, NULL, N'COMORE', N'EN'),
    (118, N'KN', N'SAINT KITTS E NEVIS', 0, NULL, NULL, N'SAINT KITTS E NEVIS', N'EN'),
    (119, N'KP', N'COREA, REPUBBLICA POPOLARE DEMOCRATICA', 0, NULL, NULL, N'COREA, REPUBBLICA POPOLARE DEMOCRATICA', N'EN'),
    (120, N'KR', N'COREA, REPUBBLICA DI', 0, NULL, NULL, N'COREANA', N'EN'),
    (121, N'KW', N'KUWAIT', 0, NULL, NULL, N'KUWAIT', N'EN'),
    (122, N'KY', N'CAYMAN, ISOLE', 0, NULL, NULL, N'CAYMAN, ISOLE', N'EN'),
    (123, N'KZ', N'KAZAKISTAN', 0, NULL, NULL, N'KAZAKISTAN', N'EN'),
    (124, N'LA', N'LAOS, REPUBBLICA DEMOCRATICA POPOLARE', 0, NULL, NULL, N'LAOS, REPUBBLICA DEMOCRATICA POPOLARE', N'EN'),
    (125, N'LB', N'LIBANO', 0, NULL, NULL, N'LIBANO', N'EN'),
    (126, N'LC', N'SAINT LUCIA', 0, NULL, NULL, N'SAINT LUCIA', N'EN'),
    (127, N'LI', N'LIECHTENSTEIN', 0, NULL, NULL, N'LIECHTENSTEIN', N'EN'),
    (128, N'LK', N'SRI LANKA', 0, NULL, NULL, N'SRI LANKA', N'EN'),
    (129, N'LR', N'LIBERIA', 0, NULL, NULL, N'LIBERIA', N'EN'),
    (130, N'LS', N'LESOTHO', 0, NULL, NULL, N'LESOTHO', N'EN'),
    (131, N'LT', N'LITUANIA', 1, NULL, N'LT', N'LITUANA', N'EN'),
    (132, N'LU', N'LUSSEMBURGO', 1, N'002', N'LU', N'LUSSEMBURGHESE', N'EN'),
    (133, N'LV', N'LETTONIA', 1, NULL, N'LV', N'LETTONE', N'EN'),
    (134, N'LY', N'LIBIA', 0, NULL, NULL, N'LIBICA', N'EN'),
    (135, N'MA', N'MAROCCO', 0, NULL, NULL, N'MAROCCO', N'EN'),
    (136, N'MD', N'MOLDOVA (MOLDAVIA)', 0, NULL, NULL, N'MOLDOVA', N'EN'),
    (137, N'MG', N'MADAGASCAR', 0, NULL, NULL, N'MADAGASCAR', N'EN'),
    (138, N'MH', N'MARSHALL, ISOLE', 0, NULL, NULL, N'MARSHALL, ISOLE', N'EN'),
    (139, N'MK', N'EX REPUBBLICA JUGOSLAVA DI MACEDONIA', 0, NULL, NULL, N'EX REPUBBLICA JUGOSLAVA DI MACEDONIA', N'EN'),
    (140, N'ML', N'MALI', 0, NULL, NULL, N'MALI', N'EN'),
    (141, N'MM', N'MYANMAR (BIRMANIA)', 0, NULL, NULL, N'MYANMAR (BIRMANIA)', N'EN'),
    (142, N'MN', N'MONGOLIA', 0, NULL, NULL, N'MONGOLIA', N'EN'),
    (143, N'MO', N'MACAO', 0, NULL, NULL, N'MACAO', N'EN'),
    (144, N'MP', N'MARIANNE SETTENTRIONALI, ISOLE', 0, NULL, NULL, N'MARIANNE SETTENTRIONALI, ISOLE', N'EN'),
    (145, N'MQ', N'MARTINICA', 0, NULL, NULL, N'MARTINICA', N'EN'),
    (146, N'MR', N'MAURITANIA', 0, NULL, NULL, N'MAURITANIA', N'EN'),
    (147, N'MS', N'MONTSERRAT', 0, NULL, NULL, N'MONTSERRAT', N'EN'),
    (148, N'MT', N'MALTA', 1, NULL, N'MT', N'MALTESE', N'EN'),
    (149, N'MU', N'MAURIZIO, ISOLA', 0, NULL, NULL, N'MAURIZIO, ISOLA', N'EN'),
    (150, N'MV', N'MALDIVE', 0, NULL, NULL, N'MALDIVE', N'EN'),
    (151, N'MW', N'MALAWI', 0, NULL, NULL, N'MALAWI', N'EN'),
    (152, N'MX', N'MESSICO', 0, NULL, NULL, N'MESSICANA', N'ES'),
    (153, N'MY', N'MALAYSIA PENINSULARE ED ORIENATLE', 0, NULL, NULL, N'MALAYSIA PENINSULARE ED ORIENATLE', N'EN'),
    (154, N'MZ', N'MOZAMBICO', 0, NULL, NULL, N'MOZAMBICO', N'EN'),
    (155, N'NA', N'NAMIBIA', 0, NULL, NULL, N'NAMIBIA', N'EN'),
    (156, N'NC', N'NUOVA CALEDONIA (COMPRESE ISOLE DELLA LEALTA'')', 0, NULL, NULL, N'NUOVA CALEDONIA (COMPRESE ISOLE DELLA LEALTA'')', N'EN'),
    (157, N'NE', N'NIGER', 0, NULL, NULL, N'NIGER', N'EN'),
    (158, N'NF', N'NORFOLK, ISOLA', 0, NULL, NULL, N'NORFOLK, ISOLA', N'EN'),
    (159, N'NG', N'NIGERIA', 0, NULL, NULL, N'NIGERIANA', N'EN'),
    (160, N'NI', N'NICARAGUA (COMPRESO ISOLE CORN)', 0, NULL, NULL, N'NICARAGUA (COMPRESO ISOLE CORN)', N'ES'),
    (161, N'NL', N'PAESI BASSI', 1, N'003', N'NL', N'OLANDESE', N'EN'),
    (162, N'NO', N'NORVEGIA (COMPRESI ARC.SVALBARD E ISOLA JAN MAYEN)', 0, NULL, NULL, N'NORVEGIA (COMPRESI ARC.SVALBARD E ISOLA JAN MAYEN)', N'EN'),
    (163, N'NP', N'NEPAL', 0, NULL, NULL, N'NEPAL', N'EN'),
    (164, N'NR', N'NAURU', 0, NULL, NULL, N'NAURU', N'EN'),
    (165, N'NU', N'NIUE, ISOLA', 0, NULL, NULL, N'NIUE, ISOLA', N'EN'),
    (166, N'NZ', N'NUOVA ZELANDA (ESCLUSA DIPENDENZA DI ROSS)', 0, NULL, NULL, N'NUOVA ZELANDA (ESCLUSA DIPENDENZA DI ROSS)', N'EN'),
    (167, N'OM', N'OMAN', 0, NULL, NULL, N'OMAN', N'EN'),
    (168, N'PA', N'PANAMA', 0, NULL, NULL, N'PANAMA', N'EN'),
    (169, N'PE', N'PERU''', 0, NULL, NULL, N'PERUVIANA', N'ES'),
    (170, N'PF', N'POLINESIA FRANCESE', 0, NULL, NULL, N'POLINESIA FRANCESE', N'FR'),
    (171, N'PG', N'PAPUASIA NUOVA GUINEA', 0, NULL, NULL, N'PAPUASIA NUOVA GUINEA', N'EN'),
    (172, N'PH', N'FILIPPINE', 0, NULL, NULL, N'FILIPPINA', N'ES'),
    (173, N'PK', N'PAKISTAN', 0, NULL, NULL, N'PAKISTAN', N'EN'),
    (174, N'PL', N'POLONIA', 1, NULL, N'PL', N'POLACCA', N'EN'),
    (175, N'PM', N'SAINT PIERRE E MIQUELON', 0, NULL, NULL, N'SAINT PIERRE E MIQUELON', N'EN'),
    (176, N'PN', N'PITCAIRN, ISOLE', 0, NULL, NULL, N'PITCAIRN, ISOLE', N'EN'),
    (177, N'PS', N'TERRITORIO PALESTINESE OCCUPATO', 0, NULL, NULL, N'TERRITORIO PALESTINESE OCCUPATO', N'EN'),
    (178, N'PT', N'PORTOGALLO', 1, N'010', N'PT', N'PORTOGHESE', N'ES'),
    (179, N'PW', N'PALAOS (PALAU)', 0, NULL, NULL, N'PALAOS (PALAU)', N'EN'),
    (180, N'PY', N'PARAGUAY', 0, NULL, NULL, N'PARAGUAY', N'ES'),
    (181, N'QA', N'QATAR', 0, NULL, NULL, N'QATAR', N'EN'),
    (182, N'QK', N'PROVVISTE E DOTAZIONI DI BORDO, ESCL. 953,954,955', 0, NULL, NULL, N'PROVVISTE E DOTAZIONI DI BORDO, ESCL. 953,954,955', N'EN'),
    (183, N'QL', N'PROV. DI BORDO CON R.D. EXPORT PER NAVI O AEREI', 0, NULL, NULL, N'PROV. DI BORDO CON R.D. EXPORT PER NAVI O AEREI', N'EN'),
    (184, N'QM', N'PROV. DI BORDO CON R.D. EXPORT PER PIAT. PETROL.', 0, NULL, NULL, N'PROV. DI BORDO CON R.D. EXPORT PER PIAT. PETROL.', N'EN'),
    (185, N'QN', N'PROV. DI BORDO CON R.D. EXPORT NON COMUNITARIE', 0, NULL, NULL, N'PROV. DI BORDO CON R.D. EXPORT NON COMUNITARIE', N'EN'),
    (186, N'QO', N'PUNTI E DEPOSITI FRANCHI', 0, NULL, NULL, N'PUNTI E DEPOSITI FRANCHI', N'EN'),
    (187, N'QP', N'CONSEGNE AI DEPOSITI DI APPROVVIGIONAMENTO', 0, NULL, NULL, N'CONSEGNE AI DEPOSITI DI APPROVVIGIONAMENTO', N'EN'),
    (188, N'QQ', N'PROVVISTE E DOTAZIONI DI BORDO, ESCL. 953,954,955', 0, NULL, NULL, N'PROVVISTE E DOTAZIONI DI BORDO, ESCL. 953,954,955', N'EN'),
    (189, N'QR', N'CONSEGNE A ORGANIZ. INTERNAZIONALI NELLA CEE', 0, NULL, NULL, N'CONSEGNE A ORGANIZ. INTERNAZIONALI NELLA CEE', N'EN'),
    (190, N'QU', N'PROVENIENZE E DESTINAZIONI NON ACCERTATE', 0, NULL, NULL, N'PROVENIENZE E DESTINAZIONI NON ACCERTATE', N'EN'),
    (191, N'RE', N'RIUNIONE  COMPRESE ISOLE EUROPA,J.DE NOVA,TROMELIN', 0, NULL, NULL, N'RIUNIONE  COMPRESE ISOLE EUROPA,J.DE NOVA,TROMELIN', N'EN'),
    (192, N'RO', N'ROMANIA', 1, NULL, N'RO', N'RUMENA', N'EN'),
    (193, N'RU', N'RUSSIA', 0, NULL, NULL, N'RUSSA', N'EN'),
    (194, N'RW', N'RUANDA', 0, NULL, NULL, N'RUANDA', N'EN'),
    (195, N'SA', N'ARABIA SAUDITA', 0, NULL, NULL, N'ARABIA SAUDITA', N'EN'),
    (196, N'SB', N'SALOMONE, ISOLE', 0, NULL, NULL, N'SALOMONE, ISOLE', N'EN'),
    (197, N'SC', N'SEYCHELLES E DIPENDENZE', 0, NULL, NULL, N'SEYCHELLES E DIPENDENZE', N'EN'),
    (198, N'SD', N'SUDAN', 0, NULL, NULL, N'SUDAN', N'EN'),
    (199, N'SE', N'SVEZIA', 1, N'030', N'SE', N'SVEDESE', N'EN'),
    (200, N'SG', N'SINGAPORE', 0, NULL, NULL, N'SINGAPORE', N'EN'),
    (201, N'SH', N'SANT''ELENA E DIPENDENZE', 0, NULL, NULL, N'SANT''ELENA E DIPENDENZE', N'EN'),
    (202, N'SI', N'SLOVENIA', 1, NULL, N'SI', N'SLOVENA', N'EN'),
    (203, N'SK', N'SLOVACCHIA', 1, NULL, N'SK', N'SLOVACCA', N'EN'),
    (204, N'SL', N'SIERRA LEONE', 0, NULL, NULL, N'SIERRA LEONE', N'EN'),
    (205, N'SM', N'SAN MARINO', 0, NULL, NULL, N'SAN MARINO', N'EN'),
    (206, N'SN', N'SENEGAL', 0, NULL, NULL, N'SENEGAL', N'EN'),
    (207, N'SO', N'SOMALIA', 0, NULL, NULL, N'SOMALA', N'EN'),
    (208, N'SR', N'SURINAME', 0, NULL, NULL, N'SURINAME', N'EN'),
    (209, N'ST', N'SAO TOME'' E PRINCIPE', 0, NULL, NULL, N'SAO TOME'' E PRINCIPE', N'EN'),
    (210, N'SU', N'UNIONE SOVIETICA', 0, NULL, NULL, N'UNIONE SOVIETICA', N'EN'),
    (211, N'SV', N'EL SALVADOR', 0, NULL, NULL, N'EL SALVADOR', N'ES'),
    (212, N'SY', N'SIRIA', 0, NULL, NULL, N'SIRIA', N'EN'),
    (213, N'SZ', N'SWAZILAND', 0, NULL, NULL, N'SWAZILAND', N'EN'),
    (214, N'TC', N'TURKS E CAICOS, ISOLE', 0, NULL, NULL, N'TURKS E CAICOS, ISOLE', N'EN'),
    (215, N'TD', N'CIAD', 0, NULL, NULL, N'CIAD', N'EN'),
    (216, N'TF', N'TERRE AUSTRALI FRANCESI', 0, NULL, NULL, N'TERRE AUSTRALI FRANCESI', N'FR'),
    (217, N'TG', N'TOGO', 0, NULL, NULL, N'TOGO', N'EN'),
    (218, N'TH', N'TAILANDIA', 0, NULL, NULL, N'TAILANDESE', N'EN'),
    (219, N'TJ', N'TAGIKISTAN', 0, NULL, NULL, N'TAGIKISTAN', N'EN'),
    (220, N'TK', N'TOKELAU, ISOLE', 0, NULL, NULL, N'TOKELAU, ISOLE', N'EN'),
    (221, N'TM', N'TURKMENISTAN', 0, NULL, NULL, N'TURKMENISTAN', N'EN'),
    (222, N'TN', N'TUNISIA', 0, NULL, NULL, N'TUNISINA', N'EN'),
    (223, N'TO', N'TONGA', 0, NULL, NULL, N'TONGA', N'EN'),
    (224, N'TP', N'TIMOR EST', 0, NULL, NULL, N'TIMOR EST', N'EN'),
    (225, N'TR', N'TURCHIA', 0, NULL, NULL, N'TURCA', N'EN'),
    (226, N'TT', N'TRINIDAD E TOBAGO', 0, NULL, NULL, N'TRINIDAD E TOBAGO', N'EN'),
    (227, N'TV', N'TUVALU', 0, NULL, NULL, N'TUVALU', N'EN'),
    (228, N'TW', N'TAIWAN', 0, NULL, NULL, N'TAIWAN', N'EN'),
    (229, N'TZ', N'TANZANIA (TANGANICA, ZANZIBAR, PENBA)', 0, NULL, NULL, N'TANZANIA (TANGANICA, ZANZIBAR, PENBA)', N'EN'),
    (230, N'UA', N'UCRAINA', 0, NULL, NULL, N'UCRAINA', N'EN'),
    (231, N'UG', N'UGANDA', 0, NULL, NULL, N'UGANDA', N'EN'),
    (232, N'UM', N'MINORI LONTANE DAGLI STATI UNITI, ISOLE', 0, NULL, NULL, N'MINORI LONTANE DAGLI STATI UNITI, ISOLE', N'EN'),
    (233, N'US', N'STATI UNITI D''AMERICA (COMPRESO PORTORICO)', 0, NULL, NULL, N'STATI UNITI D''AMERICA (COMPRESO PORTORICO)', N'EN'),
    (234, N'UY', N'URUGUAY', 0, NULL, NULL, N'URUGUAY', N'ES'),
    (235, N'UZ', N'UZBEKISTAN', 0, NULL, NULL, N'UZBEKISTAN', N'EN'),
    (236, N'VA', N'CITTA'' DEL VATICANO', 0, NULL, NULL, N'CITTA'' DEL VATICANO', N'EN'),
    (237, N'VC', N'SAINT VINCENT E GRENADINE', 0, NULL, NULL, N'SAINT VINCENT E GRENADINE', N'EN'),
    (238, N'VE', N'VENEZUELA', 0, NULL, NULL, N'VENEZUELA', N'ES'),
    (239, N'VG', N'ISOLE VERGINI BRITANNICHE', 0, NULL, NULL, N'ISOLE VERGINI BRITANNICHE', N'EN'),
    (240, N'VI', N'ISOLE VERGINI DEGLI STATI UNITI', 0, NULL, NULL, N'ISOLE VERGINI DEGLI STATI UNITI', N'EN'),
    (241, N'VN', N'VIETNAM', 0, NULL, NULL, N'VIETNAM', N'EN'),
    (242, N'VU', N'VANUATU', 0, NULL, NULL, N'VANUATU', N'EN'),
    (243, N'WF', N'WALLIS E FUTUNA, ISOLE', 0, NULL, NULL, N'WALLIS E FUTUNA, ISOLE', N'EN'),
    (244, N'WS', N'SAMOA', 0, NULL, NULL, N'SAMOA', N'EN'),
    (245, N'XA', N'OCEANIA AMERICANA (SAMOA AMER.,GUAM,IS.USA PACIF.)', 0, NULL, NULL, N'OCEANIA AMERICANA (SAMOA AMER.,GUAM,IS.USA PACIF.)', N'EN'),
    (246, N'XB', N'CANARIE, ISOLE', 0, NULL, NULL, N'CANARIE, ISOLE', N'EN'),
    (247, N'XC', N'CEUTA', 0, NULL, NULL, N'CEUTA', N'EN'),
    (248, N'XD', N'MAYOTTE', 0, NULL, NULL, N'MAYOTTE', N'EN'),
    (249, N'XE', N'OCEANIA AUSTRALIANA', 0, NULL, NULL, N'OCEANIA AUSTRALIANA', N'EN'),
    (250, N'XF', N'OCEANIA AMERICANA (SAMOA AMER.,GUAM,IS.USA PACIF.)', 0, NULL, NULL, N'OCEANIA AMERICANA (SAMOA AMER.,GUAM,IS.USA PACIF.)', N'EN'),
    (251, N'XG', N'OCEANIA NEOZELANDESE', 0, NULL, NULL, N'OCEANIA NEOZELANDESE', N'EN'),
    (252, N'XH', N'REGIONI POLARI', 0, NULL, NULL, N'REGIONI POLARI', N'EN'),
    (253, N'XI', N'CISGIORDANIA/STRISCIA DI GAZA', 0, NULL, NULL, N'CISGIORDANIA/STRISCIA DI GAZA', N'EN'),
    (254, N'XJ', N'TERRITORIO DELLA EX REP. JUGOSLAVA DI MACEDONIA', 0, NULL, NULL, N'TERRITORIO DELLA EX REP. JUGOSLAVA DI MACEDONIA', N'EN'),
    (255, N'XL', N'MELILLA', 0, NULL, NULL, N'MELILLA', N'EN'),
    (256, N'XM', N'EX REPUBBLICA JUGOSLAVA DI MACEDONIA', 0, NULL, NULL, N'EX REPUBBLICA JUGOSLAVA DI MACEDONIA', N'EN'),
    (257, N'XO', N'MARIANNE SETTENTRIONALI, ISOLE', 0, NULL, NULL, N'MARIANNE SETTENTRIONALI, ISOLE', N'EN'),
    (258, N'XP', N'CISGIORDANIA/STRISCIA DI GAZA', 0, NULL, NULL, N'CISGIORDANIA/STRISCIA DI GAZA', N'EN'),
    (259, N'XR', N'REGIONI POLARI', 0, NULL, NULL, N'REGIONI POLARI', N'EN'),
    (260, N'XZ', N'OCEANIA NEOZELANDESE (ISOLE TOKELAU,NIUE,COOK)', 0, NULL, NULL, N'OCEANIA NEOZELANDESE (ISOLE TOKELAU,NIUE,COOK)', N'EN'),
    (261, N'YD', N'YEMEN DEL SUD', 0, NULL, NULL, N'YEMEN DEL SUD', N'EN'),
    (262, N'YE', N'YEMEN', 0, NULL, NULL, N'YEMEN', N'EN'),
    (263, N'RS', N'SERBIA', 0, NULL, NULL, N'SERBA', N'EN'),
    (264, N'YT', N'MAYOTTE (GRANDE-TERRE E PAMANZI)', 0, NULL, NULL, N'MAYOTTE (GRANDE-TERRE E PAMANZI)', N'EN'),
    (266, N'ZA', N'SUDAFRICANA, REPUBBLICA', 0, NULL, NULL, N'SUDAFRICANA', N'EN'),
    (267, N'ZM', N'ZAMBIA', 0, NULL, NULL, N'ZAMBIA', N'EN'),
    (268, N'ZR', N'ZAIRE', 0, NULL, NULL, N'ZAIRE', N'EN'),
    (269, N'ZW', N'ZIMBABWE', 0, NULL, NULL, N'ZIMBABWE', N'EN'),
    (270, N'MC', N'PRINCIPATO DI MONACO', 0, NULL, NULL, N'PRINCIPATO DI MONACO', N'FR'),
    (271, N'ME', N'MONTENEGRO', 0, NULL, NULL, N'MONTENEGRO', N'EN');

    SET IDENTITY_INSERT sta.Paesi OFF;
END
GO

-- ---------------------------------------------------------------------------
-- sta.Province (sigla provincia italiana → descrizione, codice ISTAT,
-- regione). PK naturale: Prov (sigla a 2 lettere).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'sta.Province', N'U') IS NULL
BEGIN
    CREATE TABLE sta.Province
    (
        Prov                NVARCHAR(2)     NOT NULL,
        Provincia           NVARCHAR(30)    NULL,
        CodiceProvincia     SMALLINT        NULL,
        IdRegione           INT             NULL,
        DataRecord          DATETIME2(3)    NOT NULL CONSTRAINT DF_sta_Province_DataRecord DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_sta_Province PRIMARY KEY CLUSTERED (Prov)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sta.Province)
BEGIN
    INSERT INTO sta.Province (Prov, Provincia, CodiceProvincia, IdRegione) VALUES
    (N'AG', N'AGRIGENTO', 84, 15),
    (N'AL', N'ALESSANDRIA', 6, 12),
    (N'AN', N'ANCONA', 42, 10),
    (N'AO', N'AOSTA', 7, 19),
    (N'AP', N'ASCOLI PICENO', 44, 10),
    (N'AQ', N'L''AQUILA', 66, 1),
    (N'AR', N'AREZZO', 51, 16),
    (N'AT', N'ASTI', 5, 12),
    (N'AV', N'AVELLINO', 64, 4),
    (N'BA', N'BARI', 72, 13),
    (N'BG', N'BERGAMO', 16, 9),
    (N'BI', N'BIELLA', 96, 12),
    (N'BL', N'BELLUNO', 25, 20),
    (N'BN', N'BENEVENTO', 62, 4),
    (N'BO', N'BOLOGNA', 37, 5),
    (N'BR', N'BRINDISI', 74, 13),
    (N'BS', N'BRESCIA', 17, 9),
    (N'BZ', N'BOLZANO', 21, 17),
    (N'CA', N'CAGLIARI', 92, 14),
    (N'CB', N'CAMPOBASSO', 70, 11),
    (N'CE', N'CASERTA', 61, 4),
    (N'CH', N'CHIETI', 69, 1),
    (N'CL', N'CALTANISSETTA', 85, 15),
    (N'CN', N'CUNEO', 4, 12),
    (N'CO', N'COMO', 13, 9),
    (N'CR', N'CREMONA', 19, 9),
    (N'CS', N'COSENZA', 78, 3),
    (N'CT', N'CATANIA', 87, 15),
    (N'CZ', N'CATANZARO', 79, 3),
    (N'EN', N'ENNA', 86, 15),
    (N'FC', N'FORLI'' E CESENA', 40, 5),
    (N'FE', N'FERRARA', 38, 5),
    (N'FG', N'FOGGIA', 71, 13),
    (N'FI', N'FIRENZE', 48, 16),
    (N'FR', N'FROSINONE', 60, 7),
    (N'GE', N'GENOVA', 10, 8),
    (N'GO', N'GORIZIA', 31, 6),
    (N'GR', N'GROSSETO', 53, 16),
    (N'IM', N'IMPERIA', 8, 8),
    (N'IS', N'ISERNIA', 94, 11),
    (N'KR', N'CROTONE', 102, 3),
    (N'LC', N'LECCO', 100, 9),
    (N'LE', N'LECCE', 75, 13),
    (N'LI', N'LIVORNO', 49, 16),
    (N'LO', N'LODI', 101, 9),
    (N'LT', N'LATINA', 59, 7),
    (N'LU', N'LUCCA', 46, 16),
    (N'MC', N'MACERATA', 43, 10),
    (N'ME', N'MESSINA', 83, 15),
    (N'MI', N'MILANO', 15, 9),
    (N'MN', N'MANTOVA', 20, 9),
    (N'MO', N'MODENA', 36, 5),
    (N'MS', N'MASSA-CARRARA', 45, 16),
    (N'MT', N'MATERA', 77, 2),
    (N'NA', N'NAPOLI', 63, 4),
    (N'NO', N'NOVARA', 3, 12),
    (N'NU', N'NUORO', 91, 14),
    (N'OR', N'ORISTANO', 95, 14),
    (N'PA', N'PALERMO', 82, 15),
    (N'PC', N'PIACENZA', 33, 5),
    (N'PD', N'PADOVA', 28, 20),
    (N'PE', N'PESCARA', 68, 1),
    (N'PG', N'PERUGIA', 54, 18),
    (N'PI', N'PISA', 50, 16),
    (N'PN', N'PORDENONE', 93, 6),
    (N'PO', N'PRATO', 97, 16),
    (N'PR', N'PARMA', 34, 5),
    (N'PT', N'PISTOIA', 47, 16),
    (N'PU', N'PESARO E URBINO', 41, 10),
    (N'PV', N'PAVIA', 18, 9),
    (N'PZ', N'POTENZA', 76, 2),
    (N'RA', N'RAVENNA', 39, 5),
    (N'RC', N'REGGIO CALABRIA', 80, 3),
    (N'RE', N'REGGIO EMILIA', 35, 5),
    (N'RG', N'RAGUSA', 88, 15),
    (N'RI', N'RIETI', 57, 7),
    (N'RM', N'ROMA', 58, 7),
    (N'RN', N'RIMINI', 98, 5),
    (N'RO', N'ROVIGO', 29, 20),
    (N'SA', N'SALERNO', 65, 4),
    (N'SI', N'SIENA', 52, 16),
    (N'SM', N'REPUBBLICA SAN MARINO', 0, 21),
    (N'SO', N'SONDRIO', 14, 9),
    (N'SP', N'LA SPEZIA', 11, 8),
    (N'SR', N'SIRACUSA', 89, 15),
    (N'SS', N'SASSARI', 90, 14),
    (N'SV', N'SAVONA', 9, 8),
    (N'TA', N'TARANTO', 73, 13),
    (N'TE', N'TERAMO', 67, 1),
    (N'TN', N'TRENTO', 22, 17),
    (N'TO', N'TORINO', 1, 12),
    (N'TP', N'TRAPANI', 81, 15),
    (N'TR', N'TERNI', 55, 18),
    (N'TS', N'TRIESTE', 32, 6),
    (N'TV', N'TREVISO', 26, 20),
    (N'UD', N'UDINE', 30, 6),
    (N'VA', N'VARESE', 12, 9),
    (N'VB', N'VERBANIA', 99, 12),
    (N'VC', N'VERCELLI', 2, 12),
    (N'VE', N'VENEZIA', 27, 20),
    (N'VI', N'VICENZA', 24, 20),
    (N'VR', N'VERONA', 23, 20),
    (N'VT', N'VITERBO', 56, 7),
    (N'VV', N'VIBO VALENTIA', 103, 3);
END
GO