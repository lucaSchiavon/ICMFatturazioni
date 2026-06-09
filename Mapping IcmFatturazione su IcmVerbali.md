# Modulo Fatturazione — Tabelle Amministrative

> Documentazione funzionale ricavata dalla trascrizione fedele della
> registrazione `2026-05-15 15-06-13.mp4` (durata 16:42). Il contenuto è
> stato riorganizzato per la lettura e l'uso come specifica, mantenendo
> tutte le informazioni e gli esempi forniti dal narratore.

---

## 1. Inquadramento generale del programma

Il programma si chiama **Fatturazione**, ma in realtà non si limita alla sola
emissione delle fatture: la fatturazione è solo l'ultimo anello di una
catena più ampia. Il programma:

1. **Gestisce le anagrafiche** dei clienti, ovvero i soggetti che riceveranno
   la fattura.
2. **Crea attività, progetti e proposte di progetto** riferiti a questi
   clienti per una particolare attività da svolgere (progettazione di un
   edificio, ristrutturazione, ecc.).
3. **Gestisce le attività dei consulenti esterni** che operano nei
   progetti.
4. Una volta inseriti i dati necessari — quindi associato un cliente a un
   progetto, con tutte le informazioni relative — è in grado di
   **generare gli avvisi di fattura**: documenti inviati al cliente per
   avvisarlo che deve pagare una certa prestazione o un avanzamento di
   lavori.
5. Una volta **ricevuto il pagamento**, viene emessa la **fattura** vera e
   propria.

Questo è il flusso complessivo, visto da lontano, del programma
Fatturazione.

---

## 2. Le Tabelle Amministrative

Il focus di questa documentazione è sulle **Tabelle Amministrative**: è il
primo passo che bisogna compiere per poter utilizzare il programma. Senza
almeno un'anagrafica cliente, il programma non ha motivo di funzionare.

Le tabelle amministrative comprendono:

- Anagrafiche clienti
- Codici IVA
- Codici di pagamento (con i relativi Tipi di pagamento)
- Banche di appoggio (dell'azienda e dei clienti)
- Tipologie clientela

---

## 3. Anagrafiche clienti

### 3.1 Maschera principale (elenco)

La maschera **Anagrafiche** mostra tutte le anagrafiche inserite (nella
demo si tratta di dati reali di un'azienda esistente).

**Tipologie di anagrafica.** Le anagrafiche sono memorizzate
suddividendole in tre gruppi:

- **Enti pubblici**
- **Società**
- **Privati**

La distinzione è necessaria perché ha risvolti di natura fiscale e IVA
nel momento dell'emissione dell'avviso o della fattura.

**Filtro di ricerca.** Nella maschera è presente un filtro che permette di
trovare rapidamente il cliente (o i clienti) di interesse, dato che
l'elenco completo sarebbe naturalmente molto lungo.

**Visualizzazione dei dati.** Cliccando sulla riga a sinistra
dell'elenco, la parte destra della maschera si popola con i dati del
cliente selezionato: ad esempio, per un privato, nome e cognome,
indirizzo, codice fiscale, e — se presenti — la tipologia di pagamento
applicata e l'eventuale banca di appoggio.

### 3.2 Inserimento di una nuova anagrafica

Premendo **Aggiungi** si apre una maschera dedicata.

**Campi obbligatori.** I campi obbligatori sono evidenziati con **sfondo
verde**. Se uno qualsiasi di questi campi non viene compilato, il
programma non consente di salvare il record.

Sequenza tipica di inserimento di una società (esempio della demo):

1. Selezionare il tipo (Società / Privato / Ente pubblico). Nell'esempio
   si sceglie *Società*.
2. Ragione sociale (es. "Società di test").
3. Indirizzo.
4. CAP (codice di avviamento postale).
5. Città.
6. Provincia (scelta dall'elenco).
7. **Paese**: il valore *Italia* viene proposto di default all'apertura
   della maschera, perché l'azienda lavora sostanzialmente solo con
   clienti italiani.
8. Codice fiscale / P.IVA.
9. Recapiti non obbligatori (almeno in prima fase): telefono, telefax,
   cellulare, e-mail, contatto.

**Dati per la fatturazione elettronica:**

- **Codice destinatario**: serve per l'invio dell'XML della fattura
   elettronica; è qualcosa che avviene molto più avanti nel flusso. Può
   essere impostato se il cliente lo ha comunicato.
- **PEC**: in alternativa al codice destinatario, può essere impostata
   una PEC che svolgerà la stessa funzione al momento dell'emissione
   della fattura.

**Dati amministrativi associabili:**

- **Pagamento**: scelto da un campo a tendina che mostra tutti i
   pagamenti possibili (definiti nella tabella *Codici pagamento*).
- **Banca di appoggio**: una delle banche dell'azienda (definite nella
   tabella *Banche di appoggio*).
- **Codice IVA**: l'IVA che verrà applicata in fattura per questo
   cliente.
- **Tipologia di cliente**: scelta da una **tabella fissa** (non
   modificabile dall'utente), le cui voci sono imposte dall'Agenzia
   delle Entrate. Servono al commercialista per riclassificare tutte le
   fatture emesse al cliente.

**Comportamento del pulsante Salva.**

- Il pulsante **Salva** diventa visibile quando i dati minimi
   obbligatori sono stati inseriti.
- Nel momento in cui l'utente entra in un determinato campo (es.
   *Telefono*, *Contatto*, *Paese*…), il pulsante Salva si nasconde
   temporaneamente: questo permette al programma di eseguire i
   **controlli di validità** quando l'utente esce dal campo.

**Esempio di controllo di validità.** Il campo *Paese* attinge da una
tabella che contiene tutti i paesi del mondo. Se l'utente inserisce un
paese inesistente (inventato), nel momento in cui esce dal campo il
sistema rileva l'anomalia e obbliga a correggere il dato scegliendo un
paese fra quelli presenti in elenco. Finché il dato non è valido, non è
possibile salvare il record.

### 3.3 Modifica di un'anagrafica esistente

1. Si ricerca il cliente tramite il filtro (es. "Società di test").
2. Si seleziona la riga e si preme **Modifica**.
3. Si apre una maschera dedicata di modifica con **gli stessi campi**
   della maschera di inserimento.
4. Si correggono i dati errati (nell'esempio: una tipologia clientela
   non corretta) e si preme Salva.

Per la modifica vale la **stessa logica dell'inserimento**: i dati
fondamentali (sfondo verde) devono essere tutti presenti e validi.

Se l'utente entra in modifica e non vuole salvare, può sempre uscire
tramite il **pulsante Chiudi** in alto a destra della maschera, senza
intaccare il record.

### 3.4 Eliminazione di un'anagrafica

L'eliminazione è ammessa **solo se l'anagrafica non è coinvolta in
attività successive**. Ad esempio, se per una società è stato creato un
progetto (un'attività), non sarà possibile eliminare la società finché
non viene prima eliminato il progetto.

Il controllo è automatico:

- Se l'anagrafica è cancellabile, il pulsante **Elimina** è presente.
- Se non è cancellabile, il pulsante **Elimina sparisce** e non è
   utilizzabile.

L'eliminazione, quando ammessa, toglie il record dalla relativa tabella.

---

## 4. Schema dati (SQL Server)

Sul lato base dati, il diagramma SQL Server mostra:

- Una **tabella principale** `Anagrafica`, con i propri campi.
- Diverse **tabelle satellite** collegate ad `Anagrafica` tramite
   chiavi:
  - **Banche di appoggio**
  - **Codici di pagamento**, che a loro volta hanno un "padre" — la
    tabella **Tipi di pagamento**
  - **Codici IVA**
  - **Tipologie clientela**

Le sezioni seguenti illustrano queste tabelle satellite, ciascuna gestita
tramite una maschera dedicata che — come per le anagrafiche — consente di
**aggiungere, modificare ed eliminare** dati.

---

## 5. Codici IVA

### 5.1 Maschera e dati

La maschera dei Codici IVA permette di inserire, modificare ed eliminare
codici. I campi principali:

- **Codice IVA**: a scelta dell'utente (es. `24` per introdurre l'IVA al
   24%).
- **Aliquota** (%).
- **Natura**: significativa solo quando l'aliquota è 0; è un dato che
   dovrà essere acquisito quando si genera il file XML della fattura
   elettronica.
- **Obbligo bollo su fattura**.

### 5.2 Regole di compilazione

- Se l'**aliquota è diversa da 0**, la *Natura* **non è rilevante**: il
   programma rende il campo *Natura* **non editabile**. Lo stesso vale
   per *Obbligo bollo su fattura* in questa condizione.
- Se l'**aliquota è 0** (es. nuovo codice "8a" con aliquota 0), il
   campo *Natura* viene **sbloccato** e l'operatore deve obbligatoriamente
   scegliere un'operazione tra quelle previste **dall'Agenzia delle
   Entrate**.

La tabella delle *Nature* è una **tabella fissa**, indicata
dall'Agenzia delle Entrate, e non è modificabile dall'utente.

L'**obbligo del bollo su fattura** è un'informazione che l'operatore
deve sapere (o chiedere al commercialista) nel momento in cui inserisce
una certa aliquota o un certo codice IVA.

### 5.3 Operazioni

Anche su questa maschera sono disponibili:

- **Aggiungi** un nuovo codice IVA.
- **Modifica** (es. da "8a" a "8b").
- **Elimina** se il codice IVA non serve più o è errato e non è stato
   utilizzato altrove.

---

## 6. Codici di pagamento

I codici di pagamento hanno **due livelli** (visibili anche nello schema
dei dati): `STA-TipiPagamento` (padre) e `STA-CodiciPagamento` (figli).

### 6.1 Tipi di pagamento (livello padre)

I **Tipi di pagamento** sono delle descrizioni che raggruppano una serie
di pagamenti accomunati dalla **forma tecnica**. Casi semplici visti
nella demo:

- **Bonifico** → contiene tutti i pagamenti con forma tecnica
   *bonifico*.
- **RIBA** (Ricevuta Bancaria) → contiene tutti i pagamenti con forma
   tecnica *ricevuta bancaria*.
- **Assegni** (esempio di nuova tipologia aggiunta nella demo).

Operazioni disponibili sui Tipi di pagamento:

- **Aggiungi** un tipo di pagamento (es. *Assegni*).
   Quando si aggiunge un tipo è richiesta una **sigla** e
   l'indicazione dei **dati bancari da utilizzare** (es. *dati
   dell'azienda*; per gli assegni questo dato non è particolarmente
   rilevante, ma è previsto).
- **Modifica** se è stato sbagliato qualcosa.
- **Elimina** se il tipo non serve.

### 6.2 Codici di pagamento (livello figlio)

Selezionando un tipo di pagamento (es. *RIBA*), sulla destra si vedono
tutti i codici pagamento associati a quel tipo. Da qui si può
**Aggiungere** un nuovo pagamento.

Per ogni codice pagamento si definiscono:

- **Descrizione** (campo descrittivo, es. *"RIBA 30 giorni fine
   mese"*).
- **Numero scadenze**: da **1 a 3**.
- **Giorni della prima scadenza**.
- **Flag fine mese** (sì/no). Nell'esempio la sigla `FN` nella
   descrizione sta proprio per *fine mese*.
- **Giorni aggiuntivi**: alcuni pagamenti prevedono che la scadenza,
   quando cade a fine mese, parta da fine mese **più un certo numero di
   giorni**. Caso d'uso classico: una RIBA che scadrebbe il 31 agosto o
   il 31 dicembre — spesso le aziende chiedono che, per quei mesi, le
   ricevute bancarie siano spostate al 10 del mese successivo;
   impostando "10 giorni" in questo campo, il programma sa che, quando
   la scadenza è fine mese, deve aggiungere 10 giorni alla scadenza
   calcolata.
- **Condizioni di pagamento**: scelte da una tabella **fissa**
   dell'Agenzia delle Entrate; riguardano la natura del pagamento (es.
   *pagamento completo*).
- **Modalità di pagamento**: codici dell'Agenzia delle Entrate (es.
   `MP12` per la RIBA).

Confermando i dati, il pagamento viene creato. Per ogni pagamento sono
poi disponibili:

- **Modifica** se ci sono stati errori.
- **Elimina** se il pagamento non è stato utilizzato da nessuna parte.

### 6.3 Utility di verifica scadenza

È prevista una **procedura di utilità** che permette di verificare il
funzionamento di un pagamento: data una data ipotetica di emissione
fattura, calcola la scadenza attesa secondo le regole del pagamento
selezionato.

Esempio della demo:

- Data ipotetica fattura: **15 maggio**.
- Pagamento: **RIBA 30 giorni fine mese + 10 giorni aggiuntivi**.
- Calcolo: 30 giorni fine mese → 30 giugno; +10 giorni aggiuntivi → **10
   luglio**.

Questa utility serve per **testare la correttezza dei dati** impostati
sul pagamento.

---

## 7. Banche di appoggio

Le banche di appoggio sono divise in **due categorie**:

### 7.1 Banche/appoggi bancari dell'azienda

Sono le banche con cui l'azienda intrattiene rapporti, presso le quali
l'azienda ha conti intestati.

A cosa servono: ai **bonifici**. Quando un cliente paga, deve indicare
un IBAN — che è quello di una delle banche dell'azienda — su cui
effettuare il bonifico.

### 7.2 Banche/appoggi bancari dei clienti

Servono nel momento in cui si emette una **ricevuta bancaria** (RIBA).

L'emissione di una ricevuta bancaria **obbliga** a indicare un **ABI** e
un **CAB**, dati contenuti nel codice IBAN del cliente, che identificano
**banca e filiale del cliente**. Il motivo: in questo caso è l'azienda
che "raggiunge" il cliente con la ricevuta bancaria, e il cliente deve
limitarsi a ritirarla e pagarla.

### 7.3 Operazioni

Per entrambe le categorie, come per tutte le maschere amministrative,
sono disponibili:

- **Aggiungi** nuova banca.
- **Modifica** banca esistente.
- **Elimina** banca esistente, **se** non è stata utilizzata in nessuna
   operazione: in caso contrario il pulsante *Elimina* non compare.

---

## 8. Pattern UI/comportamentali ricorrenti

Si possono ricavare dai contenuti precedenti alcuni pattern trasversali a
tutte le maschere delle tabelle amministrative. Sono utili come
riferimento implementativo.

1. **CRUD uniforme.** Ogni maschera di tabella amministrativa espone i
   pulsanti **Aggiungi**, **Modifica**, **Elimina** sulla riga
   selezionata.
2. **Campi obbligatori evidenziati in verde.** Il salvataggio è
   consentito solo se tutti i campi a sfondo verde sono compilati e
   validi.
3. **Pulsante Salva contestuale.** Il pulsante *Salva* è visibile solo
   quando l'insieme dei dati minimi è soddisfatto, e viene
   temporaneamente nascosto mentre l'utente è "dentro" un campo che
   richiede controlli all'uscita.
4. **Validazione all'uscita dal campo.** I campi che attingono da
   tabelle di lookup (Paesi, Province, Tipologie clientela, Codici
   IVA, Pagamenti, Banche, Natura IVA, Condizioni e Modalità di
   pagamento, ecc.) verificano la coerenza del valore inserito quando
   l'utente lascia il campo. Un valore non presente in tabella è
   un'**anomalia** che blocca il salvataggio finché non è corretto.
5. **Tabelle fisse vs tabelle utente.** Alcune tabelle sono
   **modificabili** dall'utente (Anagrafica, Banche di appoggio,
   Codici IVA, Codici e Tipi di pagamento). Altre sono **fisse**,
   imposte dall'Agenzia delle Entrate e non modificabili (Tipologie
   clientela, Natura IVA, Condizioni e Modalità di pagamento).
6. **Eliminazione vincolata all'integrità referenziale.** Il record può
   essere eliminato solo se non è referenziato da entità "a valle"
   (es. progetti che usano l'anagrafica, fatture che usano un codice
   IVA, ecc.). Quando l'eliminazione non è ammessa, il pulsante
   *Elimina* è **nascosto**, non semplicemente disabilitato.
7. **Annullamento sicuro.** È sempre possibile chiudere la maschera
   senza salvare tramite il pulsante **Chiudi** in alto a destra.
8. **Campi con default ragionati.** Es. *Paese = Italia* in
   anagrafica, perché l'azienda lavora prevalentemente con clienti
   italiani.
9. **Campi condizionali.** L'editabilità di alcuni campi dipende dal
   valore di altri (es. *Natura IVA* editabile solo se
   *Aliquota = 0*; *Obbligo bollo* condizionato analogamente).
10. **Utility di test.** Almeno una funzione di utilità è prevista
    (calcolo scadenza per un pagamento) per validare la correttezza
    della configurazione delle tabelle.

---

## 9. Riepilogo delle entità del dominio

| Entità                   | Origine valori                     | Modificabile da utente | Note                                                                                       |
|--------------------------|------------------------------------|------------------------|--------------------------------------------------------------------------------------------|
| Anagrafica               | Inserita dall'utente               | Sì (CRUD)              | Tipo: Ente pubblico / Società / Privato. Collega Pagamento, Banca, Codice IVA, Tipologia.   |
| Tipologie clientela      | Tabella fissa Agenzia Entrate      | No                     | Usata dal commercialista per riclassificare le fatture.                                     |
| Paesi                    | Tabella di lookup                  | (—)                    | Validazione campo *Paese* dell'anagrafica.                                                  |
| Province                 | Tabella di lookup                  | (—)                    | Validazione campo *Provincia*.                                                              |
| Codici IVA               | Inseriti dall'utente               | Sì (CRUD)              | Aliquota, Natura (se aliquota 0), Obbligo bollo.                                            |
| Natura IVA               | Tabella fissa Agenzia Entrate      | No                     | Richiesta quando aliquota = 0.                                                              |
| Tipi pagamento           | Inseriti dall'utente               | Sì (CRUD)              | Padre dei *Codici pagamento*. Es. Bonifico, RIBA, Assegni.                                  |
| Codici pagamento         | Inseriti dall'utente               | Sì (CRUD)              | Descrizione, n. scadenze (1–3), giorni 1ª scadenza, fine mese, giorni aggiuntivi, ecc.      |
| Condizioni di pagamento  | Tabella fissa Agenzia Entrate      | No                     | Natura del pagamento (es. pagamento completo).                                              |
| Modalità di pagamento    | Tabella fissa Agenzia Entrate      | No                     | Codici tipo MP01…MP23 (es. MP12 RIBA).                                                       |
| Banche di appoggio       | Inserite dall'utente               | Sì (CRUD)              | Due sottogruppi: aziendali (bonifici in entrata) e clienti (RIBA, richiedono ABI/CAB).      |

---

## 10. Glossario rapido

- **Anagrafica**: dato anagrafico del cliente.
- **Avviso di fattura**: documento preliminare alla fattura, inviato al
   cliente per richiedergli un pagamento per una prestazione o un
   avanzamento di lavori.
- **Fattura**: emessa successivamente al ricevimento del pagamento
   (in questo flusso).
- **Codice destinatario / PEC**: identificativi per la trasmissione
   della fattura elettronica (XML).
- **RIBA**: Ricevuta Bancaria. Forma tecnica di pagamento che richiede
   ABI/CAB del cliente.
- **Bonifico**: forma tecnica di pagamento che usa l'IBAN dell'azienda.
- **ABI/CAB**: codici contenuti nell'IBAN che identificano banca e
   filiale.
- **MP12**: codice Agenzia Entrate per la modalità di pagamento RIBA.
- **STA-***: prefisso delle tabelle "statiche" / amministrative nel
   database (`STA-TipiPagamento`, `STA-CodiciPagamento`, ecc.).
committente è il cliente il progetto è testata attività