namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipologia di anagrafica cliente (ADR D3). Persistita in DB come
/// <c>CHAR(1)</c> con CHECK constraint su {'S', 'P', 'E'}: la persistenza
/// del valore è <b>esplicita</b> tramite l'attributo numerico, non
/// derivata dall'ordinale enum, per non legare il binario all'ordine di
/// dichiarazione.
/// </summary>
public enum TipoAnagrafica
{
    /// <summary>Società (persona giuridica, S.r.l., S.p.A., ecc.).</summary>
    Societa = 'S',

    /// <summary>Persona fisica privata.</summary>
    Privato = 'P',

    /// <summary>Ente pubblico (Comune, ASL, ministero, ecc.).</summary>
    EntePubblico = 'E',
}

/// <summary>
/// Helper di conversione fra <see cref="TipoAnagrafica"/> e il <c>CHAR(1)</c>
/// usato sul DB. Centralizziamo qui la mappatura per non disseminare
/// switch identici nei repository e nei manager.
/// </summary>
public static class TipoAnagraficaExtensions
{
    /// <summary>Restituisce il carattere usato sul DB ('S', 'P', 'E').</summary>
    public static char ToDbCode(this TipoAnagrafica tipo) => (char)tipo;

    /// <summary>
    /// Converte il carattere DB in <see cref="TipoAnagrafica"/>.
    /// Lancia <see cref="ArgumentOutOfRangeException"/> per valori non
    /// previsti — il CHECK constraint a DB lo rende un caso "impossibile",
    /// ma falliamo rumorosamente se accade per intercettare drift di schema.
    /// </summary>
    public static TipoAnagrafica FromDbCode(char code) => code switch
    {
        'S' => TipoAnagrafica.Societa,
        'P' => TipoAnagrafica.Privato,
        'E' => TipoAnagrafica.EntePubblico,
        _   => throw new ArgumentOutOfRangeException(
                    nameof(code),
                    code,
                    $"Valore inatteso per TipoAnagrafica: '{code}'."),
    };

    /// <summary>Descrizione human-readable per UI (form/elenco).</summary>
    public static string Descrizione(this TipoAnagrafica tipo) => tipo switch
    {
        TipoAnagrafica.Societa      => "Società",
        TipoAnagrafica.Privato      => "Privato",
        TipoAnagrafica.EntePubblico => "Ente pubblico",
        _ => tipo.ToString(),
    };
}
