using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ICMFatturazioni.Web.Auditing;

/// <summary>
/// Costruisce il dettaglio JSON delle righe di audit: snapshot dei campi di un
/// record (insert/delete) e diff prima→dopo (update). Esclude sempre i campi
/// sensibili (Regola 6 di CLAUDE.md): la colonna <c>fatt.Audit.Dati</c> non deve
/// mai contenere segreti.
/// </summary>
public static class AuditDettaglio
{
    // Mai serializzati, qualunque entità li esponga.
    private static readonly HashSet<string> CampiSensibili = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Salt", "TokenHash", "Password",
    };

    private static readonly JsonSerializerOptions Compatto = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },   // enum come nome, non ordinale
    };

    private static readonly JsonSerializerOptions Indentato = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Snapshot JSON dei campi non-null e non-sensibili dell'oggetto. Conserva i
    /// tipi originali (numeri, booleani, enum come nome).
    /// </summary>
    public static string Snapshot(object entita)
    {
        var el = JsonSerializer.SerializeToElement(entita, Compatto);
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (CampiSensibili.Contains(prop.Name) || prop.Value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }
                dict[prop.Name] = prop.Value;
            }
        }
        return JsonSerializer.Serialize(dict, Compatto);
    }

    /// <summary>
    /// Diff dei soli campi cambiati fra due oggetti della STESSA forma, come
    /// <c>{ campo: { prima, dopo } }</c>. Ritorna <c>null</c> se nulla è cambiato.
    /// </summary>
    public static string? Diff(object prima, object dopo)
    {
        var p = Dizionario(prima);
        var d = Dizionario(dopo);
        var cambi = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (campo, valoreDopo) in d)
        {
            p.TryGetValue(campo, out var valorePrima);
            if (!string.Equals(valorePrima, valoreDopo, StringComparison.Ordinal))
            {
                cambi[campo] = new { prima = valorePrima, dopo = valoreDopo };
            }
        }
        return cambi.Count == 0 ? null : JsonSerializer.Serialize(cambi, Compatto);
    }

    /// <summary>Riformatta un JSON in forma indentata per la UI; raw se non parsabile.</summary>
    public static string Pretty(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, Indentato);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    // Proietta un oggetto in dizionario campo→valore-stringa (per il confronto
    // del diff), saltando i campi sensibili.
    private static Dictionary<string, string?> Dizionario(object o)
    {
        var el = JsonSerializer.SerializeToElement(o, Compatto);
        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (CampiSensibili.Contains(prop.Name))
                {
                    continue;
                }
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
            }
        }
        return dict;
    }
}
