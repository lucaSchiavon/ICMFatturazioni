using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Dati pre-risolti che alimentano 1:1 il rendering del PDF (avviso o fattura).
/// Immutabile: una volta costruito, <see cref="AvvisoPdfDocument"/> lo trasforma in
/// byte senza alcun accesso a Manager/Repository/HTTP (rendering puro e testabile).
/// </summary>
internal sealed record AvvisoPdfData(
    Azienda Studio,
    Anagrafica Cliente,
    Attivita? Attivita,
    AvvisoFattura Testata,
    IReadOnlyList<AvvisoFatturaRiga> Righe,
    string? DescrizionePagamento,
    string? DescrizioneBanca,
    CalcoloFiscaleRisultato Calcolo,
    // Se valorizzato, il documento è la FATTURA nata da questo avviso (numero/data
    // fattura in barra titolo, banner "non valido ai fini fiscali", riferimento
    // avviso nel footer). Null → documento = avviso di parcella (comportamento base).
    Fattura? Fattura = null);

/// <summary>
/// Costruisce l'<see cref="AvvisoPdfData"/> a partire dall'Id di un avviso: legge
/// testata + righe, risolve gli snapshot di riferimento (cliente, azienda, attività,
/// pagamento, banca), ricalcola la cascata fiscale dagli snapshot congelati e (per la
/// fattura) allega l'entità <see cref="Fattura"/>. È il punto UNICO di assemblaggio
/// dati, condiviso da <see cref="AvvisoPdfService"/> e <c>FatturaPdfService</c>: la
/// UI non chiama mai i Repository, qui si passa solo dai Manager.
/// </summary>
internal sealed class AvvisoPdfDataBuilder
{
    private readonly IAvvisoFatturaManager   _avvisi;
    private readonly IAnagraficaManager       _anagrafiche;
    private readonly IAttivitaManager         _attivita;
    private readonly ICodicePagamentoManager  _pagamenti;
    private readonly IBancaAppoggioManager    _banche;
    private readonly ISpesaAnticipataManager  _spese;
    private readonly IAziendaManager          _azienda;

    public AvvisoPdfDataBuilder(
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

    /// <summary>
    /// Assembla i dati del documento per l'avviso indicato. <paramref name="fattura"/>
    /// non null → il documento sarà reso come fattura.
    /// </summary>
    /// <exception cref="AvvisoPdfNonTrovatoException">Avviso inesistente o annullato.</exception>
    /// <exception cref="AvvisoPdfDatiMancantiException">Dati azienda/cliente mancanti.</exception>
    public async Task<AvvisoPdfData> CostruisciAsync(Guid idAvviso, Fattura? fattura, CancellationToken ct = default)
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
                "Dati dell'azienda emittente non configurati: impossibile generare il documento.");
        var cliente = clienteT.Result
            ?? throw new AvvisoPdfDatiMancantiException("Cliente del documento non trovato.");

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

        return new AvvisoPdfData(
            Studio:               studio,
            Cliente:              cliente,
            Attivita:             attivT.Result,
            Testata:              testata,
            Righe:                dettaglio.Righe,
            DescrizionePagamento: descrizionePagamento,
            DescrizioneBanca:     descrizioneBanca,
            Calcolo:              calcolo,
            Fattura:              fattura);
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

/// <summary>
/// Implementazione di <see cref="IAvvisoPdfService"/>. Delega l'assemblaggio dati al
/// <see cref="AvvisoPdfDataBuilder"/> condiviso e il layout ad <see cref="AvvisoPdfDocument"/>.
/// MigraDoc lavora in modo sincrono: nessun Task.Run (una richiesta, niente
/// parallelismo da sfruttare), coerente con VerbalePdfService di ICMVerbali.
/// </summary>
public sealed class AvvisoPdfService : IAvvisoPdfService
{
    private readonly AvvisoPdfDataBuilder _builder;

    internal AvvisoPdfService(AvvisoPdfDataBuilder builder) => _builder = builder;

    /// <inheritdoc/>
    public async Task<byte[]> GeneraAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var data = await _builder.CostruisciAsync(idAvviso, fattura: null, ct);
        return new AvvisoPdfDocument(data).Render();
    }
}
