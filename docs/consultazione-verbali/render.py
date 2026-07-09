#!/usr/bin/env python3
"""
Render del template di documentazione in PDF.

Uso:
    python render.py dati.json [output.pdf] [template.html]

- Il template puo' essere indicato come 3o argomento, oppure con la chiave
  "TEMPLATE" nel JSON; in mancanza si usa template-documentazione.html.
- Sostituisce i segnaposto {{CHIAVE}} nel template con i valori del JSON.
- I loghi (LOGO_*_SRC) vengono incorporati come data URI: il PDF resta
  autonomo e non dipende da file esterni.
- Aggiunge testatina e pie' di pagina ripetuti (con numero di pagina),
  nascosti automaticamente sulla copertina. Il logo in testata e' quello del
  committente se presente, altrimenti quello dell'agenzia (documenti solo brand).
"""

import sys, json, base64, mimetypes, pathlib
from playwright.sync_api import sync_playwright

BASE = pathlib.Path(__file__).parent
TEMPLATE = BASE / "template-documentazione.html"


def to_data_uri(path: str) -> str:
    """Converte un file immagine (anche SVG) in data URI base64."""
    p = (BASE / path) if not pathlib.Path(path).is_absolute() else pathlib.Path(path)
    mime = mimetypes.guess_type(str(p))[0] or "image/svg+xml"
    data = base64.b64encode(p.read_bytes()).decode("ascii")
    return f"data:{mime};base64,{data}"


def fill(template: str, data: dict) -> str:
    for key, value in data.items():
        template = template.replace("{{" + key + "}}", str(value))
    return template


def header_footer(data):
    src = data.get("LOGO_CLIENTE_SRC") or data.get("LOGO_AGENZIA_SRC", "")
    logo = src if src.startswith("data:") else to_data_uri(src)
    common = "font-family:'Segoe UI',Arial,sans-serif;color:#6B7785;width:100%;"
    header = f"""
      <div style="{common}font-size:7pt;padding:6mm 18mm 0;display:flex;
                  justify-content:space-between;align-items:center;
                  border-bottom:0.4pt solid #DCE2E8;padding-bottom:2mm;">
        <img src="{logo}" style="height:7mm;max-width:45mm;object-fit:contain;">
        <span>{data['DOC_TITOLO']} — v{data['DOC_VERSIONE']}</span>
      </div>"""
    colophon = data["AGENZIA_NOME"]
    colophon += f" · {data['DOC_CLASSIFICAZIONE']}"
    footer = f"""
      <div style="{common}font-size:7pt;padding:0 18mm 6mm;display:flex;
                  justify-content:space-between;align-items:center;
                  border-top:0.4pt solid #DCE2E8;padding-top:2mm;">
        <span>{colophon}</span>
        <span style="white-space:nowrap;padding-left:6mm;">Pag. <span class="pageNumber"></span> di <span class="totalPages"></span></span>
      </div>"""
    return header, footer


def main():
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)

    data = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
    out = sys.argv[2] if len(sys.argv) > 2 else "documentazione.pdf"

    # scelta del template: 3o argomento > chiave "TEMPLATE" nel JSON > default
    tpl_name = sys.argv[3] if len(sys.argv) > 3 else data.get("TEMPLATE")
    template_path = (BASE / tpl_name) if tpl_name else TEMPLATE

    # i loghi diventano data URI prima della sostituzione
    for k in ("LOGO_CLIENTE_SRC", "LOGO_AGENZIA_SRC"):
        if k in data:
            data[k] = to_data_uri(data[k])

    full = fill(template_path.read_text(encoding="utf-8"), data)
    header, footer = header_footer(data)

    # split: la copertina e' tutto cio' che precede la sezione "INFO DOCUMENTO"
    marker = "<!-- ===================== INFO DOCUMENTO ===================== -->"
    head, after_body = full.split("<body>", 1)
    cover_part, rest_part = after_body.split(marker, 1)

    cover_doc = f"{head}<body>{cover_part}</body></html>"
    # nel corpo annullo il margine 0 della prima pagina (serviva solo alla copertina)
    body_head = head + "<style>@page:first{margin:24mm 18mm 20mm 18mm;}</style>"
    body_doc = f"{body_head}<body>{rest_part}"
    if not body_doc.rstrip().endswith("</html>"):
        body_doc += "</body></html>"

    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page()

        page.set_content(cover_doc, wait_until="networkidle")
        page.pdf(path="_cover.pdf", format="A4", print_background=True,
                 prefer_css_page_size=True)

        page.set_content(body_doc, wait_until="networkidle")
        page.pdf(path="_body.pdf", format="A4", print_background=True,
                 prefer_css_page_size=True, display_header_footer=True,
                 header_template=header, footer_template=footer)
        browser.close()

    # unisce copertina + corpo
    from pypdf import PdfWriter
    w = PdfWriter()
    w.append("_cover.pdf"); w.append("_body.pdf")
    with open(out, "wb") as f:
        w.write(f)
    for tmp in ("_cover.pdf", "_body.pdf"):
        pathlib.Path(tmp).unlink(missing_ok=True)
    print(f"OK -> {out}")


if __name__ == "__main__":
    main()
