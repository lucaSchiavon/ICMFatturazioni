using System.Globalization;

using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Costruzione e rendering del report "Scadenziario attività clienti" con
/// PDFsharp-MigraDoc. Rendering puro e sincrono: riceve uno
/// <see cref="ScadenzarioPdfData"/> già risolto e restituisce i byte del PDF.
/// Il layout ricalca il modello legacy (docs/Scadenze.pdf): A4 orizzontale,
/// titolo ripetuto su ogni pagina, righe raggruppate per anno → mese con
/// totali di mese/anno/generale, piè di pagina con data/ora di generazione,
/// filtro usato (spec) e "Pagina X di Y".
/// </summary>
internal sealed class ScadenzarioPdfDocument
{
    private readonly ScadenzarioPdfData _data;

    public ScadenzarioPdfDocument(ScadenzarioPdfData data) => _data = data;

    static ScadenzarioPdfDocument()
    {
        // Stesso resolver dei documenti avviso/fattura: font "principali" di
        // Windows (Arial) letti da %WINDIR%\Fonts, nessun font embedded.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    // ── Cultura e formattazioni ─────────────────────────────────────────────
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v) => v.ToString("N2", It);   // 1.234,56

    // ── Palette e geometria (coerenti con AvvisoPdfDocument) ────────────────
    private static readonly Color Nero        = Colors.Black;
    private static readonly Color GrigioScuro = Color.FromRgb(90, 90, 90);
    private static readonly Color GrigioBordo = Color.FromRgb(120, 120, 120);
    private static readonly Color GrigioHead  = Color.FromRgb(232, 232, 232);

    // A4 orizzontale 29,7 cm − 2×1,5 cm di margine.
    private const double ContentWidthCm = 26.7;
    private const double BordoPt        = 0.75;
    private const double RigaBordoPt    = 0.25;

    // Larghezze colonna (somma = ContentWidthCm).
    private const double ColScadenzaCm  = 2.3;
    private const double ColTipoCm      = 0.7;
    private const double ColClienteCm   = 5.0;
    private const double ColAttivitaCm  = 8.2;
    private const double ColDettaglioCm = 6.6;
    private const double ColImportoCm   = 2.5;
    private const double ColEvasaCm     = 1.4;

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
        doc.Info.Title   = "Scadenziario attività clienti";
        doc.Info.Subject = "Report scadenze di pagamento delle attività clienti";

        SetupStyles(doc);

        var section = doc.AddSection();
        section.PageSetup.PageFormat     = PageFormat.A4;
        section.PageSetup.Orientation    = Orientation.Landscape;
        section.PageSetup.TopMargin      = Unit.FromCentimeter(2.6);   // spazio per il titolo ripetuto
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

    // ── Intestazione di pagina: titolo ripetuto su ogni pagina ──────────────
    private static void BuildHeader(Section section)
    {
        var titolo = section.Headers.Primary.AddParagraph("SCADENZIARIO ATTIVITÀ CLIENTI");
        titolo.Format.Alignment  = ParagraphAlignment.Center;
        titolo.Format.Font.Bold  = true;
        titolo.Format.Font.Size  = 14;
    }

    // ── Piè di pagina: data/ora · filtro usato (spec) · Pagina X di Y ───────
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

    // ── Corpo: tabella unica con gruppi anno/mese e totali ──────────────────
    private void ComposeCorpo(Section section)
    {
        if (_data.Righe.Count == 0)
        {
            var vuoto = section.AddParagraph("Nessuna scadenza corrisponde ai filtri selezionati.");
            vuoto.Format.Font.Size   = 10;
            vuoto.Format.Font.Italic = true;
            vuoto.Format.SpaceBefore = 12;
            return;
        }

        var table = ComposeTabella(section);

        // Scorrimento delle righe (già ordinate per data): i cambi di anno/mese
        // emettono le righe-gruppo e chiudono i totali del gruppo precedente.
        int? anno = null;
        int? mese = null;
        decimal totaleMese = 0m, totaleAnno = 0m, totaleGenerale = 0m;

        foreach (var riga in _data.Righe)
        {
            if (riga.DataScadenza.Year != anno)
            {
                if (mese is not null) EmitTotaleMese(table, anno!.Value, mese.Value, totaleMese);
                if (anno is not null) EmitTotaleAnno(table, anno.Value, totaleAnno);
                anno = riga.DataScadenza.Year;
                totaleAnno = 0m;
                mese = null;
                EmitRigaAnno(table, anno.Value);
            }

            if (riga.DataScadenza.Month != mese)
            {
                if (mese is not null) EmitTotaleMese(table, anno!.Value, mese.Value, totaleMese);
                mese = riga.DataScadenza.Month;
                totaleMese = 0m;
                EmitRigaMese(table, mese.Value);
            }

            EmitRigaScadenza(table, riga);
            totaleMese     += riga.Importo;
            totaleAnno     += riga.Importo;
            totaleGenerale += riga.Importo;
        }

        EmitTotaleMese(table, anno!.Value, mese!.Value, totaleMese);
        EmitTotaleAnno(table, anno.Value, totaleAnno);
        EmitTotaleGenerale(table, totaleGenerale);
    }

    private static Table ComposeTabella(Section section)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;

        table.AddColumn(Unit.FromCentimeter(ColScadenzaCm));
        table.AddColumn(Unit.FromCentimeter(ColTipoCm));
        table.AddColumn(Unit.FromCentimeter(ColClienteCm));
        table.AddColumn(Unit.FromCentimeter(ColAttivitaCm));
        table.AddColumn(Unit.FromCentimeter(ColDettaglioCm));
        var colImporto = table.AddColumn(Unit.FromCentimeter(ColImportoCm));
        var colEvasa   = table.AddColumn(Unit.FromCentimeter(ColEvasaCm));
        colImporto.Format.Alignment = ParagraphAlignment.Right;
        colEvasa.Format.Alignment   = ParagraphAlignment.Center;

        // Riga di intestazione, ripetuta a ogni cambio pagina.
        var head = table.AddRow();
        head.HeadingFormat    = true;
        head.Format.Font.Bold = true;
        head.Format.Font.Size = 8;
        head.Shading.Color    = GrigioHead;
        head.TopPadding       = 2;
        head.BottomPadding    = 2;
        head.Borders.Bottom   = new Border { Color = Nero, Width = BordoPt, Visible = true };
        head.Cells[0].AddParagraph("SCADENZA");
        head.Cells[1].AddParagraph("T");
        head.Cells[2].AddParagraph("CLIENTE");
        head.Cells[3].AddParagraph("ATTIVITÀ");
        head.Cells[4].AddParagraph("DETTAGLIO");
        head.Cells[5].AddParagraph("IMPORTO");
        head.Cells[6].AddParagraph("EVASA");

        return table;
    }

    // Riga-gruppo "ANNO: 2026" (a tutta larghezza, filetto superiore marcato).
    private static void EmitRigaAnno(Table table, int anno)
    {
        var row = table.AddRow();
        row.TopPadding    = 6;
        row.BottomPadding = 1;
        row.Cells[0].MergeRight = 6;
        var p = row.Cells[0].AddParagraph($"ANNO: {anno}");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 10;
        row.Borders.Bottom = new Border { Color = Nero, Width = BordoPt, Visible = true };
    }

    // Riga-gruppo "MESE DI: LUGLIO" (mesi in italiano maiuscolo, come il modello).
    private static void EmitRigaMese(Table table, int mese)
    {
        var row = table.AddRow();
        row.TopPadding    = 4;
        row.BottomPadding = 1;
        row.Cells[0].MergeRight = 6;
        var p = row.Cells[0].AddParagraph($"MESE DI: {NomeMese(mese)}");
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 8.5;
        row.Borders.Bottom = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
    }

    // Riga di dettaglio: una scadenza.
    private void EmitRigaScadenza(Table table, ScadenzaReport riga)
    {
        var row = table.AddRow();
        row.TopPadding        = 3;
        row.BottomPadding     = 3;
        row.VerticalAlignment = VerticalAlignment.Top;
        row.Borders.Bottom    = new Border { Color = GrigioHead, Width = RigaBordoPt, Visible = true };

        var data = row.Cells[0].AddParagraph(riga.DataScadenza.ToString("dd/MM/yyyy", It));
        data.Format.Font.Bold = true;

        row.Cells[1].AddParagraph(riga.TipoCliente.ToDbCode().ToString());
        row.Cells[2].AddParagraph(riga.ClienteRagioneSociale);
        row.Cells[3].AddParagraph($"{Abbrevia(riga.TipoAttivitaDescrizione)}{riga.NumeroAttivita}-{riga.DescrizioneAttivita}");

        // Dettaglio + eventuali annotazioni (nota della rata, avviso di evasione).
        row.Cells[4].AddParagraph($"{Abbrevia(riga.TipoDettaglioDescrizione)}{riga.DescrizioneDettaglio}");
        if (!string.IsNullOrWhiteSpace(riga.NotaScadenza))
            AddAnnotazione(row.Cells[4], $"rata: {riga.NotaScadenza}");
        if (riga.IsEvasa)
            AddAnnotazione(row.Cells[4], riga.AvvisoDataEvasione is { } dataAvviso
                ? $"evasa nell'avviso del {dataAvviso.ToString("dd/MM/yyyy", It)}"
                : "evasa in avviso");

        row.Cells[5].AddParagraph(Eur(riga.Importo));

        if (riga.IsEvasa)
        {
            var evasa = row.Cells[6].AddParagraph("Sì");
            evasa.Format.Font.Bold = true;
        }
    }

    // Riga "TOTALE LUGLIO 2025" con importo in colonna, filetti sopra e sotto.
    private static void EmitTotaleMese(Table table, int anno, int mese, decimal totale)
        => EmitTotale(table, $"TOTALE {NomeMese(mese)} {anno}", totale, fontSize: 8.5, spessore: RigaBordoPt);

    // Riga "TOTALE 2025", più marcata.
    private static void EmitTotaleAnno(Table table, int anno, decimal totale)
        => EmitTotale(table, $"TOTALE {anno}", totale, fontSize: 9.5, spessore: BordoPt);

    // Riga finale "TOTALE GENERALE".
    private static void EmitTotaleGenerale(Table table, decimal totale)
        => EmitTotale(table, "TOTALE GENERALE", totale, fontSize: 10, spessore: BordoPt);

    private static void EmitTotale(Table table, string etichetta, decimal totale, double fontSize, double spessore)
    {
        var row = table.AddRow();
        row.TopPadding      = 2;
        row.BottomPadding   = 2;
        row.Format.Font.Bold = true;
        row.Format.Font.Size = fontSize;
        row.Borders.Top    = new Border { Color = Nero, Width = spessore, Visible = true };
        row.Borders.Bottom = new Border { Color = Nero, Width = spessore, Visible = true };

        row.Cells[0].MergeRight = 4;
        row.Cells[0].AddParagraph(etichetta);
        row.Cells[5].AddParagraph(Eur(totale));
    }

    // Annotazione secondaria sotto il dettaglio (nota della rata, evasione):
    // corsivo piccolo grigio, per non competere con la riga principale.
    private static void AddAnnotazione(Cell cell, string testo)
    {
        var p = cell.AddParagraph(testo);
        p.Format.Font.Size   = 7;
        p.Format.Font.Italic = true;
        p.Format.Font.Color  = GrigioScuro;
    }

    // "(PROGE) " dal tipo "PROGETTAZIONI": prime 5 lettere maiuscole tra parentesi,
    // come nel report legacy; stringa vuota se il tipo manca.
    private static string Abbrevia(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return string.Empty;
        var t = tipo.Trim().ToUpper(It);
        return $"({(t.Length <= 5 ? t : t[..5])}) ";
    }

    private static string NomeMese(int mese)
        => It.DateTimeFormat.GetMonthName(mese).ToUpper(It);
}
