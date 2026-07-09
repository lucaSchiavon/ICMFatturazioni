using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test di <see cref="AziendaManager"/>: salvataggio singleton (get-or-create
/// pigro), validazioni tipizzate e audit (creazione / diff).
/// </summary>
public class AziendaManagerTests
{
    private static Azienda Valida(string rs = "ICM Solutions S.r.l.", string nome = "ICM") => new()
    {
        NomeBreve = nome,
        RagioneSociale = rs,
        PIVA = "04671260232",   // P.IVA reale ICM Solutions (valida)
    };

    private static (AziendaManager Sut, FakeAziendaRepository Repo, FakeAuditManager Audit) NewSut()
    {
        var repo = new FakeAziendaRepository();
        var audit = new FakeAuditManager();
        return (new AziendaManager(repo, audit), repo, audit);
    }

    // ---- Creazione pigra ----------------------------------------------------

    [Fact]
    public async Task SalvaCedente_PrimoSalvataggio_InserisceEAuditDiCreazione()
    {
        var (sut, repo, audit) = NewSut();

        var id = await sut.SalvaCedenteAsync(Valida());

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, repo.InsertCount);
        Assert.Equal(0, repo.UpdateCount);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal(nameof(Azienda), voce.EntityType);
    }

    [Fact]
    public async Task SalvaCedente_NessunaRigaVuotaPrimaDelSalvataggio()
    {
        var (_, repo, _) = NewSut();
        // Senza salvare non deve esistere alcuna riga (creazione pigra).
        Assert.Null(await repo.GetAziendaAsync());
    }

    // ---- Aggiornamento della riga esistente ---------------------------------

    [Fact]
    public async Task SalvaCedente_SecondoSalvataggio_AggiornaStessaRigaEAuditDiff()
    {
        var (sut, repo, audit) = NewSut();

        var id1 = await sut.SalvaCedenteAsync(Valida(rs: "Ragione A"));
        var id2 = await sut.SalvaCedenteAsync(Valida(rs: "Ragione B"));

        // Stesso cedente: niente seconda INSERT, l'Id non cambia.
        Assert.Equal(id1, id2);
        Assert.Equal(1, repo.InsertCount);
        Assert.Equal(1, repo.UpdateCount);
        Assert.Equal(2, audit.Voci.Count);
        Assert.Equal(AuditOperazione.Modifica, audit.Voci[1].Operazione);

        var corrente = await repo.GetAziendaAsync();
        Assert.Equal("Ragione B", corrente!.RagioneSociale);
    }

    // ---- Normalizzazione ----------------------------------------------------

    [Fact]
    public async Task SalvaCedente_TrimmaCampiEAzzeraOpzionaliVuoti()
    {
        var (sut, repo, _) = NewSut();

        await sut.SalvaCedenteAsync(new Azienda
        {
            NomeBreve = "  ICM  ",
            RagioneSociale = "  ICM Solutions  ",
            Email = "   ",   // solo spazi → null
        });

        var c = await repo.GetAziendaAsync();
        Assert.Equal("ICM", c!.NomeBreve);
        Assert.Equal("ICM Solutions", c.RagioneSociale);
        Assert.Null(c.Email);
    }

    [Fact]
    public async Task SalvaCedente_CassaOff_AzzeraCodiceCassa()
    {
        var (sut, repo, _) = NewSut();

        await sut.SalvaCedenteAsync(new Azienda
        {
            NomeBreve = "ICM",
            RagioneSociale = "ICM Solutions",
            ApplicaCassaPrevidenziale = false,
            TipoCassaFe = "TC04",   // incoerente col flag off → deve essere azzerato
        });

        var c = await repo.GetAziendaAsync();
        Assert.Null(c!.TipoCassaFe);
    }

    // ---- Validazioni tipizzate ----------------------------------------------

    [Fact]
    public async Task SalvaCedente_RagioneSocialeVuota_Lancia()
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AziendaInvalidaException>(
            () => sut.SalvaCedenteAsync(new Azienda { NomeBreve = "ICM", RagioneSociale = "" }));
        Assert.Equal(AziendaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task SalvaCedente_NomeBreveVuoto_Lancia()
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AziendaInvalidaException>(
            () => sut.SalvaCedenteAsync(new Azienda { NomeBreve = "  ", RagioneSociale = "ICM" }));
        Assert.Equal(AziendaInvalidaMotivo.NomeBreveObbligatorio, ex.Motivo);
    }

    [Fact]
    public async Task SalvaCedente_PivaMalformata_Lancia()
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AziendaInvalidaException>(
            () => sut.SalvaCedenteAsync(new Azienda { NomeBreve = "ICM", RagioneSociale = "ICM", PIVA = "12345670786" }));
        Assert.Equal(AziendaInvalidaMotivo.PivaNonValida, ex.Motivo);
    }

    [Fact]
    public async Task SalvaCedente_CapMalformato_Lancia()
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AziendaInvalidaException>(
            () => sut.SalvaCedenteAsync(new Azienda { NomeBreve = "ICM", RagioneSociale = "ICM", IndirizzoCAP = "371" }));
        Assert.Equal(AziendaInvalidaMotivo.CapNonValido, ex.Motivo);
    }

    [Fact]
    public async Task SalvaCedente_EmailMalformata_Lancia()
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AziendaInvalidaException>(
            () => sut.SalvaCedenteAsync(new Azienda { NomeBreve = "ICM", RagioneSociale = "ICM", Email = "non-una-email" }));
        Assert.Equal(AziendaInvalidaMotivo.EmailNonValida, ex.Motivo);
    }
}
