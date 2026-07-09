using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Dati pre-risolti che alimentano 1:1 il rendering del PDF dello scadenzario.
/// Immutabile: una volta costruito, <see cref="ScadenzarioPdfDocument"/> lo
/// trasforma in byte senza alcun accesso a Manager/Repository (rendering puro
/// e testabile, stesso schema di <c>AvvisoPdfData</c>).
/// </summary>
/// <param name="Righe">Righe già filtrate e ordinate per data scadenza.</param>
/// <param name="DescrizioneFiltro">Testo del filtro usato, per il piè di pagina (spec).</param>
/// <param name="GeneratoIl">Timestamp locale di generazione, stampato nel piè di pagina.</param>
internal sealed record ScadenzarioPdfData(
    IReadOnlyList<ScadenzaReport> Righe,
    string DescrizioneFiltro,
    DateTime GeneratoIl);

/// <summary>
/// Implementazione di <see cref="IScadenzarioPdfService"/>: carica le righe via
/// <see cref="IScadenzaPagamentoManager"/> (la UI e gli endpoint non toccano mai
/// i Repository), risolve i nomi di cliente/tipo attività per la descrizione del
/// filtro e delega il layout a <see cref="ScadenzarioPdfDocument"/>.
/// </summary>
public sealed class ScadenzarioPdfService : IScadenzarioPdfService
{
    private readonly IScadenzaPagamentoManager _scadenze;
    private readonly IAnagraficaManager        _anagrafiche;
    private readonly ITipoAttivitaManager      _tipiAttivita;
    private readonly IAttivitaManager          _attivita;

    public ScadenzarioPdfService(
        IScadenzaPagamentoManager scadenze,
        IAnagraficaManager        anagrafiche,
        ITipoAttivitaManager      tipiAttivita,
        IAttivitaManager          attivita)
    {
        _scadenze     = scadenze;
        _anagrafiche  = anagrafiche;
        _tipiAttivita = tipiAttivita;
        _attivita     = attivita;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GeneraAsync(FiltroScadenzario filtro, CancellationToken ct = default)
    {
        var righe = await _scadenze.ReportScadenzarioAsync(filtro, ct);

        // Nomi leggibili per il piè di pagina (solo se il filtro li referenzia).
        string? nomeCliente = filtro.IdAnagrafica is { } idAnagrafica
            ? (await _anagrafiche.GetByIdAsync(idAnagrafica, ct))?.RagioneSociale
            : null;
        string? nomeTipoAttivita = filtro.IdTipoAttivita is { } idTipoAttivita
            ? (await _tipiAttivita.GetByIdAsync(idTipoAttivita, ct))?.Descrizione
            : null;
        // Attività specifica: etichetta "n. {Numero} — {Descrizione}" (null = tutte).
        string? nomeAttivita = filtro.IdAttivita is { } idAttivita
            && await _attivita.GetByIdAsync(idAttivita, ct) is { } att
            ? $"n. {att.Numero} — {att.Descrizione}"
            : null;

        var data = new ScadenzarioPdfData(
            Righe:             righe,
            DescrizioneFiltro: ComponiDescrizioneFiltro(filtro, nomeCliente, nomeTipoAttivita, nomeAttivita),
            GeneratoIl:        DateTime.Now);

        return new ScadenzarioPdfDocument(data).Render();
    }

    /// <summary>
    /// Compone la descrizione human-readable del filtro per il piè di pagina del
    /// report (richiesta esplicita della spec). Le scelte "neutre" (Tutte/Entrambe)
    /// dei radio scadute/evase si omettono; il caso base è il classico
    /// "Tutti i Clienti - Tutte le Attività" del report legacy.
    /// Funzione pura: unit-testata direttamente.
    /// </summary>
    internal static string ComponiDescrizioneFiltro(
        FiltroScadenzario filtro,
        string? nomeCliente,
        string? nomeTipoAttivita,
        string? nomeAttivita)
    {
        var parti = new List<string>();

        // Cliente: la selezione puntuale prevale sul filtro per tipologia.
        if (filtro.IdAnagrafica is not null)
            parti.Add($"Cliente: {nomeCliente ?? "?"}");
        else if (filtro.TipoCliente is { } tipo)
            parti.Add(tipo switch
            {
                TipoAnagrafica.Societa      => "Clienti: Società",
                TipoAnagrafica.Privato      => "Clienti: Privati",
                TipoAnagrafica.EntePubblico => "Clienti: Enti pubblici",
                _                           => "Tutti i Clienti",
            });
        else
            parti.Add("Tutti i Clienti");

        parti.Add(filtro.IdTipoAttivita is not null
            ? $"Attività: {nomeTipoAttivita ?? "?"}"
            : "Tutte le Attività");

        // Attività specifica selezionata (voce distinta dal tipo attività qui sopra).
        if (filtro.IdAttivita is not null)
            parti.Add($"Attività {nomeAttivita ?? "?"}");

        if (filtro.DallaData is { } dal && filtro.AllaData is { } al)
            parti.Add($"Scadenze dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}");
        else if (filtro.DallaData is { } soloDal)
            parti.Add($"Scadenze dal {soloDal:dd/MM/yyyy}");
        else if (filtro.AllaData is { } soloAl)
            parti.Add($"Scadenze fino al {soloAl:dd/MM/yyyy}");

        if (filtro.Scadute == FiltroScadute.SoloScadute)    parti.Add("Solo scadute");
        if (filtro.Scadute == FiltroScadute.SoloNonScadute) parti.Add("Solo non scadute");
        if (filtro.Evase   == FiltroEvase.SoloEvase)        parti.Add("Solo evase");
        if (filtro.Evase   == FiltroEvase.SoloNonEvase)     parti.Add("Solo non evase");

        return string.Join(" - ", parti);
    }
}
