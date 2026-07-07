namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Cantiere di un'attività cliente. Entità CONDIVISA con ICMVerbali: la tabella
/// fisica è <c>dbo.Cantiere</c> del DB unificato, esposta a questo applicativo
/// dalla vista aggiornabile <c>fatt.Cantiere</c> (migration 071 di ICMVerbali),
/// che rinomina <c>ProgettoId</c> in <c>IdAttivita</c> (per ICMFatturazioni i
/// Progetti di Verbali sono le Attività). È il tramite della catena
/// Verbale → Cantiere → Attività, su cui poggerà la futura maschera
/// "verbali per attività". POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class Cantiere
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdCantiere { get; set; }

    /// <summary>FK → fatt.Attivita (= dbo.Progetto). Obbligatoria: un cantiere
    /// non può esistere senza attività (NOT NULL + FK_Cantiere_Progetto a DB).</summary>
    public Guid IdAttivita { get; set; }

    /// <summary>Ubicazione del cantiere. Obbligatoria (max 300).</summary>
    public string Ubicazione { get; set; } = string.Empty;

    /// <summary>Tipologia dei lavori. Obbligatoria (max 500).</summary>
    public string Tipologia { get; set; } = string.Empty;

    /// <summary>Importo dell'appalto in euro. Facoltativo.</summary>
    public decimal? ImportoAppalto { get; set; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; set; } = true;
}
