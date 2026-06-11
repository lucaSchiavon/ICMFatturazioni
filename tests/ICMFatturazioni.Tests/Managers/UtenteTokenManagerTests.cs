using System.Security.Cryptography;
using System.Text;
using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>UtenteTokenManager</c> (magic-link attivazione/reset). Obiettivi:
///   1) la generazione persiste solo l'HASH (mai il token in chiaro), con la
///      scadenza corretta, e revoca i token precedenti dello stesso tipo;
///   2) la validazione distingue i motivi con l'ordine UX
///      NonTrovato → Revocato → GiaUsato → Scaduto;
///   3) il consumo è monouso (sentinel TOCTOU) e imposta la password.
/// La scadenza è pilotata con un <see cref="MutableTimeProvider"/>, senza attese.
/// </summary>
public class UtenteTokenManagerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Utente = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableTimeProvider(DateTimeOffset start) => _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private static (UtenteTokenManager sut, FakeUtenteTokenRepository repo, MutableTimeProvider clock) NewSut(
        int attivazioneOre = 168, int resetOre = 1)
    {
        var clock = new MutableTimeProvider(T0);
        var repo = new FakeUtenteTokenRepository(clock);
        var options = Options.Create(new UtenteTokenOptions
        {
            AttivazioneOreDefault = attivazioneOre,
            ResetOreDefault = resetOre,
        });
        return (new UtenteTokenManager(repo, new FakeAuditManager(), clock, options), repo, clock);
    }

    // Variante che espone anche l'audit, per i test che ne verificano le voci.
    private static (UtenteTokenManager sut, FakeUtenteTokenRepository repo, FakeAuditManager audit) NewSutConAudit(
        int attivazioneOre = 168, int resetOre = 1)
    {
        var clock = new MutableTimeProvider(T0);
        var repo = new FakeUtenteTokenRepository(clock);
        var audit = new FakeAuditManager();
        var options = Options.Create(new UtenteTokenOptions
        {
            AttivazioneOreDefault = attivazioneOre,
            ResetOreDefault = resetOre,
        });
        return (new UtenteTokenManager(repo, audit, clock, options), repo, audit);
    }

    private static byte[] Hash(string raw) => SHA256.HashData(Encoding.UTF8.GetBytes(raw));

    // =================================================================
    // Generazione
    // =================================================================

    [Fact]
    public async Task CreaAttivazioneAsync_PersisteSoloLHashConScadenzaCorretta()
    {
        var (sut, repo, _) = NewSut(attivazioneOre: 168);

        var raw = await sut.CreaAttivazioneAsync(Utente);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        var token = Assert.Single(repo.Tokens);
        Assert.Equal(UtenteTokenTipo.Attivazione, token.Tipo);
        Assert.Equal(Hash(raw), token.TokenHash);                 // salvato l'hash, non il raw
        Assert.NotEqual(Encoding.UTF8.GetBytes(raw), token.TokenHash);
        Assert.Equal(T0.UtcDateTime.AddHours(168), token.ScadenzaUtc);
    }

    [Fact]
    public async Task CreaResetAsync_UsaLaValiditaDiResetPiuStretta()
    {
        var (sut, repo, _) = NewSut(resetOre: 1);

        await sut.CreaResetAsync(Utente);

        Assert.Equal(T0.UtcDateTime.AddHours(1), repo.Tokens[0].ScadenzaUtc);
        Assert.Equal(UtenteTokenTipo.Reset, repo.Tokens[0].Tipo);
    }

    [Fact]
    public async Task CreaResetAsync_RevocaIlTokenDiResetPrecedente()
    {
        var (sut, repo, _) = NewSut();
        var primo = await sut.CreaResetAsync(Utente);

        await sut.CreaResetAsync(Utente);   // reinvio

        // Il primo link non deve più validare: è stato revocato.
        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ValidaAsync(primo, UtenteTokenTipo.Reset));
        Assert.Equal(UtenteTokenInvalidoMotivo.Revocato, ex.Motivo);
        Assert.Equal(2, repo.Tokens.Count);
    }

    [Fact]
    public async Task CreaResetAsync_NonRevocaUnTokenDiTipoDiverso()
    {
        var (sut, _, _) = NewSut();
        var attivazione = await sut.CreaAttivazioneAsync(Utente);

        await sut.CreaResetAsync(Utente);   // tipo diverso: non tocca l'attivazione

        var token = await sut.ValidaAsync(attivazione, UtenteTokenTipo.Attivazione);
        Assert.Equal(Utente, token.UtenteId);
    }

    // =================================================================
    // Validazione
    // =================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("token-inesistente")]
    public async Task ValidaAsync_TokenAssenteOSconosciuto_NonTrovato(string raw)
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ValidaAsync(raw, UtenteTokenTipo.Attivazione));
        Assert.Equal(UtenteTokenInvalidoMotivo.NonTrovato, ex.Motivo);
    }

    [Fact]
    public async Task ValidaAsync_TipoNonCorrispondente_NonTrovato()
    {
        var (sut, _, _) = NewSut();
        var raw = await sut.CreaAttivazioneAsync(Utente);

        // Token di attivazione validato come reset → indistinguibile da assente.
        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ValidaAsync(raw, UtenteTokenTipo.Reset));
        Assert.Equal(UtenteTokenInvalidoMotivo.NonTrovato, ex.Motivo);
    }

    [Fact]
    public async Task ValidaAsync_TokenScaduto_Scaduto()
    {
        var (sut, _, clock) = NewSut(resetOre: 1);
        var raw = await sut.CreaResetAsync(Utente);

        clock.Advance(TimeSpan.FromHours(2));   // oltre la scadenza

        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ValidaAsync(raw, UtenteTokenTipo.Reset));
        Assert.Equal(UtenteTokenInvalidoMotivo.Scaduto, ex.Motivo);
    }

    [Fact]
    public async Task ValidaAsync_RevocatoEScaduto_PrevaleRevocato()
    {
        var (sut, _, clock) = NewSut(resetOre: 1);
        var primo = await sut.CreaResetAsync(Utente);   // verrà revocato dal reinvio
        await sut.CreaResetAsync(Utente);
        clock.Advance(TimeSpan.FromHours(2));           // ora è anche scaduto

        // Entrambe le condizioni valgono: l'ordine UX mette Revocato prima.
        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ValidaAsync(primo, UtenteTokenTipo.Reset));
        Assert.Equal(UtenteTokenInvalidoMotivo.Revocato, ex.Motivo);
    }

    [Fact]
    public async Task ValidaAsync_TokenValido_RestituisceLEntity()
    {
        var (sut, _, _) = NewSut();
        var raw = await sut.CreaAttivazioneAsync(Utente);

        var token = await sut.ValidaAsync(raw, UtenteTokenTipo.Attivazione);

        Assert.Equal(Utente, token.UtenteId);
        Assert.Null(token.UsatoUtc);
    }

    // =================================================================
    // Consumo (monouso + sentinel TOCTOU)
    // =================================================================

    [Fact]
    public async Task ConsumaAsync_TokenValido_ImpostaPasswordERitornaUtente()
    {
        var (sut, repo, _) = NewSut();
        var raw = await sut.CreaAttivazioneAsync(Utente);

        var idUtente = await sut.ConsumaAsync(raw, UtenteTokenTipo.Attivazione, "hash-nuova-pwd");

        Assert.Equal(Utente, idUtente);
        Assert.Equal("hash-nuova-pwd", repo.PasswordImpostate[Utente]);
        Assert.NotNull(repo.Tokens[0].UsatoUtc);   // marcato usato
    }

    [Fact]
    public async Task ConsumaAsync_DueVolte_LaSecondaFallisceComeGiaUsato()
    {
        var (sut, _, _) = NewSut();
        var raw = await sut.CreaAttivazioneAsync(Utente);
        await sut.ConsumaAsync(raw, UtenteTokenTipo.Attivazione, "hash1");

        var ex = await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ConsumaAsync(raw, UtenteTokenTipo.Attivazione, "hash2"));
        Assert.Equal(UtenteTokenInvalidoMotivo.GiaUsato, ex.Motivo);
    }

    [Fact]
    public async Task ConsumaAsync_TokenScaduto_NonImpostaPassword()
    {
        var (sut, repo, clock) = NewSut(resetOre: 1);
        var raw = await sut.CreaResetAsync(Utente);
        clock.Advance(TimeSpan.FromHours(2));

        await Assert.ThrowsAsync<UtenteTokenInvalidoException>(
            () => sut.ConsumaAsync(raw, UtenteTokenTipo.Reset, "hashX"));
        Assert.Empty(repo.PasswordImpostate);
    }

    // =================================================================
    // Audit (mirror logging+audit): emissione → Creazione, consumo → Modifica
    // =================================================================

    [Fact]
    public async Task CreaAttivazioneAsync_RegistraAuditDiCreazione()
    {
        var (sut, repo, audit) = NewSutConAudit();

        await sut.CreaAttivazioneAsync(Utente);

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal(nameof(UtenteToken), voce.EntityType);
        Assert.Equal(repo.Tokens[0].Id, voce.EntityId);    // riferisce il token emesso
        Assert.Contains(Utente.ToString(), voce.Descrizione);
    }

    [Fact]
    public async Task ConsumaAsync_RegistraAuditDiModifica()
    {
        var (sut, repo, audit) = NewSutConAudit();
        var raw = await sut.CreaAttivazioneAsync(Utente);   // 1ª voce: Creazione

        await sut.ConsumaAsync(raw, UtenteTokenTipo.Attivazione, "hash-nuova");

        // Seconda voce: la Modifica del consumo, sullo stesso token.
        Assert.Equal(2, audit.Voci.Count);
        var consumo = audit.Voci[1];
        Assert.Equal(AuditOperazione.Modifica, consumo.Operazione);
        Assert.Equal(repo.Tokens[0].Id, consumo.EntityId);
    }
}
