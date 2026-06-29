# Checklist di rilascio — ICMFatturazioni

> **Come si usa:** copia questo file in `deploy/rilasci/AAAA-MM-GG_vX.Y.Z.md` (uno per ogni
> messa in produzione), compila l'intestazione, spunta ogni voce **dopo averla davvero
> eseguita** e annota la nota/evidenza accanto. A fine rilascio, firma e archivia una copia
> (anche in PDF). Non si va in produzione con voci bloccanti non spuntate.
>
> Rif. ISO 27001: **A8.25** (sviluppo sicuro), **A8.29** (test di sicurezza), **A8.32** (gestione dei cambiamenti).

---

## Intestazione del rilascio

| Campo | Valore |
|---|---|
| Versione / tag | `v________` |
| Data e ora | ____ / ____ / ______  ____:____ |
| Eseguito da | _______________________ |
| Approvato da | _______________________ |
| Commit (hash) | `________________` |
| Note generali | |

---

## Voci da spuntare

### 1. Codice e test
- [ ] **Test automatici verdi** — `dotnet test` eseguito, tutti i test passano.
      _Evidenza: output/screenshot del run. Note: _____________________
- [ ] **Build Release pulita** — `dotnet build -c Release` senza errori né warning nuovi.

### 2. Sicurezza
- [ ] **Scansioni eseguite senza criticità aperte** — analisi dipendenze/codice/segreti
      senza vulnerabilità *critical/high* irrisolte.
      _Comando minimo: `dotnet list package --vulnerable --include-transitive`_
- [ ] **Nessun segreto nel pacchetto** — `appsettings.Production.json` non versionato
      (escluso dal `.gitignore`); segreti via variabili d'ambiente dove possibile. Verificato.

### 3. Prova prima della produzione
- [ ] **Deploy provato in Staging/Test** — la stessa build installata e avviata su ambiente
      di prova (DB e dati separati dalla produzione), fumo di base ok: **login admin +
      apertura lista Anagrafiche + toggle tema chiaro/scuro**.
      _Note: _____________________

### 4. Database — ⚠️ schema `fatt` di proprietà di ICMVerbali
- [ ] **Oggetti `fatt` presenti su `ICMVerbaliDb` di produzione** — viste
      `fatt.Anagrafica`/`fatt.Attivita`, tabelle `fatt.Utenti`/`fatt.Ruoli`/`fatt.Log`/
      `fatt.Audit` e cataloghi, applicati col rilascio di **ICMVerbali**. **NON** applicare
      migration da questo repo (cartella `Migrations/` congelata).
      _Ultima migration ICMVerbali rilevante: n° ______. Note: ___________

### 5. Salvataggio dati
- [ ] **Backup recente verificato** — backup del DB di produzione `ICMVerbaliDb` presente e
      recente; restore provato almeno una volta.
      _Data backup di riferimento: ____ / ____ / _______

### 6. Configurazione
- [ ] **`appsettings.Production.json` predisposto** accanto a `ICMFatturazioni.Web.dll`:
      connection string a `ICMVerbaliDb` prod, SMTP, Admin/Superadmin.
- [ ] **Password admin conforme alla policy** (>=10 caratteri, con maiuscola, minuscola e
      cifra), altrimenti il seeder non crea l'utente.
- [ ] **AppPool IIS** — No Managed Code, Load User Profile = True, `ASPNETCORE_ENVIRONMENT=Production`.

### 7. Tracciabilità
- [ ] **Tag di versione creato** — `git tag vX.Y.Z` sul commit rilasciato e push del tag.

### 8. Autorizzazione
- [ ] **Approvazione al rilascio** — ok formale a procedere (email/firma del responsabile).

---

## Esito

- [ ] **Rilascio completato e verificato in produzione** (login + apertura anagrafiche ok)
- [ ] **Password admin cambiata dall'UI** dopo il primo login e `DefaultPassword` rimossa.
- [ ] Eventuali voci rinviate con accettazione del rischio: ____________________________

**Firma esecutore:** _______________  **Firma approvatore:** _______________  **Data:** ____ / ____ / ______
