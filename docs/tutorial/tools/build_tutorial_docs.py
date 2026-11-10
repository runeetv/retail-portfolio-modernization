"""Render the tutorial Markdown into a styled Word document.

Usage (from the repository root):
    python docs/tutorial/tools/build_tutorial_docs.py
    powershell -File docs/tutorial/tools/export_pdf.ps1     # Word -> PDF (needs Microsoft Word)

Requires: pip install python-docx
Supports the Markdown subset used by the tutorial: headings, paragraphs, bullet/numbered/check
lists, fenced code blocks (also indented inside list items), pipe tables, blockquote callouts,
inline **bold**, *italic*, `code`, and a <!-- pagebreak --> marker.
"""
import os
import re

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "Kiro-Legacy-Modernization-Tutorial.md")
OUT = os.path.join(HERE, "..", "Kiro-Legacy-Modernization-Tutorial.docx")

ACCENT = RGBColor(0x23, 0x2F, 0x3E)      # dark navy
KIRO = RGBColor(0x79, 0x3C, 0xD8)        # purple accent for Kiro callouts
CODE_BG = "F3F4F6"
NOTE_BG = "EEF4FB"
WARN_BG = "FDF3E7"
KIRO_BG = "F3ECFC"
HEADER_BG = "232F3E"


# ---------------------------------------------------------------- helpers
def shade(cell, hex_fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_fill)
    tc_pr.append(shd)


def cell_borders(cell, color="D0D5DD", left_color=None, left_size=None):
    tc_pr = cell._tc.get_or_add_tcPr()
    borders = OxmlElement("w:tcBorders")
    for edge in ("top", "left", "bottom", "right"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "single")
        if edge == "left" and left_color:
            el.set(qn("w:sz"), str(left_size or 24))
            el.set(qn("w:color"), left_color)
        else:
            el.set(qn("w:sz"), "4")
            el.set(qn("w:color"), color)
        borders.append(el)
    tc_pr.append(borders)


def set_cell_margins(cell, cm=0.2):
    tc_pr = cell._tc.get_or_add_tcPr()
    mar = OxmlElement("w:tcMar")
    for edge in ("top", "left", "bottom", "right"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:w"), str(int(cm * 567)))
        el.set(qn("w:type"), "dxa")
        mar.append(el)
    tc_pr.append(mar)


def repeat_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    el = OxmlElement("w:tblHeader")
    el.set(qn("w:val"), "true")
    tr_pr.append(el)


def no_split(row):
    tr_pr = row._tr.get_or_add_trPr()
    el = OxmlElement("w:cantSplit")
    el.set(qn("w:val"), "true")
    tr_pr.append(el)


INLINE = re.compile(r"(\*\*[^*]+\*\*|`[^`]+`|\*[^*\s][^*]*\*|https?://[^\s)|]+)")


def add_inline(par, text, size=None, color=None, bold=False):
    for part in INLINE.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**"):
            run = par.add_run(part[2:-2])
            run.bold = True
        elif part.startswith("`") and part.endswith("`"):
            run = par.add_run(part[1:-1])
            run.font.name = "Consolas"
            run._element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
            run.font.color.rgb = RGBColor(0x1F, 0x4E, 0x79)
            if size:
                run.font.size = Pt(size - 0.5)
        elif part.startswith("*") and part.endswith("*") and len(part) > 2:
            run = par.add_run(part[1:-1])
            run.italic = True
        elif part.startswith("http"):
            run = par.add_run(part)
            run.font.color.rgb = RGBColor(0x0B, 0x5C, 0xAD)
            run.underline = True
        else:
            run = par.add_run(part)
        if bold:
            run.bold = True
        if size and not run.font.size:
            run.font.size = Pt(size)
        if color is not None and run.font.color.rgb is None:
            run.font.color.rgb = color
    return par


def spacing(par, before=0, after=4, line=None):
    pf = par.paragraph_format
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    if line:
        pf.line_spacing = line


# ---------------------------------------------------------------- blocks
def code_block(doc, lines, lang=""):
    table = doc.add_table(rows=1, cols=1)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = table.rows[0].cells[0]
    shade(cell, CODE_BG)
    cell_borders(cell, color="D0D5DD", left_color="9AA4B2", left_size=18)
    set_cell_margins(cell, 0.25)
    par = cell.paragraphs[0]
    if lang:
        tag = par.add_run(lang.upper() + "\n")
        tag.font.size = Pt(7)
        tag.font.color.rgb = RGBColor(0x6B, 0x72, 0x80)
        tag.bold = True
    run = par.add_run("\n".join(lines))
    run.font.name = "Consolas"
    run._element.rPr.rFonts.set(qn("w:eastAsia"), "Consolas")
    run.font.size = Pt(8.5)
    spacing(par, 0, 0, 1.0)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


def callout(doc, text):
    upper = text.upper()
    if "KIRO IS THE OPERATIVE TOOL" in upper or upper.startswith("**IMPORTANT"):
        bg, bar, title_color = KIRO_BG, "793CD8", KIRO
    elif upper.startswith("**WARNING"):
        bg, bar, title_color = WARN_BG, "D97706", RGBColor(0x9A, 0x4D, 0x00)
    else:
        bg, bar, title_color = NOTE_BG, "2F6FB5", ACCENT
    table = doc.add_table(rows=1, cols=1)
    cell = table.rows[0].cells[0]
    shade(cell, bg)
    cell_borders(cell, color=bg, left_color=bar, left_size=36)
    set_cell_margins(cell, 0.3)
    par = cell.paragraphs[0]
    add_inline(par, text, size=10, color=None)
    spacing(par, 0, 0)
    for run in par.runs:
        if run.bold:
            run.font.color.rgb = title_color
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


def md_table(doc, rows):
    header = [c.strip() for c in rows[0].strip().strip("|").split("|")]
    body = [[c.strip() for c in r.strip().strip("|").split("|")] for r in rows[2:]]
    table = doc.add_table(rows=1 + len(body), cols=len(header))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = True
    for i, h in enumerate(header):
        cell = table.rows[0].cells[i]
        shade(cell, HEADER_BG)
        cell_borders(cell, color=HEADER_BG)
        set_cell_margins(cell, 0.12)
        par = cell.paragraphs[0]
        add_inline(par, h, size=9, color=RGBColor(0xFF, 0xFF, 0xFF), bold=True)
        for run in par.runs:
            run.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
        spacing(par, 0, 0)
    repeat_header(table.rows[0])
    for r, row in enumerate(body, start=1):
        no_split(table.rows[r])
        for c in range(len(header)):
            cell = table.rows[r].cells[c]
            cell_borders(cell)
            set_cell_margins(cell, 0.12)
            if r % 2 == 0:
                shade(cell, "F7F8FA")
            par = cell.paragraphs[0]
            add_inline(par, row[c] if c < len(row) else "", size=9)
            spacing(par, 0, 0)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


# ---------------------------------------------------------------- document
def setup(doc):
    sec = doc.sections[0]
    sec.page_width, sec.page_height = Cm(21.0), Cm(29.7)
    for side in ("left_margin", "right_margin"):
        setattr(sec, side, Cm(2.2))
    sec.top_margin, sec.bottom_margin = Cm(2.0), Cm(2.0)

    normal = doc.styles["Normal"]
    normal.font.name = "Calibri"
    normal._element.rPr.rFonts.set(qn("w:eastAsia"), "Calibri")
    normal.font.size = Pt(10.5)
    for name, size, color in (("Heading 1", 18, ACCENT), ("Heading 2", 13.5, ACCENT), ("Heading 3", 11.5, ACCENT)):
        st = doc.styles[name]
        st.font.name = "Calibri"
        st._element.rPr.rFonts.set(qn("w:eastAsia"), "Calibri")
        st.font.size = Pt(size)
        st.font.bold = True
        st.font.color.rgb = color
        st.paragraph_format.space_before = Pt(14 if name == "Heading 1" else 10)
        st.paragraph_format.space_after = Pt(6 if name == "Heading 1" else 4)
        st.paragraph_format.keep_with_next = True


def header_footer(section, title):
    section.different_first_page_header_footer = True
    hp = section.header.paragraphs[0]
    hp.text = ""
    r = hp.add_run(title)
    r.font.size = Pt(8)
    r.font.color.rgb = RGBColor(0x6B, 0x72, 0x80)
    hp.alignment = WD_ALIGN_PARAGRAPH.RIGHT

    fp = section.footer.paragraphs[0]
    fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = fp.add_run("Powered by Kiro  |  Page ")
    r.font.size = Pt(8)
    r.font.color.rgb = RGBColor(0x6B, 0x72, 0x80)
    for code in ("begin", None, "end"):
        if code is None:
            instr = OxmlElement("w:instrText")
            instr.set(qn("xml:space"), "preserve")
            instr.text = "PAGE"
            run = fp.add_run()
            run.font.size = Pt(8)
            run._r.append(instr)
        else:
            fld = OxmlElement("w:fldChar")
            fld.set(qn("w:fldCharType"), code)
            run = fp.add_run()
            run._r.append(fld)


def toc(doc):
    h = doc.add_paragraph()
    run = h.add_run("Contents")
    run.bold = True
    run.font.size = Pt(18)
    run.font.color.rgb = ACCENT
    par = doc.add_paragraph()
    for kind, text in (("begin", None), ("instr", 'TOC \\o "1-2" \\h \\z \\u'), ("separate", None),
                       ("text", "Right-click and choose Update Field to build the table of contents."),
                       ("end", None)):
        run = par.add_run()
        if kind == "instr":
            el = OxmlElement("w:instrText")
            el.set(qn("xml:space"), "preserve")
            el.text = text
            run._r.append(el)
        elif kind == "text":
            run.text = text
        else:
            el = OxmlElement("w:fldChar")
            el.set(qn("w:fldCharType"), kind)
            run._r.append(el)


def title_page(doc, title, subtitle):
    for _ in range(6):
        doc.add_paragraph()
    bar = doc.add_paragraph()
    r = bar.add_run("HANDS-ON TUTORIAL")
    r.bold = True
    r.font.size = Pt(11)
    r.font.color.rgb = KIRO
    t = doc.add_paragraph()
    r = t.add_run(title.replace("Hands-On Tutorial: ", ""))
    r.bold = True
    r.font.size = Pt(28)
    r.font.color.rgb = ACCENT
    spacing(t, 4, 12, 1.05)
    s = doc.add_paragraph()
    add_inline(s, subtitle, size=12.5, color=RGBColor(0x37, 0x41, 0x51))
    spacing(s, 0, 30)
    k = doc.add_table(rows=1, cols=1)
    cell = k.rows[0].cells[0]
    shade(cell, KIRO_BG)
    cell_borders(cell, color=KIRO_BG, left_color="793CD8", left_size=36)
    set_cell_margins(cell, 0.35)
    p = cell.paragraphs[0]
    add_inline(p, "**Required tool: Kiro** (https://kiro.dev). Every stage in this tutorial is executed "
                  "by the Kiro AI agent. Install Kiro and sign in before you begin.", size=11)
    for run in p.runs:
        if run.bold:
            run.font.color.rgb = KIRO
    for _ in range(8):
        doc.add_paragraph()
    m = doc.add_paragraph()
    add_inline(m, "Legacy stack: .NET 10 WPF desktop, ASP.NET Core MVC, ASP.NET Core Web API, SQL Server\n"
                  "Target: ASP.NET Core .NET 10 modular monolith (UnifiedRetail)\n"
                  "Companion repository: retail-portfolio-modernization",
               size=9.5, color=RGBColor(0x6B, 0x72, 0x80))
    doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)


def render(md_lines, doc):
    i = 0
    n = len(md_lines)
    para_buf = []

    def flush():
        if para_buf:
            p = doc.add_paragraph()
            add_inline(p, " ".join(para_buf))
            spacing(p, 0, 6, 1.1)
            para_buf.clear()

    while i < n:
        raw = md_lines[i]
        line = raw.rstrip("\n")
        stripped = line.strip()

        if stripped == "<!-- pagebreak -->":
            flush()
            doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)
            i += 1
            continue
        if stripped.startswith("```"):
            flush()
            indent = len(line) - len(line.lstrip())
            lang = stripped[3:].strip()
            i += 1
            block = []
            while i < n and not md_lines[i].strip().startswith("```"):
                block.append(md_lines[i].rstrip("\n")[indent:] if md_lines[i][:indent].strip() == "" else md_lines[i].rstrip("\n"))
                i += 1
            i += 1
            code_block(doc, block, lang)
            continue
        if stripped == "---":
            flush()
            i += 1
            continue
        m = re.match(r"^(#{2,4})\s+(.*)$", stripped)
        if m:
            flush()
            level = len(m.group(1)) - 1
            doc.add_heading(m.group(2), level=level)
            i += 1
            continue
        if stripped.startswith("|") and i + 1 < n and re.match(r"^\s*\|[\s:|-]+\|\s*$", md_lines[i + 1]):
            flush()
            rows = []
            while i < n and md_lines[i].strip().startswith("|"):
                rows.append(md_lines[i].strip())
                i += 1
            md_table(doc, rows)
            continue
        if stripped.startswith(">"):
            flush()
            quote = []
            while i < n and md_lines[i].strip().startswith(">"):
                quote.append(md_lines[i].strip()[1:].strip())
                i += 1
            callout(doc, " ".join(q for q in quote if q))
            continue
        m = re.match(r"^(\s*)- \[ \]\s+(.*)$", line)
        if m:
            flush()
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.6)
            p.paragraph_format.first_line_indent = Cm(-0.5)
            r = p.add_run("☐  ")
            r.font.size = Pt(11)
            add_inline(p, m.group(2))
            spacing(p, 0, 3)
            i += 1
            continue
        m = re.match(r"^(\s*)- (.*)$", line)
        if m:
            flush()
            p = doc.add_paragraph(style="List Bullet" if not m.group(1) else "List Bullet 2")
            add_inline(p, m.group(2))
            spacing(p, 0, 3)
            i += 1
            continue
        m = re.match(r"^(\d+)\.\s+(.*)$", stripped)
        if m and not line.startswith(" "):
            flush()
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.75)
            p.paragraph_format.first_line_indent = Cm(-0.55)
            r = p.add_run(f"{m.group(1)}.  ")
            r.bold = True
            r.font.color.rgb = ACCENT
            add_inline(p, m.group(2))
            spacing(p, 0, 4)
            i += 1
            continue
        if not stripped:
            flush()
            i += 1
            continue
        # indented continuation paragraph inside a list item
        if line.startswith("   ") and not para_buf:
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.75)
            add_inline(p, stripped)
            spacing(p, 0, 4)
            i += 1
            continue
        if stripped.startswith("*") and stripped.endswith("*") and stripped.count("*") == 2:
            flush()
            p = doc.add_paragraph()
            add_inline(p, stripped, size=8.5, color=RGBColor(0x6B, 0x72, 0x80))
            i += 1
            continue
        para_buf.append(stripped)
        i += 1
    flush()


def main():
    with open(SRC, encoding="utf-8") as fh:
        lines = fh.readlines()
    title = lines[0].lstrip("# ").strip()
    # subtitle = first non-empty line after the title
    j = 1
    while not lines[j].strip():
        j += 1
    subtitle = lines[j].strip()
    body = lines[j + 1:]
    # drop the first page break (title page provides its own)
    for k, l in enumerate(body):
        if l.strip() == "<!-- pagebreak -->":
            body = body[k + 1:]
            break

    doc = Document()
    setup(doc)
    core = doc.core_properties
    core.title = title
    core.subject = "AI-assisted legacy modernization with Kiro"
    core.keywords = "Kiro, modernization, reverse engineering, .NET, ASP.NET Core"
    title_page(doc, title, subtitle)
    toc(doc)
    doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)
    render(body, doc)
    header_footer(doc.sections[0], "AI-Assisted Legacy Modernization with Kiro")
    doc.save(OUT)
    print("Wrote", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
