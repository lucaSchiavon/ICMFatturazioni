using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>RuoloManager</c>. Obiettivi:
///   1) i ruoli di SISTEMA (Superadmin/Admin) sono protetti: non modificabili
///      né eliminabili (<see cref="RuoloProtettoException"/>);
///   2) unicità del nome in creazione e modifica (<see cref="RuoloDuplicatoException"/>);
///   3) un ruolo ancora assegnato a utenti non si elimina (<see cref="RuoloInUsoException"/>);
///   4) i ruoli custom nascono con Codice null, IsSistema false, GUID v7.
/// </summary>
public class RuoloManagerTests
{
    private static (RuoloManager sut, FakeRuoloRepository repo) NewSut()
    {
        var repo = new FakeRuoloRepository();
        return (new RuoloManager(repo, new FakeAuditManager()), repo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var repo = new FakeRuoloRepository();
        var audit = new FakeAuditManager();
        var sut = new RuoloManager(repo, audit);

        var id = await sut.CreaAsync("Magazziniere", "desc");

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Ruolo", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("Magazziniere", voce.Descrizione);
    }

    private static Ruolo SeedRuoloSistema(FakeRuoloRepository repo)
        => repo.Seed(new Ruolo
        {
            IdRuolo = Guid.NewGuid(),
            Codice = RuoliSistema.Admin,
            Nome = "Amministratore",
            IsSistema = true,
            IsAttivo = true,
        });

    // =================================================================
    // Creazione
    // =================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_NomeVuoto_LanciaArgumentException(string nome)
    {
        var (sut, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreaAsync(nome, null));
    }

    [Fact]
    public async Task CreaAsync_NomeDuplicato_LanciaRuoloDuplicato()
    {
        var (sut, _) = NewSut();
        await sut.CreaAsync("Contabile", null);

        var ex = await Assert.ThrowsAsync<RuoloDuplicatoException>(
            () => sut.CreaAsync("Contabile", null));
        Assert.Equal("Contabile", ex.Nome);
    }

    [Fact]
    public async Task CreaAsync_Valido_CreaCustomConGuidV7()
    {
        var (sut, repo) = NewSut();
        var id = await sut.CreaAsync("  Contabile  ", "Gestione contabilità");

        var creato = repo.Store[id];
        Assert.Equal("Contabile", creato.Nome);   // trim applicato
        Assert.Null(creato.Codice);
        Assert.False(creato.IsSistema);
        Assert.True(creato.IsAttivo);
        Assert.Equal((byte)0x70, (byte)(creato.IdRuolo.ToByteArray()[7] & 0xF0)); // UUID v7
    }

    // =================================================================
    // Aggiornamento
    // =================================================================

    [Fact]
    public async Task AggiornaAsync_RuoloInesistente_LanciaArgumentException()
    {
        var (sut, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.AggiornaAsync(Guid.NewGuid(), "Nuovo", null, true));
    }

    [Fact]
    public async Task AggiornaAsync_RuoloDiSistema_LanciaRuoloProtetto()
    {
        var (sut, repo) = NewSut();
        var sistema = SeedRuoloSistema(repo);

        var ex = await Assert.ThrowsAsync<RuoloProtettoException>(
            () => sut.AggiornaAsync(sistema.IdRuolo, "Rinominato", null, true));
        Assert.Equal("Amministratore", ex.Nome);
    }

    [Fact]
    public async Task AggiornaAsync_NomeDuplicatoDiUnAltroRuolo_LanciaRuoloDuplicato()
    {
        var (sut, _) = NewSut();
        await sut.CreaAsync("Contabile", null);
        var idAltro = await sut.CreaAsync("Magazziniere", null);

        await Assert.ThrowsAsync<RuoloDuplicatoException>(
            () => sut.AggiornaAsync(idAltro, "Contabile", null, true));
    }

    [Fact]
    public async Task AggiornaAsync_Valido_AggiornaNomeDescrizioneEStato()
    {
        var (sut, repo) = NewSut();
        var id = await sut.CreaAsync("Contabile", null);

        await sut.AggiornaAsync(id, "Contabile senior", "desc", isAttivo: false);

        Assert.Equal("Contabile senior", repo.Store[id].Nome);
        Assert.Equal("desc", repo.Store[id].Descrizione);
        Assert.False(repo.Store[id].IsAttivo);
    }

    [Fact]
    public async Task AggiornaAsync_StessoNomeSulloStessoRuolo_NonEDuplicato()
    {
        var (sut, repo) = NewSut();
        var id = await sut.CreaAsync("Contabile", null);

        await sut.AggiornaAsync(id, "Contabile", "nuova desc", true);

        Assert.Equal("nuova desc", repo.Store[id].Descrizione);
    }

    // =================================================================
    // Eliminazione
    // =================================================================

    [Fact]
    public async Task EliminaAsync_RuoloInesistente_LanciaArgumentException()
    {
        var (sut, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.EliminaAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EliminaAsync_RuoloDiSistema_LanciaRuoloProtetto()
    {
        var (sut, repo) = NewSut();
        var sistema = SeedRuoloSistema(repo);

        await Assert.ThrowsAsync<RuoloProtettoException>(() => sut.EliminaAsync(sistema.IdRuolo));
    }

    [Fact]
    public async Task EliminaAsync_RuoloAssegnatoAUtenti_LanciaRuoloInUso()
    {
        var (sut, repo) = NewSut();
        var id = await sut.CreaAsync("Contabile", null);
        repo.UtentiPerRuolo[id] = 3;

        var ex = await Assert.ThrowsAsync<RuoloInUsoException>(() => sut.EliminaAsync(id));
        Assert.Equal(3, ex.NumeroUtenti);
        Assert.True(repo.Store.ContainsKey(id));   // non rimosso
    }

    [Fact]
    public async Task EliminaAsync_RuoloCustomNonInUso_LoRimuove()
    {
        var (sut, repo) = NewSut();
        var id = await sut.CreaAsync("Contabile", null);

        await sut.EliminaAsync(id);

        Assert.False(repo.Store.ContainsKey(id));
    }

    // =================================================================
    // Pass-through di lettura
    // =================================================================

    [Fact]
    public async Task GetByCodice_GetById_Elenco_DeleganoAlRepository()
    {
        var (sut, repo) = NewSut();
        var sistema = SeedRuoloSistema(repo);
        var idCustom = await sut.CreaAsync("Contabile", null);

        Assert.Equal(sistema.IdRuolo, (await sut.GetByCodiceAsync(RuoliSistema.Admin))!.IdRuolo);
        Assert.NotNull(await sut.GetByIdAsync(idCustom));
        Assert.Equal(2, (await sut.ElencoAsync()).Count);
    }
}
