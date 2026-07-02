using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ISpesaAnticipataManager"/>.
/// Valida i campi, assegna il GUID v7 e traccia ogni scrittura in <c>fatt.Audit</c>
/// (best-effort: un fallimento dell'audit non fa fallire l'operazione di business).
/// </summary>
public sealed class SpesaAnticipataManager : ISpesaAnticipataManager
{
    private readonly ISpesaAnticipataRepository _repo;
    private readonly IAuditManager              _audit;

    public SpesaAnticipataManager(ISpesaAnticipataRepository repo, IAuditManager audit)
    {
        _repo  = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<SpesaAnticipata>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetByAttivitaAsync(idAttivita, ct);

    public Task<IReadOnlyList<SpesaAnticipata>> ElencoFatturabiliPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetFatturabiliByAttivitaAsync(idAttivita, ct);

    public Task<IReadOnlyList<SpesaAnticipata>> ElencoPerAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
        => _repo.GetByAvvisoAsync(idAvviso, ct);

    /// <inheritdoc/>
    public async Task<Guid> CreaAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        ValidaCampi(spesa);

        spesa.IdSpesaAnticipata = Guid.CreateVersion7();
        await _repo.InsertAsync(spesa, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "SpesaAnticipata", spesa.IdSpesaAnticipata,
                spesa.Descrizione,
                AuditDettaglio.Snapshot(new
                {
                    spesa.IdAttivita,
                    spesa.Data,
                    spesa.Descrizione,
                    spesa.Importo,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        return spesa.IdSpesaAnticipata;
    }

    /// <inheritdoc/>
    public async Task AggiornaAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        ValidaCampi(spesa);

        var prima = await _repo.GetByIdAsync(spesa.IdSpesaAnticipata, ct);
        // Lock: una spesa già associata a un avviso è congelata (si legge lo stato
        // persistito: l'entità in arrivo dalla UI non porta il link).
        if (prima is { IsInAvviso: true })
            throw new SpesaAnticipataInvalidaException(
                SpesaAnticipataMotivoInvalido.SpesaInAvviso,
                "Spesa già associata a un avviso di fattura: annulla l'avviso per poterla modificare.");

        await _repo.UpdateAsync(spesa, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "SpesaAnticipata", spesa.IdSpesaAnticipata,
                spesa.Descrizione,
                prima is not null ? AuditDettaglio.Diff(prima, spesa) : null,
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task EliminaAsync(Guid idSpesaAnticipata, CancellationToken ct = default)
    {
        var spesa = await _repo.GetByIdAsync(idSpesaAnticipata, ct);
        if (spesa is null) return;

        // Lock: una spesa già associata a un avviso non si elimina.
        if (spesa.IsInAvviso)
            throw new SpesaAnticipataInvalidaException(
                SpesaAnticipataMotivoInvalido.SpesaInAvviso,
                "Spesa già associata a un avviso di fattura: annulla l'avviso per poterla eliminare.");

        await _repo.DisattivaAsync(idSpesaAnticipata, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "SpesaAnticipata", idSpesaAnticipata,
                spesa.Descrizione,
                AuditDettaglio.Snapshot(new
                {
                    spesa.IdAttivita,
                    spesa.Data,
                    spesa.Descrizione,
                    spesa.Importo,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    private static void ValidaCampi(SpesaAnticipata s)
    {
        if (s.Data == default)
            throw new SpesaAnticipataInvalidaException(
                SpesaAnticipataMotivoInvalido.DataObbligatoria,
                "La data della spesa è obbligatoria.");

        if (string.IsNullOrWhiteSpace(s.Descrizione))
            throw new SpesaAnticipataInvalidaException(
                SpesaAnticipataMotivoInvalido.DescrizioneObbligatoria,
                "La descrizione della spesa è obbligatoria.");

        if (s.Importo <= 0)
            throw new SpesaAnticipataInvalidaException(
                SpesaAnticipataMotivoInvalido.ImportoNonValido,
                "L'importo deve essere maggiore di zero.");
    }
}
