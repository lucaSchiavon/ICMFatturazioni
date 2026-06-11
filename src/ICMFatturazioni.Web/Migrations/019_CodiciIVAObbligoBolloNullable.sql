-- =============================================================================
-- Migration 019 — fatt.CodiciIVA.ObbligoBollo diventa nullable (tri-state)
-- =============================================================================
-- Scopo
--   L'obbligo bollo non è un binario sì/no: per le operazioni non imponibili
--   (aliquota = 0) l'utente deve poter lasciare il valore **non specificato**.
--   Servono quindi tre stati:
--     * NULL = non impostato (né sì né no)
--     * 1    = sì (bollo dovuto)
--     * 0    = no (bollo non dovuto)
--
--   La migration 018 aveva creato `ObbligoBollo BIT NOT NULL DEFAULT 0`. Qui:
--     1) si elimina il default DF_CodiciIVA_ObbligoBollo (NULL = "non impostato"
--        è ora un valore significativo, non va coerciuto a 0);
--     2) si rende la colonna NULLABLE.
--
--   Operazione additiva e non distruttiva: le righe esistenti con valore 0
--   restano 0 (significato "no"), non vengono toccate.
--
-- Nota
--   La coerenza "ObbligoBollo valorizzato solo se Aliquota = 0" resta gestita
--   nell'applicazione (dialog + normalizzazione nel manager): per le aliquote
--   imponibili il manager forza il valore a NULL. NON si aggiunge un CHECK a DB
--   per non vincolare a schema una regola che potrebbe ammettere eccezioni
--   future (decisione: difesa applicativa, non di schema).
--
-- Rollback
--   UPDATE fatt.CodiciIVA SET ObbligoBollo = 0 WHERE ObbligoBollo IS NULL;
--   ALTER TABLE fatt.CodiciIVA ALTER COLUMN ObbligoBollo BIT NOT NULL;
--   ALTER TABLE fatt.CodiciIVA ADD CONSTRAINT DF_CodiciIVA_ObbligoBollo DEFAULT (0) FOR ObbligoBollo;
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- 1) Elimina il default (se presente).
IF EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = N'DF_CodiciIVA_ObbligoBollo'
      AND parent_object_id = OBJECT_ID(N'fatt.CodiciIVA')
)
BEGIN
    ALTER TABLE fatt.CodiciIVA DROP CONSTRAINT DF_CodiciIVA_ObbligoBollo;
END
GO

-- 2) Rende la colonna nullable (idempotente: se è già NULL non cambia nulla).
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'fatt.CodiciIVA')
      AND name = N'ObbligoBollo'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE fatt.CodiciIVA ALTER COLUMN ObbligoBollo BIT NULL;
END
GO
