using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager di consultazione verbali. Obiettivi:
///   1) mostrare solo i verbali il cui PDF esiste FISICAMENTE (regola di
///      dominio: i firmati senza file sono legacy → esclusi, mai rigenerati);
///   2) filtro a cascata per anagrafica / attività / cantiere.
/// Niente DB né filesystem: FakeVerbaleConsultazioneRepository + FakeVerbaleReportStorage.
/// </summary>
public class VerbaleConsultazioneManagerTests
{
    private static readonly Guid Anag1 = Guid.CreateVersion7();
    private static readonly Guid Anag2 = Guid.CreateVersion7();
    private static readonly Guid Att1  = Guid.CreateVersion7();
    private static readonly Guid Att2  = Guid.CreateVersion7();
    private static readonly Guid Cant1 = Guid.CreateVersion7();
    private static readonly Guid Cant2 = Guid.CreateVersion7();

    private static VerbaleConsultazione Verbale(
        Guid id, Guid anag, Guid att, Guid cant, string? reportPath) => new()
    {
        IdVerbale = id,
        Numero = 1,
        Anno = 2026,
        Data = new DateOnly(2026, 1, 1),
        IdAnagrafica = anag,
        RagioneSocialeCliente = "Cliente",
        IdAttivita = att,
        NumeroAttivita = "A-1",
        DescrizioneAttivita = "Attività",
        IdCantiere = cant,
        UbicazioneCantiere = "Cantiere",
        ReportPath = reportPath,
    };

    [Fact]
    public async Task ElencoEsportabili_esclude_i_verbali_senza_file_fisico()
    {
        var repo = new FakeVerbaleConsultazioneRepository();
        var conFile = Guid.CreateVersion7();
        var senzaFile = Guid.CreateVersion7();
        repo.Verbali.Add(Verbale(conFile,   Anag1, Att1, Cant1, "report/con-file.pdf"));
        repo.Verbali.Add(Verbale(senzaFile, Anag1, Att1, Cant1, "report/sparito.pdf"));

        var storage = new FakeVerbaleReportStorage();
        storage.Presenti.Add("report/con-file.pdf");   // l'altro NON è presente

        var sut = new VerbaleConsultazioneManager(repo, storage);

        var risultato = await sut.ElencoEsportabiliAsync();

        Assert.Single(risultato);
        Assert.Equal(conFile, risultato[0].IdVerbale);
    }

    [Fact]
    public async Task ElencoEsportabili_esclude_reportpath_null()
    {
        var repo = new FakeVerbaleConsultazioneRepository();
        repo.Verbali.Add(Verbale(Guid.CreateVersion7(), Anag1, Att1, Cant1, reportPath: null));

        var sut = new VerbaleConsultazioneManager(repo, new FakeVerbaleReportStorage());

        var risultato = await sut.ElencoEsportabiliAsync();

        Assert.Empty(risultato);
    }

    [Fact]
    public async Task ElencoPerFiltro_solo_anagrafica_ritorna_tutti_i_verbali_del_cliente()
    {
        var (repo, storage) = ScenarioMultiLivello();
        var sut = new VerbaleConsultazioneManager(repo, storage);

        var risultato = await sut.ElencoPerFiltroAsync(Anag1, idAttivita: null, idCantiere: null);

        // Anag1 ha 3 verbali (2 su Att1/Cant1+Cant2, 1 su Att2/Cant1); Anag2 escluso.
        Assert.Equal(3, risultato.Count);
        Assert.All(risultato, v => Assert.Equal(Anag1, v.IdAnagrafica));
    }

    [Fact]
    public async Task ElencoPerFiltro_con_attivita_restringe_all_attivita()
    {
        var (repo, storage) = ScenarioMultiLivello();
        var sut = new VerbaleConsultazioneManager(repo, storage);

        var risultato = await sut.ElencoPerFiltroAsync(Anag1, Att1, idCantiere: null);

        Assert.Equal(2, risultato.Count);
        Assert.All(risultato, v => Assert.Equal(Att1, v.IdAttivita));
    }

    [Fact]
    public async Task ElencoPerFiltro_con_cantiere_restringe_al_cantiere()
    {
        var (repo, storage) = ScenarioMultiLivello();
        var sut = new VerbaleConsultazioneManager(repo, storage);

        var risultato = await sut.ElencoPerFiltroAsync(Anag1, Att1, Cant2);

        Assert.Single(risultato);
        Assert.Equal(Cant2, risultato[0].IdCantiere);
    }

    // 4 verbali con file presente: 3 su Anag1 (Att1/Cant1, Att1/Cant2, Att2/Cant1)
    // + 1 su Anag2. Tutti hanno il file fisico.
    private static (FakeVerbaleConsultazioneRepository, FakeVerbaleReportStorage) ScenarioMultiLivello()
    {
        var repo = new FakeVerbaleConsultazioneRepository();
        var storage = new FakeVerbaleReportStorage();

        void Aggiungi(Guid anag, Guid att, Guid cant)
        {
            var path = $"report/{Guid.CreateVersion7()}.pdf";
            repo.Verbali.Add(Verbale(Guid.CreateVersion7(), anag, att, cant, path));
            storage.Presenti.Add(path);
        }

        Aggiungi(Anag1, Att1, Cant1);
        Aggiungi(Anag1, Att1, Cant2);
        Aggiungi(Anag1, Att2, Cant1);
        Aggiungi(Anag2, Att1, Cant1);

        return (repo, storage);
    }
}
