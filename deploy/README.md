# Deploy IIS — template di riferimento (ICMFatturazioni)

File di esempio per il rilascio in produzione su **IIS Windows**, allineati a
ICMVerbali (stessa suite, stesso DB `ICMVerbaliDb`). Non sono usati in
build/runtime: servono come riferimento al momento del deploy.

## File

- `appsettings.Production.example.json` — template di `appsettings.Production.json`
  da copiare accanto a `ICMFatturazioni.Web.dll` nella cartella di publish.
  Contiene placeholder `CHANGE_ME_*`. **Non versionare** la versione reale
  (gia' escluso dal `.gitignore`).
- `CHECKLIST-RILASCIO.md` — checklist da compilare ad ogni messa in produzione.

> `dotnet publish -c Release` genera gia' un `web.config` valido: in genere non
> serve un template dedicato. Editalo solo per aggiungere env var o header.

## ⚠️ Differenze rispetto a ICMVerbali

1. **Database — schema `fatt` di proprieta' di ICMVerbali.** La cartella
   `Migrations/` di **questo** repo e' **congelata**: NON applicare migration da
   qui. Il DB di produzione `ICMVerbaliDb` deve gia' contenere gli oggetti `fatt`
   (viste `fatt.Anagrafica`/`fatt.Attivita`, tabelle `fatt.Utenti`, `fatt.Ruoli`,
   `fatt.Log`, `fatt.Audit`, cataloghi…), applicati con il rilascio di ICMVerbali.
2. **Nessuna cartella uploads.** ICMFatturazioni non gestisce file (a differenza
   delle foto/firme dei verbali): non c'e' alcun `Storage:UploadsBasePath` da
   configurare ne' permessi NTFS dedicati.
3. **Policy password admin.** La `Admin:DefaultPassword` deve rispettare la policy
   (>=10 caratteri, con maiuscola, minuscola e cifra): altrimenti il
   `DatabaseSeeder` fallisce in silenzio e l'utente admin non viene creato.

## Procedura sintetica

1. `dotnet publish src/ICMFatturazioni.Web -c Release -o C:\publish\ICMFatturazioni`
2. Copiare il contenuto di `C:\publish\ICMFatturazioni` nella site dir IIS
   (es. `C:\inetpub\ICMFatturazioni\app`).
3. Creare `appsettings.Production.json` accanto a `ICMFatturazioni.Web.dll`
   partendo dal template, sostituendo i `CHANGE_ME_*`.
4. (Opzionale) Editare il `web.config` generato per env var o header di sicurezza.
5. Configurare l'AppPool IIS: **No Managed Code**, **Load User Profile = True**,
   **Idle Time-out = 0**, variabile `ASPNETCORE_ENVIRONMENT=Production`.
6. Verificare che `ICMVerbaliDb` di produzione abbia gli oggetti `fatt` (punto 1
   delle differenze) e che esista un **backup recente verificato**.
7. Binding HTTPS + certificato.
8. Primo avvio: il seeder crea admin/superadmin se assenti. Dopo il primo login,
   **cambiare la password admin** dall'UI e rimuovere `DefaultPassword`.

## Nota di sicurezza

`appsettings.Production.json` contiene segreti (connection string, password admin
iniziale, chiave SMTP). **Non committarlo nel repo** (e' escluso dal `.gitignore`).
In alternativa, passa i segreti come **variabili d'ambiente** dell'AppPool
(`ConnectionStrings__Default`, `Admin__DefaultPassword`, `Superadmin__Password`,
`Smtp__Password`) e tieni `appsettings.Production.json` con i soli valori non
sensibili.
