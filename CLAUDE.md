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
