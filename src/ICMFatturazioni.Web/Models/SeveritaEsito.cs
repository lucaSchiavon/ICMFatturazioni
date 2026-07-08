namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Gravità dell'esito di un verbale, mirror di <c>SeveritaEsito</c> di ICMVerbali
/// (colonna <c>dbo.CatalogoEsito.Severita</c>, tinyint). È la sorgente della
/// "conformità" mostrata a semaforo nella maschera di consultazione: non esiste
/// un flag booleano conforme/non conforme sul verbale, la conformità è la
/// severità dell'esito complessivo.
/// </summary>
/// <remarks>
/// Il valore di persistenza è esplicito (byte) e allineato 1:1 al catalogo di
/// ICMVerbali: 0=Conforme (verde) … 3=Sospensione (rosso). Non derivare mai
/// dall'ordinale: qui coincide per costruzione, ma resta un mirror di un dominio
/// altrui che potrebbe cambiare.
/// </remarks>
public enum SeveritaEsito : byte
{
    /// <summary>Verde — nessuna non conformità rilevata.</summary>
    Conforme = 0,

    /// <summary>Giallo — non conformità minori.</summary>
    NonConformitaMinori = 1,

    /// <summary>Arancio — non conformità gravi.</summary>
    NonConformitaGravi = 2,

    /// <summary>Rosso — sospensione dell'attività di cantiere.</summary>
    Sospensione = 3,
}
