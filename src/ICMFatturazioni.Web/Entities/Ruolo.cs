namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Ruolo applicativo. POCO mappato su <c>fatt.Ruoli</c>.
/// </summary>
/// <remarks>
/// I ruoli sono RIGHE di tabella (non un enum): l'amministratore può crearne
/// di nuovi a runtime. Due ruoli sono di SISTEMA (<see cref="IsSistema"/> =
/// true) e identificati da un <see cref="Codice"/> stabile
/// (<see cref="RuoliSistema"/>): <c>Superadmin</c> e <c>Admin</c>. Non sono
/// eliminabili né rinominabili dalla UI. I ruoli custom hanno
/// <see cref="Codice"/> null e la loro visibilità è guidata dal mapping
/// ruolo↔menu (vedi Tappa T2 / memoria menu-ruoli-dinamici).
/// </remarks>
public sealed class Ruolo
{
    /// <summary>PK GUID (v7 app-side per i custom; fisso per i ruoli di sistema).</summary>
    public Guid IdRuolo { get; set; }

    /// <summary>
    /// Codice stabile dei ruoli di sistema (<c>SUPERADMIN</c>/<c>ADMIN</c>),
    /// usato dal codice per riconoscerli a prescindere dal nome visualizzato.
    /// Null per i ruoli custom.
    /// </summary>
    public string? Codice { get; init; }

    /// <summary>Nome visualizzato (colonna <c>Ruolo</c>). Modificabile per i custom.</summary>
    public required string Nome { get; init; }

    public string? Descrizione { get; init; }

    /// <summary>Ruolo di sistema: non eliminabile né rinominabile.</summary>
    public bool IsSistema { get; init; }

    public bool IsAttivo { get; init; } = true;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>True se è il ruolo di servizio Superadmin (vede tutto + log errori).</summary>
    public bool ESuperadmin => string.Equals(Codice, RuoliSistema.Superadmin, StringComparison.Ordinal);

    /// <summary>True se è il ruolo Admin (vede tutto tranne il log errori).</summary>
    public bool EAdmin => string.Equals(Codice, RuoliSistema.Admin, StringComparison.Ordinal);
}
