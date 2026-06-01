# Decisioni Architetturali (ADR)

Questo documento raccoglie le decisioni architetturali del progetto **ICM Fatturazioni** in formato ADR leggero. Ogni voce descrive *contesto*, *opzioni valutate*, *scelta* e *motivazione*. Le regole operative derivate da queste decisioni sono codificate in `CLAUDE.md` (sezione "Convenzioni di porting"); qui resta il "perché", utile a futuri collaboratori e alla redazione del Manuale dello Sviluppatore.

**Formato**: ogni decisione ha un identificatore stabile (D1, D2, …) condiviso con la chat e con la memoria di lavoro dell'agente. Le decisioni successive non riusano i numeri delle precedenti, anche se abrogate.

**Convenzione di stato**:
- ✅ **Accettata** — in vigore
- ⏳ **In attesa** — non ancora chiusa, c'è un blocco esplicito (es. richiede input esterno)
- 🚫 **Superata** — sostituita da una decisione successiva (vedi link)

---

## D1 — Lingua di naming per entità C# e proprietà

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: il porting parte da un applicativo Access in italiano (PDF funzionale, schema, codice VBA) verso C#. Va deciso se preservare la lingua originale o tradurre in inglese seguendo le convenzioni .NET.

**Opzioni valutate**:
- (a) **Italiano fedele** al PDF/schema (`RagioneSociale`, `CodiceFiscale`, `Indirizzo`)
- (b) **Inglese idiomatico C#** (`BusinessName`, `TaxCode`, `Address`)

**Scelta**: (a) — italiano fedele.

**Motivazione**: preserva mappatura 1:1 con il DB Access originale e con i documenti di riferimento (PDF, schema PNG), facilita il debugging incrociato col vecchio applicativo e rispetta il dominio aziendale italiano (fatturazione, partite IVA, codici fiscali, AdE). La perdita di "idiomaticità .NET" è un costo accettabile in un gestionale di dominio italiano.

---

## D2 — Organizzazione dei nomi tabelle SQL

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: l'originale Access usa prefissi con trattino (`STA-Paesi`, `STA-Province`, ecc.) per categorizzare le tabelle. Il trattino in SQL Server richiede l'uso di parentesi quadre (`[STA-Paesi]`) in ogni query, generando rumore e rischio di errori.

**Opzioni valutate**:
- (a) **Schema SQL Server** (`sta.Paesi`, `ana.Clienti`, `fat.Fatture`)
- (b) Identici al PDF con trattino (`[STA-Paesi]`)
- (c) Underscore (`Sta_Paesi`)
- (d) PascalCase senza prefisso (`Paesi`)

**Scelta**: (a) — schema SQL Server.

**Motivazione**: gli schema sono il meccanismo nativo SQL Server per fare ciò che i prefissi Access simulavano. Vantaggi cumulativi: nessuna parentesi quadra nelle query, raggruppamento logico preservato (`sta.` → tabelle di stato), permessi gestibili a livello di schema (es. revocare `DELETE` su tutto `fat.*` a un ruolo), futura possibilità di partizionare moduli su filegroup separati. Costo: la prima migration deve creare gli schema, e tutte le query Dapper devono usare i nomi schema-qualificati (`FROM sta.Paesi`).

---

## D3 — Rappresentazione del campo `TipoAnagrafica` (S/P/E)

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: in Access è un `char(1)` con tre valori convenzionali (`S` = standard, `P` = privato, `E` = estero). Va deciso se mantenerlo stringa o promuoverlo a enum.

**Opzioni valutate**:
- (a) **Enum C#** con persistenza `char(1)`
- (b) Stringa `char(1)` come Access

**Scelta**: (a) — enum.

**Motivazione**: type-safety (il compilatore impedisce `'X'` o `'s'` minuscolo), IntelliSense, `switch` esaustivi rilevati dal compilatore. La persistenza resta `char(1)` per fedeltà al DB. Costo zero a runtime, costo basso di mapping (un attributo o un converter Dapper).

---

## D4 — Meccanismo di autenticazione utente

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: nessuna informazione nei documenti originali su come gli utenti si autentichino. L'app Access probabilmente non aveva login formale (l'accesso era controllato a livello di file/share).

**Opzioni valutate**:
- (a) **Tabella `dbo.Utenti` dedicata + password hashata** (BCrypt/PBKDF2)
- (b) Active Directory / Windows Auth
- (c) SSO aziendale (Entra ID / Azure AD)

**Scelta**: (a) — tabella `Utenti` con cookie auth ASP.NET Core.

**Motivazione**: portabilità (l'app gira ovunque, indipendente dall'infrastruttura ICM), controllo completo sui flussi (reset password, blocco account, ruoli), coerenza con lo stack già dichiarato in `CLAUDE.md` (cookie auth). Le opzioni (b) e (c) restano possibili in futuro come provider aggiuntivi, ma richiederebbero infrastruttura (AD in dominio, registrazione tenant Entra) non disponibile al momento.

---

## D5 — Tabelle "fisse" dell'Agenzia delle Entrate

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: Nature IVA, Modalità/Condizioni di pagamento e Tipologie clientela sono cataloghi semi-fissi: pubblicati dall'AdE per la fatturazione elettronica, ma soggetti a evoluzione (nuovi codici, codici deprecati).

**Opzioni valutate**:
- (a) **Tabelle SQL con seed idempotente** in `Migrations/`
- (b) Misto: tabelle SQL + enum C# di comodo
- (c) Enum C# in codice come unica fonte

**Scelta**: (a) — tabelle SQL con seed idempotente.

**Motivazione**: editabili da admin senza rebuild dell'app (un nuovo codice AdE si aggiunge con un `INSERT`, non con un deploy), joinabili nelle query, modello standard nei gestionali. Il seed idempotente (`MERGE` o `IF NOT EXISTS`) garantisce che applicare la migration su un ambiente già popolato non duplichi righe. L'opzione (c) è stata scartata perché lega l'aggiornamento del catalogo al ciclo di release dell'applicazione.

---

## D6 — Migrazione dati reali dal DB Access

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: il DB Access esistente contiene anagrafiche e dati storici di fatturazione. Va deciso se importarli nella nuova app o partire puliti.

**Opzioni valutate**:
- (a) Importare tutto in fase finale
- (b) **Partire da DB vuoto**
- (c) Importare solo alcune tabelle (es. anagrafiche)

**Scelta**: (b) — DB vuoto.

**Motivazione**: sviluppo molto più lineare, niente ETL da progettare/mantenere, nessuna preoccupazione di mapping fra strutture (in particolare con D20 che corregge il refuso PEC e con D2 che riorganizza in schema, il mapping sarebbe non banale). I dati storici restano consultabili nel vecchio Access; le anagrafiche verranno reinserite o re-importate ad-hoc se l'utente cambierà idea (decisione reversibile).

---

## D7 — Gerarchia source-of-truth fra i documenti

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: il progetto ha 4 documenti normativi (CLAUDE.md, Schema database.png, PDF funzionale, Descrizione funzionale .md). In caso di contraddizione fra due di essi, serve una regola deterministica per evitare derive interpretative.

**Opzioni valutate**: ordine proposto da agente vs altri ordini.

**Scelta**: ordine proposto, accettato dall'utente:

1. `CLAUDE.md` (architettura, intoccabile)
2. `Schema database.png` (autorità su nomi tabelle/colonne/FK)
3. `FATTURAZIONE - TABELLE AMMINISTRATIVE.pdf` (autorità su UI + logica VBA)
4. `Descrizione funzionale applicativo.md` (autorità su scopo + regole business)

**Motivazione**: `CLAUDE.md` riflette le decisioni esplicite di progetto (questa stessa ADR ne è parte) e prevale su qualsiasi documento descrittivo del legacy. Lo schema PNG è la forma più strutturata della verità sullo schema. Il PDF cattura il comportamento implementato (codice VBA), che batte la descrizione di alto livello (.md) in caso di divergenza. La regola è "il livello più alto vince, e una contraddizione apparente del più alto va **segnalata**, non risolta in silenzio".

---

## D8 — Campi `IdCodiceMerce` e `ResaMerce` in `Anagrafica`

**Stato**: ✅ Accettata (2026-05-20, chiusura prima di Fase 2)

**Contesto**: i due campi compaiono nello schema PNG come parte di `Anagrafica` ma non sono né documentati nel funzionale né riferiti dal VBA delle pagine di amministrazione. Sembrano FK verso un modulo "Merci" appartenente a un altro perimetro applicativo non in scope.

**Opzioni valutate**:
- (a) Replicare fedelmente come colonne nullable (anche se inutilizzate)
- (b) Includere nello schema ma nascondere dalla UI
- (c) Omettere finché non emerge il modulo merci

**Scelta**: (c) — omettere.

**Motivazione**: ICM Solutions opera nel settore *Industrial Construction Management* (progetti/prestazioni), non in compravendita di merci. La descrizione funzionale (testimonianza diretta dell'utente del legacy) non menziona mai questi campi né li mostra nelle maschere demo. Tenerli nello schema produrrebbe colonne sempre `NULL` e indurrebbe un consumatore futuro a chiedersi "a cosa servono". Se in futuro emerge un modulo Merci, l'aggiunta è una migration **additiva** (nullable + FK), non distruttiva — quindi il costo di rinviare è zero. Questa scelta deroga in modo cosciente all'autorità dello schema PNG (D7 #2), perché la descrizione funzionale (D7 #3) supportata dalla testimonianza dell'utente segnala una probabile vestigialità.

---

## D9 — Tabelle lookup mancanti dallo schema PNG

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: lo schema PNG referenzia 5 tabelle (`STA-Paesi`, `STA-Province`, `STA-FE_NatureIVA`, `STA-FE_CondizioniPagamento`, `STA-FE_ModalitaPagamento`) come FK ma non le disegna. Necessario decidere se ricostruirle da fonti pubbliche (ISO/ISTAT/AdE) o partire da un export del DB Access reale.

**Opzioni valutate**:
- (a) Ricostruzione da zero da fonti ufficiali
- (b) **Export SQL/CSV dal DB Access** fornito dall'utente

**Scelta**: (b) — l'utente ha fornito il file `TabelleLookupMancanti.sql` (root del progetto) contenente schema + dati reali.

**Motivazione**: ricostruzione 1:1 dei contenuti già in uso in azienda, zero discrepanze col vecchio applicativo, copre eventuali personalizzazioni locali ICM (es. paesi non standard, province aggiornate). In Fase 1 il file va importato adattando i nomi al nuovo schema `sta.*` (vedi D2) e gestendo il tipo user-defined `dbo.DataRecord` (vedi nota in CLAUDE.md "Schema database").

---

## D10 — Libreria icone in MudBlazor

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: le brand guidelines indicavano **Lucide**, ma il progetto userà MudBlazor che integra nativamente **Material Icons / Symbols**. Forzare Lucide richiederebbe SVG inline manuali e perdita di IntelliSense.

**Opzioni valutate**:
- (a) **Material Icons** (default MudBlazor)
- (b) Lucide via SVG inline

**Scelta**: (a) — Material Icons.

**Motivazione**: zero attrito di integrazione, IntelliSense completo (`Icons.Material.Filled.Save`, ecc.), copertura ampissima, coerenza visiva con il resto dei componenti MudBlazor. La differenza stilistica con Lucide è minima per le icone gestionali tipiche (save, edit, delete, search, filter). Le brand guidelines vengono aggiornate per ammettere Material come set ufficiale del progetto.

---

## D11 — Componente tabella per viste data-dense

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: l'app è data-dense (anagrafiche, fatture, righe documento). Va scelto il componente principale tra le due opzioni MudBlazor.

**Opzioni valutate**:
- (a) **`MudDataGrid`** (virtualization, filtri/ordinamento/raggruppamenti integrati)
- (b) `MudTable` (più semplice, template manuale)

**Scelta**: (a) — `MudDataGrid` come default per le liste principali.

**Motivazione**: copre l'80% dei casi senza scrivere logica, virtualization essenziale per liste lunghe (anagrafiche aziendali tipicamente >1000 righe), export e filtri integrati riducono il codice di pagina. `MudTable` resta disponibile per casi semplici (poche righe, layout custom — es. righe di dettaglio di una fattura aperta). La customizzazione visiva per allinearsi alle guidelines è onere accettabile, fatta una volta nel tema.

---

## D12 — Comportamenti Material di default (ripple, animazioni)

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: MudBlazor di default usa il ripple effect sui click e animazioni Material standard (200-300ms). Le brand guidelines invocano "sobrietà professionale", "transizioni 150ms", "un gestionale deve sembrare immediato".

**Opzioni valutate**:
- (a) **Smorzare** per coerenza con la sobrietà
- (b) Tenere i default Material

**Scelta**: (a) — smorzare globalmente.

**Motivazione**: il ripple è percepito come "consumer/playful" ed è incompatibile con il posizionamento ICM (industrial construction management, gestionale interno). La transition a 150ms migliora la percezione di immediatezza richiesta dalle guidelines. Implementazione: parametro globale `DisableRipple="true"` sul `MudThemingProvider` + override CSS della durata transition.

---

## D13 — Sezione "Implementazione con MudBlazor" nelle brand guidelines

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: le brand guidelines erano scritte in chiave Tailwind, ma con MudBlazor il modello di applicazione cambia (`MudTheme`, `Palette`, `Typography`, non utility CSS). Senza un ponte esplicito, ogni sessione rischia di reinventare la traduzione.

**Opzioni valutate**:
- (a) **Aggiungere sezione "Implementazione con MudBlazor"** che mappa i token al `MudTheme`
- (b) Lasciare le guidelines agnostiche e gestire la traduzione solo nel codice `Theme/IcmTheme.cs`

**Scelta**: (a) — aggiungere la sezione.

**Motivazione**: rende le guidelines "eseguibili": chiunque legga il file vede esattamente quale proprietà MudBlazor corrisponde a quale token brand. Elimina ambiguità nelle prossime sessioni e per futuri collaboratori. Costo: una piccola duplicazione concettuale fra documento e codice, mitigata dal fatto che la sezione è strutturata come mappatura (non come narrativa).

---

## D14 — Logo e asset visivi

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: serve il logo ICM Solutions per topbar, favicon, login page, PDF.

**Opzioni valutate**: cartella `Evidenze` / cartella dedicata in repo / placeholder iniziale.

**Scelta**: file `Logo.png` presente in `C:\SVILUPPO\GIT\Evidenze\`, da copiare in `wwwroot/img/` in Fase 1.

**Da fare in seguito**: se in futuro servono varianti (orizzontale/verticale, su sfondo scuro per dark mode, vettoriale SVG per print/PDF), chiedere all'utente di fornirle. Il PNG attuale è sufficiente per partire.

---

## D15 — Responsività delle tabelle data-dense su mobile

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: le tabelle del gestionale hanno 8-12 colonne; su uno smartphone (<768px) non ci stanno fisicamente.

**Opzioni valutate**:
- (a) **Scroll orizzontale** + prima colonna sticky
- (b) Collapse automatico in card-stack sotto un breakpoint
- (c) Mobile non è un requisito reale

**Scelta**: (a) — scroll orizzontale + colonna chiave sticky.

**Motivazione**: preserva la densità informativa e la coerenza fra desktop e mobile (stesso layout strutturale), pattern familiare agli utenti gestionali, è il default naturale di `MudDataGrid`. La (b) richiederebbe template alternativi per ogni grid (snaturando "coerenza > creatività"). La (c) è troppo restrittiva: il browser mobile va comunque garantito (visita rapida fuori sede).

---

## D16 — Mockup di pagine tipiche

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: le brand guidelines coprono i componenti atomici ma non la composizione di pagine complete (dashboard, lista fatture, scheda anagrafica).

**Opzioni valutate**: wireframe forniti / nessuno (basarsi sul PDF) / produrli a richiesta.

**Scelta**: nessun wireframe esistente, ci si baserà sui **screenshot delle maschere Access nel PDF funzionale** per layout e ordine dei campi.

**Motivazione**: il PDF documenta in modo accurato la composizione storica delle viste; la rivisitazione visiva applica le brand guidelines (palette, tipografia, spaziature). Se durante l'implementazione emerge un caso non coperto da nessun documento, l'agente deve **segnalarlo** prima di inventare un layout (regola in `CLAUDE.md`).

---

## D17 — Dark mode

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: le brand guidelines parlano solo di Light Mode. Valutare se prevedere anche il tema scuro.

**Opzioni valutate**:
- (a) Solo Light Mode
- (b) **Light + Dark con toggle utente**

**Scelta**: (b) — dark mode prevista come opzione utente.

**Motivazione**: scelta dell'utente, va estesa la guideline che oggi non descrive una palette dark coerente con il brand. La bozza di palette dark è in `brand-guidelines.md` sezione "Dark mode" (deriva `icm-navy-900` come superficie base, schiarisce i toni del blu per garantire contrasto WCAG su sfondo scuro, riusa identica la palette semantica). Persistenza della preferenza utente in `dbo.Utenti.TemaPreferito`.

---

## D18 — Stampe ed export PDF

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: l'app dovrà produrre fatture cartacee e report PDF. Va deciso se applicarvi le brand guidelines o usare convenzioni specifiche.

**Opzioni valutate**:
- (a) Stesse guidelines in chiave "print"
- (b) **Template fiscale standard per le fatture**
- (c) Affrontarlo dopo

**Scelta**: (b) — le fatture seguono il template legale/fiscale standard; brand applicato come logo + colore istituzionale nei separatori.

**Motivazione**: la fattura elettronica è regolata da specifiche AdE (XML obbligatorio) e il PDF di cortesia per il cliente segue convenzioni del settore (intestazione formale cedente/cessionario, tabella righe, riepilogo IVA, dati pagamento). Imporre le brand guidelines visive rigide darebbe un PDF "fuori standard" rispetto a quello che il cliente si aspetta. I **report interni** invece (esportazioni, statistiche) applicheranno le brand guidelines in chiave print (Inter, palette, niente ombre).

---

## D19 — Pattern colonna `DataRecord` (audit timestamp)

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: l'originale Access ha la colonna `DataRecord` (timestamp ultima modifica/creazione) su molte ma non tutte le tabelle. Decidere se renderla uniforme nel nuovo schema.

**Opzioni valutate**:
- (a) Default su tutte le tabelle (uniformità)
- (b) **Aggiungerla solo dove serve**, caso per caso

**Scelta**: (b) — caso per caso.

**Motivazione**: alcune tabelle (lookup statiche tipo `sta.Paesi`) non hanno bisogno di un audit timestamp che non verrà mai aggiornato. Aggiungerla "per uniformità" è premature optimization e occupa spazio senza beneficio. Sulle tabelle volatili (anagrafiche, fatture) resta obbligatoria. Si valuta tabella per tabella nelle migration.

---

## D20 — Refuso `PerFatturaEletronica` vs `PECFatturaElettronica`

**Stato**: ✅ Accettata (2026-05-20)

**Contesto**: il campo PEC nell'`Anagrafica` appare con due nomi diversi nei documenti: lo schema PNG dice `PerFatturaEletronica` (con doppio refuso: `Per` invece di `PEC`, `Eletronica` invece di `Elettronica`), il codice VBA dice `PECFatturaElettronica`. Diversi.

**Opzioni valutate**:
- (a) **`PECFatturaElettronica`** (nome corretto)
- (b) `PerFatturaEletronica` (refuso conservato per compatibilità)
- (c) Verificare in produzione prima di decidere

**Scelta**: (a) — `PECFatturaElettronica`.

**Motivazione**: il VBA è la fonte affidabile (il PNG è una trascrizione successiva, con refusi di battitura). Coerente con D6 (DB vuoto, nessuna importazione), non c'è alcuna compatibilità da preservare. Trascinare un refuso per sempre solo per fedeltà a un documento errato sarebbe debito tecnico ingiustificato.

---

## Aggiornamento e governance

- Le nuove decisioni si aggiungono in fondo, **non si riusano i numeri**.
- Se una decisione viene rivista, si crea una nuova ADR che cita la precedente e la marca 🚫 **Superata** con link bidirezionale.
- L'identificatore `D<N>` è stabile e citabile in commit, PR e codice (es. `// vedi ADR D19`).
- Mantenere questo documento aggiornato è un'attività del progetto, non un nice-to-have. Ogni decisione architetturale presa in conversazione con l'agente va riportata qui prima di chiudere la sessione.
