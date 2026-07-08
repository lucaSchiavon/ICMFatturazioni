namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Coppia (numero, data) di una fattura attiva dell'anno, usata per la verifica di
/// coerenza della numerazione progressiva rispetto all'ordine cronologico
/// (<see cref="Services.IFatturaCoerenzaValidator"/>). Read-model minimale: non
/// serve l'intera entità per il controllo.
/// </summary>
public sealed record FatturaNumeroData(int Numero, DateOnly Data);
