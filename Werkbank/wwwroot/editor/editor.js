(function(){
  // Skalare = was der Wire trägt (aus dem Codegen, via rahmen.skalare) — keine eigene Liste im Editor.
  const SCALARS=()=>((MODEL.rahmen&&MODEL.rahmen.skalare)||[]);
  // Vertrags-Fakten aus dem Rahmen (vom Extractor aus dem Code gelesen).
  const ID_FELD=()=>((MODEL.rahmen&&MODEL.rahmen.aggregatIdFeld)||"");
  const KINDINFO={command:["Command","cmd"],event:["Event","evt"],rejection:["Ablehnung","rej"],valueobject:["Value Object","vo"],konfig:["Konfiguration","vo"],query:["Query","qry"],queryresponse:["Response","qrsp"],auftrag:["Auftrag","cmd"]};
  let MODEL={schemaVersion:"2",akteure:[],clients:[],records:[],enums:[],aggregate:[],decider:[],applier:[],sagas:[],states:[],transitions:[],readModels:[],stores:[],projektionen:[],reader:[],reaktionen:[],fluesse:[],codeKarten:[],dienste:[]};
  let NID=1;
  // Die ANSICHT (Werkbank P7.2): Positionen, Domänen-Filter, Akteur-Reihenfolge und die 🤖-Knoten — eine Datei auf dem Server
  //   (.werkbank/ansicht.json, Schlüssel = Karten-Id). Sie gehört weder zum Code noch zum Board; der Code ist die einzige Wahrheit.
  let HIDDEN=new Set(), ANSICHT={}, _ansT=null;
  async function ansichtLaden(){try{const r=await fetch("/api/werkbank/ansicht");ANSICHT=r.ok?(await r.json())||{}:{};}catch(e){ANSICHT={};}}
  function ansichtSpeichern(){if(_ansT)clearTimeout(_ansT);_ansT=setTimeout(()=>{_ansT=null;
    delete ANSICHT.llmNodes;   // 🤖 ist seit P7.4 eine Aktion am Rumpf, kein Knoten mehr
    fetch("/api/werkbank/ansicht",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(ANSICHT)}).catch(()=>{});},700);}
  function schluesselFuer(){HIDDEN=new Set(ANSICHT.hidden||[]);ladeAnsicht();}
  function saveHidden(){ANSICHT.hidden=[...HIDDEN];ansichtSpeichern();}
  let FILTER_OPEN=false, MM=null;
  function domHue(k){let h=0;for(let i=0;i<(k||"").length;i++)h=(h*31+k.charCodeAt(i))>>>0;return h%360;}

  const listeZuText=xs=>(xs||[]).join(", ");
  const textZuListe=t=>(t||"").split(",").map(s=>s.trim()).filter(Boolean);
  const kindLabel=k=>(KINDINFO[k]||["?","vo"])[0];
  const kindKlasse=k=>(KINDINFO[k]||["?","vo"])[1];
  function normalize(m){m=m||{};m.records=m.records||[];m.enums=m.enums||[];m.aggregate=m.aggregate||[];m.decider=m.decider||[];m.applier=m.applier||[];m.sagas=m.sagas||[];m.states=m.states||[];m.transitions=m.transitions||[];
    m.readModels=m.readModels||[];m.stores=m.stores||[];m.projektionen=m.projektionen||[];m.reader=m.reader||[];m.reaktionen=m.reaktionen||[];
    // Code-Karten (Werkbank P7.1): Typen, die das Lesen nicht als Baustein erkennt (z. B. unaufgelöster Basistyp) — kommen fertig vom Server.
    m.codeKarten=m.codeKarten||[];
    // Akteur (docs/konzept-akteure.md): wer von außen hineingibt — darf[] = die Typen seiner IDarf<T> (Command/Query/Trigger).
    m.akteure=m.akteure||[];m.akteure.forEach(a=>{if(!a._id)a._id="ak"+(NID++);a.darf=a.darf||[];});   /* art bewusst nicht vorbelegen (Herkunfts-Hash) */
    // Client (docs/konzept-akteure.md §4): die Software an der Leitung — traegt[] = Akteur-Vertrags-Teile, sendet/fragt/kenntnis = der Rand.
    // Katalog-Funktion (IFunktion): Auftrag → Ergebnis-Events; ein Prozess ruft sie mit Rufe<F> (Dann ƒ an der Regel).
    m.funktionen=m.funktionen||[];m.funktionen.forEach(f=>{if(!f._id)f._id="fk"+(NID++);f.ergebnisse=f.ergebnisse||[];f.konstruktor=f.konstruktor||[];});
    // Domänen-Dienst (IDienst, konzept-werkbank §12.3 E11): Signatur (Methoden) + Implementierungen mit Konstruktor; eine Funktions-Implementierung nimmt ihn im Konstruktor.
    m.dienste=m.dienste||[];m.dienste.forEach(d=>{if(!d._id)d._id="dn"+(NID++);d.methoden=d.methoden||[];d.methoden.forEach(x=>{x.parameter=x.parameter||[];});
      d.implementierungen=d.implementierungen||[];d.implementierungen.forEach(i=>{i.konstruktor=i.konstruktor||[];});});
    // Pipeline als Fluss (§14): Knoten (Name = Identität) mit Eingängen aus Drähten; _ids nur fürs Board.
    m.fluesse=m.fluesse||[];m.fluesse.forEach(f=>{if(!f._id)f._id="fl"+(NID++);f.knoten=f.knoten||[];f.knoten.forEach(k=>{if(!k._id)k._id="fs"+(NID++);k.eingaenge=k.eingaenge||[];k.eingaenge.forEach(e=>{e.draehte=e.draehte||[];});});});
    m.clients=m.clients||[];m.clients.forEach(c=>{if(!c._id)c._id="cl"+(NID++);c.traegt=c.traegt||[];c.sendet=c.sendet||[];c.fragt=c.fragt||[];c.kenntnis=c.kenntnis||[];});
    // Reaktion = emittierender Konsument (ISubscriber → IAsyncEnumerable<OneOf<Cmd>>): Trigger-Event → Handle → OneOf-Commands.
    m.reaktionen.forEach(r=>{if(!r._id)r._id="rk"+(NID++);r.handles=r.handles||[];r.handles.forEach(hd=>{hd.sends=hd.sends||[];hd.publishes=hd.publishes||[];});});
    // Jedes Objekt-Feld bekommt eine stabile _id → Feld-Ports eindeutig (auch bei gleichnamigen Feldern) und rename-fest.
    const stampFelder=arr=>(arr||[]).forEach(f=>{if(f&&!f._id)f._id="f"+(NID++);});
    m.records.forEach(r=>stampFelder(r.felder));m.aggregate.forEach(a=>stampFelder(a.state));m.states.forEach(s=>stampFelder(s.felder));m.readModels.forEach(rm=>stampFelder(rm.felder));
    m.decider.forEach(d=>{if(!d._id)d._id="d"+(NID++);});m.applier.forEach(a=>{if(!a._id)a._id="a"+(NID++);});m.states.forEach(s=>{if(!s._id)s._id="s"+(NID++);});m.transitions.forEach(t=>{if(!t._id)t._id="t"+(NID++);if(!t.dann)t.dann=[{}];});
    m.readModels.forEach(rm=>{if(!rm._id)rm._id="rm"+(NID++);});
    m.stores.forEach(st=>{if(!st._id)st._id="st"+(NID++);st.writeFns=st.writeFns||[];st.readFns=st.readFns||[];st.writeFns.forEach(f=>{if(!f._id)f._id="wf"+(NID++);f.params=f.params||[];});st.readFns.forEach(f=>{if(!f._id)f._id="rf"+(NID++);f.params=f.params||[];});});
    // Projektion-Handle nimmt je Event eine (oder mehrere) Schreib-FÄHIGKEIT als Parameter → fns[]. Der Store-Scope
    // wird daraus ABGELEITET (= die injizierten Stores), nicht mehr von Hand deklariert.
    m.projektionen.forEach(p=>{if(!p._id)p._id="pj"+(NID++);p.handles=p.handles||[];p.handles.forEach(hd=>{hd.fns=hd.fns||[];hd.publishes=hd.publishes||[];});delete p.stores;});
    m.reader.forEach(r=>{if(!r._id)r._id="rd"+(NID++);r.handles=r.handles||[];delete r.stores;
      // Reader liest genau eine Projektion (IReader<TProjection>) — expliziter Bindungs-Port.
      r.projektion=r.projektion||"";
      r.handles.forEach(hd=>{
        // Query-Handle nimmt je Query eine/mehrere Lese-Fähigkeiten als Parameter (auch über mehrere Stores) → fns[].
        hd.fns=hd.fns||[];
        // Symmetrie zur Schreibseite (decider.ergibt[]): je Query mehrere Responses als OneOf → responses[].
        hd.responses=hd.responses||[];});});
    // Rümpfe bleiben, wie der Code sie liefert (Rumpf aus der Datei, P7.4); ein im Code bewusst leerer Rumpf heißt „leer“.
    [...m.decider,...m.applier].forEach(n=>{if(n.rumpf==="")n.leer=true;});
    m.aggregate.forEach(a=>{if((a.state||[]).length&&!m.states.some(s=>s.aggregat===a.name))m.states.push({_id:"state:"+a.name,aggregat:a.name});});
    // Round-trip: geladene Saga.Schritte → Transition-Knoten (prozess = Saga-Name), Schritte werden vor Serveraufruf neu erzeugt.
    m.sagas.forEach(s=>{(s.schritte||[]).forEach(st=>m.transitions.push({_id:st._id||"t"+(NID++),prozess:s.name,wenn:(st.wenn||[]).slice(),...(st._ort?{_ort:st._ort}:{}),
        ...(st.sammelEvent?{sammelEvent:st.sammelEvent,sammelAusdruck:st.sammelAusdruck,sammelAnzahl:st.sammelAnzahl}:{}),
        dann:[{sende:st.sende||"",sendeJe:!!st.sendeJe,sendeJeCollection:st.sendeJeCollection||"",kompensation:st.kompensation||"",
          sendeAusdruck:st.sendeAusdruck,kompensationAusdruck:st.kompensationAusdruck,kompensationJe:!!st.kompensationJe,
          ...(st.rufe?{rufe:st.rufe}:{}),...(st.zeitlimit?{zeitlimit:st.zeitlimit}:{})}]}));s.schritte=[];});
    return m;}
  // Standard-Namespace für neue Knoten: der eines vorhandenen Aggregats/Records; sonst eine Projekt-Wurzel aus dem Code (Rahmen).
  // Akteure und Clients stehen außerhalb der Domänen in IHREM Katalog (z. B. Domain.Akteure) — ein neuer gesellt sich dazu.
  function haeufigsterNs(liste){const z=new Map();(liste||[]).forEach(x=>x.namespace&&z.set(x.namespace,(z.get(x.namespace)||0)+1));
    return [...z.entries()].sort((a,b)=>b[1]-a[1])[0]?.[0]||null;}
  // Was ein Akteur selbst hineingeben darf — die Sorten aus der Grammatik (Erzeugungen des Bausteins Akteur), nie hart kodiert.
  function akteurSorten(){return ((GR()||{}).erzeugungen||[]).filter(e=>e.baustein==="akteur").map(e=>e.sorte);}
  const akteurDarfLabel=()=>akteurSorten().map(x=>grName("sorten",x)).join(" · ")||"Command · Query";
  function defaultNsGlobal(){const w=Object.keys((MODEL.rahmen||{}).verzeichnisse||{}).sort((a,b)=>a.length-b.length)[0];
    return MODEL.aggregate[0]?.namespace||MODEL.records[0]?.namespace||(w?w+".Neu":"Neu");}

  function h(tag,attrs,...kids){const e=document.createElement(tag);
    for(const k in (attrs||{})){if(k==="class")e.className=attrs[k];else if(k.startsWith("on"))e[k]=attrs[k];else if(k==="value")e.value=attrs[k];else e.setAttribute(k,attrs[k]);}
    for(const kid of kids)if(kid!=null)e.append(kid.nodeType?kid:document.createTextNode(kid));return e;}
  const inp=(val,on,ph)=>h("input",{value:val??"",oninput:e=>on(e.target.value),placeholder:ph||""});
  const codearea=(val,on,ph)=>{const t=h("textarea",{class:"code",oninput:e=>on(e.target.value),placeholder:ph||"",rows:Math.max(3,String(val||"").split("\n").length+1)});t.value=val??"";return t;};
  // Typ-Eingabe mit Autovervollständigung (Skalare + Records + Enums = Komposition).
  const tinp=(val,on)=>h("input",{value:val??"",list:"de-typen",oninput:e=>on(e.target.value),placeholder:"Typ"});
  function datalistEl(){const dl=h("datalist",{id:"de-typen"});
    [...SCALARS(),...MODEL.records.map(r=>r.name),...MODEL.enums.map(e=>e.name)].forEach(t=>dl.append(h("option",{value:t})));return dl;}
  // Drop-Zone für graphisches Komponieren: Record-Grip aus der Liste hier ablegen.
  function dz(label,accept,onDrop){
    const d=h("div",{class:"dz",
      ondragover:e=>{e.preventDefault();d.classList.add("over");},
      ondragleave:()=>d.classList.remove("over"),
      ondrop:e=>{e.preventDefault();d.classList.remove("over");
        let data;try{data=JSON.parse(e.dataTransfer.getData("text/plain"));}catch(_){return;}
        if(accept.includes(data.kind))onDrop(data.name,data.kind);
        else{d.classList.add("nope");setTimeout(()=>d.classList.remove("nope"),400);}}},label);
    return d;
  }
  // Feld-Tabelle: EINE uniforme Zeile Name + Typ (+ optional Ausdruck) — kein Mini-Syntax.
  function feldTabelle(felder,onDel,mitAusdruck){const t=h("table",{});
    (felder||[]).forEach((f,fi)=>{const zellen=[
      h("td",{},inp(f.name,v=>f.name=v,"Feld")),
      h("td",{style:"width:160px"},tinp(f.typ,v=>f.typ=v))];
      if(mitAusdruck)zellen.push(h("td",{},inp(f.ausdruck,v=>f.ausdruck=v||undefined,"Ausdruck (abgeleitet)")));
      zellen.push(h("td",{style:"width:26px"},h("button",{class:"rm",onclick:()=>onDel(fi)},"✕")));
      t.append(h("tr",{},...zellen));});return t;}

  // ── RECORD-ZENTRISCH: ein Record-Editor für alles (Command/Event/Ablehnung/Value Object),
  //    danach KOMPOSITION zu Aggregaten (State + Decider + Applier). Feldtyp kann ein anderer
  //    Record/Enum sein → so komponieren sich Records.
  // NUR der Node-Editor, DESIGN-IN-PLACE: jeder Knoten wird direkt auf der Fläche bestückt.
  let TEIL=null;   // Verbinden-Modus: statt Voll-Render nur die betroffenen Karten neu (Set der Knoten-Ids)
  function render(){ if(TEIL){TEIL.gerufen=true;return;} ELEM=null; renderGraph(); autosave(); }
  // Teil-Neuaufbau: betroffene Karten ersetzen, Kurzfassungen/Kanten/Panel auffrischen — Fläche, Zoom und Layout bleiben stehen.
  function teilNeu(ids){if(!world||!canvas){render();return;}
    ELEM=null;ISLE=inselMenge();berechneSicht();
    // Knoten entstanden/entfallen (z. B. ein neuer Handle durch „+ Query andocken"): dann voll neu zeichnen (Raster packt neu).
    const gezeichnet=new Set([...world.querySelectorAll(".gnode2")].map(e=>e.dataset.id));
    if(gezeichnet.size!==VIS.size||[...VIS].some(id=>!gezeichnet.has(id))){render();return;}
    ids.forEach(id=>{const alt=world.querySelector('[data-id="'+id+'"]'),n=NODEBY.get(id);if(!alt||!n)return;
      for(const k in SLOTS)if(SLOTS[k]&&alt.contains(SLOTS[k]))delete SLOTS[k];
      alt.replaceWith(nodeEditor(n));});
    world.querySelectorAll(".gnode2").forEach(el=>{const n=NODEBY.get(el.dataset.id),alt=el.querySelector(".gsum");if(!n||!alt)return;
      const neu=kurzfassung(n);if(neu.innerHTML!==alt.innerHTML)alt.replaceWith(neu);});   // nur wo sich wirklich etwas ändert
    const altI=canvas.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    fokusNeu();drawEdges();zeigeInspector();autosave();}
  // Eine Modell-Änderung aus dem Verbinden-Modus ausführen: ohne Voll-Render, danach nur die betroffenen Karten.
  function imModus(ids,aendern){TEIL={gerufen:false};try{aendern();}finally{const g=TEIL.gerufen;TEIL=null;if(g)teilNeu(ids.filter(Boolean));}}

  // Eindeutiger Name — Namen sind der Referenzschlüssel für Kanten/Decider/Applier.
  function uniq(base){const all=new Set([...MODEL.records.map(r=>r.name),...MODEL.aggregate.map(a=>a.name),...MODEL.enums.map(e=>e.name),...MODEL.sagas.map(s=>s.name),...MODEL.readModels.map(x=>x.name),...MODEL.stores.map(x=>x.name),...MODEL.projektionen.map(x=>x.name),...MODEL.reader.map(x=>x.name),...MODEL.reaktionen.map(x=>x.name),...MODEL.akteure.map(x=>x.name),...MODEL.clients.map(x=>x.name),...MODEL.funktionen.map(x=>x.name),...MODEL.fluesse.map(x=>x.name),
    ...MODEL.dienste.flatMap(x=>[x.name,...(x.implementierungen||[]).map(i=>i.name)]),...MODEL.funktionen.map(x=>x.implementierung).filter(Boolean)]);
    if(!all.has(base))return base;let i=2;while(all.has(base+i))i++;return base+i;}

  function enumCard(e,ei){const c=h("div",{class:"card"});
    c.append(h("h3",{},h("span",{class:"k vo"},"Enum"),nameInp(e,"name","Enum","enum"),
      h("input",{value:e.namespace,oninput:ev=>e.namespace=ev.target.value,placeholder:"Namespace",style:"width:150px"}),
      h("button",{class:"rm",onclick:()=>{MODEL.enums.splice(ei,1);render();}},"✕")));
    c.append(h("div",{class:"blab"},"Werte (kommagetrennt):"));
    c.append(inp(listeZuText(e.werte),v=>e.werte=textZuListe(v),"Dc0, Dc2"));
    c.append(slotRow("ftype","als Feldtyp ▶","r",{type:"ftype",dir:"out",typeName:e.name},"ftype:out:enum:"+e.name));
    return c;}

  // Knoten anlegen — an einer Position (Picker) ODER sichtbar im aktuellen Ausschnitt (Toolbar).
  let SPAWN=0;
  function spawnPos(){if(!canvas)return {};const r=canvas.getBoundingClientRect();
    const c=toWorld(r.left+r.width*0.5,r.top+r.height*0.42);SPAWN++;   // Sichtmitte statt Ecke → landet im Blick
    const x=Math.round((c.x+(SPAWN%8)*28)/GRID)*GRID,y=Math.round((c.y+(SPAWN%8)*28)/GRID)*GRID;
    return VIEW.kompakt?{x,y,_kpos:{x,y}}:{x,y};}   // Kompakt-Ansicht: Position gilt für deren eigenes Layout
  // Nach dem Anlegen den neuen Knoten finden, ggf. seine (ausgeblendete) Domäne einblenden, dorthin
  //   zentrieren + pulsen — sonst geht er in hunderten Knoten unter.
  function fokussiereNeu(vorher,auswaehlen){
    // Panel des neuen Knotens sofort öffnen — dort wird er benannt und verbunden.
    if(auswaehlen){const neu=graphNodes().find(n=>!vorher.has(n.id));if(neu)waehle(neu.id);}
    requestAnimationFrame(()=>{
    const neu=graphNodes().find(n=>!vorher.has(n.id));if(!neu)return;
    if(HIDDEN.has(groupKeyOf(neu))){HIDDEN.delete(groupKeyOf(neu));saveHidden();render();
      requestAnimationFrame(()=>{centerOn(neu);pulseNode(neu);});}
    else{centerOn(neu);pulseNode(neu);}
    deFlash("+ "+(NODELABEL[neu.kind]||neu.kind)+(neu.name?" · "+neu.name:"")+" — hinzugefügt",true);});}
  // opt.still: nicht auswählen/anspringen (Verbinden-Modus legt an und verbindet selbst).
  function neuerKnoten(kind,x,y,opt){gTitel("neu: "+(NODELABEL[kind]||kind));
    const vorher=new Set(graphNodes().map(n=>n.id));
    // opt.ns (Band-Menü der Domänen-Ablage): Namespace der Domäne; opt.agg/opt.saga: naheliegender Besitzer in ihr.
    const defaultNs=()=>(opt&&opt.ns)||defaultNsGlobal(), AGG=(opt&&opt.agg)||"", SAGA=(opt&&opt.saga)||"";
    // Ohne Klickposition keine Position: packLayout setzt den Knoten in die Spalte seiner Art (typreine Spalten).
    let pos=(typeof x==="number")?{x:Math.round(x/GRID)*GRID,y:Math.round(y/GRID)*GRID}:{};
    if(VIEW.kompakt&&pos.x!=null&&!pos._kpos)pos={...pos,_kpos:{x:pos.x,y:pos.y}};
    if(kind==="aggregate")MODEL.aggregate.push({name:uniq("NeuesAggregat"),namespace:defaultNs(),state:[],...pos});
    else if(kind==="decider")MODEL.decider.push({_id:"d"+(NID++),aggregat:AGG,command:"",ergibt:[],...pos});
    else if(kind==="applier")MODEL.applier.push({_id:"a"+(NID++),aggregat:AGG,event:"",...pos});
    else if(kind==="state")MODEL.states.push({_id:"s"+(NID++),aggregat:AGG,felder:[],...pos});
    else if(kind==="enum")MODEL.enums.push({name:uniq("NeuEnum"),namespace:defaultNs(),werte:["A","B"],...pos});
    else if(kind==="saga")MODEL.sagas.push({name:uniq("NeuerProzess"),namespace:defaultNs(),triggerEvent:"",schritte:[],extraUsings:[],...pos});
    else if(kind==="transition")MODEL.transitions.push({_id:"t"+(NID++),prozess:SAGA,wenn:[],dann:[{}],...pos});
    else if(kind==="readmodel")MODEL.readModels.push({_id:"rm"+(NID++),name:uniq("NeuReadModel"),namespace:defaultNs(),felder:[{_id:"f"+(NID++),name:"Id",typ:"Guid"}],store:"",...pos});
    else if(kind==="store")MODEL.stores.push({_id:"st"+(NID++),name:uniq("NeuStore"),namespace:defaultNs(),writeFns:[],readFns:[],...pos});
    else if(kind==="projektion")MODEL.projektionen.push({_id:"pj"+(NID++),name:uniq("NeueProjektion"),namespace:defaultNs(),stores:[],append:false,pull:true,handles:[],...pos});
    else if(kind==="reader")MODEL.reader.push({_id:"rd"+(NID++),name:uniq("NeuReader"),namespace:defaultNs(),stores:[],trackDeps:true,handles:[],...pos});
    else if(kind==="reaktion")MODEL.reaktionen.push({_id:"rk"+(NID++),name:uniq("NeueReaktion"),namespace:defaultNs(),pull:true,handles:[],...pos});
    else if(kind==="funktion")neueFunktion(defaultNs(),pos);
    else if(kind==="dienst")neuerDienst(defaultNs(),pos);
    else if(kind==="quelle")neueQuelle(defaultNs(),"NeueQuelle",pos);
    else if(kind==="akteur")MODEL.akteure.push({_id:"ak"+(NID++),name:uniq("NeuerAkteur"),namespace:haeufigsterNs(MODEL.akteure)||defaultNs(),darf:[],...pos});
    else if(kind==="client")MODEL.clients.push({_id:"cl"+(NID++),name:uniq("INeuerClient"),namespace:haeufigsterNs(MODEL.clients)||defaultNs(),traegt:[],sendet:[],fragt:[],kenntnis:[],...pos});
    else MODEL.records.push({name:uniq("Neu"+kindLabel(kind).replace(/\s/g,"")),kind,namespace:defaultNs(),felder:kind==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[],...pos});
    // Heimat-Domäne in der Ansicht merken: trägt Namespace-lose Arten (📝/🤖 …) und hält Abhängige
    //   (Decider/Applier/State/Regel) in ihrer Domäne, auch wenn ihr Besitzer gelöscht wird. Der Code-Namespace geht vor (domKey).
    if(opt&&opt.ns){const neu=graphNodes().find(n=>!vorher.has(n.id));if(neu){VIEW.heim={...(VIEW.heim||{}),[neu.id]:opt.ns};if(opt.blk)VIEW.heimBlk={...(VIEW.heimBlk||{}),[neu.id]:opt.blk};speichereAnsicht();}}
    // Im Akteur-Rahmen angelegt: der Akteur darf den neuen Eingang sofort (IDarf) — so bleibt er auch in seinem Rahmen.
    if(opt&&opt.akt){const neu=graphNodes().find(n=>!vorher.has(n.id)),ak=MODEL.akteure.find(a=>a.name===opt.akt);
      if(neu&&ak){const nm=neu.ref.name;if(nm&&!(ak.darf=ak.darf||[]).includes(nm))ak.darf.push(nm);
        VIEW.heimAkteur={...(VIEW.heimAkteur||{}),[neu.id]:opt.akt};speichereAnsicht();}}
    render();
    if(!(opt&&opt.still))fokussiereNeu(vorher,true);
  }
  const addRecord=k=>neuerKnoten(k);
  const addAggregat=()=>neuerKnoten("aggregate");
  const addEnum=()=>neuerKnoten("enum");
  const addSaga=()=>neuerKnoten("saga");

  // ══ NACHRICHT MIT ZWEI SEITEN (docs/konzept-editor-pipelines.md §12): „◀ kommt aus" = wer diesen Typ erzeugt, „geht an ▶" = wer
  //   ihn konsumiert — vom Server (board.sicht.partner, Werkbank P7.4.5): je Partner Karte (springen), Text, Markierung und, was ✕ löst.
  //   Das ✕ ist eine Geste an der Karte: der Server beschreibt sie (loesen), der Editor ändert die Karte, render() schickt das Delta.
  function partnerVon(nm){const sv=(SICHT().partner||{})[nm]||{ein:[],aus:[]};
    const z=x=>({id:x.id,text:x.text,mark:x.mark||"",los:x.loesen?()=>loese(x.loesen):null});
    return {ein:(sv.ein||[]).map(z),aus:(sv.aus||[]).map(z),k:(recByName(nm)||{}).kind||""};}
  const ohneX=(arr,x)=>(arr||[]).filter(v=>v!==x);
  function loese(l){const nd=NODEBY.get(l.karte);if(!nd)return;
    if(l.feld)nd.ref[l.feld]=ohneX(nd.ref[l.feld],l.wert);
    else if(l.zusage){const a=nd.own&&nd.own.ref;if(a)a.vertrag=(a.vertrag||[]).filter(x=>x!==nd.ref);}
    else if(l.eingang!=null){const k=nd.ref,f=nd.own&&nd.own.ref,e=(k.eingaenge||[])[l.eingang],d=e&&(e.draehte||[])[l.draht];if(f&&d)flLoese(f,k,e,d);}}
  // Alle Zusagen der Akteur-Verträge (Akteur-Konzept §3) mit ihrer Karten-Id — je Auf(Event) eine Karte „Zusage" im Akteur-Rahmen.
  const aufId=(a,i)=>"auf:"+a._id+":"+i;
  const vertragsTyp=a=>a.vertragName||("I"+(a.name||""));
  function vertragsZusagen(){return MODEL.akteure.flatMap(a=>(a.vertrag||[]).map((r,i)=>({a,r,i,id:aufId(a,i)})));}

  // ══ CLIENTS (docs/konzept-akteure.md §4): die Software an der Leitung — je Client EIN Vertrag (IClientVertrag). Er steht als
  //   eigener Rahmen rechts neben den Domänen; seine Karte ist die ANSCHLUSSLEISTE (jede Zeile nennt ihre Nachricht); die Leitungen
  //   bündeln je Ziel-Rahmen. Zoom ändert daran nichts — Einzelkanten gibt es nur durch eine Handlung (Klick auf Zeile/Bündel → AUF).
  const CL_PRE="§client:";
  const istClientDom=d=>typeof d==="string"&&d.startsWith(CL_PRE);
  // Handshake-Name = Interface ohne führendes I (IArbeitsplatz → Arbeitsplatz), wie Abstractions.Akteurvertrag.ClientName.
  const clientAnzeige=c=>{const n=(c&&c.name)||"";return n.length>1&&n[0]==="I"&&n[1]!==n[1].toLowerCase()?n.slice(1):n;};
  const darfHalter=nm=>MODEL.akteure.filter(a=>(a.darf||[]).includes(nm)).map(a=>a.name);
  const clientTraeger=teil=>MODEL.clients.filter(c=>(c.traegt||[]).includes(teil));
  // Die Anschlussleiste eines Clients — vom Server (board.sicht.clients): die Ports (Richtung, Nachricht, in wessen Namen, Ziel-Karte), die
  //   verkörperten Akteure (wie der AkteurRechteGenerator: Akteure der getragenen Teile + IDarf-Halter von Sendet/Fragt) und je getragenem
  //   Vertrags-Teil sein Akteur. Ein Client, den es im Code noch nicht gibt, hat noch keine.
  const clientSicht=c=>((SICHT().clients||{})["cl:"+c._id])||{akteure:[],ports:[],teile:[]};
  const clientAkteure=c=>clientSicht(c).akteure||[];
  const clientPorts=c=>clientSicht(c).ports||[];
  // Vertrags-Teil (Interface-Name) → sein Akteur (Haupt-Vertrag oder ein weiterer Teil an einer Zusage).
  function teilAkteur(c,teil){const n=((clientSicht(c).teile||[]).find(x=>x.teil===teil)||{}).akteur;return n?MODEL.akteure.find(a=>a.name===n)||null:null;}
  // Ziel-Rahmen eines Ports: eine Zusage → der Vertrags-Rahmen ihres Akteurs; sonst der Domäne × Akteur-Rahmen der Nachrichten-Karte
  //   (bzw. der Domänen-Rahmen, wenn die Domäne keine Akteur-Rahmen hat).
  function portRahmen(p){if(!p.node)return null;if(p.ri==="zusage")return "§vertrag:"+p.akteure[0];
    const n=NODEBY.get(vertreterId(p.node));if(!n)return null;const d=domKey(n);return d+"|"+(akteurOrdnung(d).length?akteurVon(n):"");}
  // Die Bündel eines Clients: je Ziel-Rahmen eine Leitung (durabel, sobald sie eine Zusage trägt — Akteur-Konzept §5.4).
  function clientBuendel(c){const m=new Map();clientPorts(c).forEach(p=>{const k=portRahmen(p);if(!k)return;let b=m.get(k);
      if(!b)m.set(k,b={key:k,ports:[],durabel:false});b.ports.push(p);if(p.ri==="zusage"&&(p.aus||[]).length)b.durabel=true;});
    return [...m.values()];}
  // Beschriftung in Worten (keine Zähler, keine Kürzel): „sendet A, B · fragt Q · hört E · trägt IX (ImagePairKomplett → Klassifiziere…)".
  function buendelText(b){const L=(xs)=>{const u=[...new Set(xs)];return u.length>3?u.slice(0,3).join(", ")+" … und "+(u.length-3)+" weitere":u.join(", ");};
    const by=ri=>b.ports.filter(p=>p.ri===ri);const t=[];
    if(by("zusage").length){const teile=[...new Set(by("zusage").map(p=>p.teil))];
      t.push("trägt "+teile.join(", ")+" ("+L(by("zusage").map(p=>p.msg+((p.aus||[]).length?" → "+p.aus.join("/"):" · Kenntnis")))+")");}
    if(by("sendet").length)t.push("sendet "+L(by("sendet").map(p=>p.msg)));
    if(by("fragt").length)t.push("fragt "+L(by("fragt").map(p=>p.msg)));
    if(by("kenntnis").length)t.push("hört "+L(by("kenntnis").map(p=>p.msg)));
    return t.join(" · ");}
  const rahmenLabel=k=>k.startsWith("§vertrag:")?"📜 Vertrag "+k.slice(9):(()=>{const [d,a]=k.split("|");return domLabel(d)+(a?" · "+(a===OHNE_AKT?"ohne Akteur":a):"");})();
  // Welche Clients stecken an welchem Rahmen (Stecker-Zeile im Kopf): Rahmen-Schlüssel → Clients. Vertrags-Bündel zählen beim Akteur-Rahmen.
  function clientStecker(){const m=new Map(),dazu=(k,c)=>{if(!m.has(k))m.set(k,[]);if(!m.get(k).includes(c))m.get(k).push(c);};
    MODEL.clients.forEach(c=>{if(!VIS.has("cl:"+c._id))return;clientBuendel(c).forEach(b=>{
      if(b.key.startsWith("§vertrag:")){const a=MODEL.akteure.find(x=>x.name===b.key.slice(9)),n=a&&NODEBY.get("akt:"+a._id);
        if(n){const d=domKey(n);dazu(d+"|"+(akteurOrdnung(d).length?a.name:""),c);}}
      else dazu(b.key,c);});});
    return m;}
  // Auffächern (nur durch eine Handlung): {client, key?, msg?} — null = nur die Bündel.
  let AUF=null;
  const aufPasst=(was,p)=>(!was.keys||was.keys.includes(portRahmen(p)))&&(!was.msg||p.msg===was.msg);
  function auffaechern(c,was){waehle("cl:"+c._id);AUF={client:c._id,...was};
    const ids=new Set(["cl:"+c._id]);clientPorts(c).filter(p=>aufPasst(was,p)&&p.node)
      .forEach(p=>{ids.add(vertreterId(p.node));if(p.ri==="zusage"){const a=MODEL.akteure.find(x=>x.name===p.akteure[0]);if(a)ids.add("akt:"+a._id);}});
    fokussiere("menge",ids,"🔌 "+clientAnzeige(c)+" · aufgefächert");drawEdges();}
  window.deClients=()=>MODEL.clients.map(c=>({name:c.name,handshake:clientAnzeige(c),akteure:clientAkteure(c),
    buendel:clientBuendel(c).map(b=>({ziel:b.key,durabel:b.durabel,text:buendelText(b)}))}));
  // Der Akteur-Eingang einer Nachricht (Command/Query/Trigger): ⊕ → Akteure leuchten → anklicken = ihm das Recht geben.
  const darfPort=nm=>slotRow("darf","+ Akteur ⊕ (darf)","l",{type:"darf",dir:"in",rec:nm},"darf:in:"+nm);
  // Kardinalität der Konsumenten-Seite AUS DER GRAMMATIK (nie im Board-Code): „genau eins" / „dieselbe" / „beliebig".
  function grKardinalitaet(sorte){const g=GR();if(!g||!sorte)return "";const ks=(g.konsume||[]).filter(k=>k.sorte===sorte);
    if(!ks.length)return "";if(ks.every(k=>k.kardinalitaet==="eins"))return "genau eins";if(ks.some(k=>k.kardinalitaet==="dieselbe"))return "dieselbe Pipeline";return "beliebig viele";}
  // Ein Partner, dessen Domäne nicht geladen ist, steht als „↗ außerhalb" — Klick lädt die Domäne dazu.
  function partnerZeile(x){
    const n=x.id&&NODEBY.get(x.id),v=n?vertreterId(n.id):null,sichtbar=!!v&&VIS.has(v),aussen=!!n&&!geladen(n);
    const ns=n?nsVon(n):null;
    const mark=x.mark?h("span",{class:"gp-mark"},x.mark):null;
    const text=h("span",{class:"gp-t"+(n?"":" gp-abg"),title:n?(NODELABEL[n.kind]||n.kind):""},x.text);
    const row=h("div",{class:"gp-row"+(aussen?" gp-aussen":"")},
      n?h("i",{class:"kc-"+n.kind,style:"display:inline-block;width:7px;height:7px;border-radius:50%;flex:none"}):h("i",{class:"gp-welt"},"◌"),
      text,mark);
    if(aussen)row.append(h("span",{class:"gp-mark",title:"Domäne "+(ns||"?")+" ist nicht geladen — Klick lädt sie dazu"},"↗ außerhalb · "+letztesSeg(ns||"?")));
    if(n)row.onclick=()=>{if(aussen&&ns&&LADEN){if(!LADEN.some(g=>drinNs(g,ns)))LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;speichereAnsicht();render();}
      const m=NODEBY.get(n.id);if(m){waehle(m.id);centerOn(m);pulseNode(m);}};
    if(x.los)row.append(h("button",{class:"rm",title:"Verbindung lösen (ändert den Ausgang des Erzeugers)",onclick:e=>{e.stopPropagation();x.los();render();}},"✕"));
    return row;}
  // Diagnose: je Nachricht, was „◀ kommt aus" zeigt (Erzeuger + Akteure) — Lücken-Suche „woher wird das ausgelöst?".
  window.deHerkunft=()=>{const r=[];NODEBY.forEach(n=>{
    if(!["command","query","event","rejection","queryresponse"].includes(n.kind))return;const nm=n.name;
    const pv=partnerVon(nm),m=akteurMenge(nm);
    r.push({kind:n.kind,name:nm,ein:pv.ein.map(x=>x.text+(x.mark?" ["+x.mark+"]":"")),aus:pv.aus.map(x=>x.text),
      direkt:[...m.direkt],kette:[...m.kette]});});return r;};
  window.deHandles=()=>{const r=[];Object.entries(HANDLE_ART).forEach(([k,[coll,pre]])=>(MODEL[coll]||[]).forEach(o=>{const ids=handleIds(o,pre+":"+o._id);
    (o.handles||[]).forEach((hd,hi)=>r.push({art:k,owner:o.name,eingang:hd.event||hd.query||"",kind:hd.inputKind||"",akteure:((SICHT().karten[ids[hi]]||{}).akteure||[])}));}));return r;};
  // „Wer?" (docs/konzept-akteure.md §2.3): von welchem Akteur kommt die Nachricht — direkt (IDarf, oben unter „kommt aus") und über die Kette
  //   (abgeleitet, read-only, mit Pfad). Ein Eingang ohne beides ist eine Lücke (GR-HERKUNFT).
  function wer(sek,nm){if(!MODEL.akteure.length)return;const m=akteurMenge(nm),kette=[...m.kette].filter(a=>!m.direkt.has(a)).sort();
    if(kette.length){sek.append(h("div",{class:"gsec",title:"abgeleitet: ein Fluss, ein Prozess, ein Reader oder Decide erzeugt sie aus einer Nachricht dieses Akteurs — kein IDarf nötig"},"👤 über die Kette"));
      kette.forEach(a=>{const ak=MODEL.akteure.find(x=>x.name===a),pfad=kettenPfad(nm,a);
        const row=h("div",{class:"gp-row",title:pfad.join(" → ")},h("span",{class:"gp-t"},(ART_SYM[ak&&ak.art]||"👤 ")+a),h("span",{class:"gp-mark"},pfad.join(" → ")));
        if(ak)row.onclick=()=>waehle("akt:"+ak._id);sek.append(row);});}
    else if(istEingangName(nm)&&!m.direkt.size)sek.append(h("div",{class:"llmmeld err",title:"GR-HERKUNFT"},"⚠ kommt von keinem Akteur — ⊕ darf oder über eine Kette erzeugen"));}
  // Ein Pfad, auf dem nm im Namen von akt entsteht (rückwärts bis zu dessen direktem Eingang bzw. Wechsel) — vom Server.
  const kettenPfad=(nm,akt)=>(((SICHT().pfade||{})[nm])||{})[akt]||[nm];
  // Die zwei Seiten einer Nachricht: ◀ kommt aus (+ ⊕ Erzeuger) · geht an ▶ (+ ⊕ Konsument). Ports: die bestehenden Slot-Schlüssel.
  function zweiSeiten(body,nm,einPort,ausPort){
    const pv=partnerVon(nm),sorte=grNachrichtSorte(nm),kard=grKardinalitaet(sorte);
    const sek=h("div",{class:"gp"});
    sek.append(h("div",{class:"gsec"},"◀ kommt aus"));
    pv.ein.forEach(x=>sek.append(partnerZeile(x)));
    if(einPort)sek.append(einPort);
    wer(sek,nm);
    sek.append(h("div",{class:"gsec"},"geht an ▶"+(kard?" · "+kard:"")));
    if(!pv.aus.length)sek.append(h("div",{class:"gp-row gp-leer"},"— noch kein Konsument"));
    pv.aus.forEach(x=>sek.append(partnerZeile(x)));
    if(kard==="genau eins"&&pv.aus.length>1)sek.append(h("div",{class:"llmmeld err"},"⚠ "+pv.aus.length+" Konsumenten — die Grammatik erlaubt genau einen"));
    if(ausPort)sek.append(ausPort);
    body.append(sek);}
  // Partner außerhalb der geladenen Domänen (für die Kurzzeile „↗ n außerhalb") — über die Board-Kanten, für jede Knotenart gleich.
  function aussenAnzahl(n){if(LADEN===null)return 0;const ids=new Set([...(ADJ.inn.get(n.id)||[]),...(ADJ.out.get(n.id)||[])]);let z=0;
    ids.forEach(id=>{const m=NODEBY.get(id);if(m&&!geladen(m))z++;});return z;}

  function recordCard(body,r){
    body.append(h("div",{class:"gsec"},"Name · Art"));
    body.append(nameInp(r,"name","RecordName","record"));
    const kindSel=h("select",{onchange:e=>{r.kind=e.target.value;if(r.kind==="command"&&ID_FELD()&&!(r.felder||[]).some(f=>f.name===ID_FELD()))(r.felder=r.felder||[]).unshift({name:ID_FELD(),typ:"Guid"});render();}});
    Object.keys(KINDINFO).forEach(k=>{const o=h("option",{value:k},kindLabel(k));if(r.kind===k)o.selected=true;kindSel.append(o);});
    body.append(kindSel);
    // Namespace = eigener Code-Fakt (frei). Die Aggregat-Zugehörigkeit ist KEINE Eingabe: sie folgt aus der Verdrahtung
    //   (Command → Decider, Event ← Decider-Ausgang / → Applier), und der Decider/Applier hängt per ▲ an seinem Aggregat.
    body.append(h("div",{class:"gsec"},"Namespace"));
    body.append(h("input",{value:r.namespace??"",oninput:e=>r.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    if(r.kind==="command"||r.kind==="event"||r.kind==="rejection"){const ag=(SICHT().karten["rec:"+r.name]||{}).aggregat;
      body.append(h("div",{class:"gsec",title:"abgeleitet aus Decider/Applier"},"Aggregat: "+(ag||"— keinem (Decider/Applier verdrahten)")));}
    if(r.kind==="command")body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.istErzeugung=e.target.checked||undefined,...(r.istErzeugung?{checked:"checked"}:{})}),"Erzeugung (ICreationCommand)"));
    // Zwei Seiten je Nachricht (konzept-editor-pipelines §12): ◀ kommt aus (⊕ Erzeuger) · geht an ▶ (⊕ Konsument). Auf der Fläche nur die Ports (Kanten),
    //   im Panel dazu die Partner-Listen. Ein transientes Event (Ablehnung) hat jetzt auch einen Ausgang (→ Projektion/Reaktion/Pipeline).
    {let ein=null,aus=null;
     if(r.kind==="command"){ein=slotRow("sagacmd","+ Erzeuger ⊕ (Prozess · Reaktion · Pipeline)","l",{type:"sagaCmd",dir:"in",rec:r.name},"cmd:in:"+r.name);
       aus=slotRow("command","+ Konsument ⊕ (Decider) ▶","r",{type:"cmd",dir:"out",rec:r.name},"cmd:out:"+r.name);}
     else if(r.kind==="event"||r.kind==="rejection"){const c=r.kind==="event"?"event":"rejection";
       ein=slotRow(c,"+ Erzeuger ⊕ (Decider · veröffentlicht)","l",{type:"evtOut",dir:"in",rec:r.name},"evt:in:"+r.name);
       aus=slotRow(c,"+ Konsument ⊕"+(r.kind==="event"?"":" (Projektion · Reaktion · Pipeline)")+" ▶","r",{type:"evtUse",dir:"out",rec:r.name},"evt:out:"+r.name);}
     else if(r.kind==="query")aus=slotRow("query","+ Konsument ⊕ (Reader) ▶","r",{type:"query",dir:"out",rec:r.name},"qry:out:"+r.name);
     else if(r.kind==="queryresponse")ein=slotRow("qrsp","+ Erzeuger ⊕ (Reader)","l",{type:"qrsp",dir:"in",rec:r.name},"qrsp:in:"+r.name);
     // Akteur-Eingang: Command und Query (was ein Akteur hineingeben darf).
     if(akteurSorten().includes(grNachrichtSorte(r.name))){const dp=darfPort(r.name);ein=ein?h("div",{},ein,dp):dp;}
     if(ein||aus){if(INSP)zweiSeiten(body,r.name,ein,aus);else{if(ein)body.append(ein);if(aus)body.append(aus);}}}
    if(r.kind==="valueobject")body.append(slotRow("ftype","als Feldtyp ▶","r",{type:"ftype",dir:"out",typeName:r.name},"ftype:out:rec:"+r.name));
    if(r.kind==="auftrag")body.append(slotRow("auftrag","Eingang von ƒ "+(r.funktion||"— ⊕ Funktion wählen")+" ▶","r",{type:"auftragFn",dir:"out",rec:r.name},"auf:out:"+r.name));
    if(r.treiber){quellTreiber(body,r);return;}
    body.append(h("div",{class:"gsec"},"Felder"));
    (r.felder||[]).forEach((f,fi)=>body.append(feldRow(f,()=>{r.felder.splice(fi,1);render();},undefined,r.name)));
    body.append(h("button",{class:"add",onclick:()=>{(r.felder=r.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(r.felder,"feld"),typ:"string"});render();}},"+ Feld"));
  }
  // Quellen-Typ (docs/konzept-werkbank.md §3): der Treiber ist ein Code-Fakt (Basistyp), die Felder befüllt er (geerbt, nur lesbar),
  //   die Werte (Pfad, Muster, Fertig-Erkennung …) stehen in der Umgebung — hier je Umgebung gezeigt, wie der Server sie gelesen hat.
  function quellTreiber(body,r){
    body.append(h("div",{class:"gsec",title:"public sealed record "+r.name+" : "+((r.basen||[])[0]||"DateiGemeldet")+";"},"📁 "+r.treiber+"-Treiber"));
    if(INSP)dsQuelle(body,r);
    const umg=Object.entries((MODEL.rahmen||{}).umgebungen||{}),werte=umg.filter(([,u])=>u&&u.Quellen&&u.Quellen[r.name]).map(([n,u])=>[n,u.Quellen]);
    // Der Webhook ist Konfig (konzept-werkbank §12.3 E12): je Umgebung die Route, bearbeitet im 🖥 Host-Rahmen (Quellen).
    {const wh=werte.filter(([,q])=>q[r.name]&&q[r.name].Webhook).map(([n,q])=>[n,q[r.name].Webhook]);
      body.append(h("div",{class:"gsec",title:"Quellen:"+r.name+":Webhook — POST mit der Nachricht als JSON auf dem Einblick-Port; bearbeitet im 🖥 Host-Rahmen"},
        wh.length?wh.map(([n,w])=>"⇥ Webhook "+w+" ("+n+")").join(" · "):"⇥ kein Webhook (Quellen:"+r.name+":Webhook im 🖥 Host-Rahmen)"));}
    umg.filter(([,u])=>!(u&&u.Quellen&&u.Quellen[r.name])).forEach(([n])=>body.append(h("div",{class:"llmmeld err",title:"Start-Prüfung: jede Quelle braucht einen Abschnitt"},"⚠ Umgebung "+n+": kein Abschnitt Quellen:"+r.name+" — der Host startet dort nicht")));
    werte.forEach(([host,q])=>{body.append(h("div",{class:"gsec"},"Werte in Umgebung "+host));
      const zeile=(k,v)=>body.append(h("div",{class:"frow"},h("span",{class:"slotlbl",style:"min-width:110px"},k),h("span",{style:"flex:1;font-family:ui-monospace,monospace;font-size:11.5px;overflow-wrap:anywhere"},v)));
      const flach=(o,pre)=>Object.entries(o||{}).forEach(([k,v])=>v&&typeof v==="object"&&!Array.isArray(v)?flach(v,pre+k+"."):zeile(pre+k,Array.isArray(v)?v.join(", "):String(v)));
      flach(q[r.name],"");});
    body.append(h("div",{class:"gsec"},"Felder (vom Treiber befüllt)"));
    (r.felder||[]).forEach(f=>{const row=h("div",{class:"frow",title:"geerbt von "+((r.basen||[])[0]||"der Treiber-Basis")},
      h("span",{class:"slotlbl",style:"flex:1"},f.name),h("span",{style:"opacity:.75"},f.typ));if(f.name)row.append(feldPort(r.name,f.name,f.typ,f._id));body.append(row);});
  }

  // Aggregat = HUB: State-Knoten oben zuweisen, Decider links, Applier rechts. Keine Feld-Slots.
  function aggStateCard(body,a){
    body.append(topSlot("state","State",{type:"state",dir:"in",agg:a.name},"agg:state:"+a.name));
    body.append(nameInp(a,"name","Aggregat","aggregate"));
    body.append(h("input",{value:a.namespace??"",oninput:e=>a.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    const deciders=MODEL.decider.filter(d=>d.aggregat===a.name), appliers=MODEL.applier.filter(p=>p.aggregat===a.name);
    const L=h("div",{class:"col2"});L.append(h("div",{class:"gsec"},"Decider ◀"));
    deciders.forEach(d=>{const s=anchorDot("decagg");s.classList.add("i");reg("agg:left:"+a.name+":"+d._id,s,null);
      L.append(h("div",{class:"slotrow",onmouseenter:()=>hilite(d._id,null,true),onmouseleave:()=>hilite(d._id,null,false)},s,h("span",{class:"slotlbl"},d.command||"Decider")));});
    const dOpen=port("decagg");dOpen.classList.add("i");reg("agg:left:"+a.name+":open",dOpen,{type:"decAgg",dir:"in",agg:a.name});
    L.append(h("div",{class:"slotrow"},dOpen,h("span",{class:"slotlbl",style:"opacity:.7"},"+ Decider")));
    const R=h("div",{class:"col2",style:"text-align:right"});R.append(h("div",{class:"gsec"},"▶ Applier"));
    appliers.forEach(p=>{const s=anchorDot("appagg");s.classList.add("o");reg("agg:right:"+a.name+":"+p._id,s,null);
      R.append(h("div",{class:"slotrow o",onmouseenter:()=>hilite(null,p._id,true),onmouseleave:()=>hilite(null,p._id,false)},h("span",{class:"slotlbl"},p.event||"Applier"),s));});
    const aOpen=port("appagg");aOpen.classList.add("o");reg("agg:right:"+a.name+":open",aOpen,{type:"appAgg",dir:"in",agg:a.name});
    R.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.7"},"+ Applier"),aOpen));
    body.append(h("div",{class:"aggwrap"},L,R));
    const st=MODEL.states.find(s=>s.aggregat===a.name);
    body.append(h("div",{class:"gsec"},st?("State zugewiesen · "+((a.state||[]).length)+" Feld(er)"):"State-Knoten oben anschließen"));
  }
  // Decider: Command rein (links), Aggregat OBEN, OneOf-Events als MEHRERE Ausgänge (rechts, je Outcome ein Punkt).
  function deciderCard(body,d){
    body.append(topSlot("decagg","▲ Aggregat: "+(d.aggregat||"— ⊕ Aggregat wählen"),{type:"decAgg",dir:"out",dec:d._id},"dec:aggout:"+d._id));
    body.append(slotRow("command","◀ Command: "+(d.command||"—"),"l",{type:"cmd",dir:"in",dec:d._id},"dec:cmdin:"+d._id));
    body.append(h("div",{class:"gsec"},"OneOf-Ausgänge — je mögliches Event eine Zeile (⊕ bei „+ Ausgang“ → Event auf dem Graphen wählen). Das WANN macht der Decide-Rumpf."));
    (d.ergibt||[]).forEach((o,oi)=>{
      const s=port("event");s.classList.add("o");reg("dec:evtout:"+d._id+":"+o.event,s,{type:"evtOut",dir:"out",dec:d._id});
      body.append(h("div",{class:"slotrow o"},
        h("button",{class:"rm",onclick:()=>{d.ergibt.splice(oi,1);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},(o.event||"?")+" ▶"),s));
    });
    const os=port("open");os.classList.add("o");reg("dec:evtout:"+d._id+":open",os,{type:"evtOut",dir:"out",dec:d._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Ausgang ▶"),os));
    body.append(h("div",{class:"gsec"},"Decide-Rumpf"));
    body.append(codePort("dec:rumpf:"+d._id,{k:"decider",dec:d._id},null,"Decide-Logik"));
  }
  // Applier: gespiegelt zum Decider — Event rein (rechts), Aggregat OBEN. Körper = Code.
  function applierCard(body,a){
    body.append(topSlot("appagg","▲ Aggregat: "+(a.aggregat||"— ⊕ Aggregat wählen"),{type:"appAgg",dir:"out",app:a._id},"app:aggout:"+a._id));
    body.append(slotRow("event","Event: "+(a.event||"—")+" ◀","r",{type:"evtUse",dir:"in",app:a._id},"app:evtin:"+a._id));
    body.append(h("div",{class:"gsec"},"Apply-Rumpf"));
    body.append(codePort("app:rumpf:"+a._id,{k:"applier",app:a._id},null,"Apply-Logik"));
  }
  // State-Knoten: eigene State-Felder (Typ per Dropdown); per Kante rechts einem Aggregat zuweisen.
  function stateCard(body,s){
    const agg=s.aggregat?MODEL.aggregate.find(a=>a.name===s.aggregat):null;
    const felder=agg?(agg.state=agg.state||[]):(s.felder=s.felder||[]);
    body.append(slotRow("state","Aggregat ▶"+(s.aggregat?" ("+s.aggregat+")":" — frei"),"r",{type:"state",dir:"out",state:s._id},"state:out:"+s._id));
    body.append(h("div",{class:"gsec"},"State-Felder (Typ per Dropdown)"));
    felder.forEach((f,fi)=>body.append(stateFeldRow(f,()=>{felder.splice(fi,1);render();},agg?agg.name:s._id)));
    body.append(h("button",{class:"add",onclick:()=>{felder.push({_id:"f"+(NID++),name:uniqFeldName(felder,"feld"),typ:"decimal"});render();}},"+ Feld"));
    if(!agg)body.append(h("div",{class:"gsec",style:"color:#8b93a7"},"nicht zugewiesen — ⊕ anklicken und ein Aggregat wählen"));
  }

  // ══ LESESEITE: Read Model / Store (Interface = Querschnitt der Verdrahtung) / Projektion / Reader.
  //    ReadModel→Store · Projektion→Store · Event→Projektion(Handle) · Reader→Store · Query→Reader · Reader→Response.
  //    Effekt-/Query-Ops = STRUKTURIERTES Vokabular (Dropdown), kein Freicode — LLM nur an echten Logik-Stellen.
  function readModelCard(body,rm){
    body.append(slotRow("readmodel","Store ▶"+(rm.store?" ("+rm.store+")":" — frei"),"r",{type:"readmodel",dir:"out",rm:rm._id},"rm:out:"+rm._id));
    body.append(nameInp(rm,"name","ReadModel"));
    body.append(h("input",{value:rm.namespace??"",oninput:e=>rm.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{rm.geteilt=e.target.checked||undefined;render();},...(rm.geteilt?{checked:"checked"}:{})}),"geteilt — mehrere Streams schreiben hinein (IGeteiltesReadModel)"));
    body.append(h("div",{class:"gsec",style:"opacity:.6"},rm.geteilt
      ?"Geteilt ⇒ Versionsprüfung (Optimistic Concurrency); der Store ändert es nur konfliktgeprüft (EnqueueGeteilt), ein Konflikt wiederholt den Stapel. Neuer gewinnt nach Event-Zeit, nie nach Ankunft."
      :"Ein Schreiber: das Dokument gehört dem Stream seiner Id. Eine Menge über viele Streams besser als Zeilen je Stream ablegen."));
    body.append(h("div",{class:"gsec"},"Dokument-Felder (Typ per Dropdown)"));
    (rm.felder||[]).forEach((f,fi)=>body.append(stateFeldRow(f,()=>{rm.felder.splice(fi,1);render();},rm.name)));
    body.append(h("button",{class:"add",onclick:()=>{(rm.felder=rm.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(rm.felder,"feld"),typ:"string"});render();}},"+ Feld"));
  }

  // ══ RUMPF AUS DER DATEI (Werkbank P7.4, Zielbild §8.4): der Rumpf-Port zeigt den Code direkt aus der Datei — das Board trägt ihn so,
  //   wie der warme Workspace ihn gelesen hat (rumpf + Ort: _ort der Methode, _rumpf bei Implementierungen, Anweisung + Lambda-Index bei
  //   Fluss-/Regel-Lambdas, Python-Anker bei Client-Implementierungen). ✎ ersetzt genau diesen Bereich als Geste (POST /api/werkbank/rumpf);
  //   „🤖 füllen“ ist eine Aktion am Rumpf (LLM-Konsole: füllen → prüfen → schreiben, Slot = kid). 📝/🤖 sind keine eigenen Karten mehr:
  //   wo der 📝-Block unter seinem Besitzer stand, steht der Rumpf-Ausschnitt — abgeleitet, Id rp:<Karte>:<i>, keine Modell-Daten.
  function rumpfTeile(n){if(!n)return null;const r=n.ref;
    const T=(label,text,ort,kid,kachel,extra)=>({label,text:text==null?null:String(text),ort:ort||null,kid:kid||null,kachel:kachel||false,...(extra||{})});
    switch(n.kind){
      case "decider":return [T("Decide-Logik",r.rumpf,r._ort,"decide|"+r.aggregat+"|"+r.command,true,{leer:!!r.leer})];
      case "applier":return [T("Apply-Logik",r.rumpf,r._ort,"apply|"+r.aggregat+"|"+r.event,true,{leer:!!r.leer})];
      case "handle":{const o=n.own,k=o.kind==="reader"?"reader|"+o.ref.name+"|"+r.query:(o.kind==="reaktion"?"reaktion|":"projektion|")+o.ref.name+"|"+r.event;
        return [T(o.kind==="reader"?"Reader-Logik":"Handle-Logik",r.rumpf,r._ort,k,true,{kachel:"Handle "+(r.query||r.event||"")})];}
      case "fn":return [T("Impl-Logik",r.rumpf,r._rumpf,"store|"+n.own.ref.name+"|"+r.name,true,{kachel:r.name||"Store-Fn"})];
      case "funktion":{const t=[T("RufeAsync",r.rumpf,r._rumpf,r.auftrag?"funktion|"+r.name+"|"+r.auftrag:null,true,{kachel:(r.implementierung||r.name||"Funktion")+".RufeAsync"})];
        // Die Python-Implementierung (Laufort Extern, konzept-werkbank §12.3 E13): `rufe` in funktionen/<modul>.py — ✎ über PythonSync, 🤖 als Slot funktionpy.
        if(r.python)t.push(T("rufe · Python",r.python.rumpf,null,"funktionpy|"+r.name+"@"+r.python.klasse,true,
          {kachel:r.python.klasse+".rufe (Python)",python:{datei:r.python.datei,klasse:r.python.klasse,methode:r.python.methode||"rufe",signatur:r.python.signatur||null}}));
        return t;}
      // Dienst: je Implementierung je Methode ihr Rumpf (aus der Datei); ✎ als Geste an der Methode.
      case "dienst":return (r.implementierungen||[]).flatMap((im,ii)=>(im.rumpfe||[]).map(x=>T(im.name+"."+x.methode,x.rumpf,x._rumpf,null,true,
        {kachel:im.name+"."+x.methode,impl:ii,methode:x.methode})));
      case "client":return (r.implementierungen||[]).length?(r.implementierungen||[]).flatMap(im=>(im.rumpfe||[]).map(x=>T(im.klasse+"."+x.methode,x.rumpf,null,
        "zusage|"+clientAnzeige(r)+"|"+x.event+"@"+im.klasse,im.klasse+"."+x.methode,{python:{datei:im.datei,klasse:im.klasse,methode:x.methode,signatur:x.signatur||null},event:x.event,impl:im}))):null;
      case "transition":{const sg=MODEL.sagas.find(x=>x.name===r.prozess),folge=sg?regelFolge(sg,MODEL.transitions):[];
        // Lambda-Index in der Regel-Anweisung (Code-Reihenfolge): erst das Zählen (UndAlle), dann Sende/Rufe, dann die Kompensation.
        return (r.dann||[]).filter(d=>d.sende||d.rufe).flatMap(d=>{const i=folge.findIndex(x=>x.t===r&&x.d===d),basis=r.sammelEvent?1:0;
          const kid=teil=>sg&&i>=0?"saga|"+sg.name+"|"+i+"#"+regelVerb(d,teil):null,ort=k=>r._ort?{...r._ort,lambda:k}:null;
          return [T(d.rufe!==undefined?"λ Auftrag":"λ Command",d.sendeAusdruck,ort(basis),kid("sende"),false,{dann:d})]
            .concat(d.kompensation?[T("λ Kompensation",d.kompensationAusdruck,ort(basis+1),kid("komp"),false,{dann:d,teil:"komp"})]:[]);});}
      case "flussknoten":{const f=n.own.ref,name=r.ohneVariable?"#"+(f.knoten||[]).indexOf(r):r.name,ort=k=>r._ort?{...r._ort,lambda:k}:null;
        if(r.art==="je")return [T("λ Liste",r.liste,ort(0),"fluss|"+f.name+"|"+name+"#0",false)];
        return flMitLambda(r)?(r.eingaenge||[]).map((e,ei)=>T("λ Zuordnung",e.ausdruck,ort(ei),"fluss|"+f.name+"|"+name+"#"+ei,false)):null;}}
    return null;}
  // Die Rumpf-Kachel unter dem Besitzer (wo früher der 📝-Block stand): nur für Rümpfe, die im Code Text haben. Ihr Name ist der des früheren
  //   📝-Blocks — die Kartenhöhe taktet das Zeilenraster, so bleibt die Lage aller Karten gleich.
  const kachelId=(nid,i)=>"rp:"+nid+":"+i;
  function rumpfKacheln(owners){const out=[];owners.forEach(n=>(rumpfTeile(n)||[]).forEach((t,i)=>{if(t.kachel&&t.text&&t.text.trim())
    out.push({id:kachelId(n.id,i),name:typeof t.kachel==="string"?t.kachel:t.label,kind:"rumpf",ref:t,own:n,idx:i});}));return out;}
  // Ein Rumpf-Port im Panel des Besitzers (früher: 📝/🤖 andocken): der Ausschnitt aus der Datei + ✎ + 🤖 — Ziel wie bisher als target.
  function teilVonZiel(t){if(!t)return null;let id=null,i=0;
    if(t.k==="decider")id="dec:"+t.dec;else if(t.k==="applier")id="app:"+t.app;else if(t.k==="funktion")id="fk:"+t.fk;
    else if(t.k==="writeFn"||t.k==="readFn")id="fn:"+t.fn;
    else if(t.k==="pjHandle"||t.k==="rdHandle"||t.k==="reakHandle"){const [coll,pre]=t.k==="pjHandle"?["projektionen","prj"]:t.k==="rdHandle"?["reader","rdr"]:["reaktionen","rk"];
      const o=MODEL[coll].find(x=>x._id===(t.proj||t.reader||t.reaktion));if(o)id=handleIds(o,pre+":"+o._id)[t.hi];}
    else if(t.k==="clientAuf"){id="cl:"+t.client;const c=MODEL.clients.find(x=>x._id===t.client);
      i=c?(c.implementierungen||[]).flatMap((im,ii)=>(im.rumpfe||[]).map(x=>[ii,x.event])).findIndex(([ii,e])=>ii===t.impl&&e===t.event):-1;}
    else if(t.k==="regelLambda"){id="tr:"+t.trans;const tr=MODEL.transitions.find(x=>x._id===t.trans),d=tr&&(tr.dann||[])[t.dannIdx];
      i=(rumpfTeile(NODEBY.get(id))||[]).findIndex(x=>x.dann===d&&(x.teil||"sende")===(t.teil||"sende"));}
    else if(t.k==="funktionPy"){id="fk:"+t.fk;i=(rumpfTeile(NODEBY.get(id))||[]).findIndex(x=>!!x.python);}
    else if(t.k==="dienst"){id="dn:"+t.dn;i=(rumpfTeile(NODEBY.get(id))||[]).findIndex(x=>x.impl===t.impl&&x.methode===t.methode);}
    else if(t.k==="flussLambda"){const f=MODEL.fluesse.find(x=>x._id===t.fluss),k=f&&(f.knoten||[]).find(z=>z._id===t.knoten);if(k)id="fs:"+k._id;i=t.ei==null?0:t.ei;}
    const n=id&&NODEBY.get(id),teile=n&&rumpfTeile(n);return teile&&i>=0&&teile[i]?{n,i,teil:teile[i]}:null;}
  function codePort(key,target,_alt,label){const s=port("code");s.classList.add("i");reg(key,s,{type:"code",dir:"in",target});
    const tv=teilVonZiel(target);
    if(!tv)return h("div",{class:"slotrow codeempty"},s,h("span",{class:"slotlbl codemiss",style:"flex:1"},"⚙ "+(label||"Logik")+" — erst im Code anlegen (Geste), dann ✎/🤖"));
    const box=h("div",{class:"rumpfport"});rumpfWerkzeug(box,tv.teil,tv.n.id,tv.i,false,s);return box;}
  // Zustand der Rumpf-Werkzeuge (je Teil-Schlüssel): ✎ offen (Entwurf) bzw. 🤖 offen (Auftrag).
  var RUMPF_EDIT={}, RUMPF_LLM={};
  const teilKey=(nid,i)=>nid+"#"+i;
  // Ausschnitt + Werkzeuge: ✎ Bearbeiten (Geste an der Spanne), 🤖 füllen (Konsole), Vorschlag ✓/✗, ↶ nach 🤖-Übernahme.
  function rumpfWerkzeug(box,t,nid,i,voll,slot){const key=teilKey(nid,i),kid=t.kid,v=kid?VOR[kid]:null,leer=!(t.text||"").trim();
    const kopf=h("div",{class:"slotrow"},slot||null,h("span",{class:"slotlbl",style:"flex:1"+(leer?";opacity:.7":"")},
      "◀ "+t.label+(leer?(t.leer?": bewusst leer":": leer"):"")),
      h("button",{class:"codeadd",title:"Diesen Rumpf bearbeiten — eine Geste ersetzt genau diesen Bereich der Datei",onclick:()=>{RUMPF_EDIT[key]=RUMPF_EDIT[key]!=null?undefined:(v?v.rumpf:t.text||"");if(RUMPF_EDIT[key]===undefined)delete RUMPF_EDIT[key];neuZeichnen();}},"✎"),
      kid?h("button",{class:"codeadd",title:"🤖 füllen: Auftrag schreiben → claude -p füllt den Rumpf → Prüfung (warmer Workspace) → Datei",onclick:()=>{RUMPF_LLM[key]=!RUMPF_LLM[key];neuZeichnen();}},"🤖"):null);
    box.append(kopf);
    const ort=t.ort||t.python;if(ort)box.append(h("div",{class:"blab",style:"opacity:.6;font-size:10px"},(ort.datei||"")+(ort.zeile?":"+ort.zeile:"")+(ort.lambda!=null?" · λ "+ort.lambda:"")));
    if(!leer||v)box.append(h("pre",{class:"gcodeprev"+(v?" vorschlag":""),style:"cursor:default",title:v?"🤖 Vorschlag — noch nicht in der Datei":"aus der Datei (warm gelesen)"},
      codePreview(v?v.rumpf:t.text,undefined,voll)));
    if(RUMPF_EDIT[key]!=null){const ta=h("textarea",{class:"code",rows:Math.max(4,String(RUMPF_EDIT[key]).split("\n").length+1)});ta.value=RUMPF_EDIT[key];
      ta.oninput=e=>{RUMPF_EDIT[key]=e.target.value;};
      box.append(ta,h("div",{class:"llmrow"},h("button",{class:"llmok",title:"Als Geste schreiben (↶ nimmt sie zurück)",onclick:()=>rumpfSpeichern(t,key)},"✓ Speichern"),
        h("button",{class:"codeadd",onclick:()=>{delete RUMPF_EDIT[key];neuZeichnen();}},"Abbrechen")));}
    if(v){const gut=v.ok&&!(v.befunde||[]).length;
      box.append(h("div",{class:"llmmeld "+(gut?"ok":"warn")},gut?"🤖 Vorschlag · ✓ geprüft · noch nicht in der Datei":"🤖 Vorschlag · "+(v.befunde||[]).length+" Befund(e) · noch nicht in der Datei"));
      const ok=h("button",{class:"llmok",title:"In die Datei schreiben (mit „// 🤖 Prompt:“), bauen, neu lesen",onclick:()=>llmUebernehmen(kid)},"✓ Übernehmen");
      const weg=h("button",{class:"codeadd",title:"Vorschlag verwerfen — der Datei-Rumpf gilt",onclick:()=>llmVerwerfen(kid)},"✗ Verwerfen");
      if(LAUF[kid]||EINLESEN){ok.disabled=true;weg.disabled=true;}box.append(h("div",{class:"llmrow"},ok,weg));}
    else if(kid&&ZULETZT[kid])box.append(h("div",{class:"llmrow"},h("span",{class:"llmmeld ok",style:"flex:1"},"🤖 übernommen"),
      h("button",{class:"codeadd",title:"Den alten Rumpf wörtlich wiederherstellen (nur solange die Datei seitdem unverändert ist)",onclick:()=>llmRueckgaengig(kid)},"↶ Rückgängig")));
    if(kid&&RUMPF_LLM[key]){const ta=h("textarea",{class:"code",rows:2,placeholder:leer?"Auftrag, z. B. „Bestätige nur, wenn Betrag > 0.“":"Anpassung, z. B. „lehne auch ab, wenn Betrag > 1000“"});
      const ohneKontext=!LLM_STATUS||!LLM_STATUS.index;
      const senden=async()=>{const text=ta.value.trim();if(!text||LAUF[kid]||EINLESEN)return;ta.value="";
        await llmAusfuehren(kid,leer?text:"Ändere den bestehenden Rumpf.",leer?null:text,t.text);};
      ta.onkeydown=e=>{if(e.key==="Enter"&&(e.metaKey||e.ctrlKey)){e.preventDefault();senden();}};
      const knopf=h("button",{class:"llmrun",title:"Senden (Cmd/Ctrl+Enter): claude -p → Prüfung im warmen Workspace → Datei",onclick:senden},"▶ füllen");
      if(LAUF[kid]||EINLESEN||ohneKontext)knopf.disabled=true;
      box.append(ta,h("div",{class:"llmrow"},knopf,h("a",{class:"codeadd",href:"/konsole#id="+encodeURIComponent(kid),target:"_blank",title:"Runden, Prompt und Token dieses Rumpfs",style:"text-decoration:none"},"Details ↗")));}
    if(kid){const lauf=LAUF[kid],m=MELD[kid];const zeile=lauf?{k:"",t:"● "+lauf.was+" …",lauf:lauf.seit}:EINLESEN?{k:"",t:"↻ Code wird neu gelesen …"}:m||null;
      if(zeile){const el=h("div",{class:"llmmeld "+(zeile.k||"")},zeile.t);if(zeile.lauf)el.dataset.lauf=zeile.lauf;box.append(el);}}}
  // Panel einer Rumpf-Kachel: der Besitzer (▲) und der ganze Rumpf mit ✎/🤖.
  function rumpfKachelCard(body,n){
    body.append(h("a",{class:"ghl",title:"Die Karte, deren Rumpf das ist",onclick:()=>{waehle(n.own.id);centerOn(n.own);}},"▲ "+(NODELABEL[n.own.kind]||n.own.kind)+": "+(n.own.name||"?")));
    rumpfWerkzeug(body,n.ref,n.own.id,n.idx,INSP);}
  // ✎ speichern: eine Geste ersetzt genau diesen Bereich (C#: Methoden-Rumpf bzw. Lambda; Python: Rumpf der Methode) — Sperre: der Bereich
  //   muss noch so aussehen, wie er gezeigt wurde (alt).
  async function rumpfSpeichern(t,key){const text=RUMPF_EDIT[key];if(text==null)return;
    if(!t.ort&&!t.python){deFlash("⚠ Für diesen Rumpf kennt der Code noch keinen Ort — erst die Methode anlegen (Geste) und neu lesen.",false);return;}
    await gesteFlush();G_LAEUFT=true;
    try{const r=await fetch("/api/werkbank/rumpf",{method:"POST",headers:{"content-type":"application/json"},
        body:JSON.stringify({titel:"✎ "+t.label,text,alt:t.text||"",...(t.python?{python:t.python}:{ort:t.ort})})});
      const a=await r.json();
      if(!a.ok){deFlash("⚠ "+(a.fehler||"nicht geschrieben"),false);return;}
      delete RUMPF_EDIT[key];gMeldung(a);await wbLadeDiagnosen();G_BOARD=a.board;gBoardAnwenden();}
    catch(e){badge(false);deFlash("⚠ Werkbank nicht erreichbar — nicht geschrieben",false);}
    finally{G_LAEUFT=false;neuZeichnen();}}
  // Parameter-Zeilen einer Store-Funktion (Name : Typ) — die API-Signatur.
  function paramRows(fn){const box=h("div",{});
    (fn.params||[]).forEach((pr,pi)=>box.append(h("div",{class:"frow"},
      inp(pr.name,v=>pr.name=v,"param"),tinp(pr.typ,v=>pr.typ=v),
      h("button",{class:"rm",onclick:()=>{fn.params.splice(pi,1);render();}},"✕"))));
    box.append(h("button",{class:"add",onclick:()=>{(fn.params=fn.params||[]).push({name:"arg",typ:"Guid"});render();}},"+ Param"));
    return box;}

  // Store-Funktion per _id auflösen → {store, fn, isRead}. Basis für abgeleiteten Scope + Aufruf-Kanten.
  function fnById(id){for(const st of MODEL.stores){
    const w=(st.writeFns||[]).find(f=>f._id===id);if(w)return{store:st,fn:w,isRead:false};
    const r=(st.readFns||[]).find(f=>f._id===id);if(r)return{store:st,fn:r,isRead:true};}return null;}
  // Abgeleiteter Store-Scope eines Konsumenten (die Stores, deren Funktionen seine Handles aufrufen) — vom Server (board.sicht).
  const storeScope=id=>(SICHT().karten[id]||{}).storeScope||[];

  // Store-Knoten = INTERFACE-EDITOR: Write-/Read-Funktionen (Signatur = Name + Parameter [+ Rückgabe]),
  //   je Funktion ein Impl-Code-Port (der Rumpf kommt von 📝/🤖). Read Models docken oben an.
  function storeCard(body,st){
    body.append(topSlot("readmodel","Read Models ▲",{type:"readmodel",dir:"in",store:st.name},"sto:rm:"+st.name));
    body.append(nameInp(st,"name","Store"));
    body.append(h("input",{value:st.namespace??"",oninput:e=>st.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    // Impl-Klasse: aus dem Code (fest) oder — bei einem neuen Store — benennbar (leer = Vorschlag: Name ohne I-Präfix).
    if(st.impl&&st.impl.datei)body.append(h("div",{class:"gsig",title:"Die Implementierungs-Klasse aus dem Code — neue Fns bekommen dort einen Platzhalter"},"Impl: "+st.impl.name));
    else if(st.datei)body.append(h("div",{class:"gsec",style:"opacity:.7"},"Impl: keine eindeutige Klasse im Code — Methoden werden nicht ergänzt"));
    else body.append(h("div",{class:"frow",title:"Implementierungs-Klasse, die die Geste anlegt"},h("span",{class:"slotlbl"},"Impl"),
      inp(st.impl&&st.impl.name,v=>{v=v.trim();if(v)st.impl={name:v,namespace:(MODEL.rahmen&&MODEL.rahmen.storeImplNamespace)||st.namespace};else delete st.impl;},
        /^I[A-Z]/.test(st.name||"")?st.name.slice(1):(st.name||"")+"Impl")));
    const rms=MODEL.readModels.filter(x=>x.store===st.name);
    body.append(h("div",{class:"gsec"},"Dokumente: "+(rms.length?rms.map(x=>x.name).join(" · "):"— (ReadModel oben anschließen)")));
    body.append(h("div",{class:"gsep"},"Schreib-Fähigkeiten — je Fn ein Interface, ◀ Parameter eines Projektion-Handles"));
    body.append(fnListe(st,st.writeFns||[]));
    body.append(h("button",{class:"add",onclick:()=>{st.writeFns.push({_id:"wf"+(NID++),name:"NeuFunktion",params:[]});render();}},"+ Write-Funktion"));
    body.append(h("div",{class:"gsep"},"Lese-Fähigkeiten — je Fn ein Interface, ◀ Parameter eines Reader-/Pipeline-Handles"));
    body.append(fnListe(st,st.readFns||[]));
    body.append(h("button",{class:"add",onclick:()=>{st.readFns.push({_id:"rf"+(NID++),name:"HoleX",params:[],rueckgabe:""});render();}},"+ Read-Funktion"));
  }
  // Vorschlag für den Fähigkeits-Namen einer NEUEN Fn — dieselbe Regel wie der Server (DomainEditor.BoardLeseseite):
  //   I + Methode ohne „Async“; schon vergeben ⇒ + Store-Name ohne I-Präfix. Danach ist der Name Code-Fakt.
  function faehigkeitVorschlag(st,fn){const belegt=new Set();
    MODEL.stores.forEach(s=>(s.writeFns||[]).concat(s.readFns||[]).forEach(f=>{if(f!==fn&&f.faehigkeit)belegt.add(f.faehigkeit);}));
    const n=fn.name||"";const kern=n.length>5&&n.endsWith("Async")?n.slice(0,-5):n;const ohneI=x=>/^I[A-Z]/.test(x||"")?x.slice(1):(x||"");
    let v="I"+kern;if(belegt.has(v))v="I"+kern+ohneI(st.name);return v;}
  function fnBlock(st,fn,isRead){const arr=isRead?st.readFns:st.writeFns;
    const box=h("div",{style:"border-left:2px solid #2c3547;padding-left:7px;margin:6px 0"});
    // Fähigkeits-Port: ein Projektion- (write) bzw. Reader-/Pipeline-Handle (read) dockt hier an → die Fn wird sein Parameter.
    const ct=isRead?"rcall":"wcall";const cp=port("store");cp.classList.add("i");
    reg(ct+":in:"+st._id+":"+fn._id,cp,{type:ct,dir:"in",store:st._id,fn:fn._id});
    box.append(h("div",{class:"slotrow"},cp,inp(fn.name,v=>fn.name=v,"funktionsName"),
      h("button",{class:"rm",onclick:()=>{arr.splice(arr.indexOf(fn),1);render();}},"✕")));
    box.append(paramRows(fn));
    if(isRead)box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"→ Rückgabe"),tinp(fn.rueckgabe,v=>fn.rueckgabe=v)));
    // Fähigkeit = das Interface GENAU dieser Fn (CQRS051). Aus dem Code: fest (Code-Fakt). Neu: Name im Panel, leer = Vorschlag.
    if(fn.sig)box.append(h("div",{class:"gsig",title:"Das Fähigkeits-Interface aus dem Code — der Typ, den ein Handle als Parameter nimmt"},"Fähigkeit: "+(fn.faehigkeit||"?")));
    else box.append(h("div",{class:"frow",title:"Name des Fähigkeits-Interfaces, das die Geste anlegt (leer = Vorschlag)"},h("span",{class:"slotlbl"},"Fähigkeit"),
      inp(fn.faehigkeit,v=>{fn.faehigkeit=v.trim()||undefined;},faehigkeitVorschlag(st,fn))));
    box.append(codePort("impl:in:"+st._id+":"+fn._id,{k:isRead?"readFn":"writeFn",store:st._id,fn:fn._id},null,"Impl-Logik"));
    return box;}

  // Projektion = Controller: Trigger-Events (je Handle) + Store-SCOPE (mehrere möglich). Der Handle-Rumpf
  //   (welche Store-Funktionen, Reihenfolge, Bedingung, Args) kommt als CODE über den Controller-Port.
  function projektionCard(body,p){
    body.append(nameInp(p,"name","Projektion","projektion"));
    body.append(h("input",{value:p.namespace??"",oninput:e=>p.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"frow",title:"ISubscriber.SubscriberId — Kennung des Cursors (Umbenennen = neuer Cursor!)"},h("span",{class:"slotlbl"},"SubscriberId"),inp(p.subscriberId,v=>p.subscriberId=v||undefined,p.name||"")));
    // Transport-Achse: geordneter Pull (IPullSubscriber) vs. Signal (ISubscriber, best-effort). Default = Pull.
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{p.pull=e.target.checked;render();},...((p.pull!==false)?{checked:"checked"}:{})}),"Geordneter Pull (IPullSubscriber)"));
    // Garantie-Achse: append-artig ⇒ Co-Commit-Store ⇒ exactly-once (GA-1); sonst idempotenter Upsert ⇒ at-least-once genügt.
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{p.append=e.target.checked||undefined;render();},...(p.append?{checked:"checked"}:{})}),"Append-artig ⇒ Exactly-once (IAppendProjektion)"));
    body.append(h("div",{class:"gsec",style:"opacity:.6"},p.append?"Append ⇒ verlangt Co-Commit-Store (GA-1): Effekt + Marke in EINER Transaktion.":(p.pull!==false?"Idempotenter Upsert · geordneter Pull.":"Idempotenter Upsert · Signal (best-effort, verlierbar).")));
    // Als Projektion-Ziel (IReader<TProjection>) andockbar + abgeleiteter Store-Scope (aus den Handle-Aufrufen).
    const asp=port("query");asp.classList.add("i");reg("prj:asproj:"+p._id,asp,{type:"projref",dir:"in",proj:p._id});
    body.append(h("div",{class:"slotrow"},asp,h("span",{class:"slotlbl"},"◀ Reader bindet hier an")));
    const ds=storeScope("prj:"+p._id);
    body.append(h("div",{class:"gsec"},"Stores (abgeleitet): "+(ds.length?ds.join(" · "):"— (Handle → Write-Fn verdrahten)")));
    body.append(handleListe(p,"prj:"+p._id,"Trigger-Event → Handle → Write-Fn(s) · je Handle eine eigene Karte"));
    const oi=port("open");oi.classList.add("i");reg("prj:in:"+p._id+":open",oi,{type:"evtUse",dir:"in",proj:p._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Event andocken")));
  }

  // Reaktion = EMITTIERENDER Konsument (ISubscriber → IAsyncEnumerable<OneOf<Cmd>>): kein Store, keine
  //   Reset — Trigger-Event(s) ◀ → Handle → OneOf-Command-Ausgänge ▶ (das WAS ausgelöst wird ist verdrahtet,
  //   das WIE/mit-welchen-Werten macht die 📝 Controller-Logik). Der idiomatische Fan-in-Baustein.
  function reaktionCard(body,r){
    body.append(nameInp(r,"name","Reaktion","reaktion"));
    body.append(h("input",{value:r.namespace??"",oninput:e=>r.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"frow",title:"ISubscriber.SubscriberId — Kennung des Cursors (Umbenennen = neuer Cursor!)"},h("span",{class:"slotlbl"},"SubscriberId"),inp(r.subscriberId,v=>r.subscriberId=v||undefined,r.name||"")));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.pull=e.target.checked,...((r.pull!==false)?{checked:"checked"}:{})}),"Geordneter Pull (IPullSubscriber)"));
    body.append(h("div",{class:"gsec"},"Trigger-Event → Handle → OneOf-Command(s) (emittiert). Das WANN/mit-WELCHEN-Werten macht der Rumpf."));
    body.append(handleListe(r,"rk:"+r._id,"je Handle eine eigene Karte"));
    const oi=port("open");oi.classList.add("i");reg("rk:in:"+r._id+":open",oi,{type:"evtUse",dir:"in",reaktion:r._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Event andocken")));
  }

  // Ein Projektions-HANDLE (eigene Karte): Eingang-Event, Schreib-Fähigkeiten (Parameter), veröffentlichte Events, Controller-Rumpf.
  function prjHandleBody(body,p,hd,hi){
      const sl=port("event");sl.classList.add("i");reg("prj:in:"+p._id+":"+hi,sl,{type:"evtUse",dir:"in",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},"◀ Auf "+(hd.event||"?")),h("button",{class:"rm",onclick:()=>{p.handles.splice(hi,1);render();}},"✕")));
      // Schreib-Fähigkeiten (je Parameter ein Punkt → Store-Fn ziehen); Norm = genau eine.
      (hd.fns||[]).forEach((fid,fj)=>{const r=fnById(fid);const s=port("store");s.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":"+fid,s,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"darf "+(r?r.store.name+"."+r.fn.name:"?")+" ▶"),s));});
      const wo=port("store");wo.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":open",wo,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Write-Fn ▶"),wo));
      // Reaktives Event veröffentlichen (nach dem Schreiben): yield IEvent → Broker-Re-Publish (verlierbar, kein Log).
      (hd.publishes||[]).forEach((ev,ei)=>{const s=port("event");s.classList.add("o");reg("prj:pub:"+p._id+":"+hi+":"+ev,s,{type:"evtOut",dir:"out",proj:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.publishes.splice(ei,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"veröffentlicht "+(ev||"?")+" ▶"),s));});
      const po=port("event");po.classList.add("o");reg("prj:pub:"+p._id+":"+hi+":open",po,{type:"evtOut",dir:"out",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.7"},"+ veröffentlicht Event ▶ (reaktiv)"),po));
      body.append(codePort("ctrl:in:"+p._id+":"+hi,{k:"pjHandle",proj:p._id,hi:hi},null,"Controller-Logik"));
  }
  function rkHandleBody(body,r,hd,hi){

      const sl=port("event");sl.classList.add("i");reg("rk:in:"+r._id+":"+hi,sl,{type:"evtUse",dir:"in",reaktion:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},"◀ Auf "+(hd.event||"?")),h("button",{class:"rm",onclick:()=>{r.handles.splice(hi,1);render();}},"✕")));
      // Ausgelöste Commands (je Command ein Punkt → an „◀ ausgelöst von" eines Command-Knotens ziehen).
      (hd.sends||[]).forEach((c,ci)=>{const so=port("command");so.classList.add("o");reg("rk:send:"+r._id+":"+hi+":"+c,so,{type:"sagaCmd",dir:"out",reaktion:r._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.sends.splice(ci,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"sendet "+(c||"?")+" ▶"),so));});
      const so=port("command");so.classList.add("o");reg("rk:send:"+r._id+":"+hi+":open",so,{type:"sagaCmd",dir:"out",reaktion:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Command ▶"),so));
      // Reaktives Event veröffentlichen: yield IEvent → Broker-Re-Publish (verlierbar, kein Log).
      (hd.publishes||[]).forEach((ev,ei)=>{const s=port("event");s.classList.add("o");reg("rk:pub:"+r._id+":"+hi+":"+ev,s,{type:"evtOut",dir:"out",reaktion:r._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.publishes.splice(ei,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"veröffentlicht "+(ev||"?")+" ▶"),s));});
      const po=port("event");po.classList.add("o");reg("rk:pub:"+r._id+":"+hi+":open",po,{type:"evtOut",dir:"out",reaktion:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.7"},"+ veröffentlicht Event ▶ (reaktiv)"),po));
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"reakHandle",reaktion:r._id,hi:hi},null,"Controller-Logik"));
  }
  function rdHandleBody(body,r,hd,hi){

      const qin=port("query");qin.classList.add("i");reg("rdr:qin:"+r._id+":"+hi,qin,{type:"query",dir:"in",reader:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},qin,h("span",{class:"slotlbl",style:"flex:1"},"◀ Query: "+(hd.query||"?")),h("button",{class:"rm",onclick:()=>{r.handles.splice(hi,1);render();}},"✕")));
      // Lese-Fähigkeiten (je Parameter ein Punkt → Store-Read-Fn ziehen; mehrere Stores erlaubt).
      (hd.fns||[]).forEach((fid,fj)=>{const rr=fnById(fid);const s=port("store");s.classList.add("o");reg("rcall:out:"+r._id+":"+hi+":"+fid,s,{type:"rcall",dir:"out",reader:r._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"darf "+(rr?rr.store.name+"."+rr.fn.name:"?")+" ▶"),s));});
      const ro=port("store");ro.classList.add("o");reg("rcall:out:"+r._id+":"+hi+":open",ro,{type:"rcall",dir:"out",reader:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Read-Fn ▶"),ro));
      // OneOf-Responses (je mögliche Antwort ein Punkt).
      (hd.responses||[]).forEach((resp,ri)=>{
        const rout=port("qrsp");rout.classList.add("o");reg("rdr:rout:"+r._id+":"+hi+":"+resp,rout,{type:"qrsp",dir:"out",reader:r._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},
          h("button",{class:"rm",onclick:()=>{hd.responses.splice(ri,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},(resp||"?")+" ▶"),rout));
        
      });
      const rop=port("qrsp");rop.classList.add("o");reg("rdr:rout:"+r._id+":"+hi+":open",rop,{type:"qrsp",dir:"out",reader:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Response ▶"),rop));
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"rdHandle",reader:r._id,hi:hi},null,"Controller-Logik"));
  }

  // ══ HANDLE- und FN-KNOTEN: jeder Handle einer Projektion/Reader/Reaktion und jede Store-Funktion ist eine
  //   EIGENE Karte (wie der Decider je Command) — mit ihren Ports, ihrem Rumpf und dem Vertrag aus dem Code (was sie
  //   erzeugen KANN und welche Store-Fns sie rufen DARF — nur Signatur). Kein neues Modell-Objekt: die Karte zeigt owner.handles[i] bzw. die Fn.
  const HANDLE_ART={projektion:["projektionen","prj"],reader:["reader","rdr"],reaktion:["reaktionen","rk"]};
  const handleDisc=hd=>hd.query||hd.event||hd.selfName||hd.input||"";
  // Knoten-Ids der Handles eines Besitzers — über den Eingangstyp (rename-fest je Eingang), Index nur bei Kollision/leer.
  function handleIds(o,oid){const seen=new Set();return (o.handles||[]).map((hd,hi)=>{let id="hd:"+oid+":"+(handleDisc(hd)||"#"+hi);
    if(seen.has(id))id+="#"+hi;seen.add(id);return id;});}
  function handleKnoten(){const out=[];Object.entries(HANDLE_ART).forEach(([k,[coll,pre]])=>(MODEL[coll]||[]).forEach(o=>{const oid=pre+":"+o._id,ids=handleIds(o,oid);
    (o.handles||[]).forEach((hd,hi)=>out.push({id:ids[hi],name:handleDisc(hd)||"(neu)",kind:"handle",ref:hd,own:{kind:k,ref:o,id:oid},hi}));}));return out;}
  function fnKnoten(){const out=[];MODEL.stores.forEach(st=>["writeFns","readFns"].forEach(a=>(st[a]||[]).forEach(f=>
    out.push({id:"fn:"+f._id,name:f.name||"?",kind:"fn",ref:f,own:{kind:"store",ref:st,id:"sto:"+st._id},lesen:a==="readFns"}))));return out;}
  // Liste der Handles auf der Besitzer-Karte (zum Springen).
  function handleListe(o,oid,titel){const box=h("div",{});box.append(h("div",{class:"gsec"},titel));
    const ids=handleIds(o,oid);
    (o.handles||[]).forEach((hd,hi)=>box.append(h("a",{class:"ghl",title:"Handle-Karte zeigen",onclick:()=>{const n=NODEBY.get(ids[hi]);if(n){waehle(n.id);centerOn(n);pulseNode(n);}}},
      "▸ Handle("+(handleDisc(hd)||"?")+")"+(hd.signaturOffen?" ⚠":""))));
    return box;}
  function fnListe(st,arr){const box=h("div",{});
    arr.forEach(f=>box.append(h("a",{class:"ghl",title:"Fn-Karte zeigen",onclick:()=>{const n=NODEBY.get("fn:"+f._id);if(n){waehle(n.id);centerOn(n);pulseNode(n);}}},
      "▸ "+(f.name||"?")+"("+(f.params||[]).map(x=>x.typ).join(", ")+")"+(f.rueckgabe?" → "+f.rueckgabe:""))));
    return box;}
  // Kopf des Handles: Signatur + Form, offene Signatur als Warnung.
  function vertragKopf(hd){const box=h("div",{});
    if(hd.signatur)box.append(h("div",{class:"gsig",title:"Rückgabe-Signatur aus dem Code"},"⟶ "+hd.signatur));
    if(hd.signaturOffen)box.append(h("div",{class:"llmmeld err"},"⚠ offene Signatur — mögliche Ausgaben unbekannt (CQRS050: OneOf<…> konkreter Typen)"));
    if(hd.form==="nichts")box.append(h("div",{class:"gsec",style:"opacity:.65"},"gibt nichts zurück (Task) — nur Effekte"));
    return box;}
  function handleCard(body,n){const o=n.own.ref,hd=n.ref,hi=n.hi;
    body.append(h("a",{class:"ghl",onclick:()=>{const m=NODEBY.get(n.own.id);if(m){waehle(m.id);centerOn(m);}}},"▲ "+(NODELABEL[n.own.kind]||n.own.kind)+": "+(o.name||"?")));
    body.append(vertragKopf(hd));
    if(n.own.kind==="projektion")prjHandleBody(body,o,hd,hi);
    else if(n.own.kind==="reader")rdHandleBody(body,o,hd,hi);
    else if(n.own.kind==="reaktion")rkHandleBody(body,o,hd,hi);}
  function fnCard(body,n){const st=n.own.ref;
    body.append(h("a",{class:"ghl",onclick:()=>{const m=NODEBY.get(n.own.id);if(m){waehle(m.id);centerOn(m);}}},"▲ Store: "+(st.name||"?")));
    body.append(h("div",{class:"gsec"},n.lesen?"Lese-Fähigkeit — ◀ Parameter von Reader-Handles":"Schreib-Fähigkeit — ◀ Parameter von Projektions-Handles"));
    body.append(fnBlock(st,n.ref,n.lesen));
    // Wer diese Fähigkeit als Parameter verlangt (Signatur-Fakt: darf rufen — nicht „ruft wann").
    const rufer=[];MODEL.projektionen.concat(MODEL.reader).forEach(o=>(o.handles||[]).forEach(hd=>{if((hd.fns||[]).includes(n.ref._id))rufer.push({o,hd});}));
    if(rufer.length){body.append(h("div",{class:"gsec"},"Fähigkeit von"));
      rufer.forEach(({o,hd})=>body.append(h("div",{class:"gaus"},o.name+".Handle("+handleDisc(hd)+")")));}}
  // Reader = Controller: Queries (je Handle) + Store-SCOPE + Response; Handle-Rumpf kommt als Code.
  function readerCard(body,r){
    body.append(nameInp(r,"name","Reader"));
    body.append(h("input",{value:r.namespace??"",oninput:e=>r.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.trackDeps=e.target.checked,...((r.trackDeps!==false)?{checked:"checked"}:{})}),"TrackDeps (Redis-Deps)"));
    // IReader<TProjection>: der Reader liest GENAU EINE Projektion — expliziter Bindungs-Port.
    const pb=port("query");pb.classList.add("o");reg("rdr:proj:"+r._id,pb,{type:"projref",dir:"out",reader:r._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"liest Projektion: "+(r.projektion||"— andocken")+" ▶"),pb));
    const ds=storeScope("rdr:"+r._id);
    body.append(h("div",{class:"gsec"},"Stores (abgeleitet): "+(ds.length?ds.join(" · "):"— (Handle → Read-Fn verdrahten)")));
    body.append(h("div",{class:"gsec"},"Query → Handle → Read-Fn(s) (auch mehrere Stores) → OneOf-Responses. Das WANN macht der Rumpf."));
    body.append(handleListe(r,"rdr:"+r._id,"Query → Handle → Read-Fn(s) → OneOf-Responses · je Handle eine eigene Karte"));
    const oi=port("open");oi.classList.add("i");reg("rdr:qin:"+r._id+":open",oi,{type:"query",dir:"in",reader:r._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Query andocken")));
  }

  // ══ LOGIK-ELEMENT „Katalog-Funktion" (IFunktion): Signatur + Rumpf — ◀ EIN Auftrag hinein, ▶ OneOf-Ergebnis-Events heraus,
  //    ⚙ Lese-Fähigkeiten als Parameter, 📝/🤖 der Rumpf (RufeAsync). Die Typen sind EIGENE Karten (Auftrag, Events): am Element stehen
  //    sie nur als Verweis (Klick öffnet die Karte), verbunden wird per ⊕, „＋" legt eine neue Typ-Karte an und verbindet sie gleich.
  //    Deklariert (Name, Felder) wird ein Typ nur an seiner Karte — nie im Knoten, der ihn benutzt (auch nicht am rufenden Fluss-Knoten).
  // Eine neue Funktion — EIN Weg (Palette, Fluss, Prozess): I<Stamm> mit Auftrag <Stamm>Auftrag und einem Ergebnis <Stamm>Erledigt.
  function neueFunktion(ns,pos){const nm=uniq("INeueFunktion"),stamm=nm.replace(/^I(?=[A-ZÄÖÜ])/,"");
    const an=uniq(stamm+"Auftrag"),en=uniq(stamm+"Erledigt");
    MODEL.records.push({name:an,kind:"auftrag",funktion:nm,namespace:ns,felder:[]},{name:en,kind:"event",namespace:ns,felder:[]});
    MODEL.funktionen.push({_id:"fk"+(NID++),name:nm,namespace:ns,auftrag:an,ergebnisse:[en],...(pos||{})});return nm;}
  // Ein Typ an einem Element = VERWEIS auf seine Karte: Name und Felder nur zum Lesen, Klick öffnet die Karte, ✕ löst nur die Verbindung.
  function typVerweis(name,o){o=o||{};const r=name?recByName(name):null,rechts=o.seite==="o";
    const row=h("div",{class:"slotrow"+(rechts?" o":"")});
    if(o.port&&!rechts)row.append(o.port);
    if(o.titel)row.append(h("span",{class:"slotlbl",style:"opacity:.7"},o.titel));
    const txt=!name?(o.leer||"— ⊕ verbinden"):r?r.name+"("+(r.felder||[]).map(f=>f.name+": "+f.typ).join(", ")+")":"⚠ "+name+" — Typ fehlt";
    row.append(h("span",{class:"slotlbl",style:"flex:1"+(rechts?";text-align:right":"")+(r?";cursor:pointer":"")+(name&&!r?";color:#cf6f68":""),
      title:r?(r.namespace||"")+"."+r.name+" — Klick: Karte öffnen (Name und Felder werden dort bearbeitet)":"",onclick:r?()=>zeigeLadend("rec:"+r.name):null},txt));
    (o.extra||[]).forEach(x=>row.append(x));
    if(o.onRemove&&name)row.append(h("button",{class:"rm",title:o.removeTitel||"Verbindung lösen (der Typ bleibt)",onclick:o.onRemove},"✕"));
    if(o.port&&rechts)row.append(o.port);
    return row;}
  // Eine Verbindung zwischen zwei Port-Beschreibungen herstellen — derselbe Weg wie ⊕ (applyLink), ohne Karte auf dem Board.
  const verbindeInfo=(O,I)=>applyLink({__slot:O},{__slot:I});
  // „＋" an einer Verbindungs-Zeile: einen neuen Typ als EIGENE Karte anlegen und sofort verbinden (Name/Felder dann an seiner Karte).
  function neuerTypFuer(kind,basis,ns,verbinde){const r={name:uniq(basis),kind,namespace:ns||defaultNsGlobal(),
      felder:kind==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[]};
    MODEL.records.push(r);verbinde(r);deFlash("＋ "+r.name+" als eigene Karte angelegt und verbunden — Name und Felder an seiner Karte",true);return r;}
  const neuKnopf=(titel,on,txt)=>h("button",{class:"rm",title:titel,onclick:on},txt||"＋");
  // Der Signatur-Baustein einer Funktion: Eingang · Ausgänge (OneOf) · Fähigkeiten · Rumpf — nur auf IHRER Karte.
  function funktionSignatur(body,f){const stamm=(f.name||"Funktion").replace(/^I(?=[A-ZÄÖÜ])/,"");
    body.append(h("div",{class:"gsec",title:"record "+(f.auftrag||"X")+"(…) : IAuftrag<"+(f.name||"F")+">"},"◀ Eingang (Auftrag)"));
    const ap=port("auftrag");ap.classList.add("i");reg("fk:auftrag:"+f._id,ap,{type:"auftragFn",dir:"in",fk:f._id});
    body.append(typVerweis(f.auftrag,{port:ap,leer:"— ⊕ Auftrag verbinden",removeTitel:"Auftrag lösen (der Typ bleibt)",
      onRemove:()=>{const r=recByName(f.auftrag);if(r&&r.funktion===f.name)delete r.funktion;f.auftrag="";render();},
      extra:f.auftrag?[]:[neuKnopf("Neuen Auftrag als eigene Karte anlegen und verbinden",()=>neuerTypFuer("auftrag",stamm+"Auftrag",f.namespace,
        r=>verbindeInfo({type:"auftragFn",dir:"out",rec:r.name},{type:"auftragFn",dir:"in",fk:f._id})))]}));
    body.append(h("div",{class:"gsec",title:"Task<OneOf<…>> RufeAsync(…) — je Fall ein Ausgang, persistente Events"},"Ausgänge ▶ (OneOf)"));
    (f.ergebnisse||[]).forEach(e=>{const o=port("event");o.classList.add("o");reg("fk:out:"+f._id+":"+e,o,{type:"evtOut",dir:"out",fk:f._id});
      body.append(typVerweis(e,{port:o,seite:"o",removeTitel:"kein Ergebnis dieser Funktion mehr (der Typ bleibt)",
        onRemove:()=>{f.ergebnisse=(f.ergebnisse||[]).filter(x=>x!==e);render();}}));});
    {const op=port("open");op.classList.add("o");reg("fk:out:"+f._id+":open",op,{type:"evtOut",dir:"out",fk:f._id});
      body.append(h("div",{class:"slotrow o"},neuKnopf("Neues Ergebnis-Event als eigene Karte anlegen und verbinden",()=>neuerTypFuer("event",
          stamm+((f.ergebnisse||[]).length?"Fall":"Erledigt"),f.namespace,r=>verbindeInfo({type:"evtOut",dir:"out",fk:f._id},{type:"evtOut",dir:"in",rec:r.name}))),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"+ Ergebnis ⊕ (Event) ▶"),op));}
    funktionFaehigkeiten(body,f);
    funktionDienste(body,f);
    // Rumpf = RufeAsync der Implementierung: Code-Eingang wie am Decider (📝 Code-Block, 🤖 LLM füllt ihn). Ohne Block läuft die
    //   Funktion extern (Python-Worker über gRPC) — der Laufort steht in der Umgebung (Funktionen:<F>:Laufort = Extern).
    body.append(h("div",{class:"gsec",title:f.implementierung?"public sealed class "+f.implementierung+" : "+f.name:"noch keine C#-Implementierung im Code"},
      "Rumpf · RufeAsync"+(f.implementierung?" · "+f.implementierung:" (neu)")));
    body.append(codePort("fk:impl:"+f._id,{k:"funktion",fk:f._id},null,"RufeAsync"));
    // Python (Laufort Extern, konzept-werkbank §12.3 E13): die Implementierung in funktionen/<modul>.py — ⟳ legt sie an, sobald eine Umgebung die Funktion extern rechnet.
    {const ext=Object.entries((MODEL.rahmen||{}).umgebungen||{}).filter(([,u])=>(((u||{}).Funktionen||{})[f.name]||{}).Laufort==="Extern").map(([n])=>n);
      if(f.python){body.append(h("div",{class:"gsec",title:f.python.datei+" — class "+f.python.klasse+"("+(f.python.basis||"…Basis")+")"},
          "Rumpf · rufe (Python) · "+f.python.klasse+(ext.length?" · Laufort Extern in "+ext.join(", "):" · (Laufort Host — läuft nur mit Extern)")));
        body.append(codePort("fk:py:"+f._id,{k:"funktionPy",fk:f._id},null,"rufe"));}
      else if(ext.length)body.append(h("div",{class:"gsec",style:"opacity:.7"},"Python: Laufort Extern in "+ext.join(", ")+" — ⟳ legt die Implementierung (funktionen/…py, Basis aus der Signatur) an, dann ✎/🤖 am Rumpf."));}
    // Noch keine C#-Implementierung: eine Geste legt sie an (Klasse mit RufeAsync als throw-Stub, Konstruktor wie oben) — danach ✎/🤖.
    if(!f.implementierung&&f.entwurf==null)body.append(h("div",{class:"slotrow"},h("button",{class:"add",title:"public sealed class "+f.name.replace(/^I(?=[A-ZÄÖÜ])/,"")+" : "+f.name+" — als Geste in den Code",
      onclick:()=>{f.entwurf="";gTitel("ƒ "+f.name+": C#-Implementierung anlegen");render();}},"＋ C#-Implementierung anlegen")));}
  function funktionCard(body,f){
    // Wer ruft (Prozess-Regel „Dann rufe", Pipeline-Knoten ƒ, Ablauf „dann …") — ⊕ verbindet von hier aus.
    body.append(topSlot("auftrag","◀ gerufen von ⊕ (Prozess-Regel · Pipeline-Knoten)",{type:"rufe",dir:"in",fk:f._id},"fk:in:"+f.name));
    body.append(h("div",{class:"gsec"},"Name · Namespace"));
    if(f.datei)body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},f.name+" — aus dem Code (Umbenennen ist ein Refactoring)"));
    else body.append(nameInp(f,"name","Funktion","funktion"));
    body.append(h("input",{value:f.namespace??"",oninput:e=>f.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    funktionSignatur(body,f);
    if(INSP)dsFunktion(body,f);
    // Ablauf als Kette (Prozess): was NACH dieser Funktion kommt — nur, wo ein Prozess sie ruft. In einer Pipeline verdrahtet der Fluss.
    const sgs=[...new Set(MODEL.transitions.filter(t=>(t.dann||[]).some(d=>d.rufe===f.name)).map(t=>t.prozess))].map(n=>MODEL.sagas.find(x=>x.name===n)).filter(Boolean);
    const fl=MODEL.fluesse.filter(x=>(x.knoten||[]).some(k=>k.art==="funktion"&&k.typ===f.name));
    if(fl.length)body.append(h("div",{class:"gsec",style:"opacity:.7"},"gerufen in ⛓ "+fl.map(x=>x.name).join(", ")));
    if(!sgs.length&&fl.length)return;
    body.append(h("div",{class:"gsec"},"Ablauf ▶ dann …"));
    kettenZeilen(body,"fk:"+f._id);
    if(!sgs.length){body.append(h("div",{class:"gsec",style:"opacity:.6"},"Noch in keinem Ablauf — am Prozess unter „erst …“ oder an einer anderen Funktion unter „dann …“ einhängen."));return;}
    const erg=(f.ergebnisse||[]);if(!erg.length){body.append(h("div",{class:"gsec",style:"opacity:.6"},"Erst ein Ergebnis festlegen — es löst den nächsten Schritt aus."));return;}
    // Je Ergebnis (und Prozess) ein Ausgang: ⊕ → eine Funktion oder einen Command anklicken = nächster Schritt.
    sgs.forEach(sg=>erg.forEach(e=>body.append(slotRow("command",(sgs.length>1?sg.name+" · ":"")+"bei "+e+" ▶ dann ⊕ (ƒ · Command)","r",
      {type:"kette",dir:"out",saga:sg.name,bei:e},"kette:fk:"+f._id+":"+sg.name+":"+e))));
  }
  // Die Read-Fn eines Stores, deren Fähigkeits-Interface typ ist (neu angelegte Fns ohne festen Namen: ihr Vorschlag).
  function leseFnZu(typ){for(const st of MODEL.stores)for(const fn of st.readFns||[])
    if((fn.faehigkeit||(fn.sig?null:faehigkeitVorschlag(st,fn)))===typ)return {store:st,fn};return null;}
  // Lese-Fähigkeiten: Parameter nach IAusfuehrung (je Aufruf aus einem Bereich). Nur lesen — schreiben bleibt dem Aggregat.
  //   Verbunden wird wie am Handle: ⊕ → eine Read-Fn eines Stores anklicken (ihr Fähigkeits-Interface wird der Parameter).
  function funktionFaehigkeiten(body,f){
    body.append(h("div",{class:"gsec"},"⚙ liest (Lese-Fähigkeiten)"));
    (f.faehigkeiten||[]).forEach((p,i)=>{const s=port("store");s.classList.add("o");reg("rcall:out:fk:"+f._id+":"+i,s,{type:"rcall",dir:"out",fk:f._id});
      const z=leseFnZu(p.typ);
      body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",title:"Fähigkeit entfernen",onclick:()=>{f.faehigkeiten.splice(i,1);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"+(z?";cursor:pointer":""),title:z?"Klick: "+z.store.name+"."+z.fn.name+" öffnen":"",onclick:z?()=>zeigeLadend("fn:"+z.fn._id):null},
          "⚙ "+p.typ+" "+p.name+(z?" → "+z.store.name+"."+z.fn.name:"")),s));});
    body.append(slotRow("store","+ Lese-Fähigkeit ⊕ (Read-Fn eines Stores) ▶","r",{type:"rcall",dir:"out",fk:f._id},"rcall:out:fk:"+f._id+":open"));
    // Angeboten von (IDarf<IFunktion>, Akteur-Tor): ⊕ → einen Akteur anklicken.
    {const anb=MODEL.akteure.filter(a=>(a.darf||[]).includes(f.name));
      body.append(slotRow("darf","◀ angeboten von ⊕ (Akteur, IDarf)"+(anb.length?": ":""),"l",{type:"darf",dir:"in",rec:f.name,funktion:true},"darf:in:"+f.name));
      if(anb.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},
        ...anb.map(a=>h("button",{class:"akt-dom",onclick:()=>waehle("akt:"+a._id)},(ART_SYM[a.art]||"👤 ")+a.name))));}
    body.append(h("div",{class:"gsec",style:"opacity:.6"},(f.faehigkeiten||[]).length
      ? "Mit Fähigkeiten läuft die Funktion nur im Host (Laufort Host) — nicht in Python/extern."
      : "Laufort = Konfig der Umgebung (Funktionen:"+(f.name||"F")+":Laufort): Host (C#-Rumpf unten als Code-Block) oder Extern (Python-Worker, dann kein Code-Block)."));
    {const lo=Object.entries((MODEL.rahmen||{}).umgebungen||{}).map(([n,u])=>[n,((u||{}).Funktionen||{})[f.name]]);
      if(lo.length)body.append(h("div",{class:"gsec",style:"opacity:.8"},"Laufort je Umgebung: "+lo.map(([n,k])=>n+" = "+(k?(k.Laufort||"Host")+(k.Slots?" ×"+k.Slots:""):"⚠ nicht gebunden")).join(" · ")));}
  }

  // ══ LOGIK-ELEMENT „Domänen-Dienst" (IDienst, konzept-werkbank §12.3 E11): Signatur (Methoden) · Implementierungen (je ihr Konstruktor, ⊕ weitere Dienste) ·
  //   Rümpfe aus der Datei. Ein Dienst steht nur im Konstruktor einer Funktions- bzw. Dienst-Implementierung (CQRS089) — ⊕ an der Funktion
  //   „🧩 braucht". Welche Implementierung läuft, sagt die Umgebung (🖥 Host-Rahmen → Dienste); gebunden wird generiert (GeneratedDienste).
  const dienstByName=nm=>MODEL.dienste.find(d=>d.name===String(nm||"").replace(/\?$/,"").trim());
  const paramName=(t,belegt)=>{let n=String(t||"x").replace(/^I(?=[A-ZÄÖÜ])/,"");n=n.charAt(0).toLowerCase()+n.slice(1);
    const b=new Set(belegt||[]);if(!b.has(n))return n;let i=2;while(b.has(n+i))i++;return n+i;};
  // „Typ name, Typ name" → Parameter (Kommas in <…> gehören zum Typ).
  function parseParams(text){const teile=[];let tiefe=0,akt="";for(const ch of String(text||"")){if(ch==="<")tiefe++;if(ch===">")tiefe--;
      if(ch===","&&tiefe===0){teile.push(akt);akt="";}else akt+=ch;}if(akt.trim())teile.push(akt);
    return teile.map(x=>x.trim()).filter(Boolean).map(x=>{const i=x.lastIndexOf(" ");return i<0?{typ:x,name:paramName(x)}:{typ:x.slice(0,i).trim(),name:x.slice(i+1).trim()};});}
  // Ein neuer Dienst — EIN Weg (Funktion „＋", Domänen-Menü): I<Stamm> mit einer Methode und einer Implementierung <Stamm>.
  function neuerDienst(ns,pos){const nm=uniq("INeuerDienst"),impl=uniq(nm.replace(/^I(?=[A-ZÄÖÜ])/,""));
    const d={_id:"dn"+(NID++),name:nm,namespace:ns,methoden:[{name:"Fuehre",rueckgabe:"string",parameter:[{typ:"string",name:"eingabe"}]}],
      implementierungen:[{name:impl,konstruktor:[]}],...(pos||{})};
    MODEL.dienste.push(d);return d;}
  // Eine Konstruktor-Zeile: Dienst (🧩, ✕ löst), Konfig-Record (⚙ Konfig:<Record>) oder Rahmen (ILogger) — Klick öffnet die Karte.
  function konstruktorZeile(p,key,info,loese){const d=dienstByName(p.typ),rec=!d&&recByName(p.typ),s=d?port("dienst"):null;
    if(s){s.classList.add("o");reg(key,s,info);}
    return h("div",{class:"slotrow o"},d?h("button",{class:"rm",title:"Dienst lösen (der Dienst bleibt)",onclick:loese},"✕"):null,
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"+(d||rec?";cursor:pointer":""),title:d?"Klick: "+d.name+" öffnen":rec?"Werte aus Konfig:"+rec.name+" der Umgebung":"",
        onclick:d?()=>zeigeLadend("dn:"+d._id):rec?()=>zeigeLadend("rec:"+rec.name):null},(d?"🧩 ":rec?"⚙ ":"· ")+p.typ+" "+p.name+(rec?" (Konfig)":"")),s);}
  function funktionDienste(body,f){
    body.append(h("div",{class:"gsec",title:"Konstruktor der C#-Implementierung: Dienste (IDienst), Konfig-Records (Konfig:<Record>), ILogger<T>"},"🧩 braucht (Konstruktor)"));
    (f.konstruktor||[]).forEach((p,i)=>body.append(konstruktorZeile(p,"dn:out:fk:"+f._id+":"+i,{type:"dienst",dir:"out",fk:f._id},
      ()=>{f.konstruktor.splice(i,1);gTitel("🧩 "+p.typ+" lösen");render();})));
    const op=port("dienst");op.classList.add("o");reg("dn:out:fk:"+f._id+":open",op,{type:"dienst",dir:"out",fk:f._id});
    body.append(h("div",{class:"slotrow o"},neuKnopf("Neuen Dienst als eigene Karte anlegen und verbinden",()=>{const d=neuerDienst(f.namespace);
        verbindeInfo({type:"dienst",dir:"out",fk:f._id},{type:"dienst",dir:"in",dn:d._id});
        deFlash("＋ "+d.name+" angelegt und verbunden — Methoden und Implementierung an seiner Karte",true);}),
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"+ Dienst ⊕ (🧩) ▶"),op));}
  function dienstCard(body,d){
    body.append(topSlot("dienst","◀ genutzt von ⊕ (Funktion · Dienst-Implementierung)",{type:"dienst",dir:"in",dn:d._id},"dn:in:"+d._id));
    const nutzer=[...MODEL.funktionen.filter(f=>(f.konstruktor||[]).some(p=>dienstByName(p.typ)===d)).map(f=>["fk:"+f._id,"ƒ "+f.name]),
      ...MODEL.dienste.filter(q=>q!==d&&(q.implementierungen||[]).some(im=>(im.konstruktor||[]).some(p=>dienstByName(p.typ)===d))).map(q=>["dn:"+q._id,"🧩 "+q.name])];
    if(nutzer.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},
      ...nutzer.map(([id,t])=>h("button",{class:"akt-dom",onclick:()=>waehle(id)},t))));
    body.append(h("div",{class:"gsec"},"Name · Namespace"));
    if(d.datei)body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},d.name+" — aus dem Code (Umbenennen ist ein Refactoring)"));
    else body.append(nameInp(d,"name","Dienst","dienst"));
    body.append(h("input",{value:d.namespace??"",oninput:e=>d.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec",title:"public interface "+d.name+" : IDienst { Rückgabe Methode(Typ name, …); … }"},"Signatur (Methoden)"));
    (d.methoden||[]).forEach((m,mi)=>body.append(h("div",{class:"frow"},
      h("input",{value:m.rueckgabe??"",list:"de-typen",placeholder:"Rückgabe",style:"max-width:90px",onchange:e=>{m.rueckgabe=e.target.value.trim()||"void";render();}}),
      // Eine Methode aus dem Code: Umbenennen ist ein Refactoring (Renamer — Interface UND Implementierungen); neu: einfach der Name.
      h("input",{value:m.name??"",placeholder:"Methode",style:"max-width:110px",onchange:e=>{const v=e.target.value.trim(),alt=m.name;if(!v||v===alt)return;
        if(m._ort&&d.ausCode){gUmbenennen(m,v,alt);return;}m.name=v;render();}}),
      h("input",{value:(m.parameter||[]).map(p=>p.typ+" "+p.name).join(", "),placeholder:"Typ name, …",title:"Parameter: Typ name, Typ name",
        onchange:e=>{m.parameter=parseParams(e.target.value);render();}}),
      h("button",{class:"rm",title:"Methode entfernen (am Interface; die Implementierungen behalten ihren Code)",onclick:()=>{d.methoden.splice(mi,1);render();}},"✕"))));
    body.append(h("button",{class:"add",onclick:()=>{const da=new Set((d.methoden||[]).map(x=>x.name));let n="Neu",i=2;while(da.has(n))n="Neu"+(i++);
      (d.methoden=d.methoden||[]).push({name:n,rueckgabe:"void",parameter:[]});render();}},"+ Methode"));
    body.append(h("div",{class:"gsec",title:"Klassen, die "+d.name+" implementieren — welche läuft, wählt die Umgebung (Dienste:"+d.name+":Implementierung)"},"Implementierungen"));
    if(!(d.implementierungen||[]).length)body.append(h("div",{class:"gsec",style:"opacity:.7"},"⚠ keine — ohne Implementierung bricht der Start, sobald eine Funktion den Dienst braucht."));
    (d.implementierungen||[]).forEach((im,ii)=>{
      body.append(h("div",{class:"frow",title:"public sealed class "+im.name+"(…) : "+d.name},h("span",{class:"slotlbl"},"▸"),
        h("input",{value:im.name,placeholder:"Klasse",onchange:e=>{const v=e.target.value.trim(),alt=im.name;if(!v||v===alt)return;
          if(im._ort&&d.ausCode){gUmbenennen(im,v,alt);return;}im.name=v;render();}}),im.datei?null:h("span",{class:"gp-mark"}," neu")));
      (im.konstruktor||[]).forEach((p,pi)=>body.append(konstruktorZeile(p,"dn:out:dn:"+d._id+":"+ii+":"+pi,{type:"dienst",dir:"out",dn:d._id,impl:ii},
        ()=>{im.konstruktor.splice(pi,1);render();})));
      const op=port("dienst");op.classList.add("o");reg("dn:out:dn:"+d._id+":"+ii+":open",op,{type:"dienst",dir:"out",dn:d._id,impl:ii});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"+ braucht Dienst ⊕ (🧩) ▶"),op));
      (d.methoden||[]).forEach(m=>body.append(codePort("dn:impl:"+d._id+":"+ii+":"+m.name,{k:"dienst",dn:d._id,impl:ii,methode:m.name},null,im.name+"."+m.name)));});
    {const ein=h("input",{placeholder:"Klassenname …"});
      body.append(h("div",{class:"frow"},ein,h("button",{class:"act",title:"Eine weitere Implementierung (Klasse mit throw-Stubs) — z. B. ein Fake für die Sim-Umgebung",
        onclick:()=>{const n=ein.value.trim();if(!n||uniq(n)!==n){deFlash("⚠ Name fehlt oder ist vergeben",false);return;}
          (d.implementierungen=d.implementierungen||[]).push({name:n,konstruktor:[]});render();}},"＋ Implementierung")));}
    {const lo=Object.entries((MODEL.rahmen||{}).umgebungen||{}).map(([n,u])=>[n,((u||{}).Dienste||{})[d.name]]);
      const einzig=(d.implementierungen||[]).length===1?d.implementierungen[0].name:null;
      if(lo.length)body.append(h("div",{class:"gsec",style:"opacity:.8"},"Implementierung je Umgebung: "+lo.map(([n,k])=>n+" = "+(k&&k.Implementierung?k.Implementierung
        :einzig?einzig+" (die einzige)":"⚠ wählen")).join(" · ")+" — eingestellt im 🖥 Host-Rahmen (Dienste)"));}
  }

  // ══ AKTEURE: von wem kommt was? (docs/konzept-akteure.md §2 und §8) ══
  //   Deklariert ist nur IDarf (direkt); alles Abgeleitete (Kette, Akteur-Menge je Karte, Akteur-Rahmen, Reihenfolge, Pfade) rechnet der Server
  //   (DomainEditor/AkteurAnteile.cs → rahmen.akteurMengen, GraphExtractor/Sicht → board.sicht). Hier wird nur nachgeschlagen.
  const ART_SYM={Mensch:"👤 ",Maschine:"⚙ ",Ki:"🤖 "};
  const artBasis=a=>a==="Mensch"?"IMensch":a==="Maschine"?"IMaschine":a==="Ki"?"IKi":"IAkteur";
  const OHNE_AKT="§ohneAkteur";
  function istEingangName(nm){const r=recByName(nm);return !!r&&(r.kind==="command"||r.kind==="query");}
  // Von welchem Akteur kommt eine Nachricht: direkt (IDarf) bzw. über die Kette.
  function akteurMenge(nm){const m=((MODEL.rahmen||{}).akteurMengen||{})[nm];return {direkt:new Set(m?m.direkt:[]),kette:new Set(m?m.kette:[])};}
  // Diagnose: je Domäne die Akteur-Reihenfolge und je Rahmen die Bausteine mit ihrer Akteur-Menge.
  window.deAkteure=()=>{const r={};NODEBY.forEach(n=>{if(!VIS.has(n.id))return;const d=domKey(n),o=akteurOrdnung(d),a=o.length?akteurVon(n):"—";
    const x=r[d]||(r[d]={ordnung:o,rahmen:{}});(x.rahmen[a]=x.rahmen[a]||[]).push(n.kind+":"+n.name+" {"+[...akteurSet(n)].join(",")+"}");});return r;};
  // Domänen, in denen ein Akteur wirkt (sortiert wie die Domänen-Rahmen).
  const akteurDomaenen=name=>(SICHT().akteurDomaenen||{})[name]||[];
  // Die Akteure einer Karte (Domäne × Akteur-Rahmen).
  function akteurSet(n){const k=sk(n);return new Set(k?k.akteure:n.kind==="akteur"?[n.ref.name]:[]);}
  // Reihenfolge der Akteur-Rahmen einer Domäne (docs/konzept-akteure.md §8, konzept-werkbank §8.1) — die Ansicht kann sie umsortieren (VIEW.akteurOrdnung[dom]).
  function akteurOrdnung(dom){const aus=SICHT().akteurOrdnung[dom]||[],wunsch=((VIEW.akteurOrdnung||{})[dom]||[]).filter(x=>aus.includes(x));
    return [...wunsch,...aus.filter(x=>!wunsch.includes(x))];}
  // Akteur-Rahmen einer Karte: der ERSTE Akteur der Domäne (Reihenfolge), zu dem sie gehört; eine neue, noch leere Karte bleibt im Rahmen,
  //   in dem sie angelegt wurde (VIEW.heimAkteur). Ohne Akteur (bzw. Domäne ohne Akteure): OHNE_AKT.
  function akteurVon(n){const ord=akteurOrdnung(domKey(n));if(!ord.length)return OHNE_AKT;
    const s=akteurSet(n),h=(VIEW.heimAkteur||{})[n.id];if(!s.size&&h&&ord.includes(h))return h;
    return ord.find(a=>s.has(a))||OHNE_AKT;}

  // ══ SICHT (Werkbank P7.3, docs/konzept-werkbank.md §8.3): die Ableitungen kommen fertig vom Server (board.sicht, GraphExtractor/Sicht) —
  //   Domäne, Block, Akteure und Akteur-Rahmen, Inseln, eingeklappte Details, Kanten, Prozess-Kette, Clients; die Fokus-Kette und ⊕ fragt der
  //   Editor eigens (/api/werkbank/kette, /api/werkbank/passt). Hier wird nichts nachgerechnet; darüber liegen nur die Wünsche der Ansicht
  //   (Heimat neuer Karten, gewünschte Akteur-Reihenfolge). Code-Blöcke (📝/🤖) gibt es nur im Editor (bis P7.4): sie folgen der Karte, an der
  //   sie hängen. Eine Karte, die es im Code noch nicht gibt (gerade angelegt, die Geste läuft), hat bis zum nächsten Board keine Sicht.
  const SICHT_LEER={karten:{},kanten:[],kontrakt:[],kette:[],akteurOrdnung:{},fluesse:{},clients:{}};
  const SICHT=()=>MODEL.sicht||SICHT_LEER;
  // Die Karte, deren Rumpf eine Rumpf-Kachel zeigt (ihr Besitzer).
  function codeTraeger(n){return n&&n.kind==="rumpf"?n.own:null;}
  // Der Sicht-Eintrag einer Karte (bzw. ihres Besitzers bei Rumpf-Kacheln); null = (noch) nicht im Code.
  function sk(n){if(!n)return null;const k=SICHT().karten[n.id];if(k)return k;
    if(n.kind==="rumpf")return SICHT().karten[n.own.id]||null;return null;}
  // 👤 AKTEUR: Name, Namespace, und EINE Liste — was er darf (IDarf<T>). ⊕ „+ darf" → passende Commands/Queries
  //   leuchten → anklicken = erlauben, ✓-Karte = entziehen. Was er HÖREN darf, ist abgeleitet (Generator), keine Eingabe.
  function akteurCard(body,a){
    body.append(h("div",{class:"gsec"},"Name · Namespace"));
    body.append(nameInp(a,"name","AkteurName","akteur"));
    body.append(h("input",{value:a.namespace??"",oninput:e=>a.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    // Art = Basistyp (IMensch / IMaschine / IKi) — ein Akteur ist immer ein Record, nie ein Dienst.
    body.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Art"),
      h("select",{onchange:e=>{a.art=e.target.value||undefined;render();}},
        ...[["","— (nur IAkteur)"],["Mensch","👤 Mensch (IMensch)"],["Maschine","⚙ Maschine (IMaschine)"],["Ki","🤖 KI (IKi)"]]
          .map(([v,t])=>h("option",{value:v,...((a.art||"")===v?{selected:"selected"}:{})},t)))));
    body.append(h("div",{class:"gsec",title:"public sealed record "+a.name+" : "+artBasis(a.art)+", IDarf<…>"},"darf ▶ — was er SELBST hineingibt"));
    if(!(a.darf||[]).length)body.append(h("div",{class:"gp-row gp-leer"},"— noch nichts (am Tor käme er nirgends durch)"));
    (a.darf||[]).filter(nm=>!MODEL.funktionen.some(f=>f.name===nm)).forEach(nm=>{const r=recByName(nm);
      const art=r?kindLabel(r.kind):"⚠ unbekannt";
      const s=anchorDot("darf");s.classList.add("o");reg("akt:darf:"+a._id+":"+nm,s,{type:"darf",dir:"out",akt:a._id,anker:true});
      body.append(h("div",{class:"slotrow o"},
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},nm+" · "+art),
        h("button",{class:"rm",title:"Recht entziehen",onclick:()=>{a.darf=a.darf.filter(x=>x!==nm);render();}},"✕"),s));});
    body.append(slotRow("darf","+ darf ⊕ ("+akteurDarfLabel()+") ▶","r",{type:"darf",dir:"out",akt:a._id},"akt:darf:"+a._id+":open"));
    // Angebotene Katalog-Funktionen (IDarf<IFunktion>, Akteur-Tor): er führt sie DRAUSSEN aus (z. B. ein Python-Worker am Vermittler).
    {const fs=(a.darf||[]).filter(nm=>MODEL.funktionen.some(f=>f.name===nm));
      body.append(h("div",{class:"gsec",title:"public sealed record "+a.name+" : …, IDarf<IFunktion> — am Vermittler darf er diese Funktion anbieten (Laufort extern)"},"ƒ bietet an (führt aus)"));
      fs.forEach(nm=>{const f=MODEL.funktionen.find(x=>x.name===nm);const s=anchorDot("darf");s.classList.add("o");reg("akt:darf:"+a._id+":"+nm,s,{type:"darf",dir:"out",akt:a._id,anker:true});
        body.append(h("div",{class:"slotrow o"},
        h("span",{class:"slotlbl",style:"flex:1;text-align:right;cursor:pointer",onclick:()=>waehle("fk:"+f._id)},"ƒ "+nm),
        h("button",{class:"rm",title:"nicht mehr anbieten",onclick:()=>{a.darf=a.darf.filter(x=>x!==nm);render();}},"✕"),s));});
      // ⊕ → eine Funktions-Karte anklicken (IDarf<IFunktion>).
      body.append(slotRow("darf","+ bietet an ⊕ (Funktion) ▶","r",{type:"darf",dir:"out",akt:a._id,nurFunktion:true},"akt:darffk:"+a._id+":open"));}
    // Was in seinem Namen über eine Kette entsteht (abgeleitet, nicht deklariert): Fluss/Prozess aus seinen Events.
    {const bew=Object.entries((MODEL.rahmen||{}).akteurMengen||{}).filter(([nm,m])=>(m.kette||[]).includes(a.name)&&!(m.direkt||[]).includes(a.name)&&istEingangName(nm)).map(([nm])=>nm).sort();
      body.append(h("div",{class:"gsec",title:"abgeleitet: Commands, die ein Fluss oder ein Prozess aus Events dieses Akteurs erzeugt — kein IDarf nötig, am Tor nicht erlaubt"},"bewirkt über die Kette (abgeleitet)"));
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},bew.length?bew.join(", "):"— nichts"));}
    const wirkt=akteurDomaenen(a.name);
    if(wirkt.length){body.append(h("div",{class:"gsec"},"wirkt in"));
      body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px"},...wirkt.map(d=>h("button",{class:"akt-dom",title:"Rahmen "+domLabel(d)+" · "+a.name+" einpassen",
        onclick:()=>{const fr=world&&[...world.querySelectorAll(".grahmen.akt")].find(x=>x.dataset.ns===d&&x.dataset.akt===a.name);
          if(fr)einpassenRahmen({x1:fr.offsetLeft,y1:fr.offsetTop,x2:fr.offsetLeft+fr.offsetWidth,y2:fr.offsetTop+fr.offsetHeight});}},domLabel(d)))));}
    // Vertrag (Akteur-Konzept §3): worauf er DRAUSSEN reagiert — je Auf(Event) eine Karte „Zusage" (Spalte neben dem Akteur). ⊕ → Events leuchten.
    body.append(h("div",{class:"gsec",title:"public interface "+vertragsTyp(a)+" : IAkteurVertrag<"+a.name+"> { … Auf(Event e); } — der Client (Python, Blazor) implementiert ihn; was er so hineingibt, darf er"},
      "Zusagen ◀ (Vertrag "+vertragsTyp(a)+")"));
    (a.vertrag||[]).forEach((r,i)=>{const s=anchorDot("event");s.classList.add("i");reg("akt:auf:"+a._id+":"+i,s,{type:"evtUse",dir:"in",akt:a._id,aufIdx:i,anker:true});
      const row=h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"flex:1"},(r.eingang||"?")+((r.ausgaenge||[]).length?" → "+r.ausgaenge.join(", ")+(r.strom?" (Strom)":""):" · zur Kenntnis")),
        h("button",{class:"rm",title:"Zusage entfernen",onclick:e=>{e.stopPropagation();a.vertrag.splice(i,1);render();}},"✕"));
      row.onclick=()=>waehle(aufId(a,i));body.append(row);});
    {const oi=port("event");oi.classList.add("i");reg("akt:auf:"+a._id+":open",oi,{type:"evtUse",dir:"in",akt:a._id,aufIdx:"open"});
      body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Zusage ⊕ (Event)")));}
    // 🔌 Clients (docs/konzept-akteure.md §4): wer diesen Vertrag trägt (Port für „trägt ⊕") und wo der Akteur sonst ankommt.
    if((a.vertrag||[]).length){const tp=port("traegt");tp.classList.add("i");reg("akt:traegt:"+a._id,tp,{type:"traegt",dir:"in",akt:a._id});
      const tr=clientTraeger(vertragsTyp(a));
      body.append(h("div",{class:"slotrow"},tp,h("span",{class:"slotlbl",style:"flex:1"},"🔌 getragen von: "+(tr.length?tr.map(clientAnzeige).join(", "):"⚠ keinem Client (GR-GETRAGEN)"))));}
    {const cs=MODEL.clients.filter(c=>clientAkteure(c).includes(a.name));
      if(cs.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},h("span",{class:"slotlbl"},"🔌 Clients:"),
        ...cs.map(c=>h("button",{class:"akt-dom",onclick:()=>waehle("cl:"+c._id)},clientAnzeige(c)))));}
    const hoert=akteurHoert(a);
    body.append(h("div",{class:"gsec",title:(a.vertrag||[]).length?"abgeleitet: genau die Events seines Vertrags + Events der Projektionen hinter seinen Queries"
      :"abgeleitet: Events der Aggregate seiner Commands + Events der Projektionen hinter seinen Queries"},"hört (abgeleitet)"));
    body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},hoert.length?hoert.join(", "):"— nichts"));
  }
  // 🔌 CLIENT-Panel (docs/konzept-akteure.md §4): der eine Vertrag dieses Clients — trägt (Akteur-Vertrags-Teile) · sendet/fragt ·
  //   hört (Kenntnis). Je Abschnitt ⊕ → passende Karten leuchten → anklicken = verbinden / ✓ = lösen. Verkörpert ist abgeleitet.
  function clientCard(body,c){
    body.append(h("div",{class:"gsec",title:"public interface "+c.name+" : IClientVertrag, …"},"Name (Interface) · Namespace"));
    if(!c.datei)body.append(nameInp(c,"name","IClient","client"));
    else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},c.name+" — aus dem Code (Umbenennen ist ein Refactoring)"));
    body.append(h("input",{value:c.namespace??"",oninput:e=>c.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},"Handshake: „"+clientAnzeige(c)+"“ · Python-Basis "+clientAnzeige(c)
      +"Basis (domain_client/generated/vertraege.py) · Server: GeneratedClientVertraege"));
    const akt=clientAkteure(c);
    body.append(h("div",{class:"gsec",title:"abgeleitet: Akteure der getragenen Teile + IDarf-Halter von Sendet/Fragt (falls kein Teil-Akteur es darf). Am Handshake gilt: ∩ Akteure des Tokens"},"verkörpert (abgeleitet)"));
    body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},...(akt.length?akt.map(n=>{const a=MODEL.akteure.find(x=>x.name===n);
      return h("button",{class:"akt-dom",onclick:()=>a&&waehle("akt:"+a._id)},(ART_SYM[a&&a.art]||"👤 ")+n);}):[h("span",{},"— noch keiner")])));
    // ↺ trägt: Akteur-Vertrags-Teile (⊕ → Akteure mit Vertrag leuchten).
    body.append(h("div",{class:"gsec",title:"interface "+c.name+" : …, IKlassifizierer — die Zusagen des Teils gehen über diese Leitung, im Namen seines Akteurs"},"↺ trägt (Akteur-Verträge)"));
    (c.traegt||[]).forEach(t=>{const a=teilAkteur(c,t),s=anchorDot("traegt");s.classList.add("o");reg("cl:traegt:"+c._id+":"+t,s,{type:"traegt",dir:"out",client:c._id,anker:true});
      const n=clientPorts(c).filter(p=>p.ri==="zusage"&&p.teil===t&&!p.fehlt).length;
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},t+(a?" · "+a.name+" · "+n+(n===1?" Zusage":" Zusagen"):" · ⚠ kein Akteur-Vertrag")),
        h("button",{class:"rm",title:"nicht mehr tragen",onclick:()=>{c.traegt=ohneX(c.traegt,t);render();}},"✕"),s));});
    body.append(slotRow("traegt","+ trägt ⊕ (Akteur mit Vertrag) ▶","r",{type:"traegt",dir:"out",client:c._id},"cl:traegt:"+c._id+":open"));
    // ▶ sendet / ? fragt: der spontane Rand — nur, was ein Akteur darf (sonst ⚠, GR-CLIENT-BEFUGT).
    body.append(h("div",{class:"gsec",title:"ISendet<T> / IFragt<T> — muss ein Akteur per IDarf dürfen; der Client-Vertrag schneidet aus der Befugnis"},"▶ sendet · ? fragt"));
    [...(c.sendet||[]).map(x=>["sendet",x]),...(c.fragt||[]).map(x=>["fragt",x])].forEach(([f,nm])=>{
      const s=anchorDot("darf");s.classList.add("o");reg("cl:darf:"+c._id+":"+nm,s,{type:"darf",dir:"out",client:c._id,anker:true});
      const hs=darfHalter(nm);
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},(f==="sendet"?"▶ ":"? ")+nm
          +(hs.length?" · als "+hs.join("/"):" · ⚠ kein Akteur darf es")),
        h("button",{class:"rm",title:"aus dem Vertrag nehmen",onclick:()=>{c[f]=ohneX(c[f],nm);render();}},"✕"),s));});
    body.append(slotRow("darf","+ sendet / fragt ⊕ (Command · Query · Trigger) ▶","r",{type:"darf",dir:"out",client:c._id},"cl:darf:"+c._id+":open"));
    // ◀ hört: eigene Kenntnis (void Auf(E)) — ohne Ausgabe.
    body.append(h("div",{class:"gsec",title:"void Auf(E e) im Client-Vertrag — nur zur Kenntnis (UI-Aktualisierung), verlierbar zugestellt"},"◀ hört (Kenntnis)"));
    (c.kenntnis||[]).forEach(nm=>{const s=anchorDot("event");s.classList.add("i");reg("cl:kenntnis:"+c._id+":"+nm,s,{type:"evtUse",dir:"in",client:c._id,anker:true});
      body.append(h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"flex:1"},"◀ "+nm),
        h("button",{class:"rm",title:"nicht mehr hören",onclick:()=>{c.kenntnis=ohneX(c.kenntnis,nm);render();}},"✕")));});
    {const oi=port("event");oi.classList.add("i");reg("cl:kenntnis:"+c._id+":open",oi,{type:"evtUse",dir:"in",client:c._id});
      body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ hört ⊕ (Event)")));}
    clientImplementierung(body,c,null);
    // Die Leitungen dieses Clients (je Ziel-Rahmen ein Bündel) — Klick fächert auf.
    const bs=clientBuendel(c);
    if(bs.length){body.append(h("div",{class:"gsec"},"🔌 Leitungen (je Ziel-Rahmen)"));
      bs.forEach(b=>{const row=h("div",{class:"gp-row",title:buendelText(b)},h("span",{class:"gp-t"},rahmenLabel(b.key)),
        h("span",{class:"gp-mark"},(b.durabel?"durabel · ":"")+buendelText(b)));row.onclick=()=>auffaechern(c,{keys:[b.key]});body.append(row);});}
  }
  // ⚙ IMPLEMENTIERUNG eines Clients — das Verhalten des Akteurs DRAUSSEN: je Zusage/Kenntnis ein Logik-Element (◀ Event, Ausgänge ▶ =
  //   Ausgabe-Vertrag, 📝 Rumpf + optional 🤖). Code-Fakt: eine Python-Klasse, die die aus dem Vertrag generierte Basis erbt.
  //   nurEvent: nur diese eine Zusage (Ansicht an der Zusage-Karte).
  function clientImplementierung(body,c,nurEvent){const ims=c.implementierungen||[];
    if(!nurEvent)body.append(h("div",{class:"gsec",title:"class X("+clientAnzeige(c)+"Basis[…]) im Python-Projekt — die Basis erzeugt der Codegen aus dem Vertrag; je Auf ihre Methode"},"⚙ Implementierung (Verhalten draußen)"));
    if(!ims.length){if(nurEvent)return;
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},
        "— noch keine: eine Python-Klasse, die "+clientAnzeige(c)+"Basis erbt (die Basis entsteht beim Bauen aus dem Vertrag)."));
      const pk=(MODEL.rahmen||{}).pythonPaket;
      if(pk)body.append(h("button",{class:"add",title:"⟳ legt nach dem Bauen der Basis "+pk+"/… an: die Klasse mit je Zusage/Kenntnis der Methode der generierten Basis (Platzhalter-Rumpf) — danach je Methode 📝/🤖",
        onclick:()=>{const kl=clientAnzeige(c)||"Client";(c.implementierungen=c.implementierungen||[]).push({sprache:"python",klasse:kl,
          datei:pk+"/"+kl.replace(/(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])/g,"_$1$2").toLowerCase()+".py",rumpfe:[],neu:true});render();}},"+ 🐍 Python-Implementierung"));
      return;}
    ims.forEach((im,ii)=>{const rs=(im.rumpfe||[]).filter(x=>!nurEvent||x.event===nurEvent);if(!rs.length&&!(im.neu&&!nurEvent))return;
      if(im.neu&&!rs.length&&!nurEvent){body.append(h("div",{class:"gsec",style:"opacity:.85",title:im.datei},"🐍 "+im.klasse+" · "+im.datei+" (neu)"),
        h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},"Entsteht beim nächsten ⟳ (nach dem Bauen der Basis) — je Zusage/Kenntnis eine Methode, dann hier je Methode 📝/🤖."),
        h("button",{class:"rm",title:"doch nicht anlegen",onclick:()=>{c.implementierungen.splice(ii,1);render();}},"✕"));return;}
      body.append(h("div",{class:"gsec",style:"opacity:.85",title:im.datei},"🐍 "+im.klasse+" · "+(im.datei||"").split("/").pop()+(nurEvent?" · 🔌 "+clientAnzeige(c):"")));
      rs.forEach(x=>{if(!nurEvent)body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl",style:"flex:1"},
          "◀ "+x.event+((x.ausgaenge||[]).length?"  → "+x.ausgaenge.join(" | ")+" ▶":"  · Kenntnis"))));
        body.append(codePort("cla:"+c._id+":"+ii+":"+x.event+(nurEvent?":z":""),{k:"clientAuf",client:c._id,impl:ii,event:x.event},null,
          x.methode+(x.rumpf==null?" (fehlt)":"")));});});}
  // Abgeleitet wie im Generator (AkteurRechteGenerator): Aggregat der erlaubten Commands → dessen Events; erlaubte Query →
  //   Reader → Projektion → deren Handle-Events. Nur Anzeige — die Laufzeit-Tabelle erzeugt der Generator.
  function akteurHoert(a){const evs=new Set(),vertrag=(a.vertrag||[]).length>0;
    (a.vertrag||[]).forEach(r=>r.eingang&&evs.add(r.eingang));   // mit Vertrag: exakt seine Eingänge
    (a.darf||[]).forEach(nm=>{const r=recByName(nm);if(!r)return;
      if(r.kind==="command"&&!vertrag){const agg=(MODEL.decider.find(d=>d.command===nm)||{}).aggregat;
        if(agg)MODEL.decider.filter(d=>d.aggregat===agg).forEach(d=>(d.ergibt||[]).forEach(o=>o.event&&evs.add(o.event)));}
      if(r.kind==="query")MODEL.reader.forEach(rd=>{if(!(rd.handles||[]).some(hd=>hd.query===nm))return;
        const p=MODEL.projektionen.find(x=>x.name===rd.projektion);(p&&p.handles||[]).forEach(hd=>hd.event&&evs.add(hd.event));});});
    return [...evs].sort();}
  // ◀ ZUSAGE (Vertrag, Akteur-Konzept §3): eine Methode Auf(Event) im Vertrag des Akteurs — Eingang, Ausgänge (konkrete Commands), Strom.
  //   Implementiert wird sie draußen (Python-Worker gegen die generierte Basis); hier steht nur die Signatur.
  function aufCard(body,n){const a=n.own.ref,r=n.ref,i=n.idx;
    body.append(h("div",{class:"gsec",title:"public interface "+vertragsTyp(a)+" : IAkteurVertrag<"+a.name+">"},"Vertrag "+vertragsTyp(a)+" · 👤 "+a.name));
    // Der Name ist nur im Entwurf frei — ein Vertrag aus dem Code behält seinen (Umbenennen ist ein Refactoring, kein Zeichnen).
    if(!a.datei)body.append(h("div",{class:"frow",title:"Name des Vertrags-Interfaces (leer = I + Akteur)"},h("span",{class:"slotlbl"},"Vertrag"),
      inp(a.vertragName,v=>{const alt=vertragsTyp(a);a.vertragName=v||undefined;const neu=vertragsTyp(a);
        MODEL.clients.forEach(c=>{c.traegt=(c.traegt||[]).map(t=>t===alt?neu:t);});},"I"+a.name)));
    const sl=port("event");sl.classList.add("i");reg("auf:in:"+a._id+":"+i,sl,{type:"evtUse",dir:"in",akt:a._id,aufIdx:i});
    body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},"◀ Auf "+(r.eingang||"?")),
      h("button",{class:"rm",title:"Zusage entfernen",onclick:()=>{a.vertrag.splice(i,1);render();}},"✕")));
    (r.ausgaenge||[]).forEach((c,ci)=>{const s=port("command");s.classList.add("o");reg("auf:send:"+a._id+":"+i+":"+c,s,{type:"sagaCmd",dir:"out",akt:a._id,aufIdx:i});
      body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{r.ausgaenge.splice(ci,1);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"gibt hinein "+c+" ▶"),s));});
    const so=port("command");so.classList.add("o");reg("auf:send:"+a._id+":"+i+":open",so,{type:"sagaCmd",dir:"out",akt:a._id,aufIdx:i});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},(r.ausgaenge||[]).length?"+ Ausgang ▶ (Command)":"+ Ausgang ▶ (Command) — ohne: nur zur Kenntnis"),so));
    body.append(h("label",{class:"cbx",title:"IAsyncEnumerable<OneOf<…>>: mehrere Commands je Zusage (z. B. Fortschritt)"},
      h("input",{type:"checkbox",onchange:e=>{if(e.target.checked)r.strom=true;else delete r.strom;render();},...(r.strom?{checked:"checked"}:{})}),"Strom (mehrere Commands je Zusage)"));
    body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal;font-family:monospace"},
      ((r.ausgaenge||[]).length?(r.strom?"IAsyncEnumerable<OneOf<"+r.ausgaenge.join(", ")+">>":"OneOf<"+r.ausgaenge.join(", ")+">"):"void")+" Auf("+(r.eingang||"?")+" e);"));
    // Das VERHALTEN: je Client, der diesen Vertrags-Teil trägt, der Rumpf seiner Implementierung (📝 + optional 🤖).
    const tr=clientTraeger(r.teil||vertragsTyp(a));
    body.append(h("div",{class:"gsec",title:"Die Zusage setzt die Software um, die den Vertrag trägt (Client) — dort steht ihr Rumpf"},"Rumpf · umgesetzt in"));
    if(!tr.length)body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},"— kein Client trägt "+(r.teil||vertragsTyp(a))+" (⊕ am Client: trägt)"));
    tr.forEach(c=>{if((c.implementierungen||[]).length)clientImplementierung(body,c,r.eingang);
      else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:pointer;white-space:normal",onclick:()=>waehle("cl:"+c._id)},"🔌 "+clientAnzeige(c)+": noch keine Implementierung"));});
  }

  // Code-Karte (Werkbank P7.1/P7.4, Zielbild §9): ein Typ, den das Lesen nicht als Baustein erkennt — roh aus der Datei; ⚠ mit Grund, wenn
  //   ein Basistyp nicht aufgelöst ist (die Karte verschwindet, sobald er es wieder ist), sonst Hilfscode ohne Baustein-Rolle.
  function codeKarteCard(body,c){const o=c._ort||{};
    body.append(h("div",{class:"blab"},(c.unaufgeloest||[]).length?"⚠ Basistyp nicht aufgelöst: "+c.unaufgeloest.join(", "):"Hilfscode — kein Baustein erkannt, roh aus der Datei"));
    body.append(h("div",{class:"blab",style:"opacity:.75"},(c.art||"Typ")+" · "+(o.datei||c.datei||"")+(o.zeile?":"+o.zeile:"")));
    body.append(h("pre",{class:"gcodeprev",style:"cursor:default;white-space:pre-wrap"},c.code||""));}
  // ══ 🤖 LLM-CODE-BLÖCKE — der ganze Weg am 🤖-Knoten, EIN Klick (▶):
  //   claude -p (Abo, keine API) füllt den Rumpf → Prüfung (Syntax; C#: probeweise im warmen Workspace, Compiler + Analyzer,
  //   bis zu 3 Reparaturrunden) → GEPRÜFT: sofort in die echte .cs → Build des Laufzeit-Projekts (alle Code-
  //   Generatoren inkl. Proto-Prepass) → Code + Kontexte neu eingelesen. Hat der Block noch keine Methode (Decide/Apply),
  //   wird sie vorher als Platzhalter geschrieben (Geste). Nur ein UNGEPRÜFTER Kandidat bleibt Vorschlag (✓ am Block).
  //   Wahrheit ist die Datei; ein Vorschlag hängt am SLOT-Schlüssel (stabil über Neu-Einlesen) und ist lokal gesichert.
  const LLM_VKEY="bractor-llm-vorschlaege";
  let VOR={};try{VOR=JSON.parse(localStorage.getItem(LLM_VKEY)||"{}")||{};}catch(e){}
  const saveVor=()=>{try{localStorage.setItem(LLM_VKEY,JSON.stringify(VOR));}catch(e){}};
  const LAUF={}, MELD={}, ZULETZT={};   // Slot → läuft seit / letzte Meldung / gerade übernommen (Rückgängig anbieten)
  let EINLESEN=false, LLM_STATUS=null;
  const neuZeichnen=()=>{try{render();}catch(e){}};
  async function llmStatus(){try{LLM_STATUS=await fetch("/api/llm/status").then(r=>r.json());}catch(e){LLM_STATUS=null;}return LLM_STATUS;}
  // Kontexte fehlen / werden erzeugt → nachfragen, bis sie da sind (danach Ruhe).
  setInterval(async()=>{if(LLM_STATUS&&LLM_STATUS.index&&!LLM_STATUS.aktualisierungLaeuft)return;
    const vor=JSON.stringify(LLM_STATUS);await llmStatus();if(JSON.stringify(LLM_STATUS)!==vor)neuZeichnen();},4000);
  llmStatus().then(neuZeichnen);
  setInterval(()=>document.querySelectorAll("#de [data-lauf]").forEach(el=>{const t=Math.round((Date.now()-(+el.dataset.lauf))/1000);
    el.textContent=el.textContent.replace(/ \(\d+ s\)$/,"")+" ("+t+" s)";}),1000);
  const postLlm=(pfad,obj)=>fetch(pfad,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(obj)}).then(r=>r.json());

  // anpassung: Wunsch zum bestehenden Rumpf; basisRumpf = der aktuelle Rumpf (offener Vorschlag, sonst der Datei-Spiegel).
  async function llmAusfuehren(kid,auftrag,anpassung,basisRumpf){
    auftrag=(auftrag||"").trim();
    if(!auftrag){MELD[kid]={k:"warn",t:"Erst den Auftrag schreiben — ohne Auftrag kein Aufruf."};neuZeichnen();return;}
    if(LAUF[kid])return;
    const v=VOR[kid];
    const anfrage={id:kid,auftrag,rumpf:anpassung?(v?v.rumpf:(basisRumpf||null)):null,anpassung:anpassung||null,maxRunden:3};
    LAUF[kid]={seit:Date.now(),was:anpassung?"passt an · schreibt · baut":"LLM schreibt · prüft · baut"};delete MELD[kid];neuZeichnen();
    let r;try{r=await postLlm("/api/llm/ausfuehren",anfrage);}
    catch(e){r={ok:false,grund:"Werkbank offline"};}
    // Noch keine Methode im Code (neu gezeichneter Decider/Applier): erst die Struktur schreiben (Platzhalter) + einlesen,
    //   dann derselbe Durchlauf. Leseseiten-Blöcke deckt der Scaffolder (noch) nicht — dort bleibt die Meldung.
    if(r&&!r.ok&&r.unbekannt&&/^(decide|apply|funktion)\|/.test(kid)){
      LAUF[kid].was="schreibt die Struktur (Geste) · liest ein";neuZeichnen();
      await gesteFlush();try{await fetch("/api/llm/aktualisieren",{method:"POST"});}catch(e){}
      LAUF[kid].was="LLM schreibt · prüft · baut";neuZeichnen();
      try{r=await postLlm("/api/llm/ausfuehren",anfrage);}catch(e){r={ok:false,grund:"Werkbank offline"};}
      if(r&&!r.ok&&r.unbekannt)r.grund=kid.startsWith("funktion|")?"Implementierung konnte nicht angelegt werden — Funktion vollständig (Auftrag ⊕, mindestens ein Ergebnis ⊕)?"
        :"Methode konnte nicht angelegt werden — Decider/Applier vollständig verdrahtet (Command/Event, Aggregat, Ausgänge)?";}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+(r.grund||"Fehler")};neuZeichnen();return;}
    const runden=r.runden||[], ende=runden[runden.length-1]||{};
    const dauer=runden.reduce((a,x)=>a+(x.dauerMs||0),0), info=runden.length+" Runde(n) · "+(dauer/1000).toFixed(1)+" s";
    if(r.geschrieben){   // geprüft → in der Datei → Generatoren gelaufen
      delete VOR[kid];saveVor();ZULETZT[kid]=true;
      MELD[kid]=r.bau&&r.bau.ok?{k:"ok",t:"✓ geschrieben: "+r.datei+":"+r.zeile+" · Generatoren grün ("+info+")"}
        :{k:"err",t:"⚠ geschrieben, aber Build rot: "+(((r.bau&&r.bau.fehler)||[])[0]||"")+" — ↶ Rückgängig am Block"};
      await neuEinlesen();return;}
    if(ende.fehler)MELD[kid]={k:"err",t:"⚠ "+ende.fehler};
    else if(ende.ergebnis==="ausserhalb")MELD[kid]={k:"warn",t:"⛔ Nicht in diesem Block lösbar: "+ende.ausserhalb+" — erst die Struktur im Editor ergänzen."};
    else if(!ende.rumpf)MELD[kid]={k:"err",t:"⚠ Antwort ohne Code ("+info+") — Details ↗"};
    else{
      VOR[kid]={rumpf:ende.rumpf,basisHash:r.basisHash,ok:!!ende.ok,befunde:ende.befunde||[],auftrag,zeit:new Date().toISOString()};saveVor();
      MELD[kid]={k:"warn",t:"Nicht geschrieben — Vorschlag mit "+(ende.befunde||[]).length+" Befund(en) nach "+info+": "+(ende.befunde||[])[0]+" · ↻ Anpassen oder ✓ trotzdem übernehmen"};}
    neuZeichnen();}

  async function llmUebernehmen(kid){const v=VOR[kid];if(!v||LAUF[kid])return;
    LAUF[kid]={seit:Date.now(),was:"schreibt + baut"};neuZeichnen();
    let r;try{r=await postLlm("/api/llm/uebernehmen",{id:kid,rumpf:v.rumpf,auftrag:v.auftrag,basisHash:v.basisHash,bauen:true});}
    catch(e){r={ok:false,grund:"Werkbank offline"};}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+r.grund+(/Hash/.test(r.grund||"")?" → ▶ Neu ausführen":"")};neuZeichnen();return;}
    delete VOR[kid];saveVor();ZULETZT[kid]=true;
    MELD[kid]=r.bau&&r.bau.ok?{k:"ok",t:"✓ geschrieben: "+r.datei+":"+r.zeile+" · Build grün (Generatoren gelaufen)"}
      :{k:"err",t:"⚠ geschrieben, aber Build rot: "+(((r.bau&&r.bau.fehler)||[])[0]||"")+" — ↶ Rückgängig am Block"};
    await neuEinlesen();}

  async function llmRueckgaengig(kid){if(LAUF[kid])return;LAUF[kid]={seit:Date.now(),was:"stellt wieder her + baut"};neuZeichnen();
    let r;try{r=await postLlm("/api/llm/rueckgaengig",{id:kid,bauen:true});}catch(e){r={ok:false,grund:"Werkbank offline"};}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+r.grund};neuZeichnen();return;}
    delete ZULETZT[kid];MELD[kid]={k:"",t:"↶ alter Rumpf wiederhergestellt"+(r.bau&&!r.bau.ok?" · Build rot":"")};await neuEinlesen();}

  function llmVerwerfen(kid){delete VOR[kid];saveVor();MELD[kid]={k:"",t:"Vorschlag verworfen — der Datei-Rumpf gilt."};neuZeichnen();}

  // Nach jedem Schreiben: EIN GraphExtractor-Lauf liest Modell + Kontexte neu → Spiegel und Nachbar-Kontexte
  //   stehen auf dem neuen Code; solange sind ▶/✓ gesperrt (sonst sähe der nächste Block veraltete Nachbarn).
  async function neuEinlesen(){EINLESEN=true;neuZeichnen();try{await deReload();}finally{EINLESEN=false;await llmStatus();neuZeichnen();}}

  // Kurzvorschau eines Code-/Intent-Textes (erste Zeilen) für den Knoten.
  function codePreview(t,leer,voll){const s=(t||"").replace(/\t/g,"    ").split("\n");const max=voll?1e9:7;
    const head=s.slice(0,max).join("\n");return (t&&t.trim())?(head+(s.length>max?"\n…":"")):(leer||"// leer");}

  // ══ COMFYUI-NODE-EDITOR: getippte Slots (Punkte) + Bézier-Kanten statt Dropdowns.
  //    Kopf ziehen = verschieben (rastet 20px); von einem Slot-Punkt ziehen = verbinden;
  //    Fläche ziehen = pannen; Mausrad = zoomen. Verbindungen (out→in, typgleich):
  //    Command→Decider · Decider→Event(Ergibt) · Decider→Aggregat(links) · Event→Applier ·
  //    Applier→Aggregat(rechts) · Applier→State-Feld(oben, optionale Zuweisungs-Markierung).
  const SVGNS="http://www.w3.org/2000/svg";
  const GRID=20;
  const NODELABEL={host:"Host",akteur:"Akteur",client:"Client",auf:"Zusage",command:"Command",event:"Event",rejection:"Ablehnung",valueobject:"Value Object",enum:"Enum",aggregate:"Aggregat",decider:"Decider",applier:"Applier",saga:"Prozess",transition:"Regel",query:"Query",queryresponse:"Response",readmodel:"Read Model",store:"Store",projektion:"Projektion",reader:"Reader",reaktion:"Reaktion",quelle:"Quelle",fluss:"Fluss",rumpf:"Code",code:"Code",state:"State",konfig:"Konfiguration",handle:"Handle",fn:"Store-Fn",funktion:"Funktion",dienst:"Dienst",auftrag:"Auftrag",flussknoten:"Pipeline"};
  let PAN={x:40,y:30,s:1}, canvas=null, world=null, svg=null, svgTop=null, SLOTS={};
  // Typ-Navigation: HLKIND = aktuell hervorgehobener Node-Typ (Board+Minimap); JUMPIX = Sprung-Cursor je Typ.
  let HLKIND=null; const JUMPIX={};
  // Alle Knoten eines Typs im Board + Minimap hervorheben (Hover über den Palette-Button).
  function highlightKind(kind,on){
    HLKIND=on?kind:null;
    if(world){const ids=new Set(graphNodes().filter(n=>n.kind===kind).flatMap(n=>VERTRETER.get(n.id)||[n.id]));
      world.querySelectorAll(".gnode2").forEach(el=>el.classList.toggle("typehi",on&&ids.has(el.dataset.id)));}
    if(MM&&MM.svg)MM.svg.querySelectorAll("rect[data-kind]").forEach(r=>r.classList.toggle("mmhi",on&&r.dataset.kind===kind));
  }
  // Viewport auf einen Knoten zentrieren.
  //   Eingeklappte Details (Decider/Code/…) → auf ihren sichtbaren Besitzer.
  function centerOn(n){if(!canvas)return;n=NODEBY.get(vertreterId(n.id))||n;const el=world.querySelector('[data-id="'+n.id+'"]');
    const w=el?el.offsetWidth:250,h=el?el.offsetHeight:120,cr=canvas.getBoundingClientRect(),p=P(n);
    PAN.x=cr.width/2-((p.x||0)+w/2)*PAN.s;PAN.y=cr.height/2-((p.y||0)+h/2)*PAN.s;applyPan();}
  // Kurzer Puls-Ring auf einem Knoten (nach dem Sprung).
  function pulseNode(n){const el=world&&world.querySelector('[data-id="'+vertreterId(n.id)+'"]');if(!el)return;
    el.classList.add("pulse");setTimeout(()=>el.classList.remove("pulse"),900);}
  // Ctrl/Cmd+Klick auf den Palette-Button: der Reihe nach zum nächsten Knoten dieses Typs springen.
  function jumpNextOfKind(kind){const list=graphNodes().filter(n=>n.kind===kind);if(!list.length)return;
    const i=(((JUMPIX[kind]??-1)+1))%list.length;JUMPIX[kind]=i;const n=list[i];
    centerOn(n);pulseNode(n);highlightKind(kind,true);
    deFlash("↪ "+(NODELABEL[kind]||kind)+" "+(i+1)+"/"+list.length+(n.name?" · "+n.name:""),true);}
  // Einsame Inseln (unverbundene Knoten aus der Graph-Analyse) — hervorheben + der Reihe nach anspringen.
  let ISLE=new Set();
  function highlightIslands(on){if(world)world.querySelectorAll(".gnode2").forEach(el=>el.classList.toggle("typehi",on&&ISLE.has(el.dataset.id)));}
  function jumpIslands(){const list=graphNodes().filter(n=>ISLE.has(n.id));if(!list.length){deFlash("✓ keine einsamen Inseln",true);return;}
    const k="§insel";const i=(((JUMPIX[k]??-1)+1))%list.length;JUMPIX[k]=i;const n=list[i];
    centerOn(n);pulseNode(n);deFlash("⚠ Insel "+(i+1)+"/"+list.length+" · "+(NODELABEL[n.kind]||n.kind)+(n.name?" "+n.name:""),true);}

  const dec=id=>MODEL.decider.find(d=>d._id===id);
  const app=id=>MODEL.applier.find(a=>a._id===id);
  // ZUGEHÖRIGKEIT = BEZIEHUNG, nie Namespace oder Name — abgeleitet auf dem Server (GraphExtractor/Sicht: Block, Aggregat je Karte).
  const recByName=n=>MODEL.records.find(r=>r.name===n);
  const baseTyp=x=>(x||"").replace(/\s/g,"").replace(/\?$/,"");
  // Positionsweise vollständige Argumentliste (fehlende Parameter → "default", damit es kompiliert).
  function argListe(cmdName,args){const cmd=recByName(cmdName);
    if(!cmd)return (args||[]).filter(Boolean);
    return (cmd.felder||[]).map((_,i)=>((args||[])[i])||"default");}
  // Transitionen eines Prozesses (explizit angesteckt via t.prozess).
  const transOf=s=>MODEL.transitions.filter(t=>t.prozess===s.name);
  // Die Regeln eines Prozesses in CODE-Reihenfolge (je Join × Dann eine Regel = eine Anweisung im Definiere-Lambda) — wie prepareSaga.
  function regelFolge(s,trans){return (trans||MODEL.transitions).filter(t=>t.prozess===s.name&&(t.wenn||[]).length)
    .flatMap(t=>(t.dann||[]).filter(d=>d.sende||d.rufe).map(d=>({t,d})));}
  // Das Regel-Verb eines Lambda-Teils (wie SlotInventar es als Disc führt): Sende/SendeJe/Rufe bzw. RückgängigDurch(Je).
  const regelVerb=(d,teil)=>teil==="komp"?(d.kompensationJe?"RückgängigDurchJe":"RückgängigDurch"):d.rufe?"Rufe":d.sendeJe?"SendeJe":"Sende";
  // Collection-Felder der Join-Events (für UndAlle-Anzahl / SendeJe-Collection), als "role.field".
  function collFelder(t){const out=[];(t.wenn||[]).forEach((e,i)=>{const r=recByName(e);if(!r)return;const role=["t","r","g"][i]||("e"+(i+1));
    (r.felder||[]).forEach(f=>{if(elementVon(f))out.push(role+"."+f.name);});});return out;}
  // Sammlung? — der Element-Typ kommt vom Extractor (Symbol: IEnumerable<T>). Nur für NIE übersetzte Entwürfe liest der
  //   Editor den eingetippten Typ-Text (List<X>/IReadOnlyList<X>/IEnumerable<X>/X[]) — dort gibt es noch keinen Code.
  const ENTWURF_SAMMLUNG=/^(?:List|IReadOnlyList|IEnumerable)<(.+)>\??$|^(.+)\[\]\??$/;
  function elementVon(f){if(!f)return "";if(f.elementTyp)return f.elementTyp;const m=ENTWURF_SAMMLUNG.exec((f.typ||"").trim());return m?(m[1]||m[2]).trim():"";}
  // Typ-Text → Element-Typ (aus den extrahierten Feldern), je Render einmal aufgebaut.
  let ELEM=null;
  function elementVonTyp(ty){if(!ELEM){ELEM=new Map();alleFelder().forEach(({f})=>{if(f&&f.elementTyp&&!ELEM.has(f.typ))ELEM.set(f.typ,f.elementTyp);});}
    return ELEM.get(ty)||elementVon({typ:ty});}
  // Feldtyp-Klassifikation für Feld-Ports (verdrahtbare Objekt-Felder).
  const istColl=ty=>!!elementVonTyp(ty);
  const istGanzzahl=ty=>/^(int|long)\??$/.test((ty||"").trim());
  // ── Typ-Komposition: den INNEREN Typ eines Feldes (List<X>/X[]/X? → X) für die VO/Enum→Feld-Verdrahtung. ──
  const innerTyp=ty=>{const e=elementVonTyp(ty);return (e||(ty||"")).trim().replace(/\?$/,"").trim();};
  // Alle verdrahtbaren Felder samt Owner-Schlüssel (identisch zum feldPort-Owner: Record/Aggregat-/ReadModel-Name bzw. State-_id).
  function alleFelder(){const out=[];
    MODEL.records.forEach(r=>(r.felder||[]).forEach(f=>out.push({owner:r.name,f})));
    MODEL.aggregate.forEach(a=>(a.state||[]).forEach(f=>out.push({owner:a.name,f})));
    MODEL.states.forEach(s=>{if(!s.aggregat)(s.felder||[]).forEach(f=>out.push({owner:s._id,f}));});
    MODEL.readModels.forEach(rm=>(rm.felder||[]).forEach(f=>out.push({owner:rm.name,f})));
    return out;}
  // Feldtyp per Verdrahtung setzen — vorhandenen Collection-/Array-/Nullable-Wrapper bewahren.
  function setFeldTyp(f,name){const cur=(f.typ||"").trim();const m=/^(List|IReadOnlyList|IEnumerable)<.*?>(\??)$/.exec(cur);
    if(m){f.typ=m[1]+"<"+name+">"+m[2];return;}if(/\[\]\??$/.test(cur)){f.typ=name+"[]"+(cur.endsWith("?")?"?":"");return;}
    f.typ=name+(cur.endsWith("?")?"?":"");}
  // VO/Enum umbenannt → in allen Feldtypen nachziehen (Wort-Grenze, Wrapper bleibt).
  function retypeFelder(old,nv){alleFelder().forEach(({f})=>{if(innerTyp(f.typ)===old)f.typ=(f.typ||"").replace(new RegExp("\\b"+old+"\\b"),nv);});}
  // Ein Feld-Typ-Eingang (◀): eine VO/Enum-Quelle andocken → setzt den Feldtyp. Klein, links.
  function typeInPort(owner,f){if(!f._id)f._id="f"+(NID++);const s=port("ftype");s.classList.add("i","sm");s.title="Typ verdrahten (VO/Enum an dieses Feld)";
    reg("ftype:in:"+owner+":"+f._id,s,{type:"ftype",dir:"in",fobj:f});return s;}
  // ── ABLAUF ALS KETTE (Node-RED-Bild): jede Prozess-Regel wird KANTEN statt einer Karte — Quelle → Ziel. Quelle = die Funktion,
  //   die das auslösende Ergebnis liefert; der Prozess selbst, wenn es sein Start ist; sonst das Event. Ziel = ƒ oder Command.
  //   Verzweigung: je Ergebnis eine eigene Kante („bei X“). Zusammenführung: mehrere Drähte in einen Knoten („und“; UndAlle „alle“).
  //   Fan-out „×N“, Kompensation gestrichelt „↩ rückgängig“ (vom Schritt zum Gegen-Command), Zeitlimit „⏳“. Das Start-Event als
  //   zusätzliches Join-Event ist nur Kontext (seine Daten), kein Draht. Nur eine noch leere Regel (ohne Wenn/Ziel) bleibt eine Karte.
  //   Die Kanten rechnet der Server (board.sicht.kette); t/sg/d sind die Board-Objekte der Regel.
  function ketteAusSicht(){return (SICHT().kette||[]).map(k=>{const t=MODEL.transitions.find(x=>"tr:"+x._id===k.tr),d=t&&(t.dann||[])[k.dannIdx];
    if(!d)return null;return {t,sg:MODEL.sagas.find(x=>x.name===t.prozess),von:k.von,nach:k.nach,bei:k.bei,d,label:k.label,art:k.art};}).filter(Boolean);}
  // ── ARGUMENTE AUTOMATISCH ZUORDNEN (wie msg in Node-RED, nur typisiert): je Feld des Ziels (Command bzw. Auftrag) die passende
  //   Quelle aus den Join-Events (Lambda-Parameter t, r, g …): gleicher Name → Name endet gleich (QuellPfad ← Pfad) → einziger
  //   gleicher Typ. Ein Wert-Objekt wird aus seinen Feldern gebaut. Das jüngste Ergebnis geht vor dem Start. Unpassendes bleibt
  //   "default" — im Panel als Tabelle „Feld ← Quelle“ änderbar. Ergebnis ist sichtbarer Code, keine Laufzeit-Magie.
  function quellFelder(wenn){const out=[];(wenn||[]).forEach((e,i)=>{const r=recByName(e),p=["t","r","g"][i]||("e"+(i+1));
    ((r&&r.felder)||[]).forEach(f=>out.push({ausdruck:p+"."+f.name,name:f.name,typ:baseTyp(f.typ),i}));});return out.sort((a,b)=>b.i-a.i);}
  function ordneZu(feld,quellen,tiefe){const typ=baseTyp(feld.typ),nm=(feld.name||"").toLowerCase(),gl=quellen.filter(q=>q.typ===typ);
    const genau=gl.find(q=>q.name.toLowerCase()===nm);if(genau)return genau.ausdruck;
    const endet=gl.filter(q=>{const qn=q.name.toLowerCase();return qn.length>2&&(nm.endsWith(qn)||qn.endsWith(nm));});if(endet.length)return endet[0].ausdruck;
    if(gl.length===1)return gl[0].ausdruck;
    const vo=recByName(typ);if(vo&&vo.kind==="valueobject"&&tiefe<2&&(vo.felder||[]).length){const a=vo.felder.map(f=>ordneZu(f,quellen,tiefe+1));
      if(a.some(x=>x!=="default"))return "new "+typ+"("+a.join(", ")+")";}
    return "default";}
  const auftragVon=fn=>(MODEL.funktionen.find(f=>f.name===fn)||{}).auftrag;
  const zielRecord=d=>d.rufe?auftragVon(d.rufe):d.sende;
  function autoArgs(zielName,wenn){const z=recByName(zielName);if(!z)return [];const q=quellFelder(wenn);return (z.felder||[]).map(f=>ordneZu(f,q,0));}
  // Nächsten Schritt anhängen — die Argumente werden gleich zugeordnet. Der Start kommt als Kontext in den Join, wenn das mehr
  //   Felder füllt (z. B. die Paar-Id für einen Command am Ende der Kette).
  function kettenAnhaengen(sg,bei,ziel){const zr=ziel.rufe?auftragVon(ziel.rufe):ziel.sende,voll=xs=>xs.filter(x=>x!=="default").length;
    let wenn=[bei],args=autoArgs(zr,wenn);
    if(sg.triggerEvent&&bei!==sg.triggerEvent){const mit=[sg.triggerEvent,bei],a2=autoArgs(zr,mit);if(voll(a2)>voll(args)){wenn=mit;args=a2;}}
    MODEL.transitions.push({_id:"t"+(NID++),prozess:sg.name,wenn,dann:[{...(ziel.rufe?{rufe:ziel.rufe}:{sende:ziel.sende}),sendeArgs:args}]});}
  // Einen Schritt der Kette wieder lösen: die Regel(n) des Prozesses, die bei „bei" genau dieses Ziel rufen/senden.
  function kettenLoesen(sgName,bei,ziel){KETTE.filter(k=>k.t.prozess===sgName&&k.art==="dann"&&k.bei===bei&&(k.d.rufe||k.d.sende)===ziel).forEach(k=>{const t=k.t;
    if((t.dann||[]).length>1)t.dann=t.dann.filter(x=>x!==k.d);else MODEL.transitions=MODEL.transitions.filter(x=>x!==t);});}
  // ARGUMENT-TABELLE eines Schritts: Feld ← Quelle (Vorschläge aus den Join-Events), „↻ automatisch“ füllt neu.
  //   Ein aus dem Code gelesener Ausdruck (λ) bleibt, bis man ihn bewusst durch die Tabelle ersetzt.
  function argTabelle(body,t,d){const zr=zielRecord(d),z=recByName(zr);if(!z||!(z.felder||[]).length)return;
    const sg=MODEL.sagas.find(x=>x.name===t.prozess);
    const ps=(t.wenn||[]).map((e,i)=>{const r=recByName(e);return {p:["t","r","g"][i]||("e"+(i+1)),typ:e,felder:(r&&r.felder)||[]};});
    if(t.sammelEvent)ps.push({p:["t","r","g"][ps.length]||("e"+(ps.length+1)),typ:"IReadOnlyList<"+t.sammelEvent+">",felder:[]});
    zoKurz(body,()=>({titel:"Prozess "+(sg?sg.name:t.prozess||"?")+" · WENN "+(t.wenn||[]).join(" ∧ ")+" → "+(d.rufe?"ƒ "+d.rufe:d.sende),ziel:z.name,
      params:ps,args:d.sendeArgs,ausdruck:d.sendeAusdruck,
      block:null,
      schreibe:a=>{d.sendeArgs=a;delete d.sendeAusdruck;},auto:()=>autoArgs(zr,t.wenn)}));}
  // Die Schritte, die von einem Knoten (Prozess-Start oder Funktion) aus „dann“ folgen — mit ✕ zum Lösen.
  function kettenZeilen(body,vonId){KETTE.filter(k=>k.art==="dann"&&k.von===vonId).forEach(k=>{
    const d=k.d,ziel=d.rufe?"ƒ "+d.rufe:"▶ "+d.sende;
    body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",title:"Schritt lösen",onclick:()=>{MODEL.transitions=MODEL.transitions.filter(x=>x!==k.t);render();}},"✕"),
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"},k.label+" ▶ "+ziel),
      h("button",{class:"rm",title:"Schritt öffnen (Argumente, Zeitlimit)",onclick:()=>waehle("tr:"+k.t._id)},"⚙")));});}
  // Vor Serveraufruf: Transitionen → Saga.Schritte (inkl. UndAlle/SendeJe) + ExtraUsings; Argumente positionsvoll.
  function prepareSaga(){MODEL.sagas.forEach(s=>{const ts=transOf(s);
    // Ein Join, mehrere Dann → je Dann EINE Regel (gleiche Bedingung). flatMap fächert die Dann auf.
    s.schritte=ts.filter(t=>(t.wenn||[]).length).flatMap(t=>(t.dann||[]).filter(d=>d.sende||d.rufe).map(d=>{
      // Ein Aufruf je Dann: ein Command an ein Aggregat (Sende) ODER eine Katalog-Funktion (Rufe) — beide mit optionalem Zeitlimit.
      const st=d.rufe?{wenn:(t.wenn||[]).slice(),rufe:d.rufe}:{wenn:(t.wenn||[]).slice(),sende:d.sende};
      if(d.zeitlimit)st.zeitlimit=d.zeitlimit;
      // Aus dem Code gelesene Ausdrücke (verbatim) reisen unverändert zurück — Vorrang vor Stub-Argumenten.
      if(t.sammelEvent){st.sammelEvent=t.sammelEvent;if(t.sammelAusdruck)st.sammelAusdruck=t.sammelAusdruck;if(t.sammelAnzahl)st.sammelAnzahl=t.sammelAnzahl;}
      if(d.sendeAusdruck)st.sendeAusdruck=d.sendeAusdruck;
      if(d.kompensationAusdruck)st.kompensationAusdruck=d.kompensationAusdruck;
      if(d.kompensationJe)st.kompensationJe=true;
      // Count-Anzahl: Feld-Auswahl (D/S); ein 📝-Ausdruck (H-Fallback) hat Vorrang.
      if(d.sendeJe&&!d.rufe){st.sendeJe=true;if(d.sendeJeCollection)st.sendeJeCollection=d.sendeJeCollection;}
      {const sa=argListe(zielRecord(d),d.sendeArgs);if(sa.length)st.sendeArgumente=sa;}   // Command ODER Auftrag einer Funktion
      if(d.kompensation){st.kompensation=d.kompensation;const ka=argListe(d.kompensation,d.kompArgs);if(ka.length)st.kompensationArgumente=ka;}
      return st;}));
    // usings: der Scaffolder leitet sie aus den referenzierten Records ab (inkl. Auslöser-Namespace);
    // s.extraUsings hält nur die aus dem Code gelesenen expliziten — hier NICHT überschreiben.
    s.extraUsings=s.extraUsings||[];});}
  // Read-only-Anker (nicht ziehbar) — nur Ankerpunkt für abgeleitete Kanten.
  function anchorDot(color){return h("div",{class:"slot s-"+color+" ro"});}
  function topAnchor(color,label,key){const s=anchorDot(color);s.classList.add("t");reg(key,s,null);return h("div",{class:"gtopfield"},s,h("span",{class:"slotlbl"},label));}
  // Namens-Eingabe: benennt um UND zieht alle Verbindungen mit (Namen sind der Referenzschlüssel).
  function renameRefs(kind,obj,old,nv){
    flTypUmbenannt(old,nv);   // Pipelines (Fluss): Funktions-, Command- und Fall-Typen ziehen mit
    if(kind==="funktion"){MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{if(d.rufe===old)d.rufe=nv;}));
      MODEL.akteure.forEach(a=>{a.darf=(a.darf||[]).map(x=>x===old?nv:x);});
      MODEL.records.forEach(r=>{if(r.kind==="auftrag"&&r.funktion===old)r.funktion=nv;});}
    // Ein noch nicht geschriebener Dienst: die Konstruktoren, die ihn nehmen, ziehen mit (aus dem Code: Roslyn-Renamer).
    if(kind==="dienst"){const um=p=>{if(String(p.typ||"").replace(/\?$/,"").trim()===old)p.typ=nv;};
      MODEL.funktionen.forEach(f=>(f.konstruktor||[]).forEach(um));MODEL.dienste.forEach(q=>(q.implementierungen||[]).forEach(im=>(im.konstruktor||[]).forEach(um)));}
    if(kind==="akteur"){
      // Ein Vertrag ohne eigenen Namen heißt I+Akteur — Clients, die ihn tragen, ziehen mit.
      if(!obj.vertragName)MODEL.clients.forEach(c=>{c.traegt=(c.traegt||[]).map(t=>t==="I"+old?"I"+nv:t);});}
    if(kind==="aggregate"){
      MODEL.decider.forEach(d=>{if(d.aggregat===old)d.aggregat=nv;});
      MODEL.applier.forEach(a=>{if(a.aggregat===old)a.aggregat=nv;});
      MODEL.states.forEach(s=>{if(s.aggregat===old)s.aggregat=nv;});
    }else if(kind==="record"){
      // Katalog-Funktionen: ihr Auftrag und ihre Ergebnisse sind Record-Namen.
      MODEL.funktionen.forEach(f=>{if(f.auftrag===old)f.auftrag=nv;f.ergebnisse=(f.ergebnisse||[]).map(x=>x===old?nv:x);});
      // Akteure dürfen den Typ unter seinem neuen Namen (IDarf<T> folgt dem Typ).
      MODEL.akteure.forEach(a=>{a.darf=(a.darf||[]).map(x=>x===old?nv:x);
        (a.vertrag||[]).forEach(r=>{if(r.eingang===old)r.eingang=nv;r.ausgaenge=(r.ausgaenge||[]).map(x=>x===old?nv:x);});});
      // … und der Rand der Clients (ISendet/IFragt/Kenntnis folgen dem Typ).
      MODEL.clients.forEach(c=>["sendet","fragt","kenntnis"].forEach(f=>{c[f]=(c[f]||[]).map(x=>x===old?nv:x);}));
      if(obj.kind==="valueobject")retypeFelder(old,nv);   // Typ-Komposition: Feldtypen mitziehen
      if(obj.kind==="command"){
        MODEL.decider.forEach(d=>{if(d.command===old)d.command=nv;});
        const rx=new RegExp("\\b"+old.replace(/[.*+?^${}()|[\]\\]/g,"\\$&")+"\\b","g");
        MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{if(d.sende===old)d.sende=nv;if(d.kompensation===old)d.kompensation=nv;
          if(d.sendeAusdruck)d.sendeAusdruck=d.sendeAusdruck.replace(rx,nv);if(d.kompensationAusdruck)d.kompensationAusdruck=d.kompensationAusdruck.replace(rx,nv);}));
        // Reaktion-Handles: ausgelöste Commands mitziehen.
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{hd.sends=(hd.sends||[]).map(x=>x===old?nv:x);}));
      }else{
        MODEL.decider.forEach(d=>(d.ergibt||[]).forEach(o=>{if(o.event===old)o.event=nv;}));
        MODEL.applier.forEach(a=>{if(a.event===old)a.event=nv;});
        MODEL.sagas.forEach(s=>{if(s.triggerEvent===old)s.triggerEvent=nv;});
        MODEL.transitions.forEach(t=>{t.wenn=(t.wenn||[]).map(w=>w===old?nv:w);if(t.sammelEvent===old)t.sammelEvent=nv;if(t.sammelAnzahlFeld&&t.sammelAnzahlFeld.rec===old)t.sammelAnzahlFeld.rec=nv;});
        // Reader-Handles: Query (Eingang) und OneOf-Responses (Ausgänge) mitziehen.
        MODEL.reader.forEach(r=>(r.handles||[]).forEach(hd=>{if(hd.query===old)hd.query=nv;hd.responses=(hd.responses||[]).map(x=>x===old?nv:x);}));
        // Reaktion-Handles: Trigger-Event mitziehen.
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;}));
        // Projektion-Handles: Trigger-Event (Abo) UND veröffentlichte reaktive Events mitziehen.
        MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;hd.publishes=(hd.publishes||[]).map(x=>x===old?nv:x);}));
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{hd.publishes=(hd.publishes||[]).map(x=>x===old?nv:x);}));
      }
    }else if(kind==="projektion"){
      // IReader<TProjection>-Bindung (per Name) mitziehen.
      MODEL.reader.forEach(r=>{if(r.projektion===old)r.projektion=nv;});
    }else if(kind==="enum"){retypeFelder(old,nv);}   // Typ-Komposition: Feldtypen mitziehen
  }
  // Eine Karte aus dem Code (sie hat einen Ort) benennt der Server um — Roslyn-Renamer, alle Verweise auch in Lambdas (Werkbank P7.2);
  //   eine gerade erst angelegte zieht ihre Verweise im Board mit und geht mit der nächsten Geste hinaus.
  function nameInp(o,p,ph,kind){const old=o[p];
    return h("input",{value:old??"",placeholder:ph||"Name",
      onchange:e=>{const nv=e.target.value;if(!nv){e.target.value=old??"";return;}
        if(nv!==old&&p==="name"&&o._ort&&o.ausCode){gUmbenennen(o,nv,old);return;}
        if(nv!==old&&kind)renameRefs(kind,o,old,nv);o[p]=nv;gTitel("umbenennen "+old+" → "+nv);render();}});}
  // Feld-Ausgang: jedes Objekt-Feld ist eine verdrahtbare Wert-Quelle (field ▶). Typ-Check am Eingang.
  // Eindeutiger Default-Feldname innerhalb eines Objekts (keine gleichnamigen Felder → keine doppelten „feld").
  function uniqFeldName(arr,base){const set=new Set((arr||[]).map(f=>f.name));if(!set.has(base))return base;let i=2;while(set.has(base+i))i++;return base+i;}
  // Feld nach stabiler _id auflösen (rename-fest); Basis für Kante/Label/Codegen der Feld-Verdrahtung.
  function feldRef(rec,fid){const r=recByName(rec);return r&&(r.felder||[]).find(x=>x._id===fid);}
  // Feld-Ausgang: KEY per stabiler _id (nicht Name!) → gleichnamige Felder kollidieren nicht mehr.
  function feldPort(owner,field,ftyp,fid){const s=port("field");s.classList.add("o","sm");s.title=(field||"?")+" : "+(ftyp||"?")+" — als Wert verdrahten";
    reg("field:out:"+owner+":"+(fid||field),s,{type:"field",dir:"out",rec:owner,field:field,fid:fid,ftyp:ftyp});return s;}
  function feldRow(f,onDel,mitAusdruck,owner){const row=h("div",{class:"frow"},
      h("input",{value:f.name??"",oninput:e=>f.name=e.target.value,onchange:()=>render(),placeholder:"Feld"}),
      tinp(f.typ,v=>f.typ=v));
    if(mitAusdruck)row.append(h("input",{value:f.ausdruck??"",oninput:e=>f.ausdruck=e.target.value||undefined,placeholder:"=Ausdruck",style:"flex:1;width:auto"}));
    row.append(h("button",{class:"rm",onclick:onDel},"✕"));
    if(owner&&f.name)row.append(feldPort(owner,f.name,f.typ,f._id));
    if(owner)row.prepend(typeInPort(owner,f));return row;}
  // Typ-Auswahl per Dropdown (Skalare + Value Objects + Enums) — im State-Knoten.
  function typSelect(val,on){const s=h("select",{style:"flex:1;width:auto",onchange:e=>on(e.target.value)});
    const opts=[...SCALARS(),...MODEL.records.filter(r=>r.kind==="valueobject").map(r=>r.name),...MODEL.enums.map(e=>e.name)];
    if(val&&!opts.includes(val))opts.unshift(val);
    opts.forEach(t=>{const o=h("option",{value:t},t);if(t===val)o.selected=true;s.append(o);});return s;}
  function stateFeldRow(f,onDel,owner){const row=h("div",{class:"frow"},
    h("input",{value:f.name??"",oninput:e=>f.name=e.target.value,placeholder:"Feld"}),
    typSelect(f.typ,v=>f.typ=v),
    h("button",{class:"rm",onclick:onDel},"✕"));
    if(owner&&f.name)row.append(feldPort(owner,f.name,f.typ,f._id));
    if(owner)row.prepend(typeInPort(owner,f));return row;}

  // Port: auf der Fläche unsichtbar (Karten sind immer kompakt) — im Panel startet ein Klick den Verbinden-Modus.
  function port(color){const s=h("div",{class:"slot s-"+color,title:"Klick: passende Knoten leuchten auf dem Graphen — dort anklicken = verbinden / lösen"});
    s.onpointerdown=e=>{e.stopPropagation();e.preventDefault();if(s.closest(".ginsp"))vbStart(s);};return s;}
  // __key: derselbe Schlüssel wie der Port der Karte auf der Fläche (so findet der Verbinden-Modus Kanten + Quelle wieder).
  function reg(key,el,info){el.__slot=info;el.__key=key;if(!INSP)SLOTS[key]=el;return el;}   // Inspector-Kopien verdrahten nicht
  function slotRow(color,label,side,info,key){const s=port(color);s.classList.add(side==="l"?"i":"o");reg(key,s,info);
    return side==="l"?h("div",{class:"slotrow"},s,h("span",{class:"slotlbl"},label))
                     :h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},label),s);}
  function topSlot(color,label,info,key){const s=port(color);s.classList.add("t");reg(key,s,info);
    return h("div",{class:"gtopfield"},s,h("span",{class:"slotlbl"},label));}

  // ══ PIPELINE ALS FLUSS (docs/konzept-editor-pipelines.md §14) ══════════════════════════════════════════════════════════
  //   Eine Pipeline ist eine Fläche aus Knoten (Quelle · ƒ Funktion · ▶ Command · ⧉ Je), verbunden mit Drähten. Jeder Knoten hat
  //   EINE Form: ein Eingang, je Fall ein Ausgangs-Port (dazu ⏳/✕). Jede Geste hier ist genau ein Code-Fakt
  //   (var x = draht.Rufe<F>(λ) …), geschrieben vom Scaffolder, 1:1 zurückgelesen vom Extractor. Die Pipeline steht in einem
  //   eigenen Rahmen (wie ein Client), innen nach FLUSSTIEFE angeordnet (Quelle links, jeder Draht nach rechts).
  const FL_PRE="§fluss:";
  const istFlussDom=d=>typeof d==="string"&&d.startsWith(FL_PRE);
  // 🖥 Host-Rahmen (docs/konzept-werkbank.md §8.1): die Umgebung als Karten rechts neben den Clients — kein Domänen-Modell.
  const HOST_KEY="§host";
  const istHostDom=d=>d===HOST_KEY;
  const istAussenDom=d=>istClientDom(d)||istFlussDom(d)||istHostDom(d);
  const flussVonDom=d=>MODEL.fluesse.find(f=>FL_PRE+f._id===d);
  const FL_SYM={quelle:"⛲",auf:"◆",funktion:"ƒ",command:"▶",je:"⧉",warte:"⧗"};
  const FL_ART={quelle:"Quelle",auf:"Event-Quelle",funktion:"Funktion",command:"Command",je:"Je-Rahmen",warte:"Warten (Event im Strom)"};
  const flWarteAuf=k=>(k.warteAuf&&k.warteAuf.length?k.warteAuf:[k.typ]).filter(Boolean);
  // ══ PIPELINE: ein Knoten = eine ZUGEWIESENE Funktion — Eingang und Ausgänge ergeben sich aus ihrem Interface, nichts wird im
  //   Knoten definiert: ƒ = Auftrag → Ergebnisse (IFunktion) · ▶ = Command → Decide-Events (Aggregat) · ⛲ = → Quell-Nachricht ·
  //   ◆/⧗ = ← Event. Drähte verbinden Knoten direkt (beschriftet mit dem Typ des Ausgangs). Die Verbindung zur Domäne ist die
  //   Zuweisung: ƒ ⇢ Funktions-Karte (Katalog), ▶ → Command im Aggregat, ⛲ ⇢ Nachricht; ◆/⧗ ← Event aus dem Log.
  //   Der Rahmen steht IN der Domäne, auf die der Fluss wirkt (die Aggregate seiner Commands, eindeutig — sonst sein Namespace; vom Server).
  //   Was der Server je Knoten ableitet (board.sicht.fluesse, Werkbank P7.4.5): Ports (Fälle + ⏳/✕, je mit der Nachricht des Ports),
  //   Vorgänger, Tiefe, gehörte Events, Zuweisung. Ein Knoten, den es im Code noch nicht gibt (die Geste läuft), hat bis zum nächsten Board keine.
  var FL_SICHT=null,FL_SICHT_VON=null;   // var: vor dem ersten Board abgefragt (TDZ)
  function flSicht(k){const S=SICHT();if(FL_SICHT_VON!==S){FL_SICHT_VON=S;FL_SICHT=new Map();
      Object.values(S.fluesse||{}).forEach(f=>Object.entries(f.knoten||{}).forEach(([id,x])=>FL_SICHT.set(id,x)));}
    return (k&&FL_SICHT.get("fs:"+k._id))||{};}
  const flZuweisung=k=>flSicht(k).zuweisung||null;
  const flHoert=k=>flSicht(k).hoert||[];
  // Das Interface eines Knotens als eine Zeile: Eingang → Ausgänge.
  function flSignatur(k){if(k.art==="funktion"){const fk=MODEL.funktionen.find(x=>x.name===k.typ);return fk?(fk.auftrag||"?")+" → "+((fk.ergebnisse||[]).join(" | ")||"?"):"";}
    if(k.art==="command")return (k.typ||"?")+" → "+(flDecideEvents(k).join(" | ")||"?");
    if(k.art==="quelle")return "→ "+(k.typ||"?");return "";}
  function flussKnotenNodes(){return MODEL.fluesse.flatMap(f=>(f.knoten||[]).map(k=>({id:"fs:"+k._id,name:(FL_SYM[k.art]||"")+" "+k.name,
    kind:"flussknoten",ref:k,own:{id:"fl:"+f._id,name:f.name,kind:"fluss",ref:f}})));}
  const flKnoten=(f,name)=>(f.knoten||[]).find(k=>k.name===name);
  const flFluss=k=>MODEL.fluesse.find(f=>(f.knoten||[]).includes(k));
  const flQuelleArt=k=>k&&(k.art==="quelle"||k.art==="auf");
  // Die Vorgänger (Knoten-Namen), aus denen ein Knoten Drähte bekommt — auch der Je-Rahmen eines Elements/Sammelns.
  const flVor=k=>flSicht(k).vor||[];
  const flDecideEvents=k=>[...new Set(MODEL.decider.filter(d=>d.command===k.typ).flatMap(d=>(d.ergibt||[]).map(o=>o.event)))];
  // Die Ports eines Knotens: die Fälle (Ergebnisse der Funktion, Events aus dem Decide, die Quell-Nachricht und ihr Strom) und ⏳/✕.
  const flPorts=k=>flSicht(k).ports||[];
  const flPortGleich=(d,p)=>(d.port||"fall")===p.port&&(p.port!=="fall"||(d.fall||null)===(p.fall||null));
  // Wer an einem Port hängt (Knoten + Eingang).
  function flAbnehmer(f,k,p){const r=[];(f.knoten||[]).forEach(z=>(z.eingaenge||[]).forEach((e,ei)=>{
    if((e.draehte||[]).some(d=>d.von===k.name&&flPortGleich(d,p)))r.push({z,e,ei});}));return r;}
  // Das Ziel eines Aufrufs: der Auftrag der Funktion bzw. der Command-Record — seine Felder werden zugeordnet.
  function flZiel(k){if(k.art==="funktion"){const f=MODEL.funktionen.find(x=>x.name===k.typ);return f?recByName(f.auftrag):null;}
    return k.art==="command"?recByName(k.typ):null;}
  // Welche Nachricht ein Draht trägt (für die Zuordnung): die seines Quell-Ports.
  function flDrahtTyp(f,d){const v=flKnoten(f,d.von),p=v&&flPorts(v).find(p=>flPortGleich(d,p));return p?p.typ:null;}
  function flFelderVon(typ){if(typ==="ZeitlimitAbgelaufen"||typ==="SchrittAbgelehnt")return [{name:"Grund",typ:"string"}];
    if(typ==="QuellStrom")return [{name:"Id",typ:"Guid"},{name:"Version",typ:"int"}];
    const r=recByName(typ);return r?(r.felder||[]):[];}
  // Lambda-Parameter wie der Scaffolder (Scaffolder.FlussParameter): benannt nach den Knoten, von denen die Drähte kommen.
  function flParameter(f,e){if(e.je&&!e.sammle)return [e.je];let roh;
    if(e.je){const je=flKnoten(f,e.je),q=((je&&(je.eingaenge||[])[0])||{}).draehte||[];roh=[(q[0]||{}).von||"quelle",((e.draehte||[])[0]||{}).von||"liste"];}
    else roh=(e.draehte||[]).map(d=>(d.port||"fall")==="fall"?d.von:d.von+(d.port==="zeitlimit"?"Zeit":d.port==="strom"?"Strom":"Abgelehnt"));
    const n=[];roh.forEach(x=>{let y=x,i=2;while(n.includes(y))y=x+(i++);n.push(y);});return n;}
  // Die Quellen der Zuordnung: je Lambda-Parameter seine Felder (Ausdruck „param.Feld").
  function flQuellen(f,e){const ps=flParameter(f,e);
    if(e.je&&!e.sammle){const je=flKnoten(f,e.je);return [{p:ps[0],typ:(je&&je.typ)||"",felder:flFelderVon(je&&je.typ)}];}
    if(e.je){const je=flKnoten(f,e.je),q=((je&&(je.eingaenge||[])[0])||{}).draehte||[],t=q[0]?flDrahtTyp(f,q[0]):null;
      return [{p:ps[0],typ:t,felder:flFelderVon(t)},{p:ps[1],typ:"IReadOnlyList<"+(((e.draehte||[])[0]||{}).fall||"?")+">",felder:[]}];}
    return (e.draehte||[]).map((d,i)=>{const t=flDrahtTyp(f,d);return {p:ps[i],typ:t,felder:flFelderVon(t)};});}
  function flKandidaten(f,e){return flQuellen(f,e).flatMap(s=>[{ausdruck:s.p,name:s.p,typ:s.typ},...(s.felder||[]).map(x=>({ausdruck:s.p+"."+x.name,name:x.name,typ:baseTyp(x.typ)}))]);}
  // Automatische Zuordnung je Zielfeld: gleicher Name → gleiches Namensende + Typ → einziger gleicher Typ → sonst default.
  function flAutoArgs(f,k,e){const z=flZiel(k);if(!z)return [];const kand=flKandidaten(f,e);
    return (z.felder||[]).map(fe=>flAutoWert(kand,fe,0));}
  //   Ein Wertobjekt ohne passende Quelle wird aus seinen Feldern gebaut (new BildMeta(quelle.Dateiname, …)).
  function flAutoWert(kand,fe,tiefe){const t=baseTyp(fe.typ);
    const gleich=kand.find(x=>x.name===fe.name&&x.ausdruck.includes("."));if(gleich)return gleich.ausdruck;
    const ende=kand.find(x=>x.ausdruck.includes(".")&&x.typ===t&&(x.name.endsWith(fe.name)||fe.name.endsWith(x.name)));if(ende)return ende.ausdruck;
    const typ=kand.filter(x=>x.typ===t);if(typ.length===1)return typ[0].ausdruck;
    const vo=recByName(t);if(vo&&vo.kind==="valueobject"&&tiefe<2&&(vo.felder||[]).length){const subs=vo.felder.map(sf=>flAutoWert(kand,sf,tiefe+1));
      if(subs.some(x=>x!=="default"))return "new "+t+"("+subs.join(", ")+")";}
    return "default";}
  // Deklarations-Reihenfolge = Code-Reihenfolge: ordnet der Schreiber (FlussPipeline.Geordnet) — eine Geste schickt die Knoten, wie sie sind.
  // Liegt „von" (transitiv) hinter „ziel"? Dann ergäbe ein Draht von → ziel einen Kreis.
  function flErreicht(f,ziel,von){const seen=new Set(),q=[von];while(q.length){const x=q.pop();if(x===ziel)return true;if(seen.has(x))continue;seen.add(x);
    const k=flKnoten(f,x);if(k)flVor(k).forEach(v=>q.push(v));}return false;}
  const flTiefen=f=>new Map((f.knoten||[]).map(k=>[k.name,flSicht(k).tiefe||0]));
  // Die Bedingung eines Knotens: über welche Fälle (Weichen) seine Drähte kommen — zwei Drähte schließen sich aus, wenn sie an
  //   derselben Weiche verschiedene Fälle nehmen. Daraus folgt der Vorschlag ∨ (schließen sich aus) bzw. ∧ (laufen parallel).
  function flBedingung(f,name,memo){memo=memo||new Map();if(memo.has(name))return memo.get(name);memo.set(name,{});
    const k=flKnoten(f,name);let c={};
    if(k){const pro=(k.eingaenge||[]).map(e=>{const m={};[...(e.draehte||[]),...(e.je?[{von:e.je}]:[])].forEach(d=>{
        Object.assign(m,flBedingung(f,d.von,memo));if(d.fall||(d.port&&d.port!=="fall"))m[d.von]=d.fall||d.port;});return m;});
      if(pro.length===1)c=pro[0];else if(pro.length>1)Object.keys(pro[0]).forEach(x=>{if(pro.every(m=>m[x]===pro[0][x]))c[x]=pro[0][x];});}
    memo.set(name,c);return c;}
  function flExklusiv(f,a,b){const ca={...flBedingung(f,a.von)},cb={...flBedingung(f,b.von)};
    if(a.fall||(a.port&&a.port!=="fall"))ca[a.von]=a.fall||a.port;if(b.fall||(b.port&&b.port!=="fall"))cb[b.von]=b.fall||b.port;
    return Object.keys(ca).some(x=>x in cb&&ca[x]!==cb[x]);}
  function flName(f,basis){let b=(basis||"knoten").replace(/[^A-Za-zÄÖÜäöüß0-9_]/g,"");b=b.charAt(0).toLowerCase()+b.slice(1);if(!b)b="knoten";
    let n=b,i=2;while(flKnoten(f,n))n=b+(i++);return n;}
  // Neuer Knoten (optional mit Eingang aus einem Port) — die Zuordnung wird sofort automatisch vorgeschlagen.
  function flNeu(f,art,typ,ein){const basis=art==="funktion"?(typ||"schritt").replace(/^I(?=[A-ZÄÖÜ])/,""):art==="je"?"je":art==="command"?(typ||"command"):(typ||art);
    const k={_id:"fs"+(NID++),name:flName(f,basis),art,typ:typ||"",eingaenge:ein?[ein]:[]};if(art==="je")k.liste="";
    (f.knoten=f.knoten||[]).push(k);if(ein&&art!=="je")ein.argumente=flAutoArgs(f,k,ein);return k;}
  const flDraht=(von,p)=>({von,...(p.fall?{fall:p.fall}:{}),...(p.port&&p.port!=="fall"?{port:p.port}:{})});
  // Ein Draht vom Port p des Knotens „von" in einen bestehenden Knoten: ∨ (eigener Eingang), wenn er sich mit den Drähten des
  //   ersten Eingangs ausschließt, sonst ∧ (in den ersten Eingang). Der Modus ist danach im Panel umschaltbar.
  function flVerbinde(f,von,p,ziel){if(von===ziel.name||flErreicht(f,ziel.name,von)){deFlash("Kreis: „"+ziel.name+"“ liegt vor „"+von+"“ — ein Draht läuft nur vorwärts.",true);return false;}
    const d=flDraht(von,p),es=ziel.eingaenge||(ziel.eingaenge=[]),e0=es.find(e=>!e.je);
    if(!e0||(e0.draehte||[]).every(x=>flExklusiv(f,x,d))){const e={draehte:[d]};es.push(e);e.argumente=flAutoArgs(f,ziel,e);}
    else{e0.draehte.push(d);delete e0.ausdruck;e0.argumente=flAutoArgs(f,ziel,e0);}
    return true;}
  function flLoese(f,z,e,d){e.draehte=(e.draehte||[]).filter(x=>x!==d);
    if(!(e.draehte||[]).length&&!e.je)z.eingaenge=z.eingaenge.filter(x=>x!==e);else{delete e.ausdruck;e.argumente=flAutoArgs(f,z,e);}}
  // ∧ ⇄ ∨: einen Draht in einen eigenen Eingang lösen bzw. zum ersten Eingang hinzunehmen.
  function flUmschalten(f,z,e,d){if((e.draehte||[]).length>1){e.draehte=e.draehte.filter(x=>x!==d);delete e.ausdruck;e.argumente=flAutoArgs(f,z,e);
      const n={draehte:[d]};z.eingaenge.push(n);n.argumente=flAutoArgs(f,z,n);}
    else{const e0=z.eingaenge.find(x=>x!==e&&!x.je);if(!e0)return;z.eingaenge=z.eingaenge.filter(x=>x!==e);e0.draehte.push(d);delete e0.ausdruck;e0.argumente=flAutoArgs(f,z,e0);}}
  // Umbenennen: der Name ist die Identität — Drähte, Je-Bezüge und Ausdrücke („name.Feld") ziehen mit.
  function flUmbenennen(f,k,nv){const alt=k.name;nv=(nv||"").replace(/[^A-Za-zÄÖÜäöüß0-9_]/g,"");if(!nv||nv===alt)return;if(flKnoten(f,nv)){deFlash("„"+nv+"“ gibt es in dieser Pipeline schon.",true);return;}
    // Ein Knoten aus dem Code ist eine Variable im Definiere-Lambda — der Server benennt sie samt Verwendungen um (Renamer).
    if(k._ort&&!k.ohneVariable&&f.ausCode){gUmbenennen(k,nv,alt);return;}
    const re=new RegExp("\\b"+alt+"\\b","g");
    (f.knoten||[]).forEach(z=>(z.eingaenge||[]).forEach(e=>{(e.draehte||[]).forEach(d=>{if(d.von===alt)d.von=nv;});if(e.je===alt)e.je=nv;
      if(e.argumente)e.argumente=e.argumente.map(a=>a.replace(re,nv));if(e.ausdruck)e.ausdruck=e.ausdruck.replace(re,nv);}));
    k.name=nv;}
  // Typ-Umbenennungen (Funktion/Record) ziehen in die Flüsse.
  function flTypUmbenannt(alt,nv){MODEL.fluesse.forEach(f=>(f.knoten||[]).forEach(k=>{if(k.typ===alt)k.typ=nv;
    (k.eingaenge||[]).forEach(e=>(e.draehte||[]).forEach(d=>{if(d.fall===alt)d.fall=nv;}));}));}
  // Hat der Knoten einen Bau-Lambda je Eingang? (Funktion ruft mit Auftrag, Command baut den Command; Warten/Quellen nicht.)
  function flMitLambda(k){return k.art==="funktion"||k.art==="command";}
  function flLoeschen(f,k){f.knoten=(f.knoten||[]).filter(x=>x!==k);
    (f.knoten||[]).forEach(z=>{(z.eingaenge||[]).forEach(e=>{e.draehte=(e.draehte||[]).filter(d=>d.von!==k.name);if(e.je===k.name)delete e.je;});
      z.eingaenge=(z.eingaenge||[]).filter(e=>(e.draehte||[]).length||e.je);});}
  // Neue Pipeline: Namespace aus der Domäne (oder der ersten im Board), eine Quelle als Start.
  function neuePipeline(ns){ns=ns||((MODEL.aggregate[0]||{}).namespace)||((MODEL.records[0]||{}).namespace)||"Domain";
    const f={_id:"fl"+(NID++),name:uniq("NeuePipeline"),namespace:ns,bauer:"p",knoten:[]};
    const q={_id:"fs"+(NID++),name:"quelle",art:"quelle",typ:"",eingaenge:[]};f.knoten.push(q);
    MODEL.fluesse.push(f);render();setTimeout(()=>zeigeKnoten("fs:"+q._id),0);}
  // Neue Funktion aus dem Fluss heraus: Auftrag + ein Ergebnis-Event als EIGENE Karten, im Namespace der Pipeline.
  function flNeueFunktion(f){const nm=neueFunktion(f.namespace);deFlash("ƒ "+nm+" angelegt — Auftrag, Ergebnisse und Felder an ihrer Karte (Klick auf „ruft ƒ …“).",true);return nm;}
  function flNeueQuellNachricht(f){return neueQuelle(f.namespace,"QuelleGemeldet");}
  // Ein neuer Quellen-Typ (docs/konzept-werkbank.md §3): nur der Typ — `public sealed record X : DateiGemeldet;`. Basis und Felder des
  //   fest codierten Treibers kommen vom Server (rahmen.treiber); die Felder sind geerbt, also nur zum Lesen und Zuordnen da.
  function neueQuelle(ns,stamm,pos){const tr=((MODEL.rahmen||{}).treiber||{}).Datei||{basis:"DateiGemeldet",felder:[]},n=uniq(stamm);
    MODEL.records.push({name:n,kind:"event",namespace:ns,treiber:"Datei",basen:[tr.basis],typart:"public sealed record",ohneParameterliste:true,
      felder:(tr.felder||[]).map(f=>({...f,_id:"f"+(NID++),geerbt:true})),...(pos||{})});return n;}
  const flKnotenById=id=>{for(const f of MODEL.fluesse)for(const k of f.knoten||[])if(k._id===id)return k;return null;};
  // Port-Schlüssel eines Ausgangs (Fall je Ergebnis, ⏳, ✕, Strom) — derselbe für das Panel und den Kanten-Abgleich (KANTEN).
  const flAusKey=(k,p)=>"fs:aus:"+k._id+":"+(p.port||"fall")+":"+(p.fall||"");
  // Ziel geändert (andere Funktion/anderer Command) → Zuordnung neu vorschlagen; ein aus dem Code gelesener Lambda bleibt.
  function flNeuZuordnen(k){const f=flFluss(k);if(f)(k.eingaenge||[]).forEach(e=>{if(!e.ausdruck)e.argumente=flAutoArgs(f,k,e);});}
  // „＋" an einem Ausgang: einen NEUEN Knoten anlegen, vom Port verdrahtet. Was er ruft/sendet, verbindet man danach an ihm (⊕).
  function flNeuAmPort(f,k,p,art){const neu=flNeu(f,art,"",{draehte:[flDraht(k.name,p)]});
    if(art==="warte"){neu.warteAuf=[];neu.zeitlimit="TimeSpan.FromHours(1)";}
    render();setTimeout(()=>zeigeKnoten("fs:"+neu._id),0);}
  // Die Karte eines Fluss-Knotens (Panel): Pipeline · Knoten · was er ist (Verbindung zu einer EIGENEN Karte: Nachricht, Funktion,
  //   Command — per ⊕, nie per Dropdown, nie im Knoten deklariert) · Eingänge (Drähte, ∧/∨, Zuordnung) · Ausgänge (je Port ⊕ / ＋).
  function flussKnotenCard(body,n){const k=n.ref,f=n.own.ref;
    body.append(h("div",{class:"gsec"},"⛓ Pipeline"));
    body.append(h("div",{class:"slotrow"},h("input",{value:f.name||"",placeholder:"Pipeline-Name",onchange:e=>{const nv=e.target.value.trim();if(nv&&nv!==f.name){f.name=nv;render();}}}),
      h("input",{value:f.namespace||"",placeholder:"Namespace",onchange:e=>{f.namespace=e.target.value.trim();render();}})));
    body.append(h("div",{class:"gsec"},(FL_SYM[k.art]||"")+" "+(FL_ART[k.art]||k.art)));
    body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl"},"Name"),h("input",{value:k.name,title:"Der Name ist die Identität im Fluss (var "+k.name+" = …) — Drähte und Ausdrücke ziehen mit",
      onchange:e=>{flUmbenennen(f,k,e.target.value.trim());render();}})));
    if(flQuelleArt(k)){
      const an=(a,t,ti)=>h("button",{class:"rm",title:ti,style:k.art===a?"background:#2f5d46;color:#c8f0d8;border-color:#2f5d46":"",onclick:()=>{if(k.art!==a){k.art=a;render();}}},t);
      body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl"},"Art"),an("quelle","⛲ Katalog-Quelle","Datei, Timer, Webhook … — jede Nachricht startet einen Vorgang"),
        an("auf","◆ Event aus dem Log","Jedes Vorkommen eines Events im Log startet einen Vorgang")));
      const ep=port("event");ep.classList.add("i");reg("fs:ein:"+k._id,ep,{type:"evtUse",dir:"in",flk:k._id,role:"quelle"});
      body.append(typVerweis(k.typ,{port:ep,titel:k.art==="quelle"?"meldet":"Event",leer:"— ⊕ "+(k.art==="quelle"?"Quell-Nachricht":"Event")+" verbinden",
        removeTitel:"Nachricht lösen (der Typ bleibt)",onRemove:()=>{k.typ="";render();},
        extra:k.art==="quelle"&&!k.typ?[neuKnopf("Neue Quelle (Datei-Treiber) als eigene Karte anlegen und verbinden",()=>{const q=flNeueQuellNachricht(f);
          verbindeInfo({type:"evtUse",dir:"out",rec:q},{type:"evtUse",dir:"in",flk:k._id,role:"quelle"});deFlash("＋ "+q+" als eigene Karte angelegt und verbunden — Felder an seiner Karte",true);})]:[]}));
      // Webhooks der Quelle (konzept-werkbank §12.3 E12) — Konfig je Umgebung (Quellen:<Typ>:Webhook), hier nur angezeigt; bearbeitet im 🖥 Host-Rahmen.
      (k.bindungen||[]).forEach(b=>body.append(h("div",{class:"gsec",style:"opacity:.8",title:"Konfig der Umgebung"+(b.datei?" · "+b.datei:"")},
        (b.modus==="webhook"?"⇥ Webhook POST ":b.modus==="timer"?"⏱ Timer ":"📁 Datei ")+(b.ort||"")+(b.umgebung?" ("+b.umgebung+")":"")+"  →  "+(k.typ||"Nachricht"))));
      body.append(h("div",{class:"gsec",style:"opacity:.6"},k.art==="quelle"?"Jede Nachricht startet einen Vorgang (genau einmal, Kennung). Der Treiber folgt aus dem Basistyp der Quelle, seine Werte aus der Konfig (Quellen:"+(k.typ||"Typ")+").":"Jedes Vorkommen dieses Events im Log startet einen Vorgang."));}
    else if(k.art==="funktion"){const fk=MODEL.funktionen.find(x=>x.name===k.typ);
      // Die Funktion ist eine eigene Karte (Auftrag, Ergebnisse, Fähigkeiten, Rumpf) — hier nur die Verbindung „ruft".
      const rp=port("auftrag");rp.classList.add("o");rp.title=k.typ||"(Funktion)";reg("fs:rufe:"+k._id,rp,{type:"rufe",dir:"out",flk:k._id});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.7"},"ruft"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"+(fk?";cursor:pointer":k.typ?";color:#cf6f68":""),title:fk?"Klick: Funktions-Karte öffnen (Auftrag, Ergebnisse, Fähigkeiten, Rumpf)":"",
          onclick:fk?()=>zeigeLadend("fk:"+fk._id):null},fk?"ƒ "+fk.name:k.typ?"⚠ "+k.typ+" — Funktion fehlt":"— ⊕ Funktion verbinden"),
        k.typ?h("button",{class:"rm",title:"Funktion lösen",onclick:()=>{k.typ="";render();}},"✕")
          :neuKnopf("Neue Funktion anlegen und hier rufen (Auftrag und Ergebnisse an ihrer Karte)",()=>{const nm=flNeueFunktion(f);
            verbindeInfo({type:"rufe",dir:"out",flk:k._id},{type:"rufe",dir:"in",fk:MODEL.funktionen.find(x=>x.name===nm)._id});}),rp));
      if(fk)body.append(h("div",{class:"gsec",style:"opacity:.7",title:"Das Interface der Funktion — bearbeitet an ihrer Karte"},"Interface: "+flSignatur(k)));}
    else if(k.art==="command"){
      const sp=port("command");sp.classList.add("o");reg("fs:send:"+k._id,sp,{type:"sagaCmd",dir:"out",flk:k._id});
      body.append(typVerweis(k.typ,{port:sp,seite:"o",titel:"sendet",leer:"— ⊕ Command verbinden",removeTitel:"Command lösen (der Typ bleibt)",onRemove:()=>{k.typ="";render();},
        extra:k.typ?[]:[neuKnopf("Neuen Command als eigene Karte anlegen und verbinden",()=>neuerTypFuer("command","NeuerCommand",f.namespace,
          r=>verbindeInfo({type:"sagaCmd",dir:"out",flk:k._id},{type:"sagaCmd",dir:"in",rec:r.name})))]}));
      const dc=MODEL.decider.find(d=>d.command===k.typ);if(dc)body.append(h("div",{class:"gsec",style:"opacity:.7;cursor:pointer",title:"Decider öffnen",onclick:()=>waehle("dec:"+dc._id)},"→ Aggregat "+(dc.aggregat||"?")));}
    else if(k.art==="warte"){
      body.append(h("div",{class:"gsec",style:"opacity:.7"},"Wartet im Strom der Quelle auf das ERSTE dieser Events danach — jedes ist ein Ausgang; kommt keines binnen Zeitlimit, feuert ⏳. Ein Event nach dem Zeitlimit zählt nicht."));
      flWarteAuf(k).forEach((t,i)=>body.append(typVerweis(t,{titel:"⧗",removeTitel:"nicht mehr darauf warten",
        onRemove:()=>{k.warteAuf=flWarteAuf(k).filter((_,j)=>j!==i);k.typ=k.warteAuf[0]||"";render();}})));
      const wp=port("open");wp.classList.add("i");reg("fs:ein:"+k._id,wp,{type:"evtUse",dir:"in",flk:k._id,role:"warte"});
      body.append(h("div",{class:"slotrow"},wp,h("span",{class:"slotlbl"},"+ warte auf ⊕ (Event)")));}
    else if(k.art==="je"){body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl"},"Liste"),h("input",{value:k.liste||"",placeholder:"x => x.Bilder",onchange:e=>{k.liste=e.target.value;render();}})));
      body.append(codePort("fle:"+k._id+":je",{k:"flussLambda",fluss:f._id,knoten:k._id,ei:null},null,"λ Liste",!!(f.datei&&k.liste)));
      body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl"},"Element-Typ"),h("input",{value:k.typ||"",placeholder:"string",onchange:e=>{k.typ=e.target.value.trim();render();}})));
      body.append(h("div",{class:"gsec",style:"opacity:.6"},"Die folgenden Knoten laufen einmal je Element, parallel; „Sammle“ wartet auf alle (in Element-Reihenfolge)."));}
    if(k.art==="funktion"||k.art==="command"||k.art==="warte")body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl"},"⏳ Zeitlimit"),
      h("input",{value:k.zeitlimit||"",placeholder:"z. B. TimeSpan.FromMinutes(5)",onchange:e=>{const v=e.target.value.trim();if(v)k.zeitlimit=v;else delete k.zeitlimit;render();}})));
    // ── Eingänge: Drähte von früheren Knoten (⊕ = einen Ausgang anklicken), dazu „je Element"/„sammle" aus einem Je-Rahmen ──
    if(!flQuelleArt(k)){body.append(h("div",{class:"gsec"},"◀ Eingang"+((k.eingaenge||[]).length>1?"  ·  ∨ jeder Weg einzeln":"")));
      if(!(k.eingaenge||[]).length)body.append(h("div",{class:"gsec",style:"opacity:.6"},"Kein Draht — ⊕ hier und einen Ausgang eines früheren Knotens anklicken."));
      (k.eingaenge||[]).forEach((e,ei)=>flEingangZeilen(body,f,k,e,ei));
      body.append(slotRow("open","+ Draht ◀ ⊕ (Ausgang eines früheren Knotens)","l",{type:"draht",dir:"in",flk:k._id},"fs:in:"+k._id));
      if(k.art!=="je"&&(f.knoten||[]).some(x=>x.art==="je"&&x!==k)){
        body.append(slotRow("open","+ je Element von ⊕ (Je-Rahmen)","l",{type:"jeEl",dir:"in",flk:k._id},"fs:jeein:"+k._id));
        body.append(slotRow("open","+ sammle ⊕ (Je-Rahmen)","l",{type:"jeSa",dir:"in",flk:k._id},"fs:jesain:"+k._id));}}
    // ── Ausgänge: je Port ⊕ (in einen bestehenden späteren Knoten, ∧/∨ automatisch) oder ＋ (neuer Knoten), darunter die Abnehmer ──
    body.append(h("div",{class:"gsec"},"Ausgänge ▶"));
    if(k.art==="je"){
      body.append(slotRow("open","je Element ▶ ⊕ (Knoten, die einmal je Element laufen)","r",{type:"jeEl",dir:"out",flk:k._id},"fs:jeel:"+k._id));
      body.append(slotRow("open","sammle ▶ ⊕ (Knoten, die auf alle Elemente warten)","r",{type:"jeSa",dir:"out",flk:k._id},"fs:jesa:"+k._id));
      (f.knoten||[]).forEach(z=>(z.eingaenge||[]).forEach(e=>{if(e.je!==k.name)return;
        body.append(h("div",{class:"slotrow o fl-ab"},h("span",{class:"fl-ziel",onclick:()=>zeigeKnoten("fs:"+z._id)},"→ "+(FL_SYM[z.art]||"")+" "+z.name+(e.sammle?"  (sammle)":"  (je Element)")),
          h("button",{class:"rm",title:"lösen",onclick:()=>{z.eingaenge=z.eingaenge.filter(x=>x!==e);render();}},"✕")));}));}
    else{const ports=flPorts(k);
      if(!ports.length)body.append(h("div",{class:"gsec",style:"opacity:.6"},k.art==="funktion"?(k.typ?"Die Funktion hat noch kein Ergebnis — an ihrer Karte „+ Ergebnis“.":"Erst ⊕ eine Funktion verbinden — ihre Ergebnisse sind die Ausgänge."):"Erst ⊕ verbinden — die Ausgänge folgen daraus."));
      ports.forEach(p=>{const ab=flAbnehmer(f,k,p),op=port("open");op.classList.add("o");reg(flAusKey(k,p),op,{type:"draht",dir:"out",flk:k._id,port:p.port,fall:p.fall||null});
        const neu=[["funktion","＋ƒ","Neuer Funktions-Knoten an diesem Ausgang (was er ruft: an ihm ⊕)"],["command","＋▶","Neuer Command-Knoten an diesem Ausgang (was er sendet: an ihm ⊕)"]];
        if(p.port==="fall")neu.push(["je","＋⧉","Neuer Je-Rahmen: die folgenden Knoten laufen je Element einer Liste"]);
        if(p.port==="strom")neu.push(["warte","＋⧗","Auf ein späteres Event in diesem Strom warten — mit Zeitlimit (Frist als Rennen)"]);
        body.append(h("div",{class:"slotrow o fl-port"+(p.port!=="fall"?" fl-fehler":"")},h("span",{class:"slotlbl",style:"flex:1"},p.label+" ▶"),
          ...neu.map(([a,t,ti])=>neuKnopf(ti,()=>flNeuAmPort(f,k,p,a),t)),op));
        ab.forEach(({z,e})=>body.append(h("div",{class:"slotrow o fl-ab"},h("span",{class:"fl-ziel",onclick:()=>zeigeKnoten("fs:"+z._id)},"→ "+(FL_SYM[z.art]||"")+" "+z.name+((e.draehte||[]).length>1?"  (∧)":"")),
          h("button",{class:"rm",title:"Draht lösen",onclick:()=>{const d=e.draehte.find(x=>x.von===k.name&&flPortGleich(x,p));if(d)flLoese(f,z,e,d);render();}},"✕"))));});}
    body.append(h("button",{class:"add",style:"margin-top:8px",onclick:()=>{flLoeschen(f,k);SEL=null;render();}},"🗑 Knoten löschen"));
    body.append(h("button",{class:"add",onclick:()=>{MODEL.fluesse.splice(MODEL.fluesse.indexOf(f),1);SEL=null;render();}},"🗑 Pipeline löschen"));}
  // Ein Eingang im Panel: seine Drähte (∧/∨ umschaltbar, ✕ lösen) und die Zuordnung „Feld ← Ausdruck".
  function flEingangZeilen(body,f,k,e,ei){
    const kopf=e.je?(e.sammle?"⧉ sammelt „"+e.je+"“":"⧉ je Element von „"+e.je+"“"):((e.draehte||[]).length>1?"∧ wartet auf alle":"");
    if(ei>0)body.append(h("div",{class:"gsec fl-oder"},"∨ oder"));
    if(kopf)body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl fl-modus"},kopf),h("button",{class:"rm",title:"Eingang entfernen",onclick:()=>{k.eingaenge.splice(ei,1);render();}},"✕")));
    (e.draehte||[]).forEach(d=>{const v=flKnoten(f,d.von),txt=d.von+((d.port||"fall")==="fall"?(d.fall?"."+d.fall:""):d.port==="zeitlimit"?".⏳":d.port==="strom"?".Strom":".✕");
      const ex=(e.draehte||[]).length>1||(k.eingaenge||[]).filter(x=>!x.je).length>1;
      body.append(h("div",{class:"slotrow fl-draht"},h("span",{class:"fl-ziel",onclick:()=>v&&zeigeKnoten("fs:"+v._id)},"← "+(v?FL_SYM[v.art]+" ":"? ")+txt),
        !e.je&&ex?h("button",{class:"rm",title:(e.draehte||[]).length>1?"Als eigenen Weg (∨) lösen":"Zum ersten Eingang nehmen (∧)",onclick:()=>{flUmschalten(f,k,e,d);render();}},(e.draehte||[]).length>1?"∨":"∧"):null,
        h("button",{class:"rm",title:"Draht lösen",onclick:()=>{flLoese(f,k,e,d);render();}},"✕")));});
    // Der Lambda dieses Eingangs ist Logik: Ausschnitt aus der Datei, ✎ ersetzt genau diesen Lambda, 🤖 füllt/ändert ihn.
    if(flMitLambda(k))body.append(codePort("fle:"+k._id+":"+ei,{k:"flussLambda",fluss:f._id,knoten:k._id,ei},null,"λ Zuordnung",!!(f.datei&&e.ausdruck)));
    // Zuordnung „Zielfeld ← Eingang": visuell im Zuordnungs-Editor (Knoten + Drähte); im Panel nur die Kurzfassung.
    const z=flZiel(k);if(!z||k.art==="je")return;
    zoKurz(body,()=>({titel:"⛓ "+f.name+" · "+k.name+" (Eingang "+(ei+1)+")",ziel:z.name,
      params:flQuellen(f,e).map(q=>({p:q.p,typ:q.typ,felder:q.felder})),args:e.argumente,ausdruck:e.ausdruck,
      block:null,
      schreibe:a=>{e.argumente=a;delete e.ausdruck;},auto:()=>flAutoArgs(f,k,e)}));}
  // ══ ZUORDNUNGS-EDITOR (visuell, Knoten + Drähte): baut den Eingang eines Aufrufs — links die Eingänge (Lambda-Parameter mit ihren
  //   Feldern, Wert-Objekte aufgeklappt), rechts das Ziel (Auftrag/Command) mit seinen Feldern, dazwischen Bau-Knoten: „new V(…)" für
  //   ein Wert-Objekt (seine Felder sind wieder Eingänge) und „ƒx" für einen freien Ausdruck. Ein Draht = ein Argument. Gespeichert wird
  //   wie bisher die Argument-Liste je Zielfeld (der Scaffolder schreibt daraus p => new Ziel(…)); ein aus dem Code gelesener Lambda
  //   wird beim Öffnen in Knoten zerlegt (Parameter-Namen angeglichen) und erst bei einer Änderung durch die Zuordnung ersetzt.
  let ZO=null;
  // Argumente auf oberster Ebene trennen (Klammern und Zeichenketten zählen, „=>" ist kein Schließen).
  function zoSplit(s){const out=[];let d=0,q=null,cur="";for(let i=0;i<s.length;i++){const c=s[i];
      if(q){cur+=c;if(c==="\\"){cur+=s[++i]||"";continue;}if(c===q)q=null;continue;}
      if(c==='"'||c==="'"){q=c;cur+=c;continue;}
      if("([{".includes(c))d++;else if(")]}".includes(c))d--;
      if(c===","&&d===0){out.push(cur.trim());cur="";continue;}cur+=c;}
    if(cur.trim())out.push(cur.trim());return out;}
  // „new X(a, b)" → {typ, args} — nur, wenn die schließende Klammer das Ende ist.
  function zoKonstruktor(s){const m=/^new\s+([A-Za-z_][\w.]*)\s*\(/.exec(s||"");if(!m)return null;let d=0,q=null,start=m[0].length-1;
    for(let i=start;i<s.length;i++){const c=s[i];if(q){if(c==="\\"){i++;continue;}if(c===q)q=null;continue;}
      if(c==='"'||c==="'"){q=c;continue;}if(c==="(")d++;else if(c===")"){d--;if(d===0)return i===s.length-1?{typ:m[1].split(".").pop(),args:zoSplit(s.slice(start+1,i))}:null;}}
    return null;}
  // Ein gelesener Lambda „(a, b) => new Ziel(…)" → Argument-Liste mit den Parameter-Namen des Editors; sonst null.
  function zoLambda(aus,namen,ziel,anzahl){const m=/^\s*(?:\(([^)]*)\)|([A-Za-z_]\w*))\s*=>\s*([\s\S]*?)\s*;?\s*$/.exec(aus||"");if(!m)return null;
    const ps=(m[1]!=null?m[1].split(","):[m[2]]).map(x=>x.trim().split(/\s+/).pop()).filter(Boolean);
    const k=zoKonstruktor(m[3].trim());if(!k||k.typ!==ziel||k.args.length!==anzahl)return null;
    return k.args.map(a=>{ps.forEach((p,i)=>{if(namen[i]&&p!==namen[i])a=a.replace(new RegExp("\\b"+p+"\\b","g"),"§"+i+"§");});return a.replace(/§(\d+)§/g,(_,i)=>namen[+i]);});}
  // Alle Quell-Ausdrücke mit Typ: der Parameter selbst, seine Felder und (eine Ebene) die Felder seiner Wert-Objekte.
  function zoQuellTypen(params){const m=new Map();params.forEach(pa=>{m.set(pa.p,pa.typ||"");(pa.felder||[]).forEach(f=>{const t=baseTyp(f.typ),x=pa.p+"."+f.name;m.set(x,t);
      const vo=recByName(t);if(vo&&vo.kind==="valueobject")(vo.felder||[]).forEach(g=>m.set(x+"."+g.name,baseTyp(g.typ)));});});return m;}
  function zoWert(s,qt){s=(s||"").trim();if(!s||s==="default"||s==="default!")return {a:"leer"};
    if(qt.has(s))return {a:"q",x:s};
    const k=zoKonstruktor(s);if(k){const r=recByName(k.typ);if(r&&(r.felder||[]).length===k.args.length)return {a:"neu",typ:r.name,args:k.args.map(x=>zoWert(x,qt))};}
    return {a:"text",x:s};}
  const zoText=w=>w.a==="q"?w.x:w.a==="text"?((w.x||"").trim()||"default"):w.a==="neu"?"new "+w.typ+"("+w.args.map(zoText).join(", ")+")":"default";
  function zoHol(p){let w=ZO.baum[p[0]];for(let i=1;i<p.length;i++)w=w&&w.args?w.args[p[i]]:null;return w;}
  function zoSetze(p,w){if(p.length===1)ZO.baum[p[0]]=w;else{const e=zoHol(p.slice(0,-1));if(e&&e.args)e.args[p[p.length-1]]=w;}zoSchreib();zoRender();}
  function zoSchreib(){ZO.ctx.schreibe(ZO.baum.map(zoText));ZO.roh=null;autosave();}
  const zoPasst=(a,b)=>!a||!b||baseTyp(a)===baseTyp(b);
  function zoOeffne(ctx){const z=recByName(ctx.ziel);if(!z){deFlash("Kein Ziel-Typ — erst ⊕ eine Funktion bzw. einen Command verbinden.",false);return;}
    const n=(z.felder||[]).length,qt=zoQuellTypen(ctx.params);ctx.qt=qt;let args=null,roh=null;
    if(ctx.ausdruck){args=zoLambda(ctx.ausdruck,ctx.params.map(p=>p.p),z.name,n);if(!args)roh=ctx.ausdruck;}
    if(!args)args=ctx.args&&ctx.args.length===n?ctx.args:ctx.auto();
    ZO={ctx,z,baum:(z.felder||[]).map((_,i)=>zoWert(args[i],qt)),roh,arm:null};
    document.addEventListener("keydown",zoTaste,true);zoRender();}
  function zoTaste(e){if(e.key!=="Escape")return;e.stopPropagation();e.preventDefault();if(ZO&&ZO.arm){ZO.arm=null;zoRender();}else zoZu();}
  function zoZu(){ZO=null;document.removeEventListener("keydown",zoTaste,true);const o=document.getElementById("gzo");if(o)o.remove();render();}
  // Ausgangs-Punkt (Quelle oder Bau-Knoten) und Eingangs-Punkt (Zielfeld bzw. Feld eines Bau-Knotens).
  function zoAus(src){const s=h("span",{class:"zo-p zo-o"+(ZO.arm&&JSON.stringify(ZO.arm)===JSON.stringify(src)?" zo-arm":""),title:"Ziehen (oder anklicken, dann ein Feld anklicken) = verbinden"});
    s.dataset.src=JSON.stringify(src);s.onpointerdown=e=>zoZiehe(e,src,s);return s;}
  function zoEin(pfad,typ){const s=h("span",{class:"zo-p zo-i",title:"Eingang "+(typ||"")+" — hier ablegen"});s.dataset.pfad=JSON.stringify(pfad);s.dataset.t=typ||"";
    s.onclick=()=>{if(ZO.arm)zoVerbinde(ZO.arm,pfad,typ);};return s;}
  function zoVerbinde(src,pfad,typ){
    if(!zoPasst(src.t,typ)){deFlash("Typ passt nicht: "+(src.t||"?")+" → "+(typ||"?"),false);return;}
    ZO.arm=null;
    if(src.x!=null){zoSetze(pfad,{a:"q",x:src.x});return;}
    const k=JSON.stringify(src.n),z=JSON.stringify(pfad);if(z===k||z.startsWith(k.slice(0,-1)+","))return;   // nicht in sich selbst
    const w=zoHol(src.n);ZO.arm=null;if(src.n.length===1)ZO.baum[src.n[0]]={a:"leer"};else{const e=zoHol(src.n.slice(0,-1));e.args[src.n[src.n.length-1]]={a:"leer"};}
    zoSetze(pfad,w);}
  // Ziehen per Pointer-Capture am Anschluss (Bewegung/Loslassen kommen sicher an); ohne Bewegung = Quelle wählen (Klick-Klick).
  function zoZiehe(e,src,el){e.preventDefault();e.stopPropagation();const box=ZO.flaeche,svg=ZO.svg,br=()=>box.getBoundingClientRect();
    const p0=zoPunkt(el),tmp=document.createElementNS("http://www.w3.org/2000/svg","path");tmp.setAttribute("class","zo-tmp");svg.append(tmp);
    box.querySelectorAll(".zo-i").forEach(i=>i.classList.add(zoPasst(src.t,i.dataset.t)?"zo-ok":"zo-nein"));
    let bewegt=false,fertig=false;const sx=e.clientX,sy=e.clientY;try{el.setPointerCapture(e.pointerId);}catch(_){}
    const mv=ev=>{if(Math.abs(ev.clientX-sx)+Math.abs(ev.clientY-sy)>4)bewegt=true;const r=br();
      tmp.setAttribute("d",zoBez(p0.x,p0.y,ev.clientX-r.left+box.scrollLeft,ev.clientY-r.top+box.scrollTop));};
    const ende=ev=>{if(fertig)return;fertig=true;el.removeEventListener("pointermove",mv);el.removeEventListener("pointerup",ende);
      el.removeEventListener("lostpointercapture",ende);tmp.remove();box.querySelectorAll(".zo-i").forEach(i=>i.classList.remove("zo-ok","zo-nein"));
      if(!bewegt){ZO.arm=ZO.arm&&JSON.stringify(ZO.arm)===JSON.stringify(src)?null:src;zoRender();return;}
      const t=document.elementFromPoint(ev.clientX,ev.clientY),ziel=t&&t.closest&&t.closest(".zo-i");
      if(ziel)zoVerbinde(src,JSON.parse(ziel.dataset.pfad),ziel.dataset.t);};
    el.addEventListener("pointermove",mv);el.addEventListener("pointerup",ende);el.addEventListener("lostpointercapture",ende);}
  const zoBez=(x1,y1,x2,y2)=>{const dx=Math.max(40,Math.abs(x2-x1)*0.5);return "M"+x1+","+y1+" C"+(x1+dx)+","+y1+" "+(x2-dx)+","+y2+" "+x2+","+y2;};
  function zoPunkt(el){const r=el.getBoundingClientRect(),b=ZO.flaeche.getBoundingClientRect();
    return {x:r.left+r.width/2-b.left+ZO.flaeche.scrollLeft,y:r.top+r.height/2-b.top+ZO.flaeche.scrollTop};}
  // Eine Feld-Zeile mit Eingang: Name : Typ, woher der Wert kommt, ＋ new V / ƒx / ✕.
  function zoFeld(f,w,pfad){const t=baseTyp(f.typ),vo=recByName(t),row=h("div",{class:"zo-r"+(w.a==="leer"?" zo-leer":"")});
    row.append(zoEin(pfad,t),h("span",{class:"zo-l"},f.name),h("span",{class:"zo-t"},f.typ));
    row.append(h("span",{class:"zo-v"},w.a==="q"?"← "+w.x:w.a==="neu"?"← new "+w.typ:w.a==="text"?"← ƒx":"default"));
    if(vo&&vo.kind==="valueobject"&&w.a!=="neu")row.append(h("button",{class:"zo-b",title:"Wert-Objekt "+vo.name+" aus seinen Feldern bauen (Bau-Knoten)",
      onclick:()=>zoSetze(pfad,{a:"neu",typ:vo.name,args:(vo.felder||[]).map(()=>({a:"leer"}))})},"＋ new "+vo.name));
    if(w.a!=="text")row.append(h("button",{class:"zo-b",title:"Freier C#-Ausdruck als Wert (Bau-Knoten ƒx)",onclick:()=>zoSetze(pfad,{a:"text",x:w.a==="q"?w.x:""})},"ƒx"));
    if(w.a!=="leer")row.append(h("button",{class:"zo-b zo-x",title:"Verbindung lösen (default)",onclick:()=>zoSetze(pfad,{a:"leer"})},"✕"));
    return row;}
  function zoRender(){let o=document.getElementById("gzo");if(!ZO){if(o)o.remove();return;}
    const alt=o&&o.querySelector(".zo-f"),sl=alt?alt.scrollLeft:0,st=alt?alt.scrollTop:0;
    if(!o){o=h("div",{id:"gzo"});["pointerdown","wheel","click","dblclick"].forEach(ev=>o.addEventListener(ev,e=>e.stopPropagation()));document.body.append(o);}
    o.textContent="";const {ctx,z}=ZO;
    const kopfP=ctx.params.map(p=>p.p),kopf=kopfP.length===1?kopfP[0]:"("+kopfP.join(", ")+")";
    o.append(h("div",{class:"zo-k"},h("b",{},"🔀 Zuordnung"),h("span",{class:"zo-sub2"},ctx.titel),h("span",{style:"flex:1"}),
      h("button",{title:"Jedes Zielfeld aus den Eingängen vorschlagen (gleicher Name → Namensende + Typ → einziger gleicher Typ)",
        onclick:()=>{ZO.baum=ctx.auto().map(x=>zoWert(x,ctx.qt));zoSchreib();zoRender();}},"↻ automatisch"),
      h("button",{class:"go",onclick:zoZu},"Fertig (Esc)")));
    if(ctx.block&&ctx.block.text)o.append(h("div",{class:"zo-hin"},"⚠ Der Lambda kommt aus dem 📝-Block „"+ctx.block.name+"“ — er hat beim Schreiben Vorrang. ",
      h("button",{onclick:()=>{ctx.block.loesen();ctx.block=null;zoRender();}},"Block lösen, Zuordnung gilt")));
    if(ZO.roh)o.append(h("div",{class:"zo-hin"},"Der Lambda aus dem Code ist kein einfacher Konstruktor — die erste Änderung ersetzt ihn durch die Zuordnung:",h("pre",{},ZO.roh)));
    if(ZO.arm)o.append(h("div",{class:"zo-hin"},"Quelle gewählt — jetzt ein passendes Feld (grün) anklicken · Esc = abbrechen"));
    const fl=h("div",{class:"zo-f"}),spalten=h("div",{class:"zo-sp"}),svg=document.createElementNS("http://www.w3.org/2000/svg","svg");svg.setAttribute("class","zo-svg");
    ZO.flaeche=fl;ZO.svg=svg;fl.append(svg,spalten);o.append(fl);
    // Links: Eingänge.
    const links=h("div",{class:"zo-col"},h("div",{class:"zo-ct"},"◀ Eingänge (Drähte)"));
    ctx.params.forEach(pa=>{const fehlt=pa.typ&&!(pa.felder||[]).length&&!recByName(baseTyp(pa.typ))&&!/^IReadOnlyList</.test(pa.typ);
      const card=h("div",{class:"zo-n zo-q"},h("div",{class:"zo-nh"},h("span",{style:"flex:1"},pa.p+" : "+(pa.typ||"?")),
        fehlt?h("span",{class:"zo-x",title:"Diesen Typ gibt es (noch) nicht — als Karte anlegen, dann erscheinen seine Felder"},"⚠ Typ fehlt"):null,zoAus({x:pa.p,t:pa.typ||""})));
      (pa.felder||[]).forEach(f=>{const x=pa.p+"."+f.name,t=baseTyp(f.typ);card.append(h("div",{class:"zo-r"},h("span",{class:"zo-l"},f.name),h("span",{class:"zo-t"},f.typ),zoAus({x,t})));
        const vo=recByName(t);if(vo&&vo.kind==="valueobject")(vo.felder||[]).forEach(g=>card.append(h("div",{class:"zo-r zo-tief"},h("span",{class:"zo-l"},"· "+g.name),h("span",{class:"zo-t"},g.typ),zoAus({x:x+"."+g.name,t:baseTyp(g.typ)}))));});
      links.append(card);});
    if(!ctx.params.length)links.append(h("div",{class:"zo-leer2"},"Kein Draht im Eingang — erst einen Draht verbinden."));
    // Mitte: Bau-Knoten (Wert-Objekt, Ausdruck) — rekursiv aus dem Baum.
    const mitte=h("div",{class:"zo-col"},h("div",{class:"zo-ct"},"Bau-Knoten"));
    const baue=(w,pfad)=>{if(w.a==="neu"){const v=recByName(w.typ);
        const card=h("div",{class:"zo-n zo-bau"},h("div",{class:"zo-nh"},h("span",{style:"flex:1"},"new "+w.typ+"(…)"),zoAus({n:pfad,t:w.typ})));
        ((v&&v.felder)||[]).forEach((g,j)=>card.append(zoFeld(g,w.args[j]||{a:"leer"},pfad.concat(j))));mitte.append(card);
        w.args.forEach((a,j)=>baue(a,pfad.concat(j)));}
      else if(w.a==="text"){const dl="zodl"+pfad.join("_");
        mitte.append(h("div",{class:"zo-n zo-bau"},h("div",{class:"zo-nh"},h("span",{style:"flex:1"},"ƒx Ausdruck"),zoAus({n:pfad,t:""})),
          h("input",{value:w.x||"",list:dl,placeholder:"C#, z. B. "+(kopfP[0]||"p")+".Feld ?? \"\"",onchange:e=>{w.x=e.target.value;zoSchreib();zoRender();}}),
          h("datalist",{id:dl},...[...ctx.qt.keys()].map(x=>h("option",{value:x})))));}};
    ZO.baum.forEach((w,i)=>baue(w,[i]));
    if(mitte.childNodes.length===1)mitte.append(h("div",{class:"zo-leer2"},"Keine — an einem Zielfeld „＋ new …“ oder „ƒx“."));
    // Rechts: das Ziel.
    const rechts=h("div",{class:"zo-col"},h("div",{class:"zo-ct"},"Ziel ▶"));
    const zc=h("div",{class:"zo-n zo-z"},h("div",{class:"zo-nh"},h("span",{style:"flex:1;cursor:pointer",title:"Karte öffnen",onclick:()=>{const id="rec:"+z.name;zoZu();zeigeLadend(id);}},"new "+z.name+"(…)")));
    (z.felder||[]).forEach((f,i)=>zc.append(zoFeld(f,ZO.baum[i]||{a:"leer"},[i])));
    if(!(z.felder||[]).length)zc.append(h("div",{class:"zo-leer2"},z.name+" hat keine Felder — an seiner Karte „+ Feld“."));
    rechts.append(zc);spalten.append(links,mitte,rechts);
    o.append(h("div",{class:"zo-fuss"},h("span",{class:"zo-ct"},"Lambda"),h("pre",{},kopf+" => new "+z.name+"("+ZO.baum.map(zoText).join(", ")+")")));
    fl.scrollLeft=sl;fl.scrollTop=st;
    requestAnimationFrame(()=>{if(!ZO||ZO.svg!==svg)return;svg.setAttribute("width",fl.scrollWidth);svg.setAttribute("height",fl.scrollHeight);
      if(ZO.arm)fl.querySelectorAll(".zo-i").forEach(i=>i.classList.add(zoPasst(ZO.arm.t,i.dataset.t)?"zo-ok":"zo-nein"));
      const aus=new Map([...fl.querySelectorAll(".zo-o")].map(e=>[e.dataset.src,e]));
      fl.querySelectorAll(".zo-i").forEach(ein=>{const pfad=JSON.parse(ein.dataset.pfad),w=zoHol(pfad);if(!w||w.a==="leer")return;
        const t=fl.querySelector('.zo-o[data-src="'+CSS.escape(JSON.stringify(w.a==="q"?{x:w.x,t:ctx.qt.get(w.x)||""}:{n:pfad,t:w.a==="neu"?w.typ:""}))+'"]')
          ||[...aus.values()].find(e=>{const s=JSON.parse(e.dataset.src);return w.a==="q"?s.x===w.x:JSON.stringify(s.n)===JSON.stringify(pfad);});
        if(!t)return;const a=zoPunkt(t),b=zoPunkt(ein),p=document.createElementNS("http://www.w3.org/2000/svg","path");
        p.setAttribute("d",zoBez(a.x,a.y,b.x,b.y));p.setAttribute("class","zo-draht"+(w.a==="q"?"":" zo-draht-bau"));
        const ti=document.createElementNS("http://www.w3.org/2000/svg","title");ti.textContent="Klick: lösen";p.append(ti);
        p.onclick=()=>zoSetze(pfad,{a:"leer"});svg.append(p);});});}
  // Kurzfassung im Panel: je Zielfeld seine Quelle — bearbeitet wird im Zuordnungs-Editor.
  function zoKurz(body,mk){const ctx=mk(),z=recByName(ctx.ziel);if(!z)return;
    const n=(z.felder||[]).length,args=ctx.ausdruck?zoLambda(ctx.ausdruck,ctx.params.map(p=>p.p),z.name,n):(ctx.args&&ctx.args.length===n?ctx.args:null);
    const box=h("div",{class:"zo-kurz",title:"Klick: Zuordnung im Knoten-Editor bearbeiten",onclick:()=>zoOeffne(mk())});
    box.append(h("div",{class:"zo-kurz-k"},"🔀 "+z.name+"(…) ← "+ctx.params.map(p=>p.p).join(", ")));
    if(ctx.ausdruck&&!args)box.append(h("pre",{class:"fl-lambda"},ctx.ausdruck));
    else (z.felder||[]).forEach((f,i)=>{const a=(args||[])[i]||"default";box.append(h("div",{class:"zo-kurz-r"+(a==="default"?" zo-leer":"")},f.name+" ← "+a));});
    box.append(h("button",{class:"add",onclick:e=>{e.stopPropagation();zoOeffne(mk());}},"🔀 Zuordnung im Knoten-Editor"));
    body.append(box);}
  function flussKurz(n){const k=n.ref,f=n.own.ref;
    if(flQuelleArt(k))return (k.art==="quelle"?"Quelle · meldet ":"Auf ")+(k.typ||"?")+[...new Set((k.bindungen||[]).map(b=>(b.modus==="webhook"?" · ⇥ ":" · ")+(b.ort||"")))].join("");
    if(k.art==="je")return "je Element · "+(k.liste||"Liste?");
    if(k.art==="warte")return "wartet auf "+(flWarteAuf(k).join(" | ")||"?")+(k.zeitlimit?" · ⏳ "+k.zeitlimit:" · ⏳ fehlt");
    const ein=(k.eingaenge||[]).map(e=>e.je?(e.sammle?"sammle "+e.je:"je "+e.je):(e.draehte||[]).map(d=>d.von).join(" ∧ ")).join(" ∨ ");
    return (k.art==="funktion"?"ƒ ":"▶ ")+(k.typ||"?")+": "+flSignatur(k)+(k.zeitlimit?" · ⏳":"")+(ein?" · ← "+ein:"");}
  // Layout eines Pipeline-Rahmens: Spalte = Flusstiefe, Zeile = Reihenfolge innerhalb der Tiefe (Vorgänger-Zeile bevorzugt).
  //   Im Rahmen stehen nur die Knoten (dazu höchstens der Akteur der Quelle, vor ihr) — Typen und Interfaces leben im Katalog.
  function flussLayout(f,nodes,sz,RP,RK,SPK){const t=flTiefen(f),spalten=new Map(),vor=new Map();
    const schl=n=>n.kind==="flussknoten"?n.ref.name:n.id;
    nodes.forEach(n=>{const fl=n.kind==="flussknoten",c=fl?(t.get(n.ref.name)||0):-1;if(!spalten.has(c))spalten.set(c,[]);spalten.get(c).push(n);vor.set(schl(n),fl?flVor(n.ref):[]);});
    const GAPX=110,GAPY=26,placed=[];let x=RP,maxY=0;const zeile=new Map();
    [...spalten.keys()].sort((a,b)=>a-b).forEach(d=>{const col=spalten.get(d);let w=200;
      // Zeile: wo der erste Vorgänger steht (Kette auf einer Linie), sonst unten anschließend.
      const belegt=new Set();col.forEach(n=>{const v=(vor.get(schl(n))||[]).map(x=>zeile.get(x)).filter(z=>z!=null);let z=v.length?Math.min(...v):0;while(belegt.has(z))z++;belegt.add(z);zeile.set(schl(n),z);w=Math.max(w,sz(n).w);});
      col.forEach(n=>{placed.push({n,rx:x,ry:0,z:zeile.get(schl(n))});});x+=w+GAPX;});
    const ZH=Math.max(60,...nodes.map(n=>sz(n).h))+GAPY;
    placed.forEach(p=>{p.ry=RK+RP+SPK+p.z*ZH;maxY=Math.max(maxY,p.ry+sz(p.n).h);});
    return {placed,leer:[],aktLeer:[],w:Math.max(420,x-GAPX+RP),h:maxY+RP};}
  // Slice einer Pipeline: ihre Knoten + was ihnen zugewiesen ist (Funktion, Command, Nachricht) + die Events, die sie hören.
  // ◎ am Fluss-Rahmen: seine Knoten, ihre Zuweisungen und die Events, auf die sie hören — vom Server.
  const flussSlice=f=>new Set(((SICHT().fluesse||{})["fl:"+f._id]||{}).slice||[]);
  // 🔗 Rahmen einer Pipeline: Kopf = Name · Namespace · Knoten-Zahl · ＋ Quelle-Knoten wählen · ◎ · ⤢.
  function flussRahmen(r){const f=flussVonDom(r.ns),fr=h("div",{class:"grahmen fluss"});fr.dataset.ns=r.ns;
    Object.assign(fr.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});
    fr.style.setProperty("--dc","hsl("+domHue(r.ns)+" 55% 60%)");
    const q=f&&(f.knoten||[]).find(flQuelleArt);
    const k=h("div",{class:"grahmen-k",title:f?"public sealed class "+f.name+" : IPipeline — "+(f.namespace||"")+"\nJede Quell-Nachricht startet einen Vorgang; die Knoten laufen als Dirigent (ein Actor je Vorgang), Funktionen holen sich Ausführer per Pull.":""},
      h("span",{class:"gr-t akt-n",onclick:e=>{e.stopPropagation();if(q)waehle("fs:"+q._id);}},"⛓ "+(f?f.name:"?")),
      h("span",{class:"gr-p"},f?(f.namespace||""):""),h("span",{class:"gr-z"},f?(f.knoten||[]).length+" Knoten":""),
      f?h("button",{title:"Pipeline markieren (Slice) — mit den Nachrichten, über die sie an der Domäne hängt",onclick:e=>{e.stopPropagation();fokussiere("menge",flussSlice(f),"⛓ "+f.name);}},"◎"):null,
      h("button",{title:"Pipeline einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"));
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};fr.append(k);return fr;}
  // Kanten eines Flusses: je Draht Knoten → Knoten, beschriftet mit dem Typ des Ausgangs (∧ = mehrere Drähte in einen Eingang,
  //   ∨ = eigener Eingang, ⏳ amber, ✕ rot gestrichelt). Dazu die ZUWEISUNG je Knoten: ƒ ⇢ Funktions-Karte, ▶ → Command im Aggregat,
  //   ⛲ ⇢ Nachricht; ◆/⧗ ← Event. Klick auf eine Kante öffnet ihr Ziel.
  function flussKanten(ELS,mkE){const domEl=id=>ELS.get(id)||((VERTRETER.get(id)||[]).map(v=>ELS.get(v)).find(Boolean));
    MODEL.fluesse.forEach(f=>(f.knoten||[]).forEach(z=>{const ez=ELS.get("fs:"+z._id);if(!ez)return;
      const zw=flZuweisung(z),ew=zw&&domEl(zw);
      if(ew){const cmd=z.art==="command",col=cmd?"#4a86d6":z.art==="quelle"?"#4fb06a":"#c08a2e",nm=(NODEBY.get(zw)||{}).name||"";
        const l=mkE({el:ez},{el:ew},col,!cmd,"fluss zuweisung");l.dataset.a="fs:"+z._id;l.dataset.b=zw;
        l.titel=z.name+(cmd?" sendet "+nm+" ins Aggregat":z.art==="quelle"?" meldet "+nm:" ruft ƒ "+nm+" ("+flSignatur(z)+")")+"\nKlick: "+nm+" öffnen";l.klick=()=>waehle(zw);}
      flHoert(z).forEach(nm=>{const ek=domEl("rec:"+nm);if(!ek)return;const l=mkE({el:ek},{el:ez},"#4fb06a",false,"fluss");l.festB="l";
        l.titel=nm+" aus dem Log → "+z.name+"\nKlick: "+z.name+" öffnen";l.dataset.a="rec:"+nm;l.dataset.b="fs:"+z._id;l.klick=()=>waehle("fs:"+z._id);});
      const es=z.eingaenge||[],gesehen=new Map();
      // Je Draht EINE Kante (kommt er in mehreren ∨-Eingängen vor, wird er nicht doppelt gezeichnet). KONTEXT = ein Draht im ∧ von
      //   einem Vorgänger eines anderen Drahts desselben Eingangs (er ist ohnehin schon da) — dünn, ohne Beschriftung.
      const istKontext=(e,d)=>!d.je&&(e.draehte||[]).length>1&&(e.draehte||[]).some(d2=>d2!==d&&d2.von!==d.von&&flErreicht(f,d.von,d2.von));
      es.forEach((e,ei)=>{const ds=e.je?[{von:e.je,je:true},...(e.sammle?(e.draehte||[]):[])]:(e.draehte||[]);
        ds.forEach(d=>{const kontext=istKontext(e,d);
          const key=d.von+"|"+(d.port||"fall")+"|"+(d.fall||"")+"|"+(d.je?"je":"");const g=gesehen.get(key);
          if(g){g.ei.add(ei);g.kontext=g.kontext&&kontext;return;}gesehen.set(key,{d,e,ei:new Set([ei]),kontext});});});
      gesehen.forEach(({d,e,ei,kontext})=>{const v=flKnoten(f,d.von),ev=v&&ELS.get("fs:"+v._id);if(!ev)return;
        const port=d.port||"fall",oder=es.length>1&&ei.size<es.length,und=!e.je&&(e.draehte||[]).filter(x=>!istKontext(e,x)).length>1;
        const farbe=kontext?"#6f7a91":d.je?"#7fa6d9":port==="zeitlimit"?"#d7a23c":port==="abgelehnt"?"#cf6f68":port==="strom"?"#8fb3d9":"#c08a2e";
        const l=mkE({el:ev},{el:ez},farbe,kontext||port!=="fall"||!!d.je,"fluss"+(kontext?" kontext":""));l.festA="r";l.festB="l";
        l.label=kontext?"":(d.je?(e.sammle?"⧉ je":"⧉ je Element"):(port==="zeitlimit"?"⏳":port==="abgelehnt"?"✕":port==="strom"?"⇢ Strom":(d.fall||(flQuelleArt(v)?v.typ:"")||"")))
          +(kontext?"":(und&&!e.je?" ∧":"")+(oder?" ∨":"")+(e.sammle&&!d.je?" ⧉ sammle":""));
        l.titel=(kontext?"Kontext: ":"")+d.von+" → "+z.name+(oder?" (∨ eigener Weg)":und?" (∧ wartet auf alle)":"")+"\nKlick: "+z.name+" öffnen";
        l.dataset.a="fs:"+v._id;l.dataset.b="fs:"+z._id;l.klick=()=>waehle("fs:"+z._id);});}));}
  function graphNodes(){
    const basis=[...MODEL.akteure.map(a=>({id:"akt:"+a._id,name:a.name,kind:"akteur",ref:a})),
            ...MODEL.clients.map(c=>({id:"cl:"+c._id,name:clientAnzeige(c),kind:"client",ref:c})),
            ...vertragsZusagen().map(({a,r,i,id})=>({id,name:r.eingang||"?",kind:"auf",ref:r,idx:i,own:{id:"akt:"+a._id,name:a.name,kind:"akteur",ref:a}})),
            ...MODEL.records.map(r=>({id:"rec:"+r.name,name:r.name,kind:r.kind,ref:r})),
            ...MODEL.aggregate.map(a=>({id:"agg:"+a.name,name:a.name,kind:"aggregate",ref:a})),
            ...MODEL.states.map(s=>({id:"st:"+s._id,name:s.aggregat||"frei",kind:"state",ref:s})),
            ...MODEL.decider.map(d=>({id:"dec:"+d._id,name:d.command||"",kind:"decider",ref:d})),
            ...MODEL.applier.map(a=>({id:"app:"+a._id,name:a.event||"",kind:"applier",ref:a})),
            ...MODEL.enums.map(e=>({id:"enum:"+e.name,name:e.name,kind:"enum",ref:e})),
            ...MODEL.sagas.map(s=>({id:"saga:"+s.name,name:s.name,kind:"saga",ref:s})),
            ...MODEL.transitions.map(t=>({id:"tr:"+t._id,name:((t.dann||[]).map(d=>d.rufe||d.sende).filter(Boolean).join(", ")),kind:"transition",ref:t})),
            ...MODEL.funktionen.map(f=>({id:"fk:"+f._id,name:f.name,kind:"funktion",ref:f})),
            ...MODEL.dienste.map(d=>({id:"dn:"+d._id,name:d.name,kind:"dienst",ref:d})),
            ...MODEL.readModels.map(rm=>({id:"rm:"+rm._id,name:rm.name,kind:"readmodel",ref:rm})),
            ...MODEL.stores.map(s=>({id:"sto:"+s._id,name:s.name,kind:"store",ref:s})),
            ...MODEL.projektionen.map(p=>({id:"prj:"+p._id,name:p.name,kind:"projektion",ref:p})),
            ...MODEL.reader.map(r=>({id:"rdr:"+r._id,name:r.name,kind:"reader",ref:r})),
            ...MODEL.reaktionen.map(r=>({id:"rk:"+r._id,name:r.name,kind:"reaktion",ref:r})),
            ...MODEL.codeKarten.map(c=>({id:c._id,name:c.name,kind:"code",ref:c})),
            ...flussKnotenNodes(),...handleKnoten(),...fnKnoten(),...hostKnoten()];
    return [...basis,...rumpfKacheln(basis)];
  }
  // ── AGGREGATSWEISE ANORDNUNG: jedes Aggregat ein gekachelter Block (eigenes Rollen-Mini-Layout),
  //    aggregat-übergreifende Knoten (Sagas/Reaktionen/geteilte Typen) im „Geteilt"-Band.
  const SHARED_KEY="§geteilt";
  // Das AKTEUR-BAND (docs/konzept-akteure.md §8): ein eigener Block je Domäne — ganz links, auf der Eingangsseite, vor den
  //   Aggregat-Blöcken. Kommt DAZU, ersetzt keine Spalte. Domäne aus dem Graphen (alle IDarf-Ziele in einer Domäne → dort).
  const AKTEUR_KEY="§akteure";
  const ROLE_AKTEUR={akteur:0,auf:1};   // Spalte „Zusage" (Vertrag, Akteur-Konzept §3) neben dem Akteur
  // Rollen-Spalten innerhalb eines Aggregat-Blocks (links→rechts = Schreibfluss, dann Leseseite).
  //   Leseseite: Projektion (Hub) → ihre Handles → Store-Fns → Store/ReadModel; Query → Reader-Handle → Response, Reader (Hub).
  //   Nach dem Event der Prozess (Regel → Auftrag → ƒ → Ergebnis),
  //   der in die Command-Spalte zurück sendet — so liest sich ein Ablauf in EINEM Block.
  const ROLE_AGG={command:3,decider:4,aggregate:5,state:5,event:6,rejection:6,applier:7,
    saga:8,transition:9,auftrag:10,funktion:11,ergebnis:12,dienst:12.5,valueobject:13,enum:13,projektion:14,"handle:projektion":15,"handle:reaktion":15,
    fn:16,store:17,readmodel:17,query:18,"handle:reader":19,queryresponse:20,reader:21};
  // Rollen-Spalten im Geteilt-Band.
  const ROLE_SHARED={saga:0,transition:1,auftrag:2,funktion:3,ergebnis:3.5,dienst:4,reaktion:2,"handle:reaktion":3,konfig:7,valueobject:8,enum:8,command:9,event:9,rejection:9,
    fn:10,store:11,readmodel:11,"handle:projektion":12,projektion:13,"handle:reader":14,reader:15,query:14,queryresponse:16,rumpf:17,code:17};
  // Rolle eines Knotens (vom Server): Handles je Besitzer-Art (Projektions- vs. Reader-Handle liegen in verschiedenen Spalten), Ergebnis-Events
  //   einer Funktion hinter ihr (Spalte „ergebnis“), sonst die Art. Funktionen einer Kette: je Tiefe eine eigene Spalte (11.01, 11.02 …).
  const rolle=(n,map)=>{const sr=(SICHT().karten[n.id]||{}).rolle,k=sr||(n.kind==="handle"?"handle:"+n.own.kind:n.kind);
    if(k==="funktion"&&map.funktion!==undefined)return map.funktion+Math.min(TIEFE.get(n.id)||0,90)/100;
    return map[k]!==undefined?map[k]:99;};
  const rollenVon=blk=>blk===SHARED_KEY?ROLE_SHARED:blk===AKTEUR_KEY?ROLE_AKTEUR:ROLE_AGG;

  // Die Rumpf-Plätze eines Knotens (je Rumpf die Kachel-Id oder null) — was der Knoten unter sich an Zeilen reserviert (Rumpf + 🤖-Platz),
  //   auch leer. null = der Knoten hat keinen Rumpf.
  function codeEingaenge(n){const t=rumpfTeile(n);return t?t.map((x,i)=>x.kachel&&x.text&&x.text.trim()?kachelId(n.id,i):null):null;}
  // ══ GRAPH-BASIS: die Kanten auf Karten-Ebene (Karten-Id → Karten-Id) kommen vom Server (board.sicht.kanten — Layout, Inseln, Kette,
  //   Nachbarn im Panel); die Rumpf-Kacheln hängen über ihren Besitzer (own) am Graphen, nicht über Kanten.
  const kanten=()=>SICHT().kanten;
  // Diagnose/Prüf-Skripte: Knoten + Kanten des Boards (dieselbe Wahrheit wie Layout und Insel-Erkennung).
  window.deGraph=()=>({knoten:graphNodes().map(n=>({id:n.id,kind:n.kind,name:n.name})),kanten:kanten()});
  // Inseln = Karten in winzigen Komponenten (≤ 2) OHNE Aggregat — die „einsamen" / unverdrahteten (vom Server).
  const inselMenge=()=>new Set(Object.entries(SICHT().karten).filter(([,k])=>k.insel).map(([id])=>id));
  // Aggregat-Untergruppe (oder null = Brücke: cross-cutting, gehört keinem Aggregat).
  function subGroupOf(n){const g=groupKeyOf(n);return g===SHARED_KEY?null:g;}

  // Block einer Karte (Aggregat, SHARED_KEY = Brücke, AKTEUR_KEY = Akteur-Band) — vom Server (board.sicht).
  function groupKeyOf(n){const k=sk(n);if(k)return k.block;return n.kind==="akteur"||n.kind==="auf"?AKTEUR_KEY:SHARED_KEY;}
  // ── MESS-BASIERTES PACKING: nach dem Rendern die ECHTEN Knotengrößen messen und die Aggregat-
  //    Blöcke ÜBERLAPPUNGSFREI per Shelf-Packing setzen. Nur frische/Seed-Boards (alle x/y leer);
  //    vollständig arrangierte Boards (Handanordnung) bleiben unberührt. Zwei Ebenen:
  //    (1) im Block: Rollen→Spalten, Spaltenbreite = max. gemessene Knotenbreite, Stapeln nach echter Höhe.
  //    (2) Blöcke: Shelf-Packing mit echten Block-Maßen → keine Domäne überlappt eine andere.
  function packLayout(force){
    if(!world)return;
    const all=graphNodes().filter(n=>VIS.has(n.id));   // nur was gezeichnet wird (eingeklappte Details liegen im Besitzer)
    if(!all.length)return;
    const setze=(n,x,y)=>{const p=P(n);p.x=Math.round(x);p.y=Math.round(y);
      const el=world.querySelector('[data-id="'+n.id+'"]');if(el){el.style.left=p.x+"px";el.style.top=p.y+"px";}};
    // Echte Knotengrößen EINMAL aus dem DOM messen (Position-unabhängig) → Karte id→{w,h}.
    const dim=new Map();
    world.querySelectorAll(".gnode2").forEach(el=>{dim.set(el.dataset.id,{w:el.offsetWidth||280,h:el.offsetHeight||120});});
    const sz=n=>dim.get(n.id)||{w:280,h:120};
    const positioned=n=>{const p=P(n);return typeof p.x==="number"&&typeof p.y==="number";};
    // Knoten ohne Position (neu angelegt / neu sichtbar): im Raster hat jeder Knoten seine feste Zeile → neu packen.
    if(all.some(n=>!positioned(n)))force=true;
    // Raster-Signatur (sichtbare Knoten + Zahl ihrer Code-Eingänge): ändert sie sich (Handle/Fn dazu, Art aus-/eingeblendet),
    //   stimmen die reservierten Zeilen nicht mehr → neu packen. Gleiche Signatur = Handanordnung bleibt.
    const LAY=KPOS[VIEW.details?"d":"k"]||(KPOS[VIEW.details?"d":"k"]={});
    // Der Akteur-Rahmen gehört zur Lage (Domäne × Akteur): wandert eine Karte in einen anderen Akteur-Rahmen, neu packen.
    const sig=hash(all.filter(n=>n.kind!=="rumpf").map(n=>n.id+":"+((codeEingaenge(n)||[]).length)+":"+domKey(n)+":"+akteurVon(n)).join("|")+"#"+(VIEW.aus||[]).join(",")+"#"+(VIEW.domNeu||[]).join(",")
      +"#"+JSON.stringify(VIEW.akteurOrdnung||{})
      // Clients stapeln sich nach ihrer Höhe (die Anschlussleiste wächst mit dem Vertrag) → ändert sie sich, neu packen.
      +"#"+all.filter(n=>n.kind==="client").map(n=>n.id+":"+Math.round(sz(n).h/20)).join(",")
      // Pipelines: ihre Verdrahtung bestimmt die Tiefe (Spalte) — ändert sie sich, neu packen.
      +"#"+MODEL.fluesse.map(f=>(f.knoten||[]).map(k=>k.name+"<"+flVor(k).join("+")).join(",")+"@"+FLHEIM.get(FL_PRE+f._id)).join("|"));
    if(LAY.__sig!==sig)force=true;
    // Wie viele Knotenpaare überlappen aktuell deutlich? (früher Abbruch, sobald „viele").
    const overlaps=()=>{const b=all.map(n=>{const s=sz(n),p=P(n);return {x:p.x||0,y:p.y||0,w:s.w,h:s.h};});let c=0;
      for(let i=0;i<b.length;i++)for(let j=i+1;j<b.length;j++){const A=b[i],B=b[j];
        if(Math.min(A.x+A.w,B.x+B.w)-Math.max(A.x,B.x)>16 && Math.min(A.y+A.h,B.y+B.h)-Math.max(A.y,B.y)>16){if(++c>all.length)return c;}}
      return c;};
    // Ein arrangiertes Board (Handanordnung) NICHT neu würfeln — AUSSER es überlappt grob (alter/kaputter
    // Stand aus der Ansichts-Datei → heilen). „▦ Neu anordnen" ruft mit force=true.
    if(!force && all.some(positioned) && overlaps() < Math.max(3, Math.floor(all.length*0.12))) return;

    const COLGAP=48,ROWGAP=26,SHELFGAP=150,COMPGAP=240,KGAP=90,LEER_W=640,LEER_H=110;   // RP/RK: Rahmen-Rand/-Kopf (zeichneRahmen)
    const isCode=n=>n.kind==="rumpf";
    // ── ZEILENRASTER: jede Karte belegt genau eine Rasterzeile (Höhe = höchste sichtbare Karte + Abstand), alles fluchtet.
    //   Ein Knoten mit Rümpfen (Decider, Applier, Projektion, Reader, Reaktion, Store-Fn, Funktion, Client …) belegt 1 + je Rumpf GENAU
    //   2 Zeilen: die Rumpf-Kachel (Ausschnitt aus der Datei, P7.4) und darunter den Platz, auf dem früher der 🤖-Knoten stand — er bleibt
    //   reserviert, damit die Lage der Karten gleich bleibt (🤖 ist jetzt eine Aktion am Rumpf). Ein leerer Rumpf reserviert ebenso.
    //   Command/Event/Ablehnung/Query … stehen in der Zeile ihres Partners. Ausgeblendete Rümpfe reservieren nichts.
    const EINZUG=16, ZGAP=12;
    const codeAn=!istAus("rumpf"), llmAn=codeAn;
    const ZEILE=Math.max(28,...all.filter(n=>n.kind!=="client").map(n=>sz(n).h))+ZGAP;   // die (hohe) Anschlussleiste eines Clients taktet nicht mit
    const layoutBlock=(nodes,roleMap)=>{
      const rest=nodes.filter(n=>!isCode(n)), codes=nodes.filter(isCode);
      const codeById=new Map(codes.map(c=>[c.id,c]));
      // Rumpf-Plätze je Besitzer in Eingangs-Reihenfolge (Handle/Fn); null = leerer Rumpf (Platz bleibt reserviert).
      const genutzt=new Set();
      const plaetze=n=>{if(!codeAn)return [];
        const src=codeEingaenge(n);if(!src)return [];
        return src.map(id=>{const c=id&&codeById.get(id);if(c)genutzt.add(c.id);return c||null;});};
      const PL=new Map();rest.forEach(n=>{const p=plaetze(n);if(p.length)PL.set(n.id,p);});
      const hoehe=n=>1+(PL.get(n.id)||[]).reduce((s,c)=>s+(llmAn?2:1),0);
      const rawOf=n=>rolle(n,roleMap);
      const colRoles=[...new Set(rest.map(rawOf))].sort((a,b)=>a-b);
      const byRole=new Map(colRoles.map(r=>[r,[]]));rest.forEach(n=>byRole.get(rawOf(n)).push(n));
      // Belegung je Spalte (Zeilen-Set) + Zeile je Knoten.
      const belegt=new Map(colRoles.map(r=>[r,new Set()])), zeile=new Map();
      const frei=(role,z,len)=>{const b=belegt.get(role);for(let i=0;i<len;i++)if(b.has(z+i))return false;return true;};
      const setzeZ=(role,n,z,len)=>{const b=belegt.get(role);for(let i=0;i<len;i++)b.add(z+i);zeile.set(n.id,z);};
      const ab=(role,z,len)=>{while(!frei(role,z,len))z++;return z;};
      const imBlock=new Set(rest.map(n=>n.id));
      // Nachbarn = Board-Kanten UND Ablauf-Kette (Prozess → ƒ → ƒ): so liegt eine Kette auf einer Zeile.
      const kettenNb=id=>KETTE.filter(k=>k.von===id||k.nach===id).map(k=>k.von===id?k.nach:k.von);
      const nachbarZ=id=>[...(ADJ.inn.get(id)||[]),...(ADJ.out.get(id)||[]),...kettenNb(id)].filter(x=>imBlock.has(x)&&zeile.has(x)).map(x=>zeile.get(x));
      // Wunschzeile: Event → sein Applier (sonst der erzeugende Decider); sonst die oberste Zeile eines platzierten Nachbarn.
      const wunsch=n=>{const vor=KETTE.filter(k=>k.art==="dann"&&k.nach===n.id&&imBlock.has(k.von)&&zeile.has(k.von)).map(k=>zeile.get(k.von));
        if(vor.length)return Math.min(...vor);   // Kettenschritt: auf der Zeile seines Vorgängers (erst … dann … auf einer Linie)
        if(n.kind==="event"){const ap=(ADJ.out.get(n.id)||[]).filter(x=>x.startsWith("app:")&&zeile.has(x)).map(x=>zeile.get(x));if(ap.length)return Math.min(...ap);}
        const z=nachbarZ(n.id);return z.length?Math.min(...z):null;};
      const zweiHop=n=>{let best=null;[n.id,...(ADJ.inn.get(n.id)||[]),...(ADJ.out.get(n.id)||[])].forEach(x=>nachbarZ(x).forEach(z=>{if(best===null||z<best)best=z;}));return best;};
      // (1) Besitzer-Spalten zuerst, lückenlos gestapelt (Reihenfolge: nahe an bereits platzierten Nachbarn, sonst Modell-Reihenfolge).
      //   Die Store-Fn-Spalte zuletzt: sie richtet sich an ihren Aufrufern aus (Projektions- UND Reader-Handles).
      const fnRolle=roleMap.fn;
      [...colRoles.filter(r=>r!==fnRolle),...colRoles.filter(r=>r===fnRolle)].forEach(role=>{const own=byRole.get(role).filter(n=>PL.has(n.id));if(!own.length)return;
        const key=new Map(own.map((n,i)=>[n.id,[zweiHop(n)??1e9,i]]));
        own.sort((a,b)=>{const A=key.get(a.id),B=key.get(b.id);return A[0]-B[0]||A[1]-B[1];});
        // Store-Fns stehen auf der Höhe ihres ersten Aufrufers (freie Zeile ab dort); alle anderen Besitzer lückenlos.
        let z=0;own.forEach(n=>{const len=hoehe(n),k=key.get(n.id)[0];
          if(role===fnRolle&&k<1e9){setzeZ(role,n,ab(role,k,len),len);return;}
          z=ab(role,z,len);setzeZ(role,n,z,len);z+=len;});});
      // (2) übrige Knoten je Spalte in Rollen-Reihenfolge: in ihre Wunschzeile (erste freie ab dort), sonst ans Ende.
      colRoles.forEach(role=>{const rest2=byRole.get(role).filter(n=>!zeile.has(n.id));
        const w=new Map(rest2.map(n=>[n.id,wunsch(n)]));
        const mit=rest2.filter(n=>w.get(n.id)!==null).sort((a,b)=>w.get(a.id)-w.get(b.id)), ohneW=rest2.filter(n=>w.get(n.id)===null);
        mit.forEach(n=>setzeZ(role,n,ab(role,w.get(n.id),1),1));
        let z=0;ohneW.forEach(n=>{z=ab(role,z,1);setzeZ(role,n,z,1);z++;});});
      // Spalten → x; Rumpf-Kacheln eingerückt unter dem Besitzer.
      let cx=0,maxZ=0;const placed=[];
      colRoles.forEach(role=>{let w=120;
        byRole.get(role).forEach(n=>{const z=zeile.get(n.id),s=sz(n);placed.push({n,rx:cx,ry:z*ZEILE});
          // Besitzer: Breite für die Einrückung immer reservieren (wie zu Zeiten der 🤖-Knoten) — die Lage bleibt.
          w=Math.max(w,s.w+(PL.has(n.id)?(llmAn?2:1)*EINZUG:0));
          let zz=z+1;(PL.get(n.id)||[]).forEach(c=>{
            if(c){placed.push({n:c,rx:cx+EINZUG,ry:zz*ZEILE});w=Math.max(w,sz(c).w+EINZUG);}
            zz+=llmAn?2:1;});
          maxZ=Math.max(maxZ,zz);});
        cx+=w+COLGAP;});
      // Rumpf-Kacheln ohne Besitzer in diesem Block (z. B. die Rümpfe einer Client-Implementierung: der Client hat seinen eigenen Rahmen,
      //   seine Rümpfe stehen in der Domäne seines Namespaces): eigene Spalte, ebenfalls im Raster.
      const orphan=codes.filter(c=>!genutzt.has(c.id));
      if(orphan.length){let w=120,z=0;
        orphan.forEach(c=>{placed.push({n:c,rx:cx,ry:z*ZEILE});w=Math.max(w,sz(c).w);z++;});
        maxZ=Math.max(maxZ,z);cx+=w+COLGAP;}
      // Über jeder Spalte Platz für ihren Spalten-Rahmen-Kopf (SPK, zeichneRahmen).
      return {placed:placed.map(p=>({...p,ry:p.ry+SPK})),w:Math.max(0,cx-COLGAP),h:Math.max(0,maxZ*ZEILE-ZGAP)+SPK};
    };
    // Shelf-Packing über {w,h,…}-Boxen; setzt rx/ry; liefert Gesamtmaße.
    const shelf=(boxes,gap,factor)=>{const area=boxes.reduce((s,b)=>s+b.w*b.h,0),maxW=Math.max(1,...boxes.map(b=>b.w));
      const ROWW=Math.max(maxW,Math.sqrt(area)*(factor||1.2));let cx=0,ry=0,rh=0,tw=0;
      boxes.forEach(b=>{if(cx>0&&cx+b.w>ROWW){ry+=rh+gap;cx=0;rh=0;}b.rx=cx;b.ry=ry;cx+=b.w+gap;rh=Math.max(rh,b.h);tw=Math.max(tw,cx-gap);});
      return {w:tw,h:ry+rh};};
    // Eine KOMPONENTE: Aggregat-Blöcke (ROLE_AGG) + EIN Brücken-Block (ROLE_SHARED, cross-cutting), intern gepackt.
    const layoutComp=(nodes)=>{
      const subs=new Map();nodes.forEach(n=>{const s=blockVon(n);if(!subs.has(s))subs.set(s,[]);subs.get(s).push(n);});
      const aggKeys=[...subs.keys()].filter(k=>k!==SHARED_KEY&&k!==AKTEUR_KEY).sort();
      // Akteur-Band zuerst → beim Shelf-Packing ganz links (Eingangsseite).
      const blocks=(subs.has(AKTEUR_KEY)?[layoutBlock(subs.get(AKTEUR_KEY),ROLE_AKTEUR)]:[]).concat(aggKeys.map(k=>layoutBlock(subs.get(k),ROLE_AGG)));
      if(subs.has(SHARED_KEY))blocks.push(layoutBlock(subs.get(SHARED_KEY),ROLE_SHARED));
      const d=shelf(blocks,SHELFGAP,1.35);
      const placed=[];blocks.forEach(b=>b.placed.forEach(p=>placed.push({n:p.n,rx:b.rx+p.rx,ry:b.ry+p.ry})));
      return {placed,w:d.w,h:d.h};
    };
    // ── HIERARCHISCH NACH DOMÄNEN (Namespace-Baum): je Domäne eine Region = ihre Aggregat-/Brücken-Blöcke wie gehabt
    //   (layoutComp: Rollen-Spalten, Code/🤖 unter dem Besitzer), darunter ihre Inseln, darunter ihre Unterdomänen.
    //   Jede Region reserviert Rand (RP) + Kopfzeile (RK) für den Rahmen (zeichneRahmen) → Rahmen überlappen nie.
    const proDom=new Map();all.forEach(n=>{const d=domKey(n);if(!proDom.has(d))proDom.set(d,[]);proDom.get(d).push(n);});
    const BAUM=domBaum([...proDom.keys(),...(VIEW.domNeu||[])]);
    const region=ns=>{if(istFlussDom(ns))return flussLayout(flussVonDom(ns),proDom.get(ns)||[],sz,RP,RK,SPK);
      const alleK=BAUM.kinder.get(ns)||[],kinder=alleK.filter(k=>!istFlussDom(k)).map(region);
      const eigen=proDom.get(ns)||[];
      const placed=[],leer=[],aktLeer=[];let y=RK+RP,w=0;
      // Pipelines der Domäne stehen UNMITTELBAR ÜBER dem Akteur-Rahmen, in dem ihre Commands liegen (Eingangsseite) — die Drähte
      //   ▶ → Command und Event → nächster Knoten bleiben kurz. Ohne Commands hier: zuoberst.
      const flZiel=fns=>{const f=flussVonDom(fns),a=((f&&f.knoten)||[]).filter(k=>k.art==="command"&&k.typ).map(k=>NODEBY.get("rec:"+k.typ))
        .filter(n=>n&&VIS.has(n.id)&&domKey(n)===ns).map(akteurVon);return a.length?a[0]:null;};
      const flk=alleK.filter(istFlussDom).map(k=>({d:region(k),ziel:flZiel(k)})),flGesetzt=new Set();
      const setzeFl=ziel=>flk.forEach(x=>{if(flGesetzt.has(x)||(ziel!==undefined&&x.ziel!==ziel))return;flGesetzt.add(x);
        x.d.placed.forEach(p=>placed.push({n:p.n,rx:RP+p.rx,ry:y+p.ry}));y+=x.d.h+KGAP;w=Math.max(w,x.d.w);});
      const ord0=akteurOrdnung(ns);
      flk.forEach(x=>{if(x.ziel==null||!ord0.length)setzeFl(x.ziel);});
      const insel=eigen.filter(istInselLage),haupt=eigen.filter(n=>!istInselLage(n));
      // Domäne × Akteur (docs/konzept-akteure.md §8): je Akteur (Reihenfolge der Domäne) ein eigener Rahmen mit dem bewährten Block-Layout darin,
      //   untereinander; „ohne Akteur" zuletzt. Eine Domäne ohne Akteure bleibt, wie sie war (ein Block-Layout).
      const ord=akteurOrdnung(ns);
      if(haupt.length&&!ord.length){const c=layoutComp(haupt);c.placed.forEach(p=>placed.push({n:p.n,rx:RP+p.rx,ry:y+p.ry}));y+=c.h;w=c.w;}
      else if(ord.length){const gr=new Map();haupt.forEach(n=>{const a=akteurVon(n);if(!gr.has(a))gr.set(a,[]);gr.get(a).push(n);});
        let erst=true;
        [...ord,OHNE_AKT].forEach(a=>{const g=gr.get(a);if(!g&&a===OHNE_AKT)return;if(!erst)y+=AGAP;erst=false;setzeFl(a);
          if(!g){aktLeer.push({ns,akt:a,rx:RP,ry:y});y+=AK;w=Math.max(w,AKLEER_W);return;}   // leer: nur Kopf + „↥ auch“
          const c=layoutComp(g);c.placed.forEach(p=>placed.push({n:p.n,rx:RP+AP+p.rx,ry:y+AK+AP+p.ry}));
          y+=AK+2*AP+c.h;w=Math.max(w,c.w+2*AP);});}
      setzeFl();   // übrige (Ziel-Akteur-Rahmen leer)
      if(insel.length){if(haupt.length)y+=SHELFGAP/2;const IW=Math.max(700,w);let ix=0,iy=0,rh=0;
        insel.forEach(n=>{const s=sz(n);if(ix>0&&ix+s.w>IW){iy+=rh+ROWGAP;ix=0;rh=0;}placed.push({n,rx:RP+ix,ry:y+iy});ix+=s.w+COLGAP;rh=Math.max(rh,s.h);w=Math.max(w,ix-COLGAP);});
        y+=iy+rh;}
      if(!eigen.length&&!kinder.length&&!flk.length){leer.push({ns,rx:0,ry:0});w=LEER_W;y+=LEER_H;}
      if(kinder.length){if(eigen.length||aktLeer.length)y+=KGAP;const d=shelf(kinder,KGAP,1.2);
        kinder.forEach(k=>{k.placed.forEach(p=>placed.push({n:p.n,rx:RP+k.rx+p.rx,ry:y+k.ry+p.ry}));k.leer.forEach(l=>leer.push({ns:l.ns,rx:RP+k.rx+l.rx,ry:y+k.ry+l.ry}));
          k.aktLeer.forEach(l=>aktLeer.push({ns:l.ns,akt:l.akt,rx:RP+k.rx+l.rx,ry:y+k.ry+l.ry}));});
        y+=d.h;w=Math.max(w,d.w);}
      return {placed,leer,aktLeer,w:w+2*RP,h:y+RP};};
    const regions=BAUM.oben.filter(ns=>!istAussenDom(ns)).map(region);
    shelf(regions,COMPGAP,1.05);
    // Pipelines (Fluss, §14): je Pipeline ein Rahmen RECHTS neben den Domänen (untereinander), innen nach Flusstiefe.
    {const FLGAP=420;let rechts=0,fy=0;regions.forEach(d=>rechts=Math.max(rechts,d.rx+d.w));
      BAUM.oben.filter(istFlussDom).forEach(ns=>{const f=flussVonDom(ns);if(!f)return;const d=flussLayout(f,proDom.get(ns)||[],sz,RP,RK,SPK);
        d.rx=rechts+(regions.length?FLGAP:0);d.ry=fy;fy+=d.h+KGAP;regions.push(d);});}
    // Clients (docs/konzept-akteure.md §4): je Client ein Rahmen, untereinander RECHTS neben allen Domänen (Außenwelt).
    {const CLGAP=520;let rechts=0,cy=0;regions.forEach(d=>rechts=Math.max(rechts,d.rx+d.w));
      BAUM.oben.filter(istClientDom).forEach(ns=>{const d=region(ns);d.rx=rechts+(regions.length?CLGAP:0);d.ry=cy;
        // Höhe aus der echten Karte (die Leiste ist höher als eine Rasterzeile).
        const hh=Math.max(d.h,...(proDom.get(ns)||[]).map(n=>sz(n).h+RK+2*RP+SPK));cy+=hh+KGAP;regions.push(d);});}
    // 🖥 Host (Werkbank §8.1): ein Rahmen RECHTS neben den Clients, die Umgebungs-Karten in drei Spalten.
    {const hk=all.filter(n=>n.kind==="host");if(hk.length){let rechts=0;regions.forEach(d=>rechts=Math.max(rechts,d.rx+d.w));
      const SP=3,bw=Math.max(...hk.map(n=>sz(n).w)),hs=new Array(SP).fill(RP+RK+SPK),placed=[];
      hk.forEach((n,i)=>{const c=i%SP;placed.push({n,rx:RP+c*(bw+COLGAP),ry:hs[c]});hs[c]+=sz(n).h+ROWGAP;});
      regions.push({placed,leer:[],aktLeer:[],rx:rechts+(regions.length?520:0),ry:0,w:2*RP+SP*bw+(SP-1)*COLGAP,h:Math.max(...hs)+RP});}}
    regions.forEach(d=>{d.placed.forEach(p=>setze(p.n,d.rx+p.rx,d.ry+p.ry));
      d.leer.forEach(l=>{LAY["§leer:"+l.ns]={x:d.rx+l.rx,y:d.ry+l.ry};});
      d.aktLeer.forEach(l=>{LAY["§aktleer:"+l.ns+"|"+l.akt]={x:d.rx+l.rx,y:d.ry+l.ry};});});
    LAY.__sig=sig;if(VIEW.kompakt)speichereKpos();
  }
  // Karten auf der Fläche sind immer kompakt (Kopf + Kurzfassung); das Formular lebt im Panel.
  const istZu=()=>true;   // Karten sind immer kompakt — bearbeitet wird im Panel
  function nodeEditor(n){
    const zu=istZu(n);
    const dg=diagFuer(n.id),nurWarn=dg.length>0&&dg.every(x=>x.art==="warnung");
    const el=h("div",{class:"gnode2 n-"+n.kind+(zu?" collapsed":"")+(ISLE.has(n.id)?" island":"")+(dg.length?(nurWarn?" diagwarn":" diag"):"")
      +(n.kind==="rumpf"&&n.ref.kid&&VOR[n.ref.kid]?" llmvor":"")+(n.kind==="code"&&(n.ref.unaufgeloest||[]).length?" unaufgeloest":"")});el.dataset.id=n.id;
    const pos=P(n);el.style.left=(pos.x||0)+"px";el.style.top=(pos.y||0)+"px";
    const title=(n.kind==="code"&&(n.ref.unaufgeloest||[]).length?"⚠ ":"")+(NODELABEL[n.kind]||n.kind);
    const head=h("div",{class:"ghead"},
      h("span",{class:"gtitle"+(n.name?" hatname":""),title:title+(n.name?" · "+n.name:"")},h("span",{class:"gk"},title+(n.name?" · ":"")),h("span",{class:"gn"},n.name||"")),
      dg.length?h("span",{class:"gdiag"+(nurWarn?" warn":""),title:dg.map(d=>d.text).join("\n")},(nurWarn?"⚠ ":"⛔ ")+dg.length):null,
      dsBadge(n),
      n.kind==="host"||n.kind==="code"||n.kind==="rumpf"?null:h("span",{class:"gx",title:"Löschen",onclick:()=>delNode(n)},"✕"));
    head.onpointerdown=e=>{if(e.target.classList.contains("gx")||e.target.classList.contains("gcol"))return;startMove(e,el,P(n),()=>waehle(n.id));};
    el.append(head);
    el.append(kurzfassung(n));
    const body=h("div",{class:"gbody"});
    fuelleKoerper(body,n);
    el.append(body);dsDropZiel(el,n);return el;
  }
  // Der Formular-Körper eines Knotens — auf der Fläche (aufgeklappt) UND im Inspector derselbe.
  function fuelleKoerper(body,n){
    if(n.kind==="aggregate")aggStateCard(body,n.ref);
    else if(n.kind==="state")stateCard(body,n.ref);
    else if(n.kind==="decider")deciderCard(body,n.ref);
    else if(n.kind==="applier")applierCard(body,n.ref);
    else if(n.kind==="enum")body.append(enumCard(n.ref,MODEL.enums.indexOf(n.ref)));
    else if(n.kind==="saga")prozessHubCard(body,n.ref);
    else if(n.kind==="transition")transitionCard(body,n.ref);
    else if(n.kind==="readmodel")readModelCard(body,n.ref);
    else if(n.kind==="store")storeCard(body,n.ref);
    else if(n.kind==="projektion")projektionCard(body,n.ref);
    else if(n.kind==="reader")readerCard(body,n.ref);
    else if(n.kind==="reaktion")reaktionCard(body,n.ref);
    else if(n.kind==="funktion")funktionCard(body,n.ref);
    else if(n.kind==="dienst")dienstCard(body,n.ref);
    else if(n.kind==="akteur")akteurCard(body,n.ref);
    else if(n.kind==="client")clientCard(body,n.ref);
    else if(n.kind==="auf")aufCard(body,n);
    else if(n.kind==="rumpf")rumpfKachelCard(body,n);
    else if(n.kind==="handle")handleCard(body,n);
    else if(n.kind==="fn")fnCard(body,n);
    else if(n.kind==="flussknoten")flussKnotenCard(body,n);
    else if(n.kind==="host")hostCard(body,n);
    else if(n.kind==="code")codeKarteCard(body,n.ref);
    else recordCard(body,n.ref);
  }
  function delNode(n){const k=n.kind,ref=n.ref;if(k==="host")return;gTitel("löschen "+(NODELABEL[k]||k)+" "+(n.name||""));
    // Ein gelöschter Eingang (Command/Query) verschwindet auch aus den Befugnissen (IDarf) der Akteure.
    {const nm=MODEL.records.includes(ref)?ref.name:null;if(nm)MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(nm))a.darf=ohneX(a.darf,nm);
      // … und aus den Verträgen: als Eingang fällt die ganze Zusage weg, als Ausgang nur der Ausgang.
      if((a.vertrag||[]).length)a.vertrag=a.vertrag.filter(r=>r.eingang!==nm).map(r=>(r.ausgaenge||[]).includes(nm)?{...r,ausgaenge:ohneX(r.ausgaenge,nm)}:r);});
      // … und aus dem Rand der Clients (sendet/fragt/kenntnis).
      MODEL.clients.forEach(c=>{["sendet","fragt","kenntnis"].forEach(f=>{if((c[f]||[]).includes(nm))c[f]=ohneX(c[f],nm);});});}
    {const nm=MODEL.records.includes(ref)&&(ref.kind==="event"||ref.kind==="rejection")?ref.name:null;
      if(nm)MODEL.clients.forEach(c=>{if((c.kenntnis||[]).includes(nm))c.kenntnis=ohneX(c.kenntnis,nm);});}
    // … und aus den Funktionen: ein gelöschtes Ergebnis fällt aus dem OneOf, ein gelöschter Auftrag lässt den Eingang offen.
    if(MODEL.records.includes(ref))MODEL.funktionen.forEach(f=>{f.ergebnisse=ohneX(f.ergebnisse,ref.name);if(f.auftrag===ref.name)f.auftrag="";});
    if(VIEW.heim&&VIEW.heim[n.id]){delete VIEW.heim[n.id];speichereAnsicht();}
    if(VIEW.heimBlk&&VIEW.heimBlk[n.id]){delete VIEW.heimBlk[n.id];speichereAnsicht();}
    if(VIEW.heimAkteur&&VIEW.heimAkteur[n.id]){delete VIEW.heimAkteur[n.id];speichereAnsicht();}
    if(k==="aggregate")MODEL.aggregate.splice(MODEL.aggregate.indexOf(ref),1);
    else if(k==="state")MODEL.states.splice(MODEL.states.indexOf(ref),1);
    else if(k==="decider")MODEL.decider.splice(MODEL.decider.indexOf(ref),1);
    else if(k==="applier")MODEL.applier.splice(MODEL.applier.indexOf(ref),1);
    else if(k==="enum")MODEL.enums.splice(MODEL.enums.indexOf(ref),1);
    else if(k==="saga")MODEL.sagas.splice(MODEL.sagas.indexOf(ref),1);
    else if(k==="transition")MODEL.transitions.splice(MODEL.transitions.indexOf(ref),1);
    else if(k==="readmodel")MODEL.readModels.splice(MODEL.readModels.indexOf(ref),1);
    else if(k==="store")MODEL.stores.splice(MODEL.stores.indexOf(ref),1);
    else if(k==="projektion")MODEL.projektionen.splice(MODEL.projektionen.indexOf(ref),1);
    else if(k==="reader")MODEL.reader.splice(MODEL.reader.indexOf(ref),1);
    else if(k==="reaktion")MODEL.reaktionen.splice(MODEL.reaktionen.indexOf(ref),1);
    else if(k==="funktion"){MODEL.funktionen.splice(MODEL.funktionen.indexOf(ref),1);MODEL.akteure.forEach(a=>{a.darf=ohneX(a.darf,ref.name);});}
    // Ein gelöschter Dienst fällt aus jedem Konstruktor (Funktions- und Dienst-Implementierungen) — dieselbe Geste.
    else if(k==="dienst"){MODEL.dienste.splice(MODEL.dienste.indexOf(ref),1);
      MODEL.funktionen.forEach(f=>{if((f.konstruktor||[]).some(p=>dienstByName(p.typ)===ref))f.konstruktor=f.konstruktor.filter(p=>dienstByName(p.typ)!==ref);});
      MODEL.dienste.forEach(q=>(q.implementierungen||[]).forEach(im=>{im.konstruktor=(im.konstruktor||[]).filter(p=>p.typ.replace(/\?$/,"").trim()!==ref.name);}));}
    else if(k==="akteur")MODEL.akteure.splice(MODEL.akteure.indexOf(ref),1);
    else if(k==="client"){MODEL.clients.splice(MODEL.clients.indexOf(ref),1);AUF=null;}
    else if(k==="auf"){const v=n.own.ref.vertrag||[];v.splice(v.indexOf(ref),1);}
    else if(k==="handle"){const hs=n.own.ref.handles||[];hs.splice(hs.indexOf(ref),1);}
    else if(k==="flussknoten")flLoeschen(n.own.ref,ref);
    else if(k==="fn"){const st=n.own.ref;["writeFns","readFns"].forEach(a=>{const i=(st[a]||[]).indexOf(ref);if(i>=0)st[a].splice(i,1);});
      MODEL.projektionen.concat(MODEL.reader).forEach(o=>(o.handles||[]).forEach(hd=>{hd.fns=(hd.fns||[]).filter(x=>x!==ref._id);}));}
    else MODEL.records.splice(MODEL.records.indexOf(ref),1);
    render();}

  // Koordinaten: Welt = unskaliert; canvas = Bildschirm.
  // Nur der Transform wird sofort geschrieben (billig, der Browser bündelt ihn ohnehin je Frame). Alles daraus Abgeleitete
  //   (Minimap-Rahmen, Culling) läuft gebündelt EINMAL pro Frame (sichtFrame) — nie je Wheel-/Pointer-Event,
  //   und ohne Layout-Lesen: die Canvas-Größe kommt aus dem ResizeObserver-Cache (CV), die Knoten-Geometrie aus GEO.
  function applyPan(){if(!world)return;world.style.transform="translate("+PAN.x+"px,"+PAN.y+"px) scale("+PAN.s+")";planeSicht();}
  let SICHT_RAF=0, CV={w:0,h:0}, CV_RO=null, GEO=new Map();
  function planeSicht(){if(!SICHT_RAF)SICHT_RAF=requestAnimationFrame(()=>{SICHT_RAF=0;sichtFrame();});}
  function sichtFrame(){if(!canvas||!world)return;canvas.classList.toggle("fern",PAN.s<0.45);updateMinimapViewport();kulle();
    // Domänen-Rahmen: Linie + Kopf wachsen beim Rauszoomen mit (Variable NUR auf der Rahmen-Ebene → invalidiert nur die Rahmen).
    const re=world.querySelector(".grahmen-ebene"),rz=Math.min(2,Math.max(1,1/PAN.s)).toFixed(2),lz=Math.min(8,Math.max(1,1/PAN.s)).toFixed(2);
    if(re&&(re.style.getPropertyValue("--rz")!==rz||re.style.getPropertyValue("--lz")!==lz)){re.style.setProperty("--rz",rz);re.style.setProperty("--lz",lz);}}
  function beobachteCanvas(){if(CV_RO)CV_RO.disconnect();CV_RO=null;const r=canvas.getBoundingClientRect();CV.w=r.width;CV.h=r.height;
    if(window.ResizeObserver){CV_RO=new ResizeObserver(es=>{const c=es[es.length-1].contentRect;CV.w=c.width;CV.h=c.height;planeSicht();});CV_RO.observe(canvas);}}
  // Zieh-Gesten (Pan, Minimap-Ziehen): nur WÄHREND der Geste eine eigene GPU-Ebene (reines Verschieben → Raster bleibt gültig).
  //   Danach fällt sie weg → Neu-Rastern in der aktuellen Zoomstufe (scharf, und rausgezoomt winzig statt Welt×Start-Zoom).
  const bewegtAn=()=>{if(canvas)canvas.classList.add("bewegt");}, bewegtAus=()=>{if(canvas)canvas.classList.remove("bewegt");};
  // Knoten-Geometrie (Welt-Koordinaten) EINMAL je Layout-Änderung messen; die Welt-/Kanten-Ebene auf den Inhalt ausdehnen.
  function messeWelt(){GEO=new Map();if(!world)return;let mx=0,my=0;
    world.querySelectorAll(".gnode2").forEach(el=>{const g={el,x:el.offsetLeft,y:el.offsetTop,w:el.offsetWidth,h:el.offsetHeight,weg:el.classList.contains("weg")};
      GEO.set(el.dataset.id,g);mx=Math.max(mx,g.x+g.w);my=Math.max(my,g.y+g.h);});
    const rb=zeichneRahmen();mx=Math.max(mx,rb.x2);my=Math.max(my,rb.y2);
    const W=Math.max(6000,Math.ceil(mx+400))+"px",H=Math.max(4000,Math.ceil(my+400))+"px";
    [world,svg,svgTop].forEach(e=>{if(e&&(e.style.width!==W||e.style.height!==H)){e.style.width=W;e.style.height=H;}});
    planeSicht();}
  // Culling: Knoten außerhalb Sichtfenster + ½ Fenster Rand werden nicht gemalt (visibility — Maße bleiben für Anker/Kanten).
  function kulle(){if(!CV.w||!GEO.size)return;const W=CV.w/PAN.s,H=CV.h/PAN.s,x0=-PAN.x/PAN.s-W/2,y0=-PAN.y/PAN.s-H/2,x1=x0+2*W,y1=y0+2*H;
    GEO.forEach(g=>{const weg=!g.el.classList.contains("dragging")&&(g.x>x1||g.x+g.w<x0||g.y>y1||g.y+g.h<y0);
      if(weg!==g.weg){g.weg=weg;g.el.classList.toggle("weg",weg);}});}

  // ── DOMÄNEN-FILTER ────────────────────────────────────────────────────────────────────────
  function allDomains(){
    const counts=new Map();
    graphNodes().forEach(n=>{const g=groupKeyOf(n);counts.set(g,(counts.get(g)||0)+1);});
    const order=MODEL.aggregate.map(a=>a.name).filter(k=>counts.has(k));
    [...counts.keys()].filter(k=>k!==SHARED_KEY&&!order.includes(k)).sort().forEach(k=>order.push(k));
    if(counts.has(SHARED_KEY))order.push(SHARED_KEY);
    return order.map(k=>({key:k,label:k===SHARED_KEY?"⋯ Geteilt":k,count:counts.get(k)}));
  }
  window.deFilter=function(){FILTER_OPEN=!FILTER_OPEN;renderFilterPanel();};
  function renderFilterPanel(){
    if(!canvas)return;
    const old=canvas.querySelector(".gfilter");if(old)old.remove();
    if(!FILTER_OPEN)return;
    const p=h("div",{class:"gfilter"});
    // (1) Knotenarten ausblenden — z. B. 🤖 LLM, 📝 Code, Ablehnungen, Value Objects. Ausgeblendete Arten reservieren im Raster nichts.
    const zahl=new Map();graphNodes().forEach(n=>zahl.set(n.kind,(zahl.get(n.kind)||0)+1));
    // 📝 Code und 🤖 LLM immer anbieten (auch bevor es einen 🤖-Knoten gibt) — sonst nur Arten, die im Modell vorkommen.
    const arten=Object.keys(NODELABEL).filter(k=>zahl.has(k)||k==="rumpf").concat([...zahl.keys()].filter(k=>!NODELABEL[k]));
    const MENU={rumpf:"📝 Rumpf"};
    p.append(h("h4",{},h("span",{},"👁 Knotenarten"),
      h("span",{style:"cursor:pointer;color:#8a93a7",title:"Schließen",onclick:()=>{FILTER_OPEN=false;renderFilterPanel();}},"✕")));
    p.append(h("div",{class:"mm-q"},
      h("button",{onclick:()=>{VIEW.aus=[];speichereAnsicht();render();}},"Alle"),
      h("button",{title:"Nur die Fachknoten — die Rumpf-Ausschnitte (📝) ausblenden",onclick:()=>{VIEW.aus=["rumpf"];speichereAnsicht();render();}},"Ohne Code")));
    arten.forEach(k=>{const cb=h("input",{type:"checkbox"});cb.checked=!istAus(k);
      cb.onchange=()=>setzeAus(k,!cb.checked);
      p.append(h("label",{},cb,h("i",{class:"kc-"+k,style:"width:9px;height:9px;border-radius:50%;flex:none;display:inline-block"}),
        h("span",{style:"flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"},MENU[k]||NODELABEL[k]||k),
        h("span",{style:"color:#6b7488"},String(zahl.get(k)||0))));});
    // (2) Domänen.
    p.append(h("h4",{style:"margin-top:10px"},h("span",{},"🗂 Domänen")));
    p.append(h("div",{class:"mm-q"},
      h("button",{onclick:()=>{HIDDEN.clear();saveHidden();render();}},"Alle"),
      h("button",{onclick:()=>{allDomains().forEach(d=>HIDDEN.add(d.key));saveHidden();render();}},"Keine")));
    allDomains().forEach(d=>{
      const cb=h("input",{type:"checkbox"});cb.checked=!HIDDEN.has(d.key);
      cb.onchange=()=>{if(cb.checked)HIDDEN.delete(d.key);else HIDDEN.add(d.key);saveHidden();render();};
      const sw=h("i",{style:"width:9px;height:9px;border-radius:2px;flex:none;background:hsl("+domHue(d.key)+" 45% 55%)"});
      p.append(h("label",{},cb,sw,h("span",{style:"flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap"},d.label),
        h("span",{style:"color:#6b7488"},String(d.count))));
    });
    p.onpointerdown=e=>e.stopPropagation();
    p.ondblclick=e=>e.stopPropagation();
    canvas.append(p);
  }

  // ── MINIMAP (klickbar, zeigt den aktuellen Viewport) ──────────────────────────────────────
  function buildMinimap(cv){
    const box=h("div",{class:"gminimap",title:"Minimap — klicken/ziehen zum Springen"});
    const s=document.createElementNS(SVGNS,"svg");box.append(s);
    cv.append(box);
    MM={box:box,svg:s,vp:null,scale:1,minx:0,miny:0};
    let drag=false,br=null;   // Minimap-Rechteck einmal je Geste messen, nicht je pointermove
    const jump=ev=>{const r=br||box.getBoundingClientRect();
      const wx=MM.minx+(ev.clientX-r.left)/MM.scale, wy=MM.miny+(ev.clientY-r.top)/MM.scale;
      PAN.x=CV.w/2-wx*PAN.s;PAN.y=CV.h/2-wy*PAN.s;applyPan();};
    const ende=()=>{if(!drag)return;drag=false;br=null;bewegtAus();};
    box.addEventListener("pointerdown",e=>{e.stopPropagation();e.preventDefault();drag=true;br=box.getBoundingClientRect();bewegtAn();try{box.setPointerCapture(e.pointerId);}catch(x){}jump(e);});
    box.addEventListener("pointermove",e=>{if(drag)jump(e);});
    box.addEventListener("pointerup",ende);box.addEventListener("pointercancel",ende);box.addEventListener("lostpointercapture",ende);
    box.addEventListener("dblclick",e=>e.stopPropagation());
  }
  function drawMinimap(){
    if(!MM||!MM.svg||!world)return;
    while(MM.svg.firstChild)MM.svg.removeChild(MM.svg.firstChild);
    const vis=graphNodes().filter(n=>VIS.has(n.id));
    const boxes=vis.map(n=>{const g=GEO.get(n.id),p=P(n);
      return {x:p.x||0,y:p.y||0,w:(g&&g.w)||250,h:(g&&g.h)||120,key:groupKeyOf(n),kind:n.kind};});
    if(!boxes.length){MM.vp=null;return;}
    let minx=1e9,miny=1e9,maxx=-1e9,maxy=-1e9;
    boxes.forEach(b=>{minx=Math.min(minx,b.x);miny=Math.min(miny,b.y);maxx=Math.max(maxx,b.x+b.w);maxy=Math.max(maxy,b.y+b.h);});
    const pad=100;minx-=pad;miny-=pad;maxx+=pad;maxy+=pad;
    const r=MM.box.getBoundingClientRect();const mmW=r.width||212, mmH=r.height||150;
    const scale=Math.min(mmW/(maxx-minx), mmH/(maxy-miny));
    MM.scale=scale;MM.minx=minx;MM.miny=miny;
    MM.svg.setAttribute("viewBox","0 0 "+mmW+" "+mmH);
    const frag=document.createDocumentFragment();
    boxes.forEach(b=>{const el=document.createElementNS(SVGNS,"rect");
      el.setAttribute("x",((b.x-minx)*scale).toFixed(1));el.setAttribute("y",((b.y-miny)*scale).toFixed(1));
      el.setAttribute("width",Math.max(1,b.w*scale).toFixed(1));el.setAttribute("height",Math.max(1,b.h*scale).toFixed(1));
      el.setAttribute("rx","1");el.setAttribute("fill","hsl("+domHue(b.key)+" 45% 56%)");el.setAttribute("opacity",".85");
      el.setAttribute("data-kind",b.kind);if(HLKIND&&b.kind===HLKIND)el.classList.add("mmhi");frag.append(el);});
    const vp=document.createElementNS(SVGNS,"rect");vp.setAttribute("class","mmvp");frag.append(vp);
    MM.svg.append(frag);MM.vp=vp;updateMinimapViewport();
  }
  function updateMinimapViewport(){
    if(!MM||!MM.vp||!canvas)return;
    const cr={width:CV.w,height:CV.h};if(!cr.width)return;
    MM.vp.setAttribute("x",((-PAN.x/PAN.s-MM.minx)*MM.scale).toFixed(1));
    MM.vp.setAttribute("y",((-PAN.y/PAN.s-MM.miny)*MM.scale).toFixed(1));
    MM.vp.setAttribute("width",Math.max(3,(cr.width/PAN.s)*MM.scale).toFixed(1));
    MM.vp.setAttribute("height",Math.max(3,(cr.height/PAN.s)*MM.scale).toFixed(1));
  }
  function toWorld(cx,cy){const r=canvas.getBoundingClientRect();return {x:(cx-r.left-PAN.x)/PAN.s,y:(cy-r.top-PAN.y)/PAN.s};}
  function slotCenter(el){const wr=world.getBoundingClientRect(),r=el.getBoundingClientRect();
    return {x:(r.left+r.width/2-wr.left)/PAN.s,y:(r.top+r.height/2-wr.top)/PAN.s};}
  const bez=(x1,y1,x2,y2)=>{const dx=Math.max(46,Math.abs(x2-x1)*0.5);return "M"+x1+","+y1+" C"+(x1+dx)+","+y1+" "+(x2-dx)+","+y2+" "+x2+","+y2;};

  function applyLink(a,b){
    const O=a.__slot.dir==="out"?a.__slot:b.__slot, I=a.__slot.dir==="out"?b.__slot:a.__slot;gTitel("verbinden ("+O.type+")");
    if(O.type==="darf"&&O.client){const c=MODEL.clients.find(x=>x._id===O.client);if(c&&I.rec){const f=(recByName(I.rec)||{}).kind==="query"?"fragt":"sendet";
        if(!(c[f]=c[f]||[]).includes(I.rec))c[f].push(I.rec);if(!darfHalter(I.rec).length)deFlash("⚠ kein Akteur darf "+I.rec+" — GR-CLIENT-BEFUGT (erst darf ⊕ beim Akteur)",false);}}
    else if(O.type==="traegt"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);
      if(c&&a){const t=vertragsTyp(a);if(!(c.traegt=c.traegt||[]).includes(t))c.traegt.push(t);}}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak&&I.rec&&!(ak.darf=ak.darf||[]).includes(I.rec))ak.darf.push(I.rec);}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d)d.command=O.rec;}
    // Rufe<F>: eine Prozess-Regel („Dann rufe") bzw. ein Pipeline-Knoten ƒ ruft die Funktion; die Zuordnung wird neu vorgeschlagen.
    else if(O.type==="rufe"){const fk=MODEL.funktionen.find(x=>x._id===I.fk);if(fk){
      if(O.flk){const k=flKnotenById(O.flk);if(k){k.typ=fk.name;flNeuZuordnen(k);}}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans),d=t&&(t.dann||[])[O.dannIdx];if(d){if(d.rufe!==fk.name){delete d.sendeAusdruck;delete d.sendeArgs;}d.rufe=fk.name;}}}}
    // Ablauf „dann …": ein neuer Schritt im Prozess (Regel bei diesem Ergebnis → Funktion bzw. Command).
    else if(O.type==="kette"){const sg=MODEL.sagas.find(x=>x.name===O.saga);
      const z=I.type==="rufe"?{rufe:(MODEL.funktionen.find(x=>x._id===I.fk)||{}).name}:{sende:I.rec};if(sg&&(z.rufe||z.sende))kettenAnhaengen(sg,O.bei,z);}
    // Fluss: Draht von einem Ausgang in einen späteren Knoten (∧/∨ automatisch); Je-Rahmen → „je Element"/„sammle".
    else if(O.type==="draht"){const v=flKnotenById(O.flk),z=flKnotenById(I.flk),f=v&&flFluss(v);if(v&&z&&f)flVerbinde(f,v.name,{port:O.port,fall:O.fall},z);}
    else if(O.type==="jeEl"||O.type==="jeSa"){const v=flKnotenById(O.flk),z=flKnotenById(I.flk),f=v&&flFluss(v),sa=O.type==="jeSa";
      if(v&&z&&f&&!(z.eingaenge||[]).some(e=>e.je===v.name&&!!e.sammle===sa)){const e2=sa?{je:v.name,sammle:true,draehte:[]}:{je:v.name,draehte:[]};
        (z.eingaenge=z.eingaenge||[]).push(e2);if(!sa)e2.argumente=flAutoArgs(f,z,e2);}}
    // Auftrag → Funktion: der eine Eingang (IAuftrag<F>); ein Auftrag gehört genau einer Funktion.
    else if(O.type==="auftragFn"){const f=MODEL.funktionen.find(x=>x._id===I.fk),r=recByName(O.rec);if(f&&r){
      MODEL.funktionen.forEach(x=>{if(x!==f&&x.auftrag===r.name)x.auftrag="";});const alt=recByName(f.auftrag);if(alt&&alt.funktion===f.name)delete alt.funktion;
      f.auftrag=r.name;r.funktion=f.name;}}
    else if(O.type==="evtOut"){
      if(O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk);if(f&&!(f.ergebnisse=f.ergebnisse||[]).includes(I.rec))f.ergebnisse.push(I.rec);}
      else if(O.dec){const d=dec(O.dec);if(d&&!(d.ergibt||[]).some(x=>x.event===I.rec))(d.ergibt=d.ergibt||[]).push({event:I.rec});}
      // Konsument veröffentlicht ein reaktives Event (HandlerOutputRouter: yield IEvent → Broker-Re-Publish).
      else if(O.proj){const p=MODEL.projektionen.find(x=>x._id===O.proj);const hd=p&&p.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}
      else if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}}
    else if(O.type==="prozess"){const t=MODEL.transitions.find(x=>x._id===O.trans);if(t)t.prozess=I.saga;}
    else if(O.type==="evtUse"){
      // Event → Pipeline-Knoten: Quelle (meldet / Event aus dem Log) bzw. Warten (eines von mehreren Events im Strom).
      if(I.flk){const k=flKnotenById(I.flk);if(k){if(I.role==="warte"){const l=flWarteAuf(k);if(!l.includes(O.rec))l.push(O.rec);k.warteAuf=l;k.typ=l[0];}else k.typ=O.rec;}}
      else if(I.client){const c=MODEL.clients.find(x=>x._id===I.client);if(c&&!(c.kenntnis=c.kenntnis||[]).includes(O.rec))c.kenntnis.push(O.rec);}
      else if(I.app){const p=app(I.app);if(p)p.event=O.rec;}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p){p.handles=p.handles||[];
        if(I.handleIdx==="open"){if(!p.handles.some(x=>x.event===O.rec))p.handles.push({event:O.rec,effekt:""});}
        else p.handles[I.handleIdx].event=O.rec;}}
      else if(I.trigger){const sg=MODEL.sagas.find(x=>x.name===I.saga);if(sg)sg.triggerEvent=O.rec;}
      // Event → Zusage im Vertrag eines Akteurs (Akteur-Konzept §3): neue Zusage bzw. Eingang einer bestehenden umhängen.
      else if(I.akt&&I.aufIdx!=null){const ak=MODEL.akteure.find(x=>x._id===I.akt);if(ak){ak.vertrag=ak.vertrag||[];
        if(I.aufIdx==="open"){if(!ak.vertrag.some(r=>r.eingang===O.rec))ak.vertrag.push({eingang:O.rec,ausgaenge:[]});}
        else if(ak.vertrag[I.aufIdx])ak.vertrag[I.aufIdx].eingang=O.rec;}}
      else if(I.trans){const t=MODEL.transitions.find(x=>x._id===I.trans);if(t){
        t.wenn=t.wenn||[];if(I.wennIdx==="open"){if(!t.wenn.includes(O.rec))t.wenn.push(O.rec);}else t.wenn[I.wennIdx]=O.rec;}}
      else if(I.reaktion){const r=MODEL.reaktionen.find(x=>x._id===I.reaktion);if(r){r.handles=r.handles||[];
        if(I.handleIdx==="open"){if(!r.handles.some(x=>x.event===O.rec))r.handles.push({event:O.rec,sends:[]});}
        else r.handles[I.handleIdx].event=O.rec;}}}
    else if(O.type==="sagaCmd"){
      if(O.akt&&typeof O.aufIdx==="number"){const ak=MODEL.akteure.find(x=>x._id===O.akt),r=ak&&(ak.vertrag||[])[O.aufIdx];
        if(r){r.ausgaenge=r.ausgaenge||[];if(!r.ausgaenge.includes(I.rec))r.ausgaenge.push(I.rec);}}
      else if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.sends=hd.sends||[];if(!hd.sends.includes(I.rec))hd.sends.push(I.rec);}}
      // Pipeline-Knoten ▶ sendet genau diesen Command ins Aggregat.
      else if(O.flk){const k=flKnotenById(O.flk);if(k){k.typ=I.rec;flNeuZuordnen(k);}}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans);const d=t&&(t.dann||[])[O.dannIdx];if(d){
        if(O.role==="komp"){if(d.kompensation!==I.rec)delete d.kompensationAusdruck;d.kompensation=I.rec;}
        else{if(d.sende!==I.rec)delete d.sendeAusdruck;d.sende=I.rec;}}}}
    else if(O.type==="decAgg"){const d=dec(O.dec);if(d)d.aggregat=I.agg;}
    else if(O.type==="appAgg"){const p=app(O.app);if(p)p.aggregat=I.agg;}
    // ── Leseseite: ReadModel→Store, Projektion-Handle→Write-Fn, Reader-Handle→Read-Fn, Reader→Projektion,
    //    Event→Projektion, Query→Reader, Reader→Response, CODE (📝/🤖) → Rumpf-Port (Controller / Impl). ──
    else if(O.type==="readmodel"){const rm=MODEL.readModels.find(x=>x._id===O.rm);if(rm)rm.store=I.store;}
    // Projektion-Handle ruft eine Write-Fn (Store-Scope fällt daraus ab) — anhängen, dedup.
    else if(O.type==="wcall"){const p=MODEL.projektionen.find(x=>x._id===O.proj);const hd=p&&p.handles[O.handleIdx];if(hd){hd.fns=hd.fns||[];if(!hd.fns.includes(I.fn))hd.fns.push(I.fn);}}
    // Reader-Handle ruft eine Read-Fn (auch über mehrere Stores) — anhängen, dedup.
    // Funktion liest über eine Store-Fähigkeit: ihr Interface wird ein Parameter (nur Read-Fns mit Fähigkeits-Interface).
    // 🧩 Dienst in den Konstruktor einer Funktions- bzw. Dienst-Implementierung (fehlt die C#-Implementierung der Funktion: sie entsteht mit).
    else if(O.type==="dienst"){const d=MODEL.dienste.find(x=>x._id===I.dn);
      if(d&&O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk);if(f&&!(f.konstruktor=f.konstruktor||[]).some(p=>dienstByName(p.typ)===d)){
        f.konstruktor.push({typ:d.name,name:paramName(d.name,f.konstruktor.map(p=>p.name))});if(!f.implementierung&&f.entwurf==null)f.entwurf="";}}
      else if(d&&O.dn){const q=MODEL.dienste.find(x=>x._id===O.dn),im=q&&(q.implementierungen||[])[O.impl];
        if(im&&q!==d&&!(im.konstruktor=im.konstruktor||[]).some(p=>dienstByName(p.typ)===d))im.konstruktor.push({typ:d.name,name:paramName(d.name,im.konstruktor.map(p=>p.name))});}}
    else if(O.type==="rcall"&&O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk),z=fnById(I.fn);if(f&&z){
      const t=z.fn.faehigkeit||(z.fn.sig?null:faehigkeitVorschlag(z.store,z.fn));
      if(!t)deFlash("⚠ "+z.store.name+"."+z.fn.name+" hat kein Fähigkeits-Interface (CQRS051) — als Parameter nicht nutzbar",false);
      else if(!(f.faehigkeiten=f.faehigkeiten||[]).some(p=>p.typ===t)){if(!z.fn.faehigkeit)z.fn.faehigkeit=t;
        let nm=t.replace(/^I(?=[A-ZÄÖÜ])/,"");nm=nm.charAt(0).toLowerCase()+nm.slice(1);f.faehigkeiten.push({typ:t,name:nm});}}}
    else if(O.type==="rcall"){const r=MODEL.reader.find(x=>x._id===O.reader);const hd=r&&r.handles[O.handleIdx];if(hd){hd.fns=hd.fns||[];if(!hd.fns.includes(I.fn))hd.fns.push(I.fn);}}
    // IReader<TProjection>: Reader → genau eine Projektion binden.
    else if(O.type==="projref"){const r=MODEL.reader.find(x=>x._id===O.reader);const p=MODEL.projektionen.find(x=>x._id===I.proj);if(r&&p)r.projektion=p.name;}
    else if(O.type==="query"){const r=MODEL.reader.find(x=>x._id===I.reader);if(r){r.handles=r.handles||[];
      if(I.handleIdx==="open"){if(!r.handles.some(x=>x.query===O.rec))r.handles.push({query:O.rec,responses:[]});}
      else r.handles[I.handleIdx].query=O.rec;}}
    // OneOf-Response: mehrere Antworten je Query → anhängen (dedup), gespiegelt zu decider.ergibt.
    else if(O.type==="qrsp"){const r=MODEL.reader.find(x=>x._id===O.reader);const hd=r&&r.handles[O.handleIdx];if(hd){hd.responses=hd.responses||[];if(!hd.responses.includes(I.rec))hd.responses.push(I.rec);}}
    // Typ-Komposition: VO/Enum-Quelle → Feld-Typ-Eingang → setzt den Feldtyp (Wrapper bleibt erhalten).
    else if(O.type==="ftype"){if(I.fobj)setFeldTyp(I.fobj,O.typeName);}
    // Feld-Port → Konsument (verdrahtet statt Dropdown): Fan-out-Collection (want=collection). (Σ/Count-Join entfernt.)
    else if(O.type==="field"){if(I.trans){const t=MODEL.transitions.find(x=>x._id===I.trans);if(t){
      if(I.want==="collection"){const d=(t.dann||[])[I.dannIdx];if(d)d.sendeJeCollectionFeld={rec:O.rec,field:O.field,fid:O.fid};}}}}
    else if(O.type==="state"){const s=MODEL.states.find(x=>x._id===O.state);const A=I.agg;
      if(s){s.aggregat=A;const agg=MODEL.aggregate.find(a=>a.name===A);
        if(agg){if((!agg.state||!agg.state.length)&&s.felder&&s.felder.length)agg.state=s.felder;s.felder=undefined;
          MODEL.states=MODEL.states.filter(x=>x===s||x.aggregat!==A);}}}
    render();
  }

  // Verbindung LÖSEN — das Gegenstück zu applyLink (gleiche Port-Typen, gleiche Modell-Felder, nur rückwärts).
  function loeseLink(a,b){
    const O=a.__slot.dir==="out"?a.__slot:b.__slot, I=a.__slot.dir==="out"?b.__slot:a.__slot;gTitel("lösen ("+O.type+")");
    const ohne=(arr,x)=>(arr||[]).filter(v=>v!==x);
    const handle=(liste,id,idx)=>{const o=liste.find(x=>x._id===id);return o&&typeof idx==="number"?(o.handles||[])[idx]:null;};
    if(O.type==="darf"&&O.client){const c=MODEL.clients.find(x=>x._id===O.client);if(c){c.sendet=ohne(c.sendet,I.rec);c.fragt=ohne(c.fragt,I.rec);}}
    else if(O.type==="traegt"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);if(c&&a)c.traegt=ohne(c.traegt,vertragsTyp(a));}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak)ak.darf=ohne(ak.darf,I.rec);}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d&&d.command===O.rec)d.command="";}
    else if(O.type==="rufe"){if(O.flk){const k=flKnotenById(O.flk);if(k)k.typ="";}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans),d=t&&(t.dann||[])[O.dannIdx];if(d){d.rufe="";delete d.sendeAusdruck;delete d.sendeArgs;}}}
    else if(O.type==="kette")kettenLoesen(O.saga,O.bei,I.type==="rufe"?(MODEL.funktionen.find(x=>x._id===I.fk)||{}).name:I.rec);
    else if(O.type==="draht"){const v=flKnotenById(O.flk),z=flKnotenById(I.flk),f=v&&flFluss(v);if(v&&z&&f)(z.eingaenge||[]).slice().forEach(e=>{
      const d=(e.draehte||[]).find(x=>x.von===v.name&&flPortGleich(x,{port:O.port,fall:O.fall}));if(d)flLoese(f,z,e,d);});}
    else if(O.type==="jeEl"||O.type==="jeSa"){const v=flKnotenById(O.flk),z=flKnotenById(I.flk);
      if(v&&z)z.eingaenge=(z.eingaenge||[]).filter(e=>!(e.je===v.name&&!!e.sammle===(O.type==="jeSa")));}
    else if(O.type==="auftragFn"){const f=MODEL.funktionen.find(x=>x._id===I.fk),r=recByName(O.rec);if(f&&f.auftrag===O.rec){f.auftrag="";if(r)delete r.funktion;}}
    else if(O.type==="evtOut"){
      if(O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk);if(f)f.ergebnisse=ohne(f.ergebnisse,I.rec);}
      else if(O.dec){const d=dec(O.dec);if(d)d.ergibt=(d.ergibt||[]).filter(x=>x.event!==I.rec);}
      else{const hd=handle(O.proj?MODEL.projektionen:MODEL.reaktionen,O.proj||O.reaktion,O.handleIdx);if(hd)hd.publishes=ohne(hd.publishes,I.rec);}}
    else if(O.type==="prozess"){const t=MODEL.transitions.find(x=>x._id===O.trans);if(t)t.prozess="";}
    else if(O.type==="evtUse"){
      if(I.flk){const k=flKnotenById(I.flk);if(k){if(I.role==="warte"){k.warteAuf=flWarteAuf(k).filter(x=>x!==O.rec);k.typ=k.warteAuf[0]||"";}else k.typ="";}}
      else if(I.client){const c=MODEL.clients.find(x=>x._id===I.client);if(c)c.kenntnis=ohne(c.kenntnis,O.rec);}
      else if(I.app){const p=app(I.app);if(p)p.event="";}
      else if(I.trigger){const sg=MODEL.sagas.find(x=>x.name===I.saga);if(sg)sg.triggerEvent="";}
      else if(I.akt&&I.aufIdx!=null){const ak=MODEL.akteure.find(x=>x._id===I.akt);if(ak)ak.vertrag=(ak.vertrag||[]).filter(r=>r.eingang!==O.rec);}
      else if(I.trans){const t=MODEL.transitions.find(x=>x._id===I.trans);if(t)t.wenn=ohne(t.wenn,O.rec);}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p)p.handles=(p.handles||[]).filter(h=>h.event!==O.rec);}
      else if(I.reaktion){const r=MODEL.reaktionen.find(x=>x._id===I.reaktion);if(r)r.handles=(r.handles||[]).filter(h=>h.event!==O.rec);}}
    else if(O.type==="sagaCmd"){
      if(O.akt&&typeof O.aufIdx==="number"){const ak=MODEL.akteure.find(x=>x._id===O.akt),r=ak&&(ak.vertrag||[])[O.aufIdx];if(r)r.ausgaenge=ohne(r.ausgaenge,I.rec);}
      else if(O.reaktion){const hd=handle(MODEL.reaktionen,O.reaktion,O.handleIdx);if(hd)hd.sends=ohne(hd.sends,I.rec);}
      else if(O.flk){const k=flKnotenById(O.flk);if(k)k.typ="";}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans);const d=t&&(t.dann||[])[O.dannIdx];
        if(d){if(O.role==="komp"){delete d.kompensation;delete d.kompensationAusdruck;}else{d.sende="";delete d.sendeAusdruck;}}}}
    else if(O.type==="decAgg"){const d=dec(O.dec);if(d)d.aggregat="";}
    else if(O.type==="appAgg"){const p=app(O.app);if(p)p.aggregat="";}
    else if(O.type==="readmodel"){const rm=MODEL.readModels.find(x=>x._id===O.rm);if(rm)rm.store="";}
    else if(O.type==="wcall"){const hd=handle(MODEL.projektionen,O.proj,O.handleIdx);if(hd)hd.fns=ohne(hd.fns,I.fn);}
    else if(O.type==="rcall"&&O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk),z=fnById(I.fn);
      if(f&&z)f.faehigkeiten=(f.faehigkeiten||[]).filter(p=>p.typ!==z.fn.faehigkeit);}
    else if(O.type==="dienst"){const d=MODEL.dienste.find(x=>x._id===I.dn);
      if(d&&O.fk){const f=MODEL.funktionen.find(x=>x._id===O.fk);if(f)f.konstruktor=(f.konstruktor||[]).filter(p=>dienstByName(p.typ)!==d);}
      else if(d&&O.dn){const q=MODEL.dienste.find(x=>x._id===O.dn),im=q&&(q.implementierungen||[])[O.impl];if(im)im.konstruktor=(im.konstruktor||[]).filter(p=>dienstByName(p.typ)!==d);}}
    else if(O.type==="rcall"){const hd=handle(MODEL.reader,O.reader,O.handleIdx);if(hd)hd.fns=ohne(hd.fns,I.fn);}
    else if(O.type==="projref"){const r=MODEL.reader.find(x=>x._id===O.reader);if(r)r.projektion="";}
    else if(O.type==="query"){const r=MODEL.reader.find(x=>x._id===I.reader);if(r)r.handles=(r.handles||[]).filter(h=>h.query!==O.rec);}
    else if(O.type==="qrsp"){const hd=handle(MODEL.reader,O.reader,O.handleIdx);if(hd)hd.responses=ohne(hd.responses,I.rec);}
    else if(O.type==="state"){const st=MODEL.states.find(x=>x._id===O.state);if(st)st.aggregat="";}
    else{deFlash("Diese Verbindung lässt sich hier nicht lösen — im Formular ändern.",false);return;}
    render();
  }

  // ══ GRAMMATIK (Phase 1, docs/konzept-editor-komposition.md §3): EINE Quelle — DomainEditor.Grammatik, vom Extractor als
  //   rahmen.grammatik ins Board gelegt. Hier ist nichts davon hart kodiert: Sorten, Tabelle, Regeln kommen aus dem Board — nur zum ANZEIGEN.
  //   Was an einen Port passt (⊕), entscheidet der Server (GraphExtractor/Sicht.Passt, POST /api/werkbank/passt); ein gesperrtes Ziel nennt
  //   die Regel beim Namen.
  const GR=()=>(MODEL.rahmen&&MODEL.rahmen.grammatik)||null;
  const grName=(liste,id)=>{const g=GR();const x=g&&(g[liste]||[]).find(y=>y.id===id);return x?x.name:id;};
  const grSymbol=s=>{const g=GR();const x=g&&(g.sorten||[]).find(y=>y.id===s);return x?x.symbol:"•";};
  // Nachricht (Name) → Sorte: Record-Art (Quell-Nachricht über ihre Treiber-Basis).
  function grNachrichtSorte(name){const g=GR(),r=recByName(name);if(r)return (r.kind==="event"&&(r.treiber||(g.quellBasis&&(r.basen||[]).includes(g.quellBasis))))?"quelle":(g.recordSorte||{})[r.kind]||null;
    return null;}
  // 📐 Grammatik: die Tabelle und „Regel → Build-Gegenstück" ins Ausgabe-Panel.
  window.deGrammatik=function(){const g=GR(),out=document.getElementById("de-out");if(!out)return;out.innerHTML="";
    if(!g){out.append(h("div",{class:"find warning"},"Keine Grammatik im Board — GraphExtractor neu laufen lassen."));return;}
    const KARD={eins:"genau 1",beliebig:"beliebig viele",dieselbe:"dieselbe"};
    out.append(h("div",{class:"find"},"📐 Grammatik der Kompositions-Sprache — Sorte → Eingang (Kardinalität je Nachricht):"));
    g.konsume.forEach(k=>out.append(h("div",{class:"find info"},grSymbol(k.sorte)+" "+grName("sorten",k.sorte)+" → "+grName("bausteine",k.baustein)+" · "+(KARD[k.kardinalitaet]||k.kardinalitaet)+" · "+k.regel)));
    out.append(h("div",{class:"find"},"Wer erzeugt was:"));
    g.erzeugungen.forEach(e=>out.append(h("div",{class:"find info"},grName("bausteine",e.baustein)+" → "+grSymbol(e.sorte)+" "+grName("sorten",e.sorte)+" · "+e.regel)));
    out.append(h("div",{class:"find"},"Regel → Build-Gegenstück:"));
    g.regeln.forEach(r=>out.append(h("div",{class:"find "+(r.build==="offen"?"warning":"info"),title:r.text},r.id+" · "+r.name+" — Build: "+r.build)));};

  // ══ DOMÄNEN LADEN (Blank-Start): Der Editor startet LEER; im Start-Dialog wählt man, welche Domänen (Namespaces) geladen werden.
  //   Reine Editor-Sicht: das Modell bleibt vollständig (Gesten sehen alles) — nicht Geladenes wird nur nicht
  //   gezeigt und ist kein Verbindungsziel. Entwürfe (neu im Editor) sind immer sichtbar.
  let LADEN=null, START_OFFEN=false;   // null = alles geladen, [] = leer, sonst die gewählten Namespaces
  const elternNs=ns=>{const i=(ns||"").lastIndexOf(".");return i>0?ns.slice(0,i):"";};
  const drinNs=(E,ns)=>!!ns&&(E===""||ns===E||ns.startsWith(E+"."));
  const letztesSeg=ns=>(ns||"").slice((ns||"").lastIndexOf(".")+1);
  // Namespace eines Knotens — aus dem Code (Namespace-Feld) bzw. über die Verdrahtung zu seinem Besitzer; null = keiner.
  function nsVon(n){const r=n.ref,k=n.kind;
    if(k==="handle"||k==="fn")return nsVon(n.own);
    if(k==="auf"){const e=recByName(r.eingang);return e?e.namespace:nsVon(n.own);}
    if(k==="decider"||k==="applier"||k==="state"){const a=MODEL.aggregate.find(x=>x.name===r.aggregat);return a?a.namespace:null;}
    if(k==="transition"){const s=MODEL.sagas.find(x=>x.name===r.prozess);return s?s.namespace:null;}
    if(k==="rumpf")return nsVon(n.own);
    return r.namespace||null;}
  // Stammt der Knoten aus dem Code (sonst: Entwurf, immer sichtbar)? Abgeleitete Knoten folgen ihrem Besitzer.
  function ausCodeVon(n){const k=n.kind,r=n.ref;
    if(k==="handle"||k==="fn"||k==="auf")return ausCodeVon(n.own);
    if(k==="state"){const a=MODEL.aggregate.find(x=>x.name===r.aggregat);return !!(a&&a.ausCode);}
    if(k==="transition"){const sg=MODEL.sagas.find(x=>x.name===r.prozess);return sg?!!sg.ausCode:r.ausCode===true;}
    if(k==="rumpf")return ausCodeVon(n.own);
    return r.ausCode===true;}
  // Geladen, wenn seine DOMÄNE (Graph-Zuordnung, domKey — z. B. ImagePairProjection → ImagePair) ODER sein eigener Namespace
  //   (z. B. „Projections“ ausdrücklich gewählt) in der Auswahl liegt. Die Domänen-Gehörigkeit steht über dem Namespace.
  function geladen(n){if(n.kind==="client")return LADEN===null||clientPorts(n.ref).some(p=>{const m=p.node&&NODEBY.get(p.node);return m&&m.kind!=="client"&&geladen(m);});
    if(LADEN===null||!ausCodeVon(n))return true;const x=nsVon(n),d=domKey(n);
    if(istFlussDom(d)){const f=flussVonDom(d),hm=FLHEIM.get(d);return LADEN.some(g=>(f&&f.namespace&&drinNs(g,f.namespace))||(hm&&drinNs(g,hm)));}
    return LADEN.some(g=>(x&&drinNs(g,x))||(d!==OHNE_DOM&&drinNs(g,d)));}
  window.deLaden=function(){START_OFFEN=true;render();};
  // Start-Dialog: leer starten, Domänen wählen (Häkchen am Eltern-Namespace lädt alles darunter) oder alles laden.
  function zeigeStart(){if(!canvas)return;const alt=canvas.querySelector(".gstart");if(alt)alt.remove();if(!START_OFFEN)return;
    const nsAlle=new Set();["records","enums","aggregate","sagas","readModels","stores","projektionen","reader","reaktionen"]
      .forEach(c=>(MODEL[c]||[]).forEach(x=>{if(x&&x.namespace&&x.ausCode)for(let p=x.namespace;p;p=elternNs(p))nsAlle.add(p);}));
    // Wurzel ohne Ein-Kind-Kette (z. B. „Domain“) nicht als Wahl zeigen — darunter beginnen die Domänen.
    let top="";for(;;){const k=[...nsAlle].filter(x=>elternNs(x)===top);if(k.length!==1)break;top=k[0];}
    const alle=[...nsAlle].filter(x=>x!==top&&drinNs(top,x)).sort();
    const zahl=new Map();basisZaehl(alle,zahl);
    const vor=new Set((LADEN&&LADEN.length?LADEN:VIEW.geladen)||[]),cbs=new Map();   // Vorauswahl: die letzte Wahl
    const box=h("div",{class:"gstart"});["pointerdown","dblclick","wheel","click"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));
    const anwenden=wahl=>{LADEN=wahl;START_ERST=false;if(wahl)VIEW.geladen=wahl;speichereAnsicht();START_OFFEN=false;SEL=null;render();
      requestAnimationFrame(()=>passeEin([...VIS],1,0.2));};
    box.append(h("h3",{},"Womit starten?"),
      h("div",{class:"gstart-t"},"Das Board startet leer. Wähle die Domänen, die geladen werden — der Rest bleibt im Code unberührt. Was du anlegst, wird sofort Code und bleibt sichtbar."),
      h("div",{class:"gstart-k"},h("button",{class:"act go",onclick:()=>anwenden([])},"◻ Leer starten"),h("button",{class:"act",onclick:()=>anwenden(null)},"Alles laden")));
    const liste=h("div",{class:"gstart-l"});
    const gewaehlt=()=>[...cbs.entries()].filter(([,c])=>c.checked&&!c.disabled).map(([x])=>x);
    const knopf=h("button",{class:"act go",onclick:()=>anwenden(gewaehlt())},"Laden ("+vor.size+")");
    alle.forEach(ns=>{const tiefe=ns.split(".").length-(top?top.split(".").length:0)-1;const cb=h("input",{type:"checkbox"});cb.checked=vor.has(ns);cbs.set(ns,cb);
      cb.onchange=()=>{cbs.forEach((c,x)=>{if(x!==ns&&drinNs(ns,x)){c.checked=false;c.disabled=cb.checked;}});knopf.textContent="Laden ("+gewaehlt().length+")";};
      liste.append(h("label",{style:"padding-left:"+(tiefe*18)+"px"},cb,h("span",{class:"gstart-n"},letztesSeg(ns)),h("span",{class:"gstart-z"},(zahl.get(ns)||0)+" Elemente")));});
    cbs.forEach((c,ns)=>{if(c.checked)cbs.forEach((d,x)=>{if(x!==ns&&drinNs(ns,x)){d.checked=false;d.disabled=true;}});});
    box.append(liste,h("div",{class:"gstart-k"},knopf,!START_ERST?h("button",{class:"act",onclick:()=>{START_OFFEN=false;render();}},"Abbrechen"):null));
    canvas.append(box);}
  let START_ERST=false;
  // Code-Elemente je Namespace (rekursiv) für die Anzeige im Dialog.
  function basisZaehl(alle,zahl){graphNodes().forEach(n=>{if(["handle","fn","rumpf","state","decider","applier","transition"].includes(n.kind)||!ausCodeVon(n))return;
      const x=nsVon(n),d=domKey(n);alle.forEach(m=>{if((x&&drinNs(m,x))||(d!==OHNE_DOM&&drinNs(m,d)))zahl.set(m,(zahl.get(m)||0)+1);});});}

  // ══ VERBINDEN-MODUS (Hybrid, docs/konzept-werkbank.md §8.5 + A.3 „⊕-Modus“): Port im Panel anklicken → alle PASSENDEN
  //   Knoten leuchten auf dem Graphen (Ablauf-Ansicht, Rest abgeblendet) → Karte anklicken = verbinden, ✓-Karte = lösen.
  //   Einfache Ports enden nach einem Klick, Listen-Ports bleiben offen bis Esc/Fertig. Was passt, sagt der Server (vbKandidaten).
  let VB=null;   // {key, label, einzel, filter}
  function vbQuelle(){const i=canvas&&canvas.querySelector(".ginsp");return VB&&i?[...i.querySelectorAll(".slot")].find(x=>x.__key===VB.key)||null:null;}
  // Kandidaten: typgleich UND von der Grammatik erlaubt → K; typgleich, aber verboten → G (gesperrt, mit Regel) — das Urteil fällt der Server
  //   (POST /api/werkbank/passt, Anschluss-Regel + DomainEditor.Grammatik). Mitgefragt werden die Nachrichten des Katalogs (nicht als Karte
  //   auf dem Board). Eine schon gezeichnete Verbindung bleibt lösbar, auch wenn die Grammatik sie heute sperrte (das weiß nur die Fläche).
  const portInfo=x=>{const o={};for(const [k,v] of Object.entries(x.__slot||{}))if(v==null||typeof v!=="object")o[k]=v;return o;};
  async function vbKandidaten(q,kat){const by=new Map();Object.values(SLOTS).forEach(x=>{if(x&&knotenIdVon(x))by.set(x.__key,x);});kat.forEach(f=>by.set(f.__key,f));
    const r=await fetch("/api/werkbank/passt",{method:"POST",headers:{"content-type":"application/json"},
      body:JSON.stringify({port:{key:q.__key,info:portInfo(q)},kandidaten:[...by.values()].map(x=>({key:x.__key,info:portInfo(x)}))})});
    const a=await r.json(),K=new Map(),G=new Map(),katOk=new Set(),dazu=(m,id,v)=>{if(!m.has(id))m.set(id,[]);m.get(id).push(v);};
    a.passt.forEach(k=>{const x=by.get(k);if(!x)return;if(x.__kat)katOk.add(k);else dazu(K,knotenIdVon(x),x);});
    a.gesperrt.forEach(g=>{const x=by.get(g.key);if(!x||x.__kat)return;if(vbPartner(q,x))dazu(K,knotenIdVon(x),x);else dazu(G,knotenIdVon(x),{x,v:{regel:g.regel,text:g.text}});});
    return {K,G,katOk};}
  // Verbunden? Eine gezeichnete Kante zwischen dem Kandidaten-Port und einem Port DIESES Knotens mit demselben Typ. Anker-Zeilen (Decider am
  //   Aggregat, Regel am Prozess) tragen keine Beschreibung: hängt der Kandidat an einer, ist er verbunden (gelöst wird über den offenen Port).
  //   Genau dieser Port (gleicher Schlüssel) — nur „offene" Sammel-Ports (…:open, z. B. „+ Ausgang") zählen knotenweit.
  const istOffenerPort=key=>/:open\b|open(evt|trg)?$/.test(key||"");
  function vbPartner(q,x){const offen=istOffenerPort(q.__key);
    for(const [k1,k2] of KANTEN){const o=k1===x.__key?k2:(k2===x.__key?k1:null);if(!o)continue;
      if(o===q.__key)return SLOTS[o]||q;
      const e=SLOTS[o];if(!offen||!e||knotenIdVon(e)!==SEL)continue;
      if(!e.__slot)return q;   // eine Anker-Zeile dieser Karte (z. B. Decider am Aggregat): verbunden — gelöst wird über den offenen Port
      if(e.__slot.type===q.__slot.type&&e.__slot.dir===q.__slot.dir&&e.__slot.handleIdx===q.__slot.handleIdx)return e;}
    // Umgekehrt: der Kandidat bietet nur seinen offenen Sammel-Port an (z. B. Akteur „+ bietet an ⊕"), die Kante hängt an einer
    //   seiner Zeilen — dann zählt jede Kante zwischen q und einem gleichartigen Port DIESER Karte (Lösen geht über den Namen).
    if(istOffenerPort(x.__key)){const xid=knotenIdVon(x);
      for(const [k1,k2] of KANTEN){const o=k1===q.__key?k2:(k2===q.__key?k1:null);if(!o)continue;
        const e=SLOTS[o];if(e&&e.__slot&&knotenIdVon(e)===xid&&e.__slot.type===x.__slot.type&&e.__slot.dir===x.__slot.dir)return q;}}
    return null;}
  // Lesbarer Name eines Ports: Beschriftung + Werte der Eingabefelder seiner Zeile (z. B. der Name einer Store-Funktion).
  const slotLabel=x=>{const r=x.closest(".slotrow,.gtopfield,.frow");if(!r)return x.__slot.type;
    const werte=[...r.querySelectorAll("input,select")].map(i=>i.value).filter(Boolean);
    const text=(r.textContent||"").replace(/[✕▶◀▲]/g," ");
    return (werte.join(" ")+" "+text).replace(/\s+/g," ").trim().slice(0,60)||x.__slot.type;};
  // Hält der Port EINEN Wert (Decider→Command, Applier→Event …)? Dann endet der Modus nach einem Klick.
  function istEinzel(I){const t=I.type,d=I.dir;
    if(t==="cmd")return d==="in";
    if(t==="prozess"||t==="decAgg"||t==="appAgg"||t==="projref"||t==="readmodel"||t==="state"||t==="prompt")return d==="out";
    if(t==="code"||t==="ftype")return d==="in";
    if(t==="evtUse")return d==="in"&&(!!I.app||!!I.trigger||typeof I.handleIdx==="number"||typeof I.wennIdx==="number"||typeof I.aufIdx==="number"||(!!I.flk&&I.role!=="warte"));
    if(t==="sagaCmd")return d==="out"&&(!!I.trans||!!I.flk);
    if(t==="rufe")return d==="out";
    return false;}
  // ⊕ anklicken = Modus an (derselbe ⊕ nochmal = aus). Beenden auch mit Esc oder Klick ins Leere.
  function vbStart(x){if(!x.__key)return;if(VB&&VB.key===x.__key){vbEnde();return;}
    VB={key:x.__key,label:slotLabel(x),einzel:istEinzel(x.__slot)};vbZeige(true);}
  function vbEnde(){VB=null;if(!world)return;world.classList.remove("vbmodus");if(canvas)canvas.classList.remove("vbaktiv");
    world.querySelectorAll(".vb-kand,.vb-verb,.vb-quelle,.vb-gesperrt").forEach(e=>e.classList.remove("vb-kand","vb-verb","vb-quelle","vb-gesperrt"));
    canvas.querySelectorAll(".gvbwahl,.gvbkat").forEach(e=>e.remove());canvas.querySelectorAll(".ginsp .slot.vb-aktiv").forEach(e=>e.classList.remove("vb-aktiv"));}
  var VB_NR=0;
  async function vbZeige(einpassen){if(!VB||!world||!canvas)return;let q=vbQuelle();if(!q){vbEnde();return;}
    const nr=++VB_NR,vb=VB,kat=vbKatalogKandidaten(q);let a;
    try{a=await vbKandidaten(q,kat);}catch(e){deFlash("⚠ Werkbank nicht erreichbar — keine Kandidaten",false);return;}
    if(nr!==VB_NR||VB!==vb||!world||!canvas)return;   // inzwischen ein anderer Port, der Modus ist aus oder neu gezeichnet
    q=vbQuelle();if(!q){vbEnde();return;}
    const K=a.K;VB.K=K;VB.G=a.G;
    world.classList.add("vbmodus");canvas.classList.add("vbaktiv");q.classList.add("vb-aktiv");
    // Aktiven Port im (verkleinerten) Panel sichtbar halten.
    const insp=q.closest(".ginsp");if(insp){const d=q.getBoundingClientRect().top-insp.getBoundingClientRect().top;if(d<0||d>insp.clientHeight-30)insp.scrollTop+=d-40;}
    world.querySelectorAll(".gnode2").forEach(el=>{const id=el.dataset.id,xs=K.get(id);
      const passt=!!xs;
      el.classList.toggle("vb-kand",passt);el.classList.toggle("vb-verb",passt&&xs.some(x=>vbPartner(q,x)));el.classList.toggle("vb-quelle",id===SEL);
      const gs=!passt&&VB.G&&VB.G.get(id);el.classList.toggle("vb-gesperrt",!!gs);el.title=gs?"✕ "+gs[0].v.text:"";});
    vbKatalog(q,kat.filter(f=>a.katOk.has(f.__key)));
    if(!K.size&&!VB.KAT)deFlash("Keine passenden Knoten für diesen Anschluss"+(VB.G&&VB.G.size?" — "+VB.G.size+" gesperrt: "+[...VB.G.values()][0][0].v.text:"."),false);
    if(einpassen){const cr=canvas.getBoundingClientRect(),imBild=[...K.keys()].some(id=>{const el=world.querySelector('[data-id="'+id+'"]');if(!el)return false;
      const r=el.getBoundingClientRect();return r.right>cr.left&&r.left<cr.right&&r.bottom>cr.top&&r.top<cr.bottom;});
      if(!imBild)vbEinpassen([...K.keys()].filter(id=>VIS.has(id)));}}
  // ── KATALOG im Verbinden-Modus: Nachrichten, die zum Port passen, aber NICHT als Karte auf dem Board stehen (andere Domäne nicht
  //   geladen, eingeklappt) — anklicken = verbinden (ihre Domäne wird dazugeladen). Dazu „＋ neu": eine neue Nachricht der passenden
  //   Sorte als EIGENE Karte anlegen und sofort verbinden (Name/Felder dann an ihrer Karte). Gilt für jeden Nachrichten-Port
  //   (Decider-Ausgang, Funktions-Auftrag/-Ergebnis, Prozess-Regel, Handle …) — welcher Port welche Sorte nimmt: wie die Karten-Ports.
  // Welche Record-Arten an einen Port passen KÖNNEN (Port-Typ|Richtung der Gegenseite) — aus der Grammatik (Grammatik.EditorKatalogSorte).
  const vbSorte=key=>((GR()||{}).katalogSorte||{})[key];
  const VB_NEU={event:"NeuesEvent",rejection:"NeueAblehnung",command:"NeuerCommand",query:"NeueQuery",queryresponse:"NeueAntwort",auftrag:"NeuerAuftrag",valueobject:"NeuerWert"};
  // Die Nachrichten, die zum Port passen KÖNNTEN (Sorte des Ports) — ob sie passen, sagt der Server mit den übrigen Kandidaten.
  function vbKatalogKandidaten(q){const S=q.__slot,gegen=S.dir==="out"?"in":"out",kinds=vbSorte(S.type+"|"+gegen);if(!kinds)return [];
    return MODEL.records.filter(r=>kinds.includes(r.kind)).map(r=>({__key:"kat:"+r.name,__kat:r,__slot:{type:S.type,dir:gegen,rec:r.name}}));}
  function vbKatalog(q,erlaubt){canvas.querySelectorAll(".gvbkat").forEach(e=>e.remove());VB.KAT=null;
    const S=q.__slot,gegen=S.dir==="out"?"in":"out",kinds=vbSorte(S.type+"|"+gegen);if(!kinds)return;
    const fake=r=>({__slot:{type:S.type,dir:gegen,rec:r.name}});
    const liste=erlaubt.map(f=>f.__kat).filter(r=>!(VB.K&&VB.K.has("rec:"+r.name))).map(r=>({r,f:fake(r)}))
      .sort((a,b)=>a.r.name.localeCompare(b.r.name));
    VB.KAT=liste;
    const verbinde=(r,neu)=>{const q2=vbQuelle()||q;imModus([SEL,"rec:"+r.name],()=>applyLink(q2,fake(r)));
      const n=NODEBY.get("rec:"+r.name)||{kind:r.kind,ref:r,id:"rec:"+r.name};const ns=r.namespace;
      if(LADEN&&ns&&!LADEN.some(g=>drinNs(g,ns))){LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;speichereAnsicht();}
      if(neu){vbEnde();render();setTimeout(()=>zeigeKnoten("rec:"+r.name),0);deFlash("＋ "+r.name+" angelegt und verbunden — Name und Felder an seiner Karte",true);return;}
      if(VB&&VB.einzel)vbEnde();render();if(VB)vbZeige(false);};
    const neu=()=>{const k=kinds[0],sel=SEL&&NODEBY.get(SEL),ns=(sel&&nsVon(sel))||defaultNsGlobal();
      let basis=VB_NEU[k]||"Neu";if(S.fk){const f=MODEL.funktionen.find(x=>x._id===S.fk);const st=((f&&f.name)||"").replace(/^I(?=[A-ZÄÖÜ])/,"");if(st)basis=st+(k==="auftrag"?"Auftrag":"Erledigt");}
      const r={name:uniq(basis),kind:k,namespace:ns,felder:k==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[]};
      MODEL.records.push(r);verbinde(r,true);};
    const liste2=h("div",{class:"gvbliste"});
    const fuelle=filter=>{liste2.textContent="";const f=(filter||"").toLowerCase();
      liste.filter(c=>!f||c.r.name.toLowerCase().includes(f)||(c.r.namespace||"").toLowerCase().includes(f)).slice(0,60).forEach(c=>{const n=NODEBY.get("rec:"+c.r.name);
        const aussen=n&&!geladen(n);
        liste2.append(h("button",{title:(c.r.namespace||"")+(aussen?" — Domäne nicht geladen, wird beim Verbinden dazugeladen":""),onclick:()=>verbinde(c.r,false)},
          h("span",{},c.r.name),h("span",{class:"gvbns"},(aussen?"↗ ":"")+letztesSeg(c.r.namespace||"?"))));});
      if(!liste2.childNodes.length)liste2.append(h("div",{class:"gvbns"},"— keine weitere passende Nachricht"));};
    const such=h("input",{placeholder:"suchen …",oninput:e=>fuelle(e.target.value)});
    const box=h("div",{class:"gvbkat"},h("div",{class:"gpick-t"},"Nachricht aus anderen Bereichen ("+liste.length+")"),
      h("button",{class:"gvbneu",title:"Eine neue "+(kindLabel(kinds[0])||kinds[0])+" als eigene Karte anlegen und verbinden",onclick:neu},"＋ neu: "+(kindLabel(kinds[0])||kinds[0])),
      such,liste2);
    fuelle("");["pointerdown","click","wheel"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));canvas.append(box);}
  // Einpassen: die dem gewählten Knoten NÄCHSTEN Kandidaten — so viele, wie in die Ablauf-Ansicht (Zoom ≥ 0,4) passen.
  function vbEinpassen(ids){if(!ids.length||!canvas||!world)return;
    const box=id=>{const n=NODEBY.get(id),el=world.querySelector('[data-id="'+id+'"]');if(!n||!el)return null;const p=P(n);
      return {x:p.x||0,y:p.y||0,w:el.offsetWidth,h:el.offsetHeight};};
    const q=SEL&&box(SEL),cr=canvas.getBoundingClientRect(),pad=60,MIN=0.4;
    const mitte=b=>({x:b.x+b.w/2,y:b.y+b.h/2}),mq=q?mitte(q):null;
    const liste=ids.map(id=>({id,b:box(id)})).filter(e=>e.b)
      .sort((a,b)=>{if(!mq)return 0;const A=mitte(a.b),B=mitte(b.b);return Math.hypot(A.x-mq.x,A.y-mq.y)-Math.hypot(B.x-mq.x,B.y-mq.y);});
    let x1=q?q.x:1e9,y1=q?q.y:1e9,x2=q?q.x+q.w:-1e9,y2=q?q.y+q.h:-1e9;const wahl=q?[SEL]:[];
    for(const e of liste){const nx1=Math.min(x1,e.b.x),ny1=Math.min(y1,e.b.y),nx2=Math.max(x2,e.b.x+e.b.w),ny2=Math.max(y2,e.b.y+e.b.h);
      const sk=Math.min((cr.width-2*pad)/Math.max(1,nx2-nx1),(cr.height-2*pad)/Math.max(1,ny2-ny1));
      if(sk<MIN&&wahl.length>(q?1:0))break;x1=nx1;y1=ny1;x2=nx2;y2=ny2;wahl.push(e.id);}
    passeEin(wahl,0.74,MIN);}
  function vbKlick(id){const q=vbQuelle();if(!q){vbEnde();return;}const xs=(VB.K&&VB.K.get(id))||[];
    // Typgleich, aber von der Grammatik verboten → nicht verbinden, die Regel nennen (Modus bleibt).
    if(!xs.length&&VB.G&&VB.G.get(id)){const v=VB.G.get(id)[0].v;deFlash("✕ "+v.text,false);const o=document.getElementById("de-out");
      if(o){o.innerHTML="";o.append(h("div",{class:"find error"},"["+v.regel+"] "+v.text));}return;}
    if(!xs.length){vbEnde();waehle(id);return;}   // nicht passend → Modus aus, diese Karte auswählen
    if(xs.length===1){vbSchalte(q,xs[0]);return;}
    // Mehrere passende Ports an EINER Karte (z. B. Store mit mehreren Write-Fns) → kleine Auswahl an der Karte.
    canvas.querySelectorAll(".gvbwahl").forEach(e=>e.remove());
    const el=world.querySelector('[data-id="'+id+'"]'),cr=canvas.getBoundingClientRect(),r=el.getBoundingClientRect();
    const box=h("div",{class:"gvbwahl"},h("div",{class:"gpick-t"},"Welcher Anschluss?"),
      ...xs.map(x=>h("button",{onclick:()=>{box.remove();vbSchalte(vbQuelle(),x);}},(vbPartner(q,x)?"✓ ":"")+slotLabel(x))));
    box.style.left=(r.right-cr.left+6)+"px";box.style.top=(r.top-cr.top)+"px";
    ["pointerdown","click"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));canvas.append(box);}
  function vbSchalte(q,x){if(!q||!x)return;const partner=vbPartner(q,x),ziel=knotenIdVon(x);
    // Bei Einzel-Ports hängt der BISHERIGE Partner mit dran (dessen Karte verliert ihr ✓ / ihre Kante).
    const alt=VB&&VB.einzel?[...(VB.K||new Map()).entries()].filter(([,xs])=>xs.some(y=>vbPartner(q,y))).map(([id])=>id):[];
    imModus([SEL,ziel,...alt],()=>{if(partner)loeseLink(partner,x);else applyLink(q,x);});
    vbZeige(false);}
  // Kanten aus dem MODEL zeichnen (Slot-Mitte → Slot-Mitte; eingeklappt → an den Kopf).
  //   Eingeklappt: an die passende KOPF-SEITE (Ausgang rechts, Eingang links, oben mittig) statt in die Kopfmitte.
  function anchor(el){if(el.offsetParent!==null)return slotCenter(el);const nd=el.closest(".gnode2");const hd=nd&&nd.querySelector(".ghead");
    if(!hd)return slotCenter(el);const d=sdir(el),y=nd.offsetTop+hd.offsetHeight/2;
    if(d>0)return {x:nd.offsetLeft+nd.offsetWidth,y};if(d<0)return {x:nd.offsetLeft,y};return {x:nd.offsetLeft+nd.offsetWidth/2,y:nd.offsetTop};}
  // Anschlussseite eines Slots: rechts (.o)=+1, links (.i)=-1, oben/sonst=0.
  const sdir=el=>el&&el.classList.contains("o")?1:(el&&el.classList.contains("i")?-1:0);
  const knotenIdVon=el=>{const g=el&&el.closest(".gnode2");return g?g.dataset.id:"";};
  let KANTEN=[];   // gezeichnete Port-Paare [schlüsselA, schlüsselB] — Grundlage für ✓/Lösen im Verbinden-Modus
  function drawEdges(){
    if(!svg)return;messeWelt();KANTEN=[];[...svg.querySelectorAll(".glink:not(.tmp)")].forEach(p=>p.remove());
    if(svgTop)[...svgTop.querySelectorAll(".glink")].forEach(p=>p.remove());
    // Pfade sammeln und erst am Ende einhängen: Lesen (Anker) und Schreiben (DOM) nicht verschränken → ein Layout statt Hunderte.
    const fSvg=document.createDocumentFragment(),fTop=document.createDocumentFragment();
    // DYNAMISCHE ANSCHLUSSSEITEN: Kanten werden erst GESAMMELT (KZ), dann gezeichnet. Ein Ende ist entweder eine eingeklappte
    //   Karte ({el}) — dann wählt sie je Kante die dem Partner ZUGEWANDTE Seite (oben/unten/links/rechts; horizontaler vs.
    //   vertikaler Abstand der Rechtecke) und mehrere Linien auf derselben Seite werden entlang der Seite verteilt (nach Lage des
    //   Partners sortiert → keine Kreuzungen, kein Sammelpunkt) — oder ein sichtbarer Slot ({pt,dir}, fester Punkt).
    const KZ=[];
    const mkE=(EA,EB,color,dash,cls,ds,tgt)=>{const k={EA,EB,color,dash,cls,ds,tgt,dataset:{}};KZ.push(k);return k;};
    const mk=(A,B,da,db,color,dash,cls,ds,tgt)=>mkE({pt:A,dir:{x:da,y:0}},{pt:B,dir:{x:db,y:0}},color,dash,cls,ds,tgt);
    const ende=sl=>{if(sl.offsetParent!==null){const d=sdir(sl);return {pt:slotCenter(sl),dir:d?{x:d,y:0}:null};}
      const nd=sl.closest(".gnode2");return nd?{el:nd}:{pt:slotCenter(sl),dir:null};};
    const add=(k1,k2,color,dash)=>{const a=SLOTS[k1],b=SLOTS[k2];if(!a||!b)return;KANTEN.push([k1,k2]);
      const p=mkE(ende(a),ende(b),color,dash);p.dataset.a=knotenIdVon(a);p.dataset.b=knotenIdVon(b);};
    // Akteur → was er darf (rosa): vom Akteur-Band zur Nachricht.
    MODEL.akteure.forEach(a=>(a.darf||[]).forEach(nm=>add("akt:darf:"+a._id+":"+nm,"darf:in:"+nm,"#e07ab4")));
    // Vertrag (Akteur-Konzept §3): Event → Zusage → Commands (rosa) — die Kette läuft durch den Akteur draußen.
    vertragsZusagen().forEach(({a,r,i})=>{if(r.eingang)add("evt:out:"+r.eingang,"auf:in:"+a._id+":"+i,"#e07ab4");
      (r.ausgaenge||[]).forEach(c=>{if(c)add("auf:send:"+a._id+":"+i+":"+c,"cmd:in:"+c,"#e07ab4");});});
    // Client-Vertrag (docs/konzept-akteure.md §4): die bestehenden Verbindungen kennt der Verbinden-Modus (✓/lösen) über KANTEN —
    //   gezeichnet werden sie als BÜNDEL (unten); Einzelkanten nur aufgefächert (AUF, durch eine Handlung).
    MODEL.clients.forEach(c=>{
      (c.sendet||[]).concat(c.fragt||[]).forEach(nm=>{if(SLOTS["cl:darf:"+c._id+":"+nm]&&SLOTS["darf:in:"+nm])KANTEN.push(["cl:darf:"+c._id+":"+nm,"darf:in:"+nm]);});
      (c.kenntnis||[]).forEach(nm=>{if(SLOTS["cl:kenntnis:"+c._id+":"+nm]&&SLOTS["evt:out:"+nm])KANTEN.push(["evt:out:"+nm,"cl:kenntnis:"+c._id+":"+nm]);});
      (c.traegt||[]).forEach(t=>{const a=teilAkteur(c,t);if(a&&SLOTS["cl:traegt:"+c._id+":"+t]&&SLOTS["akt:traegt:"+a._id])KANTEN.push(["cl:traegt:"+c._id+":"+t,"akt:traegt:"+a._id]);});});
    MODEL.decider.forEach(d=>{
      if(d.command)add("cmd:out:"+d.command,"dec:cmdin:"+d._id,"#4a86d6");
      if(d.aggregat)add("dec:aggout:"+d._id,"agg:left:"+d.aggregat+":"+d._id,"#33b1a6",true);
      (d.ergibt||[]).forEach(o=>{if(o.event)add("dec:evtout:"+d._id+":"+o.event,"evt:in:"+o.event,"#4fb06a");});
    });
    MODEL.applier.forEach(p=>{
      if(p.event)add("evt:out:"+p.event,"app:evtin:"+p._id,"#4fb06a");
      if(p.aggregat)add("app:aggout:"+p._id,"agg:right:"+p.aggregat+":"+p._id,"#d1953f",true);
    });
    MODEL.states.forEach(s=>{if(s.aggregat)add("state:out:"+s._id,"agg:state:"+s.aggregat,"#e0b64d");});
    // Interne Aggregat-Kanten: welcher Decider (links) erzeugt das Event welches Appliers (rechts) — Linie IM Aggregat.
    MODEL.aggregate.forEach(a=>{
      const dl=MODEL.decider.filter(d=>d.aggregat===a.name), al=MODEL.applier.filter(p=>p.aggregat===a.name);
      dl.forEach(d=>(d.ergibt||[]).forEach(o=>al.filter(p=>p.event===o.event).forEach(p=>{
        const x=SLOTS["agg:left:"+a.name+":"+d._id], y=SLOTS["agg:right:"+a.name+":"+p._id];if(!x||!y)return;
        if(x.offsetParent===null||y.offsetParent===null)return;   // Aggregat eingeklappt: keine Linie im Kopf
        const l=mk(anchor(x),anchor(y),1,-1,"#7f8aa0",false,"internal",[d._id,p._id],svgTop);l.dataset.a="dec:"+d._id;l.dataset.b="app:"+p._id;})));
    });
    // Prozess-Hub: Auslöser-Event → Prozess.
    MODEL.sagas.forEach(s=>{if(s.triggerEvent)add("evt:out:"+s.triggerEvent,"saga:trigger:"+s.name,"#9d78d6");});
    // Transitionen: an Hub angesteckt (gestrichelt), Join/UndAlle rein, Sende/Kompensation raus, Konstruktor-Args intern.
    MODEL.transitions.forEach(t=>{
      if(t.prozess)add("tr:prozess:"+t._id,"hub:in:"+t.prozess+":"+t._id,"#9d78d6",true);
      (t.wenn||[]).forEach((e,j)=>{if(e)add("evt:out:"+e,"tr:in:"+t._id+":"+j,"#4fb06a");});
      (t.dann||[]).forEach((d,di)=>{
        if(d.rufe)add("tr:rufe:"+t._id+":"+di,"fk:in:"+d.rufe,"#c08a2e");
        if(d.sende)add("tr:sende:"+t._id+":"+di,"cmd:in:"+d.sende,"#4a86d6");
        if(d.kompensation)add("tr:komp:"+t._id+":"+di,"cmd:in:"+d.kompensation,"#cf6f68",true);
      });
    });
    // Katalog-Funktion: Auftrag (Record) → Funktion (gestrichelt), Funktion → ihre Ergebnis-Events.
    MODEL.funktionen.forEach(f=>{if(f.auftrag)add("auf:out:"+f.auftrag,"fk:auftrag:"+f._id,"#c08a2e",true);
      (f.ergebnisse||[]).forEach(e=>{if(e)add("fk:out:"+f._id+":"+e,"evt:in:"+e,"#4fb06a");});
      // ⚙ Lese-Fähigkeit (Parameter) → die Read-Fn, deren Interface sie ist.
      (f.faehigkeiten||[]).forEach((p,i)=>{const z=leseFnZu(p.typ);if(z)add("rcall:out:fk:"+f._id+":"+i,"rcall:in:"+z.store._id+":"+z.fn._id,"#c08a3e");});
      // 🧩 Dienst im Konstruktor → der Dienst.
      (f.konstruktor||[]).forEach((p,i)=>{const d=dienstByName(p.typ);if(d)add("dn:out:fk:"+f._id+":"+i,"dn:in:"+d._id,"#7d9b55",true);});});
    MODEL.dienste.forEach(q=>(q.implementierungen||[]).forEach((im,ii)=>(im.konstruktor||[]).forEach((p,i)=>{const d=dienstByName(p.typ);
      if(d&&d!==q)add("dn:out:dn:"+q._id+":"+ii+":"+i,"dn:in:"+d._id,"#7d9b55",true);})));
    // Verbindungen, die schon anders gezeichnet werden (Fluss-Kanten, Ablauf-Kette) — hier nur als Port-Paar für ✓/Lösen im Verbinden-Modus.
    const nurPaar=(k1,k2)=>{if(SLOTS[k1]&&SLOTS[k2])KANTEN.push([k1,k2]);};
    MODEL.fluesse.forEach(f=>(f.knoten||[]).forEach(z=>{
      if(z.typ&&z.art==="funktion")nurPaar("fs:rufe:"+z._id,"fk:in:"+z.typ);
      if(z.typ&&z.art==="command")nurPaar("fs:send:"+z._id,"cmd:in:"+z.typ);
      if(z.typ&&flQuelleArt(z))nurPaar("evt:out:"+z.typ,"fs:ein:"+z._id);
      if(z.art==="warte")flWarteAuf(z).forEach(e=>nurPaar("evt:out:"+e,"fs:ein:"+z._id));
      (z.eingaenge||[]).forEach(e=>{
        if(e.je){const j=flKnoten(f,e.je);if(j)nurPaar((e.sammle?"fs:jesa:":"fs:jeel:")+j._id,(e.sammle?"fs:jesain:":"fs:jeein:")+z._id);}
        (e.draehte||[]).forEach(d=>{const v=flKnoten(f,d.von);if(v)nurPaar(flAusKey(v,d),"fs:in:"+z._id);});});}));
    KETTE.filter(k=>k.art==="dann").forEach(k=>{
      const von=k.von.startsWith("fk:")?"kette:fk:"+k.von.slice(3)+":"+k.sg.name+":"+k.bei:k.von.startsWith("saga:")?"kette:saga:"+k.sg.name:null;
      if(von)nurPaar(von,k.d.rufe?"fk:in:"+k.d.rufe:"cmd:in:"+k.d.sende);});
    // ── Leseseite: ReadModel→Store, Projektion/Reader→Store-Scope, Event→Projektion, Query→Reader,
    //    Reader→Response, und CODE (📝/🤖) → Rumpf-Ports (gestrichelt). ──
    const CODE="#8a8f9c";
    MODEL.readModels.forEach(rm=>{if(rm.store)add("rm:out:"+rm._id,"sto:rm:"+rm.store,"#c9a24b");});
    MODEL.stores.forEach(st=>{
    });
    MODEL.projektionen.forEach(p=>{
      (p.handles||[]).forEach((hd,hi)=>{if(hd.event)add("evt:out:"+hd.event,"prj:in:"+p._id+":"+hi,"#4fb06a");
        // Handle → aufgerufene Write-Fn (Store-Scope ist damit sichtbar-abgeleitet).
        (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)add("wcall:out:"+p._id+":"+hi+":"+fid,"wcall:in:"+f.store._id+":"+fid,"#2f9d95");});
        // veröffentlichtes reaktives Event (gestrichelt/teal = Broker, verlierbar).
        (hd.publishes||[]).forEach(ev=>{if(ev)add("prj:pub:"+p._id+":"+hi+":"+ev,"evt:in:"+ev,"#2fd6b0",true);});});
    });
    MODEL.reader.forEach(r=>{
      // IReader<TProjection>: Reader → Projektion (der gemeinsame Store ist der Treffpunkt).
      if(r.projektion){const p=MODEL.projektionen.find(x=>x.name===r.projektion);if(p)add("rdr:proj:"+r._id,"prj:asproj:"+p._id,"#c08a3e",true);}
      (r.handles||[]).forEach((hd,hi)=>{if(hd.query)add("qry:out:"+hd.query,"rdr:qin:"+r._id+":"+hi,"#9678d6");
        // Handle → aufgerufene Read-Fn(s), auch über mehrere Stores.
        (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)add("rcall:out:"+r._id+":"+hi+":"+fid,"rcall:in:"+f.store._id+":"+fid,"#c08a3e");});
        (hd.responses||[]).forEach(resp=>{if(resp)add("rdr:rout:"+r._id+":"+hi+":"+resp,"qrsp:in:"+resp,"#9d78d6");});});
    });
    // Reaktion (emittierend): Event → Handle, Handle → OneOf-Command(s), 📝 → Controller-Rumpf.
    MODEL.reaktionen.forEach(r=>{
      (r.handles||[]).forEach((hd,hi)=>{
        if(hd.event)add("evt:out:"+hd.event,"rk:in:"+r._id+":"+hi,"#4fb06a");
        (hd.sends||[]).forEach(c=>{if(c)add("rk:send:"+r._id+":"+hi+":"+c,"cmd:in:"+c,"#4a86d6");});
        (hd.publishes||[]).forEach(ev=>{if(ev)add("rk:pub:"+r._id+":"+hi+":"+ev,"evt:in:"+ev,"#2fd6b0",true);});});
    });
    // ── Typ-Komposition: VO/Enum → Feld (welches Feld benutzt diesen Typ), gestrichelt. ──
    const voNamen=new Set(MODEL.records.filter(r=>r.kind==="valueobject").map(r=>r.name));
    const enNamen=new Set(MODEL.enums.map(e=>e.name));
    alleFelder().forEach(({owner,f})=>{const bt=innerTyp(f.typ);
      if(voNamen.has(bt))add("ftype:out:rec:"+bt,"ftype:in:"+owner+":"+f._id,"#49a996",true);
      else if(enNamen.has(bt))add("ftype:out:enum:"+bt,"ftype:in:"+owner+":"+f._id,"#8a8aa0",true);});
    // ── Zusammengezogene Kanten: Pfade DURCH eingeklappte Details (Command →[Decider]→ Event …) Kopf an Kopf. ──
    const ELS=new Map([...world.querySelectorAll(".gnode2")].map(e=>[e.dataset.id,e]));
    KONTRAKT.forEach(([u,v])=>{const eu=ELS.get(u),ev=ELS.get(v);if(!eu||!ev)return;
      const kv=(NODEBY.get(v)||{}).kind,col=kv==="event"?"#4fb06a":(kv==="command"?"#4a86d6":"#8a8f9c");
      const p=mkE({el:eu},{el:ev},col,false,"kontrakt");p.dataset.a=u;p.dataset.b=v;});
    // ── Ablauf-Kette: Start/ƒ → nächster Schritt (ƒ oder Command), beschriftet mit dem auslösenden Ergebnis; Klick öffnet den Schritt. ──
    KETTE.forEach(k=>{const eu=ELS.get(k.von),ev=ELS.get(k.nach);if(!eu||!ev)return;const komp=k.art==="komp";
      const l=mkE({el:eu},{el:ev},komp?"#cf6f68":"#c08a2e",komp,"kette");l.dataset.a=k.von;l.dataset.b=k.nach;
      l.label=k.label;l.titel=(k.bei?"bei "+k.bei+" — ":"")+"Klick: Schritt öffnen (Argumente, Zeitlimit)";l.klick=()=>waehle("tr:"+k.t._id);});
    // ── Pipelines (Fluss): je Draht Port → Knoten, links → rechts. ──
    flussKanten(ELS,mkE);
    // ── Hub-Kanten: Handle → Besitzer (Projektion/Reader/Reaktion/Pipeline), Store-Fn → Store — gestrichelt, wie Decider → Aggregat. ──
    NODEBY.forEach(n=>{if(n.kind!=="handle"&&n.kind!=="fn")return;const eu=ELS.get(n.id),eo=ELS.get(n.own.id);if(!eu||!eo)return;
      const l=mkE({el:eu},{el:eo},"#6f7a91",true,"hub");l.dataset.a=n.id;l.dataset.b=n.own.id;});
    // ── Zeichnen: Seiten wählen → je Karte+Seite verteilen → Kurve senkrecht zur Seite. ──
    const DIR={r:{x:1,y:0},l:{x:-1,y:0},o:{x:0,y:-1},u:{x:0,y:1}};
    const RC=new Map();
    const rect=el=>{let r=RC.get(el);if(!r){const hd=el.querySelector(".ghead");
      r={x1:el.offsetLeft,y1:el.offsetTop,x2:el.offsetLeft+el.offsetWidth,y2:el.offsetTop+el.offsetHeight,hy:el.offsetTop+(hd?hd.offsetHeight/2:12)};RC.set(el,r);}return r;};
    const box=E=>E.el?rect(E.el):{x1:E.pt.x,y1:E.pt.y,x2:E.pt.x,y2:E.pt.y};
    const seite=(R,Q)=>{const gx=Math.max(Q.x1-R.x2,R.x1-Q.x2),gy=Math.max(Q.y1-R.y2,R.y1-Q.y2);
      if(gx>=gy)return (Q.x1+Q.x2)>=(R.x1+R.x2)?"r":"l";return (Q.y1+Q.y2)>=(R.y1+R.y2)?"u":"o";};
    // ── Client-Leitungen: je Client × Ziel-Rahmen EIN Bündel (dick; durchgezogen = trägt eine Zusage/durabel, gestrichelt = nur
    //   Kenntnis/Fragen/Senden), beschriftet in Worten. Aufgefächert (AUF) zusätzlich die Einzelkanten Karte → Karte. Zoom ändert nichts.
    MODEL.clients.forEach(c=>{const ec=ELS.get("cl:"+c._id);if(!ec)return;const farbe="hsl("+domHue(CL_PRE+c.name)+" 60% 62%)";
      clientBuendel(c).forEach(b=>{const R=FRGEO.get(b.key);if(!R)return;
        const Q={x1:ec.offsetLeft,y1:ec.offsetTop,x2:ec.offsetLeft+ec.offsetWidth,y2:ec.offsetTop+ec.offsetHeight};
        const s=seite(R,Q),cy=Math.max(R.y1+40,Math.min(R.y2-20,(Q.y1+Q.y2)/2)),cx=Math.max(R.x1+40,Math.min(R.x2-40,(Q.x1+Q.x2)/2));
        const pt=s==="r"?{x:R.x2,y:cy}:s==="l"?{x:R.x1,y:cy}:s==="o"?{x:cx,y:R.y1}:{x:cx,y:R.y2};
        const k=mkE({el:ec},{pt,dir:DIR[s]},farbe,!b.durabel,"gbuendel");k.festA="l";k.label=buendelText(b);k.titel="🔌 "+clientAnzeige(c)+" → "+rahmenLabel(b.key)+"\n"+k.label
          +"\n"+(b.durabel?"durchgezogen: trägt eine Zusage — die Kette hängt durabel an diesem Client":"gestrichelt: nur Senden/Fragen/Kenntnis (verlierbar)")+"\nKlick: auffächern";
        k.dataset.a="cl:"+c._id;k.dataset.b=(b.ports.find(p=>p.node)||{}).node||"";k.klick=()=>auffaechern(c,{keys:[b.key]});});
      if(!AUF||AUF.client!==c._id)return;
      const FARBE={zusage:"#e07ab4",sendet:"#4a86d6",fragt:"#9678d6",kenntnis:"#4fb06a"};
      clientPorts(c).filter(p=>p.node&&aufPasst(AUF,p)).forEach(p=>{const ziel=ELS.get(vertreterId(p.node));if(!ziel)return;
        const k=p.ri==="kenntnis"?mkE({el:ziel},{el:ec},FARBE[p.ri],true,"gauf"):mkE({el:ec},{el:ziel},FARBE[p.ri],p.ri!=="zusage","gauf");
        if(p.ri==="kenntnis")k.festB="l";else k.festA="l";   // Clients stehen rechts: Leitungen treten links aus
        k.dataset.a="cl:"+c._id;k.dataset.b=vertreterId(p.node);});});
    const grp=new Map();
    const reg=(E,s,other,k,end)=>{const key=E.el.dataset.id+"|"+s;let g=grp.get(key);if(!g)grp.set(key,g=[]);g.push({k,end,other,el:E.el});};
    KZ.forEach(k=>{const RA=box(k.EA),RB=box(k.EB);
      if(k.EA.el){k.sa=k.festA||seite(RA,RB);reg(k.EA,k.sa,RB,k,"a");}
      if(k.EB.el){k.sb=k.festB||seite(RB,RA);reg(k.EB,k.sb,RA,k,"b");}});
    grp.forEach((g,key)=>{const s=key.slice(key.lastIndexOf("|")+1),R=rect(g[0].el),quer=s==="o"||s==="u",n=g.length;
      g.sort((p,q)=>quer?(p.other.x1+p.other.x2)-(q.other.x1+q.other.x2):(p.other.y1+p.other.y2)-(q.other.y1+q.other.y2));
      g.forEach((e,i)=>{const t=(i+0.5)/n;
        e.k["p"+e.end]=quer?{x:R.x1+(R.x2-R.x1)*(0.15+0.7*t),y:s==="o"?R.y1:R.y2}
                           :{x:s==="l"?R.x1:R.x2,y:n===1?R.hy:R.y1+6+(R.y2-R.y1-12)*t};});});
    KZ.forEach(k=>{const A=k.pa||k.EA.pt,B=k.pb||k.EB.pt;
      const va=k.sa?DIR[k.sa]:(k.EA.dir||{x:B.x>=A.x?1:-1,y:0}),vb=k.sb?DIR[k.sb]:(k.EB.dir||{x:A.x>=B.x?1:-1,y:0});
      const d=Math.min(400,Math.max(40,Math.hypot(B.x-A.x,B.y-A.y)*0.4));
      const p=document.createElementNS(SVGNS,"path");p.setAttribute("class","glink"+(k.cls?" "+k.cls:""));p.setAttribute("fill","none");
      p.setAttribute("stroke",k.color);p.setAttribute("stroke-width","2.2");if(k.dash)p.setAttribute("stroke-dasharray","5 4");
      if(k.ds){p.dataset.dec=k.ds[0];p.dataset.app=k.ds[1];}Object.assign(p.dataset,k.dataset);
      p.setAttribute("d","M"+A.x+","+A.y+" C"+(A.x+va.x*d)+","+(A.y+va.y*d)+" "+(B.x+vb.x*d)+","+(B.y+vb.y*d)+" "+B.x+","+B.y);
      ((k.tgt||svg)===svg?fSvg:fTop).append(p);
      // Client-Bündel: Titel (Hover), Klick = auffächern, Beschriftung in Worten an der Kurvenmitte (Bezier t=½).
      if(k.klick){const tt=document.createElementNS(SVGNS,"title");tt.textContent=k.titel||"";p.append(tt);
        p.addEventListener("pointerdown",e=>e.stopPropagation());p.addEventListener("click",e=>{e.stopPropagation();k.klick();});}
      if(k.label){const c1={x:A.x+va.x*d,y:A.y+va.y*d},c2={x:B.x+vb.x*d,y:B.y+vb.y*d};
        const mx=0.125*A.x+0.375*c1.x+0.375*c2.x+0.125*B.x,my=0.125*A.y+0.375*c1.y+0.375*c2.y+0.125*B.y;
        const t=document.createElementNS(SVGNS,"text");t.setAttribute("class","glink gbuendel-t");t.setAttribute("x",mx);t.setAttribute("y",my-8);
        t.setAttribute("text-anchor","middle");t.textContent=k.label;Object.assign(t.dataset,k.dataset);fSvg.append(t);}});
    svg.append(fSvg);if(svgTop)svgTop.append(fTop);
    wendeFokusAn();fokusChip();
    if(typeof SP!=="undefined")spurZeichne();
  }
  // Interne Linien hervorheben, wenn man über die zugehörige Decider-/Applier-Zeile fährt.
  function hilite(decId,appId,on){document.querySelectorAll("#de .glink.internal").forEach(p=>{
    if((decId&&p.dataset.dec===decId)||(appId&&p.dataset.app===appId))p.classList.toggle("hot",on);});}

  // Verschieben (Knoten) / Pannen (Fläche).
  //   Kopf nur antippen (ohne Ziehen) = auswählen (onClick) → Inspector + Slice-Fokus.
  function startMove(e,el,ref,onClick){e.preventDefault();el.classList.add("dragging");
    const sx=e.clientX,sy=e.clientY,ox=ref.x||0,oy=ref.y||0;let moved=false;
    const mv=ev=>{if(!moved&&Math.abs(ev.clientX-sx)+Math.abs(ev.clientY-sy)<4)return;moved=true;
      ref.x=Math.round((ox+(ev.clientX-sx)/PAN.s)/GRID)*GRID;
      ref.y=Math.round((oy+(ev.clientY-sy)/PAN.s)/GRID)*GRID;
      el.style.left=ref.x+"px";el.style.top=ref.y+"px";drawEdges();};
    const up=()=>{el.classList.remove("dragging");window.removeEventListener("pointermove",mv);window.removeEventListener("pointerup",up);
      if(!moved){if(onClick)onClick();}else{if(VIEW.kompakt)speichereKpos();autosave();drawMinimap();}};
    window.addEventListener("pointermove",mv);window.addEventListener("pointerup",up);}
  let PANNED=false;
  function startPan(e){e.preventDefault();canvas.classList.add("panning");bewegtAn();const sx=e.clientX,sy=e.clientY,ox=PAN.x,oy=PAN.y;PANNED=false;
    const mv=ev=>{if(Math.abs(ev.clientX-sx)+Math.abs(ev.clientY-sy)>3)PANNED=true;PAN.x=ox+(ev.clientX-sx);PAN.y=oy+(ev.clientY-sy);applyPan();};
    const up=()=>{canvas.classList.remove("panning");bewegtAus();window.removeEventListener("pointermove",mv);window.removeEventListener("pointerup",up);};
    window.addEventListener("pointermove",mv);window.addEventListener("pointerup",up);}

  // ══ ANSICHT (reine Darstellung — Modell, Scaffolder und Round-trip bleiben unberührt) ══════════════════════
  //   (1) Kompakt-Karten: Knoten standardmäßig eingeklappt (Kopf + Kurzfassung), bearbeitet wird im INSPECTOR.
  //   (2) Details eingeklappt: Decider/Applier/State/Code/LLM/Ablehnung/VO/Enum/Response liegen IN ihrem
  //       sichtbaren Besitzer (Chips + Inspector-Sektionen); Pfade durch sie werden als Kopf-Kanten zusammengezogen.
  //   (5) Slice-Fokus: Klick auf einen Knoten → sein vertikaler Schnitt bleibt hell, der Rest tritt zurück.
  // details: Decider/Applier/State/Code/LLM/Ablehnung/Typen sind IMMER eigene Knoten auf dem Graphen (nie eingeklappt).
  const KPOS_VERSION=8;   // hochzählen, wenn sich Kartengrößen ändern → gespeicherte Anordnung wird einmalig neu gepackt
  let VIEW={kompakt:true,details:true,aus:[],domNeu:[],heim:{},heimBlk:{},heimAkteur:{},akteurOrdnung:{}}, KPOS={k:{},d:{}};
  function ladeAnsicht(){if(ANSICHT.view)VIEW={...VIEW,...ANSICHT.view};
    VIEW.details=true;VIEW.kompakt=true;
    KPOS=ANSICHT.kpos||{};
    if(!KPOS.k||!KPOS.d||KPOS.v!==KPOS_VERSION)KPOS={k:{},d:{},v:KPOS_VERSION};}
  // Ausgeblendete Knotenarten (Auswahl-Menü „👁 Ausblenden"): pro Browser + Solution gespeichert (VIEW.aus).
  const istAus=k=>Array.isArray(VIEW.aus)&&VIEW.aus.includes(k);
  function setzeAus(k,aus){const a=new Set(VIEW.aus||[]);if(aus)a.add(k);else a.delete(k);VIEW.aus=[...a];speichereAnsicht();render();}
  function speichereAnsicht(){ANSICHT.view=VIEW;ansichtSpeichern();}
  function speichereKpos(){ANSICHT.kpos=KPOS;ansichtSpeichern();}
  // Position eines Knotens in der AKTUELLEN Ansicht. Voll = x/y im Modell (wandert ins Board); Kompakt = eigenes,
  //   nur browser-lokales Layout (KPOS) — die kleineren Karten brauchen eine dichtere Anordnung.
  //   Je Detail-Stufe ein eigenes Layout (k = Details eingeklappt, d = Details als Knoten) — Umschalten verliert nichts.
  function P(n){if(!VIEW.kompakt)return n.ref;
    const L=KPOS[VIEW.details?"d":"k"]||(KPOS[VIEW.details?"d":"k"]={});
    let p=L[n.id];if(!p){p=L[n.id]={};if(n.ref._kpos){p.x=n.ref._kpos.x;p.y=n.ref._kpos.y;}}
    if(n.ref._kpos)delete n.ref._kpos;return p;}
  let INSP=false, INSP_SCROLL=null;   // INSP: gerade wird eine Inspector-Kopie gebaut (keine Slot-Registrierung)

  let VIS=new Set(), VERTRETER=new Map(), DETAILS=new Map(), EINGEKLAPPT=new Set(), NODEBY=new Map(), KONTRAKT=[], KETTE=[], TIEFE=new Map();
  // Pipeline IN der Domäne: FLHEIM = Rahmen-Schlüssel (§fluss:id) → Domäne, in der der Fluss steht.
  let FLHEIM=new Map();
  let ADJ={out:new Map(),inn:new Map()};
  const vertreterId=id=>(VERTRETER.get(id)||[id])[0];
  function berechneSicht(){
    const alle=graphNodes();NODEBY=new Map(alle.map(n=>[n.id,n]));
    const push=(m,k,v)=>{let a=m.get(k);if(!a)m.set(k,a=[]);if(!a.includes(v))a.push(v);};
    ADJ={out:new Map(),inn:new Map()};
    kanten().forEach(([a,b])=>{if(a===b||!NODEBY.has(a)||!NODEBY.has(b))return;push(ADJ.out,a,b);push(ADJ.inn,b,a);});
    FLHEIM=new Map(MODEL.fluesse.map(f=>[FL_PRE+f._id,((SICHT().fluesse||{})["fl:"+f._id]||{}).heim||f.namespace||null]));
    // Vertreter (kompakt) = sichtbarer Besitzer, transitiv durch eingeklappte Besitzer (Decider → Command) — vom Server; ein Code-Block hat
    //   den Vertreter seiner Karte. Ohne Besitzer bleibt ein Detail selbst sichtbar (neu angelegt / unverdrahtet → nichts verschwindet).
    const vert=n=>{if(VIEW.details)return [n.id];if(n.kind==="rumpf")return vert(n.own);
      const k=SICHT().karten[n.id];return (k&&k.vertreter)||[n.id];};
    VIS=new Set();VERTRETER=new Map();DETAILS=new Map();EINGEKLAPPT=new Set();
    alle.forEach(n=>{const v=vert(n);VERTRETER.set(n.id,v);
      if(v.length===1&&v[0]===n.id){if(!HIDDEN.has(groupKeyOf(n))&&!istAus(n.kind)&&geladen(n))VIS.add(n.id);}
      else{EINGEKLAPPT.add(n.id);v.forEach(o=>push(DETAILS,o,n.id));}});
    // Ablauf als Kette: Kettenschritt-Regeln werden Kanten. Auftrag und Ergebnis-Events einer Funktion bleiben IMMER eigene Karten —
    //   die Funktion ist definiert wie ein Decider: ◀ Auftrag-Karte · ƒ (📝 + 🤖 darunter) · Ergebnis-Karten ▶, auf einer Zeile.
    KETTE=ketteAusSicht();const weg=new Set(KETTE.map(k=>"tr:"+k.t._id));
    weg.forEach(id=>{if(VIS.delete(id))EINGEKLAPPT.add(id);});
    // Tiefe in der Kette (Start = 0): Funktionen stehen danach in eigenen Spalten nebeneinander — erst … dann … von links nach rechts.
    TIEFE=new Map(Object.entries(SICHT().karten).filter(([,k])=>k.tiefe!=null).map(([id,k])=>[id,k.tiefe]));
    // Zusammengezogene Kanten (kompakt): sichtbar →(eingeklappt)*→ sichtbar, nicht ins Aggregat — vom Server, hier nur die sichtbaren.
    KONTRAKT=VIEW.details?[]:(SICHT().kontrakt||[]).filter(([u,v])=>VIS.has(u)&&VIS.has(v));
  }

  // ══ EIN FOKUS (docs/konzept-werkbank.md §8.5, Werkbank P6.4): ersetzt Slice, Ketten-Fokus, ◎ und ⤢ — ohne Vorgang die statische Kette der
  //   gewählten Karte (bzw. eine markierte Menge: Rahmen, Spalte, Vertrag, aufgefächerter Client), mit Vorgang die echte Spur. Angezeigt und
  //   gelöst wird er an EINER Stelle (Ansichts-Leiste: ◎ Fokus · ⤢ · ✕). FOKUS = {art: kette|menge|vorgang, titel, ids?}; FOCUS = die Karten.
  //   Die statische Kette (Nachrichtenfluss vorwärts/rückwärts bzw. der gerichtete Schnitt einer Leseseite, eines Aggregats, Akteurs oder
  //   Clients) rechnet der Server (GET /api/werkbank/kette) — je Code-Stand und Ansicht einmal; bis sie da ist, steht die Karte allein im Fokus.
  //   Code-Blöcke (📝/🤖) kennt nur der Editor: ihre Kette ist die ihrer Karte, die Rümpfe der beteiligten Logik-Elemente hängen mit dran.
  let SEL=null, FOCUS=null, FOKUS=null;
  var KETTEN=new Map(),KETTEN_STAND=null;
  const RUMPF_ARTEN=new Set(["handle","decider","applier","fn","funktion","flussknoten","transition","client"]);
  // Die Rumpf-Kacheln der Logik-Elemente in der Kette gehören mit in den Fokus.
  function mitRuempfen(ids,id){const res=new Set(ids);res.add(id);
    NODEBY.forEach(n=>{if(n.kind==="rumpf"&&ids.has(n.own.id)&&RUMPF_ARTEN.has(n.own.kind))res.add(n.id);});
    return res;}
  function ketteFuer(id){const n=NODEBY.get(id),t=n&&n.kind==="rumpf"?codeTraeger(n):null,karte=t?t.id:id;
    if(KETTEN_STAND!==SICHT()){KETTEN_STAND=SICHT();KETTEN=new Map();}
    const key=karte+"|"+(!!VIEW.details),c=KETTEN.get(key);
    if(c instanceof Set)return mitRuempfen(c,id);
    if(!c)KETTEN.set(key,fetch("/api/werkbank/kette?karte="+encodeURIComponent(karte)+"&details="+(!!VIEW.details)).then(r=>r.json())
      .then(a=>{if(KETTEN.get(key) instanceof Set)return;KETTEN.set(key,new Set(a.ids));if(FOKUS&&FOKUS.art==="kette"&&SEL===id){fokusNeu();wendeFokusAn();fokusChip();}})
      .catch(()=>KETTEN.delete(key)));
    return new Set([id]);}
  function fokusNeu(){
    if(typeof SP!=="undefined"&&SP.vorgang){FOKUS={art:"vorgang",titel:spTitel()};FOCUS=spurMenge();return;}
    if(FOKUS&&FOKUS.art==="menge"){FOCUS=FOKUS.ids;return;}
    if(SEL&&NODEBY.has(SEL)){const n=NODEBY.get(SEL);FOKUS={art:"kette",titel:"Kette · "+(n.name||NODELABEL[n.kind]||n.kind)};FOCUS=ketteFuer(SEL);return;}
    if(FOKUS&&FOKUS.art==="kette")FOKUS=null;FOCUS=null;}
  function fokussiere(art,ids,titel){FOKUS=art?{art,ids,titel}:null;fokusNeu();wendeFokusAn();fokusChip();}
  // Alles lösen: Auswahl, Vorgang, markierte Menge.
  function fokusLoesen(){if(typeof SP!=="undefined"&&SP.gewaehlt){spWaehle(null);}FOKUS=null;if(SEL)waehle(null);else{fokusNeu();wendeFokusAn();fokusChip();}}
  function fokusChip(){const el=document.getElementById("gfokus");if(!el)return;el.textContent="";
    if(!FOKUS||!FOCUS){el.style.display="none";return;}el.style.display="";
    const sym=FOKUS.art==="vorgang"?"⟿":FOKUS.art==="menge"?"▣":"◎";
    el.append(h("span",{class:"gf-t",title:FOKUS.art==="vorgang"?"Die echte Spur des gewählten Vorgangs":FOKUS.art==="kette"?"Die statische Kette der gewählten Karte":"Markierte Menge"},sym+" "+FOKUS.titel),
      h("span",{class:"gf-z"},String([...FOCUS].filter(i=>VIS.has(vertreterId(i))).length)),
      h("button",{title:"Fokus einpassen",onclick:()=>passeEin([...FOCUS].map(vertreterId).filter(i=>VIS.has(i)),1)},"⤢"),
      h("button",{title:"Fokus lösen (Esc)",onclick:fokusLoesen},"✕"));}
  function waehle(id){
    if(VB){if(id&&id!==SEL){vbKlick(id);return;}if(!id){vbEnde();return;}}
    if(AUF&&id!=="cl:"+AUF.client){AUF=null;if(world)drawEdges();}
    if(typeof SP!=="undefined"&&id)SP.schritt=null;
    SEL=id||null;if(FOKUS&&FOKUS.art==="menge")FOKUS=null;
    fokusNeu();wendeFokusAn();fokusChip();zeigeInspector();}
  function wendeFokusAn(){if(!world)return;const an=!!FOCUS;world.classList.toggle("fokus",an);
    const selV=SEL?(VERTRETER.get(SEL)||[SEL]):[];
    world.querySelectorAll(".gnode2").forEach(el=>{const id=el.dataset.id;el.classList.toggle("inslice",an&&FOCUS.has(id));el.classList.toggle("sel",selV.includes(id));});
    if(!an)return;
    world.querySelectorAll(".glink").forEach(p=>p.classList.toggle("inslice",FOCUS.has(p.dataset.a)&&FOCUS.has(p.dataset.b)));}
  document.addEventListener("keydown",e=>{if(e.key!=="Escape")return;if(VB){vbEnde();return;}if(SEL){waehle(null);return;}
    if(typeof SP!=="undefined"&&SP.schritt){SP.schritt=null;zeigeInspector();return;}if(FOKUS)fokusLoesen();});

  // ── (1) INSPECTOR: die Detail-Sicht GENAU EINES Knotens (sein Formular) + Nachbarn zum Springen.
  function zeigeInspector(){if(!canvas)return;const alt=canvas.querySelector(".ginsp");if(alt)alt.remove();
    if(!SEL){if(typeof SP!=="undefined"&&SP.schritt)spSchrittPanel();return;}
    const n=NODEBY.get(SEL);if(!n){SEL=null;return;}
    const p=h("div",{class:"ginsp"});p.dataset.sel=SEL;
    ["pointerdown","dblclick","wheel","click"].forEach(ev=>p.addEventListener(ev,e=>e.stopPropagation()));
    const nm=n.name||NODELABEL[n.kind]||n.kind;
    p.append(h("div",{class:"gi-h"},h("span",{class:"gi-k kc-"+n.kind},NODELABEL[n.kind]||n.kind),h("span",{class:"gi-n",title:nm},nm),
      h("button",{title:"Schließen (Esc)",onclick:()=>waehle(null)},"✕")));
    const sektion=(m,titel)=>{const sec=h("div",{class:"gi-sec"});
      if(titel)sec.append(h("div",{class:"gi-st kc-"+m.kind},titel));
      const b=h("div",{class:"gbody"});INSP=true;try{fuelleKoerper(b,m);}finally{INSP=false;}sec.append(b);return sec;};
    // NUR der gewählte Knoten — Decider, Ablehnung, Code, LLM … sind eigene Knoten auf dem Graphen mit eigener Detail-Sicht.
    if(n.kind!=="host")p.append(h("div",{class:"gi-hint"},"⊕ anklicken → passende Knoten leuchten → anklicken = verbinden / lösen · Esc oder ⊕ nochmal = fertig"));
    {const dg=diagFuer(n.id);if(dg.length)p.append(h("div",{class:"gi-diag"},...dg.map(d=>h("div",{class:"find "+(d.art==="warnung"?"warning":"error")},d.text))));}
    p.append(sektion(n,null));
    // Nachbarn zum Springen (Klick wählt den Nachbarn und zeigt DESSEN Detail-Sicht).
    const eigen=new Set([n.id]),rein=new Set(),raus=new Set();
    const sammle=(ids,ziel)=>(ids||[]).forEach(y=>(VERTRETER.get(y)||[y]).forEach(v=>{if(!eigen.has(v))ziel.add(v);}));
    eigen.forEach(x=>{sammle(ADJ.inn.get(x),rein);sammle(ADJ.out.get(x),raus);});
    const rel=h("div",{class:"gi-rel"});
    const liste=(titel,set)=>{if(!set.size)return;rel.append(h("h5",{},titel));
      // Nachbar in einer nicht geladenen Domäne: benannt („↗ außerhalb"), Klick lädt die Domäne dazu und springt hin.
      [...set].map(id=>NODEBY.get(id)).filter(Boolean).forEach(m=>{const aussen=!geladen(m),ns=nsVon(m);
        rel.append(h("a",{title:(NODELABEL[m.kind]||m.kind)+(aussen?" — Domäne "+(ns||"?")+" nicht geladen: Klick lädt sie dazu":""),
          onclick:()=>{if(aussen&&ns&&LADEN){if(!LADEN.some(g=>drinNs(g,ns)))LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;speichereAnsicht();render();}
            const n2=NODEBY.get(m.id);if(n2){waehle(n2.id);centerOn(n2);pulseNode(n2);}}},
          h("i",{class:"kc-"+m.kind,style:"display:inline-block;width:7px;height:7px;border-radius:50%;margin-right:5px"}),m.name||NODELABEL[m.kind],
          aussen?h("span",{class:"gp-mark",style:"margin-left:6px"},"↗ außerhalb · "+letztesSeg(ns||"?")):null));});};
    liste("◀ Eingang",rein);liste("Ausgang ▶",raus);if(rel.childNodes.length)p.append(rel);
    canvas.append(p);
    if(INSP_SCROLL&&INSP_SCROLL.id===SEL)p.scrollTop=INSP_SCROLL.top;INSP_SCROLL=null;}

  // Kurzfassung auf der eingeklappten Karte: das Wesentliche in einer Zeile + Chips der eingeklappten Details.
  const kurz=xs=>{xs=[...new Set((xs||[]).filter(Boolean))];return xs.slice(0,2).join(", ")+(xs.length>2?" +"+(xs.length-2):"");};
  // 🔌 Die ANSCHLUSSLEISTE eines Clients (= sein Vertrag), immer lesbar auf der Karte: je Richtung, darin je Akteur, jede Nachricht beim
  //   Namen. Klick auf eine Nachricht fächert genau diese Leitung auf (Einzelkante); der Rest bleibt gebündelt.
  function anschlussleiste(n){const c=n.ref,P=clientPorts(c),box=h("div",{class:"gsum gleiste",onclick:()=>waehle(n.id)});
    const name=(p,txt)=>{const b=h("span",{class:"lp"+(p.node?"":" lp-fehlt")+(AUF&&AUF.client===c._id&&AUF.msg===p.msg?" an":""),
        title:p.node?"Klick: diese Leitung auffächern":"⚠ nicht im Modell"},txt||p.msg);
      b.onclick=e=>{e.stopPropagation();if(p.node)auffaechern(c,{msg:p.msg});};return b;};
    const zeile=(kopf,teile,kl)=>{const z=h("div",{class:"lz"+(kl?" "+kl:"")},h("span",{class:"lk"},kopf));
      teile.forEach((t,i)=>{if(i)z.append(", ");z.append(t);});box.append(z);};
    const sym=a=>(ART_SYM[(MODEL.akteure.find(x=>x.name===a)||{}).art]||"👤 ")+a;
    const grp=ri=>{const m=new Map();P.filter(p=>p.ri===ri).forEach(p=>{const k=(p.akteure||[]).join(" / ");if(!m.has(k))m.set(k,[]);m.get(k).push(p);});return m;};
    (c.traegt||[]).forEach(t=>{const a=teilAkteur(c,t);
      zeile("↺ trägt "+t+(a?" · als "+sym(a.name):" · ⚠ kein Akteur-Vertrag"),[],"lt");
      P.filter(p=>p.ri==="zusage"&&p.teil===t&&!p.fehlt).forEach(p=>zeile("   ◀",[name(p,p.msg+((p.aus||[]).length?" → "+p.aus.join(", ")+(p.strom?" (Strom)":""):" · Kenntnis"))],"lr"));});
    [["sendet","▶ sendet"],["fragt","? fragt"]].forEach(([ri,kopf])=>grp(ri).forEach((ps,k)=>
      zeile(kopf+(k?" als "+k.split(" / ").map(sym).join(" / "):" · ⚠ kein Akteur darf es"),ps.map(p=>name(p)),k?"":"lwarn")));
    if(P.some(p=>p.ri==="kenntnis"))zeile("◀ hört",P.filter(p=>p.ri==="kenntnis").map(p=>name(p)));
    if(!P.length)box.append(h("div",{class:"lz lleer"},"— leerer Vertrag: im Panel ⊕ trägt / sendet / hört"));
    return box;}
  function kurzfassung(n){if(n.kind==="client")return anschlussleiste(n);if(n.kind==="host")return hostKurz(n);const r=n.ref,k=n.kind;let t="";
    if(k==="code")t=(r.unaufgeloest||[]).length?"⚠ "+r.unaufgeloest.join(", ")+" nicht aufgelöst":(r.art||"Typ")+" · kein Baustein — roh";
    if(k==="command"){const ev=MODEL.decider.filter(d=>d.command===r.name).flatMap(d=>(d.ergibt||[]).map(o=>o.event)).filter(e=>(recByName(e)||{}).kind!=="rejection");
      t=ev.length?"→ "+kurz(ev):"→ (kein Decider)";}
    else if(k==="event"){const c=MODEL.decider.filter(d=>(d.ergibt||[]).some(o=>o.event===r.name)).map(d=>d.command);t=c.length?"← "+kurz(c):"";}
    else if(k==="aggregate")t=MODEL.decider.filter(d=>d.aggregat===r.name).length+" Commands · "+MODEL.applier.filter(a=>a.aggregat===r.name).length+" Events · "+(r.state||[]).length+" State-Felder";
    else if(k==="projektion"||k==="reaktion")t=(r.handles||[]).length+" Handles · ← "+kurz((r.handles||[]).map(x=>x.event));
    else if(k==="reader")t=(r.handles||[]).length+" Handles · ? "+kurz((r.handles||[]).map(x=>x.query));
    else if(k==="flussknoten")t=flussKurz(n);
    else if(k==="saga")t="Auslöser: "+(r.triggerEvent||"—")+" · "+transOf(r).length+" Regeln";
    else if(k==="transition")t="WENN "+kurz(r.wenn)+" → "+kurz((r.dann||[]).map(d=>d.rufe?"ƒ "+d.rufe+(d.zeitlimit?" ⏳":""):d.sende+(d.zeitlimit?" ⏳":"")));
    else if(k==="funktion"){const dn=KETTE.filter(x=>x.art==="dann"&&x.von==="fk:"+r._id).map(x=>x.d.rufe?"ƒ "+x.d.rufe:x.d.sende);
      t=(r.auftrag||"?")+" → "+((r.ergebnisse||[]).length?kurz(r.ergebnisse):"(kein Ergebnis)")+(dn.length?" · dann "+kurz(dn):"");}
    else if(k==="auftrag")t="Eingang von ƒ "+(r.funktion||"—");
    else if(k==="dienst")t=((r.methoden||[]).length?kurz(r.methoden.map(m=>m.name)):"keine Methode")+" · "+((r.implementierungen||[]).length?"⚙ "+kurz(r.implementierungen.map(i=>i.name)):"⚠ keine Implementierung");
    else if(k==="store")t=(r.writeFns||[]).length+" schreibend · "+(r.readFns||[]).length+" lesend";
    else if(k==="readmodel"&&r.geteilt)t="⇄ geteilt";
    else if(k==="auf")t="◀ "+(r.eingang||"?")+" → "+((r.ausgaenge||[]).length?kurz(r.ausgaenge)+(r.strom?" (Strom)":""):"zur Kenntnis");
    else if(k==="akteur")t=(ART_SYM[r.art]||"")+((r.darf||[]).length?"darf "+kurz(r.darf):"darf nichts")
      +((r.vertrag||[]).length?" · Zusagen auf "+kurz(r.vertrag.map(x=>x.eingang)):"");
    else if(k==="handle"){const aus=(r.ausgaenge||[]).length?(r.ausgaenge||[]).filter(a=>a.art!=="storefn").map(a=>a.typ)
        :[...(r.responses||[]),...(r.sends||[]),...(r.publishes||[])];
      const fns=(r.fns||[]).map(fid=>{const f=fnById(fid);return f?f.fn.name:null;}).filter(Boolean);
      t=(aus.length?"→ "+[...new Set(aus)].join(" | "):(fns.length?"":"→ nichts"))+(fns.length?(aus.length?" · ":"")+"ruft "+kurz(fns):"")+(r.signaturOffen?" · ⚠ offen":"");}
    else if(k==="fn")t="("+(r.params||[]).map(x=>x.typ).join(", ")+")"+(r.rueckgabe?" → "+r.rueckgabe:"");
    else if(k==="rumpf"){const v=r.kid&&VOR[r.kid],m=r.kid&&MELD[r.kid];
      t=r.kid&&LAUF[r.kid]?"● "+LAUF[r.kid].was+" …":v?"🤖 Vorschlag wartet auf ✓ Übernehmen":((r.text||"").trim().split("\n")[0]||"// leer");}
    if(Array.isArray(r.felder)&&k!=="aggregate")t+=(t?" · ":"")+r.felder.length+" Felder";
    // Partner in nicht geladenen Domänen: die Kante endet nicht im Leeren, sie ist benannt (Panel: „↗ außerhalb", Klick lädt).
    {const za=aussenAnzahl(n);if(za)t+=(t?" · ":"")+"↗ "+za+" außerhalb";}
    const box=h("div",{class:"gsum",title:"Klick: im Inspector bearbeiten",onclick:()=>waehle(n.id)});
    if(t)box.append(h("div",{class:"gs-t",title:t},t));
    const det=(DETAILS.get(n.id)||[]).map(id=>NODEBY.get(id)).filter(Boolean);
    if(det.length){const chips=h("div",{class:"gchips"}),zahl={};det.forEach(m=>zahl[m.kind]=(zahl[m.kind]||0)+1);
      const LBL={decider:"⚖ Decider",applier:"↻ Applier",state:"▤ State",rejection:"✕ Ablehnung",valueobject:"◇ VO",enum:"◇ Enum",queryresponse:"↩ Response"};
      Object.keys(zahl).forEach(kk=>{let lbl=(LBL[kk]||kk)+(zahl[kk]>1?" ×"+zahl[kk]:"");
        if(kk==="rumpf")lbl="{ } "+det.filter(m=>m.kind==="rumpf").reduce((s,m)=>s+((m.ref.text||"").split("\n").length),0)+" Zeilen";
        chips.append(h("span",{class:"gchip"},lbl));});
      if(det.some(m=>(m.kind==="decider"||m.kind==="applier")&&!(m.ref.rumpf||"").trim()&&!m.ref.leer))
        chips.append(h("span",{class:"gchip warn",title:"Decide-/Apply-Rumpf fehlt"},"⚙ Logik fehlt"));
      box.append(chips);}
    return box;}

  // ── (4) SEMANTISCHER ZOOM ──
  function zoomAuf(s){if(!canvas)return;const cr=canvas.getBoundingClientRect(),wx=(cr.width/2-PAN.x)/PAN.s,wy=(cr.height/2-PAN.y)/PAN.s;
    PAN.s=s;PAN.x=cr.width/2-wx*s;PAN.y=cr.height/2-wy*s;applyPan();}
  // Knoten-Menge einpassen (minS: mindestens diese Zoomstufe).
  function passeEin(ids,maxS,minS){if(!canvas||!world||!ids||!ids.length)return;let x1=1e9,y1=1e9,x2=-1e9,y2=-1e9;
    ids.forEach(id=>{const n=NODEBY.get(id),el=world.querySelector('[data-id="'+id+'"]');if(!n||!el)return;const p=P(n);
      x1=Math.min(x1,p.x||0);y1=Math.min(y1,p.y||0);x2=Math.max(x2,(p.x||0)+el.offsetWidth);y2=Math.max(y2,(p.y||0)+el.offsetHeight);});
    if(x1>x2)return;const cr=canvas.getBoundingClientRect(),pad=60;
    const s=Math.max(minS||0.08,Math.min(maxS||1,(cr.width-2*pad)/Math.max(1,x2-x1),(cr.height-2*pad)/Math.max(1,y2-y1)));
    PAN.s=s;PAN.x=cr.width/2-(x1+x2)/2*s;PAN.y=cr.height/2-(y1+y2)/2*s;applyPan();}
  // ══ DOMÄNEN-RAHMEN: rein visuelle Blöcke UM das bestehende Layout (Rollen-Spalten, Code/🤖 unter dem Besitzer bleiben).
  //   Domäne = Namespace, hierarchisch (Unterdomänen liegen im Rahmen ihrer Eltern-Domäne; packLayout sortiert danach).
  //   Der Rahmen folgt den ECHTEN Kartenpositionen (GEO) — zieht man Karten, wächst er mit. Kopf = kleine Menüleiste.
  //   Leere neue Domänen (VIEW.domNeu) und die Heimat namespace-loser Knoten (VIEW.heim) leben nur in der Ansicht.
  const RP=36, RK=46, SPK=34, SPAD=8;   // Domänen-Rand/-Kopf, Spalten-Kopf/-Rand (Welt-px) — packLayout reserviert RP/RK je Region, SPK je Spalte
  const AP=22, AK=40, AGAP=28, AKLEER_W=560;   // Akteur-Rahmen (Domäne × Akteur, docs/konzept-akteure.md §8): Rand, Kopf, Abstand, Breite eines leeren
  const OHNE_DOM="§ohne";
  // Domäne einer Karte (Rahmen) — vom Server (board.sicht): das Aggregat, zu dem sie laut Graph gehört (Projektion über ihre Events,
  //   Reader/Store/ReadModel über ihre Projektion, Ablauf-Einheiten dort, wo sie wirken, Datentypen bei ihren Nachbarn), sonst ihr Namespace;
  //   Clients und Pipelines haben eigene Rahmen, Zusagen stehen beim Akteur, ein Akteur in der ersten Domäne, in der er wirkt. Darüber nur die
  //   Ansicht: die Heimat namespace-loser Karten (VIEW.heim). Eine Karte, die es im Code noch nicht gibt, steht bis zum Board in ihrem Namespace.
  let FRGEO=new Map();   // FRGEO: Rahmen-Geometrie je Schlüssel (Domäne|Akteur, §vertrag:Akteur) — Ziel der Client-Leitungen
  function domKey(n){if(n.kind==="host")return HOST_KEY;
    // Der Rumpf einer Client-Implementierung steht in der Domäne des Client-Namespaces (der Client-Rahmen ist nur die Anschlussleiste).
    if(n.kind==="rumpf"&&n.own.kind==="client")return nsVon(n.own)||OHNE_DOM;
    const k=sk(n);if(k&&k.domaene)return k.domaene;
    if(!k){if(n.kind==="client")return CL_PRE+n.ref.name;if(n.kind==="flussknoten")return FL_PRE+n.own.ref._id;
      if(n.kind==="rumpf")return domKey(n.own);}
    return (!k&&nsVon(n))||(VIEW.heim||{})[n.id]||OHNE_DOM;}
  const domLabel=ns=>ns===OHNE_DOM?"⋯ ohne Domäne":istHostDom(ns)?"🖥 Host · "+(WB.zustand&&WB.zustand.aktiv||"Umgebung"):istClientDom(ns)?"🔌 "+clientAnzeige({name:ns.slice(CL_PRE.length)}):istFlussDom(ns)?"⛓ "+((flussVonDom(ns)||{}).name||"Pipeline"):letztesSeg(ns);
  // Block (Aggregat bzw. Brücke) eines Knotens: aus dem Graphen; ein über eine Spalte angelegter, noch unverdrahteter Knoten
  //   behält bis zur Verdrahtung den Block dieser Spalte (VIEW.heimBlk) — statt im Inselkasten zu landen.
  const blockVon=n=>subGroupOf(n)||(VIEW.heimBlk||{})[n.id]||SHARED_KEY;
  const istInselLage=n=>ISLE.has(n.id)&&!(VIEW.heimBlk||{})[n.id];
  const domOrd=(a,b)=>istAussenDom(a)!==istAussenDom(b)?(istAussenDom(a)?1:-1):a===OHNE_DOM?1:b===OHNE_DOM?-1:a.localeCompare(b);
  const bausteine=z=>z+(z===1?" Baustein":" Bausteine");
  // Gemeinsame Wurzel (z. B. „Domain“) — darunter beginnen die Domänen (wie im Start-Dialog).
  function domWurzel(){const alle=new Set();graphNodes().forEach(n=>{const x=domKey(n)===OHNE_DOM||istAussenDom(domKey(n))?null:domKey(n);if(x)for(let p=x;p;p=elternNs(p))alle.add(p);});
    (VIEW.domNeu||[]).forEach(x=>{for(let p=x;p;p=elternNs(p))alle.add(p);});
    FLHEIM.forEach(x=>{if(x)for(let p=x;p;p=elternNs(p))alle.add(p);});
    let top="";for(;;){const k=[...alle].filter(x=>elternNs(x)===top);if(k.length!==1||![...alle].some(y=>elternNs(y)===k[0]))break;top=k[0];}
    return top;}
  // Baum über die gegebenen Domänen (+ ihre Eltern-Namespaces bis unter die Wurzel).
  function domBaum(keys){const w=domWurzel(),alle=new Set();
    const kette=ns=>{for(let p=ns;p&&p!==w;p=elternNs(p)){alle.add(p);if(!drinNs(w,p))break;}};
    keys.forEach(ns=>{if(istFlussDom(ns)){alle.add(ns);const hm=FLHEIM.get(ns);if(hm&&hm!==w)kette(hm);return;}
      if(ns===OHNE_DOM||ns===w||istAussenDom(ns)){alle.add(ns);return;}kette(ns);});
    const elternIn=ns=>{if(istFlussDom(ns)){const hm=FLHEIM.get(ns);return hm&&hm!==w&&alle.has(hm)?hm:null;}
      if(ns===OHNE_DOM||ns===w||istAussenDom(ns))return null;const e=elternNs(ns);return alle.has(e)&&e!==w?e:null;};
    const kinder=new Map();alle.forEach(ns=>{const e=elternIn(ns);if(e){if(!kinder.has(e))kinder.set(e,[]);kinder.get(e).push(ns);}});
    kinder.forEach(a=>a.sort(domOrd));
    return {oben:[...alle].filter(ns=>!elternIn(ns)).sort(domOrd),kinder,elternIn};}
  // Rahmen aus GEO zeichnen (nach jeder Geometrie-Messung). Liefert die Ausdehnung für die Weltgröße.
  function zeichneRahmen(){const aus={x2:0,y2:0};if(!world)return aus;
    let ebene=world.querySelector(".grahmen-ebene");if(!ebene){ebene=h("div",{class:"grahmen-ebene"});world.insertBefore(ebene,world.firstChild);}
    ebene.textContent="";
    const box=new Map(),zahl=new Map(),ids=new Map();
    const dazu=(ns,x1,y1,x2,y2)=>{const b=box.get(ns);if(!b)box.set(ns,{x1,y1,x2,y2});else{b.x1=Math.min(b.x1,x1);b.y1=Math.min(b.y1,y1);b.x2=Math.max(b.x2,x2);b.y2=Math.max(b.y2,y2);}};
    GEO.forEach((g,id)=>{const n=NODEBY.get(id);if(!n)return;const d=domKey(n);dazu(d,g.x,g.y,g.x+g.w,g.y+g.h);
      if(!ids.has(d))ids.set(d,[]);ids.get(d).push(id);});
    // ── SPALTEN-RAHMEN: die senkrechten Rollen-Spalten je Block (Aggregat bzw. Brücke) einer Domäne — dieselbe Zuordnung
    //   wie packLayout (subGroupOf + rolle). 📝/🤖 gehören zur Spalte ihres Besitzers (sie stehen eingerückt darunter);
    //   Inseln bilden einen eigenen Kasten. Aus GEO gebildet → folgt gezogenen Karten.
    const spalten=new Map(),codes=[];
    const sp=(key,init)=>{let c=spalten.get(key);if(!c)spalten.set(key,c={...init,x1:1e9,y1:1e9,x2:-1e9,y2:-1e9,ids:[],kinds:new Set()});return c;};
    const nimm=(c,id,g,k)=>{c.x1=Math.min(c.x1,g.x);c.y1=Math.min(c.y1,g.y);c.x2=Math.max(c.x2,g.x+g.w);c.y2=Math.max(c.y2,g.y+g.h);c.ids.push(id);if(k)c.kinds.add(k);};
    // Akteur-Rahmen (Domäne × Akteur, docs/konzept-akteure.md §8): nur in Domänen mit Akteuren; sonst null (ein Block-Layout wie bisher).
    const aktVon=n=>akteurOrdnung(domKey(n)).length?akteurVon(n):null;
    GEO.forEach((g,id)=>{const n=NODEBY.get(id);if(!n)return;const d=domKey(n);
      if(n.kind==="client"||istFlussDom(d)||istHostDom(d))return;   // Client = Anschlussleiste; Pipeline-Knoten + ihre Karten liegen im Pipeline-Rahmen, nicht in Spalten
      if(istInselLage(n)){nimm(sp(d+"|§insel",{dom:d,blk:null,role:"insel",akt:null}),id,g,n.kind);return;}
      if(n.kind==="rumpf"){codes.push([n,g,id]);return;}
      const blk=blockVon(n),role=rolle(n,rollenVon(blk)),akt=aktVon(n);
      nimm(sp(d+"|"+akt+"|"+blk+"|"+role,{dom:d,blk,role,akt}),id,g,n.kind==="handle"?"handle:"+n.own.kind:role===rollenVon(blk).ergebnis?"ergebnis":n.kind);});
    const fest=[...spalten.values()].filter(c=>c.role!=="insel");
    codes.forEach(([n,g,id])=>{const d=domKey(n),bk=blockVon(n),ak=aktVon(n);let best=null;
      fest.forEach(c=>{if(c.dom!==d||c.blk!==bk||c.akt!==ak||g.x<c.x1-4||g.x>c.x1+80||g.y<c.y1)return;if(!best||c.x1>best.x1)best=c;});
      if(best)nimm(best,id,g,null);else nimm(sp(d+"|"+ak+"|"+blockVon(n)+"|code",{dom:d,blk:blockVon(n),role:"code",akt:ak}),id,g,n.kind);});
    spalten.forEach(c=>{c.r={x1:c.x1-SPAD,y1:c.y1-SPK,x2:c.x2+SPAD,y2:c.y2+SPAD};dazu(c.dom,c.r.x1,c.r.y1,c.r.x2,c.r.y2);});
    // Akteur-Rahmen = Hülle seiner Spalten (+ Rand/Kopf); leere (alles schon weiter oben) = nur Kopf an der Packlage.
    const LAYA=KPOS[VIEW.details?"d":"k"]||{};
    const akt=new Map();
    spalten.forEach(c=>{if(c.akt==null)return;const k=c.dom+"|"+c.akt,e=AP-SPAD;let a=akt.get(k);
      const x1=c.r.x1-e,y1=c.r.y1-e-AK,x2=c.r.x2+e,y2=c.r.y2+e;
      if(!a)akt.set(k,a={dom:c.dom,akt:c.akt,x1,y1,x2,y2,ids:[]});else{a.x1=Math.min(a.x1,x1);a.y1=Math.min(a.y1,y1);a.x2=Math.max(a.x2,x2);a.y2=Math.max(a.y2,y2);}
      a.ids.push(...c.ids);});
    new Set([...box.keys()]).forEach(d=>akteurOrdnung(d).forEach(a=>{const k=d+"|"+a;if(akt.has(k))return;const p=LAYA["§aktleer:"+k];
      if(p)akt.set(k,{dom:d,akt:a,x1:p.x,y1:p.y,x2:p.x+AKLEER_W,y2:p.y+AK,ids:[],leer:true});}));
    akt.forEach(a=>dazu(a.dom,a.x1,a.y1,a.x2,a.y2));
    const B=domBaum([...box.keys(),...(VIEW.domNeu||[])]);
    const LAY=KPOS[VIEW.details?"d":"k"]||{};
    let unten=0;box.forEach(b=>unten=Math.max(unten,b.y2));
    const rahmen=[];FRGEO=new Map();const STK=clientStecker();
    // Bottom-up: Rahmen = eigene Karten + Kinder-Rahmen, um Rand und Kopf erweitert.
    const rechne=(ns,tiefe)=>{const kinder=(B.kinder.get(ns)||[]).map(k=>rechne(k,tiefe+1)).filter(Boolean);
      const b=box.get(ns);let r=b?{x1:b.x1-RP,y1:b.y1-RP-RK,x2:b.x2+RP,y2:b.y2+RP}:null;
      kinder.forEach(k=>{if(!r)r={x1:k.x1-RP,y1:k.y1-RP-RK,x2:k.x2+RP,y2:k.y2+RP};else{r.x1=Math.min(r.x1,k.x1-RP);r.y1=Math.min(r.y1,k.y1-RP-RK);r.x2=Math.max(r.x2,k.x2+RP);r.y2=Math.max(r.y2,k.y2+RP);}});
      if(!r){if(!(VIEW.domNeu||[]).includes(ns))return null;   // leere neue Domäne: Platzhalter (von packLayout reserviert)
        const p=LAY["§leer:"+ns]||{x:0,y:unten+RP+RK+120};unten=Math.max(unten,p.y+RK+2*RP+110);
        r={x1:p.x,y1:p.y,x2:p.x+640+2*RP,y2:p.y+RK+2*RP+110,leer:true};}
      const z=(ids.get(ns)||[]).length+kinder.reduce((s,k)=>s+k.z,0);
      const out={...r,ns,tiefe,z,eigen:ids.get(ns)||[],kinderIds:kinder.flatMap(k=>k.alleIds),alleIds:[...(ids.get(ns)||[]),...kinder.flatMap(k=>k.alleIds)]};
      rahmen.push(out);return out;};
    B.oben.forEach(ns=>rechne(ns,0));
    rahmen.sort((a,b)=>a.tiefe-b.tiefe).forEach(r=>{FRGEO.set(r.ns+"|",r);
      if(istClientDom(r.ns)){ebene.append(clientRahmen(r));aus.x2=Math.max(aus.x2,r.x2);aus.y2=Math.max(aus.y2,r.y2);return;}
      if(istFlussDom(r.ns)){ebene.append(flussRahmen(r));aus.x2=Math.max(aus.x2,r.x2);aus.y2=Math.max(aus.y2,r.y2);return;}
      if(istHostDom(r.ns)){ebene.append(hostRahmen(r));aus.x2=Math.max(aus.x2,r.x2);aus.y2=Math.max(aus.y2,r.y2);return;}
      const hue=domHue(r.ns===OHNE_DOM?SHARED_KEY:r.ns);
      const fr=h("div",{class:"grahmen"+(r.tiefe?" unter":"")+(akteurOrdnung(r.ns).length?" mitakt":"")});fr.dataset.ns=r.ns;
      Object.assign(fr.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});fr.style.setProperty("--dc","hsl("+hue+" 45% 55%)");
      const k=h("div",{class:"grahmen-k",title:r.ns===OHNE_DOM?"Knoten ohne Namespace":r.ns},
        h("span",{class:"gr-t"},domLabel(r.ns)),h("span",{class:"gr-p"},r.ns===OHNE_DOM?"":r.ns),
        akteurOrdnung(r.ns).length?h("span",{class:"gr-p",title:"Akteur-Rahmen dieser Domäne (Reihenfolge = wo Geteiltes steht)"},"👤 "+akteurOrdnung(r.ns).join(" › ")):null,
        h("span",{class:"gr-z"},r.leer?"leer":bausteine(r.z)),
        akteurOrdnung(r.ns).length?null:steckerZeile(STK.get(r.ns+"|"),[r.ns+"|"]),
        h("button",{title:"Baustein in dieser Domäne anlegen",onclick:e=>{e.stopPropagation();bausteinMenue(e.clientX,e.clientY,r.ns);}},"＋"),
        r.ns!==OHNE_DOM?h("button",{title:"Unterdomäne anlegen",onclick:e=>{e.stopPropagation();neueDomaene(e.clientX,e.clientY,r.ns);}},"＋ ▤"):null,
        h("button",{title:"Domäne einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"),
        h("button",{title:"Mehr",onclick:e=>{e.stopPropagation();domMenue(e.clientX,e.clientY,r);}},"⋯"));
      k.oncontextmenu=e=>{e.preventDefault();e.stopPropagation();domMenue(e.clientX,e.clientY,r);};
      k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};
      fr.append(k);if(r.leer)fr.append(h("div",{class:"gr-leer"},"Leere Domäne — ＋ legt den ersten Baustein an"));
      ebene.append(fr);aus.x2=Math.max(aus.x2,r.x2);aus.y2=Math.max(aus.y2,r.y2);});
    akt.forEach(a=>{FRGEO.set(a.dom+"|"+a.akt,a);ebene.append(akteurRahmen(a,STK.get(a.dom+"|"+a.akt)));aus.x2=Math.max(aus.x2,a.x2);aus.y2=Math.max(aus.y2,a.y2);});
    spalten.forEach(c=>{if(c.blk===AKTEUR_KEY&&c.role===ROLE_AKTEUR.auf){FRGEO.set("§vertrag:"+c.akt,c.r);ebene.append(vertragsRahmen(c));return;}
      const el=h("div",{class:"gspalte"+(c.role==="insel"?" insel":"")});const r=c.r;
      Object.assign(el.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});
      const ORD=Object.keys(NODELABEL),ix=k=>{const i=ORD.indexOf(k.replace(/^handle:.*/,"handle"));return i<0?99:i;};
      const kinds=[...c.kinds].sort((x,y)=>ix(x)-ix(y)),haupt=kinds[0]||"rumpf";
      const titel=c.role==="insel"?"⚠ Inseln":c.role==="code"?"📝 Code":[...new Set(kinds.map(spaltenName))].join(" · ");
      const neu=spaltenNeu(c);
      const k=h("div",{class:"gspalte-k",title:titel+" — "+(c.blk===AKTEUR_KEY?"👤 Akteur-Band · ":c.blk&&c.blk!==SHARED_KEY?"Aggregat "+c.blk+" · ":"")+domLabel(c.dom)},
        h("i",{class:"kc-"+(haupt.startsWith("handle:")?"handle":haupt)}),h("span",{class:"gs-t"},titel),h("span",{class:"gs-z"},String(c.ids.length)),
        neu.length?h("button",{title:"In dieser Spalte anlegen",onclick:e=>{e.stopPropagation();
          if(neu.length===1)spalteAnlegen(c,neu[0]);else menue(e.clientX,e.clientY,titel,neu.map(kk=>({t:"＋ "+(NODELABEL[kk]||kk),fn:()=>spalteAnlegen(c,kk)})));}},"＋"):null,
        h("button",{title:"Spalte markieren (Fokus)",onclick:e=>{e.stopPropagation();fokussiere("menge",new Set(c.ids),"Spalte "+titel);}},"◎"));
      k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};
      el.append(k);ebene.append(el);});
    return aus;}
  // 📜 Der VERTRAG eines Akteurs als Rahmen (docs/konzept-akteure.md §3): die Schnittstelle, gegen die der Client programmiert —
  //   innen je Auf(Event) eine Zusage-Karte, die Kanten laufen zu den Events (◀) und Commands (▶). Kopf: Name · Ein/Aus ·
  //   ＋ Zusage (Events leuchten) · ◎ · ⤢. Klick auf den Namen = Akteur-Panel.
  function vertragsRahmen(c){const r=c.r,ak=MODEL.akteure.find(x=>x.name===c.akt),v=(ak&&ak.vertrag)||[];
    const el=h("div",{class:"gspalte vertrag"});
    Object.assign(el.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});
    const ein=v.map(x=>x.eingang).filter(Boolean),aus=[...new Set(v.flatMap(x=>x.ausgaenge||[]))];
    const typ=ak?vertragsTyp(ak):"?";
    const k=h("div",{class:"gspalte-k",title:"public interface "+typ+" : IAkteurVertrag<"+c.akt+"> — der Client (Python-Worker …) implementiert ihn; "
        +"generiert: domain_client/generated/vertraege.py ("+c.akt+"Basis)\n◀ hört: "+(ein.join(", ")||"—")+"\n▶ gibt hinein: "+(aus.join(", ")||"—"),
        onclick:e=>{e.stopPropagation();if(ak)waehle("akt:"+ak._id);}},
      h("i",{class:"kc-auf"}),h("span",{class:"gs-t"},"📜 Vertrag "+typ),
      h("span",{class:"gs-z"},"◀ "+ein.length+" · "+aus.length+" ▶"),
      ak?h("button",{title:"Neue Zusage: Event wählen (passende Events leuchten)",onclick:e=>{e.stopPropagation();waehle("akt:"+ak._id);
        setTimeout(()=>{const p=SLOTS["akt:auf:"+ak._id+":open"];if(p)vbStart(p);},0);}},"＋"):null,
      h("button",{title:"Vertrag markieren (Fokus)",onclick:e=>{e.stopPropagation();fokussiere("menge",new Set(c.ids),"📜 Vertrag "+typ);}},"◎"),
      h("button",{title:"Vertrag einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"));
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};
    el.append(k);return el;}
  // Ein Akteur-Rahmen (Domäne × Akteur, docs/konzept-akteure.md §8): Kopf = Akteur (Klick → sein Panel) · Domäne · Zahl · „↥ auch“ (Doppelungen,
  //   die in einem früheren Akteur-Rahmen stehen) · ＋ (für diesen Akteur entwerfen) · ◎ · ⤢ · ⋯ (Reihenfolge).
  function akteurRahmen(a,stecker){const ohne=a.akt===OHNE_AKT,ak=!ohne&&MODEL.akteure.find(x=>x.name===a.akt),
    r={x1:a.x1,y1:a.y1,x2:a.x2,y2:a.y2};
    const fr=h("div",{class:"grahmen akt"+(ohne?" ohne":"")+(a.leer?" leer":"")+(ak&&!akteurDirektIn(ak,a.dom)?" kette":"")});
    fr.dataset.ns=a.dom;fr.dataset.akt=a.akt;
    Object.assign(fr.style,{left:a.x1+"px",top:a.y1+"px",width:(a.x2-a.x1)+"px",height:(a.y2-a.y1)+"px"});
    fr.style.setProperty("--dc",ohne?"#d0584f":"hsl("+domHue("akt:"+a.akt)+" 55% 64%)");
    const auch=ohne?[]:auchWoanders(a.akt,a.dom);
    const k=h("div",{class:"grahmen-k akt-k",title:(ohne?"Bausteine ohne Akteur — kein IDarf und keine Kette (GR-HERKUNFT)":
        a.akt+" in "+a.dom+(ak&&!akteurDirektIn(ak,a.dom)?" — wirkt hier nur über die Kette":""))},
      h("span",{class:"gr-t akt-n",onclick:e=>{e.stopPropagation();if(ak)waehle("akt:"+ak._id);}},ohne?"⚠ ohne Akteur":(ART_SYM[ak&&ak.art]||"👤 ")+a.akt),
      auch.length?h("span",{class:"akt-auch",title:"Gehört auch "+a.akt+", steht aber im Rahmen eines früheren Akteurs (Doppelungen dort, wo sie zuerst auftreten)"},
        "↥ auch: ",...auch.slice(0,6).map(([n,wo])=>h("button",{class:"akt-ref",title:n.name+" — steht bei "+wo,onclick:e=>{e.stopPropagation();zeigeKnoten(n.id);}},n.name)),
        auch.length>6?h("span",{class:"gr-z"}," +"+(auch.length-6)):null):null,
      h("span",{class:"gr-z"},a.leer?"alles schon weiter oben":bausteine(a.ids.length)),
      steckerZeile(stecker,[a.dom+"|"+a.akt,"§vertrag:"+a.akt]),
      ak?h("button",{title:"Für "+a.akt+" entwerfen: neuer Command / neue Query / neuer Trigger — "+a.akt+" darf ihn sofort (IDarf)",
        onclick:e=>{e.stopPropagation();menue(e.clientX,e.clientY,"＋ für "+a.akt,["command","query"].map(kk=>({t:"＋ "+(NODELABEL[kk]||kk),fn:()=>fuerAkteurAnlegen(a,kk)})));}},"＋"):null,
      h("button",{title:"Rahmen markieren (Fokus)",onclick:e=>{e.stopPropagation();fokussiere("menge",new Set(a.ids),(ohne?"ohne Akteur":a.akt)+" in "+domLabel(a.dom));}},"◎"),
      h("button",{title:"Rahmen einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"),
      !ohne?h("button",{title:"Reihenfolge der Akteur-Rahmen",onclick:e=>{e.stopPropagation();akteurRahmenMenue(e.clientX,e.clientY,a);}},"⋯"):null);
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};
    fr.append(k);return fr;}
  // 🔌 Stecker-Zeile im Rahmen-Kopf: welche Clients an diesem Rahmen stecken (Klick = Client wählen, seine Leitung hierher auffächern).
  function steckerZeile(cs,keys){if(!cs||!cs.length)return null;
    return h("span",{class:"cl-stecker",title:"Clients, deren Leitung an diesem Rahmen steckt (docs/konzept-akteure.md §4)"},"🔌 ",
      ...cs.map(c=>h("button",{class:"akt-ref",title:"Client "+clientAnzeige(c)+" — Klick: seine Leitung hierher auffächern",
        onclick:e=>{e.stopPropagation();auffaechern(c,{keys});}},clientAnzeige(c))));}
  // 🔌 Der Rahmen eines Clients (Außenwelt): Kopf = Name (Handshake) · verkörperte Akteure · generierte Basis · ◎ · ⤢. Innen die Karte
  //   = die Anschlussleiste. Gestrichelt umrandet: der Client ist Software draußen, nicht Domäne.
  function clientRahmen(r){const c=MODEL.clients.find(x=>CL_PRE+x.name===r.ns),fr=h("div",{class:"grahmen client"});fr.dataset.ns=r.ns;
    Object.assign(fr.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});
    fr.style.setProperty("--dc","hsl("+domHue(r.ns)+" 60% 62%)");
    const akt=c?clientAkteure(c):[];
    const k=h("div",{class:"grahmen-k",title:c?"public interface "+c.name+" : IClientVertrag, … — Handshake „"+clientAnzeige(c)+"“, Python-Basis "+clientAnzeige(c)+"Basis (domain_client/generated/vertraege.py)":""},
      h("span",{class:"gr-t akt-n",onclick:e=>{e.stopPropagation();if(c)waehle("cl:"+c._id);}},domLabel(r.ns)),
      h("span",{class:"gr-p"},akt.length?"verkörpert "+akt.map(n=>(ART_SYM[(MODEL.akteure.find(a=>a.name===n)||{}).art]||"👤 ")+n).join(", "):"verkörpert noch keinen Akteur"),
      c?h("button",{title:"Client markieren (Slice)",onclick:e=>{e.stopPropagation();waehle("cl:"+c._id);}},"◎"):null,
      h("button",{title:"Client einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"));
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};fr.append(k);return fr;}
  // Gibt der Akteur in dieser Domäne direkt etwas hinein (IDarf) — oder wirkt er hier nur über eine Kette?
  function akteurDirektIn(ak,dom){return (ak.darf||[]).some(nm=>{const n=NODEBY.get("rec:"+nm);return n&&domKey(n)===dom;});}
  // Doppelungen: Bausteine dieser Domäne, die auch dem Akteur gehören, aber im Rahmen eines früheren Akteurs stehen.
  function auchWoanders(name,dom){const r=[];const ART=["command","query","event","rejection","aggregate","reader","projektion","reaktion","saga","handle","store","readmodel","queryresponse"];
    NODEBY.forEach(n=>{if(!VIS.has(n.id)||!ART.includes(n.kind)||domKey(n)!==dom||!akteurSet(n).has(name))return;const wo=akteurVon(n);if(wo!==name)r.push([n,wo]);});
    return r.sort((x,y)=>ART.indexOf(x[0].kind)-ART.indexOf(y[0].kind)||x[0].name.localeCompare(y[0].name));}
  // Wie zeigeKnoten — liegt der Knoten in einer nicht geladenen Domäne, wird sie erst dazugeladen.
  function zeigeLadend(id){const n=NODEBY.get(id),ns=n&&nsVon(n);
    if(n&&LADEN&&!geladen(n)&&ns){LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;speichereAnsicht();render();setTimeout(()=>zeigeKnoten(id),0);return;}
    zeigeKnoten(id);}
  function zeigeKnoten(id){const g=GEO.get(id);if(g)einpassenRahmen({x1:g.x-260,y1:g.y-160,x2:g.x+g.w+260,y2:g.y+g.h+160});waehle(id);}
  // ＋ im Akteur-Rahmen: der neue Eingang gehört sofort diesem Akteur (IDarf) — Entwerfen „für wen".
  function fuerAkteurAnlegen(a,kind){const ag=MODEL.aggregate.find(x=>x.namespace===a.dom);
    neuerKnoten(kind,undefined,undefined,{ns:a.dom===OHNE_DOM?null:a.dom,agg:ag&&ag.name,blk:ag?ag.name:SHARED_KEY,akt:a.akt});}
  function akteurRahmenMenue(cx,cy,a){const ord=akteurOrdnung(a.dom),i=ord.indexOf(a.akt),ak=MODEL.akteure.find(x=>x.name===a.akt);
    const setze=neu=>{VIEW.akteurOrdnung={...(VIEW.akteurOrdnung||{}),[a.dom]:neu};speichereAnsicht();render();};
    menue(cx,cy,a.akt+" · "+domLabel(a.dom),[
      {t:"↑ weiter nach oben",aus:i<=0,title:"Geteilte Bausteine wandern mit zum neuen Ersten",fn:()=>{const o=[...ord];o.splice(i-1,0,o.splice(i,1)[0]);setze(o);}},
      {t:"↓ weiter nach unten",aus:i<0||i>=ord.length-1,fn:()=>{const o=[...ord];o.splice(i+1,0,o.splice(i,1)[0]);setze(o);}},
      {t:"↺ abgeleitete Reihenfolge",aus:!((VIEW.akteurOrdnung||{})[a.dom]),title:"Ersteller → wer worauf reagiert → Art → Name",
        fn:()=>{const v={...(VIEW.akteurOrdnung||{})};delete v[a.dom];VIEW.akteurOrdnung=v;speichereAnsicht();render();}},
      {trenn:true},
      {t:"👤 Akteur öffnen",aus:!ak,fn:()=>waehle("akt:"+ak._id)}]);}
  // Spalten-Kopf: Name je Art; ＋ legt eine Art dieser Rolle an (Handles/Store-Fns entstehen am Besitzer, nicht frei).
  const spaltenName=k=>k==="handle:projektion"?"Projektion-Handle":k==="handle:reader"?"Reader-Handle":k==="handle:pipeline"?"Pipeline-Handle":k==="handle:reaktion"?"Reaktion-Handle":k==="ergebnis"?"Ergebnis ƒ":NODELABEL[k]||k;
  const NICHT_FREI=new Set(["fn","handle","auf","ergebnis"]);
  function spaltenNeu(c){if(c.role==="insel"||c.role==="code")return [];
    const map=rollenVon(c.blk);
    return Object.keys(map).filter(k=>map[k]===c.role&&!k.includes(":")&&!NICHT_FREI.has(k));}
  function spalteAnlegen(c,kind){const agg=c.blk&&c.blk!==SHARED_KEY&&c.blk!==AKTEUR_KEY?c.blk:(MODEL.aggregate.find(a=>a.namespace===c.dom)||{}).name,
    sg=MODEL.sagas.find(s=>s.namespace===c.dom);
    neuerKnoten(kind,undefined,undefined,{ns:c.dom===OHNE_DOM?null:c.dom,agg,saga:sg&&sg.name,blk:c.blk||SHARED_KEY});}
  function einpassenRahmen(r){if(!CV.w)return;const w=r.x2-r.x1,hh=r.y2-r.y1,s=Math.max(0.08,Math.min(1,(CV.w-80)/w,(CV.h-80)/hh));
    PAN.s=s;PAN.x=CV.w/2-(r.x1+w/2)*s;PAN.y=CV.h/2-(r.y1+hh/2)*s;applyPan();}

  // Popover-Menü (gpick-Optik) an einer Bildschirmposition. items: {t,fn,aus,gefahr,title} | {kopf} | {trenn}
  function menue(cx,cy,titel,items){if(!canvas)return;canvas.querySelectorAll(".gpick").forEach(p=>p.remove());
    const r=canvas.getBoundingClientRect(),m=h("div",{class:"gpick gmenue"});
    if(titel)m.append(h("div",{class:"gpick-t"},titel));
    items.forEach(it=>{if(!it)return;if(it.trenn){m.append(h("div",{class:"gm-tr"}));return;}
      if(it.kopf){m.append(h("div",{class:"gpick-t"},it.kopf));return;}
      const b=h("button",{class:it.gefahr?"gm-gefahr":"",title:it.title||"",onclick:e=>{e.stopPropagation();m.remove();if(!it.aus)it.fn();}},it.t);
      if(it.aus)b.disabled=true;m.append(b);});
    ["pointerdown","dblclick","wheel","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>{e.stopPropagation();if(ev==="contextmenu")e.preventDefault();}));
    canvas.append(m);
    m.style.left=Math.max(4,Math.min(cx-r.left,r.width-m.offsetWidth-8))+"px";m.style.top=Math.max(4,Math.min(cy-r.top,r.height-m.offsetHeight-8))+"px";
    const zu=e=>{if(!m.contains(e.target)){m.remove();window.removeEventListener("pointerdown",zu,true);}};
    setTimeout(()=>window.addEventListener("pointerdown",zu,true),0);}
  // Kleine Eingabe (Name einer neuen Domäne).
  function frage(cx,cy,titel,vorschlag,hinweis,ok){if(!canvas)return;canvas.querySelectorAll(".gpick").forEach(p=>p.remove());
    const r=canvas.getBoundingClientRect(),m=h("div",{class:"gpick gmenue"});
    const inp=h("input",{value:vorschlag||"",spellcheck:"false"}),fehler=h("div",{class:"gm-fehler"});
    const los=()=>{const f=ok(inp.value.trim());if(f){fehler.textContent=f;return;}m.remove();};
    inp.onkeydown=e=>{e.stopPropagation();if(e.key==="Enter")los();if(e.key==="Escape")m.remove();};
    m.append(h("div",{class:"gpick-t"},titel),inp,hinweis?h("div",{class:"gm-hint"},hinweis):null,fehler,
      h("div",{style:"display:flex;gap:4px"},h("button",{onclick:los},"Anlegen"),h("button",{onclick:()=>m.remove()},"Abbrechen")));
    ["pointerdown","dblclick","wheel","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>e.stopPropagation()));
    canvas.append(m);m.style.left=Math.max(4,Math.min(cx-r.left,r.width-m.offsetWidth-8))+"px";m.style.top=Math.max(4,Math.min(cy-r.top,r.height-m.offsetHeight-8))+"px";
    inp.focus();inp.select();}
  const NS_SEG=/^[\p{L}_][\p{L}\p{N}_]*$/u;
  function neueDomaene(cx,cy,eltern){const wurzel=eltern||domWurzel();
    frage(cx,cy,eltern?"＋ Unterdomäne in „"+letztesSeg(eltern)+"“":"＋ Neue Domäne","Bestellung",
      "Namespace: "+(wurzel?wurzel+".":"")+"<Name> — Punkte = tiefer schachteln",name=>{
        if(!name)return "Name fehlt";
        if(!name.split(".").every(s=>NS_SEG.test(s)))return "Nur Buchstaben, Ziffern, _ (je Segment, nicht mit Ziffer beginnen)";
        const ns=(wurzel?wurzel+".":"")+name;
        if(graphNodes().some(n=>nsVon(n)===ns)||(VIEW.domNeu||[]).includes(ns))return "„"+name+"“ gibt es schon";
        VIEW.domNeu=[...(VIEW.domNeu||[]),ns];
        if(LADEN&&!LADEN.some(g=>drinNs(g,ns))){LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;}
        speichereAnsicht();render();
        requestAnimationFrame(()=>{const fr=world&&[...world.querySelectorAll(".grahmen")].find(d=>d.dataset.ns===ns);
          if(fr)einpassenRahmen({x1:fr.offsetLeft,y1:fr.offsetTop,x2:fr.offsetLeft+fr.offsetWidth,y2:fr.offsetTop+fr.offsetHeight});});
        deFlash("＋ Domäne "+ns,true);return null;});}
  // ＋ in der Menüleiste: Baustein IN dieser Domäne (Namespace; Decider/Applier/State ans Aggregat, Regel an den Prozess der Domäne).
  const BAU_GRUPPEN=[["Außen",["akteur","client"]],["Schreibseite",["command","event","rejection","aggregate","state","decider","applier"]],
    ["Typen",["valueobject","enum","konfig"]],["Abläufe",["saga","transition","funktion","dienst","reaktion","quelle"]],
    ["Leseseite",["readmodel","store","projektion","query","queryresponse","reader"]]];
  function bausteinMenue(cx,cy,ns){const agg=MODEL.aggregate.find(a=>a.namespace===ns),sg=MODEL.sagas.find(s=>s.namespace===ns);
    const it=[];BAU_GRUPPEN.forEach(([g,ks])=>{it.push({kopf:g});ks.forEach(k=>it.push({t:"＋ "+(NODELABEL[k]||k),
      fn:()=>neuerKnoten(k,undefined,undefined,{ns:ns===OHNE_DOM?null:ns,agg:agg&&agg.name,saga:sg&&sg.name})}));});
    menue(cx,cy,"＋ in "+domLabel(ns),it);}
  function domMenue(cx,cy,r){const neu=(VIEW.domNeu||[]).includes(r.ns);
    menue(cx,cy,r.ns===OHNE_DOM?domLabel(r.ns):r.ns,[
      {t:"＋ Baustein…",fn:()=>bausteinMenue(cx,cy,r.ns)},
      r.ns!==OHNE_DOM?{t:"＋ Unterdomäne…",fn:()=>neueDomaene(cx,cy,r.ns)}:null,
      {t:"⤢ Einpassen",fn:()=>einpassenRahmen(r)},
      r.alleIds.length?{t:"◎ Fokus: alle Bausteine markieren",fn:()=>fokussiere("menge",new Set(r.alleIds),"Domäne "+domLabel(r.ns))}:null,
      neu?{trenn:1}:null,
      neu?{t:"✕ Leere Domäne entfernen",gefahr:1,aus:!r.leer,title:r.leer?"":"Erst die Bausteine löschen — eine Domäne mit Inhalt entsteht aus dem Code",
        fn:()=>{VIEW.domNeu=(VIEW.domNeu||[]).filter(x=>x!==r.ns);speichereAnsicht();render();}}:null]);}
  // Rechtsklick auf die leere Fläche: neue Domäne.
  function flaechenMenue(e){e.preventDefault();menue(e.clientX,e.clientY,"Fläche",[{t:"＋ Neue Domäne…",fn:()=>neueDomaene(e.clientX,e.clientY,null)}]);}

  // Ansichts-Leiste über der Fläche.
  function ansichtLeiste(){
    const btn=(lbl,on,title,fn)=>h("button",{class:on?"on":"",title,onclick:fn},lbl);
    return h("div",{class:"gview"},
      h("b",{style:"color:#cbd3e1"},"Ansicht"),
      btn("＋ Domäne",false,"Neue Domäne (Namespace) anlegen — Rechtsklick auf die Fläche geht auch",e=>neueDomaene(e.clientX,e.clientY+20,null)),
      h("button",{title:"Geladene Domänen ändern",onclick:()=>deLaden()},"📂 "+(LADEN===null?"alles geladen":!LADEN.length?"leer":LADEN.map(letztesSeg).join(", "))),
      btn("⤢ Alles zeigen",false,"Ganzes Board einpassen",()=>passeEin([...VIS],1)),
      h("span",{id:"gfokus",class:"gfokus",style:"display:none"}),
      btn("👁 Ausblenden"+((VIEW.aus||[]).length?" ("+VIEW.aus.length+")":"")+" ▾",FILTER_OPEN,"Knotenarten und Domänen ein-/ausblenden",()=>{FILTER_OPEN=!FILTER_OPEN;render();}),
      h("span",{style:"margin-left:8px"},"Karte antippen = Panel · ⊕ im Panel = passende Knoten leuchten, anklicken = verbinden/lösen · Esc = fertig · Kopf ziehen = verschieben"));}

  function renderGraph(){
    const root=document.getElementById("de-form");
    const altI=root.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    root.innerHTML="";
    root.append(datalistEl());
    ISLE=inselMenge();       // einsame Inseln (unverbundene Knoten) — vom Server
    berechneSicht();         // sichtbar / eingeklappte Details / zusammengezogene Kanten
    if(SEL&&!NODEBY.has(SEL))SEL=null;fokusNeu();
    const tb=(kind,label)=>h("button",{class:"add jumpable",
      title:label.replace(/^\+\s*/,"")+" — Hover: alle hervorheben · Ctrl/Cmd+Klick: zum nächsten springen · Klick: neu",
      onmouseenter:()=>highlightKind(kind,true),onmouseleave:()=>highlightKind(kind,false),
      onclick:e=>{if(e.ctrlKey||e.metaKey){e.preventDefault();jumpNextOfKind(kind);}else neuerKnoten(kind);}},label);
    root.append(h("div",{class:"gtoolbar"},
      // Palette mit Ankern (docs/konzept-werkbank.md §8.5): etwas NEUES beginnen. Zugehöriges entsteht mit „＋" am Port bzw. im Domänen-Menü.
      tb("aggregate","+ Aggregat"),
      h("button",{class:"add",title:"Neuer Fluss: Quelle → Katalog-Funktionen → Commands, frei verdrahtet",onclick:()=>neuePipeline()},"+ ⛓ Fluss"),
      tb("funktion","+ ƒ Funktion"),tb("quelle","+ 📥 Quelle"),tb("akteur","+ 👤 Akteur"),tb("client","+ 🔌 Client"),tb("projektion","+ Projektion"),
      h("button",{class:"add island-btn",style:"margin-left:auto",
        title:"Einsame Inseln (unverbundene Knoten) — Hover: markieren · Klick: der Reihe nach anspringen",
        onmouseenter:()=>highlightIslands(true),onmouseleave:()=>highlightIslands(false),onclick:()=>jumpIslands()},
        (ISLE.size?"⚠ ":"✓ ")+ISLE.size+" Inseln")));
    root.append(ansichtLeiste());
    SLOTS={};SYNC={};   // Code-Sync-Registry je Render neu aufbauen (nur sichtbare Knoten pollen)
    canvas=h("div",{class:"gcanvas ablauf"});   // die Karten-Darstellung (Kopf ohne Körper; bearbeitet wird im Panel)
    world=h("div",{class:"gworld"+(VIEW.kompakt?" kompakt":"")});
    svg=document.createElementNS(SVGNS,"svg");svg.setAttribute("class","gedges");world.append(svg);
    const nodes=graphNodes();
    // Domänen-Filter + eingeklappte Details: nicht rendern (Kanten zu ihnen entfallen bzw. werden zusammengezogen).
    const vis=nodes.filter(n=>VIS.has(n.id));
    vis.forEach(n=>world.append(nodeEditor(n)));
    svgTop=document.createElementNS(SVGNS,"svg");svgTop.setAttribute("class","gedges top");world.append(svgTop);
    if(!vis.length)world.append(h("div",{class:"gempty"},LADEN&&!LADEN.length?"Leeres Board — über die Palette neu entwerfen oder „🗂 Domänen laden“."
      :"Nichts sichtbar — nichts geladen (🗂 Domänen laden), alles ausgeblendet (👁) oder leeres Modell."));
    canvas.append(world);root.append(canvas);
    beobachteCanvas();
    buildMinimap(canvas);
    renderFilterPanel();
    packLayout();  // jetzt ist world im Dokument → echte Knotengrößen messbar → überlappungsfreies Packing
    messeWelt();   // Geometrie-Cache (Minimap, Culling) + Weltgröße = Inhalt
    applyPan();
    drawMinimap();
    canvas.onpointerdown=e=>{const t=e.target;
      if(t.closest&&(t.closest(".ghead")||t.closest("input,textarea,select,button,.slot")))return;
      startPan(e);};
    canvas.oncontextmenu=e=>{const t=e.target;if(t.closest&&t.closest(".gnode2,.ginsp,.gfilter,.gminimap,.gpick,.gstart"))return;flaechenMenue(e);};
    canvas.ondblclick=e=>{if(e.target.closest&&e.target.closest(".gnode2"))return;
      const w=toWorld(e.clientX,e.clientY),r=canvas.getBoundingClientRect();
      showPicker(canvas,e.clientX-r.left,e.clientY-r.top,w.x,w.y);};
    canvas.onwheel=e=>{e.preventDefault();const r=canvas.getBoundingClientRect(),mx=e.clientX-r.left,my=e.clientY-r.top;
      const ns=Math.min(2,Math.max(0.08,PAN.s*(e.deltaY<0?1.1:0.9))),wx=(mx-PAN.x)/PAN.s,wy=(my-PAN.y)/PAN.s;
      PAN.s=ns;PAN.x=mx-wx*ns;PAN.y=my-wy*ns;applyPan();};
    // Klick ins Leere (ohne Pannen) = Auswahl/Fokus aufheben.
    canvas.onclick=e=>{if(PANNED)return;const t=e.target;
      if(t.closest&&t.closest(".gnode2,.ginsp,.gfilter,.gminimap,.gpick"))return;if(SEL)waehle(null);};
    drawEdges();requestAnimationFrame(drawEdges);
    zeigeInspector();
    zeigeStart();
    if(VB)vbZeige(false);
  }
  function showPicker(canvas,sx,sy,wx,wy){
    canvas.querySelectorAll(".gpick").forEach(p=>p.remove());
    const pick=h("div",{class:"gpick"});pick.style.left=sx+"px";pick.style.top=sy+"px";
    const opt=(kind,label)=>h("button",{onclick:()=>{pick.remove();neuerKnoten(kind,wx,wy);}},label);
    pick.append(h("div",{class:"gpick-t"},"Was soll hier entstehen?"),
      opt("aggregate","Aggregat"),h("button",{onclick:()=>{pick.remove();neuePipeline();}},"⛓ Fluss"),opt("funktion","ƒ Funktion"),opt("quelle","📥 Quelle"),
      opt("akteur","👤 Akteur"),opt("client","🔌 Client"),opt("projektion","Projektion"));
    canvas.append(pick);
    setTimeout(()=>{const off=ev=>{if(!pick.contains(ev.target)){pick.remove();document.removeEventListener("pointerdown",off);}};document.addEventListener("pointerdown",off);},0);
  }

  // Prozess-HUB: Prozess<Auslöser>. Transitionen stecken sich HIER an (sichtbare Kante, keine Ableitung).
  function prozessHubCard(body,s){
    body.append(topSlot("event","Auslöser (startet): "+(s.triggerEvent||"— ⊕ Event wählen"),{type:"evtUse",dir:"in",saga:s.name,trigger:true},"saga:trigger:"+s.name));
    body.append(nameInp(s,"name","Prozess","saga"));
    body.append(h("input",{value:s.namespace??"",oninput:e=>s.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    const aggs=(SICHT().karten["saga:"+s.name]||{}).beruehrt||[];
    body.append(h("div",{class:"gsec"},"berührt (abgeleitet): "+(aggs.length?aggs.join(" · "):"—")));
    body.append(h("div",{class:"gsec"},"Regeln ◀ (anstecken)"));
    // Je Regel eine Zeile — Klick öffnet die Regel (Wenn/Dann, Bau-Lambda mit 📝/🤖, Zeitlimit, Kompensation), wie die Zusagen am Akteur.
    transOf(s).forEach(t=>{const p=anchorDot("prozess");p.classList.add("i");reg("hub:in:"+s.name+":"+t._id,p,null);
      const row=h("div",{class:"slotrow",style:"cursor:pointer",title:"Regel öffnen"},p,h("span",{class:"slotlbl"},"WENN "+((t.wenn||[])[0]||"?")+((t.wenn||[]).length>1?" +"+((t.wenn.length-1)+(t.sammelEvent?1:0))+"":(t.sammelEvent?" +alle":""))+" → "+((t.dann||[]).map(d=>d.rufe?"ƒ "+d.rufe:(d.sende||"?")).join(", ")||"?")));
      row.onclick=()=>waehle("tr:"+t._id);body.append(row);});
    const oi=port("prozess");oi.classList.add("i");reg("hub:in:"+s.name+":open",oi,{type:"prozess",dir:"in",saga:s.name});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Regel anstecken")));
    // Ablauf als Kette: was beim Start ZUERST passiert (weitere Schritte hängen an den Funktions-Knoten: „dann ▶“).
    body.append(h("div",{class:"gsec"},"Ablauf ▶ erst …"));
    kettenZeilen(body,"saga:"+s.name);
    if(s.triggerEvent)body.append(slotRow("command","bei "+s.triggerEvent+" ▶ erst ⊕ (ƒ · Command)","r",{type:"kette",dir:"out",saga:s.name,bei:s.triggerEvent},"kette:saga:"+s.name));
    else body.append(h("div",{class:"gsec",style:"opacity:.6"},"Erst einen Auslöser wählen."));
  }
  // Elementtyp der SendeJe-Collection (z. B. List<Guid> → Guid) — für den Typ des z-Pins.
  function collElemTyp(t){if(!t.sendeJeCollection)return "";const dot=t.sendeJeCollection.indexOf(".");if(dot<0)return "";
    const role=t.sendeJeCollection.slice(0,dot),field=t.sendeJeCollection.slice(dot+1);const j=["t","r","g"].indexOf(role);
    const r=recByName((t.wenn||[])[j]);const f=r&&(r.felder||[]).find(x=>x.name===field);const e=elementVon(f);return e?baseTyp(e):"";}
  // REGEL-KNOTEN: liest sich als Satz WENN … DANN SENDE … SONST ↩ … (eine DSL-Regel = ein SagaSchritt).
  //   Logik (Argument-Bau, Count-Ausdruck) ist Fülle-Zeit → Stub, KEINE Argument-Pins mehr.
  // REGEL = kleine KREUZUNG im Event/Command-Fluss (Petri-Transition): Event-Eingänge (Join) → Command-Ausgang.
  //   KEINE wiederholten Namen, KEINE Sektionen — der Inhalt lebt in den verdrahteten Event/Command-Nodes
  //   (Name nur als Hover-Titel + über die Kante sichtbar). Der Join = mehrere zusammenlaufende Kanten.
  function transitionCard(body,t){
    const mk=(txt,title,on,onclick)=>h("span",{title:title||"",...(onclick?{onclick}:{}),
      style:"font-size:9px;padding:0 3px;border-radius:3px;border:1px solid #3a4453;opacity:"+(on?"1":".55")+";"+(onclick?"cursor:pointer;":"")+(on?"background:#2f5d46;color:#c8f0d8;border-color:#2f5d46":"")},txt);
    body.append(topSlot("prozess","→ Prozess"+(t.prozess?": "+t.prozess:" (andocken)"),{type:"prozess",dir:"out",trans:t._id},"tr:prozess:"+t._id));
    // ── WENN (Join): Event-Eingänge UNTEREINANDER, beliebig viele (keine 3er-Grenze) ──
    body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl",style:"opacity:.55"},"Wenn ◀ (Join)")));
    (t.wenn||[]).forEach((e,j)=>{const p=port("event");p.classList.add("i");p.title=e||"(Event)";reg("tr:in:"+t._id+":"+j,p,{type:"evtUse",dir:"in",trans:t._id,wennIdx:j});
      body.append(h("div",{class:"slotrow"},p,h("span",{class:"slotlbl",style:"flex:1;opacity:.75"},e||"(Event)"),h("button",{class:"rm",onclick:()=>{t.wenn.splice(j,1);render();}},"✕")));});
    // Offener „+ und"-Eingang (unbegrenzt).
    const oi=port("open");oi.classList.add("i");oi.title="+ und (Event)";reg("tr:in:"+t._id+":open",oi,{type:"evtUse",dir:"in",trans:t._id,wennIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,mk("+ und","weiteres Bedingungs-Event")));
    // ── DANN (mehrere): ein Join, N Commands — je Dann ein Command-Ausgang + eigene Kompensation ──
    (t.dann||[]).forEach((d,di)=>{
      if(d.rufe!==undefined){
        // Aufruf-Knoten: die Funktion steht ausdrücklich da (Rufe<F>), ihre Ergebnisse sind die nächsten Events im Graph.
        // ⊕ → eine Funktions-Karte anklicken; „＋" legt eine neue Funktion an (Auftrag/Ergebnis an IHRER Karte).
        const fa=port("auftrag");fa.classList.add("o");fa.title=d.rufe||"(Funktion)";reg("tr:rufe:"+t._id+":"+di,fa,{type:"rufe",dir:"out",trans:t._id,dannIdx:di});
        const fk=MODEL.funktionen.find(f=>f.name===d.rufe);
        body.append(h("div",{class:"slotrow o"},
          h("button",{class:"rm",onclick:()=>{t.dann.splice(di,1);if(!t.dann.length)t.dann.push({});render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.75"+(fk?";cursor:pointer":""),title:fk?"Klick: Funktions-Karte öffnen":"",onclick:fk?()=>zeigeLadend("fk:"+fk._id):null},
            "Dann rufe "+(fk?"ƒ "+fk.name:d.rufe?"⚠ "+d.rufe:"— ⊕ Funktion")),
          d.rufe?null:neuKnopf("Neue Funktion anlegen und hier rufen",()=>{const nm=neueFunktion((MODEL.sagas.find(x=>x.name===t.prozess)||{}).namespace||defaultNsGlobal());
            verbindeInfo({type:"rufe",dir:"out",trans:t._id,dannIdx:di},{type:"rufe",dir:"in",fk:MODEL.funktionen.find(x=>x.name===nm)._id});}),fa));
        if(fk)body.append(h("div",{class:"gsec",style:"opacity:.7"},"Auftrag "+(fk.auftrag||"?")+" → "+((fk.ergebnisse||[]).join(" | ")||"(kein Ergebnis)")));
        if(d.sendeAusdruck)body.append(h("div",{class:"gsec",title:"aus dem Code gelesen — wird verbatim zurückgeschrieben",style:"font-family:monospace;opacity:.7;white-space:pre-wrap"},"λ "+d.sendeAusdruck));
        body.append(regelCodePort(t,d,di,"sende"));
        argTabelle(body,t,d);
        body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"⏳"),inp(d.zeitlimit,v=>{if(v)d.zeitlimit=v;else delete d.zeitlimit;},"Zeitlimit, z. B. TimeSpan.FromSeconds(30)")));
        const ko2=port("rejection");ko2.classList.add("o");ko2.title=d.kompensation||"(Kompensation)";reg("tr:komp:"+t._id+":"+di,ko2,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"komp"});
        body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.55"},"↩ "+(d.kompensation||"")),ko2));
        if(d.kompensation)body.append(regelCodePort(t,d,di,"komp"));
        return;}
      const so=port("command");so.classList.add("o");so.title=d.sende||"(Command)";reg("tr:sende:"+t._id+":"+di,so,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"sende"});
      body.append(h("div",{class:"slotrow o"},
        h("button",{class:"rm",onclick:()=>{t.dann.splice(di,1);if(!t.dann.length)t.dann.push({});render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.75"},"Dann "+(d.sende||"")),
        mk("×N","Fan-out (SendeJe) — N Commands je Element",!!d.sendeJe,()=>{d.sendeJe=d.sendeJe?undefined:true;render();}),so));
      if(d.sendeAusdruck)body.append(h("div",{class:"gsec",title:"aus dem Code gelesen — wird verbatim zurückgeschrieben",style:"font-family:monospace;opacity:.7;white-space:pre-wrap"},"λ "+d.sendeAusdruck));
      if(d.sende)body.append(regelCodePort(t,d,di,"sende"));
      argTabelle(body,t,d);
      if(d.sende||d.zeitlimit)body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"⏳"),inp(d.zeitlimit,v=>{if(v)d.zeitlimit=v;else delete d.zeitlimit;},"Zeitlimit (optional), z. B. TimeSpan.FromMinutes(5)")));
      const ko=port("rejection");ko.classList.add("o");ko.title=d.kompensation||"(Kompensation)";reg("tr:komp:"+t._id+":"+di,ko,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"komp"});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.55"},"↩ "+(d.kompensation||"")),ko));
      if(d.kompensation)body.append(regelCodePort(t,d,di,"komp"));
    });
    body.append(h("button",{class:"add",onclick:()=>{(t.dann=t.dann||[]).push({});render();}},"+ Dann (weiterer Command am selben Join)"));
    body.append(h("button",{class:"add",onclick:()=>{(t.dann=t.dann||[]).push({rufe:""});render();}},"+ Dann ƒ (Katalog-Funktion rufen)"));
    // Je Dann → eine Regel (gleiche Bedingung). Argument-Bau = Feld-Mapping (D/S) → default-Stub; der Bau-Lambda ist Logik und hat
    //   deshalb wie jedes Logik-Element 📝 + optional 🤖 (regelCodePort) — ein gefüllter Block ist der Lambda verbatim.
  }
  function regelCodePort(t,d,di,teil){const sg=MODEL.sagas.find(x=>x.name===t.prozess);
    const imCode=!!(sg&&sg.datei&&(teil==="komp"?d.kompensationAusdruck:d.sendeAusdruck));
    return codePort("trl:"+t._id+":"+di+":"+teil,{k:"regelLambda",trans:t._id,dannIdx:di,teil},null,
      teil==="komp"?"λ Kompensation":d.rufe!==undefined?"λ Auftrag":"λ Command",imCode);}

  // ── Aktionen ──
  window.deOpen=function(){document.getElementById("de").classList.add("on");if(!MODEL.records.length&&!MODEL.aggregate.length)deBoot();else render();};
  window.deClose=function(){document.getElementById("de").classList.remove("on");};
  // Standalone (/editor): sofort zeigen; Boot lädt das gespeicherte Board (nicht mehr leer).
  window.deShow=function(){document.getElementById("de").classList.add("on");};
  window.deLeer=function(){MODEL=normalize(null);GSTAND=schnappschuss();render();};
  window.dePing=async function(){try{const r=await fetch("/api/editor/model");badge(r.ok);}catch(e){badge(false);}};
  async function loadLive(){try{const r=await fetch("/api/editor/model");if(!r.ok)throw 0;const m=await r.json();badge(true);return m;}catch(e){badge(false);return null;}}
  function badge(live){const b=document.getElementById("de-badge");b.textContent=live?"Werkbank live":"offline";b.className="badge"+(live?"":" off");}
  // ↻ Vom Graph laden: den Code neu einlesen (warmer Workspace) und das Board übernehmen — der Code ist die einzige Wahrheit;
  //   Offenes geht vorher als Geste hinaus, nur die Ansicht (Positionen, 🤖) bleibt.
  window.deReload=async function(opt){opt=opt||{};await gesteFlush();
    if(!opt.ohneExtract){deFlash("↻ lese Code neu ein …",true);
      try{const r=await fetch("/api/editor/extract",{method:"POST"});const x=await r.json();if(!x.ok)deFlash("⚠ Einlesen fehlgeschlagen: "+(x.grund||""),false);}
      catch(e){badge(false);}}
    const live=await loadLive();if(!live)return;
    MODEL=uebernimm(live,MODEL);GSTAND=schnappschuss();render();};
  // ▦ Neu anordnen: alle Positionen verwerfen → aggregatsweises Kachel-Layout neu rechnen.
  window.deReflow=function(){if(VIEW.kompakt){KPOS[VIEW.details?"d":"k"]={};speichereKpos();}else graphNodes().forEach(n=>{delete n.ref.x;delete n.ref.y;});render();};

  function deFlash(txt,ok){const b=document.getElementById("de-badge");if(!b)return;const alt=b.textContent,ac=b.className;
    b.textContent=txt;b.className="badge"+(ok?"":" off");setTimeout(()=>{b.textContent=alt;b.className=ac;},1600);}
  // Boot: der aus C# gelesene Stand ist die WAHRHEIT (das Board kommt aus dem warmen Workspace); die Ansicht (Positionen, Filter, 🤖)
  //   kommt aus ihrer eigenen Datei.
  window.deBoot=async function(){
    await ansichtLaden();
    const live=await loadLive();
    schluesselFuer();
    // Blank-Start: leer, der Start-Dialog fragt, welche Domänen geladen werden (Vorauswahl: die letzte Wahl).
    LADEN=[];START_OFFEN=true;START_ERST=true;
    MODEL=uebernimm(live,null);GSTAND=schnappschuss();render();
  };

  // ══ GESTEN (Werkbank P7.2, docs/konzept-werkbank.md §8.2 + A.2): jede Bearbeitung ist ein Patch am Code. ══
  //   Nach einer Änderung (render → autosave) rechnet der Editor den Unterschied seiner Karten zum zuletzt gelesenen Code-Stand (GSTAND) —
  //   geänderte/neue Karten ganz, entfernte als Schlüssel — und schickt ihn als Geste. Der Server schreibt den Patch (Schreiber, Löschen am
  //   Ort), liest warm neu und schickt das Board zurück: das ist der neue Stand. Umbenennen ist eine eigene Geste (Roslyn-Renamer), ↶ nimmt
  //   den letzten Patch zurück. Solange ein Eingabefeld den Fokus hat, wartet die Geste (sonst ein Patch je Tastendruck).
  const GSAMMLUNGEN=["records","enums","aggregate","decider","applier","sagas","fluesse","funktionen","dienste","akteure","clients","readModels","stores","projektionen","reaktionen","reader"];
  // Natürliche Schlüssel — dieselbe Regel wie der Server (GraphExtractor.KartenSchluessel).
  const GKEY=c=>c==="decider"?(d=>(d.aggregat||"")+"|"+(d.command||"")):c==="applier"?(a=>(a.aggregat||"")+"|"+(a.event||""))
    :["readModels","stores","projektionen","reaktionen","reader"].includes(c)?(x=>x.name||""):(x=>(x.namespace||"")+"|"+(x.name||""));
  // Verglichen wird nur, was der Code besitzen kann: keine Ansicht (x/y), keine Board-Fakten (_…), keine Code-Knoten-Bindungen und
  //   keine Rümpfe — die spiegelt der Code-Sync aus der Datei (sie gehören dem Code; eine NEUE Karte geht ohnehin ganz hinaus).
  const GOHNE=new Set(["x","y","ausCode","rumpf","leer"]);
  const gVergleich=o=>JSON.stringify(o,(k,v)=>(GOHNE.has(k)||k.startsWith("_"))?undefined:v);
  // var: render() fragt den Stand ab, ehe dieser Abschnitt durchlaufen ist (let wäre dort noch nicht initialisiert).
  var GSTAND=null,G_TIMER=null,G_LAEUFT=false,G_AENDERUNG=0,G_TITEL=null,G_BOARD=null;
  function schnappschuss(){const m=payload(),s={};
    GSAMMLUNGEN.forEach(c=>{const x=new Map(),k=GKEY(c);(m[c]||[]).forEach(o=>x.set(k(o),{karte:o,vgl:gVergleich(o)}));s[c]=x;});return s;}
  function gDelta(neu,alt){const karten={},entfernt={},neueNs=new Set();let n=0;
    GSAMMLUNGEN.forEach(c=>{const a=alt[c]||new Map(),b=neu[c]||new Map();
      b.forEach((v,k)=>{const w=a.get(k);if(w&&w.vgl===v.vgl)return;(karten[c]=karten[c]||[]).push(v.karte);n++;if(!w&&v.karte.namespace)neueNs.add(v.karte.namespace);});
      a.forEach((v,k)=>{if(!b.has(k)){(entfernt[c]=entfernt[c]||[]).push(k);n++;}});});
    return n?{karten,entfernt,neueNs:[...neueNs]}:null;}
  // Ohne ausdrücklichen Titel: was die Geste an welchen Karten tut (＋ neu, ✎ geändert, ✕ entfernt).
  function gBeschreibe(d){const t=[];
    Object.values(d.karten).flat().forEach(k=>t.push((k.ausCode?"✎ ":"＋ ")+(k.name||k.command||k.event||"?")));
    Object.values(d.entfernt).flat().forEach(k=>t.push("✕ "+k.split("|").pop()));
    return t.slice(0,3).join(", ")+(t.length>3?" …":"");}
  // Diagnose (Konsole): was der Editor gerade als Geste schicken würde — je Karte die abweichenden Felder.
  window.deGestenDelta=()=>{if(!GSTAND)return null;const jetzt=schnappschuss(),d=gDelta(jetzt,GSTAND);if(!d)return null;
    const felder=(c,k)=>{const a=(GSTAND[c].get(GKEY(c)(k))||{}).karte;if(!a)return "neu";
      return Object.keys({...a,...k}).filter(f=>!GOHNE.has(f)&&!f.startsWith("_")&&JSON.stringify(a[f])!==JSON.stringify(k[f]));};
    return {karten:Object.fromEntries(Object.entries(d.karten).map(([c,ks])=>[c,ks.map(k=>({key:GKEY(c)(k),felder:felder(c,k)}))])),entfernt:d.entfernt};};
  // Was die nächste Geste tut (die erste Beschreibung gewinnt) — Titel im Verlauf (↶).
  function gTitel(t){if(!G_TITEL)G_TITEL=t;}
  function gFokus(){const a=document.activeElement;return !!(a&&/^(INPUT|TEXTAREA|SELECT)$/.test(a.tagName)&&a.type!=="checkbox"&&a.closest&&a.closest("#de"));}
  function autosave(){G_AENDERUNG++;if(G_TIMER)clearTimeout(G_TIMER);G_TIMER=setTimeout(gesteSenden,250);}
  document.addEventListener("focusout",()=>setTimeout(()=>{if(gFokus())return;if(G_BOARD&&!G_LAEUFT)gBoardAnwenden();autosave();},0));
  async function gesteSenden(){G_TIMER=null;if(!GSTAND||G_LAEUFT||gFokus())return;
    const jetzt=schnappschuss(),delta=gDelta(jetzt,GSTAND);if(!delta)return;
    const titel=G_TITEL||gBeschreibe(delta);G_TITEL=null;G_LAEUFT=true;const stand=G_AENDERUNG;
    try{const r=await fetch("/api/werkbank/geste",{method:"POST",headers:{"content-type":"application/json"},
        body:JSON.stringify({titel,karten:delta.karten,entfernt:delta.entfernt})});
      const a=await r.json();badge(true);
      if(!a.ok){deFlash("⚠ "+(a.fehler||"Geste nicht geschrieben"),false);G_BOARD=a.board;gBoardAnwenden();return;}
      // Neu Angelegtes bleibt sichtbar: seine Domäne zählt ab jetzt als geladen.
      if(LADEN&&delta.neueNs.length)delta.neueNs.forEach(ns=>{if(!LADEN.some(g=>drinNs(g,ns)))LADEN.push(ns);});
      gMeldung(a);GSTAND=jetzt;G_BOARD=a.board;await wbLadeDiagnosen();
      if(G_AENDERUNG===stand&&!gFokus())gBoardAnwenden();else autosave();}
    catch(e){badge(false);deFlash("⚠ Werkbank nicht erreichbar — Geste nicht geschrieben",false);}
    finally{G_LAEUFT=false;}}
  function gMeldung(a){const n=(a.dateien||[]).length;
    if(n)deFlash((/^[✎＋✕↶]/.test(a.titel)?"":"✎ ")+a.titel+" · "+n+" Datei"+(n===1?"":"en")+" · "+a.ms+" ms",true);
    (a.meldungen||[]).forEach((m,i)=>setTimeout(()=>deFlash("⚠ "+m,false),n?1700*(i+1):1700*i));
    if((a.meldungen||[]).length)console.log("Geste „"+a.titel+"“:",a);}
  // Das Board vom Server übernehmen (der neue Code-Stand) — nur die Ansicht des bisherigen Boards bleibt.
  function gBoardAnwenden(){const b=G_BOARD;G_BOARD=null;if(!b)return;const alt=SICHT().karten;MODEL=uebernimm(b,MODEL);GSTAND=schnappschuss();
    // Eine Karte, die bisher keinem Aggregat gehörte (geteilt) und jetzt einem gehört (z. B. gerade verbunden), zieht in die Spalte ihres
    //   Blocks um: ihre Position fällt weg, packLayout setzt sie neu. Etablierte Karten bleiben stehen.
    const L=KPOS[VIEW.details?"d":"k"]||{};Object.entries(SICHT().karten).forEach(([id,k])=>{const a=alt[id];
      if(a&&a.block===SHARED_KEY&&k.block!==SHARED_KEY){delete L[id];const n=graphNodes().find(m=>m.id===id);if(n){delete n.ref.x;delete n.ref.y;delete n.ref._kpos;}}});
    render();}
  // Offenes erst hinausschicken (vor Umbenennen, ↶, Neu lesen).
  async function gesteFlush(){if(G_TIMER){clearTimeout(G_TIMER);G_TIMER=null;}
    for(let i=0;i<200&&G_LAEUFT;i++)await new Promise(r=>setTimeout(r,50));
    if(document.activeElement&&gFokus())document.activeElement.blur();await gesteSenden();
    for(let i=0;i<200&&G_LAEUFT;i++)await new Promise(r=>setTimeout(r,50));}
  // Umbenennen: das Symbol am Ort der Karte samt aller Verweise (auch in Lambdas) — Roslyn-Renamer auf dem Server.
  async function gUmbenennen(o,neu,alt){await gesteFlush();G_LAEUFT=true;
    try{const r=await fetch("/api/werkbank/umbenennen",{method:"POST",headers:{"content-type":"application/json"},
        body:JSON.stringify({titel:"umbenennen "+alt+" → "+neu,datei:o._ort.datei,start:o._ort.start,neu})});
      const a=await r.json();
      if(!a.ok){deFlash("⚠ "+(a.fehler||"nicht umbenannt"),false);render();return;}
      if(SEL&&SEL.endsWith(":"+alt))SEL=SEL.slice(0,-alt.length)+neu;
      gMeldung(a);await wbLadeDiagnosen();G_BOARD=a.board;gBoardAnwenden();}
    catch(e){badge(false);deFlash("⚠ Werkbank nicht erreichbar — nicht umbenannt",false);}
    finally{G_LAEUFT=false;}}
  // ↶ — den letzten Patch zurücknehmen (Geste oder 🤖-Übernahme).
  window.deRueckgaengig=async function(){await gesteFlush();G_LAEUFT=true;
    try{const r=await fetch("/api/werkbank/rueckgaengig",{method:"POST"});const a=await r.json();
      if(!a.ok){deFlash("⚠ "+(a.fehler||"nichts zurückgenommen"),false);return;}
      deFlash(a.titel,true);await wbLadeDiagnosen();G_BOARD=a.board;gBoardAnwenden();}
    catch(e){badge(false);deFlash("⚠ Werkbank nicht erreichbar",false);}
    finally{G_LAEUFT=false;}};

  // ── Board übernehmen (Code-Stand) ───────────────────────────────────────────────────────────
  // Das Board kommt vollständig aus dem Code. Vom bisherigen Board bleibt nur, was der Code nicht kennt: Positionen (x/y).
  function hash(t){let h=5381;for(let i=0;i<t.length;i++)h=((h<<5)+h+t.charCodeAt(i))>>>0;return h.toString(36);}
  function maxId(m){let mx=0;JSON.stringify(m||{}).replace(/"_id":"[a-z_]*?(\d+)"/g,(_,n)=>{mx=Math.max(mx,+n);return _;});return mx;}
  function uebernimm(code,alt){
    NID=Math.max(NID,maxId(alt)+1);
    const neu=normalize(code?JSON.parse(JSON.stringify(code)):null);
    GSAMMLUNGEN.forEach(col=>(neu[col]||[]).forEach(x=>{x.ausCode=true;}));
    neu.transitions.forEach(t=>{t.ausCode=true;});
    if(!alt)return neu;
    alt=normalize(alt);
    GSAMMLUNGEN.forEach(col=>{const key=GKEY(col),altBy=new Map((alt[col]||[]).map(x=>[key(x),x]));
      (neu[col]||[]).forEach(l=>{const s=altBy.get(key(l));if(!s)return;if(s.x!=null){l.x=s.x;l.y=s.y;}});});
    // Transitionen (Saga-Regeln): Lage über die Regel-Signatur.
    const altTr=(alt.transitions||[]);
    neu.sagas.forEach(sg=>{const at=altTr.filter(t=>t.prozess===sg.name);if(!at.length)return;
      const sig=t=>JSON.stringify([t.wenn||[],t.sammelEvent||"",(t.dann||[]).map(d=>[d.sende||"",d.rufe||"",d.kompensation||""])]);
      const altBySig=new Map(at.map(t=>[sig(t),t]));
      neu.transitions.filter(t=>t.prozess===sg.name).forEach(t=>{const a=altBySig.get(sig(t));if(a&&a.x!=null){t.x=a.x;t.y=a.y;}});});
    // Abgeleitete Knoten: State-Knoten (Lage); die Rumpf-Kacheln sind abgeleitet (Lage über ihre Id in der Ansicht).
    const altStates=new Map((alt.states||[]).map(x=>[x.aggregat,x]));
    neu.states.forEach(x=>{const a=altStates.get(x.aggregat);if(a&&a.x!=null){x.x=a.x;x.y=a.y;}});
    return neu;}
  window.deDownload=function(){prepareSaga();const blob=new Blob([JSON.stringify(MODEL,null,2)],{type:"application/json"});
    const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download="domain-model.json";a.click();};
  // Das MODEL als Gesten-Stand (Schnappschuss): Rümpfe aus dem Code, ein leerer Rumpf bleibt leer.
  function payload(){prepareSaga();const m=JSON.parse(JSON.stringify(MODEL));
    [...m.decider,...m.applier].forEach(n=>{if(n.leer)n.rumpf="";});
    return m;}
  // Ausgabe-Panel: sobald Prüfen/Grammatik/Meldungen hineinschreiben, einblenden (mit ✕ zum Schließen).
  (function(){const out=document.getElementById("de-out");if(!out||!window.MutationObserver)return;
    new MutationObserver(()=>{if(out.querySelector(".outzu"))return;out.classList.add("zeigen");
      const zu=h("button",{class:"outzu",title:"Ausgabe schließen",onclick:()=>out.classList.remove("zeigen")},"✕");out.prepend(zu);})
      .observe(out,{childList:true});})();


  // ══ WERKBANK (docs/konzept-werkbank.md §5 + §8.1, Werkbank P3.6) ═════════════════════════════════════════════════════════════
  //   Kopfzeile: Umgebung ▾ · Build (Stand) · Lauf · Postgres/Redis/Consul · ⟳ · 📸. Der 🖥 Host-Rahmen zeigt die Abschnitte der
  //   gewählten Umgebung als Karten; ihr Panel zeichnet EIN generischer Formular-Renderer aus dem Schema (/api/werkbank/schema) —
  //   keine handgebauten Formulare je Karte. Befunde des letzten ⟳ (Compiler/Analyzer mit Typ + Member) und Boot-Fehler (mit Karte)
  //   stehen an der Karte, die sie betreffen. Alle Daten kommen fertig vom Server (Frontend-Bauregel P3): das JS kennt kein
  //   Umgebungs-Modell, nur das Schema und die Datei, wie sie ist.
  const WB={zustand:null,schema:null,umg:null,umgName:null,dirty:false,uhr:null,diag:[],boot:[],letzter:null,refs:{},ver:0,es:null};
  const HOST_ORDNUNG=["Umgebung","EventStore","VersionIndex","Cluster","Schreibpfad","Transport","Identitaeten","Quellen","Funktionen","Dienste","Konfig"];
  const HOST_SYM={Umgebung:"🏷",EventStore:"🗄",VersionIndex:"⚡",Cluster:"🕸",Schreibpfad:"✍",Transport:"🔗",Identitaeten:"🪪",Quellen:"📁",Funktionen:"ƒ",Dienste:"🧩",Konfig:"⚙",Uhr:"🕰"};
  function hostKnoten(){if(!WB.schema)return [];
    const ab=[...HOST_ORDNUNG.filter(k=>WB.schema[k]),...Object.keys(WB.schema).filter(k=>!HOST_ORDNUNG.includes(k)),"Uhr"];
    return ab.map(k=>({id:"host:"+k,name:(HOST_SYM[k]||"")+" "+k,kind:"host",ref:WB.refs[k]||(WB.refs[k]={abschnitt:k})}));}
  const wbJson=async(url,opt)=>{const r=await fetch(url,opt);if(!r.ok)throw new Error("HTTP "+r.status);return r.json();};
  const kurzWert=v=>v==null?"—":Array.isArray(v)?v.join(", "):typeof v==="object"?Object.keys(v).length+" Einträge":String(v);

  // ── Karte + Rahmen ──
  function hostKurz(n){const k=n.ref.abschnitt,box=h("div",{class:"gsum",onclick:()=>waehle(n.id)});
    if(k==="Uhr"){box.append(WB.zustand&&WB.zustand.art!=="Sim"?"Prod: Versatz gesperrt (0)":WB.uhr&&WB.uhr.versatz?"Versatz "+WB.uhr.versatz:"Versatz 0 (DB noch nicht angelegt)");return box;}
    const v=(WB.umg||{})[k],sch=(WB.schema||{})[k]||{};
    if(v==null){box.append(h("span",{class:"gp-mark"},"Standardwerte"));return box;}
    if(sch.art==="je"||sch.art==="frei"){const ks=Object.keys(v);box.append(ks.length?ks.join(", "):"—");return box;}
    Object.entries(v).slice(0,3).forEach(([a,b],i)=>{if(i)box.append(" · ");box.append(h("b",{},a),": "+kurzWert(b));});
    return box;}
  function hostRahmen(r){const z=WB.zustand||{},fr=h("div",{class:"grahmen host"});fr.dataset.ns=r.ns;
    Object.assign(fr.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});
    fr.style.setProperty("--dc","hsl(200 35% 62%)");
    const k=h("div",{class:"grahmen-k",title:"umgebungen/umgebung."+(z.aktiv||"?")+".json — bearbeitet wird im Panel, wirksam nach ⟳"},
      h("span",{class:"gr-t"},domLabel(r.ns)),
      h("span",{class:"gr-p"},[z.art,z.datenbank&&"DB "+z.datenbank,z.cluster&&"Cluster "+z.cluster,z.ports&&"gRPC :"+z.ports.grpc+(z.ports.einblick?" · Einblick :"+z.ports.einblick:"")].filter(Boolean).join(" · ")),
      h("button",{title:"Host-Rahmen einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"));
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};fr.append(k);return fr;}

  // ── Panel: EIN generischer Formular-Renderer (Schema-Knoten → Steuerelement) ──
  //   art klasse → Felder; art je → je Schlüssel eine Klasse (+ anlegen/entfernen); art frei → JSON; typ enum/bool/zahl/dauer/string/liste/map.
  //   Ein leeres Feld heißt „Standard" (der Schlüssel fällt aus der Datei); Platzhalter zeigen den Standardwert.
  function wbFeld(sch,wert,setze){if(!sch)return null;
    if(sch.art==="klasse")return wbKlasse(sch,wert||{},setze);
    if(sch.art==="je")return wbJe(sch,wert||{},setze);
    if(sch.art==="frei")return wbFrei(sch,wert,setze);
    const t=sch.typ,ph=sch.standard!=null?String(sch.standard):"";
    if(t==="enum"||t==="bool"){const werte=t==="bool"?["true","false"]:sch.werte||[];
      const sel=h("select",{onchange:e=>{const v=e.target.value;setze(v===""?undefined:t==="bool"?v==="true":v);}},
        h("option",{value:""},"— Standard"+(ph?" ("+ph+")":"")+" —"),...werte.map(w=>h("option",{value:w},w)));
      sel.value=wert==null?"":String(wert);return sel;}
    if(t==="zahl")return h("input",{type:"number",value:wert??"",placeholder:ph,oninput:e=>setze(e.target.value===""?undefined:Number(e.target.value))});
    // Guid und Zeitpunkt (Formulare aus dem Code, P5): Text mit Knopf für einen frischen Wert; Guids schlägt die Liste der zuletzt gesehenen vor.
    if(t==="guid"||t==="zeit"){const i=h("input",{value:wert??"",list:t==="guid"?"ds-ids":null,placeholder:t==="guid"?"Guid":"2026-10-12T10:00:00+02:00",
        oninput:e=>setze(e.target.value.trim()===""?undefined:e.target.value.trim())});
      return h("span",{class:"wb-mit"},i,h("button",{class:"act klein",title:t==="guid"?"neue Guid":"jetzt",
        onclick:()=>{i.value=t==="guid"?crypto.randomUUID():new Date().toISOString();setze(i.value);}},t==="guid"?"🎲":"jetzt"));}
    if(t==="liste"){const el=(sch.element||{}).typ,wandle=x=>el==="zahl"?Number(x):el==="bool"?x==="true":x;
      return h("input",{value:Array.isArray(wert)?wert.join(", "):wert??"",placeholder:ph||"kommagetrennt"+(el&&el!=="string"?" ("+el+")":""),
      oninput:e=>{const v=e.target.value.trim();setze(v?v.split(",").map(x=>x.trim()).filter(Boolean).map(wandle):undefined);}});}
    if(t==="map")return wbMap(wert||{},setze);
    return h("input",{value:wert??"",placeholder:ph||(t==="dauer"?"hh:mm:ss":""),title:t==="dauer"?"Dauer (hh:mm:ss)":"",oninput:e=>setze(e.target.value===""?undefined:e.target.value)});}
  function wbKlasse(sch,obj,setze){const box=h("div",{class:"wb-klasse"});
    Object.entries(sch.felder||{}).forEach(([k,f])=>{
      const sub=v=>{const neu={...obj};if(v===undefined)delete neu[k];else neu[k]=v;obj=neu;setze(Object.keys(neu).length?neu:undefined);};
      if(f.art==="klasse")box.append(h("div",{class:"wb-sub"},h("div",{class:"wb-st"},k),wbKlasse(f,obj[k]||{},sub)));
      else box.append(h("div",{class:"frow"},h("span",{class:"slotlbl",title:(f.clr?f.clr+" — ":"")+(f.hilfe||k)},k),wbFeld(f,obj[k],sub)));});
    return box;}
  function wbJe(sch,obj,setze){const box=h("div",{class:"wb-je"});
    const neu=v=>{obj=v;setze(Object.keys(v).length?v:undefined);};
    // Schlüssel aus dem Code (eintraege: je Dienst/Konfig-Record sein eigenes Schema) — das Formular kommt fertig vom Server.
    const eig=sch.eintraege||{};
    Object.keys(obj).forEach(name=>{
      const zeile=h("details",{class:"wb-eintrag",open:""},h("summary",{},name,eig[name]||!sch.eintraege?null:h("span",{class:"gp-mark",title:"Im Code gibt es keinen "+(sch.schluessel||"Eintrag")+" dieses Namens"}," ⚠ unbekannt"),
        h("button",{class:"rm",title:sch.schluessel+" "+name+" entfernen",onclick:e=>{e.preventDefault();const x={...obj};delete x[name];neu(x);zeigeInspector();}},"✕")),
        wbKlasse(eig[name]||sch.wert,obj[name]||{},v=>{neu({...obj,[name]:v||{}});}));
      box.append(zeile);});
    const frei=Object.keys(eig).filter(k=>!(k in obj));
    if(frei.length)box.append(h("div",{class:"gsec",style:"opacity:.7"},"aus dem Code, noch ohne Eintrag (Standard): "+frei.join(", ")));
    const ein=h("input",{placeholder:(sch.schluessel||"Name")+" …",list:frei.length?"wb-je-"+(sch.schluessel||"x"):null});
    if(frei.length)box.append(h("datalist",{id:"wb-je-"+(sch.schluessel||"x")},...frei.map(k=>h("option",{value:k}))));
    box.append(h("div",{class:"frow"},ein,h("button",{class:"act",onclick:()=>{const n=ein.value.trim();if(!n||obj[n])return;neu({...obj,[n]:{}});zeigeInspector();}},"＋ "+(sch.schluessel||"Eintrag"))));
    return box;}
  function wbMap(obj,setze){const box=h("div",{class:"wb-je"});
    const neu=v=>{obj=v;setze(Object.keys(v).length?v:undefined);};
    Object.entries(obj).forEach(([k,v])=>box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},k),
      h("input",{value:v??"",oninput:e=>neu({...obj,[k]:e.target.value})}),
      h("button",{class:"rm",onclick:()=>{const x={...obj};delete x[k];neu(x);zeigeInspector();}},"✕"))));
    const ein=h("input",{placeholder:"Schlüssel …"});
    box.append(h("div",{class:"frow"},ein,h("button",{class:"act",onclick:()=>{const n=ein.value.trim();if(!n||n in obj)return;neu({...obj,[n]:""});zeigeInspector();}},"＋")));
    return box;}
  function wbFrei(sch,wert,setze){const t=h("textarea",{class:"code",rows:8,title:sch.hilfe||""}),f=h("div",{class:"gm-fehler"});
    const leer=sch.liste?"[]":"{}";t.value=wert==null?leer:JSON.stringify(wert,null,2);
    t.oninput=()=>{try{const v=JSON.parse(t.value||leer);f.textContent="";setze(Object.keys(v).length?v:undefined);}catch(e){f.textContent="kein gültiges JSON: "+e.message;}};
    return h("div",{},sch.hilfe?h("div",{class:"gi-hint"},sch.hilfe):null,t,f);}
  function hostCard(body,n){if(!INSP)return;const k=n.ref.abschnitt,z=WB.zustand||{};
    if(k==="Uhr"){wbUhrPanel(body,z);return;}
    const sch=(WB.schema||{})[k];if(!sch||!WB.umg){body.append(h("div",{class:"gi-hint"},"Umgebung nicht geladen."));return;}
    const knopf=h("button",{class:"act go",onclick:wbSpeichern},"💾 In umgebung."+WB.umgName+".json schreiben");knopf.disabled=!WB.dirty;
    body.append(h("div",{class:"gi-hint"},"Werte von „"+WB.umgName+"“ ("+(z.art||"?")+") — leer = Standard; ${branch} und ${env:NAME} werden beim Start ersetzt. Wirksam nach ⟳."),
      wbFeld(sch,WB.umg[k],v=>{const neu={...WB.umg};if(v===undefined)delete neu[k];else neu[k]=v;WB.umg=neu;WB.dirty=true;knopf.disabled=false;}),
      h("div",{class:"frow"},knopf,h("button",{class:"act",title:"Änderungen verwerfen",onclick:async()=>{await wbLadeUmgebung();render();}},"↺")));}
  function wbUhrPanel(body,z){const sim=z.art==="Sim",ist=WB.uhr&&WB.uhr.versatz;
    body.append(h("div",{class:"gi-hint"},sim?"Die Uhr der Sim-DB: IDbClock = now() + Versatz (Fristen, Zeitlimits, Warten). Wirkt sofort, ohne Neustart.":"In "+(z.art||"Prod")+" ist der Versatz gesperrt (0)."));
    body.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Versatz"),h("b",{},ist||"0 (DB noch nicht angelegt)")));
    if(!sim)return;
    const ein=h("input",{value:ist||"00:00:00",placeholder:"d.hh:mm:ss"});
    const setze=async v=>{const r=await wbJson("/api/werkbank/uhr?versatz="+encodeURIComponent(v),{method:"POST"});if(!r.ok)deFlash("⚠ "+r.fehler,false);await wbLadeUhr();render();};
    body.append(h("div",{class:"frow"},ein,h("button",{class:"act",onclick:()=>setze(ein.value.trim())},"Setzen")),
      h("div",{class:"frow"},...[["+1 h","01:00:00"],["+1 Tag","1.00:00:00"],["+7 Tage","7.00:00:00"],["0","00:00:00"]].map(([t,v])=>
        h("button",{class:"act",onclick:()=>setze(v)},t))));}
  async function wbSpeichern(){try{await wbJson("/api/werkbank/umgebung/"+encodeURIComponent(WB.umgName),{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(WB.umg)});
      WB.dirty=false;deFlash("💾 umgebung."+WB.umgName+".json geschrieben — wirksam nach ⟳",true);await wbZustand();render();}
    catch(e){deFlash("⚠ nicht geschrieben: "+e.message,false);}}

  // ── Befunde an der Karte: Compiler/Analyzer (Typ + Member aus dem Syntaxbaum) und Boot-Fehler (Karte im Wirker-Format) ──
  // Die Karte zu einer stabilen Id (Werkbank P4.8): Wirker-Ids und natürliche Schlüssel liefert der Server als _id (decide:A/C, fluss:P#k,
  //   prozess:P#A→C, funktion:F, projektion:P …); Karten mit Namens-Id (rec:/agg:/enum:/saga:/host:) sind schon die Karten-Id.
  //   Eine Quelle ist ihr Nachrichten-Record, eine Pipeline ihr erster Knoten.
  function karteVonId(id){if(!id)return null;if(NODEBY.has(id))return id;
    if(id.startsWith("quelle:"))return recByName(id.slice(7))?"rec:"+id.slice(7):null;
    if(id.startsWith("pipeline:")){const f=MODEL.fluesse.find(x=>x._id===id),k=f&&(f.knoten||[])[0];return k?"fs:"+k._id:null;}
    const n=graphNodes().find(x=>x.ref&&x.ref._id===id&&x.kind!=="host");return n?n.id:null;}
  function wbZiel(d){
    if(d.karte)return d.karte.startsWith("host:")?d.karte:karteVonId(d.karte);
    // Typ, den das Board beim Bauen noch nicht kannte (neu angelegt): über den Namen.
    const t=d.typ;if(!t)return null;if(recByName(t))return "rec:"+t;
    const n=graphNodes().find(x=>x.kind!=="host"&&(x.name===t||(x.ref&&x.ref.name===t)));return n?n.id:null;}
  let DIAGC=null;
  function wbDiagListe(){if(DIAGC&&DIAGC.m===MODEL&&DIAGC.v===WB.ver)return DIAGC.l;
    const l=[...WB.diag.map(d=>({ziel:wbZiel(d),text:"["+d.code+"] "+d.text+" — "+d.datei+":"+d.zeile,art:d.schwere==="warning"?"warnung":"fehler",d})),
             ...WB.boot.map(b=>({ziel:wbZiel(b),text:"Boot: "+b.text,art:"fehler",d:b}))];
    DIAGC={m:MODEL,v:WB.ver,l};return l;}
  function diagFuer(id){if(!WB.diag.length&&!WB.boot.length)return [];
    return wbDiagListe().filter(x=>x.ziel&&(x.ziel===id||vertreterId(x.ziel)===id));}

  // ── Kopfzeile ──
  function wbChip(text,art,title,onclick){return h("span",{class:"wb-chip "+(art||""),title:title||"",onclick:onclick||null,style:onclick?"cursor:pointer":""},text);}
  function wbKopf(){const k=document.getElementById("wb-kopf");if(!k)return;const z=WB.zustand;k.textContent="";
    if(!z){k.append(wbChip("Werkbank nicht erreichbar","rot"));return;}
    const sel=h("select",{class:"wb-umg",title:"Umgebung — wählt Ports, DB und Cluster; der 🖥 Host-Rahmen zeigt ihre Werte",onchange:e=>wbAktiv(e.target.value)},
      ...z.umgebungen.map(n=>h("option",{value:n},n)));sel.value=z.aktiv;
    k.append(h("span",{class:"wb-l"},"Umgebung"),sel);
    const b=z.build;let bt,ba;
    if(!b){bt="○ nicht gebaut";ba="grau";}else if(b.ok){bt="● Build grün ("+b.stand+")";ba="gut";}else{bt="● Build rot · "+b.fehler+" Fehler";ba="rot";}
    if(b&&b.stand!==z.stand){bt+=" · Entwurf ≠ Build";if(ba==="gut")ba="gelb";}
    k.append(wbChip(bt,ba,"Stand des Codes: "+z.stand+(b?" · letzter ⟳: "+new Date(b.zeit).toLocaleTimeString():"")+" — Klick: Meldungen",wbMeldungen));
    const host=z.host||{};let lt,la;
    if(host.laeuft){lt="● Läuft · "+(z.cluster||"");la="gut";
      if(z.laufStand&&z.laufStand!==z.stand){lt+=" · Entwurf ≠ Lauf";la="gelb";}
      if(z.umgebungGeaendert){lt+=" · Konfig geändert";la="gelb";}}
    // Exit 0 bzw. 143 (SIGTERM) = angehalten; alles andere ist ein Absturz bzw. ein Boot-Fehler.
    else if(host.exitCode!=null&&host.exitCode!==0&&host.exitCode!==143){lt="● beendet (Exit "+host.exitCode+")"+((host.boot||[]).length?" · "+host.boot.length+" Boot-Fehler":"");la="rot";}
    else{lt=z.art==="Sim"?"○ Host steht":"○ "+(z.art||"")+" — läuft über das Deployment";la="grau";}
    k.append(wbChip(lt,la,host.laeuft?"PID "+host.pid+" seit "+new Date(host.seit).toLocaleTimeString()+" — Klick: Log":"Klick: Log",wbLog));
    (z.infra||[]).forEach(d=>k.append(wbChip(d.name+" "+(d.erreichbar?"●":"○"),d.erreichbar?"gut":d.betrieb==="Verwaltet"?"grau":"rot",
      d.host+":"+d.port+" · "+d.betrieb+(d.erreichbar?"":d.betrieb==="Verwaltet"?" — ⟳ startet ihn (Docker)":" — nicht erreichbar"))));
    const lauf=!!z.laeuft;
    // (disabled als Eigenschaft — h() setzt Attribute, und schon disabled="false" sperrt einen Knopf)
    const run=h("button",{class:"act go wb-run",title:"⟳ bauen → Python-Basen → neu lesen → Daten prüfen → Infrastruktur → Host ersetzen",onclick:()=>wbKreislauf(false)},lauf?"⟳ "+z.laeuft+" …":"⟳");
    const foto=h("button",{class:"act",title:"Sim-DB: Stand speichern / laden / zurücksetzen / Prod-Stand übernehmen",onclick:e=>wbStandMenue(e.clientX,e.clientY+14)},"📸");
    const vs=WB.uhr&&WB.uhr.versatz&&!/^0?0:00:00$/.test(WB.uhr.versatz)?WB.uhr.versatz:null;
    const vor=h("button",{class:"act",title:"⏩ Uhr der Sim-DB vorstellen — Fristen, Zeitlimits und Warten werden fällig"+(vs?" (Versatz "+vs+")":""),
      onclick:e=>dsUhrMenue(e.clientX,e.clientY+14)},"⏩"+(vs?" +"+vs:""));
    run.disabled=lauf;foto.disabled=lauf||z.art!=="Sim";vor.disabled=lauf||z.art!=="Sim";k.append(run,foto,vor);}
  async function wbZustand(){try{WB.zustand=await wbJson("/api/werkbank/zustand");}catch(e){WB.zustand=null;}wbKopf();}
  async function wbLadeUmgebung(){const n=WB.zustand&&WB.zustand.aktiv;if(!n)return;
    try{WB.umg=await wbJson("/api/werkbank/umgebung/"+encodeURIComponent(n));WB.umgName=n;WB.dirty=false;}catch(e){WB.umg=null;}}
  async function wbLadeUhr(){try{WB.uhr=await wbJson("/api/werkbank/uhr");}catch(e){WB.uhr=null;}}
  async function wbLadeDiagnosen(){try{const d=await wbJson("/api/werkbank/diagnosen");WB.diag=d.diagnosen||[];WB.boot=d.boot||[];WB.ver++;}catch(e){}}
  async function wbAktiv(name){if(WB.dirty&&!confirm("Ungespeicherte Änderungen an umgebung."+WB.umgName+".json verwerfen?")){wbKopf();return;}
    try{WB.zustand=await wbJson("/api/werkbank/aktiv/"+encodeURIComponent(name),{method:"POST"});}catch(e){}
    await Promise.all([wbLadeUmgebung(),wbLadeUhr()]);wbKopf();render();}

  // ── ⟳ der Kreislauf ──
  function wbOut(leeren){const out=document.getElementById("de-out");if(out&&leeren)out.innerHTML="";return out;}
  function wbZeile(out,text,art,klick){const z=h("div",{class:"find"+(art==="rot"?" error":art==="gelb"?" warning":""),style:(art==="gut"?"background:#1f3a2a;color:#9be3bf;":"")+(klick?"cursor:pointer":"")},text);
    if(klick)z.onclick=klick;out.append(z);return z;}
  function wbBericht(r){const out=wbOut(true);if(!out)return;
    wbZeile(out,(r.ok?"✓ ⟳ fertig":"✗ ⟳ angehalten")+" · Stand "+r.stand+" · "+new Date(r.zeit).toLocaleTimeString(),r.ok?"gut":"rot");
    (r.schritte||[]).forEach(s=>wbZeile(out,(s.ok?"✓ ":"✗ ")+s.name+(s.dauerMs?" ("+(s.dauerMs/1000).toFixed(1)+" s)":"")+(s.meldungen&&s.meldungen.length?" — "+s.meldungen.slice(0,4).join(" · "):""),s.ok?"":"rot"));
    wbDiagListe().forEach(x=>{const n=x.ziel&&NODEBY.get(vertreterId(x.ziel));
      wbZeile(out,(n?"→ "+(NODELABEL[n.kind]||n.kind)+" "+n.name+": ":"")+x.text,x.art==="warnung"?"gelb":"rot",n?()=>zeigeLadend(n.id):null);});}
  window.wbKreislauf=async function(zuruecksetzen){const out=wbOut(true);
    if(out)wbZeile(out,"⟳ bauen → Python-Basen → neu lesen → Daten prüfen → Infrastruktur → Host ersetzen …");
    let r;try{r=await wbJson("/api/werkbank/kreislauf"+(zuruecksetzen?"?zuruecksetzen=true":""),{method:"POST"});}
    catch(e){if(out)wbZeile(out,"⚠ Werkbank nicht erreichbar: "+e.message,"rot");return;}
    if(r.fehler){if(out)wbZeile(out,"✗ "+r.fehler,"rot");await wbZustand();return;}
    WB.letzter=r;WB.diag=r.diagnosen||[];WB.boot=r.boot||[];WB.ver++;
    if((r.schritte||[]).some(s=>s.name==="Neu lesen"&&s.ok))await deReload({ohneExtract:true});else render();
    wbBericht(r);await Promise.all([wbZustand(),wbLadeUhr()]);
    if(r.rueckfrage&&r.rueckfrage.length){const b=document.querySelector("#wb-kopf .wb-run"),rc=b?b.getBoundingClientRect():{left:200,bottom:60};
      menue(rc.left,rc.bottom+6,"Die Events der Sim-DB passen nicht mehr zum Code",[
        ...r.rueckfrage.slice(0,6).map(t=>({kopf:"• "+t})),
        {t:"↺ Sim-DB zurücksetzen und weiter",gefahr:true,title:"Schema-Evolution ist kein Ziel (§1): die Sim-DB wird geleert, gespeicherte Stände mit altem Stand werden ungültig",fn:()=>wbKreislauf(true)},
        {t:"Abbrechen",fn:()=>{}}]);}};
  function wbMeldungen(){if(WB.letzter)wbBericht(WB.letzter);else{const out=wbOut(true);if(out)wbZeile(out,"Noch kein ⟳ in dieser Sitzung.");}}
  async function wbLog(){const out=wbOut(true);if(!out)return;let l=[];try{l=await wbJson("/api/werkbank/host/log?max=200");}catch(e){}
    WB.boot.forEach(b=>wbZeile(out,"Boot: "+b.text+(b.karte?" ("+b.karte+")":""),"rot",()=>{const id=wbZiel(b);if(id)zeigeLadend(vertreterId(id));}));
    out.append(h("pre",{class:"wb-log"},l.length?l.join("\n"):"(kein Log — der Host lief in dieser Sitzung nicht)"));}

  // ── 📸 Sim-DB ──
  async function wbStandMenue(cx,cy){const z=WB.zustand;if(!z)return;let st=[];
    try{st=await wbJson("/api/werkbank/simdb/staende");}catch(e){}if(!Array.isArray(st))st=[];
    const sicher=(text,fn)=>()=>menue(cx,cy,text,[{t:"Ja, ausführen",gefahr:true,fn},{t:"Abbrechen",fn:()=>{}}]);
    menue(cx,cy,"📸 Sim-DB "+(z.datenbank||""),[
      {t:"📸 Stand speichern …",title:"CREATE DATABASE … TEMPLATE — der Host hält kurz an",fn:()=>frage(cx,cy,"📸 Stand speichern",
        "stand_"+new Date().toISOString().slice(5,16).replace(/[-T:]/g,""),"Name — Buchstaben, Ziffern, _",name=>{if(!name)return "Name fehlt";if(!/^[A-Za-z0-9_]+$/.test(name))return "Nur Buchstaben, Ziffern, _";wbSimDb("speichern",name);})},
      st.length?{kopf:"Stand laden"}:null,
      ...st.map(s=>({t:(s.gueltig?"⤓ ":"⚠ ")+s.name,aus:!s.gueltig,title:s.gueltig?"Sim-DB aus diesem Stand neu anlegen (Host hält kurz an)":"Passt nicht zum Code: "+s.gruende.join("; "),fn:()=>wbSimDb("laden",s.name)})),
      st.length?{kopf:"Stand löschen"}:null,
      ...st.map(s=>({t:"🗑 "+s.name,gefahr:true,fn:sicher("Stand „"+s.name+"“ löschen?",()=>wbSimDb("loeschen",s.name))})),
      {trenn:true},
      {t:"↺ Sim-DB zurücksetzen",gefahr:true,fn:sicher("Sim-DB "+z.datenbank+" leeren? (Der Host legt sie beim Start neu an.)",()=>wbSimDb("zuruecksetzen"))},
      {t:"⇩ Prod-Stand übernehmen",gefahr:true,title:"pg_dump der Prod-DB → Sim-DB (Siegel neu auf Sim, Uhr 0); die Prod-DB wird nur gelesen",
        fn:sicher("Sim-DB durch einen Abzug der Prod-DB ersetzen?",()=>wbSimDb("prod"))}]);}
  async function wbSimDb(aktion,stand){const out=wbOut(true);if(out)wbZeile(out,"📸 "+aktion+(stand?" „"+stand+"“":"")+" …");
    const t0=performance.now();let r;
    try{r=await wbJson("/api/werkbank/simdb/"+aktion+(stand?"?stand="+encodeURIComponent(stand):""),{method:"POST"});}catch(e){r={ok:false,fehler:e.message};}
    if(out){wbZeile(out,(r.ok?"✓ ":"✗ ")+aktion+(stand?" „"+stand+"“":"")+" · "+((performance.now()-t0)/1000).toFixed(1)+" s"+(r.fehler?" — "+r.fehler:"")+(r.host?" · Host "+r.host:""),r.ok?"gut":"rot");
      (r.meldungen||[]).forEach(m=>wbZeile(out,m));(r.boot||[]).forEach(b=>wbZeile(out,"Boot: "+b.text,"rot"));}
    await Promise.all([wbZustand(),wbLadeUhr(),wbLadeDiagnosen()]);render();}

  // ── Start: Schema, Zustand, Umgebung, Uhr, Befunde; Fortschritt per SSE, Lauf-Status alle 5 s ──
  async function wbStart(){try{WB.schema=await wbJson("/api/werkbank/schema");}catch(e){WB.schema=null;}
    await wbZustand();await Promise.all([wbLadeUmgebung(),wbLadeUhr(),wbLadeDiagnosen()]);render();
    if(window.EventSource&&!WB.es){WB.es=new EventSource("/api/werkbank/ereignisse");let t=null;
      WB.es.onmessage=e=>{let m;try{m=JSON.parse(e.data);}catch(_){return;}
        if(m.art==="schritt"){const out=wbOut(false),s=m.daten;if(out)wbZeile(out,(s.ok?"✓ ":"✗ ")+s.name+(s.dauerMs?" ("+(s.dauerMs/1000).toFixed(1)+" s)":""),s.ok?"":"rot");}
        if(m.art==="umgebung"&&m.daten&&m.daten.name===WB.umgName&&!WB.dirty)wbLadeUmgebung().then(render);
        if(m.art==="spur"){if(window.spSignal)window.spSignal();return;}
        clearTimeout(t);t=setTimeout(wbZustand,150);};}
    setInterval(()=>{if(!document.hidden)wbZustand();},5000);}
  {const boot=window.deBoot;window.deBoot=async function(){await boot();wbStart();};}

  // ══ DURCHSPIELEN (docs/konzept-werkbank.md §6, Werkbank P5) ════════════════════════════════════════════════════════════════════
  //   Nur echte Eingänge: als Akteur (Konsole → JSON-Eingang des Hosts hinter dem Akteur-Tor), Hand-Funktionen beantworten (⧗ an der
  //   Funktions-Karte), Dateien auf die Quellkarte (der echte Treiber meldet sie), ⏩ Uhr vor, ▶ externe Worker. Die Formulare zeichnet der
  //   generische Renderer aus dem Schema, das der Server aus dem gelesenen Code baut (/api/werkbank/formular) — kein Modell-Wissen im JS.
  const DS={ident:null,token:null,rechte:null,art:"command",typ:null,schemas:{},werte:{},verlauf:[],auftraege:[],handFn:[],auftragFehler:null,
    worker:[],offen:false,tab:"konsole",antwort:{},sig:"",lauf:"",ids:[]};
  const lsHole=k=>{try{return localStorage.getItem(k);}catch(e){return null;}}, lsSetze=(k,v)=>{try{localStorage.setItem(k,v);}catch(e){}};
  const hostLaeuft=()=>!!(WB.zustand&&WB.zustand.host&&WB.zustand.host.laeuft);
  const aktivName=()=>(WB.zustand||{}).aktiv||"?";
  async function dsSchema(typ){if(DS.schemas[typ])return DS.schemas[typ];
    const r=await fetch("/api/werkbank/formular?typ="+encodeURIComponent(typ)),j=await r.json();
    if(!r.ok)throw new Error(j.fehler||("HTTP "+r.status));return DS.schemas[typ]=j;}
  async function dsEingang(pfad,body){const opt={method:body?"POST":"GET",headers:{}};if(DS.token)opt.headers["akteur-token"]=DS.token;
    if(body){opt.headers["content-type"]="application/json";opt.body=JSON.stringify(body);}
    let r;try{r=await fetch("/api/werkbank/eingang/"+pfad,opt);}catch(e){return {ok:false,fehler:e.message,status:0};}
    let j;try{j=await r.json();}catch(e){j={ok:false,fehler:"HTTP "+r.status};}j.status=r.status;return j;}
  // Guids aus Antworten merken — das Formular schlägt sie vor (die Id eines eben angelegten Aggregats, die Treffer einer Query).
  function dsMerkeIds(obj,etikett){const roh=JSON.stringify(obj||{}),re=/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
    (roh.match(re)||[]).filter(g=>!/^0{8}-/.test(g)).forEach(g=>{DS.ids=[{id:g,t:etikett},...DS.ids.filter(x=>x.id!==g)].slice(0,40);});}
  const dsAls=()=>{const t=((DS.ident||{}).tokens||[]).find(x=>x.token===DS.token);return t?t.akteure.join(" + "):"(Tor offen)";};

  // ── Die Leiste unten (§8.1): Vorgänge · Zeitleiste · Konsole · Aufträge · Worker · Meldungen — und der Live-Puls an/aus ──
  function dsLeiste(){const el=document.getElementById("ds-leiste");if(!el)return;el.textContent="";el.classList.toggle("offen",DS.offen);
    el.append(dsTabs());
    el.append(h("datalist",{id:"ds-ids"},...DS.ids.map(x=>h("option",{value:x.id},x.t))));
    if(!DS.offen)return;
    const inhalt=h("div",{class:"ds-inhalt"});el.append(inhalt);
    const tab={konsole:dsKonsole,auftraege:dsAuftraegeTab,worker:dsWorkerTab,vorgaenge:spVorgaengeTab,zeit:spZeitTab,meldungen:spMeldungenTab}[DS.tab]||dsKonsole;
    tab(inhalt);}
  function dsTabs(){const n=DS.auftraege.length,wl=DS.worker.filter(w=>w.laeuft).length;
    const umschalten=id=>{if(DS.offen&&DS.tab===id)DS.offen=false;else{DS.offen=true;DS.tab=id;}lsSetze("ds.leiste",DS.offen?DS.tab:"");dsLeiste();};
    const tab=(id,t,titel)=>h("button",{class:"ds-tab"+(DS.offen&&DS.tab===id?" an":"")+(id==="auftraege"&&n?" warte":""),title:titel,onclick:()=>umschalten(id)},t);
    const fehler=WB.diag.filter(d=>d.schwere!=="warning").length+WB.boot.length;
    return h("div",{class:"ds-tabs"},
      tab("vorgaenge","☰ Vorgänge"+(SP.gewaehlt?" · "+spTitel():""),"Vorgänge aus dem Log des laufenden Hosts — wählen = Spur auf dem Board"),
      tab("zeit","⏱ Zeitleiste","Bahnen je Baustein; der Schieber spielt die Aufzeichnung des gewählten Vorgangs ab"),
      tab("konsole","⌨ Konsole · als Akteur","Command oder Query als Akteur an den echten Host (Token → Akteur-Tor)"),
      tab("auftraege","⧗ Aufträge"+(n?" ("+n+")":""),"Funktionen mit Laufort Hand: die Werkbank beantwortet ihre Aufträge"),
      tab("worker","▶ Worker"+(wl?" ("+wl+" läuft)":""),"Externe Funktions-Worker (Laufort Extern) starten und stoppen"),
      tab("meldungen","⚠ Meldungen"+(fehler?" ("+fehler+")":""),"Build- und Boot-Meldungen des letzten ⟳ — Klick führt zur Karte"),
      h("span",{class:"sp"}),
      h("button",{class:"ds-tab"+(SP.pulsAn?" an":""),title:"Live-Puls ohne gewählten Vorgang: Zähler und „zuletzt“ je Karte, Kantendicke nach Menge, Stau, nie benutzte Kanten blass",
        onclick:spPulsUmschalten},"〰 Puls "+(SP.pulsAn?"an":"aus")),
      h("span",{class:"ds-hint"},hostLaeuft()?"● echter Host · "+aktivName():"○ Host steht — ⟳ startet ihn"),
      h("button",{class:"ds-tab",title:DS.offen?"Leiste einklappen":"Leiste aufklappen",onclick:()=>umschalten(DS.tab)},DS.offen?"▾":"▴"));}

  // ── Konsole: als Akteur handeln ──
  function dsKonsole(box){const links=h("div",{class:"ds-links"}),rechts=h("div",{class:"ds-rechts"});box.append(links,rechts);
    const tokens=(DS.ident&&DS.ident.tokens)||[];
    if(!tokens.length)links.append(h("div",{class:"gi-hint"},"Keine Identitäten in „"+aktivName()+"“ — das Akteur-Tor ist offen. Tokens stehen im 🖥 Host-Rahmen unter Identitaeten."));
    else{const sel=h("select",{title:"Token → Akteur(e) (Abschnitt Identitaeten der Umgebung)",
        onchange:e=>{DS.token=e.target.value;lsSetze("ds.token",DS.token);DS.rechte=null;dsLeiste();dsRechte();}},
        ...tokens.map(t=>h("option",{value:t.token},t.akteure.join(" + ")+"  ·  "+t.token)));sel.value=DS.token||"";
      links.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Als"),sel));}
    const art=h("select",{onchange:e=>{DS.art=e.target.value;DS.typ=null;dsLeiste();}},h("option",{value:"command"},"Command"),h("option",{value:"query"},"Query"));art.value=DS.art;
    const liste=DS.rechte?(DS.art==="command"?DS.rechte.commands:DS.rechte.queries)||[]:[];
    const typ=h("select",{onchange:e=>{DS.typ=e.target.value||null;dsLeiste();}},
      h("option",{value:""},DS.rechte?(liste.length?"— wählen ("+liste.length+" erlaubt) —":"(nichts erlaubt)"):"…"),...liste.map(t=>h("option",{value:t},t)));typ.value=DS.typ||"";
    links.append(h("div",{class:"frow"},art,typ));
    if(DS.rechte&&DS.rechte.fehler)links.append(h("div",{class:"llmmeld err"},DS.rechte.fehler));
    if(DS.typ&&liste.includes(DS.typ)){const form=h("div",{class:"ds-form"},h("span",{class:"ds-zeit"},"Formular lädt …"));links.append(form);
      dsSchema(DS.typ).then(sch=>{form.textContent="";form.append(wbFeld(sch,DS.werte[DS.typ],v=>DS.werte[DS.typ]=v),
          h("div",{class:"frow"},h("button",{class:"act go",onclick:dsSende},"▶ "+(DS.art==="command"?"Senden":"Fragen")),
            h("button",{class:"act",title:"Formular leeren",onclick:()=>{delete DS.werte[DS.typ];dsLeiste();}},"↺"),
            h("button",{class:"act",title:"Karte auf dem Board zeigen",onclick:()=>{if(recByName(DS.typ))zeigeLadend("rec:"+DS.typ);}},"◎")));})
        .catch(e=>{form.textContent="";form.append(h("div",{class:"llmmeld err"},e.message));});}
    rechts.append(h("div",{class:"gsec"},"Verlauf — Antworten des echten Hosts"));
    if(!DS.verlauf.length)rechts.append(h("div",{class:"gi-hint"},"Noch nichts gesendet. Ein Command liefert die Events (oder die Ablehnung) seines Aggregats, eine Query die Antwort ihres Readers."));
    DS.verlauf.forEach(v=>rechts.append(dsVerlaufZeile(v)));}
  async function dsSende(){const typ=DS.typ,art=DS.art,daten=DS.werte[typ]||{};
    const v={zeit:new Date(),art,typ,als:dsAls(),daten,laeuft:true};DS.verlauf=[v,...DS.verlauf].slice(0,40);dsLeiste();
    v.r=await dsEingang(art,{typ,daten});v.laeuft=false;
    if(v.r.ok)dsMerkeIds(art==="command"?{id:v.r.aggregatId}:v.r.antwort,art==="command"?v.r.aggregat+" ("+typ+")":typ);
    dsLeiste();}
  function dsVerlaufZeile(v){const r=v.r||{};let kopf,art;
    if(v.laeuft){kopf="… "+v.typ;art="";}
    else if(r.abgelehnt){kopf="✕ "+v.typ+" — "+r.abgelehnt;art="rot";}
    else if(r.ablehnung){kopf="⚠ "+v.typ+" → abgelehnt: "+r.ablehnung.typ;art="gelb";}
    else if(r.ok&&v.art==="command"){kopf="✓ "+v.typ+" → "+((r.events||[]).map(e=>e.typ).join(", ")||"kein Event")+"  ·  "+r.aggregat+" v"+r.version;art="gut";}
    else if(r.ok){kopf="✓ "+v.typ+" → "+(r.antwort?r.antwort.typ:"?");art="gut";}
    else{kopf="✕ "+v.typ+" — "+(r.fehler||("HTTP "+r.status));art="rot";}
    const d=h("details",{class:"ds-vz "+art},h("summary",{},h("span",{class:"ds-zeit"},v.zeit.toLocaleTimeString()+" · "+v.als),kopf));
    const {status,...ohne}=r;d.append(h("pre",{class:"wb-log"},JSON.stringify({gesendet:{typ:v.typ,daten:v.daten},antwort:ohne},null,2)));
    return d;}

  // ── Aufträge (Laufort Hand) ──
  function dsAuftraegeTab(box){
    if(DS.auftragFehler){box.append(h("div",{class:"gi-hint"},DS.auftragFehler));return;}
    if(!DS.handFn.length){box.append(h("div",{class:"gi-hint"},"Keine Funktion mit Laufort Hand in „"+aktivName()+"“. Im 🖥 Host-Rahmen unter Funktionen den Laufort einer Funktion auf Hand stellen und ⟳ — dann beantwortest du ihre Aufträge hier (oder im Panel der Funktions-Karte)."));return;}
    const spalte=h("div",{class:"ds-auftraege"});box.append(spalte);
    spalte.append(h("div",{class:"gi-hint",style:"flex-basis:100%"},"Laufort Hand: "+DS.handFn.join(", ")+" — die Werkbank ist der Ausführer; die Antwort wird wie jedes Funktions-Ergebnis genau einmal ins Log geschrieben."));
    if(!DS.auftraege.length)spalte.append(h("div",{class:"gi-hint"},"Keine offenen Aufträge."));
    DS.auftraege.forEach(a=>spalte.append(dsAuftragBox(a)));}
  function dsAuftragBox(a){const st=DS.antwort[a.vorgang]||(DS.antwort[a.vorgang]={typ:a.ergebnisse[0],werte:{}});
    const box=h("div",{class:"ds-auftrag"});
    box.append(h("div",{class:"ds-ak"},h("b",{},"⧗ ƒ "+a.funktion),h("span",{class:"ds-zeit"},"  seit "+new Date(a.seit).toLocaleTimeString()+(a.akteur?" · im Auftrag von "+a.akteur:""))));
    box.append(h("div",{class:"ds-auf",title:"Vorgang "+a.vorgang+(a.ursache?" · Ursache "+a.ursache:"")},h("span",{class:"slotlbl"},a.auftrag.typ),h("code",{},JSON.stringify(a.auftrag.daten))));
    const sel=h("select",{onchange:e=>{st.typ=e.target.value;box.replaceWith(dsAuftragBox(a));}},...a.ergebnisse.map(t=>h("option",{value:t},t)));sel.value=st.typ;
    box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Ergebnis"),sel));
    const form=h("div",{class:"ds-form"});box.append(form);
    dsSchema(st.typ).then(sch=>{
      // Bequemlichkeit: gleichnamige Felder des Auftrags vorbelegen (z. B. Pfad) — überschreibbar.
      if(st.werte[st.typ]===undefined){const vor={};Object.keys(sch.felder||{}).forEach(k=>{const q=Object.keys(a.auftrag.daten||{}).find(x=>x.toLowerCase()===k.toLowerCase());if(q)vor[k]=a.auftrag.daten[q];});
        st.werte[st.typ]=Object.keys(vor).length?vor:undefined;}
      form.append(wbFeld(sch,st.werte[st.typ],v=>st.werte[st.typ]=v));}).catch(e=>form.append(h("div",{class:"llmmeld err"},e.message)));
    const meld=h("div");
    box.append(h("div",{class:"frow"},h("button",{class:"act go",onclick:()=>dsAntworte(a,{typ:st.typ,daten:st.werte[st.typ]||{}},meld)},"✓ Antworten"),
      h("button",{class:"act",title:"Der Schritt scheitert — die Fehlschlag-Marke steht im Log, wie bei einer werfenden Funktion",
        onclick:()=>{const g=prompt("Warum scheitert der Schritt?","von Hand abgelehnt");if(g)dsAntworte(a,{fehler:g},meld);}},"✕ Scheitern lassen")),meld);
    return box;}
  async function dsAntworte(a,body,meld){meld.textContent="…";const r=await dsEingang("ergebnis",{vorgang:a.vorgang,...body});
    if(r.ok){delete DS.antwort[a.vorgang];deFlash("✓ ƒ "+a.funktion+": "+(body.fehler?"gescheitert":body.typ)+" — der Ablauf läuft weiter",true);await dsTakt(true);}
    else{meld.textContent="";meld.append(h("div",{class:"llmmeld err"},r.fehler||r.ausgang||("HTTP "+r.status)));}}

  // ── Worker (Laufort Extern) ──
  function dsWorkerTab(box){const ws=DS.worker;
    if(!ws.length){box.append(h("div",{class:"gi-hint"},"Keine Funktionen in „"+aktivName()+"“ gebunden."));return;}
    const t=h("div",{class:"ds-worker"});ws.forEach(w=>t.append(dsWorkerZeile(w)));box.append(t);}
  function dsWorkerZeile(w){const z=h("div",{class:"ds-wz"});
    z.append(h("b",{},"ƒ "+w.funktion),h("span",{class:"wb-chip"+(w.laufort==="Extern"?"":" grau")},w.laufort));
    if(w.laufort!=="Extern"){z.append(h("span",{class:"ds-zeit"},w.laufort==="Host"?"rechnet im Host (C#-Implementierung)":"die Werkbank antwortet (⧗ Aufträge)"));return z;}
    if(!w.worker){z.append(h("span",{class:"ds-zeit"},"kein Worker konfiguriert (Funktionen:"+w.funktion+":Worker)"));return z;}
    z.append(h("code",{},w.worker));
    z.append(w.laeuft?h("span",{class:"wb-chip gut"},"● läuft · PID "+w.pid):w.exitCode!=null?h("span",{class:"wb-chip rot"},"beendet (Exit "+w.exitCode+")"):h("span",{class:"wb-chip grau"},"○ steht"));
    z.append(w.laeuft?h("button",{class:"act",onclick:()=>dsWorker(w.funktion,"stopp")},"■ Stoppen"):h("button",{class:"act go",onclick:()=>dsWorker(w.funktion,"start")},"▶ Worker starten"));
    z.append(h("button",{class:"act",title:"Ausgabe des Workers",onclick:()=>dsWorkerLog(w.funktion)},"Log"));
    if(w.zuletzt)z.append(h("div",{class:"ds-zeit",style:"flex-basis:100%;overflow-wrap:anywhere"},"› "+w.zuletzt));
    return z;}
  async function dsWorker(f,aktion){let r;try{r=await wbJson("/api/werkbank/worker/"+encodeURIComponent(f)+"/"+aktion,{method:"POST"});}catch(e){r={ok:false,fehler:e.message};}
    if(!r.ok){deFlash("⚠ Worker "+f+": "+(r.fehler||"?"),false);const out=wbOut(true);if(out){wbZeile(out,"✗ Worker "+f+": "+r.fehler,"rot");(r.log||[]).forEach(l=>wbZeile(out,l));}}
    else deFlash(aktion==="start"?"▶ Worker "+f+" läuft (PID "+r.pid+")":"■ Worker "+f+" gestoppt",true);
    await dsTakt(true);}
  async function dsWorkerLog(f){const out=wbOut(true);if(!out)return;let l=[];try{l=await wbJson("/api/werkbank/worker/"+encodeURIComponent(f)+"/log");}catch(e){}
    wbZeile(out,"▶ Worker "+f);out.append(h("pre",{class:"wb-log"},l.length?l.join("\n"):"(keine Ausgabe)"));}

  // ── An den Karten: Funktion (⧗ n, Worker) und Quelle (Datei ablegen) ──
  function dsBadge(n){if(n.kind!=="funktion"||!n.ref)return null;
    const a=DS.auftraege.filter(x=>x.funktion===n.ref.name).length,w=DS.worker.find(x=>x.funktion===n.ref.name&&x.laeuft);
    if(!a&&!w)return null;
    return h("span",{class:"gwait"+(a?"":" ruhig"),title:a?a+" Auftrag/Aufträge warten auf eine Antwort von Hand — Klick auf die Karte":"externer Worker läuft"},a?"⧗ "+a:"▶");}
  function dsBadges(){if(!world)return;world.querySelectorAll(".gnode2.n-funktion").forEach(el=>{const n=NODEBY.get(el.dataset.id),head=el.querySelector(".ghead");if(!n||!head)return;
    const alt=head.querySelector(".gwait"),neu=dsBadge(n);if(alt)alt.remove();if(neu)head.insertBefore(neu,head.querySelector(".gx"));});}
  function dsFunktion(body,f){const w=DS.worker.find(x=>x.funktion===f.name),a=DS.auftraege.filter(x=>x.funktion===f.name),u=aktivName();
    body.append(h("div",{class:"gsec"},"▶ Durchspielen in "+u));
    if(!w){body.append(h("div",{class:"gi-hint"},"In „"+u+"“ nicht gebunden (Funktionen:"+f.name+")."));return;}
    if(w.laufort==="Hand"){if(!a.length)body.append(h("div",{class:"gi-hint"},hostLaeuft()?"Laufort Hand — keine offenen Aufträge.":"Laufort Hand — der Host steht (⟳)."));
      a.forEach(x=>body.append(dsAuftragBox(x)));}
    else if(w.laufort==="Extern")body.append(dsWorkerZeile(w));
    else body.append(h("div",{class:"gi-hint"},"Laufort Host — die C#-Implementierung rechnet im Host. Für eine Antwort von Hand oder einen Python-Worker den Laufort im 🖥 Host-Rahmen (Funktionen) umstellen und ⟳."));}
  function dsQuelle(body,r){const u=aktivName();body.append(h("div",{class:"gsec"},"▶ Durchspielen in "+u));
    const pfad=h("div",{class:"ds-zeit",style:"overflow-wrap:anywhere"},"…");body.append(pfad);
    wbJson("/api/werkbank/quelle/"+encodeURIComponent(r.name)).then(j=>{pfad.textContent=j.pfad?"Der Treiber beobachtet "+j.pfad:"⚠ kein Pfad in "+u;}).catch(()=>{});
    body.append(h("div",{class:"frow"},h("input",{type:"file",multiple:"",onchange:e=>{dsLegeAb(r.name,[...e.target.files]);e.target.value="";}})),
      h("div",{class:"gi-hint"},"Oder Dateien auf die Quellkarte ziehen. Die Werkbank legt sie in den Pfad, melden tut sie der echte Treiber"+(hostLaeuft()?".":" — sobald der Host läuft (⟳).")));}
  async function dsLegeAb(quelle,files){for(const f of files){let r;
      try{r=await (await fetch("/api/werkbank/quelle/"+encodeURIComponent(quelle)+"/datei?name="+encodeURIComponent(f.name),{method:"POST",body:f})).json();}catch(e){r={ok:false,fehler:e.message};}
      deFlash(r.ok?"📁 "+f.name+" → "+quelle+(r.hostLaeuft?" — der Treiber meldet sie":" — Host steht, gemeldet wird beim Start"):"⚠ "+f.name+": "+r.fehler,!!r.ok);}}
  function dsDropZiel(el,n){const r=n.ref;if(!r||!r.treiber||n.kind==="host")return;
    const hatDateien=e=>[...((e.dataTransfer&&e.dataTransfer.types)||[])].includes("Files");
    el.addEventListener("dragover",e=>{if(!hatDateien(e))return;e.preventDefault();e.stopPropagation();e.dataTransfer.dropEffect="copy";el.classList.add("ds-drop");});
    el.addEventListener("dragleave",()=>el.classList.remove("ds-drop"));
    el.addEventListener("drop",e=>{if(!hatDateien(e))return;e.preventDefault();e.stopPropagation();el.classList.remove("ds-drop");dsLegeAb(r.name,[...e.dataTransfer.files]);});}
  // Eine Datei neben die Karte fallen gelassen: der Browser würde sie öffnen und den Editor verlassen.
  ["dragover","drop"].forEach(ev=>window.addEventListener(ev,e=>{if([...((e.dataTransfer&&e.dataTransfer.types)||[])].includes("Files"))e.preventDefault();}));

  // ── ⏩ Uhr vorstellen ──
  function dsUhrMenue(cx,cy){const vor=um=>async()=>{let r;try{r=await wbJson("/api/werkbank/uhr/vor?um="+encodeURIComponent(um),{method:"POST"});}catch(e){r={ok:false,fehler:e.message};}
      deFlash(r.ok?"⏩ Uhr der Sim-DB: Versatz "+r.versatz:"⚠ "+r.fehler,!!r.ok);await wbLadeUhr();wbKopf();render();};
    menue(cx,cy,"⏩ Uhr der Sim-DB"+(WB.uhr&&WB.uhr.versatz?" · Versatz "+WB.uhr.versatz:""),[
      {t:"+1 Minute",fn:vor("00:01:00")},{t:"+1 Stunde",fn:vor("01:00:00")},{t:"+1 Tag",fn:vor("1.00:00:00")},{t:"+7 Tage",fn:vor("7.00:00:00")},
      {trenn:true},
      {t:"↺ zurück auf 0",title:"Versatz 0 — die Uhr der Sim-DB geht wieder wie die echte",fn:async()=>{await wbJson("/api/werkbank/uhr?versatz=00:00:00",{method:"POST"}).catch(()=>{});await wbLadeUhr();wbKopf();render();}}]);}

  // ── Laden und Takt: Identitäten + Rechte (bei Wechsel von Host/Umgebung/Token), Aufträge + Worker alle 3 s ──
  async function dsRechte(){if(!hostLaeuft()){DS.rechte={fehler:"Der Host steht — ⟳ baut und startet ihn.",commands:[],queries:[]};return;}
    const r=await dsEingang("rechte");DS.rechte=r.status===200?r:{fehler:r.fehler||("HTTP "+r.status),commands:[],queries:[]};}
  async function dsTakt(erzwingen){
    const lauf=hostLaeuft()+"|"+((WB.zustand||{}).laufStand||"")+"|"+aktivName()+"|"+(((WB.zustand||{}).host||{}).pid||"");
    if(lauf!==DS.lauf||erzwingen===true&&!DS.ident){DS.lauf=lauf;
      try{DS.ident=await wbJson("/api/werkbank/identitaeten");}catch(e){DS.ident=null;}
      const t=(DS.ident&&DS.ident.tokens)||[];if(!t.some(x=>x.token===DS.token))DS.token=(t.find(x=>x.token===lsHole("ds.token"))||t[0]||{}).token||null;
      DS.schemas={};await dsRechte();DS.sig="";}
    if(hostLaeuft()){const r=await dsEingang("auftraege");
      if(r.status===200){DS.auftraege=r.auftraege||[];DS.handFn=r.funktionen||[];DS.auftragFehler=null;}
      else{DS.auftraege=[];DS.handFn=[];DS.auftragFehler=r.fehler||("HTTP "+r.status);}}
    else{DS.auftraege=[];DS.handFn=[];DS.auftragFehler="Der Host steht — ⟳ startet ihn.";}
    try{DS.worker=(await wbJson("/api/werkbank/worker")).worker||[];}catch(e){DS.worker=[];}
    const sig=JSON.stringify([DS.auftraege.map(a=>a.vorgang),DS.worker.map(w=>[w.funktion,w.laufort,w.laeuft,w.exitCode,w.zuletzt]),DS.handFn,DS.auftragFehler]);
    if(sig===DS.sig&&erzwingen!==true)return;
    DS.sig=sig;
    // Konsole offen: nur die Reiter auffrischen (das Formular behält Fokus); sonst die ganze Leiste.
    const el=document.getElementById("ds-leiste");
    if(DS.offen&&!["auftraege","worker"].includes(DS.tab)&&el&&el.firstChild&&erzwingen!==true)el.firstChild.replaceWith(dsTabs());else dsLeiste();
    dsBadges();
    // Panel der gewählten Funktion nur neu, wenn sich IHRE Aufträge oder ihr Worker-Zustand geändert haben (ein halb ausgefülltes
    //   Antwort-Formular überlebt das ohnehin — die Werte stehen in DS.antwort —, nur der Fokus nicht).
    const n=SEL&&NODEBY.get(SEL);
    if(n&&n.kind==="funktion"){const f=n.ref.name,w=DS.worker.find(x=>x.funktion===f)||{};
      const fs=JSON.stringify([DS.auftraege.filter(a=>a.funktion===f).map(a=>a.vorgang),w.laufort,w.laeuft,w.exitCode]);
      if(DS.fsel&&DS.fsel.f===f&&DS.fsel.s!==fs)zeigeInspector();DS.fsel={f,s:fs};}}
  async function dsStart(){const t=lsHole("ds.leiste");DS.offen=!!t;if(t)DS.tab=t;dsLeiste();
    setTimeout(()=>dsTakt(true),800);setInterval(()=>{if(!document.hidden)dsTakt();},3000);}
  {const boot=window.deBoot;window.deBoot=async function(){await boot();dsStart();};}

  // ══ SPUR (docs/konzept-werkbank.md §7.3, Werkbank P6) ═══════════════════════════════════════════════════════════════════════════════
  //   Nur View auf Einblick-Daten: der Werkbank-Server spiegelt die Spur des laufenden Hosts (SSE „neu bis seq" + Cursor) und liefert
  //   Vorgänge (gefiltert), nummerierte Wege (je Schritt die Karten, die gegangenen Kanten, Payload, Stand, ↤/↦), Zeitleisten-Bahnen und
  //   den Live-Puls fertig aus. Das JS schlägt nur Karten-Ids nach (Wirker-Id bzw. rec:Typ → Karte) und setzt Klassen, Nummern, Dicken.
  var SP={vorgaenge:null,filter:{quelle:"",akteur:"",aggregat:"",zustand:""},gewaehlt:null,zeile:null,vorgang:null,bis:null,schritt:null,
    puls:null,pulsAn:lsHole("sp.puls")!=="aus",spiel:null,fehler:null};
  const KREIS="①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳";
  const nrText=n=>n>=1&&n<=20?KREIS[n-1]:"("+n+")";
  const ZUSTAND={laeuft:"● läuft",wartet:"⧗ wartet",fertig:"✓ fertig",gescheitert:"✕ gescheitert"};
  const SP_ART={event:"",abgelehnt:"✕ abgelehnt",gescheitert:"✕ gescheitert",zeitlimit:"⏳ Zeitlimit",umgeleitet:"↪ umgeleitet"};
  const vorZeit=t=>{const s=Math.max(0,(Date.now()-Date.parse(t))/1000);return s<60?Math.round(s)+" s":s<3600?Math.round(s/60)+" min":s<86400?Math.round(s/3600)+" h":Math.round(s/86400)+" T";};
  const uhr=t=>{if(!t)return "—";const d=new Date(t);return d.toLocaleTimeString()+"."+String(d.getMilliseconds()).padStart(3,"0");};
  const spHint=t=>h("div",{class:"gi-hint"},t);
  function spTitel(){const z=SP.zeile;return (z?(z.wurzelTyp||z.prozess||"Vorgang"):"Vorgang")+" · "+(SP.gewaehlt||"").slice(0,8);}

  // Karten-Id (Wirker-Id bzw. rec:Typ) → sichtbare Karte (Vertreter). Nur Nachschlagen — je Sicht (NODEBY) einmal aufgebaut.
  let SPKN=null;
  function spKarte(id){if(!id)return null;
    if(!SPKN||SPKN.n!==NODEBY||SPKN.v!==VERTRETER){const m=new Map();NODEBY.forEach(n=>{if(n.kind!=="host"&&n.ref&&n.ref._id&&!m.has(n.ref._id))m.set(n.ref._id,n.id);});SPKN={n:NODEBY,v:VERTRETER,m,c:new Map()};}
    if(SPKN.c.has(id))return SPKN.c.get(id);
    let k=NODEBY.has(id)?id:SPKN.m.get(id)||null;
    if(!k&&id.startsWith("quelle:"))k=recByName(id.slice(7))?"rec:"+id.slice(7):null;
    if(!k&&id.startsWith("pipeline:")){const f=MODEL.fluesse.find(x=>x._id===id),z=f&&(f.knoten||[])[0];k=z?"fs:"+z._id:null;}
    const v=k?vertreterId(k):null;SPKN.c.set(id,v);return v;}
  const spSchritte=()=>{const bis=SP.bis??Infinity;return ((SP.vorgang||{}).schritte||[]).filter(s=>s.nr<=bis);};
  // Die Karten des Vorgangs bis zum Schieber — der Fokus „mit Vorgang".
  function spurMenge(){const m=new Set(),dazu=id=>{const k=spKarte(id);if(k)m.add(k);};
    spSchritte().forEach(s=>{(s.karten||[]).forEach(dazu);(s.paare||[]).forEach(([a,b])=>{dazu(a);dazu(b);});});(SP.vorgang.offen||[]).forEach(dazu);(SP.vorgang.hinweise||[]).forEach(x=>dazu(x.wirker));
    return m;}

  // ── Überlagerung: nach jedem drawEdges (Kanten tragen data-a/-b = Karten-Ids) ──
  function spurZeichne(){if(!world||!svg)return;
    world.querySelectorAll(".gspur-nr,.gpuls").forEach(x=>x.remove());
    [svg,svgTop].forEach(s=>s&&s.querySelectorAll(".gspur-t").forEach(x=>x.remove()));
    const KL=["spur","spur-alt","spur-rot","spur-warte","spur-verlierbar","spur-sel"];
    world.querySelectorAll(".gnode2.spur,.gnode2.spur-rot,.gnode2.spur-warte,.gnode2.spur-verlierbar").forEach(el=>el.classList.remove(...KL));
    const pfade=[...svg.querySelectorAll("path.glink"),...(svgTop?svgTop.querySelectorAll("path.glink"):[])];
    pfade.forEach(p=>{if(p.classList.contains("spur")||p.classList.contains("puls")||p.classList.contains("puls-leer")){p.classList.remove("spur","spur-alt","spur-rot","puls","puls-leer");p.style.strokeWidth="";}});
    world.classList.toggle("spurmodus",!!SP.vorgang);
    const ELS=new Map([...world.querySelectorAll(".gnode2")].map(e=>[e.dataset.id,e]));
    const kante=new Map();pfade.forEach(p=>{const a=p.dataset.a,b=p.dataset.b;if(!a||!b)return;
      [a+"|"+b,b+"|"+a].forEach(k=>{let l=kante.get(k);if(!l)kante.set(k,l=[]);l.push(p);});});
    if(SP.vorgang)spurVorgang(ELS,kante);else if(SP.pulsAn&&SP.puls)spurPuls(ELS,kante,pfade);}
  // Der gewählte Vorgang: gegangene Karten und Kanten leuchten, Nummern ①②③ mit Zeit an der Karte, die der Schritt schrieb, und an der
  //   Kante dorthin; Fehlschläge (✕ ⏳) rot, ältere Stände grau, Verlierbares gestrichelt, offene Aufträge (⧗) pulsieren.
  function spurVorgang(ELS,kante){const v=SP.vorgang,amZiel=new Map(),anKante=new Map(),ss=spSchritte();
    const sel=SP.schritt&&ss.find(s=>s.nr===SP.schritt);
    ss.forEach(s=>{const ks=(s.karten||[]).map(spKarte).filter(Boolean),fehl=s.art!=="event";
      const alle=new Set(ks);(s.paare||[]).forEach(([a,b])=>[a,b].forEach(x=>{const k=spKarte(x);if(k)alle.add(k);}));
      alle.forEach(k=>{const el=ELS.get(k);if(!el)return;el.classList.add("spur");if(s.alt)el.classList.add("spur-alt");if(sel===s&&ks.includes(k))el.classList.add("spur-sel");});
      const ziel=ks[ks.length-1];if(ziel){let l=amZiel.get(ziel);if(!l)amZiel.set(ziel,l=[]);l.push(s);}
      const ps=s.paare||[];ps.forEach(([a,b],i)=>{const x=spKarte(a),y=spKarte(b);if(!x||!y||x===y)return;
        (kante.get(x+"|"+y)||[]).forEach(p=>{p.classList.add("spur");if(s.alt)p.classList.add("spur-alt");if(fehl)p.classList.add("spur-rot");
          // Nummeriert: die Kante in die geschriebene Karte und der Draht zwischen zwei Fluss-Knoten bzw. Regeln (der Weg im Ablauf).
          if(i===ps.length-1||(/^(fluss|prozess):/.test(a)&&/^(fluss|prozess):/.test(b))){let l=anKante.get(p);if(!l)anKante.set(p,l=[]);if(!l.includes(s))l.push(s);}});});});
    // alt nur, wo ALLE Schritte an der Karte älter sind
    ELS.forEach((el,id)=>{if(!el.classList.contains("spur-alt"))return;const l=amZiel.get(id);if(l&&l.some(s=>!s.alt))el.classList.remove("spur-alt");});
    const text=ss=>ss.map(s=>nrText(s.nr)+(s.art==="zeitlimit"?"⏳":s.art!=="event"?"✕":"")).join("");
    const titel=ss=>ss.map(s=>nrText(s.nr)+" "+uhr(s.zeit)+" · "+s.name+(SP_ART[s.art]?" — "+SP_ART[s.art]:"")+(s.alt?" · Stand "+s.stand+" (älter)":"")).join("\n")+"\nKlick: Schritt im Panel";
    amZiel.forEach((l,id)=>{const el=ELS.get(id);if(!el)return;const rot=l.some(s=>s.art!=="event");if(rot)el.classList.add("spur-rot");
      const b=h("span",{class:"gspur-nr"+(rot?" rot":"")+(l.every(s=>s.alt)?" alt":"")+(sel&&l.includes(sel)?" an":""),title:titel(l)},text(l));
      b.addEventListener("pointerdown",e=>e.stopPropagation());b.onclick=e=>{e.stopPropagation();spSchritt(l[l.length-1].nr);};el.append(b);});
    const t0=Date.parse(v.beginn||0),dt=z=>{const d=(Date.parse(z)-t0)/1000;return "+"+(d<10?d.toFixed(2):d<600?d.toFixed(1):Math.round(d))+" s";};
    anKante.forEach((l,p)=>spLabel(p,text(l)+" "+dt(l[0].zeit),titel(l),()=>spSchritt(l[l.length-1].nr),
      (l.some(s=>s.art!=="event")?"rot":l.every(s=>s.alt)?"alt":"")+(sel&&l.includes(sel)?" an":"")));
    (v.hinweise||[]).forEach(x=>{const k=spKarte(x.wirker),el=k&&ELS.get(k);if(!el)return;
      el.classList.add(x.art==="ablehnung"?"spur-rot":"spur-verlierbar");
      el.append(h("span",{class:"gspur-nr rot hinw",title:uhr(x.zeit)+" · "+x.art+" (verlierbar, nicht im Log): "+x.text},x.art==="ablehnung"?"✕ abgelehnt":"⇢ "+x.art));});
    (v.offen||[]).forEach(id=>{const k=spKarte(id),el=k&&ELS.get(k);if(el){el.classList.add("spur","spur-warte");
      el.append(h("span",{class:"gspur-nr warte",title:"Offener Auftrag dieses Vorgangs — wartet auf einen Ausführer"},"⧗"));}});}
  function spLabel(p,text,titel,klick,kl){try{const L=p.getTotalLength(),pt=p.getPointAtLength(L*0.55);
      const t=document.createElementNS(SVGNS,"text");t.setAttribute("class","gspur-t"+(kl?" "+kl:""));t.setAttribute("x",pt.x);t.setAttribute("y",pt.y-7);
      t.setAttribute("text-anchor","middle");t.textContent=text;const tt=document.createElementNS(SVGNS,"title");tt.textContent=titel;t.append(tt);
      t.addEventListener("pointerdown",e=>e.stopPropagation());t.addEventListener("click",e=>{e.stopPropagation();klick();});
      p.parentNode.append(t);}catch(e){}}
  // Live-Puls (ohne gewählten Vorgang): Zähler und „zuletzt" je Karte, Kantendicke nach Menge, Stau (offene Aufträge), nie benutzte Kanten blass.
  const PULS_OHNE=new Set(["rumpf","code","valueobject","enum","state","host","store","readmodel","fn"]);
  function spurPuls(ELS,kante,pfade){const P=SP.puls;if(!P||!P.laeuft)return;
    const nk=new Map();(P.karten||[]).forEach(k=>{const id=spKarte(k.id);if(!id)return;const a=nk.get(id);
      if(!a||k.n>a.n)nk.set(id,{n:k.n,z:a&&Date.parse(a.z)>Date.parse(k.zuletzt)?a.z:k.zuletzt});});
    nk.forEach((a,id)=>{const el=ELS.get(id);if(el)el.append(h("span",{class:"gpuls",title:a.n+"× im Log dieses Laufs · zuletzt "+uhr(a.z)},a.n+" · "+vorZeit(a.z)));});
    const max=Math.max(1,...(P.kanten||[]).map(k=>k.n)),genutzt=new Set();
    (P.kanten||[]).forEach(k=>{const x=spKarte(k.a),y=spKarte(k.b);if(!x||!y||x===y)return;
      (kante.get(x+"|"+y)||[]).forEach(p=>{genutzt.add(p);const w=2.4+5*Math.log(1+k.n)/Math.log(1+max);
        p.style.strokeWidth=Math.max(parseFloat(p.style.strokeWidth)||0,w).toFixed(1)+"px";p.classList.add("puls");});});
    // Nie benutzte Kanten: nur Nachrichten-Fluss (keine Hub-, Typ-, Code- oder Store-Kanten), und erst, wenn überhaupt etwas lief.
    if((P.kanten||[]).length)pfade.forEach(p=>{if(genutzt.has(p)||p.classList.contains("hub")||p.classList.contains("internal")||p.classList.contains("gbuendel")||p.classList.contains("gauf"))return;
      const a=NODEBY.get(p.dataset.a),b=NODEBY.get(p.dataset.b);if(!a||!b||PULS_OHNE.has(a.kind)||PULS_OHNE.has(b.kind))return;p.classList.add("puls-leer");});
    (P.offen||[]).forEach(o=>{const id=spKarte(o.id),el=id&&ELS.get(id);if(el&&!el.querySelector(".gwait"))el.append(h("span",{class:"gpuls stau",title:o.n+" offene Aufträge beim Vermittler (Stau)"},"⧗ "+o.n));});}

  // ── Auswahl eines Vorgangs, Schieber, Schritt-Panel ──
  async function spLadeVorgang(){if(!SP.gewaehlt)return;
    try{const v=await wbJson("/api/werkbank/spur/vorgang/"+encodeURIComponent(SP.gewaehlt));
      if(v.fehler||v.laeuft===false){SP.fehler=v.fehler||"Der Host steht — ⟳ startet ihn.";return;}SP.fehler=null;SP.vorgang=v;}
    catch(e){SP.fehler=e.message;}}
  async function spWaehle(k,zeile){spSpielStopp(true);
    if(!k||(SP.gewaehlt===k&&zeile)){SP.gewaehlt=null;SP.zeile=null;SP.vorgang=null;SP.bis=null;SP.schritt=null;
      if(FOKUS&&FOKUS.art==="vorgang")FOKUS=null;fokusNeu();wendeFokusAn();fokusChip();zeigeInspector();spurZeichne();dsLeiste();return;}
    SP.gewaehlt=k;if(zeile)SP.zeile=zeile;SP.bis=null;SP.schritt=null;SP.vorgang=null;dsLeiste();
    await spLadeVorgang();if(SP.gewaehlt!==k)return;
    SEL=null;fokusNeu();wendeFokusAn();fokusChip();zeigeInspector();spurZeichne();dsLeiste();
    if(FOCUS&&FOCUS.size)passeEin([...FOCUS].filter(i=>VIS.has(i)),1);}
  function spBis(b){const n=((SP.vorgang||{}).schritte||[]).length;SP.bis=b==null||b>=n?null:Math.max(0,b);
    fokusNeu();wendeFokusAn();fokusChip();spurZeichne();spZeitAuffrischen();}
  function spSchritt(nr){SP.schritt=nr;if(SEL){SEL=null;wendeFokusAn();}zeigeInspector();spurZeichne();spZeitAuffrischen();}
  function spSchrittPanel(){if(!canvas||!SP.vorgang)return;const s=(SP.vorgang.schritte||[]).find(x=>x.nr===SP.schritt);if(!s)return;
    const p=h("div",{class:"ginsp gspur-panel"});["pointerdown","dblclick","wheel","click"].forEach(ev=>p.addEventListener(ev,e=>e.stopPropagation()));
    p.append(h("div",{class:"gi-h"},h("span",{class:"gi-k gspur-k"+(s.art!=="event"?" rot":"")},nrText(s.nr)+" Schritt"),h("span",{class:"gi-n",title:s.name},s.name),
      h("button",{title:"Vorheriger Schritt",onclick:()=>{if(s.nr>1)spSchritt(s.nr-1);}},"‹"),
      h("button",{title:"Nächster Schritt",onclick:()=>{if(s.nr<SP.vorgang.schritte.length)spSchritt(s.nr+1);}},"›"),
      h("button",{title:"Schließen (Esc)",onclick:()=>{SP.schritt=null;zeigeInspector();spurZeichne();}},"✕")));
    const zeile=(k,...v)=>p.append(h("div",{class:"frow gspur-z"},h("span",{class:"slotlbl"},k),h("span",{class:"gspur-w"},...v)));
    const karte=id=>{if(!id)return h("span",{class:"ds-zeit"},"—");const k=spKarte(id),n=k&&NODEBY.get(k);
      return n?h("a",{class:"gspur-link",title:id+" — Klick: Karte zeigen",onclick:()=>{centerOn(n);pulseNode(n);}},(NODELABEL[n.kind]||n.kind)+" · "+(n.name||id))
        :h("span",{class:"gspur-weg",title:"Die Spur stammt von einem Baustein, den der Code nicht mehr hat"},"⚠ Karte gibt es nicht mehr ("+id+")");};
    if(SP_ART[s.art])zeile("Ausgang",h("b",{class:"gspur-rot-t"},SP_ART[s.art]+(s.daten&&s.daten.Grund?" — "+s.daten.Grund:"")));
    zeile("Zeit",uhr(s.zeit));
    zeile("Knoten",karte(s.wirker));
    if(s.ueber)zeile("über",karte(s.ueber));
    zeile("Akteur",s.akteur||"—");
    zeile("Stand",h("span",{class:s.alt?"gspur-alt-t":""},(s.stand||"—")+(s.alt?" · älterer Stand (Board: "+SP.vorgang.stand+")":"")));
    zeile("Stream",h("code",{},(s.aggregat?s.aggregat+" · ":"")+(s.stream||"").slice(0,8)+" v"+s.version));
    const nrLink=n=>h("a",{class:"gspur-link",onclick:()=>spSchritt(n)},nrText(n)+" "+((SP.vorgang.schritte.find(x=>x.nr===n)||{}).name||""));
    zeile("↤ verursacht von",s.vonNr?nrLink(s.vonNr):h("span",{class:"ds-zeit"},s.ursache?"außerhalb des Vorgangs ("+s.ursache+")":"Wurzel — von außen"));
    zeile("↦ bewirkte",...((s.folgen||[]).length?s.folgen.flatMap((n,i)=>i?[h("br"),nrLink(n)]:[nrLink(n)]):[h("span",{class:"ds-zeit"},"nichts (bisher)")]));
    p.append(h("div",{class:"gsec"},"Payload"),h("pre",{class:"wb-log"},JSON.stringify(s.daten,null,2)));
    canvas.append(p);}

  // ── Leiste: ☰ Vorgänge ──
  async function spLadeVorgaenge(){const q=Object.entries(SP.filter).filter(([,v])=>v).map(([k,v])=>k+"="+encodeURIComponent(v)).join("&");
    try{SP.vorgaenge=await wbJson("/api/werkbank/spur/vorgaenge"+(q?"?"+q:""));}catch(e){SP.vorgaenge={fehler:e.message,vorgaenge:[]};}}
  function spVorgaengeTab(box){const v=SP.vorgaenge;
    if(!v){box.append(spHint("lädt …"));spLadeVorgaenge().then(()=>{if(DS.offen&&DS.tab==="vorgaenge")dsLeiste();});return;}
    if(v.laeuft===false){box.append(spHint("Der Host steht — ⟳ startet ihn; dann erscheinen hier die Vorgänge aus seinem Log."));return;}
    const fa=v.facetten||{quellen:[],akteure:[],zustaende:{}},neu=()=>spLadeVorgaenge().then(dsLeiste);
    const sel=(k,titel,werte)=>{const e=h("select",{title:titel,onchange:ev=>{SP.filter[k]=ev.target.value;neu();}},h("option",{value:""},titel+": alle"),
      ...werte.map(([w,t])=>h("option",{value:w},t)));e.value=SP.filter[k]||"";return e;};
    const agg=h("input",{placeholder:"Aggregat-Id …",value:SP.filter.aggregat||"",title:"Ein Stream des Vorgangs (ein Teil der Guid genügt) — Enter",
      onkeydown:ev=>{if(ev.key==="Enter"){SP.filter.aggregat=ev.target.value.trim();neu();}}});
    const links=h("div",{class:"ds-links sp-links"}),rechts=h("div",{class:"ds-rechts"});box.append(links,rechts);
    links.append(h("div",{class:"frow sp-filter"},sel("quelle","Quelle",(fa.quellen||[]).map(x=>[x,x])),sel("akteur","Akteur",(fa.akteure||[]).map(x=>[x,x])),
      sel("zustand","Zustand",Object.keys(ZUSTAND).map(z=>[z,ZUSTAND[z]+" ("+((fa.zustaende||{})[z]||0)+")"]))),
      h("div",{class:"frow"},agg,h("button",{class:"act",title:"Neu laden",onclick:neu},"↻"),h("span",{class:"ds-zeit"},(v.vorgaenge||[]).length+" von "+(v.gesamt||0))));
    if(v.fehler)links.append(h("div",{class:"llmmeld err"},v.fehler));
    const liste=h("div",{class:"sp-liste"});links.append(liste);
    (v.vorgaenge||[]).forEach(x=>liste.append(h("div",{class:"sp-z"+(x.korrelation===SP.gewaehlt?" an":""),title:"Korrelation "+x.korrelation+(x.wurzelWirker?" · Wurzel "+x.wurzelWirker:"")+"\nKlick: Spur auf dem Board (nochmal: lösen)",
        onclick:()=>spWaehle(x.korrelation,x)},
      h("span",{class:"sp-zu "+x.zustand},ZUSTAND[x.zustand]||x.zustand),h("b",{},x.wurzelTyp||x.prozess||"?"),
      x.prozess?h("span",{class:"ds-zeit"},"⛓ "+x.prozess):null,x.akteur?h("span",{class:"ds-zeit"},"👤 "+x.akteur):null,
      h("span",{class:"ds-zeit"},x.eintraege+" Einträge · "+(x.streams||0)+" Streams · vor "+vorZeit(x.zuletzt)),
      x.grund?h("span",{class:"sp-grund"},x.grund):null)));
    if(!(v.vorgaenge||[]).length)liste.append(spHint("Keine Vorgänge"+(Object.values(SP.filter).some(Boolean)?" für diesen Filter":"")+" — eine Datei auf die Quellkarte oder ein Command aus der ⌨ Konsole startet einen."));
    spWeg(rechts);}
  // Rechts: der Weg des gewählten Vorgangs als Liste (Klick = Schritt im Panel).
  function spWeg(box){if(!SP.gewaehlt){box.append(spHint("Einen Vorgang wählen: seine Spur erscheint auf dem Board — nummerierte Kanten ①②③ mit Zeit, Fehlschläge rot, ältere Stände grau. Ohne Vorgang zeigt das Board den Live-Puls."));return;}
    if(SP.fehler){box.append(h("div",{class:"llmmeld err"},SP.fehler));return;}
    const v=SP.vorgang;if(!v){box.append(spHint("lädt …"));return;}
    box.append(h("div",{class:"gsec"},"Weg · "+spTitel()+" · "+v.schritte.length+" Schritte"+(v.offen&&v.offen.length?" · ⧗ "+v.offen.length+" offen":"")));
    const bis=SP.bis??Infinity;
    v.schritte.forEach(s=>{const k=spKarte(s.wirker),n=k&&NODEBY.get(k);
      box.append(h("div",{class:"sp-schritt"+(s.nr>bis?" spaeter":"")+(s.art!=="event"?" rot":"")+(s.alt?" alt":"")+(SP.schritt===s.nr?" an":""),onclick:()=>spSchritt(s.nr)},
        h("span",{class:"sp-nr"},nrText(s.nr)),h("span",{class:"ds-zeit"},uhr(s.zeit)),h("b",{},s.name),
        h("span",{class:"ds-zeit"},SP_ART[s.art]||""),h("span",{class:n?"ds-zeit":"gspur-weg"},n?"← "+(n.name||s.wirker):"⚠ "+(s.wirker||"ohne Wirker")+" — Karte gibt es nicht mehr")));});
    (v.hinweise||[]).forEach(x=>box.append(h("div",{class:"sp-schritt rot verlierbar"},h("span",{class:"sp-nr"},"⇢"),h("span",{class:"ds-zeit"},uhr(x.zeit)),
      h("b",{},x.art),h("span",{class:"ds-zeit"},x.text+" (verlierbar)"))));}

  // ── Leiste: ⏱ Zeitleiste — Bahnen je Baustein, Spalten = Schritte; der Schieber zeigt das Board zum Zeitpunkt (Abspielen der Aufzeichnung) ──
  function spZeitTab(box){const v=SP.vorgang;
    if(!v){box.append(spHint(SP.gewaehlt?"lädt …":"Erst einen Vorgang wählen (☰ Vorgänge) — die Zeitleiste spielt seine Aufzeichnung ab, nie erneut aus."));return;}
    const ss=v.schritte||[],n=ss.length,t0=Date.parse(v.beginn||ss.length&&ss[0].zeit||0);
    const z=h("div",{class:"sp-zeit"});box.append(z);
    z.append(h("div",{class:"frow sp-zk"},
      h("button",{class:"act sp-spiel",title:"Abspielen (Schritt für Schritt)",onclick:()=>SP.spiel?spSpielStopp():spSpiel()},SP.spiel?"⏸":"▶"),
      h("input",{type:"range",min:0,max:n,value:SP.bis??n,class:"sp-schieber",oninput:e=>spBis(+e.target.value)}),
      h("span",{class:"ds-zeit sp-lbl"}),
      h("button",{class:"act",title:"Ganze Aufzeichnung",onclick:()=>spBis(null)},"⇥"),
      h("span",{class:"ds-zeit"},spTitel())));
    const g=h("div",{class:"sp-gitter",style:"grid-template-columns:minmax(160px,240px) repeat("+n+",28px)"});z.append(g);
    g.append(h("div",{class:"sp-bl"}));
    ss.forEach(s=>g.append(h("div",{class:"sp-dt",title:uhr(s.zeit)},"+"+((Date.parse(s.zeit)-t0)/1000).toFixed(Date.parse(s.zeit)-t0<10000?1:0))));
    (v.bahnen||[]).forEach(b=>{const k=spKarte(b.wirker),nd=k&&NODEBY.get(k);
      g.append(h("div",{class:"sp-bl"+(nd?"":" gspur-weg"),title:b.wirker,onclick:()=>{if(nd){centerOn(nd);pulseNode(nd);}}},nd?(NODELABEL[nd.kind]||nd.kind)+" · "+(nd.name||b.wirker):"⚠ "+b.wirker));
      const in_=new Set(b.schritte);
      ss.forEach(s=>g.append(in_.has(s.nr)?h("div",{class:"sp-punkt"+(s.art!=="event"?" rot":"")+(s.alt?" alt":""),"data-nr":s.nr,
          title:nrText(s.nr)+" "+uhr(s.zeit)+" · "+s.name+(SP_ART[s.art]?" — "+SP_ART[s.art]:""),onclick:()=>{spBis(s.nr);spSchritt(s.nr);}},nrText(s.nr)):h("div",{class:"sp-leer"})));});
    spZeitAuffrischen();}
  // Schieber, Beschriftung und Punkte im Ort auffrischen (der Schieber behält beim Ziehen den Fokus).
  function spZeitAuffrischen(){const el=document.querySelector("#ds-leiste .sp-zeit");if(!el||!SP.vorgang)return;const ss=SP.vorgang.schritte||[],n=ss.length,bis=SP.bis??n;
    const r=el.querySelector(".sp-schieber");if(r&&+r.value!==bis)r.value=bis;
    const l=el.querySelector(".sp-lbl");if(l)l.textContent=bis>=n?"alle "+n+" Schritte":bis===0?"vor dem ersten Schritt":"bis "+nrText(bis)+" · "+uhr(ss[bis-1].zeit)+" · "+ss[bis-1].name;
    el.querySelectorAll(".sp-punkt").forEach(p=>{const nr=+p.dataset.nr;p.classList.toggle("spaeter",nr>bis);p.classList.toggle("an",nr===SP.schritt);});
    const b=el.querySelector(".sp-spiel");if(b)b.textContent=SP.spiel?"⏸":"▶";}
  function spSpiel(){if(!SP.vorgang)return;const n=SP.vorgang.schritte.length;let i=SP.bis!=null&&SP.bis<n?SP.bis:0;spBis(i);
    SP.spiel=setInterval(()=>{i++;if(i>n){spSpielStopp();return;}spBis(i);},700);spZeitAuffrischen();}
  function spSpielStopp(still){if(SP.spiel){clearInterval(SP.spiel);SP.spiel=null;if(!still)spZeitAuffrischen();}}

  // ── Leiste: ⚠ Meldungen — Build (⟳-Schritte), Compiler/Analyzer und Boot an der Karte ──
  function spMeldungenTab(box){const r=WB.letzter,l=h("div",{class:"sp-meld"});box.append(l);
    const z=(t,art,klick)=>{const e=h("div",{class:"find"+(art==="rot"?" error":art==="gelb"?" warning":"")},t);if(klick){e.style.cursor="pointer";e.onclick=klick;}l.append(e);};
    if(!r)z("Noch kein ⟳ in dieser Sitzung.");
    else{z((r.ok?"✓ ⟳ fertig":"✗ ⟳ angehalten")+" · Stand "+r.stand+" · "+new Date(r.zeit).toLocaleTimeString(),r.ok?"":"rot");
      (r.schritte||[]).forEach(s=>z((s.ok?"✓ ":"✗ ")+s.name+(s.dauerMs?" ("+(s.dauerMs/1000).toFixed(1)+" s)":"")+(s.meldungen&&s.meldungen.length?" — "+s.meldungen.slice(0,4).join(" · "):""),s.ok?"":"rot"));}
    wbDiagListe().forEach(x=>{const n=x.ziel&&NODEBY.get(vertreterId(x.ziel));
      z((n?"→ "+(NODELABEL[n.kind]||n.kind)+" "+n.name+": ":"")+x.text,x.art==="warnung"?"gelb":"rot",n?()=>zeigeLadend(n.id):null);});
    l.append(h("div",{class:"frow"},h("button",{class:"act",title:"Ausgabe des laufenden Hosts im Ausgabe-Panel",onclick:wbLog},"Host-Log")));}

  // ── Takt: der Server meldet „spur bis" über den Fortschritts-Kanal; dazu alle 10 s (Puls „zuletzt", Zustände der Vorgänge) ──
  async function spTakt(){if(!hostLaeuft()){if(SP.puls||SP.vorgaenge){SP.puls=null;SP.vorgaenge=null;spurZeichne();if(DS.offen&&DS.tab==="vorgaenge")dsLeiste();}return;}
    const aufgaben=[];
    if(SP.pulsAn&&!SP.vorgang)aufgaben.push(wbJson("/api/werkbank/spur/puls").then(p=>{SP.puls=p;}).catch(()=>{}));
    if(SP.gewaehlt)aufgaben.push(spLadeVorgang());
    if(DS.offen&&DS.tab==="vorgaenge")aufgaben.push(spLadeVorgaenge());
    await Promise.all(aufgaben);
    if(SP.vorgang){fokusNeu();wendeFokusAn();fokusChip();}
    spurZeichne();
    if(DS.offen&&DS.tab==="vorgaenge"){const a=document.activeElement;if(!(a&&a.closest&&a.closest("#ds-leiste")&&a.tagName==="INPUT"))dsLeiste();}
    else if(DS.offen&&DS.tab==="zeit"&&!SP.spiel){const a=document.activeElement;if(!(a&&a.classList&&a.classList.contains("sp-schieber")))dsLeiste();}
    if(SP.schritt&&!SEL)zeigeInspector();}
  function spPulsUmschalten(){SP.pulsAn=!SP.pulsAn;lsSetze("sp.puls",SP.pulsAn?"an":"aus");if(!SP.pulsAn)SP.puls=null;spurZeichne();dsLeiste();if(SP.pulsAn)spTakt();}
  let SP_T=null;
  window.spSignal=()=>{clearTimeout(SP_T);SP_T=setTimeout(spTakt,250);};
  {const boot=window.deBoot;window.deBoot=async function(){await boot();setTimeout(spTakt,1200);setInterval(()=>{if(!document.hidden)spTakt();},10000);};}

})();
