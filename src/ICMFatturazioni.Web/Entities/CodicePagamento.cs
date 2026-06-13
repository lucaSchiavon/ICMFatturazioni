namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Codice di pagamento del catalogo amministrativo (dispensa cap. 3-4, livello
/// "figlio" della gerarchia tipo→codice). POCO senza dipendenze esterne.
/// </summary>
/// <remarks>
/// Descrive una regola di scadenza: <see cref="NumScadenze"/> rate, i giorni
/// alla scadenza per rata (<see cref="GGScad1"/>..<see cref="GGScad3"/>),
/// l'eventuale spostamento a <see cref="FineMese"/> e i <see cref="GGpiu"/>
/// giorni aggiuntivi. Il calcolo concreto delle date è del servizio
/// <c>IScadenzaCalculator</c>. Appartiene a un tipo (<see cref="IdTipoPagamento"/>)
/// da cui eredita il flag banca A/C.
/// </remarks>
public sealed class CodicePagamento
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdCodicePagamento { get; set; }

    /// <summary>Tipo di appartenenza (FK → <c>fatt.TipiPagamento</c>).</summary>
    public required Guid IdTipoPagamento { get; init; }

    /// <summary>Descrizione del codice (es. "BONIFICO 60 GG F.M."). Obbligatoria e univoca tra gli attivi.</summary>
    public required string DescrPag { get; init; }

    /// <summary>Numero di rate (1..3).</summary>
    public required int NumScadenze { get; init; }

    /// <summary>Giorni alla 1ª scadenza (obbligatorio).</summary>
    public required int GGScad1 { get; init; }

    /// <summary>Giorni alla 2ª scadenza (se <see cref="NumScadenze"/> ≥ 2).</summary>
    public int? GGScad2 { get; init; }

    /// <summary>Giorni alla 3ª scadenza (se <see cref="NumScadenze"/> = 3).</summary>
    public int? GGScad3 { get; init; }

    /// <summary>Giorni aggiuntivi dopo il fine mese (ammessi solo se <see cref="FineMese"/>).</summary>
    public int? GGpiu { get; init; }

    /// <summary>Spostamento a fine mese (Sì/No).</summary>
    public required bool FineMese { get; init; }

    /// <summary>Condizione di pagamento FE (codice naturale TP01.., FK → <c>fatt.CondizioniPagamento.Codice</c>).</summary>
    public string? CondizionePagamento { get; init; }

    /// <summary>Modalità di pagamento FE (codice naturale MP01.., FK → <c>fatt.ModalitaPagamento.Codice</c>).</summary>
    public string? ModalitaPagamento { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
