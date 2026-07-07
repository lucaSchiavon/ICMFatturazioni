using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Cantiere. Obiettivi:
///   1) validazione di forma → eccezioni tipizzate col motivo corretto,
///      nell'ordine UX dichiarato;
///   2) pre-check sul puntatore all'attività (esistenza + IsAttivo);
///   3) audit registrato su creazione/modifica/eliminazione;
///   4) eliminazione = soft-delete (la riga resta, IsAttivo = false).
/// Niente DB reale: FakeCantiereRepository + FakeAttivitaManager.
/// </summary>
public class CantiereManagerTests
{
    private static readonly Guid IdAttivitaValida = Guid.CreateVersion7();

    // -----------------------------------------------------------------
    // Helper: fake attività con una attività valida di default
    // -----------------------------------------------------------------
    private static FakeAttivitaManager AttivitaConDefault()
    {
        var fake = new FakeAttivitaManager();
        fake.Attivita[IdAttivitaValida] = new Attivita
        {
            IdAttivita = IdAttivitaValida,
            IdAnagrafica = Guid.CreateVersion7(),
            IdTipoAttivita = Guid.CreateVersion7(),
            Numero = "TEST-01",
            Descrizione = "Attività di test",
            IsAttivo = true,
        };
        return fake;
    }

    private static Cantiere CantiereValido(Guid? idAttivita = null) => new()
    {
        IdAttivita = idAttivita ?? IdAttivitaValida,
        Ubicazione = "Via Roma 1, Verona",
        Tipologia = "Ristrutturazione",
        ImportoAppalto = 100_000m,
    };

    // Factory del SUT: audit e attività opzionali, con default che non
    // interferiscono (fake vuoto / attività valida preregistrata).
    private static CantiereManager NewSut(
        FakeCantiereRepository fake,
        FakeAuditManager? audit = null,
        FakeAttivitaManager? attivita = null)
        => new(fake, audit ?? new FakeAuditManager(), attivita ?? AttivitaConDefault());

    private static CantiereManager NewSutSimple(FakeCantiereRepository fake)
        => NewSut(fake);

    // =================================================================
    // Creazione
    // =================================================================

    [Fact]
    public async Task CreaAsync_CantiereValido_AssegnaIdEInserisce()
    {
        var fake = new FakeCantiereRepository();
        var sut = NewSutSimple(fake);

        var id = await sut.CreaAsync(CantiereValido());

        Assert.NotEqual(Guid.Empty, id);
        var salvato = await fake.GetByIdAsync(id);
        Assert.NotNull(salvato);
        Assert.Equal("Via Roma 1, Verona", salvato.Ubicazione);
        Assert.True(salvato.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeCantiereRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(CantiereValido());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Cantiere", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("Via Roma 1, Verona", voce.Descrizione);
    }

    // =================================================================
    // Validazione di forma (ordine dei motivi = ordine UX)
    // =================================================================

    [Fact]
    public async Task CreaAsync_AttivitaVuota_LanciaMotivoAttivitaObbligatoria()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido(idAttivita: Guid.Empty);

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.AttivitaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_UbicazioneVuota_LanciaMotivoUbicazioneObbligatoria()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido();
        input.Ubicazione = "   ";

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.UbicazioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_UbicazioneOltre300_LanciaMotivoUbicazioneTroppoLunga()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido();
        input.Ubicazione = new string('x', 301);

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.UbicazioneTroppoLunga, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TipologiaVuota_LanciaMotivoTipologiaObbligatoria()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido();
        input.Tipologia = "";

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.TipologiaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TipologiaOltre500_LanciaMotivoTipologiaTroppoLunga()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido();
        input.Tipologia = new string('x', 501);

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.TipologiaTroppoLunga, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_ImportoNegativo_LanciaMotivoImportoNegativo()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido();
        input.ImportoAppalto = -1m;

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.ImportoNegativo, ex.Motivo);
    }

    // =================================================================
    // Pre-check attività (doppia difesa, lato manager)
    // =================================================================

    [Fact]
    public async Task CreaAsync_AttivitaInesistente_LanciaMotivoAttivitaInesistente()
    {
        var sut = NewSutSimple(new FakeCantiereRepository());
        var input = CantiereValido(idAttivita: Guid.CreateVersion7());   // non nel fake

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(() => sut.CreaAsync(input));

        Assert.Equal(CantiereInvalidoMotivo.AttivitaInesistente, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AttivitaDisattivata_LanciaMotivoAttivitaInesistente()
    {
        var attivita = AttivitaConDefault();
        var idDisattivata = Guid.CreateVersion7();
        attivita.Attivita[idDisattivata] = new Attivita
        {
            IdAttivita = idDisattivata,
            Numero = "OFF-01",
            Descrizione = "Disattivata",
            IsAttivo = false,
        };
        var sut = new CantiereManager(new FakeCantiereRepository(), new FakeAuditManager(), attivita);

        var ex = await Assert.ThrowsAsync<CantiereInvalidoException>(
            () => sut.CreaAsync(CantiereValido(idAttivita: idDisattivata)));

        Assert.Equal(CantiereInvalidoMotivo.AttivitaInesistente, ex.Motivo);
    }

    // =================================================================
    // Aggiornamento
    // =================================================================

    [Fact]
    public async Task AggiornaAsync_RegistraAuditDiModifica()
    {
        var fake = new FakeCantiereRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(CantiereValido());
        audit.Voci.Clear();   // ignoriamo la voce di creazione

        var modificato = CantiereValido();
        modificato.IdCantiere = id;
        modificato.Ubicazione = "Via Verdi 2, Verona";
        await sut.AggiornaAsync(modificato);

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("Via Verdi 2, Verona", voce.Descrizione);
    }

    // =================================================================
    // Eliminazione (soft-delete)
    // =================================================================

    [Fact]
    public async Task EliminaAsync_DisattivaSenzaRimuovere()
    {
        var fake = new FakeCantiereRepository();
        var sut = NewSutSimple(fake);
        var id = await sut.CreaAsync(CantiereValido());

        await sut.EliminaAsync(id);

        // La riga esiste ancora (i verbali collegati non si rompono)…
        var riga = await fake.GetByIdAsync(id);
        Assert.NotNull(riga);
        Assert.False(riga.IsAttivo);
        // …ma non compare più nell'elenco degli attivi.
        var elenco = await sut.ElencoAsync();
        Assert.DoesNotContain(elenco, c => c.IdCantiere == id);
    }

    [Fact]
    public async Task EliminaAsync_RegistraAuditDiEliminazione()
    {
        var fake = new FakeCantiereRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(CantiereValido());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
    }

    // =================================================================
    // Elenco
    // =================================================================

    [Fact]
    public async Task ElencoAsync_RestituisceSoloAttiviOrdinatiPerUbicazione()
    {
        var fake = new FakeCantiereRepository();
        var sut = NewSutSimple(fake);
        var c1 = CantiereValido(); c1.Ubicazione = "Zona industriale";
        var c2 = CantiereValido(); c2.Ubicazione = "Via Adige";
        var idEliminato = await sut.CreaAsync(CantiereValido());
        await sut.CreaAsync(c1);
        await sut.CreaAsync(c2);
        await sut.EliminaAsync(idEliminato);

        var elenco = await sut.ElencoAsync();

        Assert.Equal(2, elenco.Count);
        Assert.Equal("Via Adige", elenco[0].Ubicazione);
        Assert.Equal("Zona industriale", elenco[1].Ubicazione);
    }
}
