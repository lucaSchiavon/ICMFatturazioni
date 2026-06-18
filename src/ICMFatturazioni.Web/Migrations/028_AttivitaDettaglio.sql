-- =============================================================================
-- Migration 028 — fatt.AttivitaDettaglio + fatt.SchedulazionePagamenti
--
-- Dettagli di un'attività cliente (dispensa cap. 10.3) e
-- schedulazione dei pagamenti per ogni riga dettaglio (cap. 10.4).
--
-- Decisioni:
--   • PK GUID v7 app-side (ADR D22).
--   • UNIQUE (IdAttivita, Ordine): garantisce ordine univoco per attività;
--     il Manager gestisce lo swap con ordine temporaneo -999.
--   • HasFattura BIT: segnala righe già emesse in fattura (solo lettura
--     per il gestionale attività; impostato dal modulo fatturazione).
--   • Soft-delete con IsAttivo su entrambe le tabelle.
--   • TerminePrevisto DATE NULL: data di scadenza prevista del dettaglio.
--   • DataScadenza DATE NOT NULL: data di pagamento attesa.
-- =============================================================================

PRINT '028_AttivitaDettaglio in corso...';
GO

-- ---------------------------------------------------------------------------
-- 1. fatt.AttivitaDettaglio — righe di dettaglio di una testata attività
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.AttivitaDettaglio', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.AttivitaDettaglio
    (
        IdAttivitaDettaglio     UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_AttivitaDettaglio PRIMARY KEY,

        IdAttivita              UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT FK_AttivitaDettaglio_Attivita
                REFERENCES fatt.Attivita(IdAttivita),

        IdTipoDettaglioAttivita UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT FK_AttivitaDettaglio_TipoDettaglio
                REFERENCES fatt.TipiDettaglioAttivita(IdTipoDettaglioAttivita),

        -- Ordine di visualizzazione nella griglia (1-based).
        -- Il Manager gestisce lo swap con ordine temporaneo -999 per
        -- rispettare il UNIQUE senza disabilitarlo.
        Ordine                  INT              NOT NULL,

        DescrizioneDettaglio    NVARCHAR(200)    NOT NULL,

        Importo                 DECIMAL(18,2)    NOT NULL,

        -- Campo libero mostrato come colonna nella griglia (cap. 10.3).
        -- Include note del tipo "(QUOTA PARTE DI € X.XXX,00)".
        NotaDettaglio           NVARCHAR(500)    NULL,

        -- Data di scadenza prevista per questa voce.
        TerminePrevisto         DATE             NULL,

        -- Segnalato true dal modulo fatturazione: la riga è inclusa
        -- in una fattura emessa. Read-only per il gestionale attività.
        HasFattura              BIT              NOT NULL
            CONSTRAINT DF_AttivitaDettaglio_HasFattura DEFAULT 0,

        IsAttivo                BIT              NOT NULL
            CONSTRAINT DF_AttivitaDettaglio_IsAttivo DEFAULT 1,

        CONSTRAINT UQ_AttivitaDettaglio_IdAttivita_Ordine
            UNIQUE (IdAttivita, Ordine)
    );
    PRINT '  fatt.AttivitaDettaglio creata.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AttivitaDettaglio_IdAttivita'
      AND object_id = OBJECT_ID(N'fatt.AttivitaDettaglio')
)
    CREATE INDEX IX_AttivitaDettaglio_IdAttivita
        ON fatt.AttivitaDettaglio (IdAttivita);
GO

-- ---------------------------------------------------------------------------
-- 2. fatt.SchedulazionePagamenti — scadenze di pagamento di un dettaglio
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'fatt.SchedulazionePagamenti', N'U') IS NULL
BEGIN
    CREATE TABLE fatt.SchedulazionePagamenti
    (
        IdScadenza              UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_SchedulazionePagamenti PRIMARY KEY,

        IdAttivitaDettaglio     UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT FK_SchedulazionePagamenti_Dettaglio
                REFERENCES fatt.AttivitaDettaglio(IdAttivitaDettaglio),

        DataScadenza            DATE             NOT NULL,

        Importo                 DECIMAL(18,2)    NOT NULL,

        Nota                    NVARCHAR(200)    NULL,

        IsAttivo                BIT              NOT NULL
            CONSTRAINT DF_SchedulazionePagamenti_IsAttivo DEFAULT 1
    );
    PRINT '  fatt.SchedulazionePagamenti creata.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SchedulazionePagamenti_IdAttivitaDettaglio'
      AND object_id = OBJECT_ID(N'fatt.SchedulazionePagamenti')
)
    CREATE INDEX IX_SchedulazionePagamenti_IdAttivitaDettaglio
        ON fatt.SchedulazionePagamenti (IdAttivitaDettaglio);
GO

-- ---------------------------------------------------------------------------
-- 3. Attiva voce menu Gestione attività studio (se non già attiva)
-- ---------------------------------------------------------------------------
UPDATE fatt.SottoMenu SET Attivo = 1
WHERE IdSottoMenu = N'5b000000-0000-0000-0000-000000000011';
GO
PRINT '  Voce menu GestAttivitaStudio verificata/attivata.';
GO

PRINT '028_AttivitaDettaglio completata.';
GO
