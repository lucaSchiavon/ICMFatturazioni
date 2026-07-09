using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Web.Validation;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAziendaManager"/>: lettura del cedente corrente
/// e salvataggio "singleton" (una sola riga attiva su <c>fatt.Azienda</c>).
/// </summary>
/// <remarks>
/// Il salvataggio è un get-or-create pigro: alla prima configurazione crea la riga
/// (GUID v7 app-side, ADR D22) con audit di creazione; nelle volte successive
/// aggiorna la riga esistente con audit del diff (Regola 7). Le validazioni di
/// formato riusano <see cref="AnagraficaValidator"/> (uniformità con i form).
/// </remarks>
public sealed class AziendaManager : IAziendaManager
{
    private const string EntityType = nameof(Azienda);

    private readonly IAziendaRepository _repo;
    private readonly IAuditManager _audit;

    public AziendaManager(IAziendaRepository repo, IAuditManager audit)
    {
        _repo = repo;
        _audit = audit;
    }

    /// <inheritdoc/>
    public Task<Azienda?> GetAziendaAsync(CancellationToken ct = default)
        => _repo.GetAziendaAsync(ct);

    /// <inheritdoc/>
    public async Task<Guid> SalvaCedenteAsync(Azienda input, CancellationToken ct = default)
    {
        Valida(input);

        var esistente = await _repo.GetAziendaAsync(ct);

        if (esistente is null)
        {
            // Prima configurazione: creazione pigra della riga cedente.
            var nuovo = Normalizza(input, Guid.CreateVersion7(), isAttivo: true);
            await _repo.InsertAsync(nuovo, ct);
            await _audit.RegistraCreazioneAsync(EntityType, nuovo.IdAzienda,
                nuovo.RagioneSociale, AuditDettaglio.Snapshot(nuovo), ct);
            return nuovo.IdAzienda;
        }

        // Aggiornamento della riga esistente: si conservano Id e stato attivo.
        var aggiornato = Normalizza(input, esistente.IdAzienda, esistente.IsAttivo);
        await _repo.UpdateAsync(aggiornato, ct);
        await _audit.RegistraModificaAsync(EntityType, aggiornato.IdAzienda,
            aggiornato.RagioneSociale, AuditDettaglio.Diff(esistente, aggiornato), ct);
        return aggiornato.IdAzienda;
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Validazione di forma. L'ordine dei controlli è quello che l'utente vedrà
    /// come motivo del fallimento (CLAUDE.md "Ordine dei controlli è UX").
    /// </summary>
    private static void Valida(Azienda a)
    {
        if (string.IsNullOrWhiteSpace(a.RagioneSociale))
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.RagioneSocialeObbligatoria,
                "La ragione sociale è obbligatoria.");

        if (string.IsNullOrWhiteSpace(a.NomeBreve))
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.NomeBreveObbligatorio,
                "Il nome breve (alias interno) è obbligatorio.");

        // Campi facoltativi: validati solo se valorizzati (i Valida* tornano null su vuoto).
        if (AnagraficaValidator.ValidaPartitaIva(a.PIVA) is not null)
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.PivaNonValida,
                "La partita IVA non è valida (11 cifre, controllo di coerenza fallito).");

        if (AnagraficaValidator.ValidaCodiceFiscaleAzienda(a.CodiceFiscale) is not null)
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.CodiceFiscaleNonValido,
                "Il codice fiscale non è valido (11 cifre per società o 16 caratteri per persona).");

        if (AnagraficaValidator.ValidaCap(a.IndirizzoCAP) is not null)
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.CapNonValido,
                "Il CAP deve essere di 5 cifre.");

        if (AnagraficaValidator.ValidaEmail(a.Email) is not null)
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.EmailNonValida,
                "L'indirizzo email non è valido.");

        if (AnagraficaValidator.ValidaPec(a.PEC) is not null)
            throw new AziendaInvalidaException(
                AziendaInvalidaMotivo.PecNonValida,
                "L'indirizzo PEC non è valido.");
    }

    // Trim di tutti i campi testo (vuoto → null per gli opzionali); NomeBreve e
    // RagioneSociale restano non-null (già garantiti da Valida). Id e IsAttivo
    // sono decisi dal chiamante (nuova riga vs riga esistente).
    private static Azienda Normalizza(Azienda a, Guid id, bool isAttivo) => new()
    {
        IdAzienda                  = id,
        NomeBreve                  = a.NomeBreve.Trim(),
        RagioneSociale             = a.RagioneSociale.Trim(),
        PIVA                       = Pulisci(a.PIVA),
        CodiceFiscale              = Pulisci(a.CodiceFiscale),
        IndirizzoVia               = Pulisci(a.IndirizzoVia),
        IndirizzoCivico            = Pulisci(a.IndirizzoCivico),
        IndirizzoComune            = Pulisci(a.IndirizzoComune),
        IndirizzoProvincia         = Pulisci(a.IndirizzoProvincia),
        IndirizzoCAP               = Pulisci(a.IndirizzoCAP),
        IndirizzoPaese             = Pulisci(a.IndirizzoPaese),
        Telefono                   = Pulisci(a.Telefono),
        Telefax                    = Pulisci(a.Telefax),
        Email                      = Pulisci(a.Email),
        PEC                        = Pulisci(a.PEC),
        REA                        = Pulisci(a.REA),
        REAFe                      = Pulisci(a.REAFe),
        CCIAA                      = Pulisci(a.CCIAA),
        CCIAAFe                    = Pulisci(a.CCIAAFe),
        CapitaleSociale            = Pulisci(a.CapitaleSociale),
        CapitaleSocialeFe          = Pulisci(a.CapitaleSocialeFe),
        RegimeFiscale              = Pulisci(a.RegimeFiscale),
        StatoLiquidazione          = Pulisci(a.StatoLiquidazione),
        SocioUnico                 = Pulisci(a.SocioUnico),
        Identificativo             = Pulisci(a.Identificativo),
        ApplicaCassaPrevidenziale  = a.ApplicaCassaPrevidenziale,
        TipoCassaFe                = a.ApplicaCassaPrevidenziale ? Pulisci(a.TipoCassaFe) : null,
        SoggettoARitenuta          = a.SoggettoARitenuta,
        TipoRitenutaFe             = a.SoggettoARitenuta ? Pulisci(a.TipoRitenutaFe) : null,
        CausalePagamentoRitenutaFe = a.SoggettoARitenuta ? Pulisci(a.CausalePagamentoRitenutaFe) : null,
        IsAttivo                   = isAttivo,
    };

    private static string? Pulisci(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
