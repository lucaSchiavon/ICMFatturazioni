using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Implementazione di <see cref="ICurrentUserAccessor"/> basata su
/// <see cref="AuthenticationStateProvider"/>: funziona nei circuiti Blazor
/// Server (dove <c>HttpContext</c> è null) leggendo i claim del cookie firmati
/// al login. L'id arriva da <see cref="ClaimTypes.NameIdentifier"/> (= IdUtente)
/// e il nome da <see cref="ClaimTypes.Name"/> (= Username), coerenti con
/// <c>SignInUtenteAsync</c> in Program.cs.
/// </summary>
internal sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly AuthenticationStateProvider _authState;

    public CurrentUserAccessor(AuthenticationStateProvider authState) => _authState = authState;

    public async Task<(Guid? Id, string? Nome)> GetAsync(CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal user;
        try
        {
            // Fuori da un circuito autenticato (seed all'avvio, endpoint
            // anonimi) la chiamata può lanciare o restituire uno stato anonimo:
            // in entrambi i casi → nessun utente.
            var state = await _authState.GetAuthenticationStateAsync();
            user = state.User;
        }
        catch
        {
            return (null, null);
        }

        if (user.Identity?.IsAuthenticated != true)
        {
            return (null, null);
        }

        var idRaw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var nome = user.FindFirst(ClaimTypes.Name)?.Value;
        Guid? id = Guid.TryParse(idRaw, out var g) ? g : null;
        return (id, nome);
    }
}
