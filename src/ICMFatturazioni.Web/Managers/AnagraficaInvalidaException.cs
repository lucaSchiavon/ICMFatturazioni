namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su <c>Anagrafica</c> è stata rifiutata.
/// L'ordine di enumerazione segue la regola in CLAUDE.md ("Ordine dei
/// controlli in eccezioni tipizzate è UX"): il valore mostrato all'utente
/// è il primo che si verifica nella sequenza di controlli del manager.
/// </summary>
public enum AnagraficaInvalidaMotivo
{
    /// <summary>La ragione sociale è vuota o solo whitespace.</summary>
    RagioneSocialeObbligatoria,

    /// <summary>
    /// Tipologia "Ente pubblico" non supportata: la generazione dei tracciati
    /// XML per la Pubblica Amministrazione (FatturaPA/FPA12, split payment) non è
    /// implementata. Si possono creare solo anagrafiche Privato o Società.
    /// </summary>
    EntePubblicoNonSupportato,

    /// <summary>
    /// La sigla paese non corrisponde a nessun record di <c>fatt.Paesi</c>
    /// (intercettata via FK constraint).
    /// </summary>
    PaeseInesistente,

    /// <summary>
    /// La sigla provincia non corrisponde a nessun record di
    /// <c>fatt.Province</c> (intercettata via FK constraint).
    /// </summary>
    ProvinciaInesistente,

    /// <summary>
    /// Il codice di pagamento indicato non esiste o non è attivo
    /// (pre-check manager su <c>fatt.CodiciPagamento</c>; FK come sentinel).
    /// </summary>
    PagamentoInesistente,

    /// <summary>
    /// La banca di appoggio indicata non esiste o non è attiva
    /// (pre-check manager su <c>fatt.BancheAppoggio</c>; FK come sentinel).
    /// </summary>
    BancaInesistente,

    /// <summary>
    /// La banca scelta non è coerente con il <c>FlagBanca</c> del pagamento:
    /// pagamento "dati azienda" ⟹ banca aziendale; pagamento "dati cliente"
    /// ⟹ banca di QUESTO cliente. Regola di dominio che incrocia due tabelle:
    /// presidiata solo a livello applicativo (non è esprimibile come FK).
    /// </summary>
    BancaNonCoerenteColPagamento,

    /// <summary>
    /// Il codice IVA indicato non esiste o non è attivo
    /// (pre-check manager su <c>fatt.CodiciIVA</c>; FK come sentinel).
    /// </summary>
    CodiceIVAInesistente,
}

/// <summary>
/// Eccezione tipizzata sollevata quando un'operazione di Insert/Update
/// su <c>Anagrafica</c> fallisce per dati non validi.
/// </summary>
public sealed class AnagraficaInvalidaException : Exception
{
    public AnagraficaInvalidaMotivo Motivo { get; }

    public AnagraficaInvalidaException(AnagraficaInvalidaMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public AnagraficaInvalidaException(AnagraficaInvalidaMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Sollevata quando si tenta di eliminare un'anagrafica con riferimenti
/// in entità a valle (progetti, avvisi, fatture). La UI deve nascondere
/// il pulsante "Elimina" prima del tentativo, ma il manager rilancia
/// comunque in caso di race condition (pattern "doppia difesa").
/// </summary>
public sealed class AnagraficaConDipendenzeException : Exception
{
    public Guid IdAnagrafica { get; }

    public AnagraficaConDipendenzeException(Guid idAnagrafica)
        : base($"L'anagrafica {idAnagrafica} non può essere eliminata perché è referenziata da altre entità.")
    {
        IdAnagrafica = idAnagrafica;
    }
}
