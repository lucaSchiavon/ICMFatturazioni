using System.Net.Mail;
using System.Text.RegularExpressions;

namespace ICMFatturazioni.Web.Validation;

/// <summary>
/// Validatori di formato per i campi anagrafici (partita IVA, codice fiscale,
/// email, PEC, codice destinatario SDI) usati dai dialog dell'app tramite la
/// <c>Validation</c> di <c>MudForm</c>. Funzioni pure, senza dipendenze, testabili
/// in isolamento. Portati e allineati all'analogo validator di ICMVerbali
/// (principio di uniformità della suite).
/// </summary>
/// <remarks>
/// Convenzione: i campi facoltativi restano tali. I metodi <c>Valida*</c>
/// restituiscono <c>null</c> (= valido) su stringa vuota/spazi e validano il
/// FORMATO solo se un valore è stato inserito. L'obbligatorietà resta gestita a
/// parte (<c>Required</c> del controllo). La firma <c>string? → string?</c> è
/// quella attesa da MudBlazor: <c>null</c> = valido, messaggio = errore.
/// </remarks>
public static partial class AnagraficaValidator
{
    // Codice destinatario SDI: 6 caratteri alfanumerici per la Pubblica
    // Amministrazione (Codice Univoco Ufficio IPA) oppure 7 per i privati/B2B.
    [GeneratedRegex(@"^[A-Za-z0-9]{6,7}$")]
    private static partial Regex CodiceDestinatarioSdiRegex();

    // CAP italiano: esattamente 5 cifre.
    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex CapRegex();

    // Valori della tabella "dispari" del codice fiscale, indicizzati 0-9 (cifre) e
    // 10-35 (A-Z). Cifra e lettera omocodica condividono lo stesso indice.
    private static readonly int[] CfOdd =
    [
        1, 0, 5, 7, 9, 13, 15, 17, 19, 21,       // 0-9
        1, 0, 5, 7, 9, 13, 15, 17, 19, 21,       // A-J
        2, 4, 18, 20, 11, 3, 6, 8, 12, 14,       // K-T
        16, 10, 22, 25, 24, 23,                  // U-Z
    ];

    // ---- Email --------------------------------------------------------------

    public static bool EmailValida(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        // MailAddress è permissivo: accetta local-part con spazi ("a b@x.com") e
        // domini senza TLD ("a@b"). Rifiutiamo whitespace interno e richiediamo un
        // punto nel dominio per escludere indirizzi non instradabili.
        if (v.Any(char.IsWhiteSpace) || !MailAddress.TryCreate(v, out var addr))
            return false;

        var host = addr.Host;
        return host.Length >= 3
            && host.Contains('.')
            && !host.StartsWith('.')
            && !host.EndsWith('.');
    }

    public static string? ValidaEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) || EmailValida(value)
            ? null
            : "Indirizzo email non valido.";

    /// <summary>
    /// La PEC ha lo stesso formato di un'email ordinaria (è una casella di posta
    /// certificata): validiamo la forma dell'indirizzo, non la sua "certificazione".
    /// </summary>
    public static string? ValidaPec(string? value) =>
        string.IsNullOrWhiteSpace(value) || EmailValida(value)
            ? null
            : "Indirizzo PEC non valido.";

    // ---- Partita IVA (11 cifre, algoritmo di controllo ministeriale) --------

    public static bool PartitaIvaValida(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        if (v.Length != 11 || !v.All(char.IsAsciiDigit))
            return false;

        // Luhn sulle 11 cifre (l'11a è il carattere di controllo): posizioni pari
        // (0-based dispari) raddoppiate con sottrazione di 9 se > 9.
        var sum = 0;
        for (var i = 0; i < 11; i++)
        {
            var n = v[i] - '0';
            if (i % 2 == 1)
            {
                n *= 2;
                if (n > 9)
                    n -= 9;
            }
            sum += n;
        }

        return sum % 10 == 0;
    }

    public static string? ValidaPartitaIva(string? value) =>
        string.IsNullOrWhiteSpace(value) || PartitaIvaValida(value)
            ? null
            : "Partita IVA non valida (11 cifre, controllo di coerenza fallito).";

    // ---- Codice fiscale persona (16 caratteri) ------------------------------
    //
    // Validazione SOLO formale: struttura + carattere di controllo (calcolato dai
    // 15 caratteri precedenti con le tabelle del D.M. 23/12/1976). È un checksum
    // di coerenza interna della stringa: intercetta refusi/cifre invertite, MA non
    // verifica che il CF appartenga davvero a una data persona (qui non ci sono
    // cognome/nome/data/comune per rigenerarlo, né la tabella dei codici catastali).

    public static bool CodiceFiscalePersonaValido(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().ToUpperInvariant();
        if (v.Length != 16)
            return false;

        // Struttura: 6 lettere, 2 alfa (giorno/omocodia), 1 lettera (mese),
        // 2 alfa (giorno+sesso/omocodia), 1 lettera (comune), 3 alfa, 1 controllo.
        for (var i = 0; i < 16; i++)
        {
            var c = v[i];
            var isLetterOnly = i is 0 or 1 or 2 or 3 or 4 or 5 or 8 or 11 or 15;
            if (isLetterOnly)
            {
                if (c is < 'A' or > 'Z')
                    return false;
            }
            else if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var idx = IndiceAlfanumerico(v[i]);
            // Posizioni dispari (1-based) usano la tabella "dispari"; le pari usano
            // il valore diretto (cifra 0-9 o lettera A-Z = 0-25).
            sum += (i % 2 == 0) ? CfOdd[idx] : (idx < 10 ? idx : idx - 10);
        }

        var atteso = (char)('A' + sum % 26);
        return v[15] == atteso;
    }

    // 0-9 per le cifre, 10-35 per A-Z.
    private static int IndiceAlfanumerico(char c) =>
        char.IsAsciiDigit(c) ? c - '0' : c - 'A' + 10;

    // ---- Codice fiscale azienda (11 cifre numerico oppure 16 = persona) -----

    public static bool CodiceFiscaleAziendaValido(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        // Società/enti: CF numerico a 11 cifre (stesso formato/checksum della P.IVA).
        // Ditte individuali: CF di persona a 16 caratteri.
        return v.Length switch
        {
            11 => PartitaIvaValida(v),
            16 => CodiceFiscalePersonaValido(v),
            _ => false,
        };
    }

    public static string? ValidaCodiceFiscaleAzienda(string? value) =>
        string.IsNullOrWhiteSpace(value) || CodiceFiscaleAziendaValido(value)
            ? null
            : "Codice fiscale non valido (11 cifre per società o 16 caratteri per ditta individuale).";

    // ---- P.IVA / Codice fiscale (campo unico dell'anagrafica) ---------------
    //
    // Nel form Anagrafica il campo "P.IVA / Codice fiscale" accetta indistintamente
    // una partita IVA (11 cifre) o un codice fiscale (11 numerico per società, 16
    // per persona/ditta individuale). È valido se soddisfa una delle due regole.

    public static bool PivaOCodiceFiscaleValido(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim();
        return PartitaIvaValida(v) || CodiceFiscalePersonaValido(v);
    }

    public static string? ValidaPivaOCodiceFiscale(string? value) =>
        string.IsNullOrWhiteSpace(value) || PivaOCodiceFiscaleValido(value)
            ? null
            : "P.IVA (11 cifre) o codice fiscale (11 o 16 caratteri) non valido.";

    // ---- Codice destinatario SDI --------------------------------------------
    //
    // Il codice destinatario del Sistema di Interscambio è di 7 caratteri
    // alfanumerici per privati/B2B (in alternativa alla PEC) e di 6 caratteri
    // (Codice Univoco Ufficio IPA) per la Pubblica Amministrazione. Facoltativo.

    public static string? ValidaCodiceDestinatarioSdi(string? value) =>
        string.IsNullOrWhiteSpace(value) || CodiceDestinatarioSdiRegex().IsMatch(value.Trim())
            ? null
            : "Il codice destinatario SDI deve essere di 7 caratteri alfanumerici (6 per la Pubblica Amministrazione).";

    // ---- CAP (codice di avviamento postale italiano) ------------------------
    //
    // Il CAP italiano è sempre di 5 cifre. Facoltativo: vuoto = valido.

    public static string? ValidaCap(string? value) =>
        string.IsNullOrWhiteSpace(value) || CapRegex().IsMatch(value.Trim())
            ? null
            : "Il CAP deve essere di 5 cifre.";
}
