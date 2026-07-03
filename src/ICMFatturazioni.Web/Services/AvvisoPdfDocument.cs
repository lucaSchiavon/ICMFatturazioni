using System.Globalization;

using ICMFatturazioni.Web.Entities;

using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Costruzione e rendering del PDF dell'Avviso di fattura con PDFsharp-MigraDoc.
/// Rendering puro e sincrono: riceve un <see cref="AvvisoPdfData"/> già risolto e
/// restituisce i byte del PDF, senza alcun I/O (nessuna immagine da materializzare).
/// Il layout ricalca il modulo "Avviso di parcella" dello studio (docs/AvvisoDiFattura.pdf):
/// intestazione a 2 colonne (studio / cliente), barra titolo, pagamento+banca,
/// oggetto, righe, cascata fiscale (C.N.P.A.I.A. + IVA − ritenuta + spese art.15),
/// nota legale e piè di pagina con l'attività.
/// </summary>
internal sealed class AvvisoPdfDocument
{
    private readonly AvvisoPdfData _data;

    public AvvisoPdfDocument(AvvisoPdfData data) => _data = data;

    // Il documento è una FATTURA (nata da un avviso) anziché l'avviso di parcella:
    // cambia titolo, barra (numero+data fattura), note finali e footer.
    private bool IsFattura => _data.Fattura is not null;

    static AvvisoPdfDocument()
    {
        // PdfSharp 6.x non risolve i font di sistema senza un resolver esplicito.
        // UseWindowsFontsUnderWindows abilita il resolver built-in che legge i font
        // "principali" (Arial, Times, ...) da %WINDIR%\Fonts. Nessun font embedded,
        // nessun font simbolico usato → non serve un FallbackFontResolver custom.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    // ── Cultura e formattazioni monetarie ──────────────────────────────────
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static string Eur(decimal v)      => v.ToString("N2", It);              // 1.234,56
    private static string Perc(decimal v)     => v.ToString("0.##", It);            // 22  /  22,5

    // ── Palette e geometria ─────────────────────────────────────────────────
    private static readonly Color Nero       = Colors.Black;
    private static readonly Color GrigioScuro = Color.FromRgb(90, 90, 90);
    private static readonly Color GrigioBordo = Color.FromRgb(120, 120, 120);
    private static readonly Color GrigioHead  = Color.FromRgb(232, 232, 232);
    private static readonly Color AzzurroBox  = Color.FromRgb(244, 247, 250);

    private const double ContentWidthCm = 18.0;   // A4 21cm − 2×1.5cm margine
    private const double BordoPt         = 0.75;
    private const double RigaBordoPt      = 0.25;

    private const string NotaLegale =
        "AVVISO DI PARCELLA - Non costituisce fattura. Regolare fattura verrà emessa al " +
        "momento del pagamento. Documento emesso in relazione al pagamento di corrispettivi " +
        "di operazioni assoggettate ad I.V.A. Esente da bollo, art. 6 tabella B, D.P.R. 642/1972.";

    // ── Entry point ──────────────────────────────────────────────────────────
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
        if (IsFattura)
        {
            var f = _data.Fattura!;
            doc.Info.Title   = $"Fattura {f.NumeroFattura}-{f.Anno}";
            doc.Info.Subject = "Fattura (documento di cortesia)";
        }
        else
        {
            doc.Info.Title   = $"Avviso di fattura {_data.Testata.DataAvviso:dd-MM-yyyy}";
            doc.Info.Subject = "Avviso di fattura (avviso di parcella)";
        }
        doc.Info.Author  = _data.Studio.RagioneSociale;

        SetupStyles(doc);

        var section = doc.AddSection();
        section.PageSetup.PageFormat    = PageFormat.A4;
        section.PageSetup.Orientation   = Orientation.Portrait;
        section.PageSetup.TopMargin     = Unit.FromCentimeter(2.4);
        section.PageSetup.BottomMargin  = Unit.FromCentimeter(1.8);
        section.PageSetup.LeftMargin    = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin   = Unit.FromCentimeter(1.5);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.8);

        BuildFooter(section);

        ComposeIntestazione(section);
        ComposeBarraTitolo(section);
        ComposePagamento(section);
        ComposeOggetto(section);
        ComposeRighe(section);
        ComposeCascata(section);
        ComposeNoteFinali(section);

        return doc;
    }

    private static void SetupStyles(Document doc)
    {
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name  = "Arial";   // font tecnico universale su Windows
        normal.Font.Size  = 9;
        normal.Font.Color = Nero;
        normal.ParagraphFormat.SpaceAfter  = 0;
        normal.ParagraphFormat.SpaceBefore = 0;
    }

    // ── Piè di pagina: "Attività: N - descrizione" ───────────────────────────
    private void BuildFooter(Section section)
    {
        var p = section.Footers.Primary.AddParagraph();
        p.Format.Borders.Top = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
        p.Format.SpaceBefore = 2;
        p.Format.Font.Size  = 8;
        p.Format.Font.Color = GrigioScuro;

        var a = _data.Attivita;
        var testo = a is null
            ? (IsFattura ? "Fattura" : "Avviso di fattura")
            : $"Attività: {a.Numero}{(string.IsNullOrWhiteSpace(a.Descrizione) ? "" : $" - {a.Descrizione}")}";
        p.AddText(testo);

        // Sulla fattura, a destra il riferimento all'avviso di origine (tab a destra).
        if (IsFattura)
        {
            p.Format.TabStops.ClearAll();
            p.Format.AddTabStop(Unit.FromCentimeter(ContentWidthCm), TabAlignment.Right);
            p.AddTab();
            p.AddText($"Riferimento Avviso del {_data.Testata.DataAvviso:dd/MM/yyyy}");
        }
    }

    // ── 1. Intestazione a 2 colonne: studio (sx) / cliente (dx) ───────────────
    private void ComposeIntestazione(Section section)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(10.0));  // studio
        table.AddColumn(Unit.FromCentimeter(0.4));   // gap
        table.AddColumn(Unit.FromCentimeter(7.6));   // box cliente

        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Top;

        ComposeStudio(row.Cells[0]);
        ComposeCliente(row.Cells[2]);

        AddSpacer(section, 10);
    }

    private void ComposeStudio(Cell cell)
    {
        var s = _data.Studio;

        var pRag = cell.AddParagraph(s.RagioneSociale);
        pRag.Format.Alignment = ParagraphAlignment.Center;
        pRag.Format.Font.Bold = true;
        pRag.Format.Font.Size = 12;

        var indirizzo = ComponiIndirizzoStudio(s);
        if (!string.IsNullOrWhiteSpace(indirizzo))
        {
            var p = cell.AddParagraph(indirizzo);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 10;
            p.Format.SpaceBefore = 1;
        }

        var recapiti = ComponiRecapitiTelefonici(s);
        if (!string.IsNullOrWhiteSpace(recapiti))
        {
            var p = cell.AddParagraph(recapiti);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 8;
            p.Format.SpaceBefore = 2;
        }

        var mail = ComponiMail(s);
        if (!string.IsNullOrWhiteSpace(mail))
        {
            var p = cell.AddParagraph(mail);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 7.5;
        }

        var fiscale = ComponiFiscaleStudio(s);
        if (!string.IsNullOrWhiteSpace(fiscale))
        {
            var p = cell.AddParagraph(fiscale);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Bold = true;
            p.Format.Font.Size = 8;
        }
    }

    private void ComposeCliente(Cell cell)
    {
        cell.Borders.Width  = BordoPt;
        cell.Borders.Color  = GrigioBordo;
        cell.Borders.Visible = true;
        cell.Shading.Color  = AzzurroBox;
        cell.Format.LeftIndent  = 6;
        cell.Format.RightIndent = 6;
        cell.Format.SpaceBefore = 4;
        cell.Format.SpaceAfter  = 4;

        var c = _data.Cliente;

        var pSpett = cell.AddParagraph("Spett.");
        pSpett.Format.Font.Italic = true;
        pSpett.Format.Font.Size   = 8;
        pSpett.Format.Font.Color  = GrigioScuro;

        var pRag = cell.AddParagraph(c.RagioneSociale);
        pRag.Format.Font.Size = 11;
        pRag.Format.SpaceAfter = 4;

        if (!string.IsNullOrWhiteSpace(c.Indirizzo))
            cell.AddParagraph(c.Indirizzo!).Format.Font.Size = 10;

        var localita = ComponiLocalitaCliente(c);
        if (!string.IsNullOrWhiteSpace(localita))
            cell.AddParagraph(localita).Format.Font.Size = 10;

        if (!string.IsNullOrWhiteSpace(c.PIVA))
            cell.AddParagraph($"P. Iva: {c.PIVA}").Format.Font.Size = 10;
    }

    // ── 2. Barra titolo ───────────────────────────────────────────────────────
    private void ComposeBarraTitolo(Section section)
    {
        if (IsFattura) ComposeBarraTitoloFattura(section);
        else           ComposeBarraTitoloAvviso(section);
    }

    // Barra fattura: FATTURA | Numero Fattura | Data Fattura | Pagina | Valuta
    private void ComposeBarraTitoloFattura(Section section)
    {
        var f = _data.Fattura!;

        var table = section.AddTable();
        table.Borders.Width  = BordoPt;
        table.Borders.Color  = Nero;
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(9.0));   // titolo
        table.AddColumn(Unit.FromCentimeter(2.8));   // numero fattura
        table.AddColumn(Unit.FromCentimeter(3.0));   // data fattura
        table.AddColumn(Unit.FromCentimeter(1.6));   // pagina
        table.AddColumn(Unit.FromCentimeter(1.6));   // valuta

        var rh = table.AddRow();
        rh.TopPadding = 3; rh.BottomPadding = 3;
        rh.VerticalAlignment = VerticalAlignment.Center;

        var titolo = rh.Cells[0].AddParagraph("FATTURA");
        titolo.Format.Alignment = ParagraphAlignment.Center;
        titolo.Format.Font.Bold = true;
        titolo.Format.Font.Size = 20;
        rh.Cells[0].MergeDown = 1;

        AddTestoCentrato(rh.Cells[1], "Numero Fattura", 8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[2], "Data Fattura",   8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[3], "Pagina",         8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[4], "Valuta",         8, bold: true, bg: GrigioHead);

        var rv = table.AddRow();
        rv.TopPadding = 3; rv.BottomPadding = 3;
        rv.VerticalAlignment = VerticalAlignment.Center;
        AddTestoCentrato(rv.Cells[1], $"{f.NumeroFattura}/{f.Anno}",          10, bold: true);
        AddTestoCentrato(rv.Cells[2], f.DataFattura.ToString("dd/MM/yyyy"), 10, bold: true);

        var pagCell = rv.Cells[3].AddParagraph();
        pagCell.Format.Alignment = ParagraphAlignment.Center;
        pagCell.Format.Font.Size = 10;
        pagCell.AddPageField();

        AddTestoCentrato(rv.Cells[4], "EUR", 10, bold: false);

        AddSpacer(section, 6);
    }

    // Barra avviso: AVVISO DI FATTURA | Data | Pagina | Valuta
    private void ComposeBarraTitoloAvviso(Section section)
    {
        var table = section.AddTable();
        table.Borders.Width  = BordoPt;
        table.Borders.Color  = Nero;
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(11.0));  // titolo
        table.AddColumn(Unit.FromCentimeter(3.4));   // data avviso
        table.AddColumn(Unit.FromCentimeter(1.6));   // pagina
        table.AddColumn(Unit.FromCentimeter(2.0));   // valuta

        // Riga header (etichette).
        var rh = table.AddRow();
        rh.TopPadding = 3; rh.BottomPadding = 3;
        rh.VerticalAlignment = VerticalAlignment.Center;

        var titolo = rh.Cells[0].AddParagraph("AVVISO DI FATTURA");
        titolo.Format.Alignment = ParagraphAlignment.Center;
        titolo.Format.Font.Bold = true;
        titolo.Format.Font.Size = 20;
        rh.Cells[0].MergeDown = 1;   // il titolo occupa entrambe le righe

        AddTestoCentrato(rh.Cells[1], "Data Avviso", 8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[2], "Pagina",      8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[3], "Valuta",      8, bold: true, bg: GrigioHead);

        // Riga valori.
        var rv = table.AddRow();
        rv.TopPadding = 3; rv.BottomPadding = 3;
        rv.VerticalAlignment = VerticalAlignment.Center;
        // Cells[0] è coperta dal merge del titolo.
        AddTestoCentrato(rv.Cells[1], _data.Testata.DataAvviso.ToString("dd/MM/yyyy"), 10, bold: true);

        var pagCell = rv.Cells[2].AddParagraph();
        pagCell.Format.Alignment = ParagraphAlignment.Center;
        pagCell.Format.Font.Size = 10;
        pagCell.AddPageField();

        AddTestoCentrato(rv.Cells[3], "EUR", 10, bold: false);

        AddSpacer(section, 6);
    }

    // ── 3. Descrizione pagamento + banca d'appoggio ───────────────────────────
    private void ComposePagamento(Section section)
    {
        var table = section.AddTable();
        table.Borders.Width  = BordoPt;
        table.Borders.Color  = Nero;
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(5.0));
        table.AddColumn(Unit.FromCentimeter(13.0));

        var rh = table.AddRow();
        rh.TopPadding = 2; rh.BottomPadding = 2;
        AddTestoCentrato(rh.Cells[0], "DESCRIZIONE PAGAMENTO", 8, bold: true, bg: GrigioHead);
        AddTestoCentrato(rh.Cells[1], "BANCA D'APPOGGIO",      8, bold: true, bg: GrigioHead);

        var rv = table.AddRow();
        rv.TopPadding = 3; rv.BottomPadding = 3;
        rv.VerticalAlignment = VerticalAlignment.Center;
        AddTestoCentrato(rv.Cells[0], _data.DescrizionePagamento ?? "—", 9, bold: false);
        AddTestoCentrato(rv.Cells[1], _data.DescrizioneBanca ?? "—",     9, bold: false);

        AddSpacer(section, 8);
    }

    // ── 4. Oggetto ────────────────────────────────────────────────────────────
    private void ComposeOggetto(Section section)
    {
        var p = section.AddParagraph();
        p.Format.SpaceAfter = 2;
        p.AddFormattedText("OGGETTO: ", TextFormat.Bold).Size = 10;
        var oggetto = string.IsNullOrWhiteSpace(_data.Testata.Oggetto)
            ? (_data.Testata.NotaSintetica ?? "")
            : _data.Testata.Oggetto!;
        var ft = p.AddFormattedText(oggetto, TextFormat.Bold);
        ft.Size = 10;

        // Stacco ampio prima della tabella righe, come nel modello dello studio:
        // l'oggetto resta "sospeso" e la sezione descrizione parte più in basso.
        AddSpacer(section, 24);
    }

    // ── 5. Righe: DESCRIZIONE ... IMPORTO  IVA ────────────────────────────────
    private void ComposeRighe(Section section)
    {
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(12.4));  // descrizione
        table.AddColumn(Unit.FromCentimeter(0.6));   // simbolo €
        table.AddColumn(Unit.FromCentimeter(3.2));   // importo (destra)
        table.AddColumn(Unit.FromCentimeter(1.8));   // IVA (destra)

        // Header con bordo inferiore.
        var rh = table.AddRow();
        rh.BottomPadding = 3;
        rh.Borders.Bottom = new Border { Color = Nero, Width = BordoPt, Visible = true };
        AddCellaTesto(rh.Cells[0], "DESCRIZIONE", 9, bold: true);
        AddCellaTesto(rh.Cells[2], "IMPORTO", 9, bold: true, alignRight: true);
        AddCellaTesto(rh.Cells[3], "IVA", 9, bold: true, alignRight: true);

        var iva = _data.Testata.AliquotaIva;

        foreach (var riga in _data.Righe.OrderBy(r => r.Ordine))
        {
            var r = table.AddRow();
            r.TopPadding = 3; r.BottomPadding = 3;
            r.Borders.Bottom = new Border { Color = GrigioBordo, Width = RigaBordoPt, Visible = true };
            r.VerticalAlignment = VerticalAlignment.Center;

            // "Tipo — Descrizione", ma senza ripetizione se coincidono o il tipo è assente.
            var etichetta = string.IsNullOrWhiteSpace(riga.Tipo)
                            || string.Equals(riga.Tipo, riga.Descrizione, StringComparison.OrdinalIgnoreCase)
                ? riga.Descrizione
                : $"{riga.Tipo} — {riga.Descrizione}";

            if (riga.IsDescrittiva)
            {
                // Solo testo: nessun importo/IVA, leggermente attenuato.
                var pd = r.Cells[0].AddParagraph(riga.Descrizione);
                pd.Format.Font.Size  = 9;
                pd.Format.Font.Color = GrigioScuro;
            }
            else
            {
                AddCellaTesto(r.Cells[0], etichetta, 9, bold: false);
                AddCellaTesto(r.Cells[1], "€", 9, bold: false, alignRight: true);
                AddCellaTesto(r.Cells[2], Eur(riga.Importo ?? 0m), 9, bold: false, alignRight: true);
                AddCellaTesto(r.Cells[3], Perc(iva), 9, bold: false, alignRight: true);
            }
        }

        AddSpacer(section, 10);
    }

    // ── 6. Cascata fiscale (allineata a destra, con segni +/−) ────────────────
    private void ComposeCascata(Section section)
    {
        var c = _data.Calcolo;

        // Le colonne "€", "importo" e "segno" ricalcano ESATTAMENTE le colonne
        // €/IMPORTO/IVA della sezione righe (ComposeRighe): stesse larghezze e
        // stessi offset, così simboli e cifre risultano incolonnati verticalmente
        // fra le due sezioni (allineamento "a foglio Excel"). L'indentazione delle
        // etichette è ottenuta spezzando la prima parte (indent 3.5 + etichetta 8.9)
        // che sommate valgono i 12.4 cm della colonna DESCRIZIONE sopra.
        var table = section.AddTable();
        table.Borders.Visible = false;
        table.AddColumn(Unit.FromCentimeter(3.5));   // indentazione
        table.AddColumn(Unit.FromCentimeter(8.9));   // etichetta  (3.5+8.9 = 12.4 = DESCRIZIONE)
        table.AddColumn(Unit.FromCentimeter(0.6));   // €          (= col € righe)
        table.AddColumn(Unit.FromCentimeter(3.2));   // importo    (= col IMPORTO righe, destra)
        table.AddColumn(Unit.FromCentimeter(1.8));   // segno      (= col IVA righe)

        // Cascata "da estratto conto": ogni valore che è il risultato di una
        // operazione con il rigo precedente (subtotale, TOTALE, TOTALE NOSTRO AVERE)
        // porta una linea di chiusura sopra l'importo, come nel modello dello studio.
        // Dal TOTALE IMPONIBILE in giù è tutto in grassetto (anche il subtotale).
        RigaCascata(table, "TOTALE IMPONIBILE", c.Imponibile, segno: null, bold: true);
        RigaCascata(table, $"Maggiorazione {Perc(_data.Testata.AliquotaCnpaia)}% C.N.P.A.I.A.",
            c.Cassa, segno: "+", bold: true);
        RigaCascata(table, "", c.ImponibilePiuCassa, segno: null, bold: true, lineaSopra: true);
        RigaCascata(table, $"I.V.A. {Perc(_data.Testata.AliquotaIva)}% su € {Eur(c.ImponibilePiuCassa)}",
            c.Iva, segno: "+", bold: true);
        RigaCascata(table, "TOTALE", c.Totale, segno: null, bold: true, lineaSopra: true);

        if (c.Ritenuta > 0)
            RigaCascata(table, $"Ritenuta Acconto {Perc(_data.Testata.AliquotaRitenuta)}% su € {Eur(c.Imponibile)}",
                c.Ritenuta, segno: "-", bold: true);

        if (c.SpeseArt15 > 0)
            RigaCascata(table, "Spese anticipate escluse art. 15", c.SpeseArt15, segno: "+", bold: true);

        // Riga di stacco prima del totale finale.
        var sep = table.AddRow();
        sep.Height = Unit.FromPoint(4);
        sep.HeightRule = RowHeightRule.Exactly;

        RigaCascata(table, "TOTALE NOSTRO AVERE S.E.& O.", c.TotaleNostroAvere, segno: null,
            bold: true, evidenzia: true, lineaSopra: true);

        AddSpacer(section, 12);
    }

    private void RigaCascata(Table table, string etichetta, decimal importo,
        string? segno, bool bold, bool evidenzia = false, bool lineaSopra = false)
    {
        var r = table.AddRow();
        r.TopPadding = 1.5; r.BottomPadding = 1.5;

        // Linea di chiusura sopra l'importo (convenzione contabile: separa gli
        // addendi dal risultato). Copre la colonna "€" + la colonna importo,
        // così la riga taglia esattamente sopra il valore come nel modello.
        if (lineaSopra)
        {
            // Le proprietà si impostano in-place: MigraDoc rifiuta la stessa
            // istanza Border condivisa fra due celle (va clonata), quindi niente
            // oggetto Border riusato.
            foreach (var cella in new[] { r.Cells[2], r.Cells[3] })
            {
                cella.Borders.Top.Color   = Nero;
                cella.Borders.Top.Width   = 0.5;
                cella.Borders.Top.Visible = true;
            }
        }

        if (!string.IsNullOrEmpty(etichetta))
        {
            var pl = r.Cells[1].AddParagraph(etichetta);
            pl.Format.Font.Bold = bold;
            pl.Format.Font.Size = evidenzia ? 10 : 9;
        }

        var pe = r.Cells[2].AddParagraph("€");
        pe.Format.Alignment = ParagraphAlignment.Right;
        pe.Format.Font.Bold = bold;
        pe.Format.Font.Size = evidenzia ? 10 : 9;

        var pv = r.Cells[3].AddParagraph(Eur(importo));
        pv.Format.Alignment = ParagraphAlignment.Right;
        pv.Format.Font.Bold = bold;
        pv.Format.Font.Size = evidenzia ? 10 : 9;

        if (!string.IsNullOrEmpty(segno))
        {
            var ps = r.Cells[4].AddParagraph(segno);
            ps.Format.Alignment = ParagraphAlignment.Center;
            ps.Format.Font.Bold = bold;
            ps.Format.Font.Size = evidenzia ? 10 : 9;
        }
    }

    // ── 7. Note finali: testo spese art.15 (se presente) + nota legale ────────
    private void ComposeNoteFinali(Section section)
    {
        var spese = _data.Testata.DescrizioneSpeseInAvviso;
        if (!string.IsNullOrWhiteSpace(spese) && _data.Calcolo.SpeseArt15 > 0)
        {
            var p = section.AddParagraph();
            p.Format.SpaceAfter = 6;
            p.Format.Font.Size = 8;
            p.AddFormattedText("Spese anticipate art. 15 D.P.R. 633/72: ", TextFormat.Italic);
            p.AddText(spese!);
        }

        if (IsFattura)
        {
            // Fattura di cortesia: blocco firma "PAGATO / studio" + banner esplicito
            // che il documento non ha valore fiscale (l'emissione vera è XML/SdI).
            AddSpacer(section, 8);
            var pPag = section.AddParagraph("PAGATO");
            pPag.Format.Alignment = ParagraphAlignment.Center;
            pPag.Format.Font.Bold = true;
            pPag.Format.Font.Size = 11;

            var pFirma = section.AddParagraph(_data.Studio.RagioneSociale);
            pFirma.Format.Alignment = ParagraphAlignment.Center;
            pFirma.Format.Font.Bold = true;
            pFirma.Format.Font.Size = 10;

            AddSpacer(section, 18);
            var banner = section.AddParagraph("------  DOCUMENTO NON VALIDO AI FINI FISCALI  ------");
            banner.Format.Alignment = ParagraphAlignment.Center;
            banner.Format.Font.Bold = true;
            banner.Format.Font.Size = 14;
            return;
        }

        var nota = section.AddParagraph(NotaLegale);
        nota.Format.Font.Bold = true;
        nota.Format.Font.Size = 7.5;
        nota.Format.SpaceBefore = 4;
    }

    // ── Helper di formattazione ───────────────────────────────────────────────
    private static void AddTestoCentrato(Cell cell, string testo, double size, bool bold, Color? bg = null)
    {
        if (bg.HasValue) cell.Shading.Color = bg.Value;
        cell.VerticalAlignment = VerticalAlignment.Center;
        var p = cell.AddParagraph(testo);
        p.Format.Alignment = ParagraphAlignment.Center;
        p.Format.Font.Bold = bold;
        p.Format.Font.Size = size;
    }

    private static void AddCellaTesto(Cell cell, string testo, double size, bool bold, bool alignRight = false)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        var p = cell.AddParagraph(testo);
        p.Format.Font.Bold = bold;
        p.Format.Font.Size = size;
        if (alignRight) p.Format.Alignment = ParagraphAlignment.Right;
    }

    // Spacer verticale affidabile (i paragrafi vuoti collassano): riga di tabella
    // con altezza esatta, come in VerbalePdfDocument.
    private static void AddSpacer(Section section, double pt)
    {
        var t = section.AddTable();
        t.Borders.Visible = false;
        t.AddColumn(Unit.FromCentimeter(ContentWidthCm));
        var r = t.AddRow();
        r.Height = Unit.FromPoint(pt);
        r.HeightRule = RowHeightRule.Exactly;
    }

    // ── Composizione stringhe di testata ──────────────────────────────────────
    private static string ComponiIndirizzoStudio(Azienda s)
    {
        var via = string.Join(" ", new[] { s.IndirizzoVia, s.IndirizzoCivico }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(via)) parti.Add(via);
        if (!string.IsNullOrWhiteSpace(s.IndirizzoCAP)) parti.Add(s.IndirizzoCAP!);
        var comune = s.IndirizzoComune;
        if (!string.IsNullOrWhiteSpace(comune))
        {
            if (!string.IsNullOrWhiteSpace(s.IndirizzoProvincia))
                comune += $" ({s.IndirizzoProvincia})";
            parti.Add(comune);
        }
        return string.Join(" - ", parti);
    }

    private static string ComponiRecapitiTelefonici(Azienda s)
    {
        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Telefono)) parti.Add($"Tel. {s.Telefono}");
        if (!string.IsNullOrWhiteSpace(s.Telefax))  parti.Add($"Fax {s.Telefax}");
        return string.Join(" - ", parti);
    }

    private static string ComponiMail(Azienda s)
    {
        var parti = new[] { s.Email, s.PEC }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return parti.Count == 0 ? "" : "Mail " + string.Join(" - ", parti);
    }

    private static string ComponiFiscaleStudio(Azienda s)
    {
        var piva = s.PIVA?.Trim();
        var cf   = s.CodiceFiscale?.Trim();
        if (!string.IsNullOrWhiteSpace(piva) && string.Equals(piva, cf, StringComparison.OrdinalIgnoreCase))
            return $"Codice Fiscale e Partita Iva: {piva}";

        var parti = new List<string>();
        if (!string.IsNullOrWhiteSpace(piva)) parti.Add($"Partita Iva: {piva}");
        if (!string.IsNullOrWhiteSpace(cf))   parti.Add($"Codice Fiscale: {cf}");
        return string.Join(" - ", parti);
    }

    private static string ComponiLocalitaCliente(Anagrafica c)
    {
        var parti = new[] { c.CAP, c.City }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var testo = string.Join(" ", parti);
        if (!string.IsNullOrWhiteSpace(c.Provincia))
            testo = string.IsNullOrWhiteSpace(testo) ? $"({c.Provincia})" : $"{testo} ({c.Provincia})";
        return testo;
    }
}
