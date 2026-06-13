using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Vista "ricca" di un codice di pagamento per la griglia: il codice con il tipo
/// (descrizione/sigla/flag) e le descrizioni di condizione/modalità già risolte
/// (JOIN su <c>fatt.TipiPagamento</c> e sulle lookup FE).
/// </summary>
public sealed record CodicePagamentoRiga(
    Guid IdCodicePagamento,
    Guid IdTipoPagamento,
    string TipoDescrizione,
    FlagBanca FlagBanca,
    string DescrPag,
    int NumScadenze,
    int GGScad1,
    int? GGScad2,
    int? GGScad3,
    int? GGpiu,
    bool FineMese,
    string? CondizionePagamento,
    string? CondizioneDescrizione,
    string? ModalitaPagamento,
    string? ModalitaDescrizione,
    bool IsAttivo);
