using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Attività: validazione campi obbligatori, coerenza date,
/// assegnazione numero progressivo, audit, doppia difesa su delete.
/// </summary>
public class AttivitaManagerTests
{
    private static readonly Guid IdAnagrafica   = Guid.NewGuid();
    private static readonly Guid IdTipoAttivita = Guid.NewGuid();

    private static Attivita Att(
        string descr           = "Progettazione residenziale",
        string numero          = "100",
        Guid?  idAnagrafica    = null,
        Guid?  idTipoAttivita  = null,
        DateOnly? projDef      = null,
        DateOnly? concessione  = null,
        DateOnly? inizio       = null) => new()
    {
        Numero              = numero,
        Descrizione         = descr,
        IdAnagrafica        = idAnagrafica  ?? IdAnagrafica,
        IdTipoAttivita      = idTipoAttivita ?? IdTipoAttivita,
        ProgettoDefinitivo  = projDef,
        ConcessioneEdilizia = concessione,
        InizioLavori        = inizio,
    };

    private static AttivitaManager NewSut(FakeAttivitaRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    // -------------------------------------------------------------------------
    // Creazione e persistenza
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersistNumero()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);

        var id = await sut.CreaAsync(Att(numero: "42"));

        Assert.NotEqual(Guid.Empty, id);
        var a = await fake.GetByIdAsync(id);
        Assert.NotNull(a);
        Assert.Equal("Progettazione residenziale", a.Descrizione);
        Assert.Equal("42", a.Numero);
    }

    [Fact]
    public async Task CreaAsync_NumeroDiversi_PersistitiCorrettamente()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Att(descr: "Prima",   numero: "10"));
        var id2  = await sut.CreaAsync(Att(descr: "Seconda", numero: "20"));
        Assert.Equal("10", (await fake.GetByIdAsync(id1))!.Numero);
        Assert.Equal("20", (await fake.GetByIdAsync(id2))!.Numero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_NumeroVuoto_LanciaNumeroNonValido(string numero)
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(() => sut.CreaAsync(Att(numero: numero)));
        Assert.Equal(AttivitaInvalidoMotivo.NumeroNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake  = new FakeAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);

        var id = await sut.CreaAsync(Att());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Attivita", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrimDescrizione()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Att(descr: "  Perizia  "));
        Assert.Equal("Perizia", (await fake.GetByIdAsync(id))!.Descrizione);
    }

    // -------------------------------------------------------------------------
    // Validazione campi obbligatori
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(() => sut.CreaAsync(Att(descr: descr)));
        Assert.Equal(AttivitaInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AnagraficaVuota_LanciaObbligatoria()
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(
            () => sut.CreaAsync(Att(idAnagrafica: Guid.Empty)));
        Assert.Equal(AttivitaInvalidoMotivo.AnagraficaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TipoAttivitaVuoto_LanciaObbligatorio()
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(
            () => sut.CreaAsync(Att(idTipoAttivita: Guid.Empty)));
        Assert.Equal(AttivitaInvalidoMotivo.TipoAttivitaObbligatorio, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Validazione coerenza date
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DateCoerenti_OK()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        // Nessuna eccezione attesa.
        var id = await sut.CreaAsync(Att(
            projDef:     new DateOnly(2024, 1, 1),
            concessione: new DateOnly(2024, 6, 1),
            inizio:      new DateOnly(2024, 9, 1)));
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task CreaAsync_ProgettoDefinitivoDopoConcessione_LanciaDateIncoerenti()
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(() => sut.CreaAsync(Att(
            projDef:     new DateOnly(2024, 6, 1),
            concessione: new DateOnly(2024, 1, 1))));
        Assert.Equal(AttivitaInvalidoMotivo.DateIncoerenti, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_ConcessioneDopoInizio_LanciaDateIncoerenti()
    {
        var sut = NewSut(new FakeAttivitaRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaInvalidaException>(() => sut.CreaAsync(Att(
            concessione: new DateOnly(2024, 9, 1),
            inizio:      new DateOnly(2024, 6, 1))));
        Assert.Equal(AttivitaInvalidoMotivo.DateIncoerenti, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_SoloUnaData_OK()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        // Date parziali (solo ProgettoDefinitivo) non devono essere bloccate.
        var id = await sut.CreaAsync(Att(projDef: new DateOnly(2024, 1, 1)));
        Assert.NotEqual(Guid.Empty, id);
    }

    // -------------------------------------------------------------------------
    // Eliminazione
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EliminaAsync_ConDipendenze_LanciaEonDisattiva()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Att());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<AttivitaConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake  = new FakeAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);
        var id    = await sut.CreaAsync(Att());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoAsync_OrdinaPerNumeroDec()
    {
        var fake = new FakeAttivitaRepository();
        var sut  = NewSut(fake);
        await sut.CreaAsync(Att(descr: "Prima",   numero: "10"));
        await sut.CreaAsync(Att(descr: "Seconda", numero: "20"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("Seconda", elenco[0].Descrizione);
        Assert.Equal("Prima",   elenco[1].Descrizione);
    }
}
