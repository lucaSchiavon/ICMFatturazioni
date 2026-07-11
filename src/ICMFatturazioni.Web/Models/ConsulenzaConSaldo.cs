namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Read-model della maschera "Gestione pagamenti consulenti" (dispensa cap. 5):
/// riga consulenza a carico dello Studio con Pagato e Residuo derivati dalle
/// tranche attive. Il residuo non è mai memorizzato (sempre Importo − Pagato).
/// </summary>
public sealed class ConsulenzaConSaldo
{
    public Guid IdAttivitaConsulente { get; init; }

    /// <summary>Denominazione del consulente (join).</summary>
    public required string ConsulenteDescrizione { get; init; }

    /// <summary>Descrizione del tipo attività consulente (join).</summary>
    public required string TipoDescrizione { get; init; }

    /// <summary>Scadenza-promemoria del pagamento (dalla riga consulenza).</summary>
    public DateOnly? Scadenza { get; init; }

    /// <summary>Compenso dovuto al consulente (dalla riga consulenza).</summary>
    public decimal Importo { get; init; }

    /// <summary>Somma delle tranche di pagamento attive.</summary>
    public decimal Pagato { get; init; }

    /// <summary>Quota ancora da pagare. A zero la consulenza è saldata.</summary>
    public decimal Residuo => Importo - Pagato;
}

/// <summary>
/// Saldo di una singola riga consulenza, usato dal manager pagamenti per la
/// guardia D-C3 (tranche mai oltre il residuo).
/// </summary>
public sealed class SaldoRiga
{
    /// <summary>Compenso dovuto della riga.</summary>
    public decimal Importo { get; init; }

    /// <summary>Somma delle tranche attive (eventualmente esclusa quella in modifica).</summary>
    public decimal Pagato { get; init; }

    /// <summary>La riga è a carico dello Studio (precondizione per i pagamenti).</summary>
    public bool CaricoStudio { get; init; }
}
