# Brand Guidelines — ICM Solutions Intranet

Questo documento definisce l'identità visiva e le specifiche tecniche per l'interfaccia dell'applicativo intranet di **ICM Solutions** (anagrafiche, fatturazione, progetti, attività). È la fonte autoritativa per qualsiasi scelta estetica: colori, tipografia, componenti, layout. Tutte le specifiche sono espresse in token compatibili con **Tailwind CSS**.

## Filosofia del Design

L'interfaccia deve trasmettere **affidabilità, ordine e competenza tecnica** — coerentemente con il posizionamento ICM nel settore industrial construction management.

- **Sobrietà professionale**: nessun elemento decorativo gratuito. Ogni componente esiste per supportare un'operazione di business (consultare un'anagrafica, emettere una fattura, verificare lo stato di un progetto).
- **Leggibilità prima di tutto**: l'applicativo è data-dense (tabelle lunghe, form articolati, liste filtrabili). Contrasto del testo, spaziatura interna alle righe e gerarchia tipografica hanno priorità sull'effetto visivo.
- **Layering chiaro**: separazione delle aree di lavoro per **tonalità di superficie** (bianco / grigio molto chiaro) e bordi sottili, non per ombre marcate o riquadri pesanti.
- **Tema chiaro istituzionale**: sfondi bianchi e neutri freddi; il blu ICM e il navy sono usati con parsimonia su elementi chiave (header, azioni primarie, link, stati attivi) per non saturare la vista.
- **Coerenza > creatività**: in un gestionale la stessa azione deve apparire sempre uguale. Nessuna variante "di gusto" sui componenti già definiti qui.

## Palette Colori (Light Mode)

Le tonalità derivano direttamente dal logo ICM Solutions: il **blu industriale** delle lettere *ICM / SOLUTIONS* e il **navy profondo** della dicitura *INDUSTRIAL CONSTRUCTION MANAGEMENT*.

### Brand — Blu istituzionale (`icm-blue`)
Scala derivata dal blu primario `#245F8C`. Da usare per azioni primarie, link, stati selezionati, header.

| Token | Hex | Utilizzo |
| --- | --- | --- |
| `icm-blue-50` | `#EEF4FA` | Sfondi sottilissimi di evidenziazione (riga tabella selezionata, hover su voci di menu). |
| `icm-blue-100` | `#D6E4F1` | Sfondo di badge informativi, chip di filtro attivi. |
| `icm-blue-200` | `#AEC8E2` | Bordi di campi focus, separatori accentuati. |
| `icm-blue-300` | `#7FA7CC` | Stati disabilitati di elementi primari. |
| `icm-blue-500` | `#245F8C` | **Primary**. Bottoni di azione, link, icone attive, indicatori. |
| `icm-blue-600` | `#1D4E73` | Hover di `icm-blue-500`. |
| `icm-blue-700` | `#163D59` | Active / pressed. |

### Brand — Navy strutturale (`icm-navy`)
Riservato a header applicativi, titoli principali, testo ad alta gerarchia. Mai usato come fill di un bottone primario (lo è `icm-blue-500`).

| Token | Hex | Utilizzo |
| --- | --- | --- |
| `icm-navy-700` | `#2A3A52` | Testi forti su sfondo chiaro, etichette di colonna in tabelle. |
| `icm-navy-900` | `#1E2A3B` | Header dell'applicazione, titoli H1/H2, sidebar attiva. |

### Neutri (`gray`)
Scala fredda coerente con i toni del brand. Sostituisce i grigi caldi: non usare neutrali con sottotono giallo o rosa.

| Token | Hex | Utilizzo |
| --- | --- | --- |
| `gray-50` | `#F7F9FB` | **Background principale** dell'app. |
| `gray-100` | `#EEF2F6` | Superfici secondarie (toolbar, footer di tabella, zebra-stripe opzionale). |
| `gray-200` | `#E2E8F0` | **Bordi standard** (card, input, divisori di tabella). |
| `gray-300` | `#CBD5E1` | Bordi accentuati, separatori verticali. |
| `gray-400` | `#94A3B8` | Icone non attive, placeholder. |
| `gray-500` | `#64748B` | Testo secondario, metadati, label di form. |
| `gray-600` | `#475569` | Testo di corpo. |
| `gray-700` | `#334155` | Testo enfatizzato. |
| `gray-900` | `#0F172A` | Testo massima gerarchia (fallback al posto di `icm-navy-900` in contesti neutrali). |
| `white` | `#FFFFFF` | **Surface** di card, modali, righe di tabella. |

### Semantici
Usati esclusivamente per comunicare **stato** (badge, alert, validazione). Mai per decorare.

| Token | Hex | Utilizzo |
| --- | --- | --- |
| `success-50` | `#ECFDF5` | Sfondo badge / alert di successo. |
| `success-500` | `#16A34A` | Testo / icona / bordo di successo (fattura pagata, progetto completato). |
| `success-700` | `#15803D` | Testo di successo su sfondo `success-50`. |
| `warning-50` | `#FFFBEB` | Sfondo badge / alert di attenzione. |
| `warning-500` | `#D97706` | Testo / icona di attenzione (fattura in scadenza, anomalia non bloccante). |
| `warning-700` | `#B45309` | Testo di attenzione su sfondo `warning-50`. |
| `danger-50` | `#FEF2F2` | Sfondo badge / alert di errore. |
| `danger-500` | `#DC2626` | Testo / icona di errore (fattura scaduta, validazione fallita). |
| `danger-700` | `#B91C1C` | Testo di errore su sfondo `danger-50`. |
| `info-50` | `#EEF4FA` | Coincide con `icm-blue-50`. |
| `info-500` | `#245F8C` | Coincide con `icm-blue-500`. Le info istituzionali usano il brand. |

> **Regola d'oro**: non introdurre tonalità fuori da questa palette. Se manca un caso (es. categoria con colore distintivo, grafico multi-serie), segnalarlo e chiedere prima di scegliere arbitrariamente.

## Tipografia

- **Font Family principale**: `Inter` — scelta per leggibilità su tabelle dense, supporto completo dei segni diacritici italiani, hinting eccellente alle dimensioni piccole tipiche di un gestionale.
- **Font tabellare per cifre**: applicare `font-variant-numeric: tabular-nums` (utility Tailwind: `tabular-nums`) su importi, date, codici e quantità in tabella, per garantire l'allineamento verticale delle cifre.
- **Font monospaziato**: `JetBrains Mono` per codici fiscali, partite IVA, identificativi tecnici, anteprime di codice.
- **Fallback**: `system-ui, -apple-system, "Segoe UI", sans-serif`.

### Scala tipografica

| Token | Size / Line-height | Peso | Uso |
| --- | --- | --- | --- |
| `text-xs` | `12px / 16px` | 500 | Label di form, metadati di tabella, hint. |
| `text-sm` | `13px / 20px` | 400 | **Corpo standard** dell'applicazione (densità gestionale). |
| `text-base` | `15px / 22px` | 400 | Corpo testi lunghi, descrizioni in dettaglio. |
| `text-lg` | `17px / 24px` | 500 | Titoli di card, sezioni di form. |
| `text-xl` | `20px / 28px` | 600 | Titoli H2 (es. "Anagrafica cliente"). |
| `text-2xl` | `24px / 32px` | 600 | Titoli H1 di pagina (es. "Elenco fatture"). |

### Gerarchia e colore

- **Titoli**: `icm-navy-900`, peso 600.
- **Corpo principale**: `gray-700`, peso 400.
- **Testo secondario / metadati**: `gray-500`, peso 400.
- **Link**: `icm-blue-500`, peso 500, `underline` solo su hover.
- **Codici / valori monospaziati**: `gray-700` su sfondo `gray-100`, `text-xs`, padding `2px 6px`, `rounded-md`.

## Spaziatura e Layout

- **Sistema base**: multipli di **4px** (token Tailwind nativi: `1` = 4px, `2` = 8px, `3` = 12px, `4` = 16px, `6` = 24px, `8` = 32px).
- **Padding di card / pannelli**: `p-6` (24px). Per pannelli piccoli o nidificati: `p-4` (16px).
- **Padding di celle tabella**: `px-4 py-3` (16px × 12px) — densità leggibile senza spreco.
- **Padding di righe form**: `py-2` tra label e input, `gap-y-5` (20px) tra campi consecutivi.
- **Gap tra card in griglia**: `gap-4` (16px).
- **Larghezza massima dei contenitori di pagina**: `max-w-screen-2xl` (1536px). Form di dettaglio: `max-w-3xl` (768px) per evitare righe troppo lunghe.
- **Sidebar**: larghezza fissa `256px` (`w-64`); collassata `64px` (`w-16`).
- **Topbar / header app**: altezza `56px` (`h-14`).

## Border Radius e Ombre

L'arrotondamento è **misurato**: niente forme troppo morbide, non adatte a un gestionale.

| Token | Valore | Uso |
| --- | --- | --- |
| `rounded` | `4px` | Badge, tag, chip piccoli. |
| `rounded-md` | `6px` | **Default** per input, bottoni, select. |
| `rounded-lg` | `8px` | Card, modali, pannelli, alert. |
| `rounded-full` | `9999px` | Solo per avatar e icone-status circolari. **Mai** per bottoni d'azione. |

### Ombre

Discrete, mai drammatiche. La separazione visiva avviene prima per bordo e poi per ombra.

- `shadow-sm` (Tailwind default): card statiche su sfondo `gray-50`.
- `shadow-md`: dropdown, popover, tooltip elaborati.
- `shadow-lg`: modali. Accompagnata da overlay `bg-slate-900/40`.

## Componenti UI

### Bottoni

Altezza standard `36px` (`h-9`), padding orizzontale `px-4`, font `text-sm` peso `500`, `rounded-md`, transizione `150ms`.

- **Primary** — azione principale della vista (es. *Salva*, *Crea fattura*).
  `bg-icm-blue-500 text-white hover:bg-icm-blue-600 active:bg-icm-blue-700 disabled:bg-icm-blue-300`
- **Secondary** — azione alternativa (es. *Annulla*).
  `bg-white text-gray-700 border border-gray-300 hover:bg-gray-50 active:bg-gray-100`
- **Ghost** — azioni in toolbar o tabelle, solo icona o testo.
  `bg-transparent text-gray-600 hover:bg-gray-100 hover:text-gray-900`
- **Danger** — azioni distruttive (es. *Elimina*).
  `bg-danger-500 text-white hover:bg-danger-700`
- **Link button** — azioni di navigazione inline.
  `bg-transparent text-icm-blue-500 hover:text-icm-blue-600 hover:underline`

Per il **size small** (`h-8 px-3 text-xs`) usare solo in toolbar o cell action. Per il **size large** (`h-11 px-6 text-base`) solo in form di onboarding o azioni a tutta pagina.

### Input, select, textarea

- Altezza `36px` (`h-9`), `rounded-md`, `bg-white`, bordo `border border-gray-300`, testo `text-sm text-gray-900`, placeholder `text-gray-400`.
- Focus: `focus:border-icm-blue-500 focus:ring-2 focus:ring-icm-blue-200 focus:outline-none`.
- Errore: `border-danger-500 focus:ring-danger-200`. Messaggio di errore sotto il campo, `text-xs text-danger-700 mt-1`.
- Disabilitato: `bg-gray-100 text-gray-500 cursor-not-allowed`.
- Label sopra il campo: `text-xs font-medium text-gray-600 mb-1`.
- Hint sotto il campo: `text-xs text-gray-500 mt-1`.

### Tabelle

Cuore dell'applicativo. Specifiche rigorose.

- **Header**: `bg-gray-50`, testo `text-xs font-semibold uppercase tracking-wide text-gray-500`, padding `px-4 py-3`, bordo inferiore `border-b border-gray-200`.
- **Righe**: `bg-white`, bordo inferiore `border-b border-gray-100`, testo `text-sm text-gray-700`, padding `px-4 py-3`.
- **Hover riga**: `hover:bg-gray-50`.
- **Riga selezionata**: `bg-icm-blue-50`.
- **Zebra stripe** (opzionale, solo per tabelle molto lunghe senza interazione): righe pari `bg-gray-50`.
- **Cifre, importi, date**: classe `tabular-nums`, allineamento `text-right` per importi e quantità.
- **Cell actions** (icone in coda alla riga): visibili solo on hover della riga (`opacity-0 group-hover:opacity-100`).

### Card e Pannelli

`bg-white border border-gray-200 rounded-lg p-6 shadow-sm`. Titolo della card: `text-lg font-semibold text-icm-navy-900`, separato dal contenuto da `mb-4`.

### Modali

- Overlay: `fixed inset-0 bg-slate-900/40 backdrop-blur-sm`.
- Dialog: `bg-white rounded-lg shadow-lg max-w-lg p-6`.
- Header del modal: titolo `text-lg font-semibold text-icm-navy-900` + bottone close ghost in alto a destra.
- Footer del modal: bottoni allineati a destra, `gap-2`, ordine `[Secondary][Primary]`.

### Badge di stato

Pillole compatte per comunicare lo stato in tabelle e dettagli. `inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium`.

| Stato | Classi |
| --- | --- |
| Pagata / Completato / Attivo | `bg-success-50 text-success-700` |
| In scadenza / In attesa | `bg-warning-50 text-warning-700` |
| Scaduta / Bloccato / Errore | `bg-danger-50 text-danger-700` |
| Bozza / Neutro | `bg-gray-100 text-gray-600` |
| Info / In corso | `bg-icm-blue-50 text-icm-blue-700` |

Quando opportuno, anteporre un piccolo cerchio colorato (`w-1.5 h-1.5 rounded-full bg-[currentColor]`) per migliorare la scansione visiva nelle liste.

### Navigazione

- **Sidebar**: sfondo `bg-white`, bordo destro `border-r border-gray-200`. Voce di menu: padding `px-3 py-2`, `text-sm text-gray-700`, `rounded-md`. Hover: `bg-gray-100`. **Attiva**: `bg-icm-blue-50 text-icm-blue-700 font-medium` con barra verticale sinistra `2px` di `icm-blue-500`.
- **Topbar**: `bg-white border-b border-gray-200 h-14`, contiene breadcrumb a sinistra e azioni globali (notifiche, profilo) a destra.
- **Breadcrumb**: `text-sm text-gray-500`, separatore `/` in `text-gray-300`, voce corrente in `text-gray-900` non cliccabile.
- **Tabs**: testo `text-sm text-gray-600`, tab attiva `text-icm-blue-700 border-b-2 border-icm-blue-500`, padding `px-4 py-2`.

### Alert e Toast

`flex items-start gap-3 p-4 rounded-lg border`, icona a sinistra, titolo `text-sm font-semibold`, descrizione `text-sm` con colore meno saturo.

- **Info**: `bg-icm-blue-50 border-icm-blue-200 text-icm-blue-700`.
- **Success**: `bg-success-50 border-success-500/20 text-success-700`.
- **Warning**: `bg-warning-50 border-warning-500/20 text-warning-700`.
- **Danger**: `bg-danger-50 border-danger-500/20 text-danger-700`.

I toast appaiono in basso a destra con `shadow-md` e si auto-chiudono dopo 5s (8s per gli errori).

### Tooltip

`bg-icm-navy-900 text-white text-xs px-2 py-1 rounded shadow-md`, comparsa dopo 400ms di hover.

## Stati Globali

- **Loading**: skeleton placeholder con `bg-gray-100` e shimmer leggero. Spinner solo per azioni puntuali (es. salvataggio bottone), mai a pagina intera quando un layout è già visibile.
- **Empty state**: icona neutrale (`gray-300`), titolo `text-base font-medium text-gray-700`, descrizione `text-sm text-gray-500`, eventuale bottone primary per l'azione di creazione.
- **Errore di caricamento**: alert `danger` con possibilità di retry.

## Iconografia

- Libreria di riferimento: **Lucide** (`lucide-react`). Stile lineare 1.5px, coerente con il peso tipografico.
- Dimensione standard: `16px` inline al testo, `20px` per icone autonome in bottoni o liste, `24px` per stati vuoti.
- Colore: ereditato dal `currentColor` del contenitore. Mai colorare icone con tonalità non in palette.

## Configurazione Tailwind

Estensione minima da inserire in `tailwind.config.js` perché tutti i token sopra siano disponibili come classi utility.

```js
// tailwind.config.js
module.exports = {
  content: ["./src/**/*.{js,jsx,ts,tsx,html}"],
  theme: {
    extend: {
      colors: {
        "icm-blue": {
          50:  "#EEF4FA",
          100: "#D6E4F1",
          200: "#AEC8E2",
          300: "#7FA7CC",
          500: "#245F8C",
          600: "#1D4E73",
          700: "#163D59",
        },
        "icm-navy": {
          700: "#2A3A52",
          900: "#1E2A3B",
        },
        success: {
          50:  "#ECFDF5",
          500: "#16A34A",
          700: "#15803D",
        },
        warning: {
          50:  "#FFFBEB",
          500: "#D97706",
          700: "#B45309",
        },
        danger: {
          50:  "#FEF2F2",
          500: "#DC2626",
          700: "#B91C1C",
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'Segoe UI', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      fontSize: {
        xs:   ['12px', { lineHeight: '16px' }],
        sm:   ['13px', { lineHeight: '20px' }],
        base: ['15px', { lineHeight: '22px' }],
        lg:   ['17px', { lineHeight: '24px' }],
        xl:   ['20px', { lineHeight: '28px' }],
        '2xl':['24px', { lineHeight: '32px' }],
      },
      borderRadius: {
        DEFAULT: '4px',
        md: '6px',
        lg: '8px',
      },
      boxShadow: {
        sm: '0 1px 2px 0 rgb(15 23 42 / 0.05)',
        md: '0 4px 8px -2px rgb(15 23 42 / 0.08), 0 2px 4px -2px rgb(15 23 42 / 0.04)',
        lg: '0 12px 24px -8px rgb(15 23 42 / 0.12), 0 4px 8px -4px rgb(15 23 42 / 0.06)',
      },
    },
  },
  plugins: [
    require('@tailwindcss/forms'),
  ],
};
```

> Si raccomanda l'uso del plugin `@tailwindcss/forms` per resettare gli stili di base di input nativi prima di applicare le classi del design system.

## Accessibilità

- **Contrasto**: tutti i pairing testo/sfondo definiti qui soddisfano WCAG AA. Non introdurre testo `gray-400` su `gray-50` (sotto soglia) se non per elementi decorativi.
- **Focus visibile**: ogni elemento interattivo deve avere lo stato di focus definito (di norma anello `ring-2 ring-icm-blue-200`). Non rimuovere mai l'outline senza sostituirlo.
- **Target touch**: bottoni e link cliccabili almeno `36×36px`.
- **Aria**: tutti i bottoni icona devono avere `aria-label`. Tutte le tabelle dati devono avere `<caption>` (anche sr-only) e header con `scope`.

## Nota per lo sviluppo

- **Transizioni**: tutte le interazioni (hover, focus, apertura modal) usano durata `150ms` per stati e `200ms` per overlay, con `ease-out`. Non superare i 250ms: un gestionale deve sembrare immediato.
- **Densità**: l'applicativo gira spesso su monitor 24"+ con utenti che consultano centinaia di righe al giorno. Privilegiare la densità rispetto all'ariosità nei contesti tabellari; mantenere l'ariosità nei form di inserimento.
- **Consistenza**: prima di creare un nuovo componente, verificare che non esista già una variante sopra. Se manca davvero un caso (es. wizard multi-step, calendario, drag-and-drop), **segnalarlo** anziché inventare uno stile: questo file è autoritativo, non orientativo.
