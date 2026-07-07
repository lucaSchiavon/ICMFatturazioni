namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un <see cref="AttivitaDettaglioInvalidaException"/> è stata lanciata.
/// L'ordine riflette la priorità UX: il motivo più azionabile viene valutato per primo
/// (es. HasFattura precede gli altri perché blocca qualsiasi modifica).
/// </summary>
public enum AttivitaDettaglioMotivoInvalido
{
    /// <summary>La riga è collegata a una fattura emessa e non può essere modificata/eliminata.</summary>
    HasFattura,

    /// <summary>
    /// Almeno una scadenza (rata) del dettaglio è già inserita in un avviso di fattura
    /// (<c>SchedulazionePagamenti.IdAvvisoRiga</c> valorizzato). Il contenitore è
    /// congelato: eliminarlo/modificarlo lascerebbe l'avviso a puntare a scadenze di
    /// un dettaglio sparito. Valutato subito dopo <see cref="HasFattura"/>.
    /// </summary>
    HasScadenzaInAvviso,

    /// <summary>Il tipo dettaglio (FK) non è stato selezionato.</summary>
    TipoDettaglioObbligatorio,

    /// <summary>La descrizione del dettaglio è vuota o blank.</summary>
    DescrizioneObbligatoria,

    /// <summary>Il termine previsto non è stato indicato.</summary>
    TerminePrevistoObbligatorio,

    /// <summary>L'importo è ≤ 0.</summary>
    ImportoNonValido,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business di <c>AttivitaDettaglio</c>.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class AttivitaDettaglioInvalidaException : Exception
{
    public AttivitaDettaglioMotivoInvalido Motivo { get; }

    public AttivitaDettaglioInvalidaException(AttivitaDettaglioMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
