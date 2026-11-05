# Baut das reveal.js-Deck (zielbild.html) aus der Scroll-Seite (zielbild-seite.html): Kapitel horizontal, Details vertikal.
# Aufruf: python3 docs/praesentation/baue_deck.py [ziel.html [artifact-fassung.html]]
#   vollständige Geschichte: VORLAGE=geschichte_full.html python3 docs/praesentation/baue_deck.py docs/praesentation/zielbild_full.html
# Die Seite ist die Quelle: Inhalte dort ändern, dann das Deck neu bauen. reveal.min.css und reveal.min.js (5.1.0, MIT) werden eingebettet;
# Schriften aus schriften.css (eingebettet) – das Deck läuft vollständig ohne Netz.
import re, sys, os

HIER = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HIER, 'zielbild-seite.html')).read()
reveal_css = open(os.path.join(HIER, 'reveal.min.css')).read()
reveal_js = open(os.path.join(HIER, 'reveal.min.js')).read()
schriften = open(os.path.join(HIER, 'schriften.css')).read()


def block(marker, start=0, s=src):
    """Ausgewogener Block ab der ersten Fundstelle von marker (Tag am Anfang des Markers)."""
    i = s.index(marker, start)
    tag = re.match(r'<(\w+)', s[i:]).group(1)
    depth, pos = 0, i
    pat = re.compile(r'<(/?)' + tag + r'\b[^>]*?(/?)>')
    for m in pat.finditer(s, i):
        if m.group(2) == '/':
            continue
        depth += -1 if m.group(1) else 1
        if depth == 0:
            return s[i:m.end()], m.end()
    raise ValueError(marker)


def alle(marker):
    out, pos = [], 0
    while True:
        try:
            b, pos = block(marker, pos)
        except ValueError:
            return out
        out.append(b)


def section_of(sid):
    i = src.index(f'<section id="{sid}"')
    return src[i:src.index('</section>', i)]


css = src[src.index('<style>') + 7:src.index('</style>')]
script = src[src.index('<script>') + 8:src.rindex('</script>')]
script = script[:script.index('  // ── Blättern')] + '\n  window.__layout = layout;\n})();\n'
script = script.replace('canvases.forEach(function(c){\n      var g=c._g,w=c._w,svg=c._svg;',
                        'canvases.forEach(function(c){\n      if(!c.parentElement.clientWidth)return;\n      var g=c._g,w=c._w,svg=c._svg;')
assert 'if(!c.parentElement.clientWidth)return;' in script

# ── Bausteine aus der Seite
sig, _ = block('<div class="sig">')
maplist, _ = block('<ul class="maplist">')
ue = section_of('ueberblick')
ue_svg, _ = block('<svg class="flow"', s=ue)
ue_kern, _ = block('<div class="logbox">', s=ue)
msgs, _ = block('<div class="msgs">')
deps = alle('<div class="deps">')
protos = alle('<div class="proto">')
allg, _ = block('<div class="scroller"><div class="canvas" data-graph="allg"')
edges, _ = block('<ul class="edges">')
els = alle('<article class="el">')
ip, _ = block('<div class="scroller"><div class="canvas" data-graph="ip"')
ob = section_of('oberflaeche')
ideen, _ = block('<div class="points three ideen">', s=ob)
wf_i = ob.index('<div class="wf"')
wf, _ = block('<div class="wf"', s=ob)
wf_hl, _ = block('<div class="wf-hl">', s=ob)
slots, _ = block('<div class="slots">', s=ob)
uimod, _ = block('<pre class="code"><code>public interface IUiModule', s=ob)
treppe, _ = block('<div class="treppe">', s=ob)
ui3, _ = block('<div class="ui3">', s=ob)
keys, _ = block('<div class="keys">', s=ob)
modi, _ = block('<div class="modi">', s=ob)
mo = section_of('modul')
parts, _ = block('<div class="stage parts">', s=mo)
mo_svg, _ = block('<svg class="flow"', s=mo)
zs = section_of('zusammenspiel')
zs_svg, _ = block('<svg class="flow"', s=zs)
zs_pts, _ = block('<div class="points three">', s=zs)
ll = section_of('llm')
ll_svg, _ = block('<svg class="flow"', s=ll)
ll_pts, _ = block('<div class="points">', s=ll)
ll_pair, _ = block('<div class="pair">', s=ll)
ll_cli, _ = block('<p class="cli">', s=ll)

assert len(protos) == 11, len(protos)
assert len(els) == 12, len(els)
P = dict(zip(['agg', 'prj', 'rdr', 'fn', 'src', 'store', 'dn', 'akt', 'cli', 'uhr', 'vt'], protos))


# ── Die Geschichte (geschichte.html) mit den Bausteinen füllen
teile = {
    'sig': sig, 'maplist': maplist, 'ueberblick_svg': ue_svg, 'msgs': msgs, 'deps': ''.join(deps),
    'allg': allg, 'ip': ip, 'wf': wf, 'wf_hl': wf_hl, 'ideen': ideen, 'slots': slots, 'treppe': treppe,
    'ui3': ui3, 'keys': keys, 'modi': modi, 'modul_svg': mo_svg, 'zs_svg': zs_svg, 'llm_svg': ll_svg,
    'llm_pair': ll_pair, 'llm_cli': ll_cli,
}
for k, v in P.items():
    teile['proto:' + k] = v
for n, e in enumerate(els):
    teile[f'el_code:{n}'] = block('<pre class="code">', s=e)[0] if '<pre class="code">' in e else ''

vorlage = os.environ.get('VORLAGE', 'geschichte.html')
geschichte = open(os.path.join(HIER, vorlage)).read()
geschichte = re.sub(r'<!--.*?-->', '', geschichte, flags=re.S)
def ersetze(m):
    if m.group(1) not in teile:
        raise KeyError(m.group(1))
    return teile[m.group(1)]
folien = re.sub(r'\{\{([a-z0-9_]+(?::[a-z0-9_]+)?)\}\}', ersetze, geschichte)

deck_css = r'''
/* Aus was wird was */
.auswas{display:grid;grid-template-columns:1fr 1fr;gap:10px 40px;width:100%;margin-top:26px}
.auswas div{display:grid;grid-template-columns:minmax(0,.9fr) 36px minmax(0,1.2fr);align-items:center;padding:12px 16px;border-radius:10px;background:var(--panel);border:1px solid var(--line)}
.auswas span{font-size:19px;color:#9cc9ff}
.auswas i{font-style:normal;font-size:22px;color:var(--muted);text-align:center}
.auswas b{font:700 19px var(--disp);color:#f0c040}
.notizen{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:16px;width:100%;margin-top:20px}

/* Generierter Code */
.zahlen{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:28px;width:100%;margin-top:30px}
.zahlen div{display:flex;flex-direction:column;gap:8px}
.zahlen b{font:800 84px/1 var(--disp);font-stretch:112%;font-variant-numeric:tabular-nums;color:var(--zc)}
.zahlen span{font-size:21px;color:var(--ink2)}
.z-hand{--zc:var(--c-cmd)} .z-gen{--zc:#f0c040} .z-json{--zc:var(--muted)}
.zahlbalken{display:flex;height:22px;width:100%;margin-top:36px;border-radius:5px;overflow:hidden;gap:3px}
.zahlbalken i{display:block;background:var(--zc)}
.gen-zwei{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.15fr);gap:24px;width:100%;align-items:start}
.gen-drei{display:grid;grid-template-columns:minmax(0,.9fr) minmax(0,1.1fr) minmax(0,1fr);gap:20px;width:100%;align-items:start}
.gen-zwei>div,.gen-drei>div{display:flex;flex-direction:column;gap:10px;min-width:0}
.gen-tag{align-self:flex-start;font:600 13px var(--sans);letter-spacing:.06em;text-transform:uppercase;padding:5px 10px;border-radius:5px}
.gen-tag.hand{background:color-mix(in srgb,var(--c-cmd) 22%,var(--panel));color:#9cc9ff}
.gen-tag.gen{background:#3a2e12;color:#f0c040}
.reveal .gen-zwei pre.code{font-size:15px}
.reveal .gen-drei pre.code{font-size:13px}
.gen-zwei>div:nth-child(2) pre.code,.gen-drei>div:not(:first-child) pre.code{border-color:#f0c04055}
.klein-kette{margin-top:24px;gap:8px}
.klein-kette span:not(.pfeil){font-size:15px!important;padding:8px 12px!important}
.klein-kette .pfeil{font-size:20px!important}
.annot{display:grid;grid-template-columns:minmax(0,1.5fr) minmax(0,1fr);gap:14px 26px;width:100%;align-items:center}
.reveal .annot pre.code{font-size:15px;border-color:#f0c04055}
.notiz{display:flex;flex-direction:column;gap:4px;padding:12px 16px;border-left:4px solid #f0c040;background:var(--panel);border-radius:0 8px 8px 0}
.notiz b{font:700 19px var(--disp)}
.notiz span{font-size:16px;color:var(--muted)}
.mosaik{display:flex;flex-direction:column;gap:8px;width:100%}
.mrow{display:flex;flex-direction:row;gap:8px}
.mrow>div{min-width:0;display:flex;flex-direction:column;justify-content:flex-end;gap:4px;padding:14px;border-radius:10px;background:color-mix(in srgb,#f0c040 10%,var(--panel));border:1px solid #f0c04040;overflow:hidden}
.mrow>div.hl{background:color-mix(in srgb,#f0c040 28%,var(--panel));border-color:#f0c040}
.mrow b{font:800 28px var(--disp);color:#f0c040;font-variant-numeric:tabular-nums}
.mrow span{font-size:15px;color:var(--ink2);line-height:1.25}
.mrow.r1 b{font-size:46px}
.mrow.r1 span{font-size:19px}
.mrow.r3>div{padding:8px 10px}
.mrow.r3 b{font-size:19px}
.mrow.r3 span{font-size:12.5px}

/* Tokens, Governance */
.l-tok{--lc:#f0c040} .l-gov{--lc:var(--h-akt)}
.loesen>div{padding:22px 24px}
.loesen b{font-size:25px}
.loesen>div>span:last-child{font-size:17px}
.loesen .l-n{font-size:32px}
.ausserdem{margin-top:22px}
.balken{display:flex;flex-direction:column;gap:16px;width:100%;margin-top:6px}
.balken div{display:grid;grid-template-columns:250px 1fr 150px;align-items:center;gap:18px;font-size:20px}
.b-l{color:var(--ink2)}
.b-bar{height:30px;background:#ffffff08;border-radius:4px;display:flex;overflow:hidden}
.b-bar i{display:block;height:100%;min-width:6px;background:var(--muted)}
.b-wir .b-bar i.skel{background:#f0c040}
.b-wir .b-bar i.slot{background:var(--h-rdr)}
.b-wir .b-l{color:var(--ink);font-weight:600}
.b-w{font:600 20px var(--mono);text-align:right;color:var(--ink)}
.b-wir .b-w{color:#f0c040}
.spar{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px;width:100%;margin-top:34px}
.spar div{display:flex;flex-direction:column;gap:6px;padding:18px;border:1px solid var(--line);border-radius:10px;background:var(--panel)}
.spar b{font:800 30px var(--disp);color:#f0c040}
.spar div:nth-child(2) b{color:var(--h-rdr)}
.spar span{font-size:16px;color:var(--muted)}
.quelle{margin-top:18px;font-size:14px;color:var(--faint)}
.gov{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px;width:100%}
.gov div{display:flex;flex-direction:column;gap:8px;padding:20px;border-radius:10px;background:var(--panel);border:1px solid var(--line);border-top:4px solid var(--h-akt)}
.gov b{font:700 21px var(--disp)}
.gov span{font-size:16.5px;color:var(--muted)}
.gov code{font-size:13px}
.blick{grid-template-columns:repeat(5,minmax(0,1fr))}

/* Was wir lösen, LLM-Knoten */
.loesen{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:24px;width:100%}
.loesen>div{display:flex;flex-direction:column;gap:12px;padding:28px;border-radius:14px;background:var(--panel);border:1px solid var(--line);border-top:6px solid var(--lc)}
.l-llm{--lc:var(--h-rdr)} .l-typ{--lc:var(--c-cmd)} .l-ddd{--lc:var(--h-agg)}
.l-n{font:800 40px var(--disp);color:var(--lc)}
.loesen b{font:700 28px/1.15 var(--disp)}
.loesen>div>span:last-child{font-size:19px;color:var(--ink2)}
.ausserdem{display:flex;flex-wrap:wrap;gap:10px;margin-top:30px;align-items:center;width:100%}
.ausserdem b{font:600 15px var(--sans);letter-spacing:.1em;text-transform:uppercase;color:var(--muted);margin-right:8px}
.ausserdem span{font-size:17px;padding:8px 14px;border:1px solid var(--line);border-radius:999px;color:var(--ink2);background:var(--panel)}
.iso{display:grid;grid-template-columns:minmax(0,1.2fr) minmax(0,1fr);gap:40px;align-items:center;width:100%}
.iso-graph{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}
.iso-graph span{padding:18px 12px;border-radius:10px;text-align:center;font:600 17px var(--mono)}
.iso-graph .iso-ja{grid-column:1/-1;font-size:22px;padding:26px}
.iso-ja{background:color-mix(in srgb,var(--h-rdr) 30%,var(--panel));border:2px solid var(--h-rdr);color:var(--ink)}
.iso-nach{background:color-mix(in srgb,var(--h-rdr) 10%,var(--panel));border:2px dashed color-mix(in srgb,var(--h-rdr) 60%,transparent);color:var(--ink2)}
.iso-nein{background:var(--panel);border:1px solid var(--line2);color:var(--faint);opacity:.55}
.iso-nie{border:2px solid var(--c-hin);background:repeating-linear-gradient(135deg,transparent 0 4px,#e07a7044 4px 6px)}
.iso-legende{display:flex;flex-direction:column;gap:14px}
.iso-legende div{display:grid;grid-template-columns:28px 160px 1fr;align-items:center;font-size:18px;color:var(--muted)}
.iso-legende i{width:22px;height:22px;border-radius:5px;display:inline-block}
.iso-legende b{color:var(--ink);font:700 19px var(--disp)}
.meldungen code{font-size:16px;color:var(--ink)}
.meldungen span{color:var(--muted);font-size:18px}
.ddd{display:grid;grid-template-columns:1fr 1fr;gap:0 48px;width:100%}
.ddd div{display:grid;grid-template-columns:200px 1fr;grid-template-rows:auto auto;gap:2px 16px;padding:12px 0;border-bottom:1px solid var(--line2);align-items:center}
.ddd span{grid-row:1/3;font:500 18px var(--mono);color:var(--muted)}
.ddd b{font:700 21px var(--disp);display:flex;align-items:center;gap:10px}
.ddd em{font-style:normal;font-size:16px;color:var(--muted)}
.ddd .dot{width:14px;height:14px}
.k-llm{--hc:#c4a3f0}
.t-code{--sc:#e0b46a}
.llmknoten{display:flex;align-items:flex-end;gap:0;margin:20px 0 10px;zoom:1.3}
.llm-kante{width:130px;height:2px;border-top:2px dashed #c4a3f0;position:relative;margin:0 -6px 21px}
.llm-kante span{position:absolute;top:-22px;white-space:nowrap;left:50%;transform:translateX(-50%);font:600 12px var(--sans);color:#c4a3f0}
.llmknoten .rumpfport .lbl{color:var(--faint);font-style:italic}
.auftrag{padding:8px 14px;font:italic 13px/1.4 var(--sans);color:var(--ink2)}
.knoepfe{display:flex;gap:6px;padding:4px 12px 8px}
.knoepfe span{font:600 11.5px var(--sans);padding:4px 8px;border-radius:5px;background:#233047;border:1px solid #35507a;color:#cfe}
.knoepfe .kn-go{background:#4a3a7a;border-color:#7c5cff;color:#e0d4ff}
.lauf{padding:6px 12px 10px;font:12px var(--sans);color:var(--muted)}
.lauf b{color:#4ade80;font-weight:600}
.ablauf{display:flex;flex-direction:column;gap:10px;width:100%;max-width:1150px}
.ablauf div{display:grid;grid-template-columns:54px 230px 1fr;align-items:center;padding:12px 18px;border-radius:10px;background:var(--panel);border:1px solid var(--line);font-size:19px;color:var(--ink2)}
.ab-n{font:800 24px var(--disp);color:var(--h-rdr)}
.ablauf b{font:700 22px var(--disp);color:var(--ink)}
.ab-rot{border-color:#e07a7066!important} .ab-rot .ab-n{color:var(--c-hin)}
.ab-gruen{border-color:#4ade8066!important} .ab-gruen .ab-n{color:#4ade80}
.ausserhalb{margin-top:18px;display:flex;flex-direction:column;gap:6px;padding:14px 18px;border:2px dashed #f0c04088;border-radius:10px;max-width:1150px}
.ausserhalb code{font-size:16px;color:#f0c040;background:none;border:none}
.ausserhalb span{font-size:18px;color:var(--ink2)}
.protokoll{display:flex;flex-direction:column;width:100%;border:1px solid var(--line);border-radius:10px;overflow:hidden;margin-bottom:28px}
.protokoll div{display:grid;grid-template-columns:90px 1.4fr 1fr 110px;gap:16px;padding:13px 20px;border-top:1px solid var(--line2);font-size:19px}
.protokoll .pk-k{border-top:none;background:#ffffff08;font:600 14px var(--sans);color:var(--muted);text-transform:uppercase;letter-spacing:.06em}
.protokoll .rot{color:var(--c-hin)} .protokoll .gruen{color:#4ade80}

/* Fortsetzung: Konsistenz, Arbeitsplatz-Tag, Werkbank, LLM */
.zs-grid{display:grid;grid-template-columns:170px repeat(4,minmax(0,1fr));gap:12px 14px;align-items:center;width:100%;margin-top:10px}
.zs-grid .zh{font:600 16px var(--sans);color:var(--muted);border-bottom:1px solid var(--line);padding-bottom:8px}
.zs-grid .zl{font:600 19px var(--disp);color:var(--ink)}
.zs-grid span{font:600 18px var(--sans);padding:11px 15px;border-radius:9px;display:inline-block}
.c-hin{border:2px dashed var(--c-hin);color:var(--c-hin)}
.wahrheit{width:100%;margin:6px 0 36px}
.zellen{display:flex;gap:6px}
.zellen i{flex:1;height:44px;border-radius:4px;background:color-mix(in srgb,var(--c-evt) 35%,var(--panel))}
.zellen i.kopf{background:var(--h-agg)}
.zellen i.offen{background:color-mix(in srgb,var(--c-evt) 12%,var(--panel));outline:1px dashed color-mix(in srgb,var(--c-evt) 40%,transparent)}
.marken{position:relative;height:64px;margin-top:8px}
.marken span{position:absolute;top:0;font-size:18px;color:var(--ink2);border-top:3px solid;padding-top:8px}
.m-proj{left:calc(100%*7/14 - 4px);border-color:var(--h-prj)!important}
.m-proj em{color:var(--muted)}
.m-kopf{right:0;border-color:var(--h-agg)!important;text-align:right}
.gross-kette span:not(.pfeil){font-size:19px}
.garantie{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:22px;width:100%}
.garantie>div{display:flex;flex-direction:column;gap:10px;padding:22px;border-radius:12px;background:var(--panel);border:2px solid var(--gc)}
.garantie b{font:700 24px var(--disp);color:var(--gc);margin-bottom:6px}
.garantie span{font-size:19px;color:var(--ink2);padding-left:14px;border-left:3px solid color-mix(in srgb,var(--gc) 50%,transparent)}
.g-gut{--gc:#4ade80} .g-mittel{--gc:#f0c040} .g-los{--gc:var(--muted);border-style:dashed!important}
.snack{display:inline-flex;align-items:center;gap:14px;padding:16px 22px;border-radius:8px;background:#3a2e12;border:1px solid #f0c04066;color:#f0c040;font:600 21px var(--mono);box-shadow:0 10px 30px #000a}
.snack b{display:grid;place-items:center;width:30px;height:30px;border-radius:50%;background:#f0c040;color:#1a1400;font-family:var(--sans)}
.offline{width:100%;display:flex;flex-direction:column;gap:22px;margin-top:10px}
.ol-spur{display:grid;grid-template-columns:2fr 1fr 2fr;height:46px;border-radius:8px;overflow:hidden;font:600 17px var(--sans)}
.ol-spur span{display:grid;place-items:center}
.ol-on{background:color-mix(in srgb,var(--c-evt) 22%,var(--panel));color:var(--ink)}
.ol-off{background:repeating-linear-gradient(135deg,#3a1f24 0 10px,#2a171b 10px 20px);color:var(--c-hin)}
.ol-ereignisse{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:14px}
.ol-ereignisse span{font:600 18px var(--sans);padding:12px 16px;border-radius:9px;text-align:center}
.schritte{display:flex;flex-direction:column;gap:18px;margin-top:6px}
.schritte div{display:grid;grid-template-columns:36px 340px 1fr;align-items:center;font-size:20px;color:var(--muted)}
.schritte b{font:700 28px var(--disp);color:var(--ink)}
.sp{width:22px;height:22px;border-radius:50%;border:3px solid var(--c-cmd);border-right-color:transparent}
.sp.ok{border-color:#4ade80;background:#4ade80}
.zettel{margin-top:40px;max-width:760px;padding:34px 40px;background:#f3e8a6;color:#2a2410;font:600 30px/1.35 var(--disp);border-radius:4px;transform:rotate(-1.2deg);box-shadow:0 18px 40px #000b}
.zettel span{display:block;margin-top:18px;font:500 20px var(--sans);color:#5c5130}
.werkbank{border:1px solid var(--line);border-radius:12px;overflow:hidden;background:#0b0e15}
.wb-kopf{display:flex;align-items:center;gap:10px;padding:10px 14px;background:#141926;border-bottom:1px solid var(--line2);font-size:15px}
.wb-kopf b{margin-right:8px}
.wb-zweig{font:500 14px var(--mono);color:var(--c-ce);margin-right:auto}
.wb-k{padding:5px 12px;border:1px solid #35507a;border-radius:6px;background:#233047;color:#cfe;font-weight:600}
.wb-k.gruen{background:#2e7d5b;border-color:#3fae7f;color:#fff}
.werkbank .canvas{border:none;border-radius:0}
.reveal .werkbank svg.wires{max-height:none}
.geste{display:flex;align-items:center;gap:14px;font:600 20px var(--sans)}
.geste>span{padding:13px 18px;border-radius:10px}
.geste .plus{font-size:34px;color:var(--c-cmd);padding:0}
.c-frist{border:2px solid var(--c-fr);color:var(--c-fr);font-family:var(--mono)!important;font-size:17px!important}
pre.diff .add{color:#7ee2a0;background:#1c3a2a}
pre.diff .del{color:#ff9a9a;background:#3a1f24;text-decoration:line-through}
.reveal pre.diff{font-size:17px}
.reveal .szene pre.code{font-size:17px}
.meldungen{display:flex;flex-direction:column;gap:12px;width:100%;max-width:1100px}
.meldungen div{display:flex;align-items:center;gap:16px;padding:16px 20px;border-radius:10px;font-size:21px;background:var(--panel)}
.meldungen b{font-size:24px}
.meldungen em{margin-left:auto;font:600 15px var(--mono);font-style:normal;color:var(--muted)}
.meldungen .rot{border:1px solid #e07a7066} .meldungen .rot b{color:var(--c-hin)}
.meldungen .gruen{border:1px solid #4ade8066} .meldungen .gruen b{color:#4ade80}
.kontext{display:flex;flex-direction:column;gap:10px;padding:20px;border:1px solid var(--line);border-radius:12px;background:var(--panel);font-size:17px}
.kontext>b{font:600 18px var(--sans);margin-bottom:4px}
.kontext>div{padding:10px 14px;border-radius:8px;border:1.5px solid var(--kc);background:color-mix(in srgb,var(--kc) 12%,var(--panel));color:var(--ink2)}
.kontext code{font-size:14px}
.k-skel{--kc:var(--h-bi)} .k-sig{--kc:var(--c-cmd)} .k-nach{--kc:var(--h-prj)} .k-prompt{--kc:var(--h-fn)}
.k-nie{font-size:15px;color:var(--faint);text-decoration:line-through}
.leitplanken{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:20px;width:100%}
.leitplanken div{display:flex;flex-direction:column;gap:10px;padding:26px 22px;border-top:6px solid var(--c-cmd);background:var(--panel);border-radius:0 0 10px 10px}
.leitplanken div:nth-child(2){border-color:var(--h-agg)} .leitplanken div:nth-child(3){border-color:#4ade80} .leitplanken div:nth-child(4){border-color:var(--h-fn)}
.leitplanken b{font:700 28px var(--disp)}
.leitplanken span{font-size:19px;color:var(--muted)}
.spur{display:flex;flex-direction:column;width:100%;border:1px solid var(--line);border-radius:10px;overflow:hidden}
.spur div{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1.4fr) minmax(0,.9fr);gap:16px;padding:13px 20px;border-top:1px solid var(--line2);font-size:19px;align-items:center}
.spur .sp-k{border-top:none;background:#ffffff08;font:600 14px var(--sans);color:var(--muted);text-transform:uppercase;letter-spacing:.06em}
.spur code{font-size:15px;color:var(--c-ce)}
.spur .mut{color:var(--faint)}
.gitmock{width:100%;max-width:900px;border:1px solid var(--line);border-radius:12px;overflow:hidden;background:var(--panel);font-size:18px}
.gm-kopf{display:flex;gap:14px;align-items:center;padding:12px 18px;background:#141926;border-bottom:1px solid var(--line2)}
.gm-kopf b{font:600 16px var(--mono);color:var(--c-ce)}
.gm-kopf span{font-size:14px;color:var(--muted)}
.gm-msg{padding:16px 18px;font:600 22px var(--disp)}
.gm-f{padding:8px 18px;font:500 15px var(--mono);color:var(--ink2);border-top:1px solid var(--line2)}
.gm-f em{font-style:normal;color:var(--h-fn);margin-left:10px;font-family:var(--sans)}
.blick{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px;width:100%}
.blick div{display:flex;flex-direction:column;gap:8px;padding:22px;border:1px solid var(--line);border-radius:12px;background:var(--panel)}
.blick b{font:700 24px var(--disp)}
.blick span{font-size:18px;color:var(--muted)}

/* Geschichte: Stationen */
.zeit{font:600 20px var(--mono);color:var(--c-evt);margin:0 0 4px;letter-spacing:.02em}
.reveal .station h2{font-size:50px;margin-bottom:18px}
.reveal .kapitel h2{font-size:60px}
.kapitel .in{justify-content:center}
.szene{flex:1;min-height:0;display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:40px;align-items:center}
.mensch{display:flex;flex-direction:column;gap:26px;align-items:flex-start}
.mensch .pointe{margin:0;font-size:26px}
.neben{font-size:19px;color:var(--muted);max-width:44ch;margin:0}
.karte .stage{padding:26px 30px}
.reveal .karte .stage .node{zoom:1.5}
.reveal .karte.z1 .stage .node{zoom:1}
.fachwort{margin:16px 0 0;display:inline-flex;align-items:center;gap:12px;font-size:21px;padding:12px 22px;border:1px solid var(--line);border-radius:999px;background:var(--panel);align-self:flex-start;max-width:none}
.fachwort b{color:var(--ink)}
.ordner{display:flex;flex-direction:column;gap:10px;padding:18px 22px;border:1px solid var(--line);border-radius:10px;background:var(--panel);min-width:380px}
.ordner b{font:600 16px var(--sans);color:var(--muted)}
.datei{font:500 22px var(--mono);color:var(--ink);padding:10px 14px;background:#1f2a3d;border-radius:6px}
.bilder{display:flex;align-items:center;gap:24px;padding-bottom:30px}
.bild{position:relative;border-radius:8px;border:1px solid var(--line)}
.bild.roh{width:240px;height:180px;background:repeating-linear-gradient(90deg,transparent 0 29px,#ffffff10 29px 30px),linear-gradient(135deg,#3a3f48,#20242b)}
.bild.vor{width:130px;height:98px;background:repeating-linear-gradient(90deg,transparent 0 15px,#ffffff18 15px 16px),linear-gradient(135deg,#7d8ca3,#1b2230)}
.bild span{position:absolute;left:0;bottom:-30px;font:500 15px var(--mono);color:var(--muted);white-space:nowrap}
.dateiname{font:600 44px var(--mono);display:flex}
.dateiname .t{color:var(--c-ce)} .dateiname .sep,.dateiname .e{color:var(--faint)} .dateiname .v{color:#f0c040}
.miniliste{border:1px solid var(--line);border-radius:10px;overflow:hidden;min-width:480px;background:var(--panel)}
.miniliste div{display:flex;gap:12px;align-items:center;padding:11px 16px;font:500 17px var(--mono);border-bottom:1px solid var(--line2);color:var(--ink2)}
.miniliste .neuz{background:color-mix(in srgb,var(--c-evt) 18%,var(--panel));color:var(--ink)}
.miniliste em{width:9px;height:9px;border-radius:50%;display:inline-block}
.miniliste em.g{background:#4ade80} .miniliste em.y{background:#f0c040}
.taste{font:700 72px var(--mono);width:124px;height:124px;display:grid;place-items:center;border:2px solid var(--line);border-bottom-width:9px;border-radius:16px;background:#1f2533;color:var(--ink)}
.c-akt{border:2px solid var(--h-akt);background:color-mix(in srgb,var(--h-akt) 16%,var(--panel))}
.c-bi{border:2px solid var(--h-bi);background:color-mix(in srgb,var(--h-bi) 16%,var(--panel))}
.mensch .kette .pfeil{font-size:26px}
.vn.klein{gap:24px}
.vn.klein .rec{min-width:200px;font-size:19px}
.figuren{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:36px;margin-top:44px;width:100%}
.figuren div{display:flex;flex-direction:column;gap:8px;padding:28px;border:1px solid var(--line);border-radius:12px;background:var(--panel)}
.fig-k{font-size:34px;color:var(--c-cmd)}
.figuren b{font:700 30px var(--disp)}
.figuren span:last-child{font-size:20px;color:var(--muted)}
.sl.code .in{justify-content:flex-start}
.reveal .sl.code h2{font-size:34px;margin-bottom:22px}
.sl.code pre.code{font-size:17px}
.zwei-code{display:grid;grid-template-columns:1fr 1fr;gap:24px;align-items:start}
.zwei-code pre.code{font-size:14.5px}
.reveal .log.rueck{min-width:820px}
.reveal .log.rueck div{font-size:22px;padding:10px 22px}
.log i,.legende i{width:14px;height:14px;border-radius:3px;display:inline-block;flex:none}
.w-src{background:var(--h-src)} .w-fn{background:var(--h-fn)} .w-agg{background:var(--h-agg)}
.legende{font-size:18px;color:var(--muted);margin-top:22px;display:flex;align-items:center;gap:8px}
.reveal .protos.n4{grid-template-columns:repeat(4,minmax(0,1fr))}
.reveal .protos.n4 .proto .stage .node{zoom:1}
.schluss .drei{display:flex;flex-direction:column;gap:12px;margin-top:40px}
.schluss .drei p{font:600 30px var(--disp);color:var(--ink2);margin:0}
.reveal .erz h2,.reveal .station h2,.reveal .kapitel h2{text-wrap:balance}

/* Erzählung */
.erz .in{justify-content:center;align-items:flex-start}
.reveal .erz h2{font-size:58px;line-height:1.08;max-width:26ch;margin-bottom:48px}
.pointe{font:600 28px/1.3 var(--disp);color:var(--ink);margin-top:44px;max-width:none}
.pointe b{color:var(--c-evt)}
.tri{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:40px;width:100%}
.tri>div{display:flex;flex-direction:column;gap:16px;align-items:flex-start}
.tri>div>span{font-size:22px;color:var(--muted)}
.mock{font:600 22px var(--sans);padding:18px 24px;border-radius:10px;display:inline-block}
.m-btn{background:var(--c-cmd);color:#fff}
.m-file{background:var(--panel);border:1px solid var(--line);font:500 19px var(--mono);color:var(--ink2)}
.m-ki{background:#1c2430;border:1px solid var(--line);color:#f0c040;font-family:var(--mono)}
.vn{display:flex;align-items:center;gap:48px}
.reveal .rec{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:20px 26px;min-width:340px;display:flex;flex-direction:column;gap:10px;font-size:22px}
.reveal .rec b{font:700 24px var(--disp);margin-bottom:6px}
.reveal .rec div{display:flex;justify-content:space-between;gap:30px}
.reveal .rec span{color:var(--muted)}
.reveal .rec em{font-style:normal;font-family:var(--mono);color:var(--ink)}
.reveal .rec .neu em{color:var(--c-evt)}
.reveal .rec s{color:var(--c-hin);margin-right:10px}
.reveal .pfeil{font-size:34px;color:var(--muted);line-height:1}
.fragen{display:flex;flex-direction:column;gap:14px}
.fragen span{font:600 30px var(--disp);color:var(--c-hin)}
.reveal .log{display:flex;flex-direction:column;border-left:4px solid var(--c-evt);min-width:780px}
.reveal .log div{display:flex;gap:24px;align-items:baseline;padding:12px 22px;font-size:24px;border-bottom:1px solid var(--line2)}
.reveal .log time{font:500 18px var(--mono);color:var(--muted)}
.reveal .log.klein{min-width:0}
.reveal .log.klein div{font-size:17px;padding:8px 16px;gap:14px}
.reveal .log.klein time{font-size:14px}
.falten{display:grid;grid-template-columns:auto auto 1fr;gap:36px;align-items:center;width:100%}
.falten .pfeil{font-size:24px}
.ansichten{display:flex;gap:18px;flex-wrap:wrap}
.reveal .ansichten .rec{min-width:230px;font-size:18px;padding:16px 20px}
.reveal .ansichten .rec b{font-size:20px}
.kette{display:flex;align-items:center;gap:14px;flex-wrap:wrap}
.kette>span:not(.pfeil),.schlangen span:not(.pfeil){font:600 20px var(--sans);padding:13px 18px;border-radius:10px;display:inline-block}
.reveal .kette>span.rec{display:flex;flex-direction:column;font-size:18px}
.kette small,.ergebnis small{display:block;font:400 15px var(--sans);color:var(--muted);margin-top:2px}
.c-wunsch{background:var(--c-cmd);color:#fff}
.c-fakt{background:color-mix(in srgb,var(--c-evt) 20%,var(--panel));border:2px solid var(--c-evt)}
.c-ab{border:2px dashed var(--c-hin);color:var(--c-hin)}
.c-ent{border:2px solid var(--h-agg);background:color-mix(in srgb,var(--h-agg) 18%,var(--panel))}
.c-quelle{border:2px solid var(--h-src);background:color-mix(in srgb,var(--h-src) 14%,var(--panel))}
.c-reakt{border:2px solid var(--h-fn);background:color-mix(in srgb,var(--h-fn) 16%,var(--panel))}
.c-frage{background:var(--c-qry);color:#fff}
.c-ansicht{border:2px solid var(--h-prj);background:color-mix(in srgb,var(--h-prj) 16%,var(--panel))}
.c-log{background:#1b2231;border:1px solid var(--line);font-family:var(--disp)!important}
.ergebnis{display:flex!important;flex-direction:column;gap:10px;align-items:flex-start;padding:0!important}
.ergebnis>span{font:600 22px var(--sans);padding:14px 20px;border-radius:10px}
.ergebnis>small{margin:0 0 0 6px;font-size:16px}
.schlangen{display:flex;flex-direction:column;gap:22px}
.schlangen>div{display:flex;align-items:center;gap:12px;justify-content:flex-end;width:760px}
.schlangen .c-wunsch.k{font-size:17px!important;padding:10px 14px!important}
.schlangen .c-ent{min-width:120px;text-align:center}
.reveal .rec.antwort{min-width:0;gap:6px}
.reveal .rec .z{font:500 18px var(--mono);color:var(--ink)}
.erz .flowpanel{width:100%}
.reveal svg.erzflow{max-height:520px}
.woerter{display:grid;grid-template-columns:1fr 1fr;gap:0 80px;width:100%;max-width:1240px}
.woerter div{display:grid;grid-template-columns:34px 270px 1fr;align-items:center;padding:13px 0;border-bottom:1px solid var(--line2);font-size:26px}
.woerter b{font-family:var(--disp)}
.woerter div>span:last-child{font:500 22px var(--mono);color:var(--c-ce)}
.dot{width:18px;height:18px;border-radius:50%;background:var(--d)}
.dot.sq{border-radius:4px}
.dot.hohl{background:none;border:3px solid var(--d)}
.zwei{display:grid;grid-template-columns:1fr 1fr;gap:40px;width:100%;max-width:1100px}
.zwei div{padding:28px;border:1px solid var(--line);border-radius:12px;background:var(--panel);display:flex;flex-direction:column;gap:10px}
.zwei b{font:700 32px var(--disp)}
.zwei span{font-size:22px;color:var(--muted)}

html,body{height:100%;margin:0;background:var(--bg)}
.reveal{background:var(--bg);color:var(--ink);font:18px/1.55 var(--sans)}
.reveal .slides{text-align:left}
.reveal .slides section.sl{height:100%;padding:34px 56px;box-sizing:border-box}
.reveal .slides section.sl>.in{height:100%;display:flex;flex-direction:column}
.reveal .slides section.sl .bd{flex:1;min-height:0;display:flex;flex-direction:column;justify-content:center}
.reveal h1{font-size:96px}
.reveal h2{font-size:42px;margin-bottom:6px}
.reveal .kicker{margin-bottom:10px}
.reveal .lead{font-size:20px;margin:10px 0 18px;max-width:80ch}
.reveal .wrap,.reveal .wide{max-width:none;padding:0;margin:0}
.reveal .controls{color:var(--c-cmd)}
.reveal .progress{color:var(--c-cmd);height:4px}
.reveal .slide-number{background:transparent;color:var(--faint);font:12px var(--sans);right:16px;bottom:12px}
.reveal svg.flow{max-height:590px}
.titel .in{justify-content:center}
.titel .lead{font-size:24px;max-width:54ch}
.nav-hint{margin-top:48px;color:var(--muted);font-size:16px}
.nav-hint kbd{font-size:14px;padding:3px 8px;margin-right:3px}
.reveal .sig{margin-top:0}
.reveal .sig pre.code{font-size:17px}
.reveal .maplist li{font-size:18px}
.reveal .msg p{font-size:18px}
.reveal .msg .g{font-size:15px}
.reveal .msg h3{font-size:21px}
.reveal .deps{font-size:16px}
.reveal .protos{display:grid;gap:24px;grid-template-columns:repeat(3,minmax(0,1fr))}
.reveal .protos.n2{grid-template-columns:repeat(2,minmax(0,520px));justify-content:center}
.reveal .proto p{font-size:17px}
.reveal .proto .meta{font-size:15px}
.gross .logbox h3,.gross .points h3{font-size:34px}
.gross .logbox p,.gross .points p{font-size:24px;line-height:1.45}
.gross .logbox,.gross .points{gap:56px}
.gross .points:not(.three){grid-template-columns:repeat(2,minmax(0,1fr));row-gap:48px}
.gross .edges{grid-template-columns:1fr 1fr;gap:0 56px}
.gross .edges li{font-size:23px;padding:20px 0}
.reveal .el{border:none;padding:0;grid-template-columns:minmax(0,1.15fr) minmax(0,1fr)}
.reveal .el h3{font-size:30px}
.reveal .el p{font-size:18px}
.reveal .el .art{font-size:13px}
.reveal .el pre.code{font-size:12.5px}
.reveal .el .stage{min-height:0}
.chips{display:flex;flex-wrap:wrap;gap:10px;max-width:1100px}
.chips span{font:500 16px var(--mono);padding:8px 14px;border:1px solid var(--line);border-radius:6px;background:var(--panel);color:var(--ink2)}
.wfsl .bd{justify-content:flex-start}
.reveal .wf-b{min-height:300px}
.reveal .wf,.reveal .wf-hl{min-width:0;width:100%}
.reveal .slots{margin-top:28px}
.reveal .slots b{font-size:20px}
.reveal .slots span{font-size:16px}
.reveal .ideen h3{font-size:24px}
.reveal .ideen p{font-size:18px}
.code-gross pre.code{font-size:17px;max-width:1000px}
.reveal .treppe span{font-size:18px}
.reveal .treppe div{padding:16px 20px}
.reveal .treppe b{font-size:18px}
.reveal .treppe code{font-size:13px}
.reveal h3.sub{margin:0 0 12px}
.reveal .ui3 .subl{font-size:16px}
.reveal .keys div{font-size:20px;padding:18px 24px;grid-template-columns:220px minmax(0,1fr)}
.reveal .keys .lv{font-size:17px}
.reveal .keys kbd{font-size:14px}
.reveal .modi span{font-size:22px}
.reveal .modi b{font-size:30px}
.reveal .modi div{padding:24px 26px}
.reveal .pair pre.code{font-size:17px}
.reveal .pair{grid-template-columns:1fr 1fr}
.reveal .cli{font-size:18px}
.reveal .parts{padding:40px}
.reveal .proto .stage .node,.reveal .el .stage .node,.reveal .sig .stage .node{zoom:1.25}
.reveal .parts .node{zoom:1.3}
.reveal .ui3 .pf,.reveal .ui3 .bt,.reveal .ui3 .einst{zoom:1.15}
'''

html = f'''<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<!-- Präsentation des Zielbilds (reveal.js): ← → Kapitel, ↑ ↓ Details, Esc Übersicht. Erzeugt aus der Scroll-Fassung. -->
<title>Software, die nichts vergisst</title>
<style>
{schriften}
</style>
<style>
{reveal_css}
</style>
<style>
{css}
{deck_css}
</style>
</head>
<body>
<div class="reveal"><div class="slides">
{folien}
</div></div>
<script>
{reveal_js}
</script>
<script>
{script}
Reveal.initialize({{{'fragments:false,' if os.environ.get('ALLE') else ''}hash:true,width:1440,height:810,margin:0.03,center:false,controls:true,progress:true,
  slideNumber:'h.v',transition:'slide',backgroundTransition:'none'}}).then(function(){{window.__layout();}});
Reveal.on('slidechanged',function(){{window.__layout();}});
Reveal.on('overviewshown',function(){{window.__layout();}});
Reveal.on('resize',function(){{window.__layout();}});
</script>
</body>
</html>
'''
ziel = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HIER, 'zielbild.html')
open(ziel, 'w').write(html)
if len(sys.argv) > 2:   # Fassung ohne eigenes Gerüst (für ein Artifact)
    i = html.index('<title>'); j = html.index('</head>'); k = html.index('<body>') + 6; e = html.rindex('</body>')
    open(sys.argv[2], 'w').write(html[i:j].strip() + '\n' + html[k:e].strip() + '\n')
print('ok', len(html))
