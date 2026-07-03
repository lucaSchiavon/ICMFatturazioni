using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Implementazione di <see cref="IFatturaPdfService"/>. Carica la fattura, riusa il
/// builder condiviso per assemblare i dati dell'avviso di origine (allegando l'entità
/// fattura) e delega il layout ad <see cref="AvvisoPdfDocument"/> in modalità fattura.
/// </summary>
public sealed class FatturaPdfService : IFatturaPdfService
{
    private readonly AvvisoPdfDataBuilder _builder;
    private readonly IFattureManager      _fatture;

    internal FatturaPdfService(AvvisoPdfDataBuilder builder, IFattureManager fatture)
    {
        _builder = builder;
        _fatture = fatture;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GeneraAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _fatture.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaPdfNonTrovatoException(idFattura);

        var data = await _builder.CostruisciAsync(fattura.IdAvviso, fattura, ct);
        return new AvvisoPdfDocument(data).Render();
    }
}
