-- =============================================================================
-- Migration 023 — Foreign Key da fatt.Anagrafica verso i cataloghi di Fase 3
-- =============================================================================
-- Scopo
--   Chiude la "Tappa 6" del porting: l'anagrafica, finora con i puntatori
--   IdPag / IdBancaAppoggio / IdCodiciIVA come UNIQUEIDENTIFIER nullable
--   SENZA vincolo (migration 005), ottiene ora le 3 FK reali verso le tabelle
--   parent create in Fase 3:
--     * IdPag           -> fatt.CodiciPagamento (IdCodicePagamento)
--     * IdBancaAppoggio -> fatt.BancheAppoggio  (IdBancaAppoggio)
--     * IdCodiciIVA     -> fatt.CodiciIVA       (IdCodiceIVA)
--
--   IdTipologieClientela resta volutamente ORFANO (nullable, senza FK):
--   la tabella TipologieClientela non si crea (decisione 2026-06-13, punto di
--   porting P2 — alimenta gli studi di settore, non implementati ora).
--
-- Ciclo di Foreign Key (IMPORTANTE)
--   La nuova FK_Anagrafica_BancaAppoggio chiude un CICLO con la FK già
--   esistente FK_BancheAppoggio_Cliente (BancheAppoggio.IdCliente ->
--   Anagrafica.IdAnagrafica, migration 020):
--
--       Anagrafica.IdBancaAppoggio ──► BancheAppoggio.IdBancaAppoggio
--       Anagrafica.IdAnagrafica    ◄── BancheAppoggio.IdCliente
--
--   SQL Server consente FK cicliche, ma VIETA azioni di CASCADE sul ciclo.
--   Tutte le FK qui usano quindi l'azione di default ON DELETE NO ACTION /
--   ON UPDATE NO ACTION (non specificata = default). È corretto: le colonne
--   sono nullable e il dominio usa soft-delete (IsAttivo), non hard DELETE.
--   Il ciclo si gestisce a runtime con la sequenza insert(NULL) -> insert
--   banca -> update IdBancaAppoggio (vedi AnagraficaManager / dialog).
--
-- Idempotenza
--   Ogni FK è creata solo se assente (sys.foreign_keys). Prima della
--   creazione, un pre-clean azzera eventuali puntatori orfani: senza di esso
--   l'ADD CONSTRAINT WITH CHECK fallirebbe su righe storiche incoerenti.
--
-- Rollback
--   ALTER TABLE fatt.Anagrafica DROP CONSTRAINT FK_Anagrafica_CodiceIVA;
--   ALTER TABLE fatt.Anagrafica DROP CONSTRAINT FK_Anagrafica_BancaAppoggio;
--   ALTER TABLE fatt.Anagrafica DROP CONSTRAINT FK_Anagrafica_CodicePagamento;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Pre-clean difensivo: azzera i puntatori che non hanno riscontro nel parent.
-- Idempotente (NULL non viene toccato; le righe coerenti non cambiano).
-- ---------------------------------------------------------------------------
UPDATE a
   SET a.IdPag = NULL
  FROM fatt.Anagrafica AS a
 WHERE a.IdPag IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM fatt.CodiciPagamento cp WHERE cp.IdCodicePagamento = a.IdPag);
GO

UPDATE a
   SET a.IdBancaAppoggio = NULL
  FROM fatt.Anagrafica AS a
 WHERE a.IdBancaAppoggio IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM fatt.BancheAppoggio ba WHERE ba.IdBancaAppoggio = a.IdBancaAppoggio);
GO

UPDATE a
   SET a.IdCodiciIVA = NULL
  FROM fatt.Anagrafica AS a
 WHERE a.IdCodiciIVA IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM fatt.CodiciIVA ci WHERE ci.IdCodiceIVA = a.IdCodiciIVA);
GO

-- ---------------------------------------------------------------------------
-- FK 1 — IdPag -> fatt.CodiciPagamento
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Anagrafica_CodicePagamento')
BEGIN
    ALTER TABLE fatt.Anagrafica WITH CHECK
        ADD CONSTRAINT FK_Anagrafica_CodicePagamento
        FOREIGN KEY (IdPag) REFERENCES fatt.CodiciPagamento (IdCodicePagamento);
END
GO

-- ---------------------------------------------------------------------------
-- FK 2 — IdBancaAppoggio -> fatt.BancheAppoggio (chiude il ciclo: NO ACTION)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Anagrafica_BancaAppoggio')
BEGIN
    ALTER TABLE fatt.Anagrafica WITH CHECK
        ADD CONSTRAINT FK_Anagrafica_BancaAppoggio
        FOREIGN KEY (IdBancaAppoggio) REFERENCES fatt.BancheAppoggio (IdBancaAppoggio);
END
GO

-- ---------------------------------------------------------------------------
-- FK 3 — IdCodiciIVA -> fatt.CodiciIVA
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Anagrafica_CodiceIVA')
BEGIN
    ALTER TABLE fatt.Anagrafica WITH CHECK
        ADD CONSTRAINT FK_Anagrafica_CodiceIVA
        FOREIGN KEY (IdCodiciIVA) REFERENCES fatt.CodiciIVA (IdCodiceIVA);
END
GO
