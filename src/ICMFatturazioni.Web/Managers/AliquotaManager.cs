using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAliquotaManager"/>. Valida i campi, assegna il
/// GUID v7, protegge le aliquote di sistema dall'eliminazione e traccia ogni
/// scrittura in <c>fatt.Audit</c> (best-effort).
/// </summary>
public sealed class AliquotaManager : IAliquotaManager
{
    // Codici delle aliquote di sistema in fatt.Aliquote (seed migration 055).
    private const string CodiceCnpaia   = "CNPAIA";
    private const string CodiceRitenuta = "RITENUTA";

    // Default di fallback se un codice non è presente in tabella.
    private const decimal DefaultCnpaia   = 4m;
    private const decimal DefaultRitenuta = 20m;

    private readonly IAliquotaRepository _repo;
    private readonly IAuditManager       _audit;

    public AliquotaManager(IAliquotaRepository repo, IAuditManager audit)
    {
        _repo  = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<Aliquota>> ElencoAsync(CancellationToken ct = default)
        => _repo.GetAttiviAsync(ct);

    public Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default)
        => _repo.GetByIdAsync(idAliquota, ct);

    /// <inheritdoc/>
    public async Task<Guid> CreaAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        ValidaCampi(aliquota);

        aliquota.IdAliquota = Guid.CreateVersion7();
        await _repo.InsertAsync(aliquota, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "Aliquota", aliquota.IdAliquota, aliquota.Descrizione,
                AuditDettaglio.Snapshot(new { aliquota.Codice, aliquota.Descrizione, aliquota.Valore }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        return aliquota.IdAliquota;
    }

    /// <inheritdoc/>
    public async Task AggiornaAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        ValidaCampi(aliquota);

        var prima = await _repo.GetByIdAsync(aliquota.IdAliquota, ct);
        await _repo.UpdateAsync(aliquota, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "Aliquota", aliquota.IdAliquota, aliquota.Descrizione,
                prima is not null ? AuditDettaglio.Diff(prima, aliquota) : null,
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task EliminaAsync(Guid idAliquota, CancellationToken ct = default)
    {
        var aliquota = await _repo.GetByIdAsync(idAliquota, ct);
        if (aliquota is null) return;

        // Le aliquote di sistema (con Codice) sono usate dal calcolo dell'avviso:
        // non eliminabili (valore e descrizione restano comunque modificabili).
        if (aliquota.IsSistema)
            throw new AliquotaInvalidaException(
                AliquotaMotivoInvalido.AliquotaDiSistema,
                "Questa è un'aliquota di sistema usata dal calcolo dell'avviso e non può essere eliminata. Puoi modificarne il valore.");

        await _repo.DisattivaAsync(idAliquota, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "Aliquota", idAliquota, aliquota.Descrizione,
                AuditDettaglio.Snapshot(new { aliquota.Codice, aliquota.Descrizione, aliquota.Valore }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task<AliquoteFiscali> GetAliquoteAvvisoAsync(CancellationToken ct = default)
    {
        var attive = await _repo.GetAttiviAsync(ct);

        var cnpaia = attive.FirstOrDefault(a =>
            string.Equals(a.Codice, CodiceCnpaia, StringComparison.OrdinalIgnoreCase))?.Valore ?? DefaultCnpaia;

        var ritenuta = attive.FirstOrDefault(a =>
            string.Equals(a.Codice, CodiceRitenuta, StringComparison.OrdinalIgnoreCase))?.Valore ?? DefaultRitenuta;

        return new AliquoteFiscali(cnpaia, ritenuta);
    }

    private static void ValidaCampi(Aliquota a)
    {
        if (string.IsNullOrWhiteSpace(a.Descrizione))
            throw new AliquotaInvalidaException(
                AliquotaMotivoInvalido.DescrizioneObbligatoria,
                "La descrizione dell'aliquota è obbligatoria.");

        // Aliquota percentuale plausibile: 0..100.
        if (a.Valore < 0 || a.Valore > 100)
            throw new AliquotaInvalidaException(
                AliquotaMotivoInvalido.ValoreNonValido,
                "Il valore dell'aliquota deve essere compreso tra 0 e 100.");
    }
}
