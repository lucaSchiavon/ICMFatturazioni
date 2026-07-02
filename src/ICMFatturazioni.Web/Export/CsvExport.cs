using System.Text;

namespace ICMFatturazioni.Web.Export;

/// <summary>
/// Generatore CSV per l'export delle griglie amministrative (Audit, Log).
/// Scelte deliberate per la compatibilità con Excel italiano:
/// <list type="bullet">
///   <item>separatore <c>;</c> (punto e virgola): è quello atteso da Excel con
///     locale IT, dove la virgola è separatore decimale;</item>
///   <item>UTF-8 <b>con BOM</b>: senza il preambolo Excel interpreterebbe il file
///     come ANSI e romperebbe gli accenti;</item>
///   <item>fine riga CRLF, come da RFC 4180.</item>
/// </list>
/// Include la mitigazione CSV-injection (Regola 5): un valore che inizia con
/// <c>= + - @</c> viene prefissato con un apostrofo così Excel non lo interpreta
/// come formula.
/// </summary>
public static class CsvExport
{
    private const char Separatore = ';';
    private const string FineRiga = "\r\n";

    /// <summary>
    /// Costruisce i byte del CSV (BOM incluso) da un'intestazione e dalle righe.
    /// Ogni riga è una sequenza di celle già convertite in stringa (null → vuoto).
    /// </summary>
    public static byte[] Genera(IReadOnlyList<string> intestazioni,
                                IEnumerable<IReadOnlyList<string?>> righe)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(Separatore, intestazioni.Select(Escape)));
        sb.Append(FineRiga);

        foreach (var riga in righe)
        {
            sb.Append(string.Join(Separatore, riga.Select(Escape)));
            sb.Append(FineRiga);
        }

        var preambolo = Encoding.UTF8.GetPreamble();          // BOM
        var corpo     = Encoding.UTF8.GetBytes(sb.ToString());
        var buffer    = new byte[preambolo.Length + corpo.Length];
        Buffer.BlockCopy(preambolo, 0, buffer, 0, preambolo.Length);
        Buffer.BlockCopy(corpo, 0, buffer, preambolo.Length, corpo.Length);
        return buffer;
    }

    // Quoting RFC 4180 + neutralizzazione formule.
    private static string Escape(string? valore)
    {
        var v = valore ?? string.Empty;

        // Anti CSV-injection: neutralizza i caratteri che Excel tratta da formula.
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@')
            v = "'" + v;

        // Serve il quoting se contiene separatore, virgolette o a-capo.
        if (v.Contains(Separatore) || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
            v = "\"" + v.Replace("\"", "\"\"") + "\"";

        return v;
    }
}
