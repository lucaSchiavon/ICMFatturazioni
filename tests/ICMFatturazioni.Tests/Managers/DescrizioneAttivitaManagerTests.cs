using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Descrizioni attività: normalizzazione (trim, ordine clamp),
/// validazione, unicità, audit. Nessuna dipendenza → niente EEliminabile.
/// </summary>
public class DescrizioneAttivitaManagerTests
{
    private static DescrizioneAttivita Desc(string testo = "Lavori di progettazione", int ordine = 0) => new()
    {
        Descrizione = testo,
        Ordine      = ordine,
    };

    private static DescrizioneAttivitaManager NewSut(FakeDescrizioneAttivitaRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Desc());

        Assert.NotEqual(Guid.Empty, id);
        var d = await fake.GetByIdAsync(id);
        Assert.Equal("Lavori di progettazione", d!.Descrizione);
        Assert.True(d.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Desc());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("DescrizioneAttivita", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string testo)
    {
        var sut = NewSut(new FakeDescrizioneAttivitaRepository());
        var ex = await Assert.ThrowsAsync<DescrizioneAttivitaInvalidaException>(() => sut.CreaAsync(Desc(testo: testo)));
        Assert.Equal(DescrizioneAttivitaInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Desc(testo: "Lavori di progettazione"));

        var ex = await Assert.ThrowsAsync<DescrizioneAttivitaInvalidaException>(
            () => sut.CreaAsync(Desc(testo: "lavori di progettazione")));  // case-insensitive
        Assert.Equal(DescrizioneAttivitaInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrim()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Desc(testo: "  Lavori di progettazione  "));
        var d = await fake.GetByIdAsync(id);
        Assert.Equal("Lavori di progettazione", d!.Descrizione);
    }

    [Fact]
    public async Task CreaAsync_OrdineNegativo_ClampedAZero()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Desc(ordine: -5));
        var d = await fake.GetByIdAsync(id);
        Assert.Equal(0, d!.Ordine);
    }

    [Fact]
    public async Task AggiornaAsync_StessaDescrizione_NonConsideraDuplicata()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Desc(testo: "Lavori di progettazione", ordine: 0));

        await sut.AggiornaAsync(new DescrizioneAttivita
        {
            IdDescrizioneAttivita = id,
            Descrizione           = "Lavori di progettazione",
            Ordine                = 5,
        });
        var d = await fake.GetByIdAsync(id);
        Assert.Equal(5, d!.Ordine);
    }

    [Fact]
    public async Task EliminaAsync_DisattivaEAudita()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Desc());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task ElencoAsync_SoloAttiviOrdinatiPerOrdinePoiDescrizione()
    {
        var fake = new FakeDescrizioneAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Desc(testo: "Perizia tecnica", ordine: 2));
        await sut.CreaAsync(Desc(testo: "Relazione finale", ordine: 1));
        await sut.CreaAsync(Desc(testo: "Analisi preliminare", ordine: 1));

        var elenco = await sut.ElencoAsync();
        Assert.Equal(3, elenco.Count);
        // ordine 1 prima — poi alfabetico
        Assert.Equal("Analisi preliminare", elenco[0].Descrizione);
        Assert.Equal("Relazione finale", elenco[1].Descrizione);
        Assert.Equal("Perizia tecnica", elenco[2].Descrizione);
    }
}
