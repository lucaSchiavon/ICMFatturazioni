using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager SpesaAnticipata: validazione campi, CRUD, audit.
/// </summary>
public class SpesaAnticipataManagerTests
{
    private static readonly Guid IdAttivita = Guid.NewGuid();

    private static SpesaAnticipata Spesa(
        DateOnly? data        = null,
        string    descrizione = "BOLLO",
        decimal   importo     = 50m,
        Guid?     idAttivita  = null) => new()
    {
        IdAttivita  = idAttivita ?? IdAttivita,
        Data        = data ?? new DateOnly(2026, 6, 19),
        Descrizione = descrizione,
        Importo     = importo,
    };

    private static (SpesaAnticipataManager sut, FakeSpesaAnticipataRepository repo, FakeAuditManager audit)
        NewSut()
    {
        var repo  = new FakeSpesaAnticipataRepository();
        var audit = new FakeAuditManager();
        var sut   = new SpesaAnticipataManager(repo, audit);
        return (sut, repo, audit);
    }

    // -------------------------------------------------------------------------
    // Creazione
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersistente()
    {
        var (sut, repo, _) = NewSut();

        var id = await sut.CreaAsync(Spesa(importo: 200m, descrizione: "ACCESSO AGLI ATTI"));

        Assert.NotEqual(Guid.Empty, id);
        var s = await repo.GetByIdAsync(id);
        Assert.NotNull(s);
        Assert.Equal(200m, s.Importo);
        Assert.Equal("ACCESSO AGLI ATTI", s.Descrizione);
        Assert.True(s.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditCreazione()
    {
        var (sut, _, audit) = NewSut();

        var id = await sut.CreaAsync(Spesa());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("SpesaAnticipata", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    // -------------------------------------------------------------------------
    // Validazione campi
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DataDefault_LanciaDataObbligatoria()
    {
        var (sut, _, _) = NewSut();
        var spesa = new SpesaAnticipata { IdAttivita = IdAttivita, Descrizione = "BOLLO", Importo = 50m };

        var ex = await Assert.ThrowsAsync<SpesaAnticipataInvalidaException>(() => sut.CreaAsync(spesa));
        Assert.Equal(SpesaAnticipataMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaDescrizioneObbligatoria(string descrizione)
    {
        var (sut, _, _) = NewSut();

        var ex = await Assert.ThrowsAsync<SpesaAnticipataInvalidaException>(
            () => sut.CreaAsync(Spesa(descrizione: descrizione)));
        Assert.Equal(SpesaAnticipataMotivoInvalido.DescrizioneObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreaAsync_ImportoNonPositivo_LanciaImportoNonValido(decimal importo)
    {
        var (sut, _, _) = NewSut();

        var ex = await Assert.ThrowsAsync<SpesaAnticipataInvalidaException>(
            () => sut.CreaAsync(Spesa(importo: importo)));
        Assert.Equal(SpesaAnticipataMotivoInvalido.ImportoNonValido, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Aggiornamento / Eliminazione
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AggiornaAsync_ModificaImporto_PersisteEAudita()
    {
        var (sut, repo, audit) = NewSut();
        var id = await sut.CreaAsync(Spesa(importo: 50m));
        audit.Voci.Clear();

        var aggiornata = Spesa(importo: 75m);
        aggiornata.IdSpesaAnticipata = id;
        await sut.AggiornaAsync(aggiornata);

        Assert.Equal(75m, (await repo.GetByIdAsync(id))!.Importo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
    }

    [Fact]
    public async Task EliminaAsync_DisattivaEAudita()
    {
        var (sut, repo, audit) = NewSut();
        var id = await sut.CreaAsync(Spesa());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await repo.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task EliminaAsync_Inesistente_NonLancia()
    {
        var (sut, _, audit) = NewSut();

        await sut.EliminaAsync(Guid.NewGuid());

        Assert.Empty(audit.Voci);
    }

    // -------------------------------------------------------------------------
    // Lock: spesa già associata a un avviso di fattura
    // -------------------------------------------------------------------------

    // Semina direttamente nel repo una spesa già associata (IdAvviso valorizzato).
    private static SpesaAnticipata SpesaInAvviso() => new()
    {
        IdSpesaAnticipata = Guid.NewGuid(),
        IdAttivita        = IdAttivita,
        Data              = new DateOnly(2026, 6, 19),
        Descrizione       = "BOLLO",
        Importo           = 50m,
        IdAvviso          = Guid.NewGuid(),
    };

    [Fact]
    public async Task AggiornaAsync_SpesaInAvviso_LanciaSpesaInAvviso()
    {
        var (sut, repo, _) = NewSut();
        var inAvviso = SpesaInAvviso();
        await repo.InsertAsync(inAvviso);

        var modifica = Spesa(importo: 99m);
        modifica.IdSpesaAnticipata = inAvviso.IdSpesaAnticipata;

        var ex = await Assert.ThrowsAsync<SpesaAnticipataInvalidaException>(
            () => sut.AggiornaAsync(modifica));
        Assert.Equal(SpesaAnticipataMotivoInvalido.SpesaInAvviso, ex.Motivo);
        Assert.Equal(50m, (await repo.GetByIdAsync(inAvviso.IdSpesaAnticipata))!.Importo);
    }

    [Fact]
    public async Task EliminaAsync_SpesaInAvviso_LanciaSpesaInAvviso()
    {
        var (sut, repo, _) = NewSut();
        var inAvviso = SpesaInAvviso();
        await repo.InsertAsync(inAvviso);

        var ex = await Assert.ThrowsAsync<SpesaAnticipataInvalidaException>(
            () => sut.EliminaAsync(inAvviso.IdSpesaAnticipata));
        Assert.Equal(SpesaAnticipataMotivoInvalido.SpesaInAvviso, ex.Motivo);
        Assert.True((await repo.GetByIdAsync(inAvviso.IdSpesaAnticipata))!.IsAttivo);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoPerAttivitaAsync_OrdinaPerDataAscEEscludeDisattivate()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync(Spesa(data: new DateOnly(2026, 7, 23), descrizione: "ACCESSO"));
        await sut.CreaAsync(Spesa(data: new DateOnly(2026, 6, 19), descrizione: "BOLLO"));
        var idDaEliminare = await sut.CreaAsync(Spesa(data: new DateOnly(2026, 5, 1), descrizione: "ALTRO"));
        await sut.EliminaAsync(idDaEliminare);

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);

        Assert.Equal(2, lista.Count);
        Assert.Equal(new DateOnly(2026, 6, 19), lista[0].Data);
        Assert.Equal(new DateOnly(2026, 7, 23), lista[1].Data);
    }

    [Fact]
    public async Task ElencoPerAttivitaAsync_FiltraPerAttivita()
    {
        var (sut, _, _) = NewSut();
        var altraAttivita = Guid.NewGuid();
        await sut.CreaAsync(Spesa());
        await sut.CreaAsync(Spesa(idAttivita: altraAttivita));

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);

        Assert.Single(lista);
    }
}
