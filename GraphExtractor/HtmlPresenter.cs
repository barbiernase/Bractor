namespace GraphExtractor;

/// <summary>
/// Rendert den Wissensgraphen als self-contained, interaktives Event-Modeling-Board (Canvas):
///   • Bounded Contexts als Container; darin die Aggregate; um jedes Aggregat seine einzelnen Funktionen
///     (Decide-Handler) visuell isoliert — Command REIN, Events (das OneOf) RAUS.
///   • Die emittierten Events fließen sichtbar in Projektionen (unten) und Sagas (oben).
///   • Domänen-Filter (Kontexte ein/ausblenden), Pan/Zoom.
///   • Trigger-Simulator: eine Auslöser-Nachricht schicken und die Token wellenweise durchs Board laufen
///     sehen — steuerbar über Prev / Next / Play / Reset.
/// Die gesamte Graph-Struktur ist als JSON eingebettet (window.GRAPH) und bleibt abfragbar.
/// </summary>
public static class HtmlPresenter
{
    public static string Render(KnowledgeGraph graph, string json) =>
        Template.Replace("/*__GRAPH_JSON__*/", json);

    private const string Template = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Wissensgraph — Event-Modeling-Board</title>
<style>
  :root{
    --bg:#0f1216; --panel:#161b22; --panel2:#1c222b; --ink:#e6edf3; --muted:#8b98a9; --line:#242c37;
    --evt:#3b82f6; --rej:#6b7480; --cmd:#e0902b; --agg:#22b07d; --proc:#8b5cf6; --proj:#14b8a6; --pipe:#f97316; --accent:#8b5cf6;
  }
  *{box-sizing:border-box}
  html,body{height:100%;margin:0}
  body{display:flex;font:13px/1.5 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;background:var(--bg);color:var(--ink);overflow:hidden}
  aside{width:300px;flex:0 0 300px;background:var(--panel);border-right:1px solid var(--line);display:flex;flex-direction:column;overflow:hidden}
  .brand{padding:16px 18px;border-bottom:1px solid var(--line)}
  .brand h1{margin:0;font-size:15px}
  .brand small{color:var(--muted);font-size:11px}
  .badge{display:inline-block;padding:2px 8px;border-radius:6px;font-size:10px;font-weight:700;margin-top:8px}
  .badge.auth{background:#0f2a20;color:var(--agg)} .badge.fallback{background:#2c1414;color:#d64545}
  .scroll{overflow-y:auto;flex:1;padding:14px 16px}
  .sec{margin-bottom:18px}
  .sec h2{font-size:11px;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);margin:0 0 8px}
  .row{display:flex;align-items:center;gap:8px;padding:3px 0;font-size:12px;cursor:pointer;user-select:none}
  .row input{accent-color:var(--accent)}
  .sw{width:11px;height:11px;border-radius:3px;flex:0 0 11px}
  .row .ct{margin-left:auto;color:var(--muted);font-size:11px}
  .mini{display:flex;gap:6px;margin-bottom:10px}
  .mini button{flex:1;background:var(--panel2);border:1px solid var(--line);color:var(--muted);border-radius:6px;padding:5px;font-size:11px;cursor:pointer}
  .mini button:hover{color:var(--ink)}
  select{width:100%;padding:8px;border-radius:8px;border:1px solid var(--line);background:var(--panel2);color:var(--ink);font:inherit;margin-bottom:10px}
  .ctrls{display:flex;gap:6px;margin-bottom:8px}
  .ctrls button{flex:1;background:var(--panel2);border:1px solid var(--line);color:var(--ink);border-radius:8px;padding:9px 0;font-size:14px;cursor:pointer}
  .ctrls button:hover{border-color:var(--accent)}
  .ctrls button.primary{background:var(--accent);border-color:var(--accent);color:#fff;font-weight:600}
  .ctrls button:disabled{opacity:.35;cursor:default}
  .stepinfo{background:var(--panel2);border:1px solid var(--line);border-radius:8px;padding:9px 11px;font-size:12px;min-height:54px}
  .stepinfo .n{color:var(--muted);font-size:11px}
  .stepinfo b{color:var(--accent)}
  .legend{display:flex;flex-wrap:wrap;gap:6px 12px;font-size:11px;color:var(--muted)}
  .legend span{display:inline-flex;align-items:center;gap:5px}
  .dot{width:9px;height:9px;border-radius:3px;display:inline-block}
  main{flex:1;position:relative;overflow:hidden}
  canvas{display:block;width:100%;height:100%;cursor:grab}
  canvas.grabbing{cursor:grabbing}
  #tip{position:absolute;pointer-events:none;background:#0b0e12ee;border:1px solid var(--line);border-radius:8px;padding:8px 10px;font-size:12px;max-width:300px;display:none;z-index:10}
  #tip .k{color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.05em}
  #tip .rows{color:var(--muted);font-size:11px;margin-top:4px;font-family:ui-monospace,Menlo,monospace}
  #hud{position:absolute;left:14px;bottom:12px;font-size:11px;color:var(--muted);background:#0b0e12aa;border:1px solid var(--line);border-radius:6px;padding:4px 8px}
  details{margin-top:6px} summary{cursor:pointer;color:var(--muted);font-size:11px}
  code{font-family:ui-monospace,Menlo,monospace;font-size:11px;color:var(--evt)}
  .livebadge{display:none;background:#0f2a20;color:var(--agg);border-radius:6px;padding:1px 7px;font-size:10px;font-weight:700;margin-left:6px}
  #cmdform{margin:4px 0 6px}
  #cmdform label{display:block;font-size:11px;color:var(--muted);margin:6px 0 2px}
  #cmdform input{width:100%;padding:6px 8px;border-radius:6px;border:1px solid var(--line);background:var(--panel2);color:var(--ink);font:inherit}
  #cmdform input[type=checkbox]{width:auto}
  #cmdform .send{width:100%;margin-top:10px;background:var(--accent);border:none;color:#fff;border-radius:8px;padding:9px;font-weight:600;cursor:pointer}
  #cmdform .rst{background:none;border:none;color:var(--muted);font-size:11px;cursor:pointer;margin-top:6px;text-decoration:underline;display:block}
  #states{margin-top:8px;font:11px ui-monospace,Menlo,monospace;color:var(--muted)}
  #states .s{padding:3px 0;border-top:1px solid var(--line)}
  #states b{color:var(--agg)}
  #instlist .itype{font-size:10px;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);margin:8px 0 3px}
  #instlist .inst{padding:5px 7px;border:1px solid var(--line);border-radius:7px;margin-bottom:4px;cursor:pointer;font-size:12px}
  #instlist .inst:hover{border-color:var(--accent)}
  #instlist .inst.sel{border-color:var(--agg);background:#22b07d14}
  #instlist .inst b{color:var(--agg)}
  #instlist .iid{color:var(--muted);font-size:10px;font-family:ui-monospace,Menlo,monospace;margin-left:6px}
  #instlist .ivals{color:var(--muted);font-size:11px;font-family:ui-monospace,Menlo,monospace;margin-top:2px}
  #instlist .ihint{color:var(--muted);font-size:11px}
  #inspector{margin-top:8px}
  #inspector .card{border:1px solid var(--agg);border-radius:8px;padding:8px 10px;background:#22b07d0d}
  #inspector .chead{font-size:12px;font-weight:600;color:var(--agg);margin-bottom:5px}
  #inspector .frow{display:flex;justify-content:space-between;font-size:12px;font-family:ui-monospace,Menlo,monospace;padding:1px 0}
  #inspector .fk{color:var(--muted)}
  #inspector .old{color:var(--muted);text-decoration:line-through;opacity:.55}
  #inspector .new{color:var(--agg)}
  #inspector .hist{margin-top:6px;font-size:11px;color:var(--muted)}
  #inspector .hh{text-transform:uppercase;letter-spacing:.05em;font-size:10px;margin-bottom:2px}
  #inspector .hrow{font-family:ui-monospace,Menlo,monospace;padding:1px 0;border-top:1px solid var(--line)}
  #inspector .hrow b{color:var(--cmd)}
</style>
</head>
<body>
<aside>
  <div class="brand">
    <h1>Wissensgraph</h1>
    <small>Event-Modeling-Board · Contexts › Aggregate › Funktionen</small><br>
    <span class="badge" id="routing"></span>
  </div>
  <div class="scroll">
    <div class="sec">
      <h2>Nachricht schicken<span class="livebadge" id="livebadge">● LIVE-Runtime</span></h2>
      <select id="trigsel"></select>
      <div id="cmdform"></div>
      <div class="ctrls">
        <button id="prev" title="zurück">⏮</button>
        <button id="play" class="primary" title="abspielen">▶</button>
        <button id="next" title="weiter">⏭</button>
        <button id="reset" title="zurücksetzen">⟲</button>
        <button id="cov" title="Abdeckung grün/grau (welche Zweige je gefeuert)">▦</button>
      </div>
      <div class="stepinfo" id="stepinfo"><span class="n">Trigger wählen und ▶ / ⏭ drücken.</span></div>
      <div id="states"></div>
    </div>
    <div class="sec" id="aggsec" style="display:none">
      <h2>Aggregate <span style="opacity:.5;font-weight:400;text-transform:none;letter-spacing:0">· angelegte Instanzen</span></h2>
      <div id="instlist"></div>
      <div id="inspector"></div>
    </div>
    <div class="sec">
      <h2>Bounded Contexts</h2>
      <div class="mini"><button id="ctx-all">alle</button><button id="ctx-none">keine</button><button id="ctx-saga">nur Sagas</button></div>
      <div id="ctxlist"></div>
    </div>
    <div class="sec">
      <h2>Legende</h2>
      <div class="legend">
        <span><i class="dot" style="background:var(--cmd)"></i>Funktion (Command rein)</span>
        <span><i class="dot" style="background:var(--evt)"></i>Event raus</span>
        <span><i class="dot" style="background:var(--rej)"></i>Ablehnung</span>
        <span><i class="dot" style="background:var(--agg)"></i>Aggregat</span>
        <span><i class="dot" style="background:var(--proc)"></i>Saga</span>
        <span><i class="dot" style="background:var(--proj)"></i>Projektion</span>
      </div>
      <details>
        <summary>JSON abfragen</summary>
        <div style="color:var(--muted);font-size:11px;margin-top:6px">
          <code>window.GRAPH</code> in der Konsole:<br>
          <code>GRAPH.nodes.filter(n=>n.context==='Konto')</code><br>
          <code>GRAPH.edges.filter(e=>e.kind==='produces')</code>
        </div>
      </details>
    </div>
  </div>
</aside>
<main>
  <canvas id="cv"></canvas>
  <div id="tip"></div>
  <div id="hud">Scrollen = Zoom · Ziehen = Pan · Hover = Details</div>
</main>

<script>
const GRAPH = /*__GRAPH_JSON__*/;
window.GRAPH = GRAPH;

const nodes=GRAPH.nodes, edges=GRAPH.edges;
const N=Object.fromEntries(nodes.map(n=>[n.id,n]));
const nodeIdOf=(k,name)=>{ const n=nodes.find(n=>n.kind===k&&n.name===name); return n?n.id:null; };
const esc=s=>(s??'').replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]));
const contexts=GRAPH.meta.contexts.slice();
const ctxCount={}; nodes.forEach(n=>ctxCount[n.context]=(ctxCount[n.context]||0)+1);
function ctxColor(c){ let h=0; for(const ch of c) h=(h*31+ch.charCodeAt(0))%360; return `hsl(${h} 55% 58%)`; }
const sagaContexts=new Set(nodes.filter(n=>n.kind==='process').map(n=>n.context)
  .concat(edges.filter(e=>e.kind==='sends'||e.kind==='compensates').flatMap(e=>[N[e.from].context,N[e.to].context])));
const visibleCtx=new Set(contexts); const isVis=c=>visibleCtx.has(c);

(function(){ const b=document.getElementById('routing'); const auth=GRAPH.meta.routingSource==='GeneratedCommandRouting';
  b.className='badge '+(auth?'auth':'fallback'); b.textContent='Routing: '+GRAPH.meta.routingSource; })();

// ── Kontext-Filter ────────────────────────────────────────────────────────────
const ctxlist=document.getElementById('ctxlist');
contexts.forEach(c=>{ const row=document.createElement('label'); row.className='row';
  row.innerHTML=`<input type="checkbox" checked data-c="${c}"><i class="sw" style="background:${ctxColor(c)}"></i>${c}<span class="ct">${ctxCount[c]}</span>`;
  row.querySelector('input').onchange=e=>{ e.target.checked?visibleCtx.add(c):visibleCtx.delete(c); applyFilter(); };
  ctxlist.appendChild(row); });
function setAllCtx(pred){ visibleCtx.clear(); ctxlist.querySelectorAll('input').forEach(i=>{ const on=pred(i.dataset.c); i.checked=on; if(on)visibleCtx.add(i.dataset.c); }); applyFilter(); }
document.getElementById('ctx-all').onclick=()=>setAllCtx(()=>true);
document.getElementById('ctx-none').onclick=()=>setAllCtx(()=>false);
document.getElementById('ctx-saga').onclick=()=>setAllCtx(c=>sagaContexts.has(c));
function applyFilter(){ computeBoard(); needFit=true; resize(); }

// ── Board-Layout (deterministisch, verschachtelt) ────────────────────────────
const SZ={HW:214,HHEAD:24,ROW:18,HPAD:8,GAPH:12,APAD:14,AHEAD:26,CPAD:18,CHEAD:32,GAPA:14,GAPC:44,LANEH:66,LHEAD:32,LROW:30,LANEGAP:90,LANEW:186,PROCW:300,LANEGX:16,MAXROW:3000};
let board={};

const cmdsOfAgg=name=>nodes.filter(n=>n.kind==='command'&&n.command&&n.command.routedTo===name);
const aggsOfCtx=c=>nodes.filter(n=>n.kind==='aggregate'&&n.context===c);
function measureHandler(cmd){ return SZ.HHEAD + Math.max(1,(cmd.command.produces||[]).length)*SZ.ROW + SZ.HPAD; }
function measureAgg(agg){ const cs=cmdsOfAgg(agg.name); let h=SZ.AHEAD+SZ.APAD; cs.forEach(c=>h+=measureHandler(c)+SZ.GAPH); if(cs.length)h-=SZ.GAPH; else h+=28; return {h:h+SZ.APAD, cmds:cs}; }
function measureCtx(c){ const ags=aggsOfCtx(c); const ms=ags.map(measureAgg); let h=SZ.CHEAD+SZ.CPAD; ms.forEach(m=>h+=m.h+SZ.GAPA); if(ags.length)h-=SZ.GAPA; h+=SZ.CPAD; return {w:SZ.HW+SZ.APAD*2+SZ.CPAD*2, h, ags, ms}; }

function computeBoard(){
  board={ctx:[],agg:{},handler:{},pillsByEvt:{},lane:{},edges:[],pick:[],bounds:{minx:0,miny:0,maxx:0,maxy:0}};
  const visCtx=contexts.filter(c=>isVis(c)&&aggsOfCtx(c).length>0);
  const measured=visCtx.map(c=>({c,m:measureCtx(c)}));
  // Umbruch-Grenze der Lane aus der geschätzten Inhaltsbreite.
  let est=0; measured.forEach(({m})=>est+=m.w+SZ.GAPC);
  const rightBound=Math.max(Math.min(est,SZ.MAXROW),SZ.LANEW*4);
  // Obere Lane ZUERST (Sagas haben variable Höhe je Regelzahl) → bestimmt, wo die Contexts beginnen.
  placeLane([...nodes.filter(n=>n.kind==='process'&&isVis(n.context)), ...nodes.filter(n=>n.kind==='pipeline'&&isVis(n.context))], 0, rightBound);
  let topBottom=SZ.LANEH; Object.values(board.lane).forEach(b=>topBottom=Math.max(topBottom,b.y+b.h));
  const midTop=topBottom+SZ.LANEGAP;
  let cx=0,rowY=0,rowH=0;
  measured.forEach(({c,m})=>{
    if(cx>0 && cx+m.w>SZ.MAXROW){ cx=0; rowY+=rowH+SZ.GAPC; rowH=0; }
    placeContext(c,m,cx,midTop+rowY);
    cx+=m.w+SZ.GAPC; rowH=Math.max(rowH,m.h);
  });
  const contentBottom=midTop+rowY+rowH;
  placeLane(nodes.filter(n=>n.kind==='projection'&&isVis(n.context)), contentBottom+SZ.LANEGAP, rightBound);
  buildEdges();
  computeBounds();
}
function placeContext(c,m,ox,oy){
  board.ctx.push({c,x:ox,y:oy,w:m.w,h:m.h});
  let ay=oy+SZ.CHEAD; const ax=ox+SZ.CPAD;
  m.ags.forEach((agg,i)=>{ const am=m.ms[i]; const aw=SZ.HW+SZ.APAD*2;
    board.agg[agg.id]={x:ax,y:ay,w:aw,h:am.h,node:agg}; board.pick.push({x:ax,y:ay,w:aw,h:am.h,node:agg});
    let hy=ay+SZ.AHEAD; const hx=ax+SZ.APAD;
    am.cmds.forEach(cmd=>{ const hh=measureHandler(cmd);
      board.handler[cmd.id]={x:hx,y:hy,w:SZ.HW,h:hh,node:cmd,portIn:{x:hx,y:hy+SZ.HHEAD/2}};
      board.pick.unshift({x:hx,y:hy,w:SZ.HW,h:hh,node:cmd});
      (cmd.command.produces||[]).forEach((o,k)=>{ const py=hy+SZ.HHEAD+k*SZ.ROW; const evtId=nodeIdOf('event',o.event);
        const pill={cmdId:cmd.id,event:o.event,persisted:o.persisted,evtId,x:hx+8,y:py,w:SZ.HW-16,h:SZ.ROW,portOut:{x:hx+SZ.HW,y:py+SZ.ROW/2}};
        if(evtId)(board.pillsByEvt[evtId] ||= []).push(pill); });
      hy+=hh+SZ.GAPH; });
    ay+=am.h+SZ.GAPA; });
}
const RF=9, RLH=13; // Regel-Font + Zeilenhöhe (Weltkoordinaten)
function laneW(n){ return n.kind==='process' ? SZ.PROCW : SZ.LANEW; }
function wrapLines(s,maxW){ ctx.font=RF+'px -apple-system,sans-serif'; const ws=s.split(' '); const out=[]; let cur='';
  ws.forEach(w=>{ const t=cur?cur+' '+w:w; if(!cur||ctx.measureText(t).width<=maxW)cur=t; else {out.push(cur);cur=w;} }); if(cur)out.push(cur); return out; }
function ruleLines(r,innerW){
  const cond='Auf '+(r.when||[]).join(' + ')+(r.join==='count'?' (alle '+(r.sammel||'')+')':'');
  const send='→ '+(r.fanOut?'je ':'')+r.sends+(r.compensates?'  ↩ '+r.compensates:'');
  return wrapLines(cond,innerW).map(t=>['c',t]).concat(wrapLines(send,innerW).map(t=>['s',t])); }
function laneH(n){ if(n.kind!=='process') return SZ.LANEH;
  const innerW=SZ.PROCW-24; let lines=0; (n.process.rules||[]).forEach(r=>lines+=ruleLines(r,innerW).length);
  return SZ.LHEAD + Math.max(1,lines)*RLH + (n.process.rules||[]).length*4 + 6; }
function placeLane(items,y,rightBound){ let x=0,yy=y,rowMax=0;
  items.forEach(n=>{ const h=laneH(n), w=laneW(n);
    if(x>0&&x+w>rightBound){x=0;yy+=rowMax+SZ.LANEGX;rowMax=0;}
    board.lane[n.id]={x,y:yy,w,h,node:n}; board.pick.push({x,y:yy,w,h,node:n});
    rowMax=Math.max(rowMax,h); x+=w+SZ.LANEGX; }); }
function clip(s,maxW){ if(ctx.measureText(s).width<=maxW)return s; let t=s; while(t.length>1&&ctx.measureText(t+'…').width>maxW)t=t.slice(0,-1); return t+'…'; }
const topC=r=>({x:r.x+r.w/2,y:r.y}), botC=r=>({x:r.x+r.w/2,y:r.y+r.h});
function buildEdges(){ const E=[];
  edges.forEach(e=>{
    if(e.kind==='sends'||e.kind==='compensates'||e.kind==='pipelineEmits'){ const s=board.lane[e.from],h=board.handler[e.to];
      if(s&&h)E.push({p1:botC(s),p2:h.portIn,kind:e.kind,key:edgeKey(e)}); }
    else if(e.kind==='triggers'||e.kind==='advances'){ const s=board.lane[e.to],pills=board.pillsByEvt[e.from]||[];
      if(s)pills.forEach(p=>E.push({p1:p.portOut,p2:topC(s),kind:e.kind,key:edgeKey(e)})); }
    else if(e.kind==='consumedBy'){ const pr=board.lane[e.to],pills=board.pillsByEvt[e.from]||[];
      if(pr)pills.forEach(p=>E.push({p1:p.portOut,p2:topC(pr),kind:e.kind,key:edgeKey(e)})); }
  }); board.edges=E; }
function computeBounds(){ let a=1e9,b=1e9,c=-1e9,d=-1e9;
  const acc=r=>{a=Math.min(a,r.x);b=Math.min(b,r.y);c=Math.max(c,r.x+r.w);d=Math.max(d,r.y+r.h);};
  board.ctx.forEach(acc); Object.values(board.lane).forEach(acc);
  if(a>c){a=b=0;c=d=100;} board.bounds={minx:a,miny:b,maxx:c,maxy:d}; }
const edgeKey=e=>e.from+'>'+e.to+'>'+e.kind;

// ── Canvas / Kamera ──────────────────────────────────────────────────────────
let W=1000,H=700; const cv=document.getElementById('cv'), ctx=cv.getContext('2d');
const cam={x:0,y:0,zoom:1}; let DPR=Math.max(1,window.devicePixelRatio||1); let needFit=true;
function resize(){ const r=cv.getBoundingClientRect(); W=r.width;H=r.height; cv.width=W*DPR;cv.height=H*DPR;
  if(needFit&&W>0&&H>0){ fit(); needFit=false; } render(); }
function fit(){ const b=board.bounds; if(W<=0||H<=0)return; const pad=70, gw=b.maxx-b.minx+pad*2, gh=b.maxy-b.miny+pad*2;
  cam.zoom=Math.max(.08,Math.min(W/gw,H/gh,1.3)); cam.x=(b.minx+b.maxx)/2; cam.y=(b.miny+b.maxy)/2; }
const S=(x,y)=>[(x-cam.x)*cam.zoom+W/2,(y-cam.y)*cam.zoom+H/2];
const Wld=(sx,sy)=>[(sx-W/2)/cam.zoom+cam.x,(sy-H/2)/cam.zoom+cam.y];

const KIND_COL={command:'#e0902b',aggregate:'#22b07d',process:'#8b5cf6',projection:'#14b8a6',pipeline:'#f97316'};
const EDGE_COL={sends:'#e0902b',compensates:'#d64545',pipelineEmits:'#f97316',triggers:'#8b5cf6',advances:'#8b5cf6',consumedBy:'#14b8a6'};

function rr(x,y,w,h,r){ ctx.beginPath(); ctx.moveTo(x+r,y); ctx.arcTo(x+w,y,x+w,y+h,r); ctx.arcTo(x+w,y+h,x,y+h,r); ctx.arcTo(x,y+h,x,y,r); ctx.arcTo(x,y,x+w,y,r); ctx.closePath(); }
function render(){
  ctx.setTransform(DPR,0,0,DPR,0,0); ctx.clearRect(0,0,W,H);
  ctx.lineJoin='round'; ctx.textBaseline='middle';
  const z=cam.zoom;
  // 1) Kanten (hinter den Boxen)
  drawEdges(false);
  // 2) Kontext-Container
  board.ctx.forEach(b=>{ const [x,y]=S(b.x,b.y);
    ctx.fillStyle=ctxColor(b.c).replace('hsl','hsla').replace(')',' / 8%)'); ctx.strokeStyle=ctxColor(b.c).replace('hsl','hsla').replace(')',' / 40%)'); ctx.lineWidth=1.4;
    rr(x,y,b.w*z,b.h*z,12*z); ctx.fill(); ctx.stroke();
    ctx.fillStyle=ctxColor(b.c); ctx.font=`700 ${12*z}px -apple-system,sans-serif`; ctx.textAlign='left';
    ctx.fillText('◆ '+b.c.toUpperCase(), x+12*z, y+16*z); });
  // 3) Aggregate
  Object.values(board.agg).forEach(b=>{ const [x,y]=S(b.x,b.y); const act=simActive.has(b.node.id);
    ctx.fillStyle='#22b07d18'; ctx.strokeStyle=act?'#22b07d':'#22b07d66'; ctx.lineWidth=act?2:1.3;
    rr(x,y,b.w*z,b.h*z,10*z); ctx.fill(); ctx.stroke();
    ctx.fillStyle='#2fd18f'; ctx.font=`700 ${11.5*z}px -apple-system,sans-serif`; ctx.textAlign='left';
    ctx.fillText('▣ '+b.node.name, x+10*z, y+13*z); });
  // 4) Funktionen (Handler): Command-Kopf + OneOf-Events
  Object.values(board.handler).forEach(b=>{ const cmd=b.node; const [x,y]=S(b.x,b.y); const act=simActive.has(cmd.id);
    ctx.globalAlpha=simMode&&!act?.4:1;
    // Kopf = Command (rein)
    ctx.fillStyle=act?'#e0902b':'#e0902b22'; ctx.strokeStyle='#e0902b'; ctx.lineWidth=act?2:1.2;
    rr(x,y,b.w*z,SZ.HHEAD*z,7*z); ctx.fill(); ctx.stroke();
    ctx.fillStyle=act?'#1a1206':'#f0b464'; ctx.font=`700 ${11*z}px -apple-system,sans-serif`; ctx.textAlign='left';
    ctx.fillText('▸ '+cmd.name, x+8*z, y+SZ.HHEAD*z/2);
    // Ausgänge = Events (raus), das gekapselte OneOf
    (cmd.command.produces||[]).forEach((o,k)=>{ const py=y+(SZ.HHEAD+k*SZ.ROW)*z; const evtId=nodeIdOf('event',o.event);
      const on=simActive.has(evtId); const col=o.persisted?'#3b82f6':'#6b7480';
      const gedeckt=COV.has('produces:'+cmd.name+'->'+o.event); const a0=ctx.globalAlpha; if(covMode&&!gedeckt)ctx.globalAlpha=a0*.28;
      ctx.fillStyle=on?col:(o.persisted?'#3b82f622':'#6b748022'); ctx.strokeStyle=col; ctx.lineWidth=on?1.8:1; if(!o.persisted)ctx.setLineDash([3,2]);
      rr(x+8*z,py+2*z,(b.w-16)*z,(SZ.ROW-3)*z,5*z); ctx.fill(); ctx.stroke(); ctx.setLineDash([]);
      ctx.fillStyle=on?'#fff':(o.persisted?'#9cc2ff':'#aab3c0'); ctx.font=`${10*z}px -apple-system,sans-serif`;
      ctx.fillText((o.persisted?'● ':'⃠ ')+o.event, x+15*z, py+SZ.ROW*z/2);
      if(covMode&&gedeckt){ ctx.fillStyle='#22c55e'; ctx.beginPath(); ctx.arc(x+(b.w-14)*z,py+SZ.ROW*z/2,2.4*z,0,7); ctx.fill(); }
      ctx.globalAlpha=a0; });
    ctx.globalAlpha=1; });
  // 5) Lanes: Sagas (oben) + Projektionen (unten)
  Object.values(board.lane).forEach(b=>{ const n=b.node; const [x,y]=S(b.x,b.y); const act=simActive.has(n.id); const col=KIND_COL[n.kind];
    ctx.globalAlpha=simMode&&!act?.4:1;
    ctx.fillStyle=col+ (act?'':'22'); ctx.strokeStyle=col; ctx.lineWidth=act?2.4:1.4;
    rr(x,y,b.w*z,b.h*z,9*z); ctx.fill(); ctx.stroke();
    if(n.kind==='process'){
      // Kopf: Name + Muster/Auslöser
      ctx.textAlign='left'; ctx.fillStyle=act?'#fff':'#e6edf3'; ctx.font=`700 ${11*z}px -apple-system,sans-serif`;
      ctx.fillText(clip('⬡ '+n.name,(b.w-18)*z), x+10*z, y+14*z);
      ctx.fillStyle=col; ctx.font=`${8.4*z}px -apple-system,sans-serif`;
      ctx.fillText(clip(n.process.pattern+' · Auslöser '+n.process.trigger,(b.w-18)*z), x+10*z, y+26*z);
      // Transitionen: Auf <Events> → Sende <Command> — voll umgebrochen, nie abgeschnitten.
      const innerW=b.w-24; let ly=y+SZ.LHEAD*z;
      (n.process.rules||[]).forEach(r=>{
        ruleLines(r,innerW).forEach(([k,t])=>{
          ctx.font=`${RF*z}px -apple-system,sans-serif`;
          ctx.fillStyle = k==='c' ? '#9cc2ff' : '#f0b464';
          ctx.fillText(t, x+(k==='c'?12:18)*z, ly+RF*z);
          ly += RLH*z;
        });
        ly += 4*z; // Abstand zwischen Regeln
      });
    } else {
      ctx.textAlign='center'; ctx.fillStyle=act?'#fff':'#e6edf3'; ctx.font=`700 ${11.5*z}px -apple-system,sans-serif`;
      const icon=n.kind==='projection'?'▤ ':'⛁ '; ctx.fillText(icon+n.name, x+b.w*z/2, y+b.h*z/2-6*z);
      ctx.fillStyle=col; ctx.font=`${9.5*z}px -apple-system,sans-serif`;
      const sub=n.kind==='projection'?n.projection.subscriberId:'Pipeline';
      ctx.fillText(sub, x+b.w*z/2, y+b.h*z/2+9*z);
    }
    ctx.textAlign='left'; ctx.globalAlpha=1; });
  // 6) aktive Kanten oben drauf
  if(simMode) drawEdges(true);
}
function drawEdges(activeOnly){ const z=cam.zoom;
  board.edges.forEach(e=>{ const act=simActiveEdges.has(e.key); if(activeOnly&&!act)return; if(!activeOnly&&act&&simMode)return;
    const [x1,y1]=S(e.p1.x,e.p1.y),[x2,y2]=S(e.p2.x,e.p2.y); const my=(y1+y2)/2;
    ctx.strokeStyle=act?EDGE_COL[e.kind]:(simMode?'#232b36':'#2c3542'); ctx.lineWidth=act?2.4:1;
    ctx.globalAlpha=act?1:(simMode?.5:.6); if(e.kind==='compensates')ctx.setLineDash([5,3]);
    ctx.beginPath(); ctx.moveTo(x1,y1); ctx.bezierCurveTo(x1,my,x2,my,x2,y2); ctx.stroke(); ctx.setLineDash([]);
    // Pfeilspitze
    const ang=Math.atan2(y2-my,0.001); const s=5.5; ctx.fillStyle=ctx.strokeStyle;
    ctx.beginPath(); ctx.moveTo(x2,y2); ctx.lineTo(x2-Math.cos(ang-.5)*s,y2-Math.sin(ang-.5)*s); ctx.lineTo(x2-Math.cos(ang+.5)*s,y2-Math.sin(ang+.5)*s); ctx.closePath(); ctx.fill();
    ctx.globalAlpha=1; }); }

// ── Interaktion ───────────────────────────────────────────────────────────────
let hover=null,panning=false,last=[0,0];
cv.addEventListener('mousedown',ev=>{ panning=true; cv.classList.add('grabbing'); last=[ev.offsetX,ev.offsetY]; });
window.addEventListener('mousemove',ev=>{ const r=cv.getBoundingClientRect(); const ox=ev.clientX-r.left,oy=ev.clientY-r.top;
  if(panning){ cam.x-=(ox-last[0])/cam.zoom; cam.y-=(oy-last[1])/cam.zoom; last=[ox,oy]; render(); }
  else { const [wx,wy]=Wld(ox,oy); const n=pick(wx,wy); hover=n; showTip(n,ev.clientX,ev.clientY); } });
window.addEventListener('mouseup',()=>{ panning=false; cv.classList.remove('grabbing'); });
cv.addEventListener('wheel',ev=>{ ev.preventDefault(); const [wx,wy]=Wld(ev.offsetX,ev.offsetY);
  cam.zoom*=Math.exp(-ev.deltaY*.001); cam.zoom=Math.max(.06,Math.min(4,cam.zoom));
  const [nx,ny]=Wld(ev.offsetX,ev.offsetY); cam.x+=wx-nx; cam.y+=wy-ny; render(); },{passive:false});
function pick(wx,wy){ for(const p of board.pick){ if(wx>=p.x&&wx<=p.x+p.w&&wy>=p.y&&wy<=p.y+p.h)return p.node; } return null; }
const tip=document.getElementById('tip');
function showTip(n,cx,cy){ if(!n){tip.style.display='none';return;}
  let rows='';
  if(n.command){ const p=n.command.produces||[]; const ok=p.filter(o=>o.persisted).map(o=>o.event), rej=p.filter(o=>!o.persisted).map(o=>o.event);
    rows=`↳ ${n.command.routedTo||'—'} · ${(n.command.origin||[]).join(', ')}<br>OneOf&lt; ${ok.join(', ')}${rej.length?' | ⃠ '+rej.join(', '):''} &gt;`; }
  else if(n.aggregate){ rows=`${(n.aggregate.handles||[]).length} Funktionen · ${(n.aggregate.emits||[]).length} Events`; }
  else if(n.process){ rows=`${n.process.pattern} · Auslöser ${n.process.trigger} · ${n.process.rules.length} Regeln`; }
  else if(n.projection){ rows=`Subscriber ${n.projection.subscriberId} · konsumiert ${(n.projection.consumes||[]).join(', ')}`; }
  else if(n.pipeline){ rows=`${n.pipeline.handles.length} Handles`; }
  tip.innerHTML=`<div class="k">${n.kind} · ${n.context}</div><b>${esc(n.name)}</b><div class="rows">${rows}</div>`;
  tip.style.display='block'; tip.style.left=(cx+14)+'px'; tip.style.top=(cy+14)+'px'; }

// ── Simulation ────────────────────────────────────────────────────────────────
let frames=[],frameIdx=-1,simMode=false,playing=null;
let simActive=new Set(),simActiveEdges=new Set();
function out(id){ return edges.filter(e=>e.from===id); }
function computeFrames(startId){
  const start=N[startId]; const activated=new Set([startId]); const firedRules=new Set();
  const note0 = start.kind==='command' ? `Command <b>${start.name}</b> geschickt → ${start.command.routedTo||'?'}` : `Trigger <b>${start.name}</b> tritt ein`;
  const fr=[{add:[startId],edges:[],note:note0}];
  let guard=0;
  while(guard++<40){ const add=new Set(),used=[];
    nodes.filter(n=>n.kind==='process'&&activated.has(n.id)).forEach(p=>{ p.process.rules.forEach((r,ri)=>{ const key=p.id+'#'+ri; if(firedRules.has(key))return;
      const whenIds=r.when.map(w=>nodeIdOf('event',w)).filter(Boolean); const sammel=r.sammel?nodeIdOf('event',r.sammel):null;
      if(!whenIds.every(id=>activated.has(id))||(sammel&&!activated.has(sammel)))return; firedRules.add(key);
      const cmdId=nodeIdOf('command',r.sends); if(cmdId&&!activated.has(cmdId)){ add.add(cmdId); const se=edges.find(e=>e.kind==='sends'&&e.from===p.id&&e.to===cmdId); if(se)used.push(se); } }); });
    fr[fr.length-1].add.forEach(id=>{ const n=N[id];
      if(n.kind==='event'){ out(id).forEach(e=>{ if(['triggers','advances','consumedBy'].includes(e.kind)){ used.push(e); if(!activated.has(e.to))add.add(e.to); } }); }
      else if(n.kind==='command'){ out(id).filter(e=>e.kind==='routedTo').forEach(e=>{ used.push(e); if(!activated.has(e.to))add.add(e.to); });
        out(id).filter(e=>e.kind==='produces').forEach(e=>{ const ev=N[e.to]; if(ev.event&&!ev.event.persisted)return; used.push(e); if(!activated.has(e.to))add.add(e.to); }); } });
    if(add.size===0&&used.length===0)break;
    add.forEach(id=>activated.add(id)); fr.push({add:[...add],edges:used,note:noteOf([...add])}); }
  return fr;
}
function noteOf(add){ const by=k=>add.map(id=>N[id]).filter(n=>n.kind===k).map(n=>n.name);
  const p=[],c=by('command'),e=by('event'),pr=by('process'),a=by('aggregate'),pj=by('projection');
  if(pr.length)p.push(`Saga <b>${pr.join(', ')}</b> erwacht`); if(c.length)p.push(`Funktion <b>${c.join(', ')}</b> aufgerufen`);
  if(a.length)p.push(`Aggregat ${a.join(', ')} verarbeitet`); if(e.length)p.push(`Event <b>${e.join(', ')}</b> raus`);
  if(pj.length)p.push(`Projektion ${pj.join(', ')} aktualisiert`); return p.join(' · ')||'—'; }
function gotoFrame(i){ frameIdx=Math.max(0,Math.min(frames.length-1,i));
  simActive=new Set(); simActiveEdges=new Set(); let changed=false;
  for(let k=0;k<=frameIdx;k++){ frames[k].add.forEach(id=>simActive.add(id)); (frames[k].edges||[]).forEach(e=>simActiveEdges.add(edgeKey(e))); }
  edges.forEach(e=>{ if(simActive.has(e.from)&&simActive.has(e.to))simActiveEdges.add(edgeKey(e)); }); // aktives Teilnetz (auch Live)
  frames[frameIdx].add.forEach(id=>{ const c=N[id].context; if(!visibleCtx.has(c)){ visibleCtx.add(c); const inp=ctxlist.querySelector(`input[data-c="${c}"]`); if(inp)inp.checked=true; changed=true; } });
  if(changed)computeBoard();
  const si=document.getElementById('stepinfo'); si.innerHTML=`<span class="n">Schritt ${frameIdx} / ${frames.length-1}</span><br>${frames[frameIdx].note}`;
  document.getElementById('prev').disabled=frameIdx<=0; document.getElementById('next').disabled=frameIdx>=frames.length-1; render(); }
function startSim(t){ simMode=true; frames=computeFrames(t); gotoFrame(0); }
function stopSim(){ simMode=false; simActive.clear(); simActiveEdges.clear(); playing&&clearInterval(playing); playing=null;
  document.getElementById('play').textContent='▶'; document.getElementById('stepinfo').innerHTML='<span class="n">Trigger wählen und ▶ / ⏭ drücken.</span>'; render(); }
const trigsel=document.getElementById('trigsel');
const triggerEvents=[...new Set(edges.filter(e=>e.kind==='triggers').map(e=>e.from))]
  .map(id=>({id,name:N[id].name,proc:edges.filter(e=>e.kind==='triggers'&&e.from===id).map(e=>N[e.to].name).join(', ')})).sort((a,b)=>a.name.localeCompare(b.name));
let selHtml='<option value="">— Nachricht wählen —</option>';
selHtml+='<optgroup label="▶ Auslöser-Events (starten eine Saga)">'
  + triggerEvents.map(t=>`<option value="${t.id}">${t.name} → ${t.proc}</option>`).join('') + '</optgroup>';
const cmdByAgg={}; nodes.filter(n=>n.kind==='command'&&n.command.routedTo).forEach(c=>{ (cmdByAgg[c.command.routedTo] ||= []).push(c); });
Object.keys(cmdByAgg).sort().forEach(a=>{ selHtml+=`<optgroup label="▸ Commands → ${a}">`
  + cmdByAgg[a].sort((x,y)=>x.name.localeCompare(y.name)).map(c=>`<option value="${c.id}">${c.name}</option>`).join('') + '</optgroup>'; });
trigsel.innerHTML=selHtml;

// ── LIVE-Runtime (SimHost): echte, wertabhängige Ausführung ─────────────────
const SID='board-'+Math.floor(performance.now());
let LIVE=false; const SCHEMA={};
let covMode=false; const COV=new Set();
function refreshCoverage(){ fetch('/api/coverage').then(r=>r.ok?r.json():[]).then(ids=>{ COV.clear(); (ids||[]).forEach(i=>COV.add(i)); render(); }).catch(()=>{}); }
document.getElementById('cov').onclick=()=>{ covMode=!covMode; document.getElementById('cov').classList.toggle('primary',covMode); if(covMode)refreshCoverage(); else render(); };
const setStep=html=>{ document.getElementById('stepinfo').innerHTML=html; };
fetch('/api/schema').then(r=>r.ok?r.json():null).then(list=>{ if(!list)return;
  LIVE=true; list.forEach(c=>SCHEMA[c.name]=c); document.getElementById('livebadge').style.display='inline-block';
  document.getElementById('aggsec').style.display='block';
  if(trigsel.value&&N[trigsel.value]?.kind==='command')renderForm(N[trigsel.value]);
  refreshState();
}).catch(()=>{});

// ── Instanz-Inspektor: konkrete Aggregate, ihre Werte, ihre Änderungen ──────
const INST={}; const HIST={}; let selId=null;
const fmt=v=>v===true?'true':v===false?'false':(v==null?'—':v);
function newGuid(){ return (self.crypto&&crypto.randomUUID)?crypto.randomUUID():'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g,c=>{const r=Math.random()*16|0;return (c==='x'?r:(r&3|8)).toString(16);}); }
function mergeStates(list){ (list||[]).forEach(s=>{ INST[s.id]={id:s.id,typ:s.aggregate,label:s.label,fields:s.fields}; }); if(!selId&&list&&list.length)selId=list[0].id; }
function refreshState(){ fetch('/api/state?sessionId='+encodeURIComponent(SID)).then(r=>r.ok?r.json():[]).then(list=>{ mergeStates(list); renderInstances(); renderInspector(); }).catch(()=>{}); }
function keyFields(f){ return Object.entries(f).map(([k,v])=>k+' '+fmt(v)).join(' · '); }
function renderInstances(){ const el=document.getElementById('instlist'); if(!el)return;
  const arr=Object.values(INST); if(!arr.length){ el.innerHTML='<div class="ihint">Noch keine — schick ein Erzeugungs-Command (z.B. EroeffneKonto).</div>'; return; }
  const byTyp={}; arr.forEach(x=>{(byTyp[x.typ]=byTyp[x.typ]||[]).push(x);});
  let h=''; Object.keys(byTyp).sort().forEach(t=>{ h+='<div class="itype">▣ '+t+'</div>';
    byTyp[t].sort((a,b)=>a.label.localeCompare(b.label,undefined,{numeric:true})).forEach(x=>{
      h+='<div class="inst'+(x.id===selId?' sel':'')+'" data-id="'+x.id+'"><b>'+x.label+'</b><span class="iid">'+x.id.slice(0,8)+'</span><div class="ivals">'+keyFields(x.fields)+'</div></div>'; }); });
  el.innerHTML=h; el.querySelectorAll('.inst').forEach(d=>d.onclick=()=>{ selId=d.dataset.id; renderInstances(); renderInspector(); }); }
function renderInspector(){ const el=document.getElementById('inspector'); if(!el)return;
  const x=INST[selId]; if(!x){ el.innerHTML=''; return; }
  const last=(HIST[selId]||[]).slice(-1)[0]; const chg={}; if(last)(last.changes||[]).forEach(c=>chg[c.feld]=c);
  const rows=Object.entries(x.fields).map(([k,v])=>{ const c=chg[k];
    return '<div class="frow"><span class="fk">'+k+'</span>'+(c?'<span><span class="old">'+fmt(c.vorher)+'</span> → <b class="new">'+fmt(c.nachher)+'</b></span>':'<span>'+fmt(v)+'</span>')+'</div>'; }).join('');
  const hist=(HIST[selId]||[]).slice(-6).reverse().map(e=>'<div class="hrow"><b>'+e.command+'</b> '+((e.changes&&e.changes.length)?e.changes.map(c=>c.feld+' '+fmt(c.vorher)+'→'+fmt(c.nachher)).join(', '):'(keine Änderung)')+'</div>').join('');
  el.innerHTML='<div class="card"><div class="chead">'+x.label+' <span class="iid" style="font-weight:400">'+x.id.slice(0,8)+'</span></div>'+rows+'</div>'+(hist?'<div class="hist"><div class="hh">Änderungen</div>'+hist+'</div>':''); }
function guidFeld(fl,id,c){ const isAgg=fl.name.toLowerCase()==='aggregateid';
  // aggregateId → Instanzen des Ziel-Aggregats; Referenz-Felder (z.B. NeuesKonto) → Instanzen ANDERER Aggregate.
  const opts=Object.values(INST).filter(x=>isAgg?x.typ===c.aggregate:x.typ!==c.aggregate); const creation=c.creation&&isAgg;
  const def=creation?'__neu__':(isAgg?(opts.find(x=>x.id===selId)?selId:(opts[0]?opts[0].id:'__neu__')):(opts[0]?opts[0].id:'__neu__'));
  const o=opts.map(x=>'<option value="'+x.id+'"'+(x.id===def?' selected':'')+'>'+x.label+' · '+x.id.slice(0,8)+'</option>').join('')
    +'<option value="__neu__"'+(def==='__neu__'?' selected':'')+'>➕ neu (frische Id)</option>';
  return '<label>'+fl.name+' <span style="opacity:.55">'+(isAgg?'Aggregat':'Referenz')+'</span></label><select id="'+id+'">'+o+'</select>'; }
function guidFor(name){ let h=0; for(const ch of name)h=(h*31+ch.charCodeAt(0))>>>0; return '00000000-0000-0000-0000-'+('000000000000'+h.toString(16)).slice(-12); }
function renderForm(node){ const c=SCHEMA[node.name]; const f=document.getElementById('cmdform'); if(!c){f.innerHTML='';return;}
  f.innerHTML=c.fields.map(fl=>{ const id='f_'+fl.name; const num=['decimal','int','long'].includes(fl.type);
    if(fl.type==='bool')return `<label>${fl.name}</label><input type="checkbox" id="${id}">`;
    if(fl.type==='guid')return guidFeld(fl,id,c);
    const def=num?(/(Betrag|Saldo|Menge|Zimmer|Plaetze|Anzahl|ProZiel)/i.test(fl.name)?100:1):'';
    return `<label>${fl.name} <span style="opacity:.55">${fl.type}</span></label><input type="${num?'number':'text'}" id="${id}" value="${def}">`; }).join('')
    + `<button class="send" id="sendbtn">▶ ${node.name} schicken</button><button class="rst" id="dslbtn">🧪 Als Test</button><button class="rst" id="rstbtn">⟲ Session zurücksetzen</button>`;
  document.getElementById('sendbtn').onclick=()=>sendCommand(node);
  document.getElementById('dslbtn').onclick=dslExport;
  document.getElementById('rstbtn').onclick=resetSession;
}
function dslExport(){ fetch('/api/dsl',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:SID})})
  .then(r=>r.text()).then(code=>{ setStep('<div class="n" style="opacity:.7">Board-Session als Test (in die Zwischenablage kopiert):</div><pre style="white-space:pre-wrap;font-size:11px;line-height:1.4;color:#9fe0a0;margin:.4rem 0 0">'+code.replace(/</g,'&lt;')+'</pre>');
    navigator.clipboard&&navigator.clipboard.writeText(code).catch(()=>{}); }); }
function sendCommand(node){ const c=SCHEMA[node.name]; const values={}; let ziel=null;
  c.fields.forEach(fl=>{ const el=document.getElementById('f_'+fl.name); if(!el)return;
    let v; if(fl.type==='bool')v=el.checked;
    else if(['decimal','int','long'].includes(fl.type))v=Number(el.value);
    else if(fl.type==='guid'){ v=el.value==='__neu__'?newGuid():el.value; if(fl.name.toLowerCase()==='aggregateid')ziel=v; }
    else v=el.value;
    values[fl.name]=v; });
  fetch('/api/step',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:SID,command:node.name,values})})
    .then(r=>r.json()).then(res=>{ if(res.error){ setStep('<span class="n" style="color:#d64545">'+res.error+'</span>'); return; }
      simMode=true; frames=(res.frames||[]).map(fr=>({add:(fr.add||[]).map(it=>nodeIdOf(it.kind,it.name)).filter(Boolean),edges:[],note:fr.note})); gotoFrame(0);
      mergeStates(res.states); (res.changes||[]).forEach(ch=>{ (HIST[ch.id]=HIST[ch.id]||[]).push({command:ch.command,changes:ch.aenderungen}); });
      if(ziel&&INST[ziel])selId=ziel; renderInstances(); renderInspector(); renderForm(node); if(covMode)refreshCoverage(); });
}
function resetSession(){ fetch('/api/reset',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:SID})}).then(()=>{ Object.keys(INST).forEach(k=>delete INST[k]); Object.keys(HIST).forEach(k=>delete HIST[k]); selId=null; renderInstances(); renderInspector(); stopSim(); setStep('<span class="n">Session zurückgesetzt.</span>'); }); }

trigsel.onchange=()=>{ const v=trigsel.value; document.getElementById('cmdform').innerHTML=''; document.getElementById('states').innerHTML='';
  if(!v){ stopSim(); return; }
  if(LIVE && N[v]?.kind==='command'){ renderForm(N[v]); setStep('<span class="n">Werte eingeben und ▶ schicken.</span>'); }
  else startSim(v); };
document.getElementById('next').onclick=()=>{ if(!simMode&&trigsel.value)startSim(trigsel.value); else gotoFrame(frameIdx+1); };
document.getElementById('prev').onclick=()=>gotoFrame(frameIdx-1);
document.getElementById('reset').onclick=()=>{ if(simMode)gotoFrame(0); };
document.getElementById('play').onclick=()=>{ if(!simMode&&trigsel.value)startSim(trigsel.value);
  if(playing){clearInterval(playing);playing=null;document.getElementById('play').textContent='▶';return;}
  document.getElementById('play').textContent='⏸';
  playing=setInterval(()=>{ if(frameIdx>=frames.length-1){clearInterval(playing);playing=null;document.getElementById('play').textContent='▶';return;} gotoFrame(frameIdx+1); },900); };

// ── Start ─────────────────────────────────────────────────────────────────────
window.addEventListener('resize',()=>{DPR=Math.max(1,window.devicePixelRatio||1);needFit=true;resize();});
computeBoard(); resize();
new ResizeObserver(()=>resize()).observe(document.querySelector('main'));
requestAnimationFrame(resize); setTimeout(resize,80);

if(triggerEvents.length){ const t=triggerEvents.find(t=>t.name==='BestellungAufgegeben')||triggerEvents[0];
  const f=computeFrames(t.id); console.log('SIM '+t.name+': '+f.length+' Frames'); f.forEach((fr,i)=>console.log('  ['+i+'] '+fr.note.replace(/<[^>]+>/g,''))); }
</script>
</body>
</html>
""";
}
