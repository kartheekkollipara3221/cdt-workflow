"""Convert an Atlas platform spec (.md) into the house-style HTML report.
Usage: python md2report.py <in.md> <out.html> <style-source.html> "<eyebrow>" "<title>" "<lead>" "<kpis: n|label;n|label>"
"""
import sys, re, html as H

src, out, style_src, eyebrow, title, lead, kpis = sys.argv[1:8]
md = open(src, encoding='utf-8').read()
style = re.search(r'<style>.*?</style>', open(style_src, encoding='utf-8').read(), re.S).group(0)

def inline(t):
    t = H.escape(t, quote=False)
    t = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', r'<a href="\2">\1</a>', t)
    t = re.sub(r'`([^`]+)`', r'<code>\1</code>', t)
    t = re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', t)
    return t

SEV = {'High': ('b-high', ''), 'Medium': ('b-med', ' med'), 'Low': ('b-low', ' low')}

def blocks(lines):
    out, i = [], 0
    while i < len(lines):
        l = lines[i]
        if l.startswith('```'):
            j = i + 1; code = []
            while not lines[j].startswith('```'):
                code.append(lines[j]); j += 1
            out.append('<pre><code>' + H.escape('\n'.join(code), quote=False) + '</code></pre>'); i = j + 1; continue
        if l.startswith('|'):
            rows = []
            while i < len(lines) and lines[i].startswith('|'):
                rows.append(lines[i]); i += 1
            cells = lambda r: [c.strip() for c in r.strip().strip('|').split('|')]
            h = ''.join(f'<th>{inline(c)}</th>' for c in cells(rows[0]))
            body = '\n'.join('<tr>' + ''.join(f'<td>{inline(c)}</td>' for c in cells(r)) + '</tr>' for r in rows[2:])
            out.append(f'<div class="table-wrap"><table>\n<tr>{h}</tr>\n{body}\n</table></div>'); continue
        if l.startswith('- '):
            items = []
            while i < len(lines) and lines[i].startswith('- '):
                items.append(f'<li>{inline(lines[i][2:])}</li>'); i += 1
            out.append('<ul>\n' + '\n'.join(items) + '\n</ul>'); continue
        if re.match(r'\d+\. ', l):
            items = []
            while i < len(lines) and re.match(r'\d+\. ', lines[i]):
                items.append(f'<li>{inline(re.sub(r"^\d+\. ", "", lines[i]))}</li>'); i += 1
            out.append('<ol>\n' + '\n'.join(items) + '\n</ol>'); continue
        if l.strip() == '':
            i += 1; continue
        out.append(f'<p>{inline(l)}</p>'); i += 1
    return '\n'.join(out)

# split into ## sections
parts = re.split(r'^## ', md, flags=re.M)
head, sections = parts[0], parts[1:]
status = re.search(r'^Status: (.*)$', head, re.M)
html_sections = []
for sec in sections:
    heading, body = sec.split('\n', 1)
    sid = re.sub(r'[^a-z0-9]+', '-', heading.lower()).strip('-')
    # split by ### subsections
    subs = re.split(r'^### ', body, flags=re.M)
    content = [blocks(subs[0].split('\n'))]
    for sub in subs[1:]:
        h3, b = sub.split('\n', 1)
        m = re.match(r'([A-Z]{2}-\d+) (.*) \((High|Medium|Low)([^)]*)\)$', h3)
        if m:
            fid, ftitle, sev, rest = m.groups()
            cls, extra = SEV[sev]
            note = f' <span class="area">{inline(rest.strip(", "))}</span>' if rest.strip(', ') else ''
            content.append(f'<div class="issue{extra}">\n<h3><span class="badge {cls}">{fid}</span>{inline(ftitle)}{note}</h3>\n{blocks(b.split(chr(10)))}\n</div>')
        else:
            content.append(f'<h3>{inline(h3)}</h3>\n' + blocks(b.split('\n')))
    html_sections.append(f'<section id="{sid}">\n<h2>{inline(heading)}</h2>\n' + '\n'.join(content) + '\n</section>')

kpi_html = ''.join(f'<div class="kpi"><b>{H.escape(k.split("|")[0])}</b><span>{H.escape(k.split("|")[1])}</span></div>\n' for k in kpis.split(';'))

doc = f'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{H.escape(title)}</title>
{style}
</head>
<body>
<main>
<div class="eyebrow">{H.escape(eyebrow)}</div>
<h1>{H.escape(title)}</h1>
<p class="lead">{inline(lead)}</p>

<div class="note small">
<p><strong>Language.</strong> This report uses ASD-STE100 Simplified Technical English at approximately 80% compliance. Code names, JSON keys and file names stay as they are in the source.</p>
<p><strong>Ownership.</strong> The target team owns the CP Atlas code. This report gives requirements and a design to that team. This report does not change the workflow engine or the target code.</p>
<p><strong>Source.</strong> <code>target-implementation-read-only/</code>. Line numbers refer to these <code>.txt</code> copies.</p>
<p><strong>Status.</strong> {inline(status.group(1)) if status else ''}</p>
<p>Related: <a href="README.md">platform index</a> · <a href="01-section-master.html">module 1: Section Master</a> · <a href="../TARGET_ATLAS_ISSUES_BY_SEVERITY.html">issues by severity</a>.</p>
</div>

<div class="kpis">
{kpi_html}</div>

{chr(10).join(html_sections)}
</main>
</body>
</html>
'''
open(out, 'w', encoding='utf-8').write(doc)
print('ok', out)
