using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>UtenteManager</c>. Obiettivi:
///   1) autenticazione robusta e anti-enumeration (ogni causa di fallimento →
///      sempre <c>null</c>, mai eccezione che distingua lo stato dell'account);
///   2) validazioni di creazione/aggiornamento (username, ruolo, lunghezza
///      password, univocità) con le eccezioni tipizzate attese;
///   3) corretta delega al repository (hashing, GUID v7, override password/tema).
/// Si usa il <see cref="PasswordHasherService"/> REALE: gli scenari di login
/// passano così dal vero round-trip hash → verify.
/// </summary>
public class UtenteManagerTests
{
    private static readonly Guid RuoloOperatore = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (UtenteManager sut, FakeUtenteRepository repo, PasswordHasherService hasher) NewSut()
    {
        var repo = new FakeUtenteRepository();
        var hasher = new PasswordHasherService();
        return (new UtenteManager(repo, hasher, new FakeAuditManager()), repo, hasher);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var repo = new FakeUtenteRepository();
        var audit = new FakeAuditManager();
        var sut = new UtenteManager(repo, new PasswordHasherService(), audit);

        var id = await sut.CreaAsync("mrossi", null, "m@x.it", RuoloOperatore, "Mario Rossi");

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Utente", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("mrossi", voce.Descrizione);
    }

    // =================================================================
    // Autenticazione
    // =================================================================

    [Theory]
    [InlineData("", "password")]
    [InlineData("   ", "password")]
    [InlineData("mario", "")]
    public async Task AutenticaAsync_CredenzialiVuote_RestituisceNull(string username, string password)
    {
        var (sut, _, _) = NewSut();
        Assert.Null(await sut.AutenticaAsync(username, password));
    }

    [Fact]
    public async Task AutenticaAsync_UtenteInesistente_RestituisceNull()
    {
        var (sut, _, _) = NewSut();
        Assert.Null(await sut.AutenticaAsync("nessuno", "qualunque1"));
    }

    [Fact]
    public async Task AutenticaAsync_UtenteDisattivato_RestituisceNull()
    {
        var (sut, repo, hasher) = NewSut();
        await repo.InsertAsync(new Utente
        {
            IdUtente = Guid.CreateVersion7(),
            Username = "spento",
            PasswordHash = hasher.HashPassword("Password123"),
            IdRuolo = RuoloOperatore,
            Attivo = false,
        });

        Assert.Null(await sut.AutenticaAsync("spento", "Password123"));
    }

    [Fact]
    public async Task AutenticaAsync_UtenteInvitatoSenzaPassword_RestituisceNull()
    {
        var (sut, repo, _) = NewSut();
        await repo.InsertAsync(new Utente
        {
            IdUtente = Guid.CreateVersion7(),
            Username = "invitato",
            PasswordHash = null,   // mai attivato
            IdRuolo = RuoloOperatore,
            Attivo = true,
        });

        Assert.Null(await sut.AutenticaAsync("invitato", "qualunque1"));
    }

    [Fact]
    public async Task AutenticaAsync_PasswordErrata_RestituisceNull()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync("mario", "passwordGiusta1", null, RuoloOperatore);

        Assert.Null(await sut.AutenticaAsync("mario", "passwordSbagliata1"));
    }

    [Fact]
    public async Task AutenticaAsync_CredenzialiCorrette_RestituisceUtenteEAggiornaUltimoLogin()
    {
        var (sut, repo, _) = NewSut();
        var id = await sut.CreaAsync("mario", "passwordGiusta1", null, RuoloOperatore);

        var utente = await sut.AutenticaAsync("mario", "passwordGiusta1");

        Assert.NotNull(utente);
        Assert.Equal(id, utente!.IdUtente);
        Assert.NotNull(repo.Store[id].UltimoLoginUtc);   // login registrato
    }

    [Fact]
    public async Task AutenticaAsync_UsernameCaseInsensitive()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync("Mario", "passwordGiusta1", null, RuoloOperatore);

        Assert.NotNull(await sut.AutenticaAsync("mario", "passwordGiusta1"));
    }

    // =================================================================
    // Creazione utente
    // =================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_UsernameVuoto_LanciaArgumentException(string username)
    {
        var (sut, _, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreaAsync(username, "Password123", null, RuoloOperatore));
    }

    [Fact]
    public async Task CreaAsync_RuoloVuoto_LanciaArgumentException()
    {
        var (sut, _, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreaAsync("mario", "Password123", null, Guid.Empty));
    }

    [Fact]
    public async Task CreaAsync_UsernameDuplicato_LanciaUtenteDuplicato()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync("mario", "Password123", null, RuoloOperatore);

        var ex = await Assert.ThrowsAsync<UtenteDuplicatoException>(
            () => sut.CreaAsync("mario", "password2", null, RuoloOperatore));
        Assert.Equal("mario", ex.Username);
    }

    [Fact]
    public async Task CreaAsync_PasswordTroppoCorta_LanciaArgumentException()
    {
        var (sut, _, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreaAsync("mario", "corta", null, RuoloOperatore));
    }

    [Fact]
    public async Task CreaAsync_SenzaPassword_CreaUtenteInvitatoConHashNull()
    {
        var (sut, repo, _) = NewSut();
        var id = await sut.CreaAsync("invitato", password: null, email: null, RuoloOperatore);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Null(repo.Store[id].PasswordHash);
        Assert.True(repo.Store[id].Attivo);
    }

    [Fact]
    public async Task CreaAsync_ConPassword_PersisteHashVerificabileEGuidV7()
    {
        var (sut, repo, hasher) = NewSut();
        var id = await sut.CreaAsync("mario", "passwordGiusta1", "m@x.it", RuoloOperatore, "Mario Rossi");

        var creato = repo.Store[id];
        Assert.NotEqual(Guid.Empty, creato.IdUtente);
        Assert.Equal((byte)0x70, (byte)(creato.IdUtente.ToByteArray()[7] & 0xF0)); // versione UUID 7
        Assert.NotNull(creato.PasswordHash);
        Assert.True(hasher.VerifyHashedPassword(creato.PasswordHash!, "passwordGiusta1"));
        Assert.Equal("Mario Rossi", creato.NomeCompleto);
    }

    // =================================================================
    // Preferenza tema
    // =================================================================

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("auto")]
    public async Task ImpostaTemaPreferitoAsync_TemaValido_AggiornaIlRepository(string tema)
    {
        var (sut, repo, _) = NewSut();
        var id = await sut.CreaAsync("mario", "Password123", null, RuoloOperatore);

        await sut.ImpostaTemaPreferitoAsync(id, tema);

        Assert.Equal(tema, repo.Store[id].TemaPreferito);
    }

    [Fact]
    public async Task ImpostaTemaPreferitoAsync_TemaNonValido_LanciaArgumentOutOfRange()
    {
        var (sut, _, _) = NewSut();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.ImpostaTemaPreferitoAsync(Guid.NewGuid(), "fucsia"));
    }

    // =================================================================
    // Aggiornamento profilo
    // =================================================================

    [Fact]
    public async Task AggiornaAsync_UsernameVuoto_LanciaArgumentException()
    {
        var (sut, _, _) = NewSut();
        var id = await sut.CreaAsync("mario", "Password123", null, RuoloOperatore);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.AggiornaAsync(id, "", null, RuoloOperatore, true));
    }

    [Fact]
    public async Task AggiornaAsync_UsernameDiUnAltroUtente_LanciaUtenteDuplicato()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync("mario", "Password123", null, RuoloOperatore);
        var idLuigi = await sut.CreaAsync("luigi", "Password123", null, RuoloOperatore);

        await Assert.ThrowsAsync<UtenteDuplicatoException>(
            () => sut.AggiornaAsync(idLuigi, "mario", null, RuoloOperatore, true));
    }

    [Fact]
    public async Task AggiornaAsync_StessoUsernameSulloStessoUtente_NonEDuplicato()
    {
        var (sut, repo, _) = NewSut();
        var id = await sut.CreaAsync("mario", "Password123", "m@x.it", RuoloOperatore);

        // Rinominare lo stesso utente con il suo username corrente non deve
        // scattare come duplicato (l'id è escluso dal pre-check).
        await sut.AggiornaAsync(id, "mario", "nuova@x.it", RuoloOperatore, false);

        Assert.Equal("nuova@x.it", repo.Store[id].Email);
        Assert.False(repo.Store[id].Attivo);
    }

    // =================================================================
    // Imposta password
    // =================================================================

    [Fact]
    public async Task ImpostaPasswordAsync_PasswordTroppoCorta_LanciaArgumentException()
    {
        var (sut, _, _) = NewSut();
        var id = await sut.CreaAsync("invitato", null, null, RuoloOperatore);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.ImpostaPasswordAsync(id, "corta"));
    }

    [Fact]
    public async Task ImpostaPasswordAsync_PasswordValida_ImpostaHashVerificabile()
    {
        var (sut, repo, hasher) = NewSut();
        var id = await sut.CreaAsync("invitato", null, null, RuoloOperatore);

        await sut.ImpostaPasswordAsync(id, "nuovaPassword1");

        var hash = repo.Store[id].PasswordHash;
        Assert.NotNull(hash);
        Assert.True(hasher.VerifyHashedPassword(hash!, "nuovaPassword1"));
    }

    // =================================================================
    // Pass-through di lettura
    // =================================================================

    [Fact]
    public async Task GetByUsername_GetById_Elenco_DeleganoAlRepository()
    {
        var (sut, repo, _) = NewSut();
        repo.RuoliNomi[RuoloOperatore] = "Operatore";
        var id = await sut.CreaAsync("mario", "Password123", null, RuoloOperatore);

        Assert.NotNull(await sut.GetByUsernameAsync("mario"));
        Assert.NotNull(await sut.GetByIdAsync(id));
        var elenco = await sut.ElencoAsync();
        Assert.Single(elenco);
        Assert.Equal("Operatore", elenco[0].RuoloNome);
    }
}
