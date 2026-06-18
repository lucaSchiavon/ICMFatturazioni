-- =============================================================================
-- Migration 026 — fatt.Attivita (testata)
-- Tappa 8: tabella principale delle attività clienti (dispensa cap. 10.2).
--
-- Regola date: ProgettoDefinitivo <= ConcessioneEdilizia <= InizioLavori
-- (enforced nel Manager C#, non come CHECK SQL per evitare conflitti con NULL).
-- Soft-delete: IsAttivo BIT DEFAULT 1 (niente hard DELETE).
-- Numero: INT IDENTITY auto-incrementato, visibile come "Nr." in UI.
-- =============================================================================

-- Tabella principale
CREATE TABLE fatt.Attivita
(
    IdAttivita          UNIQUEIDENTIFIER NOT NULL
                            CONSTRAINT PK_Attivita PRIMARY KEY,

    -- Chiavi esterne
    IdAnagrafica        UNIQUEIDENTIFIER NOT NULL
                            CONSTRAINT FK_Attivita_Anagrafica
                            REFERENCES fatt.Anagrafica(IdAnagrafica),
    IdTipoAttivita      UNIQUEIDENTIFIER NOT NULL
                            CONSTRAINT FK_Attivita_TipoAttivita
                            REFERENCES fatt.TipiAttivita(IdTipoAttivita),

    -- Numero progressivo visibile (auto-incrementato, non modificabile)
    Numero              INT NOT NULL IDENTITY(1,1),

    -- Dati principali
    Descrizione         NVARCHAR(200) NOT NULL,

    -- Date del flusso (nullable; coerenza garantita dal Manager)
    ProgettoDefinitivo  DATE NULL,
    ConcessioneEdilizia DATE NULL,
    InizioLavori        DATE NULL,

    -- Costo dell'opera commissionata (NON il compenso dello studio)
    ImportoOpera        DECIMAL(18,2) NULL,

    -- Soft-delete
    IsAttivo            BIT NOT NULL CONSTRAINT DF_Attivita_IsAttivo DEFAULT 1
);
GO

-- Indici di supporto
CREATE INDEX IX_Attivita_IdAnagrafica   ON fatt.Attivita (IdAnagrafica);
CREATE INDEX IX_Attivita_IdTipoAttivita ON fatt.Attivita (IdTipoAttivita);
CREATE INDEX IX_Attivita_Numero         ON fatt.Attivita (Numero);
CREATE INDEX IX_Attivita_IsAttivo       ON fatt.Attivita (IsAttivo);
GO

-- Attiva la voce menu "Gest. attività studio" (GestAttivitaStudio, 5b...011)
UPDATE fatt.SottoMenu
SET    Attivo = 1
WHERE  IdSottoMenu = '5b000000-0000-0000-0000-000000000011';
GO
