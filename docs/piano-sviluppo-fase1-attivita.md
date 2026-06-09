# Piano di sviluppo — Step 1: dalle tabelle amministrative alla Gestione Attività Clienti

> **Fonte primaria**: `1-Dispensa_Analisi_Gestionale_Fatturazione.pdf` (29 pp., trascrizione ragionata
> dell'incontro Roberto ↔ Luca del 2026-06-08).
> **Fonte schema dati legacy**: `AltreTabelle.sql` (DDL + seed dal DB Access/SQL `Fatturazione`).
> **Vincoli architetturali**: `CLAUDE.md` (autoritativo), `docs/decisioni-architetturali.md` (ADR).
>
> Questo documento organizza **in modo sequenziale** lo sviluppo di tutto il primo grande step
> funzionale descritto nella dispensa: dalle tabelle di supporto amministrative fino al "cuore del
> programma", la **Gestione Attività Clienti** con la nuova **schedulazione dei pagamenti**.
>
> Ogni *Tappa* eredita il pattern verticale consolidato in Fase 2 (entity → repository Dapper →
> manager con validazioni tipizzate → test xUnit con repo fake → UI Blazor/MudBlazor → migration SQL)
> e corrisponde **a un commit**.

---

## 0. Inquadramento

### 0.1 Cosa copre questo step
La dispensa descrive 13 capitoli. Questo step implementa i capitoli **2 → 10**:

| Cap. | Tema | Tappa di questo piano |
|------|------|-----------------------|
| 2 | Anagrafiche: aggiungere Pagamento / Banca / Codice IVA | Tappa 6 (integrazione) |
| 3 | Tipi e Codici di Pagamento (gerarchia padre→figlio, flag A/C) | Tappe 2 + 3 |
| 4 | Calcolo delle scadenze (fine mese, giorni aggiuntivi, rateazione) | Tappa 3 (servizio dedicato) |
| 5 | Condizioni e Modalità di pagamento (ministeriali, per XML FE) | già seedate (mig. 004) — Tappa 3 le referenzia |
| 6 | Codici IVA e Nature IVA (aliquota / aliquota zero / natura / bollo) | Tappa 1 |
| 7 | Banche di Appoggio (redesign azienda vs cliente) | Tappa 5 |
| 9 | Attività di Studio (tipi, "gestisci come", tipi dettaglio) | Tappa 7 |
| 10 | Gestione Attività Clienti (testata + dettaglio + schedulazione) | Tappe 8 + 9 + 10 |

### 0.2 Cosa è già fatto (Fasi 0-2, committate)
- **Schema applicativo unico `fatt`** creato (mig. 001 — vedi §1.1, ADR D21). Tutte le tabelle ICMFatturazioni vi risiedono.
- **Lookup ministeriali già seedate** (mig. 004): `fatt.NatureIVA` (N1-N7), `fatt.CondizioniPagamento`
  (TP01-TP03), `fatt.ModalitaPagamento` (MP01-MP22), `fatt.Paesi`, `fatt.Province`.
- **`fatt.Anagrafica`** (mig. 005) con entity/repo/manager/UI/test completi. La tabella **ha già** le
  colonne FK `IdPag`, `IdBancaAppoggio`, `IdCodiciIVA`, `IdTipologieClientela` come `INT NULL`
  **senza vincolo FK**: i vincoli verranno aggiunti quando le tabelle parent esisteranno (Tappa 6).
- `TipoAnagrafica` enum `S`/`P`/`E` (Società/Privato/Ente) — è la **segmentazione** Enti/Società/Privati
  della dispensa (Fig. 2), usata come filtro trasversale.

### 0.3 Cosa è esplicitamente RINVIATO (fuori da questo step)
- **Cap. 8 — Testi dei Moduli** (PDF avviso + e-mail precompilata): la dispensa stessa dice
  «si può soprassedere, verrà ripreso con le fatture». → fase fatture.
- **Cap. 9.4 — Studi di Settore**: non è una tabella ma viste/query verso le fatture; «modulo sospeso,
  non venduto, non mostrato al cliente». → dopo le fatture. Manteniamo solo il **flag** `StudiSettore`
  su `fatt.TipiAttivita`.
- **Cap. 11 — Collegamento al verbale di sicurezza**: «sessione dedicata». Predisponiamo solo il fatto
  che `fatt.Attivita` (testata) sarà il perno a cui i verbali si aggancieranno.

---

## 1. Decisioni da confermare PRIMA di iniziare

> Indico la mia raccomandazione; bastano conferme.

**P1 — Schema SQL applicativo: CHIUSA (2026-06-09).** Tutte le tabelle di ICMFatturazioni vivono sotto
un **unico schema `fatt`** (vedi §1.1 e ADR D21). Questo supera la vecchia ripartizione `sta`/`ana`/`fat`
ed elimina la domanda originaria su dove collocare le attività: testata, dettaglio e schedulazione
stanno in `fatt.*` come tutto il resto. Migration 001-005 e repository già rifattorizzati.

**P2 — `IdTipologieClientela` vs `TipoAnagrafica`.** L'anagrafica ha **due** concetti potenzialmente
sovrapposti: l'enum `TipoAnagrafica` (S/P/E, segmentazione di Fig. 2) e la colonna FK
`IdTipologieClientela`. La dispensa tratta la segmentazione **solo** come S/P/E. La colonna
`IdTipologieClientela` resta orfana. **Raccomando**: per ora **non** creare una tabella
`fatt.TipologieClientela`, lasciare la colonna inutilizzata (nullable, niente FK) e chiarire con il
cliente se serve davvero una classificazione fiscale separata. *Default proposto: rinviare.*

**P3 — "Descrizioni Attività standard" (cap. 9.3).** La dispensa cita un *elenco di descrizioni di
attività standard*, ordinato secondo la successione naturale del lavoro («prima il progetto, poi la
variante»), richiamabile in inserimento. Non c'è una tabella corrispondente in `AltreTabelle.sql`.
**Raccomando** una nuova tabella `fatt.DescrizioniAttivita` (`Descrizione`, `Ordine`) gestita in
Tappa 7. *Default proposto: crearla.*

*(Nota: D8 — campi `IdCodiceMerce`/`ResaMerce` — è già CHIUSA con "ometti", vedi mig. 005. Nessuna
azione.)*

### 1.1 Namespace applicativo `fatt` (ADR D21, 2026-06-09)

ICMFatturazioni e **ICMVerbali** (app già sviluppata) convergeranno su un **unico database condiviso**:
alcune entità si **fonderanno** — `Attivita`↔`Progetto`, `Anagrafica`↔`Committente`. Per questo TUTTE
le tabelle di ICMFatturazioni vivono sotto un **unico schema `fatt`** (namespace dell'app), che:
- dichiara l'ownership ("queste sono di ICMFatturazioni");
- evita collisioni col `dbo.*` di Verbali (incluse `Utenti`/`LogErrors`);
- rende la fusione futura un'operazione localizzata, non un riordino globale.

Nel contesto di ICMFatturazioni le entità mantengono i nomi **`Attivita`** e **`Anagrafica`**; in
ICMVerbali restano **`Progetto`** e **`Committente`**. La fusione è lavoro futuro.

**Tre divergenze di convenzione fra i due DB**, da risolvere con un ADR dedicato **prima** della fusione
(NON ora — solo da tenere a mente):
1. **PK**: ICMFatturazioni usa `INT IDENTITY` (fedeltà legacy Access); ICMVerbali usa `uniqueidentifier`
   (GUID). La fusione richiederà una conversione/remapping delle chiavi su un lato.
2. **Audit/soft-delete**: qui `DataRecord`; in Verbali `CreatedAt`/`UpdatedAt` + `IsAttivo`.
3. **Mapping campi**: es. `Anagrafica.PIVA` (unificato) vs `Committente.CodiceFiscale`+`PartitaIva`
   (separati); `Attivita` (testata con date/importo opera) vs `Progetto` (Codice+Nome).

---

## 2. Normalizzazioni di tipo (regole valide per tutte le migration)

`AltreTabelle.sql` viene dal DB legacy: i tipi vanno normalizzati come già fatto in mig. 004/005.

| Legacy | Nuovo | Motivo |
|--------|-------|--------|
| **PK `INT IDENTITY`** | **`uniqueidentifier` (GUID)** generato app-side con `Guid.CreateVersion7()` (UUIDv7 time-ordered), no IDENTITY/OUTPUT | **ADR D22** — uniformità con ICMVerbali |
| **`[dbo].[DataRecord]` (audit per-riga)** | **rimosso**: audit "chi-cosa" centralizzato in `fatt.Audit` | **ADR D22** (rivede D9/D19) |
| **cancellazione fisica** | **soft-delete `IsAttivo BIT NOT NULL DEFAULT 1`** sulle anagrafiche/master | **ADR D22** |
| `float` (importi: ImportoOpera, Importo, ImportoIniziale) | `DECIMAL(18,2)` | money corretto, no errori binari |
| `float` (AliquotaCodiceIVA) | `DECIMAL(5,2)` | aliquota percentuale |
| `int`/`nvarchar(1)` usati come flag (Bollo, FineMese, FlagBanca, Soggetto) | `BIT` o `CHAR(1)` + `CHECK` | semantica esplicita |
| `datetime` (date pure: ProgettoDefinitivo, Scadenza, ecc.) | `DATE` se è solo data, `DATETIME2(3)` se serve l'ora | precisione coerente |
| nomi tabella `STA-XXX` / `STA-FE_XXX` | schema `fatt.XXX`, **nome legacy italiano invariato** (no singolare Verbali) | ADR D21 + D22 |
| `idTipoXxx` (camel legacy) | proprietà C# **PascalCase italiano fedele** (`IdTipoPagamento`) | ADR D1 |

> **Eccezione lookup ministeriali Fatturazioni-only** (`fatt.Paesi`, `fatt.Province`, `fatt.NatureIVA`, `fatt.CondizioniPagamento`, `fatt.ModalitaPagamento`): mantengono le chiavi attuali (naturali/INT) — non si fondono con Verbali e le anagrafiche le referenziano per codice naturale. GUID + `IsAttivo` si applicano alle tabelle di **dominio/implementabili** (`Anagrafica`, `CodiciIVA`, `TipiPagamento`, `CodiciPagamento`, `BancheAppoggio`, `TipiAttivita`, `Attivita`, ...).

Le query SQL nei Repository usano **sempre** il nome schema-qualificato (`FROM fatt.CodiciIVA`).

---

## 3. Mappa tabelle legacy → nuovo schema

| Tabella legacy (`AltreTabelle.sql`) | Nuova tabella | Natura | Tappa |
|--------------------------------------|---------------|--------|-------|
| `STA-CodiciIVA` | `fatt.CodiciIVA` | Implementabile (CRUD) | 1 |
| `STA-FE_NatureIVA` | `fatt.NatureIVA` ✅ *(già seedata mig.004)* | Ministeriale | — |
| `STA-TipiPagamento` | `fatt.TipiPagamento` | Implementabile (CRUD) | 2 |
| `STA-CodiciPagamento` | `fatt.CodiciPagamento` | Implementabile (CRUD) | 3 |
| *(condizioni FE)* | `fatt.CondizioniPagamento` ✅ | Ministeriale | — |
| *(modalità FE)* | `fatt.ModalitaPagamento` ✅ | Ministeriale | — |
| `STA-BancheAppoggio` | `fatt.BancheAppoggio` **(redesign + IdCliente)** | Implementabile | 5 |
| `STA-TipiAttivita` | `fatt.TipiAttivita` | Implementabile | 7 |
| `STA-TipiDettaglioAttivita` | `fatt.TipiDettaglioAttivita` | Implementabile | 7 |
| *(nuova, cap. 9.3)* | `fatt.DescrizioniAttivita` | Implementabile | 7 |
| `Attivita` | `fatt.Attivita` (testata) | Operativa | 8 |
| `Attivita_Dettaglio` | `fatt.AttivitaDettaglio` | Operativa | 9 |
| *(nuova, cap. 10.4)* | `fatt.SchedulazionePagamenti` | Operativa | 10 |

Il campo `GestisciCome` di `fatt.TipiAttivita` **non** diventa tabella: è una **enumerazione C#**
(`GestisciCome { Consulenza, Progetto }`), come da cap. 9.1 / Fig. 10 (origine "Elenco valori" cablata).

---

## 4. Sequenza di sviluppo (Tappe = commit)

Ordinata per **dipendenze**: prima le tabelle parent, poi l'integrazione anagrafica, poi le attività.

```
Tappa 1  fatt.CodiciIVA          (dip: fatt.NatureIVA ✅)
Tappa 2  fatt.TipiPagamento      (nessuna dip)
Tappa 3  fatt.CodiciPagamento    (dip: TipiPagamento, Condizioni ✅, Modalità ✅) + ScadenzaCalculator
Tappa 4  UI lettura ministeriali (Condizioni/Modalità/Nature) — leggera
Tappa 5  fatt.BancheAppoggio     (redesign, dip: fatt.Anagrafica ✅) + procedura propagazione
Tappa 6  Integrazione fatt.Anagrafica (FK + selettori Pagamento/Banca/CodiceIVA + flag A/C)
─────────  ▲ chiude il cap. 2-7 della dispensa
Tappa 7  fatt.TipiAttivita + fatt.TipiDettaglioAttivita + fatt.DescrizioniAttivita
Tappa 8  fatt.Attivita           (testata, coerenza date)
Tappa 9  fatt.AttivitaDettaglio  (righe, importo studio)
Tappa 10 fatt.SchedulazionePagamenti (NUOVO requisito) + pagina "Gestione Attività Clienti"
─────────  ▲ chiude il cap. 9-10 della dispensa (il "cuore del programma")
```

Migration numerate: **006 → 015** (vedi §6).

---

### Tappa 1 — `fatt.CodiciIVA` (cap. 6)
**Migration 006_CodiciIVA.sql** — tabella + seed da `AltreTabelle.sql` (IVA 21%, IVA 22%).
Colonne: `IdCodiceIVA`, `DescrizioneCodiceIVA NVARCHAR(50)`, `AliquotaCodiceIVA DECIMAL(5,2)`,
`SiglaCodiceIVA NVARCHAR(2)`, `IdNaturaIVA INT NULL` (FK → `fatt.NatureIVA`),
`ObbligoBollo BIT NOT NULL DEFAULT 0` (legacy `Bollo`), `DataRecord`.

- **Entity** `CodiceIVA.cs`.
- **Repository** `ICodiceIVARepository`/`CodiceIVARepository` con JOIN LEFT a `fatt.NatureIVA` per la
  descrizione natura (lettura).
- **Manager** `ICodiceIVAManager`/`CodiceIVAManager` — **regola business dal cap. 6.2 (Fig. bivio)**:
  - `Aliquota > 0` ⟹ `IdNaturaIVA` **deve** essere null;
  - `Aliquota = 0` ⟹ `IdNaturaIVA` **obbligatoria** (la natura spiega *perché* è zero).
  - Eccezione tipizzata `CodiceIVAInvalidoException` con motivo (`AliquotaNonZeroConNatura`,
    `AliquotaZeroSenzaNatura`, `DescrizioneMancante`).
- **Test** xUnit: tutti i rami del bivio aliquota/natura + bollo.
- **UI**: tab "Codici IVA" in `/tabelle-amministrative` — `MudDataGrid` (Codice, Descrizione,
  Aliquota, Natura, Bollo) + dialog Aggiungi/Modifica con campo Natura **abilitato solo se aliquota = 0**
  (replica la maschera Fig. 6).
- **Done**: build pulita, test verdi, CRUD funzionante a video, bollo Sì/No persistito.

### Tappa 2 — `fatt.TipiPagamento` (cap. 3, livello padre)
**Migration 007_TipiPagamento.sql** — tabella + seed da `AltreTabelle.sql` (Bonifico `BO`/`A`,
Ricevute bancarie `RB`/`C`).
Colonne: `IdTipoPag`, `TipoPagamento NVARCHAR(50)`, `SiglaPag NVARCHAR(2)`,
`FlagBanca CHAR(1) NOT NULL CHECK (FlagBanca IN ('A','C'))`, `DataRecord`.

- **Entity** `TipoPagamento.cs` + enum `FlagBancaTipoPagamento { Azienda='A', Cliente='C' }`.
- Repo/Manager/Test/UI come da pattern.
- **Regola business (cap. 3.2)**: il `FlagBanca` decide **di chi** sono i dati bancari in fattura —
  `A` = dati banca **Azienda** (bonifico: il cliente versa sul nostro IBAN); `C` = dati banca **Cliente**
  (ricevuta bancaria: serve ABI/CAB del cliente). Qui è solo persistenza del flag; l'effetto si
  manifesta in Tappa 5/6.
- **UI**: tab "Tipi di Pagamento" con legenda del flag (A = fatture coi dati Azienda; C = dati Cliente).
- **Done**: CRUD + flag visibile e spiegato.

### Tappa 3 — `fatt.CodiciPagamento` + **ScadenzaCalculator** (cap. 3 figlio + cap. 4 + cap. 5)
È il modulo «più ostico». **Migration 008_CodiciPagamento.sql** — tabella + seed (A VISTA,
BONIFICO 60 GG F.M.).
Colonne: `IdPagamento`, `IdTipoPagamento INT` (FK → `fatt.TipiPagamento`),
`DescrPag NVARCHAR(70)`, `NumScadenze INT NOT NULL` (1..3),
`GGScad1 INT NOT NULL`, `GGScad2 INT NULL`, `GGScad3 INT NULL`, `GGpiu INT NULL`,
`FineMese BIT NOT NULL DEFAULT 0`, `IdCondizionePagamento INT NULL` (FK → `fatt.CondizioniPagamento`),
`IdModalitaPagamento INT NULL` (FK → `fatt.ModalitaPagamento`), `DataRecord`.

- **Entity** `CodicePagamento.cs`.
- **Repository** con JOIN al tipo (sigla/flag) e alle due tabelle FE (descrizioni condizione/modalità).
- **Manager** `ICodicePagamentoManager` — validazioni dal cap. 4:
  - `NumScadenze` ∈ [1,3] (limite voluto: «poi vanno gestite»);
  - i `GGScadN` valorizzati devono essere coerenti con `NumScadenze` (se 3 scadenze servono GG1/2/3);
  - `GGpiu` ammesso **solo se** `FineMese = 1` (cap. 4: «i giorni aggiuntivi presuppongono il fine mese»).
- **Servizio dedicato** `IScadenzaCalculator`/`ScadenzaCalculator` (in `Services/` o `Managers/`),
  **algoritmo dal cap. 4** (vedi §5.1 per il dettaglio): dato `dataFattura` + parametri del pagamento +
  `importoFattura`, restituisce la lista di `(numeroRata, dataScadenza, importoRata)`.
- **Test** xUnit (critici): replicare **gli esempi numerici della dispensa**:
  - 8 giu + 30 gg f.m. ⟹ 31 lug; + 10 gg aggiuntivi ⟹ 10 ago;
  - RIBA 30-60-90 f.m. su 9.000 € ⟹ 3 rate da 3.000 con scadenze 31 lug / 31 ago / 30 set;
  - test 18/06 su "Bonifico 60 gg f.m." ⟹ 31/08 (Fig. 5).
- **UI**: tab "Codici di Pagamento" — maschera "Creazione Pagamento" (Fig. 4): descrizione libera, combo
  Tipo, NumScadenze, GG prima scadenza, Fine mese Sì/No, Giorni aggiuntivi (abilitati solo se f.m.),
  combo Condizione/Modalità FE; + utility **"Test Date Scadenza"** (input data → output scadenza,
  non obbligatoria ma economica e già coperta dai test).
- **Done**: tutti gli esempi numerici della dispensa passano nei test.

### Tappa 4 — UI sola lettura tabelle ministeriali (cap. 5 + 6)
Leggera, niente CRUD utente (sono immutabili). Tab/sezioni read-only (o `MudDataGrid` senza azioni) per
`fatt.CondizioniPagamento`, `fatt.ModalitaPagamento`, `fatt.NatureIVA`. Manager di sola lettura.
Serve a "tenerle aggiornate" e a consultarle; l'aggiornamento del catalogo avverrà via migration
incrementali, non da UI. **Done**: le tre liste sono consultabili.

### Tappa 5 — `fatt.BancheAppoggio` **redesign** + propagazione (cap. 7)
La maschera legacy è dichiarata **sbagliata**: le banche dei clienti non erano legate al cliente.
**Migration 009_BancheAppoggio.sql** — modello corretto (cap. 7.3): **una sola tabella** con
`IdCliente` nullable (NULL ⟹ banca **dell'Azienda**; valorizzato ⟹ banca **di quel cliente**).
Colonne: `IdBancaAppoggio`, `IdCliente INT NULL` (FK → `fatt.Anagrafica`),
`Banca NVARCHAR(50)`, `ABI NVARCHAR(5)`, `Agenzia NVARCHAR(50)`, `CAB NVARCHAR(5)`,
`IBAN NVARCHAR(27) NULL`, `CodiceSIA NVARCHAR(10) NULL` (solo banche azienda), `DataRecord`.
Vincolo: un cliente non può avere due volte lo stesso ABI/CAB (UNIQUE filtrato su `IdCliente, ABI, CAB`).

- **Entity** `BancaAppoggio.cs` (proprietà `IsBancaAzienda => IdCliente is null`).
- **Manager** con **procedura di propagazione** (cap. 7.4) — esplicitamente «da semplificare e migliorare»:
  - alla creazione di una **nuova banca azienda** → inserisce una riga per **ogni** cliente (associa la
    banca aziendale a tutti);
  - alla creazione di un **nuovo cliente** → gli associa le banche dell'azienda.
  - Sostenibile per i volumi ridotti dello studio (≈50 clienti, non e-commerce). Implementata come
    metodo idempotente nel manager (no trigger DB), così resta testabile.
- **Logica flag A/C lato lettura** (cap. 3.3 + 7): metodo `GetBancheSelezionabiliAsync(idCliente, flag)`
  → se `flag = A` (bonifico) ritorna le **banche dell'azienda** (`IdCliente IS NULL`); se `flag = C`
  (ricevuta) ritorna le **banche di quel cliente**.
- **Test**: propagazione (N clienti ⟹ N righe), filtro A vs C, anti-duplicato ABI/CAB.
- **UI**: maschera "Banche di Appoggio" (Fig. 7) con toggle **Azienda / Clienti**; per azienda mostra
  SIA+IBAN, per cliente ABI/CAB.
- **Done**: creare una banca azienda propaga ai clienti; il filtro A/C restituisce il set giusto.

### Tappa 6 — Integrazione `fatt.Anagrafica` (cap. 2)
Chiude il "cosa manca" del cap. 2: Pagamento, Banca, Codice IVA sul soggetto.
**Migration 010_AnagraficaFK.sql** — `ALTER TABLE fatt.Anagrafica` **ADD CONSTRAINT** (additiva):
- `FK_Anagrafica_CodicePagamento (IdPag → fatt.CodiciPagamento)`
- `FK_Anagrafica_CodiceIVA (IdCodiciIVA → fatt.CodiciIVA)`
- `FK_Anagrafica_BancaAppoggio (IdBancaAppoggio → fatt.BancheAppoggio)`

- **Entity/Repo**: estendere `Anagrafica` e le query con i tre riferimenti + descrizioni in JOIN.
- **Manager**: validare che gli Id referenziati esistano; nessuna eliminazione di codice
  pagamento/banca/IVA se usato da un'anagrafica (divieto eliminazione con dipendenze).
- **UI** (`AnagraficaFormDialog`): tre selettori (Fig. 2 / Fig. 8) —
  - **Pagamento** (combo su `fatt.CodiciPagamento`) + pulsante contestuale "aggiungi al volo" un nuovo
    codice di pagamento;
  - **Banca**: combo **filtrata dal flag A/C** del tipo pagamento scelto (cap. 3.3 / 7.5):
    bonifico → banche azienda; ricevuta → banche del cliente. Uscendo dal campo pagamento, ricaricare
    l'elenco banche;
  - **Codice IVA** (combo su `fatt.CodiciIVA`) + pulsante contestuale "aggiungi al volo".
- **Vincolo di sequenza all'inserimento** (cap. 7.5, box rosso): un soggetto nuovo non ha ancora
  `IdAnagrafica`, quindi non si possono agganciare subito le coordinate cliente → **prima salvare il
  cliente**, poi aggiungere la banca. Gestire nel flusso del dialog (salvataggio in due step o id
  temporaneo).
- **Test**: aggiornare i test anagrafica; aggiungere casi su selezione pagamento→filtro banca.
- **Done**: dall'anagrafica si scelgono pagamento/banca/IVA; la banca si filtra correttamente per flag.

> ⛳ A valle della Tappa 6 i capitoli 2-7 della dispensa sono coperti end-to-end.

### Tappa 7 — Tabelle di supporto attività (cap. 9)
**Migration 011_TabelleAttivita.sql**:
- `fatt.TipiAttivita`: `IdTipoAttivita`, `TipoAttivita NVARCHAR(100)`,
  `GestisciCome NVARCHAR(20)` (persistenza dell'enum `Consulenza`/`Progetto`),
  `StudiSettore BIT NOT NULL DEFAULT 1`, `DataRecord`. Seed: CONSULENZE/Consulenza,
  PROGETTAZIONI/Progetto, ALTRO/Consulenza (da `AltreTabelle.sql`).
- `fatt.TipiDettaglioAttivita`: `IdTipoDettaglioAttivita`, `TipoDettaglioAttivita NVARCHAR(100)`,
  `DataRecord`. Seed: DISCIPLINARE, EXTRA DISCIPLINARE, VENDITA CESPITE.
- `fatt.DescrizioniAttivita` *(P3, se confermata)*: `Id`, `Descrizione`, `Ordine INT`, `DataRecord`
  (lista ordinata richiamabile in inserimento, cap. 9.3).

- **Enum** `GestisciCome { Consulenza, Progetto }` (no tabella, cap. 9.1 / Fig. 10).
- Entity/Repo/Manager/Test/UI per le tre tabelle (CRUD semplice, pattern Tappa 1).
- **Done**: le tre liste gestibili; `GestisciCome` come enum; flag StudiSettore presente.

### Tappa 8 — `fatt.Attivita` (testata) (cap. 10.2)
**Migration 012_Attivita.sql** — tabella testata nello schema `fatt` (già creato in mig. 001).
Colonne (da `AltreTabelle.sql` normalizzate): `IdAttivita`, `IdTipoAttivita INT` (FK → `fatt.TipiAttivita`),
`IdAnagrafica INT` (FK → `fatt.Anagrafica`), `DescrizioneAttivita NVARCHAR(200)`,
`NrAttivita NVARCHAR(10) NULL` (identificativo: numero o mnemonico — punto aperto col cliente),
`StatoAttivita INT NOT NULL DEFAULT 0` (workflow → enum `StatoAttivita`),
`ProgettoDefinitivo DATE NULL`, `ConcessioneEdilizia DATE NULL`, `InizioLavori DATE NULL`,
`ImportoOpera DECIMAL(18,2) NULL` (costo dell'opera, **non** il compenso studio),
`CodiceIdentificativoGara NVARCHAR(10) NULL`, `CodiceUnicoProgetto NVARCHAR(15) NULL`, `DataRecord`.

- **Manager** — **regola coerenza date (cap. 10.2, box rosso)**: ordine logico obbligatorio
  `ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori`. Eccezione tipizzata
  `AttivitaInvalidaException` (motivi: `ConcessioneAnteriorAlProgetto`, `InizioLavoriAnteriorAConcessione`,
  ...). Impedire inserimenti incoerenti.
- **UI**: selettori in alto (Anagrafica filtrabile per tipologia S/P/E, Tipo Attività, Attività esistenti)
  come Fig. 12; form testata.
- **Test**: tutte le combinazioni di incoerenza date + happy path.
- **Done**: testata salvabile solo con date coerenti.

### Tappa 9 — `fatt.AttivitaDettaglio` (cap. 10.3)
**Migration 013_AttivitaDettaglio.sql** — righe di dettaglio della testata.
Colonne: `IdAttivitaDettaglio`, `IdAttivita INT` (FK → `fatt.Attivita`),
`IdTipoDettaglioAttivita INT` (FK → `fatt.TipiDettaglioAttivita`), `Ordine INT`,
`DescrizioneDettaglio NVARCHAR(200)`, `Importo DECIMAL(18,2) NOT NULL` (**il compenso dello studio**,
«i soldi che vengono a me»), `ImportoIniziale DECIMAL(18,2) NOT NULL`, `Scadenza DATE`,
`StatoDettaglio INT NOT NULL DEFAULT 0`, `NotaDettaglio NVARCHAR(200) NULL`, `DataRecord`.
UNIQUE `(IdAttivita, Ordine)` come legacy.

- Entity/Repo/Manager/Test/UI (griglia dettagli sotto la testata, Fig. 13).
- **Done**: righe di dettaglio con importo studio collegate alla testata.

### Tappa 10 — `fatt.SchedulazionePagamenti` (**nuovo requisito chiave**, cap. 10.4) + pagina integrata
È **il requisito più importante** del nuovo sviluppo. Oggi una riga di dettaglio ha *un solo termine e un
solo importo*; serve poter **incassare in più date** lo stesso dettaglio (es. 10.000 € in 4 rate da 2.500).
L'attività resta **una sola**: la rateazione riguarda **solo i pagamenti**.

**Migration 014_SchedulazionePagamenti.sql** — sotto-tabella del dettaglio:
`IdSchedulazione`, `IdAttivitaDettaglio INT` (FK → `fatt.AttivitaDettaglio`), `NumeroRata INT`,
`DataScadenza DATE NOT NULL`, `Importo DECIMAL(18,2) NOT NULL`,
`StatoPagamento INT NOT NULL DEFAULT 0` (enum), `Nota NVARCHAR(200) NULL`, `DataRecord`.

- **Manager** `ISchedulazionePagamentiManager`:
  - la **somma delle rate** schedulate deve quadrare con l'`Importo` del dettaglio (validazione di
    coerenza, con tolleranza di arrotondamento sull'ultima rata);
  - può **riusare `ScadenzaCalculator`** (Tappa 3) per pre-popolare la schedulazione dal codice di
    pagamento del cliente (proposta automatica delle rate), poi editabile a mano;
  - CRUD delle singole rate (inserimento/modifica/eliminazione), come oggi i clienti tengono nei file Excel.
- **UI**: sotto-tabella **apribile dalla riga di dettaglio** (cap. 10.4) per gestire le rate; + la
  **pagina master "Gestione Attività Clienti"** (Fig. 12-13) che compone i tre livelli:
  selettori (Anagrafica/Tipo/Esistenti) → testata → dettagli → schedulazione.
- **Test**: quadratura somma rate = importo dettaglio; pre-popolamento da ScadenzaCalculator; CRUD rate.
- **Done**: una riga di dettaglio unica con N pagamenti datati; pagina "Gestione Attività Clienti"
  navigabile end-to-end. **Chiude il cap. 10 — il cuore del programma.**

---

## 5. Logiche di business trasversali (dettaglio)

### 5.1 Algoritmo di calcolo scadenza (cap. 4) — `ScadenzaCalculator`
Input: `dataFattura`, `numScadenze (1..3)`, `gg[] = {GGScad1, GGScad2, GGScad3}`, `fineMese (bool)`,
`ggPiu (int?)`, `importoFattura`.

Per ogni rata `i` (1..numScadenze):
1. `dataBase = dataFattura.AddDays(gg[i])` — somma dei giorni di calendario;
2. se `fineMese` ⟹ `dataBase = ultimoGiornoDelMese(dataBase)` — spostamento a fine mese;
3. se `fineMese && ggPiu > 0` ⟹ `dataBase = dataBase.AddDays(ggPiu)` — giorni aggiuntivi (solo dopo f.m.);
4. `dataScadenza[i] = dataBase`.

Ripartizione importo: `importoRata = round(importoFattura / numScadenze, 2)`; l'**ultima rata** assorbe il
resto di arrotondamento così che `Σ importoRata == importoFattura`.

Vincoli/regole:
- I giorni aggiuntivi **presuppongono** il fine mese (UI: abilitati solo se f.m. = Sì; manager: rifiuta
  `ggPiu` con `fineMese = false`).
- Le scadenze sono **tutte f.m. o tutte non f.m.** (il flag è a livello pagamento, non per rata).

Casi di test (dalla dispensa, da codificare 1:1):
| Caso | Input | Atteso |
|------|-------|--------|
| Fine mese base | 8 giu, 30 gg, f.m. | 31 lug |
| + giorni aggiuntivi | 8 giu, 30 gg, f.m., +10 | 10 ago |
| Rateazione | 1 lug, 30/60/90, f.m., 9.000 € | 3×3.000 → 31 lug / 31 ago / 30 set |
| Test Fig. 5 | 18 giu, 60 gg, f.m. | 31 ago |

### 5.2 Flag A/C e filtro banche (cap. 3.2-3.3, 7)
- Flag sul **Tipo di Pagamento**: `A` = dati banca **Azienda** (bonifico: il cliente versa sul nostro
  IBAN); `C` = dati banca **Cliente** (ricevuta bancaria: servono ABI/CAB del cliente).
- All'**applicazione su un cliente**, il campo "banca d'appoggio" si filtra:
  `A` ⟹ banche azienda (`IdCliente IS NULL`); `C` ⟹ banche di quel cliente.

### 5.3 Propagazione banche (cap. 7.4)
Volumi ridotti ⟹ procedura "costosa" ma semplice ammessa. Nuova banca azienda ⟹ riga per ogni cliente;
nuovo cliente ⟹ associazione delle banche azienda. Idempotente, nel manager (no trigger), testabile.

### 5.4 Coerenza date attività (cap. 10.2)
`ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori` (date facoltative, ma se presenti devono
rispettare l'ordine). Bloccare in inserimento/modifica.

### 5.5 Schedulazione = ripartizione dell'incasso (cap. 10.4)
La schedulazione **non duplica** l'attività: articola solo l'incasso del singolo dettaglio in N date.
`Σ rate == Importo dettaglio` (tolleranza ultima rata).

---

## 6. Riepilogo migration (numerazione progressiva)

| # | File | Contenuto |
|---|------|-----------|
| 006 | `006_CodiciIVA.sql` | `fatt.CodiciIVA` + seed (IVA 21/22%) |
| 007 | `007_TipiPagamento.sql` | `fatt.TipiPagamento` + seed (BO/A, RB/C) |
| 008 | `008_CodiciPagamento.sql` | `fatt.CodiciPagamento` + seed (A VISTA, BONIFICO 60 GG F.M.) |
| 009 | `009_BancheAppoggio.sql` | `fatt.BancheAppoggio` (modello con `IdCliente`) |
| 010 | `010_AnagraficaFK.sql` | ALTER ADD FK Pagamento/IVA/Banca su `fatt.Anagrafica` |
| 011 | `011_TabelleAttivita.sql` | `fatt.TipiAttivita` + `fatt.TipiDettaglioAttivita` + `fatt.DescrizioniAttivita` + seed |
| 012 | `012_Attivita.sql` | `fatt.Attivita` (testata) |
| 013 | `013_AttivitaDettaglio.sql` | `fatt.AttivitaDettaglio` |
| 014 | `014_SchedulazionePagamenti.sql` | `fatt.SchedulazionePagamenti` (nuovo) |

Tutte additive e idempotenti (`IF OBJECT_ID ... IS NULL`, seed `IF NOT EXISTS`), niente modifica a
migration esistenti (Regola 2 di `CLAUDE.md`). FK aggiunte solo quando i parent esistono.

---

## 7. Punti aperti da chiarire col cliente (riportati dal cap. 12.3)
- **Identificativo attività**: numero vs nomi mnemonici (`NrAttivita` resta flessibile `NVARCHAR(10)`).
- **Tabelle ministeriali**: caricare/aggiornare alle codifiche correnti AdE (Nature IVA post-2021 — es.
  N2.1/N3.1/N6.1 — non nel seed legacy: migration incrementale futura).
- **IVA estera / aliquota zero**: dettaglio gestione nature per fatturazione estera (modello già pronto).
- **Sequenza inserimento cliente**: gestione assenza `IdCliente` al primo inserimento (Tappa 6).
- **Pulsante "elimina" anagrafica**: eventuale ricollocazione (dettaglio minore di UI).
- **P2/P3** di §1 (tipologie clientela, descrizioni attività). *(P1 schema chiusa: schema unico `fatt`.)*

---

## 8. Definition of Done per l'intero step
1. Migration 006-014 applicate, DB allineato, seed ministeriali/configurazione presenti.
2. Tutte le entità con verticale completa (entity/repo/manager/test/UI) e build a **0 warning**.
3. Suite xUnit verde, inclusi i **casi numerici di calcolo scadenza** della dispensa.
4. Anagrafica con Pagamento/Banca/Codice IVA e filtro banca per flag A/C funzionante.
5. Pagina **"Gestione Attività Clienti"** navigabile: testata → dettaglio → schedulazione pagamenti.
6. Responsività verificata (desktop + mobile sticky/scroll) secondo `brand-guidelines.md`.
</content>
</invoke>
