namespace ICMFatturazioni.Web.Presentation;

/// <summary>
/// Formattazione di date/ora per la UI in chiave italiana. I timestamp sono
/// persistiti in UTC (coerenza multi-server); qui vengono convertiti nel fuso
/// italiano (Europe/Rome) e formattati <c>gg/MM/aaaa HH:mm:ss</c> per la
/// visualizzazione agli utenti.
/// </summary>
public static class DataOra
{
    // Risolto una volta sola. .NET 6+ accetta sia l'ID IANA sia quello Windows
    // su qualsiasi piattaforma; proviamo entrambi per robustezza dev/prod.
    private static readonly TimeZoneInfo FusoItalia = RisolviFusoItalia();

    /// <summary>
    /// Converte un istante UTC nel fuso italiano e lo formatta per la griglia
    /// (gg/MM/aaaa HH:mm:ss). Input trattato come UTC a prescindere dal Kind
    /// (i DATETIME2 letti da SQL arrivano con Kind=Unspecified).
    /// </summary>
    public static string Italiana(DateTime utc)
    {
        var sorgenteUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var locale = TimeZoneInfo.ConvertTimeFromUtc(sorgenteUtc, FusoItalia);
        return locale.ToString("dd/MM/yyyy HH:mm:ss");
    }

    private static TimeZoneInfo RisolviFusoItalia()
    {
        foreach (var id in new[] { "Europe/Rome", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Ultima istanza: fuso del server (meglio un orario che un'eccezione).
        return TimeZoneInfo.Local;
    }
}
