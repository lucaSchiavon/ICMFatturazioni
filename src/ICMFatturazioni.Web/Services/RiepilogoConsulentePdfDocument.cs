using System.Globalization;

using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Costruzione e rendering del report "Riepilogo attività consulente" con
/// PDFsharp-MigraDoc (stesso stack di <see cref="ScadenzarioPdfDocument"/>).
/// Layout fedele al campione legacy (AttivitaConsulenteLucaSchiavon.pdf):
/// A4 verticale, titolo su due righe (nome del consulente, o "TUTTI I
/// CONSULENTI" nella variante generale), gerarchia Cliente → Attività → righe
/// di consulenza con le tranche di pagamento in colonna, totali per riga /
/// attività / cliente (e per consulente nella variante generale) + TOTALE
/// GENERALE; piè di pagina con data/ora, filtro usato e "Pagina X di Y".
/// </summary>
internal sealed class RiepilogoConsulentePdfDocument
{
    private readonly RiepilogoConsulentePdfData _data;

    public RiepilogoConsulentePdfDocument(RiepilogoConsulentePdfData data) => _data = data;

    static RiepilogoConsulentePdfDocument()
    {
        // Stesso resolver degli altri documenti: font di Windows, nessun embedded.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    // ── Cultura e formattazioni ─────────────────────────────────────────────
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);

    // ── Palette e geometria (coerenti con gli altri report) ────────────────
    private static readonly Color Nero        = Colors.Black;
    private static readonly Color GrigioScuro = Color.FromRgb(90, 90, 90);
    private static readonly Color GrigioBordo = Color.FromRgb(120, 120, 120);
    private static readonly Color GrigioHead  = Color.FromRgb(232, 232, 232);

    // A4 verticale 21 cm − 2×1,5 cm di margine.
    private const double ContentWidthCm = 18.0;
    private const double BordoPt        = 0.75;
    private const double RigaBordoPt    = 0.25;

    // Larghezze colonna (somma = ContentWidthCm), come il campione:
    // TIPO CONSULENZA | NOTA | IMPORTO | DATA | NOTA | PAGATO | RESIDUO
    private const double ColTipoCm       = 3.6;
    private const double ColNotaRigaCm   = 2.8;
    private const double ColImportoCm    = 2.2;
    private const double ColDataCm       = 2.0;
    private const double ColNotaTrancheCm = 2.8;
    private const double ColPagatoCm     = 2.2;
    private const double ColResiduoCm    = 2.4;

    // ── Entry point ─────────────────────────────────────────────────────────
    public byte[] Render()
    {
        var document = BuildDocument();
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private Document BuildDocument()
    {
        var doc = new Document();
        doc.Info.Title   = "Riepilogo attività consulente";
        doc.Info.Subject = "Report delle consulenze a carico dello studio per consulente";

        SetupStyles(doc);

        var section = doc.AddSection();
        section.PageSetup.PageFormat     = PageFormat.A4;
        section.PageSetup.Orientation    = Orientation.Portrait;
        section.PageSetup.TopMargin      = Unit.FromCentimeter(3.2);   // spazio per il titolo a 2 righe
        section.PageSetup.BottomMargin   = Unit.FromCentimeter(2.0);
        section.PageSetup.LeftMargin     = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin    = Unit.FromCentimeter(1.5);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(1.0);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.9);

        BuildHeader(section);
        BuildFooter(section);
        ComposeCorpo(section);

        return doc;
    }

    private static void SetupStyles(Document doc)
    {
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name  = "Arial";
        normal.Font.Size  = 8;
        normal.Font.Color = Nero;
        normal.ParagraphFormat.SpaceAfter  = 0;
        normal.ParagraphFormat.SpaceBefore = 0;
    }

    // ── Intestazione: titolo + consulente (come il campione), su ogni pagina ─
    private void BuildHeader(Section section)
    {
        var titolo = section.Headers.Primary.AddParagraph("RIEPILOGO ATTIVITA' CONSULENTE");
        titolo.Format.Alignment = ParagraphAlignment.Center;
        titolo.Format.Font.Bold = true;
        titolo.Format.Font.Size = 15;

        var nome = section.Headers.Primary.AddParagraph(
            (_data.NomeConsulente ?? "TUTTI I CONSULENTI").ToUpper(It));
        nome.Format.Alignment = ParagraphAlignment.Center;
        nome.Format.Font.Bold = true;
        nome.Format.Font.Size = 13;
    }

    // ── Piè di pagina: data/ora · filtro usato · Pagina X di Y ──────────────
    private void BuildFooter(Section section)
    {
        var p = section.Footers.Primary.AddParagraph();
        p.Format.Borders.Top = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
        p.Format.SpaceBefore = 2;
        p.Format.Font.Size   = 8;
        p.Format.Font.Color  = GrigioScuro;
        p.Format.TabStops.ClearAll();
        p.Format.AddTabStop(Unit.FromCentimeter(ContentWidthCm / 2), TabAlignment.Center);
        p.Format.AddTabStop(Unit.FromCentimeter(ContentWidthCm), TabAlignment.Right);

        var generato = p.AddFormattedText(_data.GeneratoIl.ToString("dd/MM/yyyy HH:mm:ss", It));
        generato.Italic = true;

        p.AddTab();
        var filtro = p.AddFormattedText(_data.DescrizioneFiltro);
        filtro.Bold = true;

        p.AddTab();
        p.AddText("Pagina ");
        p.AddPageField();
        p.AddText(" di ");
        p.AddNumPagesField();
    }

    // ── Corpo: gerarchia consulente → cliente → attività → righe+tranche ────
    private void ComposeCorpo(Section section)
    {
        if (_data.Righe.Count == 0)
        {
            var vuoto = section.AddParagraph("Nessuna consulenza corrisponde ai filtri selezionati.");
            vuoto.Format.Font.Size   = 10;
            vuoto.Format.Font.Italic = true;
            vuoto.Format.SpaceBefore = 12;
            return;
        }

        var table = ComposeTabella(section);

        // Il livello "consulente" si emette solo nella variante generale.
        var generale = _data.NomeConsulente is null;

        // Scorrimento delle righe (già ordinate consulente → cliente → attività):
        // i cambi di gruppo chiudono i totali del gruppo precedente.
        string? consulente = null, cliente = null;
        Guid? attivita = null;
        string etichettaAttivita = string.Empty;
        decimal totAttImporto = 0, totAttPagato = 0;
        decimal totCliImporto = 0, totCliPagato = 0;
        decimal totConImporto = 0, totConPagato = 0;
        decimal totGenImporto = 0, totGenPagato = 0;

        void ChiudiAttivita()
        {
            if (attivita is null) return;
            EmitTotale(table, $"Totale {Tronca(etichettaAttivita, 55)}", totAttImporto, totAttPagato, fontSize: 8.5, spessore: RigaBordoPt);
            attivita = null; totAttImporto = 0; totAttPagato = 0;
        }

        void ChiudiCliente()
        {
            ChiudiAttivita();
            if (cliente is null) return;
            EmitTotale(table, $"Totale {cliente}", totCliImporto, totCliPagato, fontSize: 9.5, spessore: BordoPt);
            cliente = null; totCliImporto = 0; totCliPagato = 0;
        }

        void ChiudiConsulente()
        {
            ChiudiCliente();
            if (!generale || consulente is null) return;
            EmitTotale(table, $"Totale consulente {consulente}", totConImporto, totConPagato, fontSize: 9.5, spessore: BordoPt);
            consulente = null; totConImporto = 0; totConPagato = 0;
        }

        foreach (var riga in _data.Righe)
        {
            if (generale && riga.ConsulenteDescrizione != consulente)
            {
                ChiudiConsulente();
                consulente = riga.ConsulenteDescrizione;
                EmitRigaConsulente(table, consulente);
            }

            if (riga.RagioneSociale != cliente)
            {
                ChiudiCliente();
                cliente = riga.RagioneSociale;
                EmitRigaCliente(table, cliente, indent: generale);
            }

            if (riga.IdAttivita != attivita)
            {
                ChiudiAttivita();
                attivita = riga.IdAttivita;
                etichettaAttivita = $"{riga.AttivitaNumero}-{riga.AttivitaDescrizione}";
                EmitRigaAttivita(table, etichettaAttivita);
                EmitTestataColonne(table);
            }

            EmitRigaConsulenza(table, riga);

            totAttImporto += riga.Importo; totAttPagato += riga.Pagato;
            totCliImporto += riga.Importo; totCliPagato += riga.Pagato;
            totConImporto += riga.Importo; totConPagato += riga.Pagato;
            totGenImporto += riga.Importo; totGenPagato += riga.Pagato;
        }

        ChiudiConsulente();
        EmitTotale(table, "TOTALE GENERALE", totGenImporto, totGenPagato, fontSize: 10.5, spessore: BordoPt, spazioSopra: 8);
    }

    private static Table ComposeTabella(Section section)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;

        table.AddColumn(Unit.FromCentimeter(ColTipoCm));
        table.AddColumn(Unit.FromCentimeter(ColNotaRigaCm));
        var colImporto = table.AddColumn(Unit.FromCentimeter(ColImportoCm));
        table.AddColumn(Unit.FromCentimeter(ColDataCm));
        table.AddColumn(Unit.FromCentimeter(ColNotaTrancheCm));
        var colPagato  = table.AddColumn(Unit.FromCentimeter(ColPagatoCm));
        var colResiduo = table.AddColumn(Unit.FromCentimeter(ColResiduoCm));
        colImporto.Format.Alignment = ParagraphAlignment.Right;
        colPagato.Format.Alignment  = ParagraphAlignment.Right;
        colResiduo.Format.Alignment = ParagraphAlignment.Right;

        return table;
    }

    // "Consulente: X" — solo variante generale, livello più esterno.
    private static void EmitRigaConsulente(Table table, string consulente)
    {
        var row = table.AddRow();
        row.TopPadding    = 8;
        row.BottomPadding = 1;
        row.Cells[0].MergeRight = 6;
        var p = row.Cells[0].AddParagraph($"Consulente: {consulente}");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 11.5;
        row.Borders.Bottom = new Border { Color = Nero, Width = BordoPt, Visible = true };
    }

    // "Cliente: X" (filetto sotto, come il campione).
    private static void EmitRigaCliente(Table table, string cliente, bool indent)
    {
        var row = table.AddRow();
        row.TopPadding    = 6;
        row.BottomPadding = 1;
        row.Cells[0].MergeRight = 6;
        var p = row.Cells[0].AddParagraph($"Cliente: {cliente}");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 10.5;
        if (indent) p.Format.LeftIndent = Unit.FromCentimeter(0.3);
        row.Borders.Bottom = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
    }

    // "Attività: Numero-Descrizione" (indentata sotto il cliente).
    private static void EmitRigaAttivita(Table table, string etichetta)
    {
        var row = table.AddRow();
        row.TopPadding    = 4;
        row.BottomPadding = 1;
        row.Cells[0].MergeRight = 6;
        var p = row.Cells[0].AddParagraph($"Attività: {etichetta}");
        p.Format.Font.Bold  = true;
        p.Format.Font.Size  = 9;
        p.Format.LeftIndent = Unit.FromCentimeter(0.4);
        row.Borders.Bottom = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
    }

    // Testata colonne, ripetuta sotto ogni attività (come il campione).
    private static void EmitTestataColonne(Table table)
    {
        var row = table.AddRow();
        row.Format.Font.Bold = true;
        row.Format.Font.Size = 7;
        row.Shading.Color    = GrigioHead;
        row.TopPadding       = 1.5;
        row.BottomPadding    = 1.5;
        row.Cells[0].AddParagraph("TIPO CONSULENZA");
        row.Cells[1].AddParagraph("NOTA");
        row.Cells[2].AddParagraph("IMPORTO");
        row.Cells[3].AddParagraph("DATA");
        row.Cells[4].AddParagraph("NOTA");
        row.Cells[5].AddParagraph("PAGATO");
        row.Cells[6].AddParagraph("RESIDUO");
    }

    // Riga di consulenza: tipo/nota/importo; le tranche occupano le colonne
    // centrali — la prima sulla stessa riga, le successive su righe dedicate
    // (come il campione). Chiude con la riga "Totale" (importo/pagato/residuo).
    private void EmitRigaConsulenza(Table table, SchedaConsulenzaRiga riga)
    {
        var tranche = _data.TranchePerRiga.TryGetValue(riga.IdAttivitaConsulente, out var t)
            ? t : Array.Empty<AttivitaConsulentePagamento>();

        var row = table.AddRow();
        row.TopPadding        = 2;
        row.BottomPadding     = 1;
        row.VerticalAlignment = VerticalAlignment.Top;
        row.Cells[0].AddParagraph(riga.TipoDescrizione);
        if (!string.IsNullOrWhiteSpace(riga.Nota))
            row.Cells[1].AddParagraph(riga.Nota);
        row.Cells[2].AddParagraph(Eur(riga.Importo));
        if (tranche.Count > 0)
            CompilaCelleTranche(row, tranche[0]);

        foreach (var pagamento in tranche.Skip(1))
        {
            var rowTranche = table.AddRow();
            rowTranche.TopPadding    = 1;
            rowTranche.BottomPadding = 1;
            CompilaCelleTranche(rowTranche, pagamento);
        }

        // Riga "Totale" della consulenza: importo · pagato · residuo.
        var totale = table.AddRow();
        totale.TopPadding      = 1.5;
        totale.BottomPadding   = 1.5;
        totale.Format.Font.Bold = true;
        totale.Borders.Top    = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
        totale.Borders.Bottom = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
        var p = totale.Cells[0].AddParagraph("Totale");
        p.Format.LeftIndent = Unit.FromCentimeter(0.2);
        totale.Cells[2].AddParagraph(Eur(riga.Importo));
        totale.Cells[5].AddParagraph(Eur(riga.Pagato));
        totale.Cells[6].AddParagraph(Eur(riga.Residuo));
    }

    private static void CompilaCelleTranche(Row row, AttivitaConsulentePagamento pagamento)
    {
        row.Cells[3].AddParagraph(pagamento.DataPagamento.ToString("dd/MM/yyyy", It));
        if (!string.IsNullOrWhiteSpace(pagamento.Nota))
            row.Cells[4].AddParagraph(pagamento.Nota);
        row.Cells[5].AddParagraph(Eur(pagamento.Importo));
    }

    // Riga di totale (attività / cliente / consulente / generale):
    // importo · pagato · residuo nelle rispettive colonne.
    private static void EmitTotale(Table table, string etichetta, decimal importo, decimal pagato,
        double fontSize, double spessore, double spazioSopra = 2)
    {
        var row = table.AddRow();
        row.TopPadding       = spazioSopra;
        row.BottomPadding    = 2;
        row.Format.Font.Bold = true;
        row.Format.Font.Size = fontSize;
        row.Borders.Top    = new Border { Color = Nero, Width = spessore, Visible = true };
        row.Borders.Bottom = new Border { Color = Nero, Width = spessore, Visible = true };

        row.Cells[0].MergeRight = 1;
        row.Cells[0].AddParagraph(etichetta);
        row.Cells[2].AddParagraph(Eur(importo));
        row.Cells[5].AddParagraph(Eur(pagato));
        row.Cells[6].AddParagraph(Eur(importo - pagato));
    }

    // Troncamento dell'etichetta attività nei totali (il campione taglia
    // "…appartamento in condo"): evita che il totale vada a capo.
    private static string Tronca(string testo, int max)
        => testo.Length <= max ? testo : testo[..max];
}
