using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati in <b>sola lettura</b> ai verbali firmati, esposti dal DB
/// unificato tramite la vista <c>fatt.VerbaliConsultazione</c> (dominio di
/// ICMVerbali). Nessuna scrittura: i verbali si creano/firmano solo in ICMVerbali.
/// </summary>
public interface IVerbaleConsultazioneRepository
{
    /// <summary>
    /// Tutti i verbali firmati (non bozze, non cancellati) che hanno un
    /// <c>ReportPath</c> valorizzato, ordinati per data discendente. Il filtro
    /// sull'<b>esistenza fisica</b> del PDF è responsabilità del Manager.
    /// </summary>
    Task<IReadOnlyList<VerbaleConsultazione>> GetEsportabiliAsync(CancellationToken cancellationToken = default);
}
