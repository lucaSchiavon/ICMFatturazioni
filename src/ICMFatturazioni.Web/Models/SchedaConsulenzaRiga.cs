using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Read-model della maschera "Schede attività consulenti" (dispensa cap. 6):
/// punto di vista ribaltato — dal consulente verso clienti e attività seguite.
/// Una riga per consulenza, con Pagato derivato dalle tranche attive.
/// </summary>
public sealed class SchedaConsulenzaRiga
{
    public Guid IdAttivitaConsulente { get; init; }
    public Guid IdAnagrafica { get; init; }
    public Guid IdAttivita { get; init; }

    /// <summary>Ragione sociale del cliente dell'attività (join).</summary>
    public required string RagioneSociale { get; init; }

    /// <summary>Numero/codice dell'attività cliente (join).</summary>
    public required string AttivitaNumero { get; init; }

    /// <summary>Descrizione dell'attività cliente (join).</summary>
    public required string AttivitaDescrizione { get; init; }

    /// <summary>Descrizione del tipo attività consulente (join).</summary>
    public required string TipoDescrizione { get; init; }

    /// <summary>Bivio Studio/Cliente: decide in quale griglia della scheda finisce la riga.</summary>
    public CaricoConsulenza Carico { get; init; }

    /// <summary>Scadenza-promemoria (solo righe a carico Studio).</summary>
    public DateOnly? Scadenza { get; init; }

    public decimal Importo { get; init; }

    /// <summary>Somma delle tranche attive (sempre 0 per il carico Cliente).</summary>
    public decimal Pagato { get; init; }

    public string? Nota { get; init; }

    /// <summary>Quota ancora da pagare (significativa solo per il carico Studio).</summary>
    public decimal Residuo => Importo - Pagato;

    /// <summary>Consulenza aperta = residuo &gt; 0 (decisione D-C4).</summary>
    public bool Aperta => Residuo > 0;
}
