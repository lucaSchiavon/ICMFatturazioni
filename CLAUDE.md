# CLAUDE.md

Questo file fornisce le linee guida a Claude Code (claude.ai/code) per lavorare sul codice di questo repository.

## Panoramica

Questo progetto è una **Blazor Web App** basata su **.NET 10** che utilizza la modalità di rendering **InteractiveServer** (Blazor Server). L'applicazione è costruita su **ASP.NET Core** e implementa l'autenticazione tramite **cookie-based authentication**.

### Stack tecnologico

- **Framework**: .NET 10 / ASP.NET Core
- **UI**: Blazor Web App con `InteractiveServer` render mode
- **Autenticazione**: ASP.NET Core Cookie Authentication
- **Accesso dati**: Dapper (micro-ORM)
- **Database**: SQL Server Express (locale o remoto)
- **Linguaggio**: C# con Nullable Reference Types abilitati

## Architettura

L'applicazione segue un'architettura **Layered (N-Tier)** con tre livelli logici:

- **Presentation Layer (UI)** – Componenti Blazor (`.razor`) e pagine
- **Application Layer (Services / Managers)** – Orchestrazione della logica di business
- **Data Access Layer (Repositories)** – Accesso ai dati tramite Dapper

### Pattern utilizzati

- **Repository Pattern** – astrazione dell'accesso ai dati
- **Service Layer Pattern (Manager)** – orchestrazione della logica di business
- **Dependency Injection** – tutti i servizi, manager e repository sono registrati nel DI container di ASP.NET Core

### Flusso delle chiamate

```
UI (Blazor Components) → Managers (Business Logic) → Repositories (Dapper) → SQL Server
```

**Regola fondamentale**: la UI **non** deve mai chiamare direttamente i Repository. Ogni interazione con i dati passa sempre attraverso il Manager corrispondente.

### Mappatura Repository ↔ Manager

Ogni Repository ha **uno e un solo** Manager dedicato che ne incapsula la logica di business.

| Repository           | Manager           | Entità         |
|----------------------|-------------------|----------------|
| `IUserRepository`    | `IUserManager`    | `User`         |
| `IProductRepository` | `IProductManager` | `Product`      |
| `IOrderRepository`   | `IOrderManager`   | `Order`        |

(La tabella sopra è esemplificativa: estendere a tutte le entità del dominio.)

## Struttura della soluzione

```
MyApp.sln
│
├── src/
│   └── MyApp.Web/                          # Progetto Blazor Web App
│       │
│       ├── Components/                     # Componenti Blazor (Presentation Layer)
│       │   ├── Layout/                     # MainLayout, NavMenu, ecc.
│       │   ├── Pages/                      # Pagine routable (@page)
│       │   ├── Shared/                     # Componenti condivisi
│       │   ├── App.razor
│       │   ├── Routes.razor
│       │   └── _Imports.razor
│       │
│       ├── Entities/                       # Entità di dominio (POCO)
│       │   ├── User.cs
│       │   ├── Product.cs
│       │   └── Order.cs
│       │
│       ├── Repositories/                   # Data Access Layer (Dapper)
│       │   ├── Interfaces/
│       │   │   ├── IUserRepository.cs
│       │   │   └── IProductRepository.cs
│       │   ├── UserRepository.cs
│       │   └── ProductRepository.cs
│       │
│       ├── Managers/                       # Application Layer (logica di business)
│       │   ├── Interfaces/
│       │   │   ├── IUserManager.cs
│       │   │   └── IProductManager.cs
│       │   ├── UserManager.cs
│       │   └── ProductManager.cs
│       │
│       ├── Authentication/                 # Cookie auth, claims, policies
│       │   ├── CookieAuthHandler.cs
│       │   └── AuthorizationPolicies.cs
│       │
│       ├── Data/                           # Connection factory, DB helpers
│       │   ├── ISqlConnectionFactory.cs
│       │   └── SqlConnectionFactory.cs
│       │
│       ├── Migrations/                     # Script SQL versionati (NON modificare quelli esistenti)
│       │   ├── 001_InitialSchema.sql
│       │   ├── 002_AddProductsTable.sql
│       │   └── ...
│       │
│       ├── Models/                         # DTO / ViewModel per la UI
│       ├── wwwroot/                        # Asset statici
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Program.cs
│       └── MyApp.Web.csproj
│
└── tests/
    └── MyApp.Tests/                        # Progetto di test (xUnit)
        ├── Managers/
        ├── Repositories/
        └── MyApp.Tests.csproj
```

## Convenzioni di codice C#

### Nullable Reference Types

I **Nullable Reference Types** sono **sempre abilitati** (`<Nullable>enable</Nullable>` nel `.csproj`). Non disabilitare questa impostazione, né a livello di progetto né con `#nullable disable` su singoli file.

### Naming

- **PascalCase** per classi, metodi pubblici, proprietà, namespace
- **camelCase** per parametri e variabili locali
- **_camelCase** con underscore per campi privati
- **IPascalCase** per interfacce (es. `IUserRepository`)
- **Async** suffisso obbligatorio per metodi asincroni (`GetUserByIdAsync`)

### Stile

- Usare `var` quando il tipo è evidente dal contesto, altrimenti tipo esplicito
- Usare **file-scoped namespace** (`namespace MyApp.Web.Managers;`)
- Usare **target-typed `new()`** dove migliora la leggibilità
- Usare **records** per DTO immutabili
- Preferire **expression-bodied members** per metodi e proprietà a singola istruzione
- Mantenere `using` ordinati: BCL → terze parti → progetto, separati da riga vuota

### Async / Await

- Tutti i metodi che effettuano I/O (DB, HTTP, file) devono essere `async Task` / `async Task<T>`
- Non usare `.Result` o `.Wait()` – sempre `await`
- Passare `CancellationToken` lungo la catena di chiamate

### Repository (Dapper)

- I Repository accettano una `ISqlConnectionFactory` tramite costruttore (DI)
- Le query SQL sono inline con stringhe `const` o costanti private
- Usare parametri Dapper (`@param`) – **mai** concatenazione di stringhe
- I metodi restituiscono entità di dominio (`Entities/`), non `DataTable` o `dynamic`

### Manager

- Ogni Manager dipende da uno o più Repository tramite interfaccia
- I Manager **non** espongono `IDbConnection` o dettagli di Dapper alla UI
- La logica di validazione e le regole di business risiedono qui, **non** nei componenti Blazor

### Componenti Blazor

- Logica di code-behind in file `.razor.cs` (partial class) per componenti non banali
- Iniettare i **Manager**, mai i Repository (`@inject IUserManager UserManager`)
- Usare `IDisposable` / `IAsyncDisposable` quando si sottoscrivono eventi

### Commenti e leggibilità del codice

Scrivere codice pulito, documentato e auto-esplicativo. Inserire commenti **frequenti, chiari e descrittivi**, con un duplice obiettivo:

- rendere il software immediatamente comprensibile a uno sviluppatore terzo
- garantire una manutenzione futura semplice e priva di ambiguità

Privilegiare commenti che spiegano il *perché* (intento, vincolo, scelta tra alternative) rispetto a quelli che parafrasano il *cosa* già evidente dal codice. I membri pubblici di Manager e Repository portano commenti XML (`///`) quando la responsabilità non è banale.

## Pattern di design ricorrenti

Pattern che possono emergere nel codice e da preservare nelle estensioni future. Non sono dogmi: ognuno ha un trade-off esplicito, applicarli solo quando il contesto è quello descritto.

### Stato logico derivato da colonne nullable

Quando lo stato di un'entità è ortogonale e si esprime con pochi flag (timestamp `*Utc` nullable), evitare un enum esplicito da mantenere in sync. Lo stato si deriva leggendo le colonne.

- **Esempio**: `FirmaToken` ha `UsatoUtc`, `RevocatoUtc`, `ScadenzaUtc`. Lo stato "Attivo" è `UsatoUtc IS NULL AND RevocatoUtc IS NULL AND ScadenzaUtc > SYSUTCDATETIME()`.
- **Quando applicarlo**: pochi stati, davvero ortogonali (non mutuamente esclusivi *a priori*), e ognuno corrisponde a un evento datato che vuoi tracciare in audit.
- **Trade-off**: il predicato "Attivo" va replicato in ogni query (`GetUltimoAttivoAsync`, `SqlMarkTokenUsato`, …). Per stati molti o non ortogonali → usare un enum.

### Doppia difesa: manager pre-check + repository sentinel

Per ogni UPDATE che dipende da uno stato letto in precedenza (TOCTOU), proteggere il flusso in **due punti**:

1. **Manager**: pre-check esplicito che genera un'eccezione tipizzata con messaggio user-friendly (`ValidaTokenAsync` → `FirmaTokenInvalidoException`)
2. **Repository**: la stessa condizione replicata come `WHERE` sentinel nella UPDATE (`SqlMarkTokenUsato … AND RevocatoUtc IS NULL`)

Il pre-check serve la UX (messaggio specifico). Il sentinel serve la correttezza sotto race condition: anche se il manager fosse aggirato o lo stato cambiasse fra check e use, l'UPDATE non aggiorna righe e la firma fallisce.

- **Quando applicarlo**: ogni volta che lo stato controllato in lettura può cambiare per opera di un altro attore fra il check e l'UPDATE (rigenerazioni, revoche, transizioni di workflow).

### Ordine dei controlli in eccezioni tipizzate è UX

Quando un'eccezione tipizzata ha più motivi possibili (es. `FirmaTokenInvalidoMotivo` con `NonTrovato`/`Revocato`/`GiaUsato`/`Scaduto`), l'ordine di valutazione decide il messaggio mostrato all'utente.

- **Regola**: mettere per primo il motivo che meglio guida l'azione successiva dell'utente, non il primo "tecnicamente disponibile".
- **Esempio**: in `ValidaTokenAsync` l'ordine è `NonTrovato → Revocato → GiaUsato → Scaduto`. `Revocato` precede `Scaduto` perché "Link sostituito, richiedi quello aggiornato" è più azionabile di "Link scaduto".
- **Quando applicarlo**: ogni metodo che lancia un'eccezione tipizzata che espone il motivo tramite una proprietà `enum` (es. `FirmaTokenInvalidoException` con proprietà `Motivo` di tipo `FirmaTokenInvalidoMotivo`, valori `NonTrovato`/`Revocato`/`GiaUsato`/`Scaduto`).

## UI/UX e stile

### Brand guidelines

Per qualsiasi scelta estetica — palette colori, tipografia, componenti, layout CSS — fare **tassativamente** riferimento al file `brand guidelines` presente in root nel progetto (brand-guidelines.md). Non introdurre colori, font o componenti grafici al di fuori di quanto definito lì. Se il file non copre un caso specifico, segnalarlo prima di inventare uno stile nuovo: il brand è autoritativo, non orientativo.

### Responsività

Progettare e scrivere il codice dell'interfaccia con approccio **rigorosamente responsivo**. L'applicazione deve garantire un'esperienza utente ottimale su:

- browser desktop
- tablet
- smartphone

Ogni componente Blazor introdotto va verificato sui tre form factor, non solo su desktop: layout, spaziature, dimensioni dei controlli e leggibilità del testo devono adattarsi correttamente.

## Comandi build / test

Tutti i comandi vanno eseguiti dalla root della soluzione.

### Build

```bash
dotnet restore
dotnet build
dotnet build -c Release
```

### Esecuzione locale

```bash
dotnet run --project src/MyApp.Web
```

L'app sarà disponibile su `https://localhost:5001` (o porta configurata in `launchSettings.json`).

### Test

```bash
dotnet test                              # Esegue tutti i test
dotnet test --filter "FullyQualifiedName~UserManager"   # Filtra per nome
dotnet test --collect:"XPlat Code Coverage"             # Con code coverage
```

### Database

Gli script di migration vanno applicati in ordine numerico al SQL Server Express locale:

```bash
sqlcmd -S .\SQLEXPRESS -d MyAppDb -i src/MyApp.Web/Migrations/001_InitialSchema.sql
```

### Format / Lint

```bash
dotnet format                # Applica le regole di formattazione
dotnet format --verify-no-changes   # Verifica in CI
```

## Regole di modifica del codice

Queste regole sono **vincolanti** e devono essere rispettate in ogni modifica.

### 1. Pacchetti NuGet

**Chiedere sempre conferma** prima di aggiungere un nuovo pacchetto NuGet al progetto. Quando proposto, indicare:

- Nome del pacchetto e versione
- Motivazione (perché serve, quale problema risolve)
- Eventuali alternative già presenti nello stack
- Licenza del pacchetto

Non eseguire `dotnet add package` senza approvazione esplicita.

### 2. Migration esistenti

**Non modificare** i file di migration già esistenti nella cartella `Migrations/`. Le migration sono storiche e immutabili: una volta applicate a un ambiente, modificarle causa disallineamenti.

Per cambiare lo schema:

- Creare un **nuovo** file di migration con numero progressivo (es. `015_AddUserEmailIndex.sql`)
- Includere sia lo script di aggiornamento che, dove sensato, una nota di rollback

### 3. Nullable Reference Types

**Mantenere sempre abilitati** i Nullable Reference Types. Non:

- Rimuovere `<Nullable>enable</Nullable>` dai `.csproj`
- Aggiungere `#nullable disable` a inizio file
- Usare `!` (null-forgiving operator) per "silenziare" warning senza una motivazione documentata

Quando un valore può legittimamente essere null, dichiararlo esplicitamente (`string?`, `User?`) e gestirlo nel codice.

### 4. Architettura

- La UI **non** chiama direttamente i Repository
- Ogni Repository ha **un solo** Manager corrispondente
- Le entità in `Entities/` sono POCO senza dipendenze da Dapper, EF o ASP.NET
- Le query SQL stanno **solo** nei Repository

### 5. Sicurezza

- Mai loggare password, cookie di sessione o token
- Mai concatenare input utente in stringhe SQL: usare sempre parametri Dapper
- Le policy di autorizzazione si dichiarano in `Authentication/AuthorizationPolicies.cs`, non sparse nei componenti

### 6. Logging degli errori (tabella `fatt.Log`)

**Ogni errore di runtime o eccezione sollevata dall'applicazione DEVE essere persistito nella tabella `fatt.Log`.** La regola è categorica e si applica a tutti i layer (UI Blazor, Manager, Repository, middleware, background job). L'obiettivo è avere visibilità completa, in produzione, su errori subdoli, silenti all'interfaccia o difficili da riprodurre.

> **Impianto attuale (mirror di `dbo.Log` di ICMVerbali, migration `014_Log.sql`).** Sostituisce il precedente `LogErrors` (BIGINT/IDENTITY + servizio `IErrorLogger`): scrittura **asincrona disaccoppiata** e **cattura automatica** dei log del framework. Tabella **immutabile** (solo INSERT, mai UPDATE).

#### Cosa deve essere tracciato

Per ogni evento, la riga di `fatt.Log` contiene:

| Campo | Tipo SQL | Descrizione |
|---|---|---|
| `Id` | `UNIQUEIDENTIFIER` | PK, GUID v7 generato app-side |
| `TimestampUtc` | `DATETIME2(3)` | Data e ora UTC (default `SYSUTCDATETIME()`) |
| `Livello` | `TINYINT` | 3 = Warning, 4 = Error, 5 = Critical (allineato a `Microsoft.Extensions.Logging.LogLevel`) |
| `Sorgente` | `NVARCHAR(256)` | Categoria del logger o sorgente esplicita (es. `Auth.ForgotPassword`) |
| `Messaggio` | `NVARCHAR(MAX)` | `ex.Message` o messaggio formattato del log |
| `EccezioneTipo` | `NVARCHAR(512)` | FQN del tipo di eccezione (nullable) |
| `StackTrace` | `NVARCHAR(MAX)` | `ex.ToString()` completo (nullable) |
| `SpiegazioneUtente` | `NVARCHAR(MAX)` | Spiegazione user-friendly: valorizzata **solo** dal path esplicito `LogErroreAsync` (nullable) |
| `RequestId` | `NVARCHAR(128)` | `Activity.Current?.Id`, per correlare alla richiesta (nullable) |
| `UtenteId` | `UNIQUEIDENTIFIER` | Utente coinvolto, se noto (nullable) |
| `EntityId` / `EntityType` | `UNIQUEIDENTIFIER` / `NVARCHAR(128)` | Entità di dominio coinvolta, se pertinente (nullable) |

La tabella è creata dalla migration `014_Log.sql`. Indici: `TimestampUtc` (desc), `Livello`, `EntityId` (filtrato).

#### Due vie di scrittura

1. **Automatica (rete del framework)** — `DbLoggerProvider` (`ILoggerProvider`, con filtro forzato a `Warning`+) cattura i log `Warning`/`Error`/`Critical` della pipeline standard, **incluse le eccezioni non gestite** di HTTP e dei circuiti Blazor (il framework le logga a livello `Error`). Non richiede chiamate esplicite. Anti-ricorsione: esclude le categorie del proprio namespace (`*.Logging`) e di `Microsoft.Data.SqlClient`.
2. **Esplicita (path "ricco")** — `ILogManager.LogErroreAsync(eccezione, spiegazione, sorgente, utenteId?, entityId?, entityType?, ct)` nei `catch` che gestiscono un'eccezione **senza** rilanciarla, per aggiungere una `SpiegazioneUtente` che la rete automatica non può fornire. I componenti Blazor iniettano `ILogManager`, mai il repository.

Disaccoppiamento: entrambe le vie **accodano** su `ILogQueue` (channel bounded, modalità `DropWrite`: non bloccano mai la richiesta); il `LogWriterService` (`BackgroundService`) drena la coda a batch e scrive su `fatt.Log` via `ILogRepository`.

#### Registrazione DI

- **Singleton**: `ILogRepository`, `ILogQueue`, `ILogFallbackWriter`, `DbLoggerProvider` (+ `builder.Logging.AddFilter<DbLoggerProvider>(null, LogLevel.Warning)`).
- **Scoped**: `ILogManager`. **Hosted**: `LogWriterService`.
- **Rete globale** in `Program.cs`: `AppDomain.UnhandledException` e `TaskScheduler.UnobservedTaskException` → coda a livello `Critical` (errori fuori dal ciclo di richiesta: thread di background, Task non osservate).

#### Punti di cattura

L'errore va tracciato **prima** di essere mostrato all'utente o silenziato:

1. **Eccezioni non gestite HTTP / circuiti Blazor** — catturate **automaticamente** dal `DbLoggerProvider` (il framework le logga a `Error`). In produzione `UseExceptionHandler` + `ProblemDetails` per la risposta; in sviluppo la developer page.
2. **`ErrorBoundary` custom (`IcmErrorBoundary`)** — mostra il fallback UI e registra via `ILogManager.LogErroreAsync` (l'ErrorBoundary intercetta l'eccezione prima del logging del framework, quindi va loggata esplicitamente).
3. **Ogni `try/catch` nei Manager/componenti** che gestisce un'eccezione senza rilanciarla: loggare via `LogErroreAsync` con `spiegazione` prima di restituire un fallback. I `catch` che rilanciano possono evitare il log (catturato a monte).
4. **Background / hosted services** — loop avvolto in try/catch con log; un'eccezione non loggata in un `BackgroundService` è invisibile.

#### Regole anti-mascheramento

- Il logging **non deve mai** lanciare eccezioni che mascherano l'errore originale: `LogManager` e `LogWriterService` hanno il proprio `try/catch` e, in caso di fallimento della scrittura su DB (es. SQL Server irraggiungibile), ricadono su `ILogFallbackWriter` che scrive **sia su `Console.Error`** (raccolto dalle piattaforme PaaS) **sia sul file** `logs/error-logger-fallback.log` (utile on-prem/IIS).
- Mai `catch { }` vuoti. Mai `catch (Exception) { return null; }` senza log. Un'eccezione silenziata senza traccia in `fatt.Log` è un bug.
- **Sanitizzazione obbligatoria** (Regola 5): `LogSanitizer` maschera `Password`/`Token`/connection string (`→ ***`) su **entrambe** le vie, prima della persistenza. Mai password, token, cookie o stringhe di connessione in chiaro.
- Le **eccezioni tipizzate di validazione** (flusso previsto, es. `AnagraficaInvalidaException`, `UtenteTokenInvalidoException`) **NON** si loggano: non sono errori.

#### Consultazione e retention

- Pagina amministrativa **`/admin/log`** (accesso **solo Superadmin**): filtri per data/livello/sorgente/testo, dettaglio espandibile (messaggio, stack, `RequestId`), in **ora locale italiana**.
- Retention: `ILogManager.PurgaPrecedentiAsync(giorni)` (pulsante in pagina) oppure SQL job, per evitare crescita illimitata della tabella.

### 7. Audit delle modifiche dati (tabella `fatt.Audit`)

**Ogni operazione che modifica il database per mano dell'utente (INSERT / UPDATE / DELETE) DEVE essere tracciata in `fatt.Audit`.** La regola è categorica e vale per ogni CRUD o funzionalità, presente e futura. È distinta dalla Regola 6 (che traccia gli *errori* in `fatt.Log`): qui si traccia **chi ha fatto cosa** sui dati.

#### Cosa tracciare

Oltre a chi/quando/quale entità (utente snapshot, timestamp, `EntityType`+`EntityId`, descrizione breve), va salvato il **dettaglio strutturato in JSON** (colonna `fatt.Audit.Dati`) che rende evidente *cosa* è stato scritto:

- **Insert** → snapshot completo del nuovo record.
- **Update** → diff dei soli campi cambiati (`{ campo: { prima, dopo } }`); leggere lo stato **precedente** *prima* dell'update.
- **Delete** → snapshot del record eliminato.

**Mai** salvare la query SQL letterale (parametrizzata, e a rischio di esporre segreti) né i segreti stessi (`PasswordHash`/`Salt`/`TokenHash`): l'helper `AuditDettaglio` li esclude già a monte (coerente con Regola 5 e Regola 6).

#### Pattern di utilizzo

- Il Manager dipende da `IAuditManager` e registra **dopo** che la scrittura su DB è andata a buon fine: `RegistraCreazioneAsync` / `RegistraModificaAsync` / `RegistraEliminazioneAsync`.
- `descrizione` = etichetta breve leggibile (ragione sociale, username…); `dati` = JSON via `AuditDettaglio.Snapshot(...)` (insert/delete) o `AuditDettaglio.Diff(prima, dopo)` (update). Per operazioni puramente segrete (es. reset password) `dati = null`: si traccia solo l'evento.
- L'utente corrente è risolto da `ICurrentUserAccessor` (claim del cookie): il Manager passa solo il "cosa".
- L'audit è **best-effort**: un suo fallimento non deve far fallire l'operazione di business già completata (viene loggato in `fatt.Log`, non propagato).
- Scrivere **contestualmente** il test che verifica la voce di audit (operazione + `EntityType` + `EntityId`).
- **Nota firma**: `RegistraX(entityType, entityId, descrizione, string? dati = null, CancellationToken ct = default)` — passare il `CancellationToken` **nominato** (`cancellationToken:`) per non farlo collassare sul parametro `dati`.

#### Cosa NON tracciare

Operazioni che non sono modifiche di dominio dell'utente: preferenze cosmetiche (tema), timestamp tecnici (ultimo login), seed di sistema all'avvio, tabelle di lookup ministeriali read-only. In caso di dubbio, **segnalare** prima di decidere.

#### Infrastruttura

`fatt.Audit` (con colonna `Dati`) + `IAuditManager`/`AuditManager` + `IAuditRepository`/`AuditRepository` + `ICurrentUserAccessor` + helper `Auditing/AuditDettaglio` (`Snapshot`/`Diff`/`Pretty`) + pagina di consultazione `/admin/audit` (accesso Admin+Superadmin, dettaglio JSON in riga espandibile). Mirror di ICMVerbali (`dbo.Audit`), con in più il dettaglio JSON `Dati`.

## Convenzioni di porting

Regole vincolanti emerse dal decision-gate del 2026-05-20 (decisioni D1-D20). Le **motivazioni dettagliate** di ogni scelta sono in `docs/decisioni-architetturali.md` (ADR del progetto): qui restano solo le regole operative.

### Naming (D1, D2, D3)

- **Entità C# e proprietà**: italiano fedele al PDF/schema originali (`Anagrafica`, `RagioneSociale`, `CodiceFiscale`, `TipoAnagrafica`). Mai tradurre in inglese, anche se "più idiomatico".
- **Tabelle SQL**: tutte sotto un **unico schema applicativo `fatt.*`** (namespace dell'app, non prefissi nel nome). ⚠️ **Aggiornato il 2026-06-09 (ADR D21): supera la precedente ripartizione `sta`/`ana`/`fat`/`dbo`.** Motivo: ICMFatturazioni e **ICMVerbali** convergeranno su un **unico DB condiviso** (entità che si fonderanno: `Attivita`↔`Progetto`, `Anagrafica`↔`Committente`); Verbali vive sotto `dbo.*`, quindi mettere TUTTO ICMFatturazioni sotto `fatt.*` dà ownership esplicita e zero collisioni (incluse `fatt.Utenti`, `fatt.LogErrors`).
  - `fatt.*` — **tutte** le tabelle di ICMFatturazioni: lookup di stato (`fatt.Paesi`, `fatt.Province`, `fatt.NatureIVA`, `fatt.CondizioniPagamento`, `fatt.ModalitaPagamento`), anagrafiche (`fatt.Anagrafica`), codici IVA/pagamento, banche, attività (`fatt.Attivita`, `fatt.AttivitaDettaglio`, `fatt.SchedulazionePagamenti`), e le trasversali (`fatt.Utenti`, `fatt.Log`, `fatt.Audit`).
  - La prima migration (`001_CreateSchemas.sql`) crea **solo** lo schema `fatt` (`CREATE SCHEMA fatt`) prima di qualsiasi tabella.
  - Tutte le query SQL nei Repository usano il nome **schema-qualificato** (`FROM fatt.Paesi`, non `FROM Paesi`).
  - **Nomi entità invariati nel contesto ICMFatturazioni**: `Attivita` e `Anagrafica` restano tali (in ICMVerbali sono `Progetto` e `Committente`); la fusione è lavoro futuro con ADR dedicato. Divergenze note da risolvere alla fusione: PK `INT IDENTITY` (qui) vs `uniqueidentifier` (Verbali); `DataRecord` vs `CreatedAt`/`UpdatedAt`+`IsAttivo`. Vedi `docs/piano-sviluppo-fase1-attivita.md` §1.1.
- **Campi enum-like** (`TipoAnagrafica` con valori `S`/`P`/`E`): enum C# con persistenza `char(1)` (o codice testuale dove l'originale lo prevede). L'enum espone il valore di persistenza esplicitamente, mai derivato dall'ordinale.

### Allineamento a ICMVerbali (D22, 2026-06-09) — VINCOLANTE

ICMFatturazioni deve **uniformarsi a ICMVerbali** (`C:\SVILUPPO\GIT\ICMVerbali`, app già sviluppata e validata) sia nelle convenzioni DB sia nei pattern delle funzionalità. Le due app convergeranno su un DB condiviso e l'uniformità ne semplifica la gestione. Vedi ADR D22 e memoria `mirror-icmverbali`.

- **Principio operativo (precede ogni Tappa)**: prima di implementare una funzionalità nuova, **ispezionare ICMVerbali** per un equivalente (gestione utenti, cambio password, logging, audit, CRUD anagrafiche, cataloghi, ecc.) e **proporne all'utente la replica nello stesso modo** prima di scrivere codice.
- **Convenzioni DB allineate a ICMVerbali** (sostituiscono PK `INT IDENTITY` e `DataRecord` di D9/D19 per le tabelle di dominio):
  - **PK `uniqueidentifier` (GUID)**, generata app-side con **`Guid.CreateVersion7()`** (UUIDv7 *time-ordered*, come Verbali) e passata nell'INSERT (no `IDENTITY`, no `OUTPUT`). Modello: `CommittenteRepository`. *(Nota tecnica: l'ordinamento UUIDv7 non si riflette nell'ordine di clustering di SQL Server — che compara `uniqueidentifier` in modo non lessicografico — ma per i volumi del progetto la frammentazione è irrilevante; i veri vantaggi sono Id app-side, URL non enumerabili e fusione DB senza collisioni.)*
  - **Soft-delete con `IsAttivo BIT NOT NULL DEFAULT 1`** per le anagrafiche/master: niente hard `DELETE`. Letture: `GetAttivi` (`WHERE IsAttivo = 1`), `GetAll` (`ORDER BY IsAttivo DESC, ...`).
  - **Niente `CreatedAt`/`UpdatedAt` di default** sulle anagrafiche/lookup master (Verbali non li mette su `Committente`/`Cantiere`). Si aggiungono solo dove un lifecycle per-riga serve davvero (es. `Utente`, entità con workflow).
  - **Audit "chi-ha-fatto-cosa" centralizzato** in una tabella generica `fatt.Audit` (mirror di `dbo.Audit`: `UtenteId`+`UtenteNome` snapshot, `Operazione`, `EntityType`+`EntityId`, `Descrizione`), non con colonne per-riga.
  - **Logging errori** su `fatt.Log` (mirror di `dbo.Log`) via `ILogManager.LogErroreAsync(ex, spiegazione, sorgente)` + rete automatica `DbLoggerProvider`; le eccezioni tipizzate di validazione **non** si loggano (vedi loro Regola 6 / `docs/B17-logging.md`).
  - **Nomi tabella/colonna**: restano **fedeli al legacy italiano** (D1, scelta utente) — `CodiciIVA`, `Paesi`, `Anagrafica`, ecc. NON si adotta il singolare di Verbali. (Solo le *caratteristiche strutturali* si uniformano, non i nomi.)
  - **Lookup ministeriali Fatturazioni-only** (`Paesi`, `Province`, `NatureIVA`, `CondizioniPagamento`, `ModalitaPagamento`): mantengono le loro chiavi naturali/attuali (non si fondono con Verbali, le anagrafiche le referenziano per codice naturale).
- **Retrofit**: la verticale `Anagrafica` già esistente va riscritta a GUID + `IsAttivo`. `Utenti` e `LogErrors` NON si retrofittano a parte: confluiscono nei rispettivi step di mirror (auth e logging) per non produrre lavoro usa-e-getta. *(Step logging completato: `LogErrors` → `fatt.Log`/`fatt.Audit`, vedi Regola 6.)*

### Schema database (D5, D6, D9, D19, D20)

- **Tabelle "fisse" Agenzia Entrate** (Nature IVA, Condizioni/Modalità pagamento, Tipologie clientela): tabelle SQL vere, popolate da seed **idempotente** (`MERGE` o `IF NOT EXISTS`) in `Migrations/`. Niente enum C# come unica fonte.
- **DB di partenza vuoto**: nessuna importazione dei dati storici dall'Access. L'app nasce pulita.
- **Lookup di partenza**: schema + dati delle 5 tabelle di lookup sono in `TabelleLookupMancanti.sql` (root del progetto). In Fase 1 vanno importati nello schema applicativo (`fatt.*`, ADR D21) e il tipo user-defined `dbo.DataRecord` va ridichiarato (`CREATE TYPE dbo.DataRecord FROM DATETIME NOT NULL`) **oppure** normalizzato a `DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()` colonna per colonna. Scegliere la seconda opzione se non emergono dipendenze sul tipo custom.
- **Colonna `DataRecord` (audit timestamp)**: aggiunta **caso per caso**, non automatica. Tabelle volatili e tabelle dove l'audit serve davvero → sì. Tabelle di lookup statiche → no.
- **Campo PEC**: il nome corretto è `PECFatturaElettronica`. Il refuso `PerFatturaEletronica` presente nello schema PNG va **scartato** nel nuovo schema.

### Source of truth (D7)

In caso di conflitto fra documenti del progetto, la precedenza è:

1. `CLAUDE.md` (architettura, intoccabile)
2. `Schema database.png` (autorità su nomi tabelle/colonne/FK)
3. `FATTURAZIONE - TABELLE AMMINISTRATIVE.pdf` (autorità su UI + logica VBA)
4. `Descrizione funzionale applicativo.md` (autorità su scopo + regole business)

Se un documento più basso contraddice uno più alto, vince il più alto. Se la contraddizione sembra un bug del documento più alto, **segnalare** prima di decidere — non risolvere silenziosamente.

### Autenticazione (D4)

- Tabella `dbo.Utenti` dedicata con `Username` (unique), `PasswordHash`, `Salt` (se l'algoritmo non lo embedda), `Attivo BIT`, `DataRecord`.
- Algoritmo di hashing: **BCrypt** (libreria `BCrypt.Net-Next`) o **PBKDF2** (`Microsoft.AspNetCore.Cryptography.KeyDerivation`). Decidere alla creazione della Fase 1 (richiede approvazione NuGet — vedi Regola 1).
- Mai password in chiaro nemmeno temporaneamente. Reset password = genera token monouso, invia per email, l'utente sceglie la nuova.

### UI / MudBlazor (D10, D11, D12, D13, D15, D17, D18)

- **Icone**: Material Icons di MudBlazor (`<MudIcon Icon="@Icons.Material.Filled.Save" />`). Lucide non è usato.
- **Tabelle data-dense** (liste anagrafiche, fatture, righe documento): **`MudDataGrid`** con virtualization. `MudTable` solo per casi semplici (poche righe, layout custom).
- **Stile Material smorzato**: `DisableRipple="true"` globale + transition a `150ms` come da `brand-guidelines.md`. Niente FAB, niente animazioni di entrata. Un gestionale deve sembrare immediato.
- **Tematizzazione**: vedi `brand-guidelines.md` sezione "Implementazione con MudBlazor" — mappatura esplicita token → `MudTheme`.
- **Responsive tabelle su mobile**: **scroll orizzontale** con **prima colonna sticky** (di norma la "chiave umana" — ragione sociale, numero fattura). Niente trasformazione automatica in card-stack.
- **Dark mode**: prevista come opzione utente (toggle nelle impostazioni profilo). `PaletteDark` definita in `brand-guidelines.md` sezione "Dark mode". Persistenza preferenza utente in `dbo.Utenti` (colonna `TemaPreferito NVARCHAR(8) NOT NULL DEFAULT 'light'`).
- **Stampe ed export PDF**:
  - **Fatture (PDF di cortesia)**: seguono il **template fiscale standard** (intestazione formale, dati cedente/cessionario, tabella righe, totali, dati pagamento). Brand applicato come logo nell'intestazione + colore istituzionale nei separatori. Non seguono pedissequamente le guidelines colore/font dell'app.
  - **Report e altri export**: applicano le brand guidelines in chiave print (Inter, palette, niente ombre).

### Mockup e composizione di pagina (D16)

In assenza di wireframe forniti, il riferimento primario per la composizione delle pagine è:

1. Screenshot delle maschere Access nel PDF funzionale (`FATTURAZIONE - TABELLE AMMINISTRATIVE.pdf`)
2. Descrizioni di flusso/comportamento in `Descrizione funzionale applicativo.md`
3. Brand guidelines per la rivisitazione visiva

Se incontri un caso non coperto da nessuno dei tre, **segnalalo** prima di inventare un layout — `brand-guidelines.md` è autoritativo, non orientativo.

### Decisioni ancora aperte

(nessuna al momento — D8 chiusa il 2026-05-20 con scelta "ometti", vedi ADR)

## Roadmap di porting

Strategia in 4 fasi per costruire l'app verticalmente, senza accumulare codice non testato in UI. Ogni fase ha un checkpoint esplicito di approvazione utente prima di passare alla successiva.

### Fase 0 — Decisioni vincolanti (NESSUN CODICE) ✅

Chiusa il 2026-05-20. Output: sezioni "Convenzioni di porting" e "Roadmap di porting" in `CLAUDE.md` + `docs/decisioni-architetturali.md` (ADR) + aggiornamento di `brand-guidelines.md`.

### Fase 1 — Spina dorsale (1 commit)

> *Nota storica: descrive il piano originale di Fase 1. Diverse scelte sono poi evolute e questo elenco va letto come record del percorso, non come stato attuale — in particolare: schema unico `fatt` (ADR D21, supera `sta`/`ana`/`fat`); `dbo.Utenti` → `fatt.Utenti`; e l'impianto di logging (`003_LogErrors`/`IErrorLogger`/`dbo.LogErrors`) sostituito da `fatt.Log` + `DbLoggerProvider` + `ILogManager` (vedi Regola 6).*

Niente CRUD di dominio. Solo lo scheletro tecnico:

- Migration `001_CreateSchemas.sql` (creazione schema `sta`, `ana`, `fat`)
- Migration `002_Auth.sql` (tabella `dbo.Utenti` + indici)
- Migration `003_LogErrors.sql` (tabella di log errori — vedi Regola 6)
- Migration `004_LookupSta.sql` (import schema + dati da `TabelleLookupMancanti.sql`, normalizzati nello schema `sta`)
- Cookie auth funzionante (login/logout + middleware), utente seed hardcoded dev-only (da rimuovere prima del rilascio)
- `IErrorLogger` + `ErrorLogRepository` operativi e cablati nel middleware globale, `ErrorBoundary` di Blazor, `CircuitHandler`
- `MainLayout` + `NavMenu` (voci disabilitate tranne "Tabelle amministrative")
- `MudThemingProvider` con `IcmTheme` (Light + Dark)
- Pagina vuota `/tabelle-amministrative` con tabs per le 5 sezioni
- Redirect a `/login` se non autenticato

**Checkpoint**: app si avvia, login funziona, layout vuoto navigabile, errori finiscono in `dbo.LogErrors`, toggle dark mode funziona.

### Fase 2 — Verticale canonica: Anagrafica (3-5 commit)

**Banco di prova del pattern.** Se regge sull'entità più complessa, regge ovunque.

- Migration `005_Anagrafica.sql`
- **Decisione D8 chiusa** prima di iniziare
- `Anagrafica.cs` entity + `IAnagraficaRepository`/`AnagraficaRepository` (Dapper, schema `fatt`, JOIN LEFT verso `fatt.Paesi`/`fatt.Province` come da VBA originale)
- `IAnagraficaManager`/`AnagraficaManager` con validazioni (campi obbligatori, FK verso `fatt.*`, divieto eliminazione se ci sono dipendenze)
- Test xUnit del Manager con repository fake
- 3 pagine Blazor: elenco con `MudDataGrid` filtrabile, dialog "Aggiungi", dialog "Modifica" — pattern visibility-driven (`Modifica`/`Elimina` nascosti finché non c'è selezione)
- Verifica manuale browser desktop + responsive mobile (scroll orizzontale + sticky)

### Fase 3 — Replica del pattern (1 commit per entità)

Ordine consigliato per complessità crescente:

1. **Codici IVA** (campi condizionali Aliquota/Natura)
2. **Banche di appoggio** (due categorie: aziendali vs clienti)
3. **Tipi di pagamento → Codici di pagamento** (parent/child + utility verifica scadenza)
4. **Tipologie clientela** (tabella fissa, solo seed, niente CRUD utente)

Ogni entità eredita il pattern consolidato in Fase 2: stessa struttura Manager/Repository, stessa logica di validazione, stesso layout pagina.

## Principi operativi per l'agente

Questa sezione descrive **come Claude dovrebbe lavorare** sul progetto, non come è strutturata l'applicazione. È un piano distinto da quello architetturale: le sezioni precedenti descrivono il codice prodotto, questa descrive il processo per produrlo. Non confondere l'architettura *layered N-Tier* dell'app (UI → Manager → Repository) con i principi operativi qui sotto: sono livelli diversi.

### CLI-first

Prima di scrivere boilerplate a mano, verificare se esiste un comando `dotnet` adatto:

- `dotnet new` per scaffolding di progetti, classi, componenti
- `dotnet add reference` per referenze tra progetti
- `dotnet sln` per gestire la soluzione
- `dotnet add package` per nuovi pacchetti (**solo dopo conferma** — vedi Regole)

Generare file a mano solo se nessun comando copre il caso d'uso.

### Loop di auto-correzione

Quando qualcosa non funziona, non limitarsi a "far passare il build":

1. **Comprendere** l'errore leggendo l'output di MSBuild / lo stack trace
2. **Correggere** la causa, non il sintomo
3. **Verificare** con build e test
4. Se l'errore rivela un pattern ricorrente o un vincolo non documentato, **aggiornare questo `CLAUDE.md`** per prevenire la regressione

Esempio: un errore di Dependency Injection in `Program.cs` non si risolve solo registrando il servizio mancante. Va anche valutato se le convenzioni sulla registrazione di Manager/Repository nel DI container vadano esplicitate qui.

### Documento vivo

`CLAUDE.md` non è statico. Quando emergono:

- Nuovi pattern utili nel codice
- Vincoli scoperti sul comportamento di Dapper, Blazor Server o del cookie auth
- Decisioni architetturali ricorrenti

→ aggiornare il file in modo additivo. **Non** sovrascrivere sezioni esistenti senza motivazione esplicita: estenderle.

### Deliverable vs file intermedi

- **Deliverable** (versionati): codice C# in `src/`, script SQL in `Migrations/`, file di configurazione (`appsettings.json`, `.csproj`, `.sln`), documentazione, test.
- **Intermedi** (NON versionati, rigenerabili): cartelle `bin/`, `obj/`, `.tmp/`, log di build, output di code coverage.

Tutti i file intermedi devono poter essere ricreati da zero con `dotnet clean && dotnet build`. Se qualcosa non lo è, è un problema di processo da risolvere, non un file da preservare.

### Gestione di segreti e configurazioni

- **Mai** hardcodare stringhe di connessione, API key, password o certificati nel codice o nei `.csproj`
- In sviluppo locale: usare il **Secret Manager di .NET** (`dotnet user-secrets`) per valori sensibili; `appsettings.Development.json` solo per configurazione non sensibile
- In produzione: variabili d'ambiente o un secret store esterno (Azure Key Vault, AWS Secrets Manager, ecc.)
- `appsettings.json` versionato può contenere solo placeholder e configurazione non sensibile

### Automazione (opzionale)

Se nel tempo emergono script ricorrenti (setup DB locale, applicazione di migration in batch, seeding di dati di test, generazione di codice ripetitivo), raccoglierli in una cartella `execution/` alla root della soluzione. Ogni script deve essere:

- **Riproducibile** – stesso input produce stesso output
- **Documentato** – header con scopo, prerequisiti, esempio d'uso
- **Sicuro** – eseguibile in dry-run quando ha effetti distruttivi (drop tabelle, reset dati)

Linguaggi consigliati: **PowerShell** (coerente con l'ecosistema .NET su Windows) o **Bash/Python** se si lavora cross-platform.

### Allegati visivi (cartella Evidenze)

Lo sviluppo avviene da terminale (Claude Code), senza drag-and-drop diretto delle immagini. Le risorse visive utili allo sviluppo — mockup, screenshot di errori, reference grafiche — risiedono nella cartella locale:

`C:\SVILUPPO\GIT\Evidenze\`

Quando in un prompt compaiono riferimenti come *"guarda in evidenze"*, *"controlla la cartella evidenze"* o il nome di un file specifico (es. `image1.png`), accedere autonomamente al percorso corrispondente (es. `C:\SVILUPPO\GIT\Evidenze\image1.png`), aprire il file e analizzarlo prima di formulare la risposta.

### Tracciamento del processo e documentazione finale

Mantenere memoria continua e dettagliata del processo di sviluppo: decisioni architetturali prese, alternative valutate, modifiche significative al codice, vincoli emersi durante l'implementazione. Questa memoria serve sia a evitare regressioni in corso d'opera, sia come base per la documentazione finale.

Al termine del progetto verranno richiesti **due manuali distinti**:

1. **Manuale Utente** — guida ad alto livello incentrata su interfaccia e funzionalità, priva di tecnicismi, predisposta per ospitare elementi visivi (form, report, screenshot della UI, schemi esplicativi).

2. **Manuale dello Sviluppatore** — documento tecnico approfondito ("sotto il cofano") che deve includere:
   - architettura generale e struttura del progetto
   - componenti e librerie utilizzati
   - analisi pagina per pagina, con spiegazione del codice e inclusione degli snippet più significativi
   - schema del database: elenco di tabelle, campi e relazioni

Scrivere il codice e prendere decisioni tenendo presente che dovranno essere ricostruiti e raccontati in questi due manuali.
