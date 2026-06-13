namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Flag banca di un tipo di pagamento (dispensa cap. 3.2). Decide di chi sono i
/// dati bancari mostrati in fattura. Persistito in DB come <c>CHAR(1)</c> con
/// CHECK su {'A','C'}: il valore di persistenza è <b>esplicito</b> (attributo
/// numerico), non derivato dall'ordinale enum.
/// </summary>
public enum FlagBanca
{
    /// <summary>
    /// Dati banca dell'<b>Azienda</b> (bonifico): il cliente versa sul nostro IBAN.
    /// </summary>
    Azienda = 'A',

    /// <summary>
    /// Dati banca del <b>Cliente</b> (ricevuta bancaria): servono ABI/CAB del cliente.
    /// </summary>
    Cliente = 'C',
}

/// <summary>
/// Helper di conversione fra <see cref="FlagBanca"/> e il <c>CHAR(1)</c> del DB.
/// </summary>
public static class FlagBancaExtensions
{
    /// <summary>Carattere usato sul DB ('A' o 'C').</summary>
    public static char ToDbCode(this FlagBanca flag) => (char)flag;

    /// <summary>
    /// Converte il carattere DB in <see cref="FlagBanca"/>. Lancia per valori
    /// non previsti: il CHECK lo rende impossibile, ma falliamo rumorosamente
    /// per intercettare drift di schema.
    /// </summary>
    public static FlagBanca FromDbCode(char code) => code switch
    {
        'A' => FlagBanca.Azienda,
        'C' => FlagBanca.Cliente,
        _   => throw new ArgumentOutOfRangeException(nameof(code), code, $"Valore inatteso per FlagBanca: '{code}'."),
    };

    /// <summary>Descrizione human-readable per la UI.</summary>
    public static string Descrizione(this FlagBanca flag) => flag switch
    {
        FlagBanca.Azienda => "Dati banca Azienda (bonifico)",
        FlagBanca.Cliente => "Dati banca Cliente (ricevuta bancaria)",
        _ => flag.ToString(),
    };
}
