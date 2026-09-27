namespace SimHost;

/// <summary>
/// Die LLM-Konsole (Route /konsole): Code-Block wählen → Auftrag → Füllen (zustandslose Runden, geprüft) →
/// Simulieren (Decide/Apply) → Anpassen → Übernehmen (echte Datei) → Bauen / Rückgängig. Eine Seite, kein Build-Schritt.
/// Deep-Link aus dem Editor: /konsole#id=&lt;Slot-Id&gt;&amp;auftrag=&lt;Text&gt;.
/// </summary>
public static class KonsoleSeite
{
    public const string Html = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>LLM-Konsole</title>
<style>
:root{--bg:#f4f5f7;--fl:#fff;--tx:#1b2330;--mu:#5d6778;--li:#d9dde4;--so:#eceef2;--ak:#2f55c7;--ok:#1d7a4f;--okb:#dff2e8;--wa:#9a5b0c;--wab:#f7ead5;--fe:#b3261e;--feb:#f9e0de;--code:#fafbfc;
--mono:ui-monospace,"SF Mono",Menlo,Consolas,monospace;--sans:system-ui,-apple-system,"Segoe UI",sans-serif}
@media (prefers-color-scheme:dark){:root{--bg:#0f141b;--fl:#161d27;--tx:#e2e7ee;--mu:#95a1b3;--li:#2a3442;--so:#1d2632;--ak:#86a2f2;--ok:#4cc38a;--okb:#15301f;--wa:#e2a64b;--wab:#33281a;--fe:#f28b82;--feb:#3a1d1b;--code:#111821;color-scheme:dark}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--tx);font:14px/1.5 var(--sans);height:100vh;display:flex;flex-direction:column}
header{display:flex;gap:16px;align-items:center;padding:10px 16px;border-bottom:1px solid var(--li);background:var(--fl);flex-wrap:wrap}
header h1{font-size:16px;margin:0}
header .st{color:var(--mu);font-size:12.5px;flex:1;min-width:200px}
header .warn{color:var(--fe);font-weight:600}
main{flex:1;display:grid;grid-template-columns:300px minmax(0,1fr);min-height:0}
aside{border-right:1px solid var(--li);display:flex;flex-direction:column;min-height:0;background:var(--fl)}
aside input{margin:10px;padding:7px 9px;border:1px solid var(--li);border-radius:5px;background:var(--bg);color:var(--tx);font:inherit}
#liste{overflow-y:auto;flex:1;padding-bottom:10px}
.gr{font:600 11px var(--mono);letter-spacing:.06em;text-transform:uppercase;color:var(--mu);padding:10px 12px 3px}
.si{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:6px;padding:4px 12px;cursor:pointer;font-size:13px;border:0;background:none;color:inherit;width:100%;text-align:left}
.si:hover{background:var(--so)}.si[aria-current=true]{background:var(--so);box-shadow:inset 3px 0 var(--ak)}
.si span:first-child{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.si small{color:var(--mu);font-family:var(--mono)}
section#haupt{overflow-y:auto;padding:16px 20px 40px;display:flex;flex-direction:column;gap:14px}
.kasten{background:var(--fl);border:1px solid var(--li);border-radius:7px;padding:12px 14px;display:flex;flex-direction:column;gap:8px}
.kasten h2{font-size:14px;margin:0}
.meta{display:flex;flex-wrap:wrap;gap:6px}
.pill{font:12px var(--mono);padding:2px 7px;border-radius:3px;background:var(--so)}
.pill.ok{background:var(--okb);color:var(--ok)}.pill.wa{background:var(--wab);color:var(--wa)}.pill.fe{background:var(--feb);color:var(--fe)}
textarea{width:100%;min-height:64px;padding:8px;border:1px solid var(--li);border-radius:5px;background:var(--bg);color:var(--tx);font:13px/1.45 var(--mono);resize:vertical}
pre{margin:0;padding:10px;background:var(--code);border:1px solid var(--li);border-radius:5px;font:12.5px/1.5 var(--mono);overflow:auto;max-height:420px;white-space:pre}
details>summary{cursor:pointer;color:var(--mu);font-size:13px}
.zeile{display:flex;gap:8px;align-items:center;flex-wrap:wrap}
button.b{font:inherit;font-size:13px;padding:6px 12px;border-radius:5px;border:1px solid var(--li);background:var(--fl);color:var(--tx);cursor:pointer}
button.b:hover{background:var(--so)}button.b.pr{background:var(--ak);border-color:var(--ak);color:#fff}
button.b:disabled{opacity:.5;cursor:default}
button:focus-visible,input:focus-visible,textarea:focus-visible,select:focus-visible{outline:2px solid var(--ak);outline-offset:1px}
.runde{border-left:3px solid var(--li);padding-left:10px;display:flex;flex-direction:column;gap:6px}
.runde.ok{border-color:var(--ok)}.runde.fe{border-color:var(--fe)}.runde.wa{border-color:var(--wa)}
.bef{color:var(--fe);font:12.5px var(--mono);white-space:pre-wrap;margin:0}
.leer{color:var(--mu)}
select,input.w{padding:5px 7px;border:1px solid var(--li);border-radius:5px;background:var(--bg);color:var(--tx);font:13px var(--mono)}
.hinweis{font-size:12.5px;color:var(--mu)}
@media (max-width:800px){main{grid-template-columns:1fr}aside{max-height:35vh}}
</style>
</head>
<body>
<header>
  <h1>LLM-Konsole</h1>
  <span class="st" id="status">lädt…</span>
  <button class="b" id="aktualisieren" type="button" title="GraphExtractor über den aktuellen Code laufen lassen (≈ 1 min)">Kontexte neu erzeugen</button>
  <a href="/editor" class="hinweis">zum Editor</a>
</header>
<main>
  <aside>
    <input id="suche" placeholder="Slot suchen (z. B. SetzeSplit)" aria-label="Slot suchen">
    <div id="liste"></div>
  </aside>
  <section id="haupt"><p class="leer">Links einen Code-Block wählen.</p></section>
</main>
<script>
const $ = s => document.querySelector(s);
const esc = s => (s ?? '').toString().replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
const api = async (url, body) => { const r = await fetch(url, body === undefined ? {} : {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)}); return r.json(); };
let SLOTS = [], AKT = null, DETAIL = null, KANDIDAT = null, BASISHASH = null, RUNDEN = [];

async function status(){
  const s = await api('/api/llm/status');
  $('#status').innerHTML = `Anbieter: ${esc(s.anbieter)}` + (s.index ? ` · Kontexte vom ${esc(s.index.stand)} (${s.index.slots} Code-Blöcke, Skelett ≈ ${s.index.tokenSkelett} Token)` : ' · <span class="warn">keine Kontexte — „Kontexte neu erzeugen“</span>')
    + (s.warnung ? ` · <span class="warn">${esc(s.warnung)}</span>` : '');
}
async function liste(){
  SLOTS = await api('/api/llm/slots');
  zeichneListe();
}
function zeichneListe(){
  const q = $('#suche').value.toLowerCase(); let html = '', gr = null;
  for (const s of SLOTS.filter(s => s.titel.toLowerCase().includes(q))) {
    if (s.art !== gr) { gr = s.art; html += `<div class="gr">${esc(gr)}</div>`; }
    html += `<button class="si" type="button" data-id="${esc(s.id)}" aria-current="${s.id===AKT}"><span>${esc(s.titel.replace(/^\S+ /,''))}</span><small>${s.rumpf==='geschrieben'?'✓':s.rumpf==='leer'?'∅':'⚙'}</small></button>`;
  }
  $('#liste').innerHTML = html || '<p class="leer" style="padding:0 12px">keine Treffer</p>';
  document.querySelectorAll('.si').forEach(b => b.onclick = () => waehle(b.dataset.id));
}
async function waehle(id, auftrag){
  // Vom Editor kommt der Board-Schlüssel; Store-Slots tragen zusätzlich „@Implementierung“.
  if (!SLOTS.some(s => s.id === id)) { const t = SLOTS.find(s => s.id.startsWith(id + '@')); if (t) id = t.id; }
  AKT = id; KANDIDAT = null; RUNDEN = []; zeichneListe();
  DETAIL = await api('/api/llm/slot?id=' + encodeURIComponent(id));
  if (!DETAIL.ok) { $('#haupt').innerHTML = `<p class="bef">${esc(DETAIL.grund)}</p>`; return; }
  const s = DETAIL.slot, a = DETAIL.aktuell;
  BASISHASH = a.ok ? a.hash : null;
  $('#haupt').innerHTML = `
  <div class="kasten">
    <h2>${esc(s.titel)}</h2>
    <div class="meta"><span class="pill">${esc(s.datei)}:${s.zeile}</span><span class="pill">Rumpf: ${esc(s.rumpf)}</span>
      <span class="pill">Kontext ≈ ${DETAIL.tokenGesamt} Token (Skelett + Slot-Teil)</span></div>
    <details><summary>Aktueller Rumpf in der Datei</summary><pre>${esc(a.ok ? a.body : a.grund)}</pre></details>
    <details><summary>Slot-Teil des Kontexts (das Graph-Skelett kommt davor)</summary><pre>${esc(DETAIL.slotTeil)}</pre></details>
  </div>
  <div class="kasten">
    <h2>Auftrag</h2>
    <textarea id="auftrag" placeholder="Was soll der Rumpf tun?">${esc(auftrag ?? s.auftrag ?? (a.ok ? a.prompt : '') ?? '')}</textarea>
    <div class="zeile">
      <button class="b pr" id="fuellen" type="button">Füllen</button>
      <label><input type="checkbox" id="auto" checked> bei Fehlern automatisch reparieren</label>
      <label>max. <select id="max"><option>1</option><option>2</option><option selected>3</option></select> Runden</label>
      <span class="hinweis">Jede Runde ist ein neuer Aufruf: Kontext + Auftrag + nur der letzte Rumpf.</span>
    </div>
  </div>
  <div id="ergebnis"></div>`;
  $('#fuellen').onclick = () => fuellen(null);
}
async function fuellen(anpassung){
  const auftrag = $('#auftrag').value.trim();
  if (!auftrag) { alertBox('Ohne Auftrag wird kein Aufruf ausgelöst.'); return; }
  const knopf = $('#fuellen'); knopf.disabled = true; knopf.textContent = 'läuft…';
  $('#ergebnis').insertAdjacentHTML('afterbegin', '<p class="leer" id="laeuft">Modell arbeitet …</p>');
  const r = await api('/api/llm/fuellen', { id: AKT, auftrag, rumpf: anpassung ? KANDIDAT : null, anpassung, autoReparatur: $('#auto').checked, maxRunden: +$('#max').value });
  knopf.disabled = false; knopf.textContent = 'Füllen'; $('#laeuft')?.remove();
  if (!r.ok) { alertBox(r.grund); return; }
  if (r.basisHash) BASISHASH = r.basisHash;
  RUNDEN = RUNDEN.concat(r.runden);
  const letzte = r.runden[r.runden.length - 1];
  KANDIDAT = letzte && letzte.rumpf ? letzte.rumpf : KANDIDAT;
  zeichneErgebnis();
}
function alertBox(t){ $('#ergebnis').insertAdjacentHTML('afterbegin', `<div class="kasten"><p class="bef">${esc(t)}</p></div>`); }
function tok(t){ if(!t) return ''; const p=[]; if(t.eingabe!=null)p.push(`Eingabe ${t.eingabe}`); if(t.ausCache!=null)p.push(`aus Cache ${t.ausCache}`); if(t.cacheGeschrieben!=null)p.push(`Cache geschrieben ${t.cacheGeschrieben}`); if(t.ausgabe!=null)p.push(`Ausgabe ${t.ausgabe}`); return p.join(' · '); }
function zeichneErgebnis(){
  const s = DETAIL.slot;
  let html = '<div class="kasten"><h2>Runden</h2>';
  RUNDEN.forEach(r => {
    const cls = r.fehler ? 'fe' : r.ok ? 'ok' : r.ergebnis === 'ausserhalb' ? 'wa' : 'fe';
    const st = r.fehler ? 'Fehler beim Aufruf' : r.ok ? 'kompiliert/geprüft ✓' : r.ergebnis === 'ausserhalb' ? 'AUSSERHALB' : r.ergebnis === 'unlesbar' ? 'Antwort unlesbar' : `${r.befunde.length} Befund(e)`;
    html += `<div class="runde ${cls}"><div class="meta"><span class="pill">Runde ${r.nr} · ${esc(r.art)}</span><span class="pill ${cls==='ok'?'ok':cls==='wa'?'wa':'fe'}">${esc(st)}</span>
      ${r.dauerMs!=null?`<span class="pill">${(r.dauerMs/1000).toFixed(1)} s</span>`:''}<span class="pill">Prompt ≈ ${r.promptTokenSchaetzung ?? '?'} Token</span>${tok(r.token)?`<span class="pill">${esc(tok(r.token))}</span>`:''}</div>
      ${r.fehler?`<p class="bef">${esc(r.fehler)}</p>`:''}
      ${r.ausserhalb?`<p><b>${esc(r.ausserhalb)}</b><br><span class="hinweis">Das ist eine Strukturänderung im Editor (Ausgang, Feld, Kante) — kein Rumpf.</span></p>`:''}
      ${r.rumpf?`<pre>${esc(r.rumpf)}</pre>`:''}
      ${r.antwortRoh?`<pre>${esc(r.antwortRoh)}</pre>`:''}
      ${r.befunde && r.befunde.length?`<p class="bef">${esc(r.befunde.join('\n'))}</p>`:''}</div>`;
  });
  html += '</div>';
  if (KANDIDAT) {
    const art = s.art, geprueft = art === 'decide' || art === 'apply';
    html += `<div class="kasten"><h2>Kandidat</h2>
      <p class="hinweis">${geprueft ? 'Geprüft: Syntax + In-Memory-Compile mit den echten Generatoren.' : 'Geprüft: Syntax. Kompiliert wird nach dem Übernehmen mit „Projekt bauen“.'}</p>
      <div class="zeile"><button class="b pr" id="uebernehmen" type="button">Übernehmen (in die Datei schreiben)</button>
      <button class="b" id="verwerfen" type="button">Verwerfen</button></div>
      <textarea id="anpassung" placeholder="Anpassung, z. B. „bei Seed 0 den Default-Seed nehmen“"></textarea>
      <div class="zeile"><button class="b" id="anpassen" type="button">Anpassen (neue Runde mit diesem Rumpf)</button></div>
      <div id="geschrieben"></div></div>`;
    if (DETAIL.simulierbar) html += simKasten();
  }
  $('#ergebnis').innerHTML = html;
  if (KANDIDAT) {
    $('#uebernehmen').onclick = uebernehmen;
    $('#verwerfen').onclick = () => { KANDIDAT = null; zeichneErgebnis(); };
    $('#anpassen').onclick = () => { const t = $('#anpassung').value.trim(); if (t) fuellen(t); };
    if (DETAIL.simulierbar) simVerdrahten();
  }
}
function simKasten(){
  const cmds = DETAIL.commands || [];
  const opt = cmds.map(c => `<option ${c.name===DETAIL.slot.disc?'selected':''}>${esc(c.name)}</option>`).join('');
  return `<div class="kasten"><h2>Simulation mit dem Kandidaten</h2>
    <p class="hinweis">Dieselbe Laufzeit wie im Editor: Command mit Werten schicken, Events und Zustand ansehen. Der Kandidat ersetzt nur in der Simulation den Rumpf.</p>
    <div class="zeile"><select id="simcmd">${opt}</select><button class="b" id="simlos" type="button">Ausführen</button><button class="b" id="simneu" type="button">Neu beginnen</button></div>
    <textarea id="simwerte"></textarea><pre id="simaus" class="leer">—</pre></div>`;
}
function simVerdrahten(){
  const setzeWerte = () => { const c = (DETAIL.commands||[]).find(x => x.name === $('#simcmd').value); const o = {};
    (c?.felder||[]).forEach(f => o[f.name] = /Guid/.test(f.typ) ? (window.SIMID ||= crypto.randomUUID()) : /int|long|double|decimal/.test(f.typ) ? 0 : /bool/.test(f.typ) ? false : /List|\[\]|IReadOnly|Collection/.test(f.typ) ? [] : '');
    $('#simwerte').value = JSON.stringify(o, null, 2); };
  setzeWerte(); $('#simcmd').onchange = setzeWerte;
  const lauf = async neu => { let werte; try { werte = JSON.parse($('#simwerte').value); } catch(e) { $('#simaus').textContent = 'Werte sind kein gültiges JSON.'; return; }
    const r = await api('/api/llm/simulieren', { id: AKT, rumpf: KANDIDAT, command: $('#simcmd').value, werte, neu });
    if (!r.ok) { $('#simaus').textContent = (r.fehler||[]).map(f => f.meldung).join('\n') || r.grund || 'Fehler'; return; }
    const z = []; (r.frames||[]).forEach(f => { z.push(`▶ ${f.command} → ${f.aggregat}`); (f.events||[]).forEach(e => z.push(`   ${e.persistent?'Event':'Ablehnung'} ${e.typ} ${e.werte}${e.warum?'   weil '+e.warum:''}`)); });
    (r.instanzen||[]).forEach(i => z.push(`■ ${i.aggregat} ${i.label}: ` + i.felder.map(f => `${f.name}=${f.wert}${f.geaendert?'*':''}`).join(', ')));
    $('#simaus').textContent = z.join('\n') || 'keine Wirkung'; };
  $('#simlos').onclick = () => lauf(false); $('#simneu').onclick = () => { window.SIMID = null; setzeWerte(); lauf(true); };
}
async function uebernehmen(){
  const r = await api('/api/llm/uebernehmen', { id: AKT, rumpf: KANDIDAT, auftrag: $('#auftrag').value.trim(), basisHash: BASISHASH });
  if (!r.ok) { $('#geschrieben').innerHTML = `<p class="bef">${esc(r.grund)}</p>`; return; }
  $('#geschrieben').innerHTML = `<p><b>Geschrieben:</b> ${esc(r.datei)}:${r.zeile} <span class="hinweis">(Sicherung ${esc(r.sicherung)})</span><br><span class="hinweis">${esc(r.hinweis)}</span></p>
    <div class="zeile"><button class="b" id="bauen" type="button">Projekt bauen</button><button class="b" id="zurueck" type="button">Rückgängig</button></div><pre id="bauaus" class="leer">—</pre>`;
  $('#bauen').onclick = async () => { $('#bauaus').textContent = 'baut …'; const b = await api('/api/llm/bauen', { id: AKT });
    $('#bauaus').textContent = b.ok ? `✓ ${b.projekt} baut` : `✗ ${b.projekt ?? ''}\n` + (b.fehler||[]).join('\n'); };
  $('#zurueck').onclick = async () => { const z = await api('/api/llm/rueckgaengig', { id: AKT }); $('#bauaus').textContent = z.ok ? '↶ alter Rumpf wiederhergestellt' : z.grund; };
}
$('#suche').oninput = zeichneListe;
$('#aktualisieren').onclick = async () => { const b = $('#aktualisieren'); b.disabled = true; b.textContent = 'erzeugt … (≈ 1 min)';
  const r = await api('/api/llm/aktualisieren', {}); b.disabled = false; b.textContent = 'Kontexte neu erzeugen';
  await status(); await liste(); if (AKT) waehle(AKT); if (!r.ok) alertBox((r.meldung||[]).join('\n') || r.grund); };
(async () => { await status(); await liste();
  const h = new URLSearchParams(location.hash.slice(1)); if (h.get('id')) waehle(h.get('id'), h.get('auftrag')); })();
</script>
</body>
</html>
""";
}
