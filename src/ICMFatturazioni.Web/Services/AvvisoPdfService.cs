using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Dati pre-risolti che alimentano 1:1 il rendering del PDF di avviso. Immutabile:
/// una volta costruito, <see cref="AvvisoPdfDocument"/> lo trasforma in byte senza
/// alcun accesso a Manager/Repository/HTTP (rendering puro e testabile).
/// </summary>
internal sealed record AvvisoPdfData(
    Azienda Studio,
    Anagrafica Cliente,
    Attivita? Attivita,
    AvvisoFattura Testata,
    IReadOnlyList<AvvisoFatturaRiga> Righe,
    string? DescrizionePagamento,
    string? DescrizioneBanca,
    CalcoloFiscaleRisultato Calcolo);

/// <summary>
/// Implementazione di <see cref="IAvvisoPdfService"/>. Orchestrazione asincrona:
/// legge testata + righe, risolve gli snapshot di riferimento (cliente, azienda,
/// attività, pagamento, banca), ricalcola la cascata fiscale dagli snapshot
/// congelati sull'avviso e delega il layout a <see cref="AvvisoPdfDocument"/>.
/// La UI non chiama mai i Repository: qui si passa solo dai Manager.
/// </summary>
public sealed class AvvisoPdfService : IAvvisoPdfService
{
    private readonly IAvvisoFatturaManager   _avvisi;
    private readonly IAnagraficaManager       _anagrafiche;
    private readonly IAttivitaManager         _attivita;
    private readonly ICodicePagamentoManager  _pagamenti;
    private readonly IBancaAppoggioManager    _banche;
    private readonly ISpesaAnticipataManager  _spese;
    private readonly IAziendaManager          _azienda;

    public AvvisoPdfService(
        IAvvisoFatturaManager avvisi,
        IAnagraficaManager anagrafiche,
        IAttivitaManager attivita,
        ICodicePagamentoManager pagamenti,
        IBancaAppoggioManager banche,
        ISpesaAnticipataManager spese,
        IAziendaManager azienda)
    {
        _avvisi      = avvisi;
        _anagrafiche = anagrafiche;
        _attivita    = attivita;
        _pagamenti   = pagamenti;
        _banche      = banche;
        _spese       = spese;
        _azienda     = azienda;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GeneraAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var dettaglio = await _avvisi.GetDettaglioAsync(idAvviso, ct);
        if (dettaglio is null || !dettaglio.Testata.IsAttivo)
            throw new AvvisoPdfNonTrovatoException(idAvviso);

        var testata = dettaglio.Testata;

        // Letture indipendenti in parallelo (singola richiesta: nessun rischio DbContext).
        var studioT  = _azienda.GetAziendaAsync(ct);
        var clienteT = _anagrafiche.GetByIdAsync(testata.IdAnagrafica, ct);
        var attivT   = _attivita.GetByIdAsync(testata.IdAttivita, ct);
        var speseT   = _spese.ElencoPerAvvisoAsync(idAvviso, ct);
        await Task.WhenAll(studioT, clienteT, attivT, speseT);

        var studio = studioT.Result
            ?? throw new AvvisoPdfDatiMancantiException(
                "Dati dell'azienda emittente non configurati: impossibile generare l'avviso.");
        var cliente = clienteT.Result
            ?? throw new AvvisoPdfDatiMancantiException("Cliente dell'avviso non trovato.");

        // Descrizioni pagamento/banca: snapshot di soli id sulla testata → lookup.
        string? descrizionePagamento = null;
        if (testata.IdCodicePagamento is { } idPag)
            descrizionePagamento = (await _pagamenti.GetByIdAsync(idPag, ct))?.DescrPag;

        string? descrizioneBanca = null;
        if (testata.IdBancaAppoggio is { } idBanca)
            descrizioneBanca = ComponiBanca(await _banche.GetByIdAsync(idBanca, ct));

        // Cascata fiscale ricostruita dagli snapshot congelati sull'avviso: importo
        // autorevole = somma righe reali; spese art.15 = somma spese collegate.
        var imponibile = dettaglio.Righe.Where(r => !r.IsDescrittiva).Sum(r => r.Importo ?? 0m);
        var speseArt15 = speseT.Result.Sum(s => s.Importo);
        var calcolo    = _avvisi.Calcola(testata, imponibile, speseArt15);

        var data = new AvvisoPdfData(
            Studio:               studio,
            Cliente:              cliente,
            Attivita:             attivT.Result,
            Testata:              testata,
            Righe:                dettaglio.Righe,
            DescrizionePagamento: descrizionePagamento,
            DescrizioneBanca:     descrizioneBanca,
            Calcolo:              calcolo);

        // MigraDoc lavora in modo sincrono: nessun Task.Run (una richiesta, niente
        // parallelismo da sfruttare), coerente con VerbalePdfService.
        return new AvvisoPdfDocument(data).Render();
    }

    // "Banca - Agenzia - IBAN: xxx", omettendo le parti mancanti.
    private static string? ComponiBanca(Models.BancaAppoggioRiga? ba)
    {
        if (ba is null) return null;
        var parti = new List<string> { ba.BancaNome };
        if (!string.IsNullOrWhiteSpace(ba.AgenziaNome)) parti.Add(ba.AgenziaNome!);
        if (!string.IsNullOrWhiteSpace(ba.IBAN))        parti.Add($"IBAN: {ba.IBAN}");
        return string.Join(" - ", parti);
    }
}
