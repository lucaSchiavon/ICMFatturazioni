using System.Text;

using ICMFatturazioni.Web.Export;

namespace ICMFatturazioni.Tests.Export;

/// <summary>
/// Test del generatore CSV usato dall'export delle griglie Audit/Log.
/// Verifica: BOM UTF-8, separatore ';', quoting RFC 4180 e mitigazione
/// CSV-injection.
/// </summary>
public class CsvExportTests
{
    private static string DecodeSenzaBom(byte[] bytes)
    {
        var bom = Encoding.UTF8.GetPreamble();
        Assert.True(bytes.Length >= bom.Length);
        for (var i = 0; i < bom.Length; i++)
            Assert.Equal(bom[i], bytes[i]);          // preambolo BOM presente
        return Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length);
    }

    [Fact]
    public void Genera_IntestazioneERighe_ProduceCsvConSeparatorePuntoVirgola()
    {
        var bytes = CsvExport.Genera(
            new[] { "A", "B" },
            new[] { (IReadOnlyList<string?>)new string?[] { "1", "2" } });

        var testo = DecodeSenzaBom(bytes);

        Assert.Equal("A;B\r\n1;2\r\n", testo);
    }

    [Fact]
    public void Genera_ValoreConSeparatoreVirgoletteOAcapo_VieneQuotato()
    {
        var bytes = CsvExport.Genera(
            new[] { "H" },
            new[]
            {
                (IReadOnlyList<string?>)new string?[] { "con;punto e virgola" },
                new string?[] { "con \"virgolette\"" },
                new string?[] { "con\na-capo" },
            });

        var testo = DecodeSenzaBom(bytes);

        Assert.Contains("\"con;punto e virgola\"", testo);
        Assert.Contains("\"con \"\"virgolette\"\"\"", testo);   // virgolette raddoppiate
        Assert.Contains("\"con\na-capo\"", testo);
    }

    [Theory]
    [InlineData("=SOMMA(A1)")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@cmd")]
    public void Genera_ValoreDaFormula_VienePrefissatoConApostrofo(string valore)
    {
        var bytes = CsvExport.Genera(
            new[] { "H" },
            new[] { (IReadOnlyList<string?>)new string?[] { valore } });

        var testo = DecodeSenzaBom(bytes);

        // La cella dati (seconda riga) inizia con l'apostrofo di neutralizzazione.
        var righeCsv = testo.Split("\r\n");
        Assert.StartsWith("'", righeCsv[1]);
    }

    [Fact]
    public void Genera_ValoreNull_DiventaCellaVuota()
    {
        var bytes = CsvExport.Genera(
            new[] { "A", "B" },
            new[] { (IReadOnlyList<string?>)new string?[] { null, "x" } });

        var testo = DecodeSenzaBom(bytes);

        Assert.Equal("A;B\r\n;x\r\n", testo);
    }
}
