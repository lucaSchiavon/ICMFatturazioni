namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Voce di sottomenu (figlia di un <see cref="Menu"/>). POCO mappato su
/// <c>fatt.SottoMenu</c>. <see cref="PaginaRazor"/> (colonna <c>SottoMenu</c>)
/// è il nome della classe/pagina Razor a cui punta: le sottovoci puntano
/// sempre a una pagina.
/// </summary>
public sealed class SottoMenu
{
    public Guid IdSottoMenu { get; set; }
    public Guid IdMenu { get; init; }
    public required string Descrizione { get; init; }
    public required string PaginaRazor { get; init; }
    public string? Icona { get; init; }
    public int Ordine { get; init; }
    public bool Attivo { get; init; } = true;
}
