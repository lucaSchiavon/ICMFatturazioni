using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test dell'orchestratore <see cref="BancaAppoggioManager"/> sul modello
/// normalizzato. Obiettivi:
///   1) get-or-create di banca/agenzia (riuso, niente doppioni; CAB aggiornato);
///   2) validazioni (Banca obbligatoria, formato ABI/CAB, IBAN, coerenza IBAN↔ABI/CAB);
///   3) IBAN solo azienda;
///   4) anti-duplicato del legame;
///   5) DELETE con doppia difesa + audit.
/// SUT reale con i manager Banca/Agenzia su repository fake (no DB).
/// </summary>
public class BancaAppoggioManagerTests
{
    // IBAN canonico italiano: ABI 05428, CAB 11101.
    private const string IbanOk = "IT60X0542811101000000123456";

    private sealed class Sut
    {
        public FakeBancaRepository BancheRepo { get; } = new();
        public FakeAgenziaRepository AgenzieRepo { get; } = new();
        public FakeBancaAppoggioRepository AppoggiRepo { get; }
        public FakeAuditManager Audit { get; } = new();
        public BancaAppoggioManager Manager { get; }

        public Sut()
        {
            AppoggiRepo = new FakeBancaAppoggioRepository(BancheRepo, AgenzieRepo);
            var bancaMgr = new BancaManager(BancheRepo, Audit);
            var agMgr = new AgenziaManager(AgenzieRepo, Audit);
            Manager = new BancaAppoggioManager(AppoggiRepo, bancaMgr, agMgr, Audit);
        }
    }

    // L'IBAN è obbligatorio per le banche azienda: di default l'helper ne fornisce
    // uno valido (IbanOk). I test che verificano l'obbligo passano iban: null.
    private static BancaAppoggioInput Azienda(string banca = "Intesa", string? abi = null, string? agenzia = null, string? cab = null, string? iban = IbanOk)
        => new(Guid.Empty, null, banca, abi, agenzia, cab, iban);

    private static BancaAppoggioInput Cliente(Guid idCliente, string banca = "Unicredit", string? abi = null, string? agenzia = null, string? cab = null)
        => new(Guid.Empty, idCliente, banca, abi, agenzia, cab, null);

    // =================================================================
    // Get-or-create + happy path
    // =================================================================

    [Fact]
    public async Task CreaAsync_Valido_RisolveBancaAgenziaEPersiste()
    {
        var sut = new Sut();
        // ABI/CAB coerenti con IbanOk (05428/11101): l'IBAN azienda è obbligatorio.
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa", abi: "05428", agenzia: "Sede", cab: "11101"));

        var riga = await sut.Manager.GetByIdAsync(id);
        Assert.NotNull(riga);
        Assert.Equal("Intesa", riga!.BancaNome);
        Assert.Equal("05428", riga.ABI);
        Assert.Equal("Sede", riga.AgenziaNome);
        Assert.Equal("11101", riga.CAB);
        Assert.Single(sut.BancheRepo.Store);
        Assert.Single(sut.AgenzieRepo.Store);
    }

    [Fact]
    public async Task CreaAsync_StessaBancaAgenzia_RiusaLeAnagrafiche()
    {
        var sut = new Sut();
        var c1 = Guid.CreateVersion7();
        var c2 = Guid.CreateVersion7();
        await sut.Manager.CreaAsync(Cliente(c1, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));
        await sut.Manager.CreaAsync(Cliente(c2, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));

        // Una sola banca e una sola agenzia, riusate da entrambi gli appoggi.
        Assert.Single(sut.BancheRepo.Store);
        Assert.Single(sut.AgenzieRepo.Store);
    }

    [Fact]
    public async Task CreaAsync_StessaAgenziaCabDiverso_AggiornaLAgenzia_NonDuplica()
    {
        // Il fix del problema segnalato: reinserendo la stessa agenzia con CAB
        // diverso si AGGIORNA l'agenzia, non si crea un doppione divergente.
        var sut = new Sut();
        var c1 = Guid.CreateVersion7();
        var c2 = Guid.CreateVersion7();
        await sut.Manager.CreaAsync(Cliente(c1, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));
        await sut.Manager.CreaAsync(Cliente(c2, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "99999"));

        var agenzia = Assert.Single(sut.AgenzieRepo.Store.Values);
        Assert.Equal("99999", agenzia.CAB);
    }

    // =================================================================
    // Validazioni
    // =================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_BancaVuota_LanciaBancaObbligatoria(string banca)
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(() => sut.Manager.CreaAsync(Azienda(banca: banca)));
        Assert.Equal(BancaAppoggioInvalidaMotivo.BancaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_CabSenzaAgenzia_Lancia()
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", cab: "11101")));
        Assert.Equal(BancaAppoggioInvalidaMotivo.CabSenzaAgenzia, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AbiNonNumerico_LanciaAbiNonValido()
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", abi: "12A")));
        Assert.Equal(BancaAppoggioInvalidaMotivo.AbiNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_CabFormatoErrato_LanciaCabNonValido()
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", agenzia: "Sede", cab: "123")));
        Assert.Equal(BancaAppoggioInvalidaMotivo.CabNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_IbanFormalmenteErrato_LanciaIbanNonValido()
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", iban: "IT99X0542811101000000123456")));
        Assert.Equal(BancaAppoggioInvalidaMotivo.IbanNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_IbanValidoMaAbiIncoerente_LanciaIbanIncoerente()
    {
        var sut = new Sut();
        // IBAN contiene ABI 05428, ma indico 99999.
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", abi: "99999", iban: IbanOk)));
        Assert.Equal(BancaAppoggioInvalidaMotivo.IbanIncoerente, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_IbanCoerenteConAbiCab_Persiste()
    {
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(
            banca: "Intesa", abi: "05428", agenzia: "Sede", cab: "11101", iban: IbanOk));

        var riga = await sut.Manager.GetByIdAsync(id);
        Assert.Equal(IbanOk, riga!.IBAN);
        Assert.Equal("05428", riga.ABI);
    }

    [Fact]
    public async Task CreaAsync_IbanSuBancaCliente_VienePersistitoNull()
    {
        var sut = new Sut();
        var idCliente = Guid.CreateVersion7();
        // L'IBAN è solo dell'azienda: anche se passato nell'input cliente, viene
        // ignorato (forzato null) prima della validazione.
        var input = new BancaAppoggioInput(Guid.Empty, idCliente, "Unicredit", null, null, null, IbanOk);

        var id = await sut.Manager.CreaAsync(input);

        var riga = await sut.Manager.GetByIdAsync(id);
        Assert.Null(riga!.IBAN);
    }

    // =================================================================
    // IBAN obbligatorio (solo azienda) + unicità
    // =================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_AziendaSenzaIban_LanciaIbanObbligatorio(string? iban)
    {
        var sut = new Sut();
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Intesa", iban: iban)));
        Assert.Equal(BancaAppoggioInvalidaMotivo.IbanObbligatorio, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_BancaClienteSenzaIban_Ammessa()
    {
        // Il cliente non ha IBAN: l'obbligo vale solo per l'azienda.
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Cliente(Guid.CreateVersion7(), banca: "Unicredit"));
        Assert.NotNull(await sut.Manager.GetByIdAsync(id));
    }

    [Fact]
    public async Task CreaAsync_StessoIbanSuAltraBancaAzienda_LanciaIbanDuplicato()
    {
        var sut = new Sut();
        await sut.Manager.CreaAsync(Azienda(banca: "Intesa", iban: IbanOk));

        // Stesso IBAN su una banca diversa: un IBAN = un solo conto.
        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Azienda(banca: "Unicredit", iban: IbanOk)));
        Assert.Equal(BancaAppoggioInvalidaMotivo.IbanDuplicato, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_MantieneProprioIban_NonLanciaDuplicato()
    {
        // Modificare un appoggio lasciandone invariato l'IBAN non deve essere
        // scambiato per un duplicato di sé stesso (escludiId).
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa", iban: IbanOk));

        var update = new BancaAppoggioInput(id, null, "Intesa", null, "Sede", "11101", IbanOk);
        await sut.Manager.AggiornaAsync(update);

        var riga = await sut.Manager.GetByIdAsync(id);
        Assert.Equal(IbanOk, riga!.IBAN);
        Assert.Equal("Sede", riga.AgenziaNome);
    }

    // =================================================================
    // Anti-duplicato legame
    // =================================================================

    [Fact]
    public async Task CreaAsync_StessoIntestatarioBancaAgenzia_LanciaLegameDuplicato()
    {
        var sut = new Sut();
        var idCliente = Guid.CreateVersion7();
        await sut.Manager.CreaAsync(Cliente(idCliente, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));

        var ex = await Assert.ThrowsAsync<BancaAppoggioInvalidaException>(
            () => sut.Manager.CreaAsync(Cliente(idCliente, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101")));
        Assert.Equal(BancaAppoggioInvalidaMotivo.LegameDuplicato, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_StessaBancaAgenziaIntestatariDiversi_Ammesso()
    {
        var sut = new Sut();
        var c1 = Guid.CreateVersion7();
        var c2 = Guid.CreateVersion7();
        await sut.Manager.CreaAsync(Cliente(c1, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));

        // Stessa banca/agenzia ma altro cliente: ammesso.
        var id = await sut.Manager.CreaAsync(Cliente(c2, banca: "Unicredit", agenzia: "Piazza Erbe", cab: "11101"));
        Assert.NotNull(await sut.Manager.GetByIdAsync(id));
    }

    // =================================================================
    // Audit + DELETE doppia difesa
    // =================================================================

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazioneAppoggio()
    {
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa"));

        // Almeno una voce per l'appoggio (oltre a quella della banca creata).
        Assert.Contains(sut.Audit.Voci, v => v.EntityType == "BancaAppoggio" && v.EntityId == id && v.Operazione == AuditOperazione.Creazione);
    }

    [Fact]
    public async Task EliminaAsync_SeHasDipendenze_LanciaConDipendenze_NonDisattiva()
    {
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa"));
        sut.AppoggiRepo.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<BancaAppoggioConDipendenzeException>(() => sut.Manager.EliminaAsync(id));
        Assert.True((await sut.Manager.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaERegistraAudit()
    {
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa"));
        sut.Audit.Voci.Clear();

        await sut.Manager.EliminaAsync(id);

        Assert.DoesNotContain(await sut.Manager.ElencoAsync(), r => r.IdBancaAppoggio == id);
        Assert.Contains(sut.Audit.Voci, v => v.EntityType == "BancaAppoggio" && v.Operazione == AuditOperazione.Eliminazione);
    }

    [Fact]
    public async Task EEliminabileAsync_RispecchiaLoStatoDelleDipendenze()
    {
        var sut = new Sut();
        var id = await sut.Manager.CreaAsync(Azienda(banca: "Intesa"));

        Assert.True(await sut.Manager.EEliminabileAsync(id));
        sut.AppoggiRepo.DipendenzeDa.Add(id);
        Assert.False(await sut.Manager.EEliminabileAsync(id));
    }

    [Fact]
    public async Task ElencoAsync_AziendaPrima()
    {
        var sut = new Sut();
        var idCliente = Guid.CreateVersion7();
        await sut.Manager.CreaAsync(Cliente(idCliente, banca: "Unicredit"));
        await sut.Manager.CreaAsync(Azienda(banca: "Intesa"));

        var elenco = await sut.Manager.ElencoAsync();
        Assert.True(elenco[0].IsBancaAzienda);
    }
}
