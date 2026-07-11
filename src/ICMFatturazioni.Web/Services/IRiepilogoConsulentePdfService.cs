using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Generazione del report PDF "Riepilogo attività consulente" (dispensa cap. 7):
/// gerarchia Consulente → Cliente → Attività → righe con tranche di pagamento,
/// totali per riga/attività/cliente e totale generale.
/// Contiene SOLO le consulenze a carico dello Studio (decisione D-C1).
/// </summary>
public interface IRiepilogoConsulentePdfService
{
    /// <summary>Genera i byte del PDF secondo il filtro (IdConsulente null = variante generale).</summary>
    Task<byte[]> GeneraAsync(FiltroRiepilogoConsulente filtro, CancellationToken ct = default);
}
