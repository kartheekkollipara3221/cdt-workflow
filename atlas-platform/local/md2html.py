import sys, re, html
src, ref, out, title = sys.argv[1:5]
eyebrow = sys.argv[5] if len(sys.argv) > 5 else 'CP Atlas platform · module 25'
css = re.search(r'<style>.*?</style>', open(ref, encoding='utf-8').read(), re.S).group(0)
lines = open(src, encoding='utf-8').read().split('\n')

def inline(t):
    codes = []
    def keep(m):
        codes.append('<code>' + html.escape(m.group(1)) + '</code>'); return '\x00%d\x00' % (len(codes) - 1)
    t = re.sub(r'`([^`]+)`', keep, t)
    t = html.escape(t, quote=False)
    t = re.sub(r'\*\*(.+?)\*\*', r'<strong>\1</strong>', t)
    def link(m):
        href = m.group(2)
        if href.endswith('.md') and not href.startswith('..'):
            href = href[:-3] + '.html'
        return '<a href="%s">%s</a>' % (href, m.group(1))
    t = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, t)
    return re.sub(r'\x00(\d+)\x00', lambda m: codes[int(m.group(1))], t)

def cells(row):
    return [c.strip() for c in row.strip().strip('|').split('|')]

o, i, n, open_sec = [], 0, len(lines), False
while i < n:
    l = lines[i]
    if l.startswith('```'):
        i += 1; buf = []
        while not lines[i].startswith('```'):
            buf.append(lines[i]); i += 1
        o.append('<pre><code>' + html.escape('\n'.join(buf)) + '</code></pre>'); i += 1; continue
    m = re.match(r'(#{1,3}) (.*)', l)
    if m:
        lvl = len(m.group(1))
        if lvl == 1:
            o.append('<div class="eyebrow">%s</div><h1>%s</h1>' % (eyebrow, inline(m.group(2))))
        else:
            if lvl == 2:
                if open_sec: o.append('</section>')
                o.append('<section>'); open_sec = True
            o.append('<h%d>%s</h%d>' % (lvl, inline(m.group(2)), lvl))
        i += 1; continue
    if l.startswith('|') and i + 1 < n and re.match(r'\|[\s:|-]+\|$', lines[i + 1].strip()):
        o.append('<div class="table-wrap"><table><thead><tr>' + ''.join('<th>%s</th>' % inline(c) for c in cells(l)) + '</tr></thead><tbody>')
        i += 2
        while i < n and lines[i].startswith('|'):
            o.append('<tr>' + ''.join('<td>%s</td>' % inline(c) for c in cells(lines[i])) + '</tr>'); i += 1
        o.append('</tbody></table></div>'); continue
    m = re.match(r'(- |\d+\. )', l)
    if m:
        tag = 'ul' if m.group(1) == '- ' else 'ol'
        items = []
        while i < n and (re.match(r'(- |\d+\. )', lines[i]) or (lines[i].startswith('  ') and lines[i].strip())):
            if re.match(r'(- |\d+\. )', lines[i]):
                items.append(re.sub(r'^(- |\d+\. )', '', lines[i]))
            else:
                items[-1] += ' ' + lines[i].strip()
            i += 1
        o.append('<%s>%s</%s>' % (tag, ''.join('<li>%s</li>' % inline(x) for x in items), tag)); continue
    if not l.strip():
        i += 1; continue
    buf = []
    while i < n and lines[i].strip() and not re.match(r'(#{1,3} |```|\||- |\d+\. )', lines[i]):
        buf.append(lines[i].strip()); i += 1
    o.append('<p>%s</p>' % inline(' '.join(buf)))
if open_sec: o.append('</section>')
doc = '<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n<title>%s</title>\n%s\n</head>\n<body>\n<main>\n%s\n</main>\n</body>\n</html>\n' % (title, css, '\n'.join(o))
open(out, 'w', encoding='utf-8', newline='\n').write(doc)
print(len(doc), 'bytes;', doc.count('<table>'), 'tables;', doc.count('<pre>'), 'code blocks;', doc.count('<section>'), 'sections')
