using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>MenuConfigManager</c> (configurazione mapping di visibilità).
/// Obiettivi:
///   1) salvando un sottomenu il suo menu-gruppo viene incluso automaticamente
///      (altrimenti il NavMenu non mostrerebbe il gruppo né la sottovoce);
///   2) l'override per utente si salva e si rileva (HasPersonalizzazione);
///   3) la rimozione dell'override azzera entrambi gli insiemi.
/// </summary>
public class MenuConfigManagerTests
{
    private static readonly Guid Gruppo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Sotto1 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Sotto2 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static (MenuConfigManager sut, FakeMenuRepository repo) NewSut()
    {
        var repo = new FakeMenuRepository();
        repo.Menus.Add(new Menu { IdMenu = Gruppo, DescrizioneMenu = "Tabelle", Ordine = 10 });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = Sotto1, IdMenu = Gruppo, Descrizione = "Anagrafiche", PaginaRazor = "Anagrafiche", Ordine = 10 });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = Sotto2, IdMenu = Gruppo, Descrizione = "Banche", PaginaRazor = "Banche", Ordine = 20 });
        return (new MenuConfigManager(repo, new FakeAuditManager()), repo);
    }

    [Fact]
    public async Task SalvaMappingRuoloAsync_RegistraAuditDiModifica()
    {
        // SUT con un audit ispezionabile (NewSut ne crea uno interno non esposto).
        var audit = new FakeAuditManager();
        var repo = new FakeMenuRepository();
        repo.Menus.Add(new Menu { IdMenu = Gruppo, DescrizioneMenu = "Tabelle", Ordine = 10 });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = Sotto1, IdMenu = Gruppo, Descrizione = "Anagrafiche", PaginaRazor = "Anagrafiche", Ordine = 10 });
        var sut = new MenuConfigManager(repo, audit);
        var idRuolo = Guid.NewGuid();

        await sut.SalvaMappingRuoloAsync(idRuolo, Array.Empty<Guid>(), new[] { Sotto1 });

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Equal("PermessiRuolo", voce.EntityType);
        Assert.Equal(idRuolo, voce.EntityId);
    }

    [Fact]
    public async Task SalvaMappingRuoloAsync_SottomenuSelezionato_IncludeAutomaticamenteIlGruppoPadre()
    {
        var (sut, repo) = NewSut();
        var idRuolo = Guid.NewGuid();

        // Si spunta solo la sottovoce, NON il gruppo.
        await sut.SalvaMappingRuoloAsync(idRuolo, menuIds: Array.Empty<Guid>(), sottoMenuIds: new[] { Sotto1 });

        Assert.Contains(Gruppo, repo.MenuRuolo[idRuolo]);     // gruppo aggiunto d'ufficio
        Assert.Contains(Sotto1, repo.SottoMenuRuolo[idRuolo]);
    }

    [Fact]
    public async Task SalvaMappingUtenteAsync_SalvaOverrideEIncludeIlGruppo()
    {
        var (sut, repo) = NewSut();
        var idUtente = Guid.NewGuid();

        await sut.SalvaMappingUtenteAsync(idUtente, Array.Empty<Guid>(), new[] { Sotto2 });

        Assert.Contains(Gruppo, repo.MenuUtente[idUtente]);
        Assert.Contains(Sotto2, repo.SottoMenuUtente[idUtente]);
    }

    [Fact]
    public async Task GetMappingRuoloAsync_RestituisceGliInsiemiSalvati()
    {
        var (sut, repo) = NewSut();
        var idRuolo = Guid.NewGuid();
        await sut.SalvaMappingRuoloAsync(idRuolo, new[] { Gruppo }, new[] { Sotto1 });

        var mapping = await sut.GetMappingRuoloAsync(idRuolo);

        Assert.Contains(Gruppo, mapping.MenuIds);
        Assert.Contains(Sotto1, mapping.SottoMenuIds);
    }

    [Fact]
    public async Task HasPersonalizzazioneUtenteAsync_FalseSenzaOverride_TrueDopoIlSalvataggio()
    {
        var (sut, _) = NewSut();
        var idUtente = Guid.NewGuid();

        Assert.False(await sut.HasPersonalizzazioneUtenteAsync(idUtente));

        await sut.SalvaMappingUtenteAsync(idUtente, new[] { Gruppo }, new[] { Sotto1 });
        Assert.True(await sut.HasPersonalizzazioneUtenteAsync(idUtente));
    }

    [Fact]
    public async Task RimuoviPersonalizzazioneUtenteAsync_AzzeraLOverride()
    {
        var (sut, _) = NewSut();
        var idUtente = Guid.NewGuid();
        await sut.SalvaMappingUtenteAsync(idUtente, new[] { Gruppo }, new[] { Sotto1 });

        await sut.RimuoviPersonalizzazioneUtenteAsync(idUtente);

        Assert.False(await sut.HasPersonalizzazioneUtenteAsync(idUtente));
        var mapping = await sut.GetMappingUtenteAsync(idUtente);
        Assert.Empty(mapping.MenuIds);
        Assert.Empty(mapping.SottoMenuIds);
    }
}
