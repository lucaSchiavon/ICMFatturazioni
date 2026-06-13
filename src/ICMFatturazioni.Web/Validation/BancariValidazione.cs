using System.Text.RegularExpressions;

namespace ICMFatturazioni.Web.Validation;

/// <summary>
/// Validazioni bancarie offline (nessun servizio esterno): formato e checksum
/// IBAN, estrazione di ABI/CAB dall'IBAN, formato di ABI/CAB.
/// </summary>
/// <remarks>
/// In un IBAN italiano (27 caratteri) ABI e CAB sono contenuti nell'IBAN:
/// <code>IT + 2 cifre controllo + CIN (1 lettera) + ABI (5) + CAB (5) + conto (12)</code>
/// Questo permette di verificare la COERENZA tra l'IBAN e l'ABI/CAB indicati.
/// La verifica che ABI/CAB ESISTANO davvero (registro Banca d'Italia) richiede
/// un dataset esterno ed è fuori da questo helper.
/// </remarks>
public static partial class BancariValidazione
{
    // ABI e CAB italiani: esattamente 5 cifre.
    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex CodiceAbiCabRegex();

    // Lunghezza IBAN per paese (i paesi rilevanti per l'app). Per i paesi non
    // elencati si applica solo il range generico 15..34.
    private static readonly Dictionary<string, int> LunghezzaPerPaese =
        new(StringComparer.OrdinalIgnoreCase) { ["IT"] = 27, ["SM"] = 27 };

    /// <summary>
    /// Normalizza un IBAN per il confronto: rimuove spazi e porta in maiuscolo.
    /// Restituisce <c>null</c> se l'input è nullo/vuoto.
    /// </summary>
    public static string? NormalizzaIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return null;
        }
        return iban.Replace(" ", string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>
    /// <c>true</c> se l'IBAN (già normalizzato) è ben formato: solo caratteri
    /// alfanumerici, prime 2 lettere (paese) + 2 cifre (controllo), lunghezza
    /// coerente col paese (o nel range generico 15..34).
    /// </summary>
    public static bool IbanBenFormato(string ibanNormalizzato)
    {
        var s = ibanNormalizzato;
        if (s.Length is < 15 or > 34)
        {
            return false;
        }
        // Solo lettere/cifre.
        foreach (var ch in s)
        {
            var ok = ch is >= '0' and <= '9' or >= 'A' and <= 'Z';
            if (!ok)
            {
                return false;
            }
        }
        // Paese (2 lettere) + check (2 cifre).
        if (s[0] is < 'A' or > 'Z' || s[1] is < 'A' or > 'Z' || s[2] is < '0' or > '9' || s[3] is < '0' or > '9')
        {
            return false;
        }
        // Lunghezza esatta se il paese è noto.
        var paese = s[..2];
        if (LunghezzaPerPaese.TryGetValue(paese, out var len) && s.Length != len)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Verifica il checksum internazionale dell'IBAN (ISO 7064 MOD-97-10):
    /// sposta i primi 4 caratteri in coda, converte le lettere in numeri e
    /// controlla che il resto modulo 97 sia 1. Presuppone l'IBAN normalizzato.
    /// </summary>
    public static bool IbanChecksumValido(string ibanNormalizzato)
    {
        var s = ibanNormalizzato;
        if (s.Length < 4)
        {
            return false;
        }
        var riordinato = s[4..] + s[..4];
        var resto = 0;
        foreach (var ch in riordinato)
        {
            int val;
            if (ch is >= '0' and <= '9')
            {
                val = ch - '0';
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                val = ch - 'A' + 10; // 10..35: due cifre
            }
            else
            {
                return false;
            }

            if (val > 9)
            {
                resto = (resto * 10 + val / 10) % 97;
                resto = (resto * 10 + val % 10) % 97;
            }
            else
            {
                resto = (resto * 10 + val) % 97;
            }
        }
        return resto == 1;
    }

    /// <summary>
    /// IBAN completo valido = ben formato + checksum corretto.
    /// </summary>
    public static bool IbanValido(string ibanNormalizzato)
        => IbanBenFormato(ibanNormalizzato) && IbanChecksumValido(ibanNormalizzato);

    /// <summary>
    /// Estrae ABI e CAB da un IBAN italiano/sammarinese (27 caratteri):
    /// posizioni 6-10 (ABI) e 11-15 (CAB). Restituisce <c>false</c> per IBAN di
    /// altri paesi o di lunghezza non conforme.
    /// </summary>
    public static bool TryEstraiAbiCab(string ibanNormalizzato, out string abi, out string cab)
    {
        abi = string.Empty;
        cab = string.Empty;
        var paese = ibanNormalizzato.Length >= 2 ? ibanNormalizzato[..2] : string.Empty;
        if ((paese is not ("IT" or "SM")) || ibanNormalizzato.Length != 27)
        {
            return false;
        }
        abi = ibanNormalizzato.Substring(5, 5);
        cab = ibanNormalizzato.Substring(10, 5);
        return true;
    }

    /// <summary>
    /// <c>true</c> se il codice (ABI o CAB) è nel formato corretto: esattamente
    /// 5 cifre. Un valore nullo/vuoto è considerato "non presente" → <c>true</c>
    /// (la presenza/obbligatorietà è responsabilità del chiamante).
    /// </summary>
    public static bool CodiceAbiCabFormatoValido(string? codice)
        => string.IsNullOrWhiteSpace(codice) || CodiceAbiCabRegex().IsMatch(codice.Trim());
}
