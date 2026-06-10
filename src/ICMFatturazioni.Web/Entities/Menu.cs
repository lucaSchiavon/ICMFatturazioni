namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Voce di menu di primo livello. POCO mappato su <c>fatt.Menu</c>.
/// </summary>
/// <remarks>
/// <see cref="PaginaRazor"/> (colonna <c>Menu</c>) contiene il nome della
/// classe/pagina Razor a cui la voce punta; è <c>null</c> per le voci-gruppo
/// (contenitori espandibili senza pagina propria). <see cref="Attivo"/> indica
/// se la funzionalità è implementata/cliccabile (false = mostrata in grigio);
/// la VISIBILITÀ per ruolo è cosa distinta (tabella MenuRuolo).
/// </remarks>
public sealed class Menu
{
    public Guid IdMenu { get; set; }
    public required string DescrizioneMenu { get; init; }
    public string? PaginaRazor { get; init; }
    public string? Icona { get; init; }
    public int Ordine { get; init; }
    public bool Attivo { get; init; } = true;

    /// <summary>
    /// Gruppo accessibile SOLO ad Admin/Superadmin (es. "Amministrazione"):
    /// non configurabile nelle pagine permessi e invisibile ai ruoli custom.
    /// </summary>
    public bool SoloAdmin { get; init; }
}
