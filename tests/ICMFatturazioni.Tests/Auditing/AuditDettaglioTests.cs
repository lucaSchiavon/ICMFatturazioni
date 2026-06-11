using ICMFatturazioni.Web.Auditing;

namespace ICMFatturazioni.Tests.Auditing;

/// <summary>
/// Test di <c>AuditDettaglio</c>: snapshot dei campi (esclusi i segreti) e diff
/// prima→dopo dei soli campi cambiati.
/// </summary>
public class AuditDettaglioTests
{
    private sealed record Persona(string Nome, int Eta, string? PasswordHash);

    [Fact]
    public void Snapshot_IncludeICampi_EdEscludeISegreti()
    {
        var json = AuditDettaglio.Snapshot(new Persona("Mario", 30, "segretissima"));

        Assert.Contains("Mario", json);
        Assert.Contains("30", json);
        Assert.DoesNotContain("segretissima", json);     // valore del segreto
        Assert.DoesNotContain("PasswordHash", json);     // anche la chiave
    }

    [Fact]
    public void Diff_RiportaSoloICampiCambiati()
    {
        var prima = new { Nome = "Acme", Citta = "Roma" };
        var dopo = new { Nome = "Acme S.p.A.", Citta = "Roma" };

        var json = AuditDettaglio.Diff(prima, dopo);

        Assert.NotNull(json);
        Assert.Contains("Nome", json);
        Assert.Contains("Acme S.p.A.", json);
        Assert.DoesNotContain("Citta", json);            // invariato → assente
    }

    [Fact]
    public void Diff_NessunCambiamento_RestituisceNull()
    {
        var x = new { A = 1, B = "x" };

        Assert.Null(AuditDettaglio.Diff(x, new { A = 1, B = "x" }));
    }
}
