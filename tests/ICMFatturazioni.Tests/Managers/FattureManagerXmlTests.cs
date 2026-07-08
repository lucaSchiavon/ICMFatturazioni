using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test delle transizioni di stato del tracciato XML sul manager Fatture (Fase D1):
/// marcatura "XML creato", conferma/rimozione esito, con le relative guardie e il
/// tracciamento in audit (Regola 7).
/// </summary>
public class FattureManagerXmlTests
{
    private sealed class Sut
    {
        public required FattureManager             Manager { get; init; }
        public required FakeFattureRepository       Fatture { get; init; }
        public required FakeAvvisoFatturaRepository Avvisi  { get; init; }
        public required FakeAuditManager            Audit   { get; init; }
    }

    private static Sut NewSut()
    {
        var fatture = new FakeFattureRepository();
        var avvisi  = new FakeAvvisoFatturaRepository();
        var audit   = new FakeAuditManager();
        return new Sut
        {
            Manager = new FattureManager(fatture, avvisi, audit),
            Fatture = fatture,
            Avvisi  = avvisi,
            Audit   = audit,
        };
    }

    // Semina un avviso attivo + crea la fattura corrispondente; restituisce l'Id fattura.
    private static async Task<Guid> SeedFatturaAsync(Sut sut, int numero = 37)
    {
        var idAvviso = Guid.NewGuid();
        sut.Avvisi.Seed(new AvvisoFattura
        {
            IdAvviso     = idAvviso,
            IdAttivita   = Guid.NewGuid(),
            IdAnagrafica = Guid.NewGuid(),
            DataAvviso   = new DateOnly(2026, 7, 1),
            IsAttivo     = true,
        });
        return await sut.Manager.CreaAsync(new CreaFatturaRequest(idAvviso, numero, new DateOnly(2026, 7, 30)));
    }

    // =====================================================================
    // Progressivo invio
    // =====================================================================

    [Fact]
    public async Task ProssimoProgressivoInvioSeq_Incrementa()
    {
        var sut = NewSut();
        var p1 = await sut.Manager.ProssimoProgressivoInvioSeqAsync();
        var p2 = await sut.Manager.ProssimoProgressivoInvioSeqAsync();
        Assert.True(p2 > p1);
    }

    // =====================================================================
    // Coerenza lifecycle fattura ↔ XML (eliminazione)
    // =====================================================================

    [Fact]
    public async Task Annulla_BloccataSeFatturaHaXml()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "IT01961500236_00001.xml");

        // Con un XML presente la fattura NON è eliminabile: eccezione tipizzata.
        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.AnnullaAsync(id));
        Assert.Equal(FatturaMotivoInvalido.FatturaConXmlNonEliminabile, ex.Motivo);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.True(f!.IsAttivo); // resta attiva
    }

    [Fact]
    public async Task Annulla_ConsentitaSenzaXml()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);

        await sut.Manager.AnnullaAsync(id); // nessun XML → soft-delete regolare

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.False(f!.IsAttivo);
    }

    [Fact]
    public async Task ResetXml_AzzeraStatoEMetadati_EAudita()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "IT01961500236_00001.xml");
        sut.Audit.Voci.Clear();

        await sut.Manager.ResetXmlAsync(id);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.False(f!.CreatoXML);
        Assert.Null(f.ProgressivoInvio);
        Assert.Null(f.NomeFileXml);
        Assert.Null(f.DataCreazioneXmlUtc);

        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Contains("EliminaXML", voce.Dati);
    }

    [Fact]
    public async Task ResetXml_BloccatoSeEsitoOk()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "IT01961500236_00001.xml");
        await sut.Manager.ConfermaEsitoXmlAsync(id); // EsitoXML = 1

        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.ResetXmlAsync(id));
        Assert.Equal(FatturaMotivoInvalido.XmlConEsitoConfermato, ex.Motivo);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.True(f!.CreatoXML); // l'XML resta
    }

    [Fact]
    public async Task ResetXml_IdempotenteSenzaXml()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        sut.Audit.Voci.Clear();

        await sut.Manager.ResetXmlAsync(id); // niente XML → no-op

        Assert.Empty(sut.Audit.Voci);
    }

    [Fact]
    public async Task ResetXml_RiabilitaLEliminazioneDellaFattura()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "IT01961500236_00001.xml");

        // Prima l'XML, poi la fattura: l'ordine simmetrico ora funziona end-to-end.
        await sut.Manager.ResetXmlAsync(id);
        await sut.Manager.AnnullaAsync(id);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.False(f!.IsAttivo);
    }

    // =====================================================================
    // Creazione XML
    // =====================================================================

    [Fact]
    public async Task SegnaXmlCreato_ImpostaFlagEMetadati_EAuditaCreaXml()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        sut.Audit.Voci.Clear(); // scarta l'audit della creazione fattura

        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "IT01961500236_00001.xml");

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.NotNull(f);
        Assert.True(f!.CreatoXML);
        Assert.Equal("00001", f.ProgressivoInvio);
        Assert.Equal("IT01961500236_00001.xml", f.NomeFileXml);
        Assert.NotNull(f.DataCreazioneXmlUtc);

        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Equal("Fattura", voce.EntityType);
        Assert.Contains("CreaXML", voce.Dati);
    }

    [Fact]
    public async Task SegnaXmlCreato_SecondaVolta_AuditaRigeneraXml()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "f.xml");
        sut.Audit.Voci.Clear();

        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "f.xml"); // rigenerazione

        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Contains("RigeneraXML", voce.Dati);
    }

    [Fact]
    public async Task SegnaXmlCreato_FatturaInesistente_Lancia()
    {
        var sut = NewSut();
        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.SegnaXmlCreatoAsync(Guid.NewGuid(), "00001", "f.xml"));
        Assert.Equal(FatturaMotivoInvalido.FatturaNonTrovata, ex.Motivo);
    }

    // =====================================================================
    // Esito
    // =====================================================================

    [Fact]
    public async Task ConfermaEsito_PrimaDiCreareXml_Lancia()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);

        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.ConfermaEsitoXmlAsync(id));
        Assert.Equal(FatturaMotivoInvalido.XmlNonCreato, ex.Motivo);
    }

    [Fact]
    public async Task ConfermaEsito_DopoCreazione_ImpostaEsitoEAudita()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "f.xml");
        sut.Audit.Voci.Clear();

        await sut.Manager.ConfermaEsitoXmlAsync(id);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.Equal(1, f!.EsitoXML);
        Assert.NotNull(f.DataEsitoXmlUtc);
        Assert.Contains("ConfermaEsitoXML", Assert.Single(sut.Audit.Voci).Dati);
    }

    [Fact]
    public async Task TogliEsito_RiportaInAttesa_EAudita()
    {
        var sut = NewSut();
        var id  = await SeedFatturaAsync(sut);
        await sut.Manager.SegnaXmlCreatoAsync(id, "00001", "f.xml");
        await sut.Manager.ConfermaEsitoXmlAsync(id);
        sut.Audit.Voci.Clear();

        await sut.Manager.TogliEsitoXmlAsync(id);

        var f = await sut.Manager.GetByIdAsync(id);
        Assert.Equal(0, f!.EsitoXML);
        Assert.Null(f.DataEsitoXmlUtc);
        Assert.Contains("TogliEsitoXML", Assert.Single(sut.Audit.Voci).Dati);
    }

    // =====================================================================
    // Griglia
    // =====================================================================

    [Fact]
    public async Task ElencoPerXml_FiltraPerStatoCreazione()
    {
        var sut = NewSut();
        var idCreato    = await SeedFatturaAsync(sut, numero: 1);
        var idDaCreare  = await SeedFatturaAsync(sut, numero: 2);
        await sut.Manager.SegnaXmlCreatoAsync(idCreato, "00001", "f1.xml");

        var filtro = new FiltroDocumentiXml(
            IdAnagrafica: null,
            DataDa: new DateOnly(2026, 1, 1),
            DataA:  new DateOnly(2026, 12, 31),
            Creazione: StatoCreazioneXml.DaCreare,
            Esito: StatoEsitoXml.Tutti);

        var righe = await sut.Manager.ElencoPerXmlAsync(filtro);
        Assert.Single(righe);
        Assert.Equal(idDaCreare, righe[0].IdFattura);
        Assert.False(righe[0].CreatoXML);
    }
}
