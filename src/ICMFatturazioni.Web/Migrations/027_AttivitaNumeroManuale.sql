-- =============================================================================
-- Migration 027 — fatt.Attivita: Numero da IDENTITY a campo manuale
--
-- Il campo Numero diventa inputtabile dall'utente (dispensa cap. 10.2:
-- "Identificativo dell'attività — da chiarire con il cliente se numero
-- o nome mnemonico").
--
-- SQL Server non supporta ALTER COLUMN per rimuovere l'IDENTITY: si usa
-- il pattern add/copy/drop/rename column. I valori esistenti sono preservati.
--
-- Nota: la migration 028 creerà fatt.AttivitaDettaglio (precedentemente
-- prevista come 027 nei commenti del codice — numerazione slittata di 1).
-- =============================================================================

-- 1. Droppare l'indice che referenzia la colonna Numero.
DROP INDEX IF EXISTS IX_Attivita_Numero ON fatt.Attivita;
GO

-- 2. Aggiungere la colonna di transizione (nullable per ora).
ALTER TABLE fatt.Attivita ADD NumeroNew INT NULL;
GO

-- 3. Copiare i valori IDENTITY esistenti nella nuova colonna.
UPDATE fatt.Attivita SET NumeroNew = Numero;
GO

-- 4. Impostare NOT NULL ora che tutti i valori sono stati copiati.
ALTER TABLE fatt.Attivita ALTER COLUMN NumeroNew INT NOT NULL;
GO

-- 5. Eliminare la colonna IDENTITY originale.
ALTER TABLE fatt.Attivita DROP COLUMN Numero;
GO

-- 6. Rinominare NumeroNew → Numero.
EXEC sp_rename 'fatt.Attivita.NumeroNew', 'Numero', 'COLUMN';
GO

-- 7. Ricreare l'indice sulla colonna rinominata.
CREATE INDEX IX_Attivita_Numero ON fatt.Attivita (Numero);
GO
