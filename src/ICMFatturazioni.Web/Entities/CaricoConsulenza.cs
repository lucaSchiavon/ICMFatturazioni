namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Bivio "A carico" di una riga consulenza (Modulo Attività Consulenti, dispensa cap. 4):
/// stabilisce chi ha il rapporto finanziario con il consulente. Persistito come
/// <c>CHAR(1)</c> su fatt.AttivitaConsulenti.Carico (CHECK S/C, migration 077),
/// stesso pattern di <see cref="TipoAnagrafica"/>.
/// </summary>
public enum CaricoConsulenza
{
    /// <summary>Paga lo studio: la riga entra nella gestione pagamenti (pagato/residuo).</summary>
    Studio = 'S',

    /// <summary>Paga il cliente (rapporto diretto cliente-consulente): riga solo informativa.</summary>
    Cliente = 'C',
}

/// <summary>
/// Helper di conversione fra <see cref="CaricoConsulenza"/> e il <c>CHAR(1)</c>
/// usato sul DB, per non disseminare switch identici nei repository.
/// </summary>
public static class CaricoConsulenzaExtensions
{
    /// <summary>Restituisce il carattere usato sul DB ('S' o 'C').</summary>
    public static char ToDbCode(this CaricoConsulenza carico) => (char)carico;

    /// <summary>
    /// Converte il carattere DB in <see cref="CaricoConsulenza"/>.
    /// Lancia <see cref="ArgumentOutOfRangeException"/> per valori non previsti —
    /// il CHECK constraint a DB lo rende un caso "impossibile".
    /// </summary>
    public static CaricoConsulenza CaricoConsulenzaFromDbCode(char code) => code switch
    {
        'S' => CaricoConsulenza.Studio,
        'C' => CaricoConsulenza.Cliente,
        _   => throw new ArgumentOutOfRangeException(nameof(code), code,
                   "Codice Carico non previsto (attesi 'S' o 'C')."),
    };
}
