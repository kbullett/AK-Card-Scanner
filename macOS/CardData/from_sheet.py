# Builds macOS/CardData/cards.json from a text export of the AK Barcode Preservation Project
# spreadsheet. Usage: python3 from_sheet.py <export.txt> <cards.json>
import csv, io, json, re, sys, collections
src = open(sys.argv[1], encoding='utf-8', newline='').read().replace('\r\n','\n').replace('\r','\n')
tabs = {}
for block in src.split('## Sheet name: ')[1:]:
    name, _, body = block.partition('\n'); tabs[name.strip()] = list(csv.reader(io.StringIO(body)))
VER = {'Promotional':'Promo', **{f'Version {i}':f'Ver {i}' for i in range(1,7)}, **{f'Evo {i}':f'Evo {i}' for i in range(1,9)}, 'Evo 8DX':'Evo 8DX'}
def num(n):
    n = n.strip().upper().replace('-', '').replace(' ', '')
    return n if re.fullmatch(r'[ASM]\d{3}[A-Z]*', n) else None
def rar(r):
    r = r.strip().replace(' Rare', '').strip()
    return {'Ultra Kaiser':'Kaiser','Kaiser Rare':'Kaiser'}.get(r, r) or 'Normal'
def code(c): return re.sub(r'\s+', '', c)
def i(x):
    x = x.replace(',', '').strip()
    return int(x) if x.isdigit() else None
cards, notes = {}, collections.Counter()
def add(rec, ver):
    c = rec.get('c', '')
    if not rec.get('n') or not c.startswith('bn') or len(c) != 8:
        notes['skipped (no number or bad barcode)'] += 1; return
    if c in cards:
        old = cards[c]
        if ver and not old.get('v'): old['v'] = ver
        for k, v in rec.items(): old.setdefault(k, v)
        return
    if ver: rec['v'] = ver
    cards[c] = rec
for tab, ver in VER.items():
    rows = tabs.get(tab, [])
    hdr_i = next((k for k, r in enumerate(rows) if r and r[0].strip().lower() == 'number'), None)
    if hdr_i is None: continue
    hdr = [h.strip() for h in rows[hdr_i]]
    starts = [k for k, h in enumerate(hdr) if h.lower() == 'number'] + [len(hdr)]
    for r in rows[hdr_i+1:]:
        r = r + [''] * (len(hdr) - len(r))
        for a, b in zip(starts, starts[1:]):
            g = dict(zip(hdr[a:b], r[a:b]))
            n = num(g.get('Number', ''))
            if not n and not g.get('Code'): continue
            t = (g.get('Card Type', '').strip()[:1] or (n or ' ')[0]).upper()
            rec = {'t': t, 'n': n, 'nm': g.get('Card Name', '').strip(), 'r': rar(g.get('Rarity', '')), 'c': code(g.get('Code', ''))}
            if t == 'A':
                rec.update({'str': i(g.get('Power', '')), 'hp': i(g.get('Health', '')), 'k': g.get('Match', '').strip() or None,
                            'atk': {'G':'Guts','T':'Tech','P':'Power'}.get(g.get('Gold Move', '').strip())})
            elif t == 'S':
                au, lv = g.get('Aura', '').strip(), g.get('Lv', '').strip()
                rec.update({'g': i(g.get('G', '')), 'te': i(g.get('T', '')), 'p': i(g.get('P', '')),
                            'au': (f'{au} {lv}' if au not in ('', '-') else None)})
            else:
                rec.update({'mp': i(g.get('Power', '')), 'e': g.get('Miracle Effect', '').strip() or None, 'k': g.get('Match', '').strip() or None})
            add({k: v for k, v in rec.items() if v not in (None, '')}, ver)
for tab in ('ANIMAL EXPORT SHEET', 'STRONG EXPORT SHEET', 'MIRACLE EXPORT SHEET'):
    rows = tabs[tab]; hdr = [h.strip().lower() for h in rows[0]]
    for r in rows[1:]:
        g = dict(zip(hdr, r)); n = num(g.get('number', ''))
        rec = {'t': (g.get('type', '') or 'X')[0].upper(), 'n': n, 'nm': g.get('name', '').strip(), 'r': rar(g.get('rarity', '')), 'c': code(g.get('code', ''))}
        add({k: v for k, v in rec.items() if v}, None)
out = sorted(cards.values(), key=lambda c: ('ASM'.index(c['t']), c['n'], c['c']))
open(sys.argv[2], 'w', encoding='utf-8').write('[\n' + ',\n'.join(json.dumps(c, ensure_ascii=False, separators=(',', ':')) for c in out) + '\n]\n')
print(len(out), collections.Counter(c['t'] for c in out), dict(notes))
print('versions', collections.Counter(c.get('v') for c in out))
print('rarity', collections.Counter(c['r'] for c in out))
