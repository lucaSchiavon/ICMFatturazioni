# ⚠️ CARTELLA MIGRATIONS CONGELATA — non aggiungere file qui

**Dal 2026-06-19** ICMFatturazioni e ICMVerbali condividono **un unico database**
(`ICMVerbaliDb`). Lo schema `fatt` di questo progetto è stato **portato dentro
ICMVerbali** (sue migration 026–052) e da lì in poi **ICMVerbali è il proprietario
unico dello schema** (sia `dbo` sia `fatt`).

## Regole

- **NON aggiungere né modificare** file `.sql` in questa cartella: è **storica e
  congelata**, non viene più applicata ad alcun database.
- Le migration qui presenti (001–028) restano solo come **riferimento storico**
  di com'era lo schema `fatt` prima della fusione.
- Qualsiasi **nuova modifica allo schema** (tabelle/colonne `fatt`, indici, ALTER)
  va scritta come migration numerata in:
  `C:\SVILUPPO\GIT\ICMVerbali\src\ICMVerbali.Web\Migrations\`
  proseguendo la numerazione di quel repo (53, 54, …), con prefisso `Fatt` nel nome.
- In **questo** repository si modifica **solo codice C#** (entità, repository,
  manager, UI): mai lo schema del DB.

Vedi `CLAUDE.md` (sezione "Database unificato") per il dettaglio.
