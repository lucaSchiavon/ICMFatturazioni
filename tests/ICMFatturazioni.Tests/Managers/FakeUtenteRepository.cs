using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IUtenteRepository"/> per i test del
/// <c>UtenteManager</c>. Niente DB: uno store a dizionario e qualche hook per
/// pilotare gli scenari (es. <see cref="RuoliNomi"/> per la JOIN dell'elenco).
/// Lo username è confrontato case-insensitive, come la collation di colonna.
/// </summary>
internal sealed class FakeUtenteRepository : IUtenteRepository
{
    private readonly Dictionary<Guid, Utente> _store = new();

    /// <summary>Nome ruolo per id, usato solo da <see cref="GetAllConRuoloAsync"/>.</summary>
    public Dictionary<Guid, string> RuoliNomi { get; } = new();

    /// <summary>Accesso diretto allo store per le asserzioni dei test.</summary>
    public IReadOnlyDictionary<Guid, Utente> Store => _store;

    public Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var u = _store.Values.FirstOrDefault(
            x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<Utente?>(u);
    }

    public Task<Utente?> GetByIdAsync(Guid idUtente, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idUtente, out var u) ? u : null);

    public Task InsertAsync(Utente utente, CancellationToken cancellationToken = default)
    {
        _store[utente.IdUtente] = utente;
        return Task.CompletedTask;
    }

    public Task UpdateUltimoLoginAsync(Guid idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idUtente, out var u))
        {
            _store[idUtente] = Clone(u, ultimoLogin: istanteUtc);
        }
        return Task.CompletedTask;
    }

    public Task UpdateTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idUtente, out var u))
        {
            _store[idUtente] = Clone(u, tema: temaPreferito);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UtenteConRuolo>> GetAllConRuoloAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UtenteConRuolo> list = _store.Values
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(u => new UtenteConRuolo
            {
                IdUtente = u.IdUtente,
                Username = u.Username,
                Email = u.Email,
                IdRuolo = u.IdRuolo,
                RuoloNome = RuoliNomi.TryGetValue(u.IdRuolo, out var n) ? n : "?",
                Attivo = u.Attivo,
                HaPassword = !string.IsNullOrEmpty(u.PasswordHash),
                UltimoLoginUtc = u.UltimoLoginUtc,
            })
            .ToList();
        return Task.FromResult(list);
    }

    public Task<bool> ExistsUsernameAsync(string username, Guid? escludiIdUtente = null, CancellationToken cancellationToken = default)
    {
        var exists = _store.Values.Any(x =>
            string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase)
            && (escludiIdUtente is null || x.IdUtente != escludiIdUtente.Value));
        return Task.FromResult(exists);
    }

    public Task UpdateProfiloAsync(Guid idUtente, string username, string? email, Guid idRuolo, bool attivo, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idUtente, out var u))
        {
            _store[idUtente] = Clone(u, username: username, email: email, idRuolo: idRuolo, attivo: attivo);
        }
        return Task.CompletedTask;
    }

    public Task UpdatePasswordHashAsync(Guid idUtente, string? passwordHash, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idUtente, out var u))
        {
            _store[idUtente] = Clone(u, passwordHash: passwordHash, setPasswordHash: true);
        }
        return Task.CompletedTask;
    }

    // Utente ha proprietà init-only: per "aggiornare" si ricostruisce l'oggetto
    // copiando i campi e sovrascrivendo solo quelli richiesti.
    private static Utente Clone(
        Utente src,
        string? username = null,
        string? email = null,
        Guid? idRuolo = null,
        bool? attivo = null,
        string? tema = null,
        DateTime? ultimoLogin = null,
        string? passwordHash = null,
        bool setPasswordHash = false)
        => new()
        {
            IdUtente = src.IdUtente,
            Username = username ?? src.Username,
            Email = email ?? src.Email,
            PasswordHash = setPasswordHash ? passwordHash : src.PasswordHash,
            IdRuolo = idRuolo ?? src.IdRuolo,
            NomeCompleto = src.NomeCompleto,
            Attivo = attivo ?? src.Attivo,
            TemaPreferito = tema ?? src.TemaPreferito,
            UltimoLoginUtc = ultimoLogin ?? src.UltimoLoginUtc,
            CreatedAt = src.CreatedAt,
            UpdatedAt = src.UpdatedAt,
        };
}
