-- =============================================================================
-- Migration 024 — Compressione PAGE delle tabelle diagnostiche (Audit + Log)
-- =============================================================================
-- Scopo
--   Mette le fondamenta "gratuite" alla strategia di contenimento della mole di
--   audit/log su SQL Server Express (tetto 10 GB per database, condiviso con i
--   dati operativi — vedi docs/audit-dimensionamento-sql-express.pdf §1).
--
--   fatt.Audit e fatt.Log contengono molto testo ripetitivo (diff/snapshot JSON
--   in fatt.Audit.Dati; messaggi e stack trace in fatt.Log): la PAGE compression
--   recupera tipicamente il 50–70% dello spazio (nvarchar usa 2 byte/carattere).
--   Insieme al modify-delta già adottato (AuditDettaglio.Diff salva solo i campi
--   cambiati, non l'entità intera) ci colloca nello scenario "leggero" della nota
--   tecnica: ~150–300 MB/anno, ben oltre 20 anni di orizzonte.
--
--   La compressione è TRASPARENTE: una volta impostata è una proprietà permanente
--   della tabella. Il REBUILD comprime subito i dati esistenti; da lì in avanti
--   ogni INSERT/UPDATE viene compresso automaticamente da SQL Server, senza
--   manutenzione e senza modifiche al codice C# (la lettura decomprime al volo).
--
--   ALTER INDEX ALL copre l'indice clustered (= i dati della tabella) e tutti gli
--   indici non-clustered (entrambe le tabelle hanno PK clustered + indici di
--   supporto, migration 014/015). Disponibile su Express dal 2016 SP1.
--
-- Nota sulla RETENTION
--   La cancellazione dei record vecchi (retention temporale a 36 mesi) e la
--   sentinella sui 10 GB NON sono schema: vivono nel codice applicativo
--   (AuditManager.PurgaPrecedentiAsync + AuditRetentionService + sentinella su
--   sys.database_files). Questa migration prepara solo lo storage compresso.
--
-- Idempotenza
--   Il REBUILD avviene solo se almeno una partizione della tabella non è già in
--   PAGE: la migration è rieseguibile a vuoto (recreate-db.ps1 riapplica tutto).
--   Richiede QUOTED_IDENTIFIER ON per il rebuild degli indici filtrati
--   (IX_Audit_UtenteId / IX_Log_EntityId).
--
-- Rollback
--   ALTER INDEX ALL ON fatt.Audit REBUILD WITH (DATA_COMPRESSION = NONE);
--   ALTER INDEX ALL ON fatt.Log   REBUILD WITH (DATA_COMPRESSION = NONE);
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- fatt.Audit — comprime dati (clustered) + tutti gli indici non-clustered
-- ---------------------------------------------------------------------------
IF EXISTS (
    SELECT 1
      FROM sys.partitions p
      JOIN sys.tables  t ON p.object_id = t.object_id
      JOIN sys.schemas s ON t.schema_id = s.schema_id
     WHERE s.name = N'fatt' AND t.name = N'Audit'
       AND p.data_compression_desc <> N'PAGE')
BEGIN
    ALTER INDEX ALL ON fatt.Audit REBUILD WITH (DATA_COMPRESSION = PAGE);
END
GO

-- ---------------------------------------------------------------------------
-- fatt.Log — stessa logica (Messaggio/StackTrace/SpiegazioneUtente nvarchar(max))
-- ---------------------------------------------------------------------------
IF EXISTS (
    SELECT 1
      FROM sys.partitions p
      JOIN sys.tables  t ON p.object_id = t.object_id
      JOIN sys.schemas s ON t.schema_id = s.schema_id
     WHERE s.name = N'fatt' AND t.name = N'Log'
       AND p.data_compression_desc <> N'PAGE')
BEGIN
    ALTER INDEX ALL ON fatt.Log REBUILD WITH (DATA_COMPRESSION = PAGE);
END
GO
