using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Dati pre-risolti che alimentano 1:1 il rendering del report "Riepilogo
/// attività consulente". Immutabile: <see cref="RiepilogoConsulentePdfDocument"/>
/// lo trasforma in byte senza alcun accesso a Manager/Repository (stesso schema
/// di <c>ScadenzarioPdfData</c>).
/// </summary>
/// <param name="Righe">Righe già filtrate (SOLO carico Studio, D-C1) e ordinate consulente→cliente→attività.</param>
/// <param name="TranchePerRiga">Tranche attive indicizzate per riga consulenza.</param>
/// <param name="NomeConsulente">Nome del consulente (variante singola); null = variante generale.</param>
/// <param name="DescrizioneFiltro">Testo del filtro usato, per il piè di pagina.</param>
/// <param name="GeneratoIl">Timestamp locale di generazione.</param>
internal sealed record RiepilogoConsulentePdfData(
    IReadOnlyList<SchedaConsulenzaRiga> Righe,
    IReadOnlyDictionary<Guid, IReadOnlyList<AttivitaConsulentePagamento>> TranchePerRiga,
    string? NomeConsulente,
    string DescrizioneFiltro,
    DateTime GeneratoIl);

/// <summary>
/// Implementazione di <see cref="IRiepilogoConsulentePdfService"/>: carica le
/// righe e le tranche via Manager (mai Repository), applica i filtri (D-C1:
/// solo carico Studio; stato D-C4; raffinamenti cliente/attività) e delega il
/// layout a <see cref="RiepilogoConsulentePdfDocument"/>.
/// </summary>
public sealed class RiepilogoConsulentePdfService : IRiepilogoConsulentePdfService
{
    private readonly IAttivitaConsulenteManager          _consulenze;
    private readonly IAttivitaConsulentePagamentoManager _pagamenti;
    private readonly IConsulenteManager                  _consulenti;

    public RiepilogoConsulentePdfService(
        IAttivitaConsulenteManager consulenze,
        IAttivitaConsulentePagamentoManager pagamenti,
        IConsulenteManager consulenti)
    {
        _consulenze = consulenze;
        _pagamenti  = pagamenti;
        _consulenti = consulenti;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GeneraAsync(FiltroRiepilogoConsulente filtro, CancellationToken ct = default)
    {
        var tutte = filtro.IdConsulente is { } idConsulente
            ? await _consulenze.SchedaConsulenteAsync(idConsulente, ct)
            : await _consulenze.SchedaGeneraleAsync(ct);

        var righe = FiltraRighe(tutte, filtro);

        // Tranche solo per le righe che compaiono nel report.
        var idRighe = righe.Select(r => r.IdAttivitaConsulente).ToHashSet();
        var tranche = (await _pagamenti.ElencoPerConsulenteAsync(filtro.IdConsulente, ct))
            .Where(p => idRighe.Contains(p.IdAttivitaConsulente))
            .GroupBy(p => p.IdAttivitaConsulente)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AttivitaConsulentePagamento>)g.ToList());

        // Nome del consulente per il titolo (variante singola).
        string? nomeConsulente = filtro.IdConsulente is { } id
            ? (await _consulenti.GetByIdAsync(id, ct))?.Descrizione ?? "?"
            : null;

        // Nomi leggibili per il piè di pagina, presi dalle righe stesse (i
        // raffinamenti nascono dalla scheda, quindi se il filtro è valorizzato
        // c'è almeno una riga che lo descrive; in assenza di righe → "?").
        string? nomeCliente = filtro.IdAnagrafica is not null
            ? tutte.FirstOrDefault(r => r.IdAnagrafica == filtro.IdAnagrafica)?.RagioneSociale ?? "?"
            : null;
        string? nomeAttivita = filtro.IdAttivita is not null
            ? tutte.FirstOrDefault(r => r.IdAttivita == filtro.IdAttivita) is { } riga
                ? $"{riga.AttivitaNumero}-{riga.AttivitaDescrizione}"
                : "?"
            : null;

        var data = new RiepilogoConsulentePdfData(
            Righe:             righe,
            TranchePerRiga:    tranche,
            NomeConsulente:    nomeConsulente,
            DescrizioneFiltro: ComponiDescrizioneFiltro(filtro, nomeCliente, nomeAttivita),
            GeneratoIl:        DateTime.Now);

        return new RiepilogoConsulentePdfDocument(data).Render();
    }

    /// <summary>
    /// Applica i filtri del report alle righe della scheda. Funzione pura, unit-testata:
    ///   • D-C1: SOLO consulenze a carico dello Studio (i campioni legacy lo provano);
    ///   • stato D-C4: aperta = residuo &gt; 0;
    ///   • raffinamenti cliente/attività.
    /// </summary>
    internal static IReadOnlyList<SchedaConsulenzaRiga> FiltraRighe(
        IReadOnlyList<SchedaConsulenzaRiga> righe, FiltroRiepilogoConsulente filtro)
        => righe
            .Where(r => r.Carico == CaricoConsulenza.Studio)
            .Where(r => filtro.IdAnagrafica is null || r.IdAnagrafica == filtro.IdAnagrafica)
            .Where(r => filtro.IdAttivita is null || r.IdAttivita == filtro.IdAttivita)
            .Where(r => filtro.Stato switch
            {
                FiltroStatoConsulenze.Aperte => r.Aperta,
                FiltroStatoConsulenze.Chiuse => !r.Aperta,
                _                            => true,
            })
            .ToList();

    /// <summary>
    /// Descrizione human-readable del filtro per il piè di pagina, nello stesso
    /// formato del report legacy ("Tutti i Clienti - Tutte le attività - Solo
    /// consulenze aperte"). Funzione pura: unit-testata direttamente.
    /// </summary>
    internal static string ComponiDescrizioneFiltro(
        FiltroRiepilogoConsulente filtro, string? nomeCliente, string? nomeAttivita)
    {
        var parti = new List<string>
        {
            filtro.IdAnagrafica is not null ? $"Cliente: {nomeCliente ?? "?"}" : "Tutti i Clienti",
            filtro.IdAttivita is not null ? $"Attività: {nomeAttivita ?? "?"}" : "Tutte le attività",
            filtro.Stato switch
            {
                FiltroStatoConsulenze.Aperte => "Solo consulenze aperte",
                FiltroStatoConsulenze.Chiuse => "Solo consulenze chiuse",
                _                            => "Tutte le consulenze",
            },
        };
        return string.Join(" - ", parti);
    }
}
