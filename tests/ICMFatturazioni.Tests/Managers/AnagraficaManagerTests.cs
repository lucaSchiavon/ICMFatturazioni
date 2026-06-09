using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Anagrafica. Tre obiettivi:
///   1) validazione dei campi obbligatori solleva eccezioni tipizzate
///      con il motivo corretto;
///   2) il manager delega al repository senza mutare i dati;
///   3) la DELETE rispetta il pattern doppia difesa (pre-check + sentinel).
/// Niente DB reale: si usa <see cref="FakeAnagraficaRepository"/>.
/// </summary>
public class AnagraficaManagerTests
{
    // -----------------------------------------------------------------
    // Helper: costruisce un'anagrafica valida di default
    // -----------------------------------------------------------------
    private static Anagrafica AnagraficaValida(string? rs = "Acme S.r.l.") => new()
    {
        TipoAnagrafica = TipoAnagrafica.Societa,
        RagioneSociale = rs!,
        SiglaPaese     = "IT",
    };

    // =================================================================
    // Validazione campi obbligatori
    // =================================================================

    [Fact]
    public async Task CreaAsync_RagioneSocialeVuota_LanciaAnagraficaInvalidaConMotivoRagioneSociale()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var input = AnagraficaValida(rs: "");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_RagioneSocialeWhitespace_LanciaAnagraficaInvalida()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var input = AnagraficaValida(rs: "   \t  ");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_RagioneSocialeVuota_LanciaAnagraficaInvalida()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var input = AnagraficaValida(rs: "");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.AggiornaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    // =================================================================
    // Happy path: il manager delega correttamente al repository
    // =================================================================

    [Fact]
    public async Task CreaAsync_AnagraficaValida_RestituisceIdEPersisteSulRepository()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);

        var id = await sut.CreaAsync(AnagraficaValida());

        Assert.NotEqual(Guid.Empty, id);
        var persistita = await fake.GetByIdAsync(id);
        Assert.NotNull(persistita);
        Assert.Equal("Acme S.r.l.", persistita!.RagioneSociale);
    }

    [Fact]
    public async Task ElencoAsync_RestituisceLeAnagraficheOrdinatePerRagioneSociale()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);

        await sut.CreaAsync(AnagraficaValida(rs: "Beta S.p.A."));
        await sut.CreaAsync(AnagraficaValida(rs: "Alfa S.r.l."));
        await sut.CreaAsync(AnagraficaValida(rs: "Gamma S.n.c."));

        var elenco = await sut.ElencoAsync();

        Assert.Collection(elenco,
            a => Assert.Equal("Alfa S.r.l.", a.RagioneSociale),
            a => Assert.Equal("Beta S.p.A.", a.RagioneSociale),
            a => Assert.Equal("Gamma S.n.c.", a.RagioneSociale));
    }

    // =================================================================
    // DELETE: pattern doppia difesa
    // =================================================================

    [Fact]
    public async Task EliminaAsync_SeHasDipendenze_LanciaAnagraficaConDipendenze_NoCallSulRepository()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var id = await sut.CreaAsync(AnagraficaValida());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<AnagraficaConDipendenzeException>(
            () => sut.EliminaAsync(id));

        // Verifica che la riga NON sia stata rimossa nonostante la
        // chiamata: il pre-check è scattato prima del DELETE.
        Assert.NotNull(await fake.GetByIdAsync(id));
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaLaRiga()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var id = await sut.CreaAsync(AnagraficaValida());

        await sut.EliminaAsync(id);

        // Soft-delete (ADR D22): la riga resta nel repository ma disattivata,
        // e non compare più nell'elenco (che restituisce solo le attive).
        var persistita = await fake.GetByIdAsync(id);
        Assert.NotNull(persistita);
        Assert.False(persistita!.IsAttivo);
        Assert.DoesNotContain(await sut.ElencoAsync(), a => a.IdAnagrafica == id);
    }

    [Fact]
    public async Task EEliminabileAsync_RispecchiaLoStatoDelleDipendenze()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = new AnagraficaManager(fake);
        var id = await sut.CreaAsync(AnagraficaValida());

        Assert.True(await sut.EEliminabileAsync(id));

        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }

    // =================================================================
    // Conversione TipoAnagrafica ↔ char (helper)
    // =================================================================

    [Theory]
    [InlineData(TipoAnagrafica.Societa, 'S')]
    [InlineData(TipoAnagrafica.Privato, 'P')]
    [InlineData(TipoAnagrafica.EntePubblico, 'E')]
    public void TipoAnagrafica_ToDbCode_RestituisceIlCarattereAtteso(TipoAnagrafica tipo, char atteso)
    {
        Assert.Equal(atteso, tipo.ToDbCode());
    }

    [Theory]
    [InlineData('S', TipoAnagrafica.Societa)]
    [InlineData('P', TipoAnagrafica.Privato)]
    [InlineData('E', TipoAnagrafica.EntePubblico)]
    public void TipoAnagrafica_FromDbCode_RestituisceLEnumAtteso(char code, TipoAnagrafica atteso)
    {
        Assert.Equal(atteso, TipoAnagraficaExtensions.FromDbCode(code));
    }

    [Fact]
    public void TipoAnagrafica_FromDbCode_CarattereInatteso_LanciaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TipoAnagraficaExtensions.FromDbCode('X'));
    }
}
