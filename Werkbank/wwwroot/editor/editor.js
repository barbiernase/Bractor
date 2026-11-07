(function(){
  // Skalare = was der Wire trägt (aus dem Codegen, via rahmen.skalare) — keine eigene Liste im Editor.
  const SCALARS=()=>((MODEL.rahmen&&MODEL.rahmen.skalare)||[]);
  // Vertrags-Fakten aus dem Rahmen (vom Extractor aus dem Code gelesen).
  const ID_FELD=()=>((MODEL.rahmen&&MODEL.rahmen.aggregatIdFeld)||"");
  const KINDINFO={command:["Command","cmd"],event:["Event","evt"],rejection:["Ablehnung","rej"],valueobject:["Value Object","vo"],konfig:["Konfiguration","vo"],query:["Query","qry"],queryresponse:["Response","qrsp"],};
  let MODEL={schemaVersion:"2",akteure:[],clients:[],records:[],enums:[],aggregate:[],decider:[],applier:[],states:[],readModels:[],stores:[],projektionen:[],reader:[],codeKarten:[],graphen:[],katalog:[],bausteinFunktionen:[]};
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
  function normalize(m){m=m||{};m.records=m.records||[];m.enums=m.enums||[];m.aggregate=m.aggregate||[];m.decider=m.decider||[];m.applier=m.applier||[];m.states=m.states||[];
    m.readModels=m.readModels||[];m.stores=m.stores||[];m.projektionen=m.projektionen||[];m.reader=m.reader||[];
    // Code-Karten (Werkbank P7.1): Typen, die das Lesen nicht als Baustein erkennt (z. B. unaufgelöster Basistyp) — kommen fertig vom Server.
    m.codeKarten=m.codeKarten||[];
    // Akteur (docs/konzept-akteure.md): darf[] = seine IDarf<T> (Command, Query, Hinweis), hoert[] = seine IHoert<T> (Arbeit, Kenntnis).
    m.akteure=m.akteure||[];m.akteure.forEach(a=>{if(!a._id)a._id="ak"+(NID++);a.darf=a.darf||[];a.hoert=a.hoert||[];});
    // Client (docs/konzept-akteure.md §4): die Software an der Leitung — verkoerpert[] = die Akteure (IVerkoerpert<A>), kenntnis = was er hört.
    // Was über seine Leitung hineingeht, sind die IDarf dieser Akteure (vom Server abgeleitet: board.sicht.clients).
    // Funktion (IFunktion, ein Block der Hierarchie): Eingänge (Auf) mit Ausgaben ODER gerufene Methoden, Konstruktor, Umsetzungen — vom Server gelesen.
    m.bausteinFunktionen=m.bausteinFunktionen||[];m.bausteinFunktionen.forEach(f=>{f.eingaenge=f.eingaenge||[];f.umsetzungen=f.umsetzungen||[];
      f.methoden=f.methoden||[];f.methoden.forEach(x=>{x.parameter=x.parameter||[];});f.umsetzungen.forEach(u=>{u.konstruktor=u.konstruktor||[];});});
    // Graph (docs/04 §4.4): record X(Knoten…) : IGraph — je Knoten Typ, Name, [Kopf], [Von(nameof(…))] (verlierbar je Quelle). Köpfe, Ausgänge und
    //   Kanten liefert der Server (abgeleitet). Katalog: die Funktionen des Frameworks (Dateiwaechter …), die ein Graph als Knoten nennen kann.
    m.graphen=m.graphen||[];m.graphen.forEach(g=>{if(!g._id)g._id="graph:"+g.name;g.knoten=g.knoten||[];g.knoten.forEach(k=>{k.von=k.von||[];k.verlierbar=k.verlierbar||[];});});
    m.katalog=m.katalog||[];
    m.clients=m.clients||[];m.clients.forEach(c=>{if(!c._id)c._id="cl"+(NID++);c.verkoerpert=c.verkoerpert||[];c.kenntnis=c.kenntnis||[];});
    // Jedes Objekt-Feld bekommt eine stabile _id → Feld-Ports eindeutig (auch bei gleichnamigen Feldern) und rename-fest.
    const stampFelder=arr=>(arr||[]).forEach(f=>{if(f&&!f._id)f._id="f"+(NID++);});
    m.records.forEach(r=>stampFelder(r.felder));m.aggregate.forEach(a=>stampFelder(a.state));m.states.forEach(s=>stampFelder(s.felder));m.readModels.forEach(rm=>stampFelder(rm.felder));
    m.decider.forEach(d=>{if(!d._id)d._id="d"+(NID++);});m.applier.forEach(a=>{if(!a._id)a._id="a"+(NID++);});m.states.forEach(s=>{if(!s._id)s._id="s"+(NID++);});
    m.readModels.forEach(rm=>{if(!rm._id)rm._id="rm"+(NID++);});
    m.stores.forEach(st=>{if(!st._id)st._id="st"+(NID++);st.writeFns=st.writeFns||[];st.readFns=st.readFns||[];st.writeFns.forEach(f=>{if(!f._id)f._id="wf"+(NID++);f.params=f.params||[];});st.readFns.forEach(f=>{if(!f._id)f._id="rf"+(NID++);f.params=f.params||[];});});
    // Projektion-Handle nimmt je Event eine (oder mehrere) Schreib-FÄHIGKEIT als Parameter → fns[]. Der Store-Scope
    // wird daraus ABGELEITET (= die injizierten Stores), nicht mehr von Hand deklariert.
    m.projektionen.forEach(p=>{if(!p._id)p._id="pj"+(NID++);p.handles=p.handles||[];p.handles.forEach(hd=>{hd.fns=hd.fns||[];});delete p.stores;});
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
    // Die Fläche kommt mit dem nächsten Board vom Server; hier nur Panel und Kanten auffrischen.
    ELEM=null;berechneSicht();
    const altI=canvas.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    fokusNeu();drawEdges();zeigeInspector();autosave();}
  // Eine Modell-Änderung aus dem Verbinden-Modus ausführen: ohne Voll-Render, danach nur die betroffenen Karten.
  function imModus(ids,aendern){TEIL={gerufen:false};try{aendern();}finally{const g=TEIL.gerufen;TEIL=null;if(g)teilNeu(ids.filter(Boolean));}}

  // Eindeutiger Name — Namen sind der Referenzschlüssel für Kanten/Decider/Applier.
  // Alle vergebenen Typnamen — jede Sammlung darf fehlen (das Modell kennt nicht jede in jedem Stand).
  function uniq(base){const namen=k=>(MODEL[k]||[]).map(x=>x.name);
    const all=new Set([...["records","aggregate","enums","graphen","readModels","stores","projektionen","reader","akteure","clients"].flatMap(namen),
    ...(MODEL.bausteinFunktionen||[]).flatMap(x=>[x.name,...(x.umsetzungen||[]).map(u=>u.name)])]);
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
    const defaultNs=()=>(opt&&opt.ns)||defaultNsGlobal(), AGG=(opt&&opt.agg)||"";
    // Ohne Klickposition keine Position: packLayout setzt den Knoten in die Spalte seiner Art (typreine Spalten).
    let pos=(typeof x==="number")?{x:Math.round(x/GRID)*GRID,y:Math.round(y/GRID)*GRID}:{};
    if(VIEW.kompakt&&pos.x!=null&&!pos._kpos)pos={...pos,_kpos:{x:pos.x,y:pos.y}};
    if(kind==="aggregate")MODEL.aggregate.push({name:uniq("NeuesAggregat"),namespace:defaultNs(),state:[],...pos});
    else if(kind==="decider")MODEL.decider.push({_id:"d"+(NID++),aggregat:AGG,command:"",ergibt:[],...pos});
    else if(kind==="applier")MODEL.applier.push({_id:"a"+(NID++),aggregat:AGG,event:"",...pos});
    else if(kind==="state")MODEL.states.push({_id:"s"+(NID++),aggregat:AGG,felder:[],...pos});
    else if(kind==="enum")MODEL.enums.push({name:uniq("NeuEnum"),namespace:defaultNs(),werte:["A","B"],...pos});
    else if(kind==="graph"){const nm=uniq("NeuerGraph");MODEL.graphen.push({_id:"graph:"+nm,name:nm,namespace:defaultNs(),knoten:[],...pos});}
    else if(kind==="readmodel")MODEL.readModels.push({_id:"rm"+(NID++),name:uniq("NeuReadModel"),namespace:defaultNs(),felder:[{_id:"f"+(NID++),name:"Id",typ:"Guid"}],store:"",...pos});
    else if(kind==="store")MODEL.stores.push({_id:"st"+(NID++),name:uniq("NeuStore"),namespace:defaultNs(),writeFns:[],readFns:[],...pos});
    else if(kind==="projektion")MODEL.projektionen.push({_id:"pj"+(NID++),name:uniq("NeueProjektion"),namespace:defaultNs(),stores:[],append:false,pull:true,handles:[],...pos});
    else if(kind==="reader")MODEL.reader.push({_id:"rd"+(NID++),name:uniq("NeuReader"),namespace:defaultNs(),stores:[],trackDeps:true,handles:[],...pos});
    else if(kind==="funktion")neueFunktion(defaultNs(),pos);
    else if(kind==="gerufen")neueGerufene(defaultNs(),pos);
    else if(kind==="quelle")neueQuellFunktion(defaultNs(),pos);
    else if(kind==="akteur")MODEL.akteure.push({_id:"ak"+(NID++),name:uniq("NeuerAkteur"),namespace:haeufigsterNs(MODEL.akteure)||defaultNs(),darf:[],...pos});
    else if(kind==="client")MODEL.clients.push({_id:"cl"+(NID++),name:uniq("INeuerClient"),namespace:haeufigsterNs(MODEL.clients)||defaultNs(),verkoerpert:[],kenntnis:[],...pos});
    else MODEL.records.push({name:uniq((opt&&opt.name)||"Neu"+kindLabel(kind).replace(/\s/g,"")),kind,namespace:defaultNs(),felder:kind==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[],
      // Ein Command, der im Block eines Aggregats entsteht, zielt auf es (ICommand<TAggregat>) — damit gehört er ihm.
      ...(kind==="command"&&AGG&&opt&&opt.reg?{aggregat:AGG}:{}),...pos});
    // Heimat-Domäne in der Ansicht merken: trägt Namespace-lose Arten (📝/🤖 …) und hält Abhängige
    //   (Decider/Applier/State/Regel) in ihrer Domäne, auch wenn ihr Besitzer gelöscht wird. Der Code-Namespace geht vor (domKey).
    if(opt&&opt.ns){const neu=graphNodes().find(n=>!vorher.has(n.id));if(neu){VIEW.heim={...(VIEW.heim||{}),[neu.id]:opt.ns};if(opt.blk)VIEW.heimBlk={...(VIEW.heimBlk||{}),[neu.id]:opt.blk};
      if(opt.reg)VIEW.heimReg={...(VIEW.heimReg||{}),[neu.id]:opt.reg};speichereAnsicht();}}
    // In einem Decide angelegt (Event/Ablehnung): das Decide gibt die neue Nachricht aus (OneOf) — damit gehört sie seinem Aggregat.
    if(opt&&opt.ausDecide){const neu=graphNodes().find(n=>!vorher.has(n.id)),d=MODEL.decider.find(x=>x._id===opt.ausDecide);
      if(neu&&d&&!(d.ergibt=d.ergibt||[]).some(o=>o.event===neu.ref.name))d.ergibt.push({event:neu.ref.name});}
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

  // ══ NACHRICHT MIT ZWEI SEITEN (docs/konzept-editor-pipelines.md §12): „◀ kommt aus" = wer diesen Typ erzeugt, „geht an ▶" = wer
  //   ihn konsumiert — vom Server (board.sicht.partner, Werkbank P7.4.5): je Partner Karte (springen), Text, Markierung und, was ✕ löst.
  //   Das ✕ ist eine Geste an der Karte: der Server beschreibt sie (loesen), der Editor ändert die Karte, render() schickt das Delta.
  function partnerVon(nm){const sv=(SICHT().partner||{})[nm]||{ein:[],aus:[]};
    const z=x=>({id:x.id,text:x.text,mark:x.mark||"",los:x.loesen?()=>loese(x.loesen):null});
    return {ein:(sv.ein||[]).map(z),aus:(sv.aus||[]).map(z),k:(recByName(nm)||{}).kind||""};}
  const ohneX=(arr,x)=>(arr||[]).filter(v=>v!==x);
  function loese(l){const nd=NODEBY.get(l.karte);if(!nd)return;
    if(l.feld)nd.ref[l.feld]=ohneX(nd.ref[l.feld],l.wert);
    }

  // ══ CLIENTS (docs/konzept-akteure.md §4): die Software an der Leitung — je Client EIN Vertrag (IClientVertrag). Er steht als
  //   eigener Rahmen rechts neben den Domänen; seine Karte ist die ANSCHLUSSLEISTE (jede Zeile nennt ihre Nachricht); die Leitungen
  //   bündeln je Ziel-Rahmen. Zoom ändert daran nichts — Einzelkanten gibt es nur durch eine Handlung (Klick auf Zeile/Bündel → AUF).
  const CL_PRE="§client:";
  const istClientDom=d=>typeof d==="string"&&d.startsWith(CL_PRE);
  // Handshake-Name = Interface ohne führendes I (IArbeitsplatz → Arbeitsplatz), wie Abstractions.Akteurvertrag.ClientName.
  const clientAnzeige=c=>{const n=(c&&c.name)||"";return n.length>1&&n[0]==="I"&&n[1]!==n[1].toLowerCase()?n.slice(1):n;};
  const darfHalter=nm=>MODEL.akteure.filter(a=>(a.darf||[]).includes(nm)).map(a=>a.name);
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
    if(kette.length){sek.append(h("div",{class:"gsec",title:"abgeleitet: ein Graph oder eine Frist erzeugt sie aus einer Nachricht dieses Akteurs — kein IDarf nötig"},"👤 über die Kette"));
      kette.forEach(a=>{const ak=MODEL.akteure.find(x=>x.name===a),pfad=kettenPfad(nm,a);
        const row=h("div",{class:"gp-row",title:pfad.join(" → ")},h("span",{class:"gp-t"},"👤 "+a),h("span",{class:"gp-mark"},pfad.join(" → ")));
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
      body.append(h("div",{class:"gsec",title:r.kind==="command"?"Ziel im Typ (ICommand<TAggregat>), abgeleitet aus dem Decider":"abgeleitet aus Decider/Applier"},
        r.kind==="command"?(ag?"Ziel: Aggregat "+ag:"ohne Ziel — Empfänger aus Graph (Von, Kopf) oder Akteur (Arbeit)"):"Aggregat: "+(ag||"— keinem (Decider/Applier verdrahten)")));}
    if(r.kind==="command")body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.istErzeugung=e.target.checked||undefined,...(r.istErzeugung?{checked:"checked"}:{})}),"Erzeugung (ICreationCommand)"));
    // Zwei Seiten je Nachricht (konzept-editor-pipelines §12): ◀ kommt aus (⊕ Erzeuger) · geht an ▶ (⊕ Konsument). Auf der Fläche nur die Ports (Kanten),
    //   im Panel dazu die Partner-Listen. Ein transientes Event (Ablehnung) hat jetzt auch einen Ausgang (→ Projektion/Reaktion/Pipeline).
    {let ein=null,aus=null;
     if(r.kind==="command"){ein=h("div",{},slotRow("sagacmd","+ Erzeuger ⊕ (Funktion · Quelle)","l",{type:"sagaCmd",dir:"in",rec:r.name},"cmd:in:"+r.name),
         r.aggregat?null:slotRow("command","+ Erzeuger ⊕ (Decide — ohne Ziel)","l",{type:"evtOut",dir:"in",rec:r.name},"evt:in:"+r.name));
       aus=slotRow("command","+ Konsument ⊕ ("+(r.aggregat?"Decider":"Funktion · Akteur als Arbeit")+") ▶","r",{type:"cmd",dir:"out",rec:r.name},"cmd:out:"+r.name);}
     else if(r.kind==="event"||r.kind==="rejection"){const c=r.kind==="event"?"event":"rejection";
       ein=slotRow(c,"+ Erzeuger ⊕ ("+(r.kind==="event"?"Decide":"Decide · Funktion")+")","l",{type:"evtOut",dir:"in",rec:r.name},"evt:in:"+r.name);
       aus=slotRow(c,"+ Konsument ⊕ ("+(r.kind==="event"?"Projektion · Akteur · Antwort an Funktion":"Akteur")+") ▶","r",{type:"evtUse",dir:"out",rec:r.name},"evt:out:"+r.name);}
     else if(r.kind==="query")aus=slotRow("query","+ Konsument ⊕ (Reader) ▶","r",{type:"query",dir:"out",rec:r.name},"qry:out:"+r.name);
     else if(r.kind==="queryresponse")ein=slotRow("qrsp","+ Erzeuger ⊕ (Reader)","l",{type:"qrsp",dir:"in",rec:r.name},"qrsp:in:"+r.name);
     // Akteur-Eingang: Command und Query (was ein Akteur hineingeben darf).
     if(akteurSorten().includes(grNachrichtSorte(r.name))){const dp=darfPort(r.name);ein=ein?h("div",{},ein,dp):dp;}
     if(ein||aus){if(INSP)zweiSeiten(body,r.name,ein,aus);else{if(ein)body.append(ein);if(aus)body.append(aus);}}}
    if(r.kind==="valueobject")body.append(slotRow("ftype","als Feldtyp ▶","r",{type:"ftype",dir:"out",typeName:r.name},"ftype:out:rec:"+r.name));
    body.append(h("div",{class:"gsec"},"Felder"));
    (r.felder||[]).forEach((f,fi)=>body.append(feldRow(f,()=>{r.felder.splice(fi,1);render();},undefined,r.name)));
    body.append(h("button",{class:"add",onclick:()=>{(r.felder=r.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(r.felder,"feld"),typ:"string"});render();}},"+ Feld"));
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
      case "handle":{const o=n.own,k=o.kind==="reader"?"reader|"+o.ref.name+"|"+r.query:"projektion|"+o.ref.name+"|"+r.event;
        return [T(o.kind==="reader"?"Reader-Logik":"Handle-Logik",r.rumpf,r._ort,k,true,{kachel:"Handle "+(r.query||r.event||"")})];}
      case "fn":return [T("Impl-Logik",r.rumpf,r._rumpf,"store|"+n.own.ref.name+"|"+r.name,true,{kachel:r.name||"Store-Fn"})];
      // Gerufene Funktion: je Umsetzung je Methode ihr Rumpf (aus der Datei); ✎ als Geste an der Methode.
      case "bausteinfunktion":return istGerufen(r)?(r.umsetzungen||[]).flatMap((u,ii)=>(u.rumpfe||[]).map(x=>T(u.name+"."+x.methode,x.rumpf,x._rumpf,null,true,
        {kachel:u.name+"."+x.methode,impl:ii,methode:x.methode})))
        // Funktion: je Auf sein Rumpf, bei einer Quelle der von Lausche (Ort aus dem Code, ✎ als Geste an der Methode).
        :[...(r.eingaenge||[]).map((e,ei)=>T("Auf("+e.eingang+")",e.rumpf,e._ort,null,true,{kachel:r.name+".Auf("+e.eingang+")",ein:ei})),
          ...((r.lauscht||[]).length?[T("Lausche",r.lauscheRumpf,r._lausche,null,true,{kachel:r.name+".Lausche",lausche:true})]:[])];
      case "client":return (r.implementierungen||[]).length?(r.implementierungen||[]).flatMap(im=>(im.rumpfe||[]).map(x=>T(im.klasse+"."+x.methode,x.rumpf,null,
        "zusage|"+clientAnzeige(r)+"|"+x.event+"@"+im.klasse,im.klasse+"."+x.methode,{python:{datei:im.datei,klasse:im.klasse,methode:x.methode,signatur:x.signatur||null},event:x.event,impl:im}))):null;
    }
    return null;}
  // Die Rumpf-Kachel unter dem Besitzer (wo früher der 📝-Block stand): nur für Rümpfe, die im Code Text haben. Ihr Name ist der des früheren
  //   📝-Blocks — die Kartenhöhe taktet das Zeilenraster, so bleibt die Lage aller Karten gleich.
  const kachelId=(nid,i)=>"rp:"+nid+":"+i;
  function rumpfKacheln(owners){const out=[];owners.forEach(n=>(rumpfTeile(n)||[]).forEach((t,i)=>{if(t.kachel&&t.text&&t.text.trim())
    out.push({id:kachelId(n.id,i),name:typeof t.kachel==="string"?t.kachel:t.label,kind:"rumpf",ref:t,own:n,idx:i});}));return out;}
  // Ein Rumpf-Port im Panel des Besitzers (früher: 📝/🤖 andocken): der Ausschnitt aus der Datei + ✎ + 🤖 — Ziel wie bisher als target.
  function teilVonZiel(t){if(!t)return null;let id=null,i=0;
    if(t.k==="decider")id="dec:"+t.dec;else if(t.k==="applier")id="app:"+t.app;
    else if(t.k==="writeFn"||t.k==="readFn")id="fn:"+t.fn;
    else if(t.k==="pjHandle"||t.k==="rdHandle"){const [coll,pre]=t.k==="pjHandle"?["projektionen","prj"]:["reader","rdr"];
      const o=MODEL[coll].find(x=>x._id===(t.proj||t.reader||t.reaktion));if(o)id=handleIds(o,pre+":"+o._id)[t.hi];}
    else if(t.k==="clientAuf"){id="cl:"+t.client;const c=MODEL.clients.find(x=>x._id===t.client);
      i=c?(c.implementierungen||[]).flatMap((im,ii)=>(im.rumpfe||[]).map(x=>[ii,x.event])).findIndex(([ii,e])=>ii===t.impl&&e===t.event):-1;}
    else if(t.k==="gerufen"){id="fb:"+t.fb;i=(rumpfTeile(NODEBY.get(id))||[]).findIndex(x=>x.impl===t.impl&&x.methode===t.methode);}
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
      h("button",{class:"codeadd",title:"Die Übernahme ist eine Geste — ↶ nimmt sie zurück (der letzte Patch, solange die Datei seitdem unverändert ist)",onclick:()=>llmRueckgaengig(kid)},"↶ Rückgängig")));
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
    body.append(h("div",{class:"gsep"},"Lese-Fähigkeiten — je Fn ein Interface, ◀ Parameter eines Reader-Handles bzw. einer Funktion"));
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


  // Ein Projektions-HANDLE (eigene Karte): Eingang-Event, Schreib-Fähigkeiten (Parameter), Rumpf — eine Projektion gibt nichts aus.
  function prjHandleBody(body,p,hd,hi){
      const sl=port("event");sl.classList.add("i");reg("prj:in:"+p._id+":"+hi,sl,{type:"evtUse",dir:"in",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},"◀ Auf "+(hd.event||"?")),h("button",{class:"rm",onclick:()=>{p.handles.splice(hi,1);render();}},"✕")));
      // Schreib-Fähigkeiten (je Parameter ein Punkt → Store-Fn ziehen); Norm = genau eine.
      (hd.fns||[]).forEach((fid,fj)=>{const r=fnById(fid);const s=port("store");s.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":"+fid,s,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"darf "+(r?r.store.name+"."+r.fn.name:"?")+" ▶"),s));});
      const wo=port("store");wo.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":open",wo,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Write-Fn ▶"),wo));
      body.append(codePort("ctrl:in:"+p._id+":"+hi,{k:"pjHandle",proj:p._id,hi:hi},null,"Controller-Logik"));
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
  const HANDLE_ART={projektion:["projektionen","prj"],reader:["reader","rdr"]};
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
    else if(n.own.kind==="reader")rdHandleBody(body,o,hd,hi);}
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
  // Eine neue Funktion (IFunktion): als Klasse im Host, noch ohne Eingang — Eingänge und Ausgaben verbindet man an ihrer Karte (⊕).
  // Eine Quelle ist eine Funktion mit Lausche statt Auf (docs/10 §10.3.5): was sie meldet, ist ein Command ohne Ziel — als eigene Karte angelegt.
  function neueQuellFunktion(ns,pos){const nm=uniq("NeueQuelle"),cmd=uniq(nm+"Gemeldet");
    MODEL.records.push({name:cmd,kind:"command",namespace:ns,felder:[]});
    MODEL.bausteinFunktionen.push({_id:"fb"+(NID++),name:nm,namespace:ns,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[],lauscht:[cmd],...(pos||{})});return nm;}
  function neueFunktion(ns,pos){const nm=uniq("NeueFunktion");
    MODEL.bausteinFunktionen.push({_id:"fb"+(NID++),name:nm,namespace:ns,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[],...(pos||{})});return nm;}
  const neuKnopf=(titel,on,txt)=>h("button",{class:"rm",title:titel,onclick:on},txt||"＋");

  // ══ GERUFENE FUNKTION (IFunktion ohne Auf, docs/06 §6.5): Signatur (Methoden) · Umsetzungen (je ihr Konstruktor, ⊕ weitere gerufene) · Rümpfe aus
  //   der Datei. Sie steht nur im Konstruktor einer Funktion bzw. Umsetzung (CQRS089) — ⊕ „🧩 braucht". Welche Umsetzung läuft, sagt die Umgebung
  //   (🖥 Host-Rahmen → Funktionen); gebunden wird generiert (GeneratedGerufene).
  const istGerufen=f=>!!f&&(f.methoden||[]).length>0&&!(f.eingaenge||[]).length;
  const gerufenByName=nm=>MODEL.bausteinFunktionen.find(f=>f.name===String(nm||"").replace(/\?$/,"").trim()&&istGerufen(f));
  const paramName=(t,belegt)=>{let n=String(t||"x").replace(/^I(?=[A-ZÄÖÜ])/,"");n=n.charAt(0).toLowerCase()+n.slice(1);
    const b=new Set(belegt||[]);if(!b.has(n))return n;let i=2;while(b.has(n+i))i++;return n+i;};
  // „Typ name, Typ name" → Parameter (Kommas in <…> gehören zum Typ).
  function parseParams(text){const teile=[];let tiefe=0,akt="";for(const ch of String(text||"")){if(ch==="<")tiefe++;if(ch===">")tiefe--;
      if(ch===","&&tiefe===0){teile.push(akt);akt="";}else akt+=ch;}if(akt.trim())teile.push(akt);
    return teile.map(x=>x.trim()).filter(Boolean).map(x=>{const i=x.lastIndexOf(" ");return i<0?{typ:x,name:paramName(x)}:{typ:x.slice(0,i).trim(),name:x.slice(i+1).trim()};});}
  // Eine neue gerufene Funktion — EIN Weg (Funktion „＋", Menü): Schnittstelle I<Stamm> mit einer Methode und einer Umsetzung <Stamm>.
  function neueGerufene(ns,pos){const nm=uniq("INeueFunktion"),impl=uniq(nm.replace(/^I(?=[A-ZÄÖÜ])/,""));
    const f={_id:"fb"+(NID++),name:nm,namespace:ns,klasse:false,eingaenge:[],konstruktor:[],
      methoden:[{name:"Fuehre",rueckgabe:"string",parameter:[{typ:"string",name:"eingabe"}]}],umsetzungen:[{name:impl,konstruktor:[]}],...(pos||{})};
    MODEL.bausteinFunktionen.push(f);return f;}
  // Eine gerufene Funktion in einen Konstruktor nehmen (einmal je Konstruktor, nie sich selbst).
  function nimmGerufene(liste,g,selbst){if(!g||g===selbst||liste.some(p=>gerufenByName(p.typ)===g))return;liste.push({typ:g.name,name:paramName(g.name,liste.map(p=>p.name))});}
  // Die Konstruktoren einer Funktion: der Klasse bzw. der Umsetzung Nr. impl.
  const ctorVon=(f,impl)=>impl==null?(f.konstruktor=f.konstruktor||[]):((f.umsetzungen||[])[impl]||{}).konstruktor;
  // Eine Konstruktor-Zeile: gerufene Funktion (🧩, ✕ löst), Konfig-Record (⚙ Konfig:<Record>) oder Rahmen (ILogger) — Klick öffnet die Karte.
  function konstruktorZeile(p,key,info,loese){const d=gerufenByName(p.typ),rec=!d&&recByName(p.typ),s=d?port("gerufen"):null;
    if(s){s.classList.add("o");reg(key,s,info);}
    return h("div",{class:"slotrow o"},d||rec?h("button",{class:"rm",title:"Aus dem Konstruktor lösen (der Typ bleibt)",onclick:loese},"✕"):null,
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"+(d||rec?";cursor:pointer":""),title:d?"Klick: "+d.name+" öffnen":rec?"Werte aus Konfig:"+rec.name+" der Umgebung":"",
        onclick:d?()=>zeigeLadend("fb:"+d._id):rec?()=>zeigeLadend("rec:"+rec.name):null},
        (d?"🧩 ":rec?"⚙ ":/^IZustand</.test(p.typ||"")?"💾 ":"· ")+p.typ+" "+p.name+(rec?" (Konfig)":/^IZustand</.test(p.typ||"")?" (Zustand, gesichert mit der Position)":"")),s);}
  // „🧩 braucht": die Konstruktor-Zeilen einer Klasse bzw. Umsetzung mit ⊕ für eine weitere gerufene Funktion (＋ legt eine neue an) und ＋ Konfig.
  function ctorAbschnitt(body,f,impl){const liste=ctorVon(f,impl)||[],sfx=impl==null?"":":u"+impl,info={type:"gerufen",dir:"out",fb:f._id,fk:f._id,...(impl==null?{}:{impl})};
    liste.forEach((p,i)=>body.append(konstruktorZeile(p,"gr:out:fb:"+f._id+sfx+":"+i,info,()=>{liste.splice(i,1);gTitel("🧩 "+p.typ+" lösen");render();})));
    const op=port("gerufen");op.classList.add("o");reg("gr:out:fb:"+f._id+sfx+":open",op,info);
    body.append(h("div",{class:"slotrow o"},neuKnopf("Neue Technik als eigene Karte anlegen und in den Konstruktor nehmen",()=>{const g=neueGerufene(f.namespace);
        nimmGerufene(ctorVon(f,impl),g,f);render();deFlash("＋ "+g.name+" angelegt und verbunden — Methoden und Umsetzung an ihrer Karte",true);}),
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"+ Technik ⊕ (⚙) ▶"),op));
    body.append(h("div",{class:"slotrow o"},neuKnopf("Neuen Konfig-Record anlegen und in den Konstruktor nehmen",()=>{const vorher=new Set(MODEL.records.map(x=>x.name));
        neuerKnoten("konfig",undefined,undefined,{ns:f.namespace,reg:"fb:"+f._id,name:(f.name||"").replace(/^I(?=[A-ZÄÖÜ])/,"")+"Konfig"});
        const neu=MODEL.records.find(x=>!vorher.has(x.name));if(neu){ctorVon(f,impl).push({typ:neu.name,name:"konfig"});render();}}),
      h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"＋ Konfig")));}
  // ƒ FUNKTION (IFunktion, ein Block der Hierarchie): je Eingang ein Auf — ◀ Eingang ⊕ (Event · Hinweis · Quelle), Ausgaben ▶ ⊕ (Command bzw.
  //   Event/Hinweis) oder ＋ ein neues Ergebnis-Event, das ihr gehört. Die Ausgaben stehen in der Signatur (CQRS050). Ohne Auf ist sie gerufen:
  //   frei benannte Methoden, ihr Halter nimmt sie im Konstruktor (◀ genutzt von). Eine Klasse läuft im Host und nimmt im Konstruktor gerufene
  //   Funktionen und Konfig. Eine Funktion ist immer eine Klasse; eine Schnittstelle gibt es nur als Technik, mit Umsetzungen (Wahl: Instanz).
  // ⬡ GRAPH (docs/04 §4.4, docs/10 §10.3.4): record X(Knoten…) : IGraph. Je Knoten eine Zeile — Name (Konfig-Schlüssel der Instanz), Typ (Funktion,
  //   Graph im Graphen oder Katalogfunktion), [Kopf] und von wem er bekommt ([Von(nameof(…))], je Quelle gesichert oder verlierbar). Köpfe, Ausgänge
  //   und die Typen auf den Kanten leitet der Server aus dem Code ab; die Kanten stehen auf der Fläche zwischen den Karten der Knoten.
  const typBasis=t=>String(t||"").split("<")[0].trim();
  function graphKnotenKarte(t){const b=typBasis(t),f=MODEL.bausteinFunktionen.find(x=>x.name===b);if(f)return "fb:"+f._id;
    const g=(MODEL.graphen||[]).find(x=>x.name===b);return g?"gr:"+g._id:null;}
  // Wohin ein Typ geht, der den Graphen verlässt (wie die Laufzeit zustellt: Ziel im Typ, sonst Kopf genau eines Graphen oder Arbeit eines Akteurs).
  function graphZiel(t,g){const r=recByName(t);if(!r)return "?";
    if(r.kind==="command"&&r.aggregat)return "■ "+r.aggregat;
    const z=[];if(r.kind==="command")(MODEL.graphen||[]).forEach(x=>{if(x!==g&&(x.koepfe||[]).includes(t))z.push("⬡ "+x.name);});
    MODEL.akteure.forEach(a=>{if((a.hoert||[]).includes(t))z.push((r.kind==="command"?"Arbeit für ":"")+"👤 "+a.name);});
    return z.length?z.join(", "):r.kind==="rejection"?"hört niemand":"⚠ kein Empfänger";}
  function graphHerkunft(t,g){const z=[];
    MODEL.decider.forEach(d=>{if((d.ergibt||[]).some(o=>o.event===t))z.push("Decide "+d.aggregat);});
    MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(t))z.push("👤 "+a.name);});
    (MODEL.graphen||[]).forEach(x=>{if(x!==g&&(x.ausgaenge||[]).includes(t))z.push("⬡ "+x.name);});
    (MODEL.graphen||[]).forEach(x=>(x.knoten||[]).forEach(k=>{if(typBasis(k.typ)===g.name&&(k.von||[]).length)z.push("innen in ⬡ "+x.name);}));
    return z.length?[...new Set(z)].join(", "):"⚠ niemand gibt es hinein";}
  function neuerGraphKnoten(g,typ){const stamm=typBasis(typ).replace(/^I(?=[A-ZÄÖÜ])/,"");let nm=stamm,i=2;
    while((g.knoten||[]).some(k=>k.name===nm))nm=stamm+(i++);
    const kat=(MODEL.katalog||[]).some(x=>x.name===typBasis(typ)),quelle=kat||((MODEL.bausteinFunktionen.find(x=>x.name===typBasis(typ))||{}).lauscht||[]).length>0;
    (g.knoten=g.knoten||[]).push({typ,name:nm,kopf:!g.knoten.length&&!quelle,von:[],verlierbar:[]});gTitel(g.name+": Knoten "+nm);render();}
  function graphKarte(body,g){
    body.append(h("div",{class:"gsec"},"Name · Namespace"));
    body.append(nameInp(g,"name","Graph",null));
    if(!g.datei)body.append(h("input",{value:g.namespace??"",oninput:e=>g.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec",title:"von außen: was keine Quelle im Graphen speist — aus einem Decide, von einem Akteur, vom Ausgang eines anderen Graphen"},"▶ Köpfe (von außen, abgeleitet)"));
    if((g.koepfe||[]).length)g.koepfe.forEach(t=>body.append(h("div",{class:"gp-row",style:"cursor:pointer",title:"Klick: "+t+" öffnen",onclick:()=>zeigeLadend("rec:"+t)},"▶ "+t+"  ← "+graphHerkunft(t,g))));
    else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},g.quelle?"keine — ⚡ Quelle "+g.quelle+" am Kopf":"— (ein Knoten mit [Kopf] fehlt)"));
    body.append(h("div",{class:"gsec",title:"was ein Knoten ausgibt und kein Knoten über Von nimmt"},"Ausgänge ▶ (abgeleitet)"));
    if((g.ausgaenge||[]).length)g.ausgaenge.forEach(t=>body.append(h("div",{class:"gp-row",style:"cursor:pointer",title:"Klick: "+t+" öffnen",onclick:()=>zeigeLadend("rec:"+t)},t+"  → "+graphZiel(t,g))));
    else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},"—"));
    body.append(h("div",{class:"gsec",title:"jeder Parameter des Records ist ein Knoten; die Reihenfolge bedeutet nichts"},"Knoten · [Kopf] · bekommt von [Von]"));
    (g.knoten||[]).forEach((k,i)=>{const id=graphKnotenKarte(k.typ),kat=!id&&(MODEL.katalog||[]).some(x=>x.name===typBasis(k.typ));
      body.append(h("div",{class:"frow"},
        h("input",{value:k.name??"",placeholder:"Knoten",style:"max-width:130px",title:"Name des Knotens = Konfig-Schlüssel in der Instanz",
          onchange:e=>{const v=e.target.value.trim(),alt=k.name;if(!v||v===alt)return;k.name=v;
            (g.knoten||[]).forEach(x=>{x.von=(x.von||[]).map(z=>z===alt?v:z);x.verlierbar=(x.verlierbar||[]).map(z=>z===alt?v:z);});gTitel(g.name+": Knoten "+alt+" → "+v);render();}}),
        h("span",{class:"slotlbl",style:"flex:1"+(id?";cursor:pointer":""),title:id?"Klick: Karte öffnen":kat?"Katalogfunktion des Frameworks":"⚠ Typ fehlt",
          onclick:()=>{if(id)zeigeLadend(id);}},(kat?"⚡ ":id&&id.startsWith("gr:")?"⬡ ":id?"ƒ ":"⚠ ")+k.typ),
        h("label",{class:"cbx",title:"[Kopf]: nimmt von außen, was keine Quelle im Graphen speist — je Eingangstyp genau einer"},
          h("input",{type:"checkbox",onchange:e=>{k.kopf=e.target.checked;gTitel(g.name+": "+k.name+(k.kopf?" ist Kopf":" kein Kopf"));render();},...(k.kopf?{checked:"checked"}:{})}),"Kopf"),
        h("button",{class:"rm",title:"Knoten entfernen (seine Kanten gehen mit)",onclick:()=>{g.knoten.splice(i,1);
          g.knoten.forEach(x=>{x.von=ohneX(x.von,k.name);x.verlierbar=ohneX(x.verlierbar,k.name);});gTitel(g.name+": Knoten "+k.name+" entfernt");render();}},"✕")));
      const andere=(g.knoten||[]).filter(x=>x!==k);
      if(andere.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default;padding-left:16px;white-space:normal"},
        h("span",{class:"slotlbl",style:"opacity:.7"},"bekommt von"),...andere.map(x=>{const an=(k.von||[]).includes(x.name),vl=(k.verlierbar||[]).includes(x.name);
          return h("span",{style:"display:inline-flex;gap:2px"},
            h("button",{class:"akt-dom"+(an?" an":""),title:an?"[Von(nameof("+x.name+"))] entfernen":"[Von(nameof("+x.name+"))] setzen — die Ports, deren Typ "+x.name+" ausgibt",
              onclick:()=>{k.von=an?ohneX(k.von,x.name):[...(k.von||[]),x.name];if(an)k.verlierbar=ohneX(k.verlierbar,x.name);gTitel(g.name+": "+k.name+(an?" nicht mehr":"")+" von "+x.name);render();}},(an?"✓ ":"")+x.name),
            an?h("button",{class:"akt-dom"+(vl?" an":""),title:"gesichert: der Absender wartet auf „fertig“ · verlierbar: einmal geschickt, hält den Kopf nicht auf",
              onclick:()=>{k.verlierbar=vl?ohneX(k.verlierbar,x.name):[...(k.verlierbar||[]),x.name];gTitel(g.name+": "+x.name+" → "+k.name+(vl?" gesichert":" verlierbar"));render();}},vl?"⇢ verlierbar":"gesichert"):null);})));});
    // ＋ Knoten: Funktion, Quelle, Graph im Graphen oder Katalogfunktion — oder eine neue Funktion mit dem getippten Namen.
    const such=h("input",{placeholder:"＋ Knoten: Funktion, Graph oder Katalog suchen — oder neuen Namen tippen"}),liste=h("div",{style:"display:flex;flex-wrap:wrap;gap:4px;margin:4px 0"});
    const kandidaten=()=>[...MODEL.bausteinFunktionen.filter(f=>!istGerufen(f)).map(f=>({n:f.name,a:(f.lauscht||[]).length?"⚡":"ƒ"})),
      ...(MODEL.graphen||[]).filter(x=>x!==g).map(x=>({n:x.name,a:"⬡"})),...(MODEL.katalog||[]).map(x=>({n:x.name,a:"⚡ Katalog ·"}))];
    const zeige=()=>{liste.innerHTML="";const q=such.value.trim(),ql=q.toLowerCase();
      kandidaten().filter(c=>!ql||c.n.toLowerCase().includes(ql)).slice(0,30).forEach(c=>liste.append(h("button",{class:"akt-dom",onclick:()=>neuerGraphKnoten(g,c.n)},c.a+" "+c.n)));
      if(q&&/^[A-Za-zÄÖÜäöüß_][\wÄÖÜäöüß]*$/.test(q)&&!kandidaten().some(c=>c.n===q))liste.append(h("button",{class:"akt-dom",title:"Neue Funktion (Klasse im Host) anlegen und als Knoten einsetzen",
        onclick:()=>{const nm=uniq(q);MODEL.bausteinFunktionen.push({_id:"fb"+(NID++),name:nm,namespace:g.namespace,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[]});neuerGraphKnoten(g,nm);}},"＋ neue Funktion „"+q+"“"));};
    such.oninput=zeige;body.append(such,liste);zeige();
    // Instanzen (docs/10 §10.3.5): ein Graph läuft erst als Instanz — je Umgebung eine Datei graphen/<instanz>.json.
    body.append(h("div",{class:"gsec",title:"umgebungen/"+(WB.umgName||"?")+"/graphen/<instanz>.json"},"Instanzen in „"+(WB.umgName||"?")+"“"));
    const inst=Object.entries(WB.instanzen||{}).filter(([,v])=>v&&v.Graph===g.name);
    inst.forEach(([n])=>body.append(h("div",{class:"gp-row",style:"cursor:pointer",onclick:()=>waehle("host:inst:"+n)},"⬡ "+n)));
    const innen=(MODEL.graphen||[]).filter(x=>(x.knoten||[]).some(k=>typBasis(k.typ)===g.name));
    if(!inst.length)body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},innen.length?"keine eigene — läuft als Knoten in "+innen.map(x=>x.name).join(", "):"⚠ keine — er läuft erst als Instanz"));
    if(g.datei)body.append(h("button",{class:"add",onclick:()=>instanzAnlegen(g.name)},"＋ Instanz in „"+(WB.umgName||"?")+"“"));}
  // Eine Quelle (Lausche statt Auf): was sie je Element meldet — Commands ohne Ziel; nichts davon steht im Log, was sie gesehen hat, hält ihr Zustand.
  function quellAbschnitt(body,f){if(!(f.lauscht||[]).length&&(f.eingaenge||[]).length)return;
    body.append(h("div",{class:"gsec",title:"IAsyncEnumerable<…> Lausche(CancellationToken) — eine Quelle steht am Kopf ihres Graphen"},"⚡ Quelle: meldet (Lausche) ▶"));
    (f.lauscht||[]).forEach(a=>{const s=anchorDot("sagacmd");s.classList.add("o");
      body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",title:"nicht mehr melden",onclick:()=>{f.lauscht=ohneX(f.lauscht,a);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right;cursor:pointer",onclick:()=>zeigeLadend("rec:"+a)},"→ "+a),s));});
    body.append(slotRow("sagacmd","+ meldet ⊕ (Command ohne Ziel) ▶","r",{type:"sagaCmd",dir:"out",fb:f._id,ein:"lauscht"},"fb:lauscht:"+f._id));
    {const z=(f.konstruktor||[]).find(p=>/^IZustand</.test(p.typ||""));
      body.append(h("div",{class:"gsec",style:"opacity:.8"},z?"Zustand: "+z.typ+" — gesichert je Instanz und Knoten":"ohne Zustand — was sie gesehen hat, wüsste sie nach einem Neustart nicht"));}}
  function funktionsKarte(body,f){
    const gerufen=istGerufen(f);
    if(gerufen){body.append(topSlot("gerufen","◀ genutzt von ⊕ (Funktion · Umsetzung)",{type:"gerufen",dir:"in",fb:f._id,fk:f._id},"gr:in:"+f._id));
      const nutzer=MODEL.bausteinFunktionen.filter(q=>q!==f&&[q.konstruktor||[],...(q.umsetzungen||[]).map(u=>u.konstruktor||[])].some(l=>l.some(p=>gerufenByName(p.typ)===f)));
      if(nutzer.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},
        ...nutzer.map(q=>h("button",{class:"akt-dom",onclick:()=>waehle("fb:"+q._id)},"ƒ "+q.name))));}
    body.append(h("div",{class:"gsec"},"Name · Namespace · Form"));
    if(!f.datei){body.append(nameInp(f,"name","Funktion","bausteinfunktion"));
      body.append(h("input",{value:f.namespace??"",oninput:e=>f.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
      // Eine Funktion ist immer eine Klasse (CQRS100; draußen rechnet die Arbeit eines Akteurs, E9) — wählen lässt sich die Form nur für Technik:
      //   eine Klasse oder eine Schnittstelle, deren Umsetzung die Instanz wählt.
      if(gerufen)body.append(h("label",{class:"cbx",title:"Technik als Klasse — oder als Schnittstelle mit Umsetzungen (welche läuft, wählt der Knoten der Instanz)"},
        h("input",{type:"checkbox",onchange:e=>{f.klasse=e.target.checked;render();},...(f.klasse?{checked:"checked"}:{})}),"Klasse (ohne Schnittstelle)"));}
    else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},f.name+" — "+(gerufen?"gerufen · ":"")+(f.klasse?"Klasse im Host"
      :"Schnittstelle"+((f.umsetzungen||[]).length?" · Umsetzung "+f.umsetzungen.map(u=>u.name).join(", "):gerufen?" · ⚠ ohne Umsetzung":" · ⚠ eine Funktion ist eine Klasse (CQRS100)"))));
    if(!(f.eingaenge||[]).length)methodenAbschnitt(body,f);
    if(!gerufen){
    body.append(h("div",{class:"gsec",title:"je Eingang ein Auf(Eingang[, Herkunft]) — was entstehen kann, steht in seiner Rückgabe"},"◀ Eingänge · Ausgaben ▶"));
    (f.eingaenge||[]).forEach((e,i)=>{
      const ip=anchorDot(recArt(e.eingang)==="rejection"?"rejection":"event");ip.classList.add("i");
      reg("fb:ein:"+f._id+":"+i,ip,{type:"evtUse",dir:"in",fb:f._id,fk:f._id,ein:i,anker:true});
      body.append(h("div",{class:"slotrow"},ip,h("span",{class:"slotlbl",style:"flex:1;cursor:pointer",title:"Klick: "+e.eingang+" öffnen",onclick:()=>zeigeLadend("rec:"+e.eingang)},
          "Auf("+e.eingang+")"+(recArt(e.eingang)==="event"?" · Antwort auf den eigenen Command":"")),
        h("label",{class:"cbx",title:"Herkunft h: Stream, Version, Log-Zeit, Akteur, NeueId — für abgeleitete Ids"},h("input",{type:"checkbox",onchange:ev=>{e.herkunft=ev.target.checked;render();},...(e.herkunft?{checked:"checked"}:{})}),"Herkunft"),
        h("label",{class:"cbx",title:"Strom: keine bis viele Ausgaben je Ankunft (IEnumerable bzw. IAsyncEnumerable) — sonst genau eine (T) bzw. einer der Fälle (OneOf)"},h("input",{type:"checkbox",onchange:ev=>{e.strom=ev.target.checked;render();},...(e.strom?{checked:"checked"}:{})}),"Strom"),
        h("button",{class:"rm",title:"Eingang entfernen (das Auf fällt weg)",onclick:()=>{f.eingaenge.splice(i,1);gTitel("Eingang "+e.eingang+" entfernen");render();}},"✕")));
      (e.ausgaben||[]).forEach(a=>{const cmd=recArt(a)==="command",s=anchorDot(cmd?"sagacmd":recArt(a)==="rejection"?"rejection":"event");s.classList.add("o");
        reg("fb:aus:"+f._id+":"+i+":"+a,s,{type:cmd?"sagaCmd":"evtOut",dir:"out",fb:f._id,fk:f._id,ein:i,anker:true});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",title:"Ausgabe lösen (der Typ bleibt)",onclick:()=>{e.ausgaben=ohneX(e.ausgaben,a);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right;cursor:pointer",title:"Klick: "+a+" öffnen",onclick:()=>zeigeLadend("rec:"+a)},"→ "+a+(cmd?" (Command)":"")),s));});
      body.append(slotRow("sagacmd","+ Ausgabe ⊕ (Command) ▶","r",{type:"sagaCmd",dir:"out",fb:f._id,fk:f._id,ein:i},"fb:ausC:"+f._id+":"+i));
      body.append(slotRow("rejection","+ Ausgabe ⊕ (Hinweis) ▶","r",{type:"evtOut",dir:"out",fb:f._id,fk:f._id,ein:i},"fb:ausE:"+f._id+":"+i));
      body.append(h("div",{class:"slotrow o"},neuKnopf("Neuen Command ohne Ziel anlegen — der nächste Knoten im Graphen nimmt ihn (Von)",()=>{
          const stamm=(f.name||"Funktion").replace(/^I(?=[A-ZÄÖÜ])/,""),vorher=new Set(MODEL.records.map(x=>x.name));
          neuerKnoten("command",undefined,undefined,{ns:f.namespace,reg:"fb:"+f._id,name:stamm+"Ergebnis",still:true});
          const neu=MODEL.records.find(x=>!vorher.has(x.name));if(neu){(e.ausgaben=e.ausgaben||[]).push(neu.name);render();}}),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"＋ neuer Command ohne Ziel")));});
    body.append(slotRow("command","+ Eingang ⊕ (Command ohne Ziel)","l",{type:"cmd",dir:"in",fb:f._id,ein:"open"},"fb:einC:"+f._id+":open"));
    // Ein neuer Eingang: eine neue Nachricht (Command ohne Ziel) im Namespace der Funktion — im Katalog bleibt sie im Katalog — samt ihrem Auf.
    body.append(h("div",{class:"slotrow"},neuKnopf("Neue Nachricht (Command ohne Ziel) anlegen und ein Auf dafür",()=>{
        const stamm=(f.name||"Funktion").replace(/^I(?=[A-ZÄÖÜ])/,""),vorher=new Set(MODEL.records.map(x=>x.name));
        neuerKnoten("command",undefined,undefined,{ns:f.namespace,reg:"fb:"+f._id,name:stamm+"Auftrag",still:true});
        const neu=MODEL.records.find(x=>!vorher.has(x.name));if(neu){(f.eingaenge=f.eingaenge||[]).push({eingang:neu.name,ausgaben:[]});gTitel("ƒ "+f.name+": Auf("+neu.name+")");render();}}),
      h("span",{class:"slotlbl",style:"flex:1"},"＋ neuer Eingang (Nachricht und Auf)")));
    body.append(slotRow("event","+ Antwort ⊕ (Event, das ein eigener Command entschied)","l",{type:"evtUse",dir:"in",fb:f._id,fk:f._id,ein:"open"},"fb:ein:"+f._id+":open"));
    quellAbschnitt(body,f);}
    if(f.klasse){body.append(h("div",{class:"gsec",title:"Konstruktor der Klasse: Konfig-Records (Werte am Knoten der Instanz), Technik, IZustand<T>, ILogger<T>"},"⚙ braucht (Konstruktor)"));
      ctorAbschnitt(body,f,null);}
    else umsetzungsAbschnitt(body,f,gerufen);}
  // Signatur einer gerufenen Funktion: je Methode Rückgabe, Name, Parameter (an der Schnittstelle bzw. Klasse; eine Umsetzung bekommt einen Stub).
  function methodenAbschnitt(body,f){
    body.append(h("div",{class:"gsec",title:(f.klasse?"public sealed class ":"public interface ")+f.name+" : IFunktion { Rückgabe Methode(Typ name, …); … } — ohne Auf: ihr Halter ruft sie"},
      "⚙ Methoden (Technik)"+((f.methoden||[]).length?"":" (keine — mit ＋ wird sie Technik: ihr Halter ruft sie, statt dass sie Eingänge hört)")));
    (f.methoden||[]).forEach((m,mi)=>body.append(h("div",{class:"frow"},
      h("input",{value:m.rueckgabe??"",list:"de-typen",placeholder:"Rückgabe",style:"max-width:90px",onchange:e=>{m.rueckgabe=e.target.value.trim()||"void";render();}}),
      // Eine Methode aus dem Code: Umbenennen ist ein Refactoring (Renamer — Schnittstelle UND Umsetzungen); neu: einfach der Name.
      h("input",{value:m.name??"",placeholder:"Methode",style:"max-width:110px",onchange:e=>{const v=e.target.value.trim(),alt=m.name;if(!v||v===alt)return;
        if(m._ort&&f.datei){gUmbenennen(m,v,alt);return;}m.name=v;render();}}),
      h("input",{value:(m.parameter||[]).map(p=>p.typ+" "+p.name).join(", "),placeholder:"Typ name, …",title:"Parameter: Typ name, Typ name",
        onchange:e=>{m.parameter=parseParams(e.target.value);render();}}),
      h("button",{class:"rm",title:"Methode entfernen (an der Schnittstelle; die Umsetzungen behalten ihren Code)",onclick:()=>{f.methoden.splice(mi,1);render();}},"✕"))));
    body.append(h("button",{class:"add",onclick:()=>{const da=new Set((f.methoden||[]).map(x=>x.name));let n="Neu",i=2;while(da.has(n))n="Neu"+(i++);
      (f.methoden=f.methoden||[]).push({name:n,rueckgabe:"void",parameter:[]});render();}},"+ Methode"));}
  // Umsetzungen einer Schnittstelle: je Klasse ihr Konstruktor (⊕ gerufene Funktionen), bei einer gerufenen je Methode ihr Rumpf; ＋ weitere.
  function umsetzungsAbschnitt(body,f,gerufen){
    body.append(h("div",{class:"gsec",title:"Klassen, die "+f.name+" implementieren — welche läuft, wählt die Umgebung (Funktionen:"+f.name+":Implementierung)"},"Umsetzungen"));
    if(!(f.umsetzungen||[]).length)body.append(h("div",{class:"gsec",style:"opacity:.7"},gerufen
      ?"⚠ keine — ohne Umsetzung bricht der Start, sobald eine Funktion sie braucht.":"⚠ eine Funktion ist eine Klasse (CQRS100) — draußen rechnet die Arbeit eines Akteurs."));
    (f.umsetzungen||[]).forEach((u,ii)=>{
      body.append(h("div",{class:"frow",title:"public sealed class "+u.name+"(…) : "+f.name},h("span",{class:"slotlbl"},"▸"),
        h("input",{value:u.name,placeholder:"Klasse",onchange:e=>{const v=e.target.value.trim(),alt=u.name;if(!v||v===alt)return;
          if(u._ort&&u.datei){gUmbenennen(u,v,alt);return;}u.name=v;render();}}),u.datei?null:h("span",{class:"gp-mark"}," neu")));
      ctorAbschnitt(body,f,ii);
      if(gerufen)(f.methoden||[]).forEach(m=>body.append(codePort("gr:impl:"+f._id+":"+ii+":"+m.name,{k:"gerufen",fb:f._id,impl:ii,methode:m.name},null,u.name+"."+m.name)));});
    {const ein=h("input",{placeholder:"Klassenname …"});
      body.append(h("div",{class:"frow"},ein,h("button",{class:"act",title:"Eine weitere Umsetzung (Klasse mit throw-Stubs) — z. B. ein Fake für die Sim-Umgebung",
        onclick:()=>{const n=ein.value.trim();if(!n||uniq(n)!==n){deFlash("⚠ Name fehlt oder ist vergeben",false);return;}
          (f.umsetzungen=f.umsetzungen||[]).push({name:n,konstruktor:[]});render();}},"＋ Umsetzung")));}
    if(gerufen){const lo=Object.entries((MODEL.rahmen||{}).umgebungen||{}).map(([n,u])=>[n,((u||{}).Funktionen||{})[f.name]]);
      const einzig=(f.umsetzungen||[]).length===1?f.umsetzungen[0].name:null;
      if(lo.length)body.append(h("div",{class:"gsec",style:"opacity:.8"},"Umsetzung je Umgebung: "+lo.map(([n,k])=>n+" = "+(k&&k.Implementierung?k.Implementierung
        :einzig?einzig+" (die einzige)":"⚠ wählen")).join(" · ")+" — eingestellt im 🖥 Host-Rahmen (Funktionen)"));}}
  const recArt=nm=>(recByName(nm)||{}).kind||"";
  // ══ AKTEURE: von wem kommt was? (docs/konzept-akteure.md §2 und §8) ══
  //   Deklariert ist nur IDarf (direkt); alles Abgeleitete (Kette, Akteur-Menge je Karte, Akteur-Rahmen, Reihenfolge, Pfade) rechnet der Server
  //   (DomainEditor/AkteurAnteile.cs → rahmen.akteurMengen, GraphExtractor/Sicht → board.sicht). Hier wird nur nachgeschlagen.
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
  // ── BESITZ (vom Server, board.sicht.besitz): je Karte ihr Elternknoten — Hierarchie Anwendung → Blöcke → Glieder, nur aus den Signaturen.
  //   Die oberste Ebene unter der Anwendung ist die REGION einer Karte: ein Block (Aggregat, Funktion, Sicht = Store-Bündel), die
  //   Begriffe (gemeinsame Typen) oder der Rand (Akteure, Clients, Quellen). „!" = mehrdeutig (zwei Erzeuger, zwei Antwortende, zwei Stores).
  const RAND_KEY="§rand",BEGR_KEY="§begriffe",ANW_KEY="§anwendung";
  const besitzRoh=id=>(SICHT().besitz||{})[id]||null;
  const besitzVon=id=>{const b=besitzRoh(id);return b?b.replace(/!$/,""):null;};
  const mehrdeutig=n=>!!n&&(besitzRoh(n.id)||"").endsWith("!");
  function regionVon(n){if(!n)return null;if(n.kind==="rumpf")return regionVon(n.own);   // die Code-Kachel steht unter ihrem Besitzer
    let id=n.id,b=besitzVon(id);
    for(let i=0;b&&!b.startsWith("§")&&i<8;i++){id=b;b=besitzVon(b);}
    return b===ANW_KEY?id:b;}
  const istRegionKarte=ns=>typeof ns==="string"&&/^(agg|gr|fb|dn|sto):/.test(ns);
  // 👤 AKTEUR: Name, Namespace, und EINE Liste — was er darf (IDarf<T>). ⊕ „+ darf" → passende Commands/Queries
  //   leuchten → anklicken = erlauben, ✓-Karte = entziehen. Was er HÖREN darf, ist abgeleitet (Generator), keine Eingabe.
  function akteurCard(body,a){
    body.append(h("div",{class:"gsec"},"Name · Namespace"));
    body.append(nameInp(a,"name","AkteurName","akteur"));
    body.append(h("input",{value:a.namespace??"",oninput:e=>a.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec",title:"public sealed record "+a.name+" : IAkteur, IDarf<…>, IHoert<…>"},"darf ▶ — was er SELBST hineingibt (Command, Query, Hinweis)"));
    if(!(a.darf||[]).length)body.append(h("div",{class:"gp-row gp-leer"},"— noch nichts (am Tor käme er nirgends durch)"));
    (a.darf||[]).forEach(nm=>{const r=recByName(nm);
      const art=r?kindLabel(r.kind):"⚠ unbekannt";
      const s=anchorDot("darf");s.classList.add("o");reg("akt:darf:"+a._id+":"+nm,s,{type:"darf",dir:"out",akt:a._id,anker:true});
      body.append(h("div",{class:"slotrow o"},
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},nm+" · "+art),
        h("button",{class:"rm",title:"Recht entziehen",onclick:()=>{a.darf=a.darf.filter(x=>x!==nm);render();}},"✕"),s));});
    body.append(slotRow("darf","+ darf ⊕ ("+akteurDarfLabel()+") ▶","r",{type:"darf",dir:"out",akt:a._id},"akt:darf:"+a._id+":open"));
    // Was zu ihm kommt (IHoert<T>): ein Command ohne Ziel ist ARBEIT (die Werkstatt gibt ihn einer Sitzung, die ihn verkörpert, bis eine Antwort
    //   kommt), ein Event oder Hinweis KENNTNIS (gepusht, verlierbar).
    body.append(h("div",{class:"gsec",title:"IHoert<T> — Command ohne Ziel = Arbeit, Event/Hinweis = Kenntnis"},"hört ◀ — Arbeit und Kenntnis"));
    if(!(a.hoert||[]).length)body.append(h("div",{class:"gp-row gp-leer"},"— nichts"));
    (a.hoert||[]).forEach(nm=>{const r=recByName(nm),arbeit=r&&r.kind==="command";
      body.append(h("div",{class:"slotrow"},anchorDot(arbeit?"command":r&&r.kind==="rejection"?"rejection":"event"),
        h("span",{class:"slotlbl",style:"flex:1;cursor:pointer",onclick:()=>zeigeLadend("rec:"+nm)},nm+" · "+(arbeit?"Arbeit":"Kenntnis")),
        h("button",{class:"rm",title:"hört es nicht mehr",onclick:()=>{a.hoert=ohneX(a.hoert,nm);render();}},"✕")));});
    body.append(slotRow("command","+ Arbeit ⊕ (Command ohne Ziel)","l",{type:"cmd",dir:"in",akt:a._id,hoert:true},"akt:arbeit:"+a._id));
    body.append(slotRow("event","+ Kenntnis ⊕ (Event · Hinweis)","l",{type:"evtUse",dir:"in",akt:a._id,hoert:true},"akt:kenntnis:"+a._id));
    // Was in seinem Namen über eine Kette entsteht (abgeleitet, nicht deklariert): Fluss/Prozess aus seinen Events.
    {const bew=Object.entries((MODEL.rahmen||{}).akteurMengen||{}).filter(([nm,m])=>(m.kette||[]).includes(a.name)&&!(m.direkt||[]).includes(a.name)&&istEingangName(nm)).map(([nm])=>nm).sort();
      body.append(h("div",{class:"gsec",title:"abgeleitet: Commands, die ein Graph oder eine Frist in seinem Namen erzeugt — kein IDarf nötig, am Tor nicht erlaubt"},"bewirkt über die Kette (abgeleitet)"));
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},bew.length?bew.join(", "):"— nichts"));}
    const wirkt=akteurDomaenen(a.name);
    if(wirkt.length){body.append(h("div",{class:"gsec"},"wirkt in"));
      body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px"},...wirkt.map(d=>h("button",{class:"akt-dom",title:"Rahmen "+domLabel(d)+" · "+a.name+" einpassen",
        onclick:()=>{const fr=world&&[...world.querySelectorAll(".grahmen.akt")].find(x=>x.dataset.ns===d&&x.dataset.akt===a.name);
          if(fr)einpassenRahmen({x1:fr.offsetLeft,y1:fr.offsetTop,x2:fr.offsetLeft+fr.offsetWidth,y2:fr.offsetTop+fr.offsetHeight});}},domLabel(d)))));}
    // 🔌 Clients (docs/konzept-akteure.md §4): wer diesen Vertrag trägt (Port für „trägt ⊕") und wo der Akteur sonst ankommt.
    // 🔌 verkörpert von: Port für „verkörpert ⊕" am Client (IVerkoerpert<A>).
    {const tp=port("traegt");tp.classList.add("i");reg("akt:verkoerpert:"+a._id,tp,{type:"verkoerpert",dir:"in",akt:a._id});
      const cs=MODEL.clients.filter(c=>(c.verkoerpert||[]).includes(a.name));
      body.append(h("div",{class:"slotrow"},tp,h("span",{class:"slotlbl",style:"flex:1"},"🔌 verkörpert von: "+(cs.length?cs.map(clientAnzeige).join(", "):"keinem Client"))));}
    {const cs=MODEL.clients.filter(c=>clientAkteure(c).includes(a.name));
      if(cs.length)body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default"},h("span",{class:"slotlbl"},"🔌 Clients:"),
        ...cs.map(c=>h("button",{class:"akt-dom",onclick:()=>waehle("cl:"+c._id)},clientAnzeige(c)))));}
  }
  // 🔌 CLIENT-Panel (docs/konzept-akteure.md §4): ein Record, der nur nennt, wen er verkörpert (IVerkoerpert<A>, ⊕ → Akteure leuchten).
  //   Was über seine Leitung geht — was er sendet, fragt, als Arbeit annimmt und zur Kenntnis hört —, folgt aus IDarf und IHoert der Akteure.
  function clientCard(body,c){
    body.append(h("div",{class:"gsec",title:"public sealed record "+c.name+" : IClient, IVerkoerpert<…>"},"Name · Namespace"));
    if(!c.datei)body.append(nameInp(c,"name","IClient","client"));
    else body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default"},c.name+" — aus dem Code (Umbenennen ist ein Refactoring)"));
    body.append(h("input",{value:c.namespace??"",oninput:e=>c.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},"Handshake: „"+clientAnzeige(c)+"“ · Python-Basis "+clientAnzeige(c)
      +"Basis (domain_client/generated/vertraege.py) · Server: GeneratedClientVertraege"));
    // 👤 verkörpert: die Akteure (IVerkoerpert<A>) — ⊕ → Akteure leuchten → anklicken = verbinden; ✕ = lösen.
    body.append(h("div",{class:"gsec",title:"IVerkoerpert<A> — befugt ist der Akteur: was über die Leitung geht, sind seine IDarf. Am Handshake gilt: ∩ Akteure des Tokens"},"👤 verkörpert"));
    (c.verkoerpert||[]).forEach(n=>{const a=MODEL.akteure.find(x=>x.name===n),s=anchorDot("traegt");s.classList.add("o");
      reg("cl:verkoerpert:"+c._id+":"+n,s,{type:"verkoerpert",dir:"out",client:c._id,anker:true});
      body.append(h("div",{class:"slotrow o"},h("button",{class:"akt-dom",style:"flex:1;text-align:right",onclick:()=>a&&waehle("akt:"+a._id)},"👤 "+n+(a?"":" · ⚠ kein Akteur")),
        h("button",{class:"rm",title:"nicht mehr verkörpern",onclick:()=>{c.verkoerpert=ohneX(c.verkoerpert,n);render();}},"✕"),s));});
    body.append(slotRow("traegt","+ verkörpert ⊕ (Akteur) ▶","r",{type:"verkoerpert",dir:"out",client:c._id},"cl:verkoerpert:"+c._id+":open"));
    // ↺ trägt · ▶ sendet · ? fragt: abgeleitet aus den IDarf der verkörperten Akteure (nur lesend; ändern am Akteur).
    {const ps=clientPorts(c).filter(p=>p.ri!=="kenntnis");
      body.append(h("div",{class:"gsec",title:"aus IDarf und IHoert der verkörperten Akteure — Arbeit (Command ohne Ziel), gesendet (Command, Hinweis), gefragt (Query)"},"über die Leitung (abgeleitet)"));
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},ps.length
        ?ps.map(p=>(p.ri==="traegt"?"↺ ":p.ri==="fragt"?"? ":"▶ ")+p.msg+" · als "+(p.akteure||[]).join("/")).join("\n"):"— nichts"));}
    // ◀ hört (Kenntnis): abgeleitet aus dem IHoert seiner Akteure — geändert wird am Akteur.
    body.append(h("div",{class:"gsec",title:"Events und Hinweise, die seine Akteure hören (IHoert) — verlierbar gepusht"},"◀ hört (abgeleitet)"));
    body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},(c.kenntnis||[]).length?c.kenntnis.join(", "):"— nichts"));
    clientImplementierung(body,c,null);
    // Die Leitungen dieses Clients (je Ziel-Rahmen ein Bündel) — Klick fächert auf.
    const bs=clientBuendel(c);
    if(bs.length){body.append(h("div",{class:"gsec"},"🔌 Leitungen (je Ziel-Rahmen)"));
      bs.forEach(b=>{const row=h("div",{class:"gp-row",title:buendelText(b)},h("span",{class:"gp-t"},rahmenLabel(b.key)),
        h("span",{class:"gp-mark"},(b.durabel?"durabel · ":"")+buendelText(b)));row.onclick=()=>auffaechern(c,{keys:[b.key]});body.append(row);});}
  }
  // ⚙ IMPLEMENTIERUNG eines Clients — das Verhalten des Akteurs DRAUSSEN: je Arbeit/Kenntnis ein Logik-Element (◀ Event, Ausgänge ▶ =
  //   Ausgabe-Vertrag, 📝 Rumpf + optional 🤖). Code-Fakt: eine Python-Klasse, die die aus dem Vertrag generierte Basis erbt.
  //   nurEvent: nur diese eine Zusage (Ansicht an der Zusage-Karte).
  function clientImplementierung(body,c,nurEvent){const ims=c.implementierungen||[];
    if(!nurEvent)body.append(h("div",{class:"gsec",title:"class X("+clientAnzeige(c)+"Basis[…]) im Python-Projekt — die Basis erzeugt der Codegen aus dem Vertrag; je Auf ihre Methode"},"⚙ Implementierung (Verhalten draußen)"));
    if(!ims.length){if(nurEvent)return;
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},
        "— noch keine: eine Python-Klasse, die "+clientAnzeige(c)+"Basis erbt (die Basis entsteht beim Bauen aus dem Vertrag)."));
      const pk=(MODEL.rahmen||{}).pythonPaket;
      if(pk)body.append(h("button",{class:"add",title:"⟳ legt nach dem Bauen der Basis "+pk+"/… an: die Klasse mit je Arbeit/Kenntnis der Methode der generierten Basis (Platzhalter-Rumpf) — danach je Methode 📝/🤖",
        onclick:()=>{const kl=clientAnzeige(c)||"Client";(c.implementierungen=c.implementierungen||[]).push({sprache:"python",klasse:kl,
          datei:pk+"/"+kl.replace(/(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])/g,"_$1$2").toLowerCase()+".py",rumpfe:[],neu:true});render();}},"+ 🐍 Python-Implementierung"));
      return;}
    ims.forEach((im,ii)=>{const rs=(im.rumpfe||[]).filter(x=>!nurEvent||x.event===nurEvent);if(!rs.length&&!(im.neu&&!nurEvent))return;
      if(im.neu&&!rs.length&&!nurEvent){body.append(h("div",{class:"gsec",style:"opacity:.85",title:im.datei},"🐍 "+im.klasse+" · "+im.datei+" (neu)"),
        h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},"Entsteht beim nächsten ⟳ (nach dem Bauen der Basis) — je Arbeit/Kenntnis eine Methode, dann hier je Methode 📝/🤖."),
        h("button",{class:"rm",title:"doch nicht anlegen",onclick:()=>{c.implementierungen.splice(ii,1);render();}},"✕"));return;}
      body.append(h("div",{class:"gsec",style:"opacity:.85",title:im.datei},"🐍 "+im.klasse+" · "+(im.datei||"").split("/").pop()+(nurEvent?" · 🔌 "+clientAnzeige(c):"")));
      rs.forEach(x=>{if(!nurEvent)body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl",style:"flex:1"},
          "◀ "+x.event+((x.ausgaenge||[]).length?"  → "+x.ausgaenge.join(" | ")+" ▶":"  · Kenntnis"))));
        body.append(codePort("cla:"+c._id+":"+ii+":"+x.event+(nurEvent?":z":""),{k:"clientAuf",client:c._id,impl:ii,event:x.event},null,
          x.methode+(x.rumpf==null?" (fehlt)":"")));});});}

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

  // Eine 🤖-Übernahme ist eine Rumpf-Geste — zurück geht sie über das EINE ↶ der Werkbank (Verlauf), nicht über eine eigene Sicherung.
  async function llmRueckgaengig(kid){if(LAUF[kid])return;LAUF[kid]={seit:Date.now(),was:"nimmt die Geste zurück"};neuZeichnen();
    let r;try{r=await fetch("/api/werkbank/rueckgaengig",{method:"POST"}).then(x=>x.json());}catch(e){r={ok:false,fehler:"Werkbank offline"};}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+(r.fehler||"nichts zurückgenommen")};neuZeichnen();return;}
    delete ZULETZT[kid];MELD[kid]={k:"",t:r.titel};await neuEinlesen();}

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
  const NODELABEL={host:"Host",graph:"Graph",bausteinfunktion:"Funktion",akteur:"Akteur",client:"Client",command:"Command",event:"Event",rejection:"Ablehnung",valueobject:"Value Object",enum:"Enum",aggregate:"Aggregat",decider:"Decider",applier:"Applier",query:"Query",queryresponse:"Response",readmodel:"Read Model",store:"Store",projektion:"Projektion",reader:"Reader",rumpf:"Code",code:"Code",state:"State",konfig:"Konfiguration",handle:"Handle",fn:"Store-Fn",gerufen:"Technik"};
  let PAN={x:40,y:30,s:1}, canvas=null, world=null, svg=null, svgTop=null, SLOTS={};
  // Typ-Navigation: HLKIND = aktuell hervorgehobener Node-Typ (Board+Minimap); JUMPIX = Sprung-Cursor je Typ.
  let HLKIND=null; const JUMPIX={};
  // Viewport auf einen Knoten zentrieren.
  //   Eine Karte der Sicht → die (erste) Karte der Fläche, die sie zeigt.
  function centerOn(n){if(!canvas||!world)return;const el=world.querySelector('.gnode2[data-id="'+CSS.escape(n.id)+'"],.gnode2[data-karte="'+CSS.escape(n.id)+'"]');if(!el)return;
    const cr=canvas.getBoundingClientRect();
    PAN.x=cr.width/2-(el.offsetLeft+el.offsetWidth/2)*PAN.s;PAN.y=cr.height/2-(el.offsetTop+el.offsetHeight/2)*PAN.s;applyPan();}
  // Kurzer Puls-Ring auf einem Knoten (nach dem Sprung).
  function pulseNode(n){const el=world&&world.querySelector('[data-id="'+vertreterId(n.id)+'"]');if(!el)return;
    el.classList.add("pulse");setTimeout(()=>el.classList.remove("pulse"),900);}
  // Einsame Inseln (unverbundene Knoten aus der Graph-Analyse) — hervorheben + der Reihe nach anspringen.
  let ISLE=new Set();
  const dec=id=>MODEL.decider.find(d=>d._id===id);
  const app=id=>MODEL.applier.find(a=>a._id===id);
  // ZUGEHÖRIGKEIT = BEZIEHUNG, nie Namespace oder Name — abgeleitet auf dem Server (GraphExtractor/Sicht: Block, Aggregat je Karte).
  const recByName=n=>MODEL.records.find(r=>r.name===n);
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
  // Read-only-Anker (nicht ziehbar) — nur Ankerpunkt für abgeleitete Kanten.
  function anchorDot(color){return h("div",{class:"slot s-"+color+" ro"});}
  function topAnchor(color,label,key){const s=anchorDot(color);s.classList.add("t");reg(key,s,null);return h("div",{class:"gtopfield"},s,h("span",{class:"slotlbl"},label));}
  // Namens-Eingabe: benennt um UND zieht alle Verbindungen mit (Namen sind der Referenzschlüssel).
  function renameRefs(kind,obj,old,nv){
    // Graph-Knoten, deren Typ der umbenannte ist, ziehen mit (Funktion, Graph im Graphen).
    (MODEL.graphen||[]).forEach(g=>(g.knoten||[]).forEach(k=>{if(typBasis(k.typ)===old)k.typ=k.typ.replace(old,nv);}));
    // Eine noch nicht geschriebene Funktion: die Konstruktoren, die sie nehmen, ziehen mit (aus dem Code: Roslyn-Renamer).
    if(kind==="bausteinfunktion"){const um=p=>{if(String(p.typ||"").replace(/\?$/,"").trim()===old)p.typ=nv;};
      MODEL.bausteinFunktionen.forEach(q=>{(q.konstruktor||[]).forEach(um);(q.umsetzungen||[]).forEach(u=>(u.konstruktor||[]).forEach(um));});
      MODEL.akteure.forEach(a=>{a.darf=(a.darf||[]).map(x=>x===old?nv:x);});}
    if(kind==="akteur"){
      // Clients, die ihn verkörpern, ziehen mit (IVerkoerpert<A> folgt dem Akteur).
      MODEL.clients.forEach(c=>{c.verkoerpert=(c.verkoerpert||[]).map(t=>t===old?nv:t);});}
    if(kind==="aggregate"){
      MODEL.decider.forEach(d=>{if(d.aggregat===old)d.aggregat=nv;});
      MODEL.applier.forEach(a=>{if(a.aggregat===old)a.aggregat=nv;});
      MODEL.states.forEach(s=>{if(s.aggregat===old)s.aggregat=nv;});
    }else if(kind==="record"){
      // Akteure dürfen bzw. hören den Typ unter seinem neuen Namen (IDarf<T>, IHoert<T> folgen dem Typ).
      MODEL.akteure.forEach(a=>{a.darf=(a.darf||[]).map(x=>x===old?nv:x);a.hoert=(a.hoert||[]).map(x=>x===old?nv:x);});
      // Funktionen: Eingänge, Ausgaben und was eine Quelle meldet.
      MODEL.bausteinFunktionen.forEach(f=>{(f.eingaenge||[]).forEach(e=>{if(e.eingang===old)e.eingang=nv;e.ausgaben=(e.ausgaben||[]).map(x=>x===old?nv:x);});
        f.lauscht=(f.lauscht||[]).map(x=>x===old?nv:x);});
      // … und die Kenntnis der Clients (IHoert folgt dem Typ).
      MODEL.clients.forEach(c=>{c.kenntnis=(c.kenntnis||[]).map(x=>x===old?nv:x);});
      if(obj.kind==="valueobject")retypeFelder(old,nv);   // Typ-Komposition: Feldtypen mitziehen
      if(obj.kind==="command"){
        MODEL.decider.forEach(d=>{if(d.command===old)d.command=nv;(d.ergibt||[]).forEach(o=>{if(o.event===old)o.event=nv;});});
      }else{
        MODEL.decider.forEach(d=>(d.ergibt||[]).forEach(o=>{if(o.event===old)o.event=nv;}));
        MODEL.applier.forEach(a=>{if(a.event===old)a.event=nv;});
        // Reader-Handles: Query (Eingang) und OneOf-Responses (Ausgänge) mitziehen.
        MODEL.reader.forEach(r=>(r.handles||[]).forEach(hd=>{if(hd.query===old)hd.query=nv;hd.responses=(hd.responses||[]).map(x=>x===old?nv:x);}));
        // Projektion-Handles: das gehörte Event mitziehen.
        MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;buendelUmbenennen(hd,old,nv);}));
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

  // 🖥 Host-Rahmen (docs/konzept-werkbank.md §8.1): die Umgebung als Karten rechts neben den Clients — kein Domänen-Modell.
  const HOST_KEY="§host";
  const istHostDom=d=>d===HOST_KEY;
  // Kanten eines Flusses: je Draht Knoten → Knoten, beschriftet mit dem Typ des Ausgangs (∧ = mehrere Drähte in einen Eingang,
  //   ∨ = eigener Eingang, ⏳ amber, ✕ rot gestrichelt). Dazu die ZUWEISUNG je Knoten: ƒ ⇢ Funktions-Karte, ▶ → Command im Aggregat,
  //   ⛲ ⇢ Nachricht; ◆/⧗ ← Event. Klick auf eine Kante öffnet ihr Ziel.
  // ⬡ Graph-Kanten: je [Von] eine Kante zwischen den Karten der Knoten (Funktion bzw. Graph im Graphen) — grün, verlierbar gestrichelt; beschriftet
  //   mit den Typen, die sie trägt (vom Server, aus dem Code). Klick öffnet den Graphen.
  function graphNodes(){
    const basis=[...MODEL.akteure.map(a=>({id:"akt:"+a._id,name:a.name,kind:"akteur",ref:a})),
            ...MODEL.clients.map(c=>({id:"cl:"+c._id,name:clientAnzeige(c),kind:"client",ref:c})),
            ...MODEL.records.map(r=>({id:"rec:"+r.name,name:r.name,kind:r.kind,ref:r})),
            ...MODEL.aggregate.map(a=>({id:"agg:"+a.name,name:a.name,kind:"aggregate",ref:a})),
            ...MODEL.states.map(s=>({id:"st:"+s._id,name:s.aggregat||"frei",kind:"state",ref:s})),
            ...MODEL.decider.map(d=>({id:"dec:"+d._id,name:d.command||"",kind:"decider",ref:d})),
            ...MODEL.applier.map(a=>({id:"app:"+a._id,name:a.event||"",kind:"applier",ref:a})),
            ...MODEL.enums.map(e=>({id:"enum:"+e.name,name:e.name,kind:"enum",ref:e})),
            ...(MODEL.graphen||[]).map(g=>({id:"gr:"+g._id,name:g.name,kind:"graph",ref:g})),
            ...(MODEL.bausteinFunktionen||[]).map(f=>({id:"fb:"+f._id,name:f.name,kind:"bausteinfunktion",ref:f})),
            ...MODEL.readModels.map(rm=>({id:"rm:"+rm._id,name:rm.name,kind:"readmodel",ref:rm})),
            ...MODEL.stores.map(s=>({id:"sto:"+s._id,name:s.name,kind:"store",ref:s})),
            ...MODEL.projektionen.map(p=>({id:"prj:"+p._id,name:p.name,kind:"projektion",ref:p})),
            ...MODEL.reader.map(r=>({id:"rdr:"+r._id,name:r.name,kind:"reader",ref:r})),
            ...MODEL.codeKarten.map(c=>({id:c._id,name:c.name,kind:"code",ref:c})),
            ...handleKnoten(),...fnKnoten(),...hostKnoten()];
    return [...basis,...rumpfKacheln(basis)];
  }
  const SHARED_KEY="§geteilt";
  const AKTEUR_KEY="§akteure";

  // ══ GRAPH-BASIS: die Kanten auf Karten-Ebene (Karten-Id → Karten-Id) kommen vom Server (board.sicht.kanten — Layout, Inseln, Kette,
  //   Nachbarn im Panel); die Rumpf-Kacheln hängen über ihren Besitzer (own) am Graphen, nicht über Kanten.
  const kanten=()=>SICHT().kanten;
  // Diagnose/Prüf-Skripte: Knoten + Kanten des Boards (dieselbe Wahrheit wie Layout und Insel-Erkennung).
  window.deGraph=()=>({knoten:graphNodes().map(n=>({id:n.id,kind:n.kind,name:n.name})),kanten:kanten()});
  // Inseln = Karten in winzigen Komponenten (≤ 2) OHNE Aggregat — die „einsamen" / unverdrahteten (vom Server).
  const inselMenge=()=>new Set(Object.entries(SICHT().karten).filter(([,k])=>k.insel).map(([id])=>id));
  // Block einer Karte (Aggregat, SHARED_KEY = Brücke, AKTEUR_KEY = Akteur-Band) — vom Server (board.sicht).
  //   Seit der Hierarchie: je Region EIN Block — der Rand mit den Akteur-Spalten, die Begriffe mit den geteilten Spalten, ein Block mit den
  //   Aggregat-Spalten (Schreibfluss, dann Leseseite).
  function groupKeyOf(n){if(n.kind==="rumpf"&&n.own)return groupKeyOf(n.own);   // Code-Kachel + LLM-Platz: im Block ihres Besitzers
    const rg=regionVon(n);if(rg===RAND_KEY)return AKTEUR_KEY;if(rg===BEGR_KEY)return SHARED_KEY;if(rg)return rg;
    const k=sk(n);if(k)return k.block;return n.kind==="akteur"?AKTEUR_KEY:SHARED_KEY;}
  // Der Formular-Körper eines Knotens — auf der Fläche (aufgeklappt) UND im Inspector derselbe.
  function fuelleKoerper(body,n){
    if(n.kind==="aggregate")aggStateCard(body,n.ref);
    else if(n.kind==="state")stateCard(body,n.ref);
    else if(n.kind==="decider")deciderCard(body,n.ref);
    else if(n.kind==="applier")applierCard(body,n.ref);
    else if(n.kind==="enum")body.append(enumCard(n.ref,MODEL.enums.indexOf(n.ref)));
    else if(n.kind==="readmodel")readModelCard(body,n.ref);
    else if(n.kind==="store")storeCard(body,n.ref);
    else if(n.kind==="projektion")projektionCard(body,n.ref);
    else if(n.kind==="reader")readerCard(body,n.ref);
    else if(n.kind==="akteur")akteurCard(body,n.ref);
    else if(n.kind==="bausteinfunktion")funktionsKarte(body,n.ref);
    else if(n.kind==="graph")graphKarte(body,n.ref);
    else if(n.kind==="client")clientCard(body,n.ref);
    else if(n.kind==="rumpf")rumpfKachelCard(body,n);
    else if(n.kind==="handle")handleCard(body,n);
    else if(n.kind==="fn")fnCard(body,n);
    else if(n.kind==="host")hostCard(body,n);
    else if(n.kind==="code")codeKarteCard(body,n.ref);
    else recordCard(body,n.ref);
  }
  function delNode(n){const k=n.kind,ref=n.ref;if(k==="host")return;gTitel("löschen "+(NODELABEL[k]||k)+" "+(n.name||""));
    // Ein gelöschter Eingang (Command/Query) verschwindet auch aus den Befugnissen (IDarf) der Akteure.
    {const nm=MODEL.records.includes(ref)?ref.name:null;if(nm)MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(nm))a.darf=ohneX(a.darf,nm);
      // … und aus den Verträgen: als Eingang fällt die ganze Zusage weg, als Ausgang nur der Ausgang.
});
      // … und aus der Kenntnis der Clients.
      MODEL.clients.forEach(c=>{if((c.kenntnis||[]).includes(nm))c.kenntnis=ohneX(c.kenntnis,nm);});}
    {const nm=MODEL.records.includes(ref)&&(ref.kind==="event"||ref.kind==="rejection")?ref.name:null;
      if(nm)MODEL.clients.forEach(c=>{if((c.kenntnis||[]).includes(nm))c.kenntnis=ohneX(c.kenntnis,nm);});}
    // … und aus den Funktionen: ein gelöschtes Ergebnis fällt aus dem OneOf, ein gelöschter Auftrag lässt den Eingang offen.
    if(MODEL.records.includes(ref)){MODEL.bausteinFunktionen.forEach(f=>{f.lauscht=ohneX(f.lauscht,ref.name);(f.eingaenge||[]).forEach(e=>{e.ausgaben=ohneX(e.ausgaben,ref.name);});
      f.eingaenge=(f.eingaenge||[]).filter(e=>e.eingang!==ref.name);});MODEL.akteure.forEach(a=>{a.hoert=ohneX(a.hoert,ref.name);});}
    if(VIEW.heim&&VIEW.heim[n.id]){delete VIEW.heim[n.id];speichereAnsicht();}
    if(VIEW.heimBlk&&VIEW.heimBlk[n.id]){delete VIEW.heimBlk[n.id];speichereAnsicht();}
    if(VIEW.heimAkteur&&VIEW.heimAkteur[n.id]){delete VIEW.heimAkteur[n.id];speichereAnsicht();}
    if(k==="aggregate")MODEL.aggregate.splice(MODEL.aggregate.indexOf(ref),1);
    else if(k==="state")MODEL.states.splice(MODEL.states.indexOf(ref),1);
    else if(k==="decider")MODEL.decider.splice(MODEL.decider.indexOf(ref),1);
    else if(k==="applier")MODEL.applier.splice(MODEL.applier.indexOf(ref),1);
    else if(k==="enum")MODEL.enums.splice(MODEL.enums.indexOf(ref),1);
    else if(k==="readmodel")MODEL.readModels.splice(MODEL.readModels.indexOf(ref),1);
    else if(k==="store")MODEL.stores.splice(MODEL.stores.indexOf(ref),1);
    else if(k==="projektion")MODEL.projektionen.splice(MODEL.projektionen.indexOf(ref),1);
    else if(k==="reader")MODEL.reader.splice(MODEL.reader.indexOf(ref),1);
    // Eine gelöschte (noch nicht geschriebene) Funktion fällt aus jedem Konstruktor und jedem IDarf — dieselbe Geste.
    else if(k==="graph"){MODEL.graphen.splice(MODEL.graphen.indexOf(ref),1);
      MODEL.graphen.forEach(g=>{g.knoten=(g.knoten||[]).filter(x=>String(x.typ||"").split("<")[0].trim()!==ref.name);});}
    else if(k==="bausteinfunktion"){MODEL.bausteinFunktionen.splice(MODEL.bausteinFunktionen.indexOf(ref),1);
      const ohneSie=l=>(l||[]).filter(p=>String(p.typ||"").replace(/\?$/,"").trim()!==ref.name);
      MODEL.bausteinFunktionen.forEach(q=>{q.konstruktor=ohneSie(q.konstruktor);(q.umsetzungen||[]).forEach(u=>{u.konstruktor=ohneSie(u.konstruktor);});});
      MODEL.akteure.forEach(a=>{a.darf=ohneX(a.darf,ref.name);});}
    else if(k==="akteur"){MODEL.akteure.splice(MODEL.akteure.indexOf(ref),1);
      MODEL.clients.forEach(c=>{if((c.verkoerpert||[]).includes(ref.name))c.verkoerpert=ohneX(c.verkoerpert,ref.name);});}
    else if(k==="client"){MODEL.clients.splice(MODEL.clients.indexOf(ref),1);AUF=null;}
    else if(k==="handle"){const hs=n.own.ref.handles||[];hs.splice(hs.indexOf(ref),1);}
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
    const boxes=[...GEO.entries()].map(([id,g])=>{const k=FLKARTE.get(id)||{};return {x:g.x,y:g.y,w:g.w,h:g.h,key:k.band||k.spalte||"§",kind:k.art};});
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
    if(O.type==="verkoerpert"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);
      if(c&&a&&!(c.verkoerpert=c.verkoerpert||[]).includes(a.name))c.verkoerpert.push(a.name);}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak&&I.rec&&!(ak.darf=ak.darf||[]).includes(I.rec))ak.darf.push(I.rec);}
    else if((O.type==="cmd"||O.type==="evtUse")&&I.akt&&I.hoert){const ak=MODEL.akteure.find(x=>x._id===I.akt);if(ak&&O.rec&&!(ak.hoert=ak.hoert||[]).includes(O.rec))ak.hoert.push(O.rec);}
    else if(O.type==="cmd"&&I.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===I.fb);
      if(f&&!(f.eingaenge=f.eingaenge||[]).some(e=>e.eingang===O.rec))f.eingaenge.push({eingang:O.rec,hinweis:false,herkunft:false,strom:false,faehigkeiten:[],ausgaben:[],ports:[]});}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d)d.command=O.rec;}
    else if(O.type==="evtOut"&&O.dec){const d=dec(O.dec);if(d&&!(d.ergibt||[]).some(x=>x.event===I.rec))(d.ergibt=d.ergibt||[]).push({event:I.rec});}
    else if(O.type==="evtUse"&&I.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===I.fb);if(f){f.eingaenge=f.eingaenge||[];
        const hin=recArt(O.rec)==="rejection",neu={eingang:O.rec,hinweis:hin,herkunft:!hin,strom:false,faehigkeiten:[],ausgaben:[]};
        if(I.ein==="open"){if(!f.eingaenge.some(e=>e.eingang===O.rec))f.eingaenge.push(neu);}
        else if(f.eingaenge[I.ein])Object.assign(f.eingaenge[I.ein],{eingang:O.rec,hinweis:hin,herkunft:!hin});}}
    else if(O.type==="sagaCmd"&&O.fb&&O.ein==="lauscht"){const f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb);
      if(f&&I.rec&&!(f.lauscht=f.lauscht||[]).includes(I.rec))f.lauscht.push(I.rec);}
    else if((O.type==="evtOut"||O.type==="sagaCmd")&&O.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb),e=f&&(f.eingaenge||[])[O.ein];
      if(e&&I.rec&&!(e.ausgaben=e.ausgaben||[]).includes(I.rec))e.ausgaben.push(I.rec);}
    // 🧩 Eine gerufene Funktion in den Konstruktor einer Funktions-Klasse bzw. einer Umsetzung (O.impl).
    else if(O.type==="gerufen"&&O.fb){const g=MODEL.bausteinFunktionen.find(x=>x._id===I.fb),f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb),l=f&&ctorVon(f,O.impl);
      if(istGerufen(g)&&l)nimmGerufene(l,g,f);}
    else if(O.type==="evtUse"){
      if(I.app){const p=app(I.app);if(p)p.event=O.rec;}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p){p.handles=p.handles||[];
        if(I.handleIdx==="open"){if(!p.handles.some(x=>x.event===O.rec))p.handles.push({event:O.rec,effekt:""});}
        else p.handles[I.handleIdx].event=O.rec;}}
    }
    else if(O.type==="decAgg"){const d=dec(O.dec);if(d)d.aggregat=I.agg;}
    else if(O.type==="appAgg"){const p=app(O.app);if(p)p.aggregat=I.agg;}
    // ── Leseseite: ReadModel→Store, Projektion-Handle→Write-Fn, Reader-Handle→Read-Fn, Reader→Projektion,
    //    Event→Projektion, Query→Reader, Reader→Response, CODE (📝/🤖) → Rumpf-Port (Controller / Impl). ──
    else if(O.type==="readmodel"){const rm=MODEL.readModels.find(x=>x._id===O.rm);if(rm)rm.store=I.store;}
    // Projektion-Handle ruft eine Write-Fn (Store-Scope fällt daraus ab) — anhängen, dedup.
    else if(O.type==="wcall"){const p=MODEL.projektionen.find(x=>x._id===O.proj);const hd=p&&p.handles[O.handleIdx];if(hd){hd.fns=hd.fns||[];if(!hd.fns.includes(I.fn))hd.fns.push(I.fn);}}
    // Reader-Handle ruft eine Read-Fn (auch über mehrere Stores) — anhängen, dedup.
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
    if(O.type==="verkoerpert"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);if(c&&a)c.verkoerpert=ohne(c.verkoerpert,a.name);}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak)ak.darf=ohne(ak.darf,I.rec);}
    else if((O.type==="cmd"||O.type==="evtUse")&&I.akt&&I.hoert){const ak=MODEL.akteure.find(x=>x._id===I.akt);if(ak)ak.hoert=ohne(ak.hoert,O.rec);}
    else if(O.type==="cmd"&&I.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===I.fb);if(f)f.eingaenge=(f.eingaenge||[]).filter(e=>e.eingang!==O.rec);}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d&&d.command===O.rec)d.command="";}
    else if(O.type==="evtOut"&&O.dec){const d=dec(O.dec);if(d)d.ergibt=(d.ergibt||[]).filter(x=>x.event!==I.rec);}
    else if(O.type==="evtUse"&&I.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===I.fb);if(f)f.eingaenge=(f.eingaenge||[]).filter(e=>e.eingang!==O.rec);}
    else if(O.type==="sagaCmd"&&O.fb&&O.ein==="lauscht"){const f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb);if(f)f.lauscht=ohne(f.lauscht,I.rec);}
    else if((O.type==="evtOut"||O.type==="sagaCmd")&&O.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb),e=f&&(f.eingaenge||[])[O.ein];if(e)e.ausgaben=ohne(e.ausgaben,I.rec);}
    else if(O.type==="gerufen"&&O.fb){const g=MODEL.bausteinFunktionen.find(x=>x._id===I.fb),f=MODEL.bausteinFunktionen.find(x=>x._id===O.fb),l=f&&ctorVon(f,O.impl);
      if(g&&l)l.splice(0,l.length,...l.filter(p=>gerufenByName(p.typ)!==g));}
    else if(O.type==="evtUse"){
      if(I.app){const p=app(I.app);if(p)p.event="";}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p)p.handles=(p.handles||[]).filter(h=>h.event!==O.rec);}}
    else if(O.type==="decAgg"){const d=dec(O.dec);if(d)d.aggregat="";}
    else if(O.type==="appAgg"){const p=app(O.app);if(p)p.aggregat="";}
    else if(O.type==="readmodel"){const rm=MODEL.readModels.find(x=>x._id===O.rm);if(rm)rm.store="";}
    else if(O.type==="wcall"){const hd=handle(MODEL.projektionen,O.proj,O.handleIdx);if(hd)hd.fns=ohne(hd.fns,I.fn);}
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
  // Nachricht (Name) → Sorte: aus der Record-Art (Grammatik, recordSorte).
  function grNachrichtSorte(name){const g=GR(),r=recByName(name);if(r)return (g.recordSorte||{})[r.kind]||null;
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
    if(k==="decider"||k==="applier"||k==="state"){const a=MODEL.aggregate.find(x=>x.name===r.aggregat);return a?a.namespace:null;}
    if(k==="rumpf")return nsVon(n.own);
    return r.namespace||null;}
  // Stammt der Knoten aus dem Code (sonst: Entwurf, immer sichtbar)? Abgeleitete Knoten folgen ihrem Besitzer.
  function ausCodeVon(n){const k=n.kind,r=n.ref;
    if(k==="handle"||k==="fn")return ausCodeVon(n.own);
    if(k==="state"){const a=MODEL.aggregate.find(x=>x.name===r.aggregat);return !!(a&&a.ausCode);}
    if(k==="rumpf")return ausCodeVon(n.own);
    return r.ausCode===true;}
  // Geladen, wenn seine DOMÄNE (Graph-Zuordnung, domKey — z. B. ImagePairProjektion → ImagePair) ODER sein eigener Namespace
  //   (z. B. „Projections“ ausdrücklich gewählt) in der Auswahl liegt. Die Domänen-Gehörigkeit steht über dem Namespace.
  function geladen(n){if(n.kind==="client")return LADEN===null||clientPorts(n.ref).some(p=>{const m=p.node&&NODEBY.get(p.node);return m&&m.kind!=="client"&&geladen(m);});
    if(LADEN===null||!ausCodeVon(n))return true;const x=nsVon(n),d=domNs(n);
    return LADEN.some(g=>(x&&drinNs(g,x))||(d!==OHNE_DOM&&drinNs(g,d)));}
  window.deLaden=function(){START_OFFEN=true;render();};
  // Start-Dialog: leer starten, Domänen wählen (Häkchen am Eltern-Namespace lädt alles darunter) oder alles laden.
  function zeigeStart(){if(!canvas)return;const alt=canvas.querySelector(".gstart");if(alt)alt.remove();if(!START_OFFEN)return;
    const nsAlle=new Set();["records","enums","aggregate","graphen","bausteinFunktionen","readModels","stores","projektionen","reader"]
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
  function basisZaehl(alle,zahl){graphNodes().forEach(n=>{if(["handle","fn","rumpf","state","decider","applier"].includes(n.kind)||!ausCodeVon(n))return;
      const x=nsVon(n),d=domNs(n);alle.forEach(m=>{if((x&&drinNs(m,x))||(d!==OHNE_DOM&&drinNs(m,d)))zahl.set(m,(zahl.get(m)||0)+1);});});}

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
  const VB_NEU={event:"NeuesEvent",rejection:"NeueAblehnung",command:"NeuerCommand",query:"NeueQuery",queryresponse:"NeueAntwort",valueobject:"NeuerWert"};
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
      let basis=VB_NEU[k]||"Neu";if(S.fb){const f=MODEL.bausteinFunktionen.find(x=>x._id===S.fb);const st=((f&&f.name)||"").replace(/^I(?=[A-ZÄÖÜ])/,"");if(st)basis=st+"Ergebnis";}
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
    const f=document.createDocumentFragment();flKanten(f);svg.append(f);
    wendeFokusAn();fokusChip();
    if(typeof SP!=="undefined")spurZeichne();
  }
  // Interne Linien hervorheben, wenn man über die zugehörige Decider-/Applier-Zeile fährt.
  function hilite(decId,appId,on){document.querySelectorAll("#de .glink.internal").forEach(p=>{
    if((decId&&p.dataset.dec===decId)||(appId&&p.dataset.app===appId))p.classList.toggle("hot",on);});}

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
  // Position eines Knotens in der AKTUELLEN Ansicht. Voll = x/y im Modell (wandert ins Board); Kompakt = eigenes,
  //   nur browser-lokales Layout (KPOS) — die kleineren Karten brauchen eine dichtere Anordnung.
  //   Je Detail-Stufe ein eigenes Layout (k = Details eingeklappt, d = Details als Knoten) — Umschalten verliert nichts.
  function P(n){if(!VIEW.kompakt)return n.ref;
    const L=KPOS[VIEW.details?"d":"k"]||(KPOS[VIEW.details?"d":"k"]={});
    let p=L[n.id];if(!p){p=L[n.id]={};if(n.ref._kpos){p.x=n.ref._kpos.x;p.y=n.ref._kpos.y;}}
    if(n.ref._kpos)delete n.ref._kpos;return p;}
  let INSP=false, INSP_SCROLL=null;   // INSP: gerade wird eine Inspector-Kopie gebaut (keine Slot-Registrierung)

  let VIS=new Set(), VERTRETER=new Map(), DETAILS=new Map(), EINGEKLAPPT=new Set(), NODEBY=new Map(), KONTRAKT=[];
  let ADJ={out:new Map(),inn:new Map()};
  const vertreterId=id=>(VERTRETER.get(id)||[id])[0];
  function berechneSicht(){
    const alle=graphNodes();NODEBY=new Map(alle.map(n=>[n.id,n]));
    const push=(m,k,v)=>{let a=m.get(k);if(!a)m.set(k,a=[]);if(!a.includes(v))a.push(v);};
    ADJ={out:new Map(),inn:new Map()};
    kanten().forEach(([a,b])=>{if(a===b||!NODEBY.has(a)||!NODEBY.has(b))return;push(ADJ.out,a,b);push(ADJ.inn,b,a);});
    // Vertreter (kompakt) = sichtbarer Besitzer, transitiv durch eingeklappte Besitzer (Decider → Command) — vom Server; ein Code-Block hat
    //   den Vertreter seiner Karte. Ohne Besitzer bleibt ein Detail selbst sichtbar (neu angelegt / unverdrahtet → nichts verschwindet).
    const vert=n=>{if(VIEW.details)return [n.id];if(n.kind==="rumpf")return vert(n.own);
      const k=SICHT().karten[n.id];return (k&&k.vertreter)||[n.id];};
    VIS=new Set();VERTRETER=new Map();DETAILS=new Map();EINGEKLAPPT=new Set();
    alle.forEach(n=>{const v=vert(n);VERTRETER.set(n.id,v);
      if(v.length===1&&v[0]===n.id){if(!HIDDEN.has(groupKeyOf(n))&&!istAus(n.kind)&&geladen(n))VIS.add(n.id);}
      else{EINGEKLAPPT.add(n.id);v.forEach(o=>push(DETAILS,o,n.id));}});
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
  const RUMPF_ARTEN=new Set(["handle","decider","applier","fn","bausteinfunktion","client"]);
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
      h("span",{class:"gf-z"},String((flFokus()||{karten:new Set()}).karten.size)),
      h("button",{title:"Fokus einpassen",onclick:()=>passeEin([...(flFokus()||{karten:new Set()}).karten],1)},"⤢"),
      h("button",{title:"Fokus lösen (Esc)",onclick:fokusLoesen},"✕"));}
  function waehle(id){
    // Im offenen Modal führt eine Auswahl (ein Verweis in einem Formular) zur Ansicht dieser Karte, nicht auf die verdeckte Fläche.
    if(KOM&&id&&!VB&&(NODEBY.has(id)||komDaten(id))){komKarte(id,true);return;}
    if(VB){if(id&&id!==SEL){vbKlick(id);return;}if(!id){vbEnde();return;}}
    if(AUF&&id!=="cl:"+AUF.client){AUF=null;if(world)drawEdges();}
    if(typeof SP!=="undefined"&&id)SP.schritt=null;
    SEL=id||null;if(FOKUS&&FOKUS.art==="menge")FOKUS=null;
    fokusNeu();wendeFokusAn();fokusChip();zeigeInspector();}
  function wendeFokusAn(){if(!world)return;const F=flFokus(),an=!!F;world.classList.toggle("fokus",an);
    world.querySelectorAll(".gnode2").forEach(el=>{el.classList.toggle("inslice",an&&F.karten.has(el.dataset.id));el.classList.toggle("sel",!!SEL&&el.dataset.karte===SEL);});
    world.querySelectorAll(".glink").forEach(p=>p.classList.toggle("inslice",an&&F.kanten.has(+p.dataset.i)));}
  document.addEventListener("keydown",e=>{if(e.key!=="Escape")return;if(VB){vbEnde();return;}if(SEL){waehle(null);return;}
    if(typeof SP!=="undefined"&&SP.schritt){SP.schritt=null;zeigeInspector();return;}if(FOKUS)fokusLoesen();});

  // Kein Panel neben der Fläche: ein Klick hebt die Kette hervor, bearbeitet wird im Modal (Doppelklick). Ruft eine Formular-Änderung nach
  //   einer Auffrischung, folgt ihr das offene Modal; ohne Modal bleibt nur das Panel eines Spur-Schritts.
  function zeigeInspector(){if(!canvas)return;const alt=canvas.querySelector(".ginsp");if(alt)alt.remove();
    if(KOM){komZeige();return;}
    if(!SEL&&typeof SP!=="undefined"&&SP.schritt)spSchrittPanel();}
  // Kurzfassung auf der eingeklappten Karte: das Wesentliche in einer Zeile + Chips der eingeklappten Details.
  const kurz=xs=>{xs=[...new Set((xs||[]).filter(Boolean))];return xs.slice(0,2).join(", ")+(xs.length>2?" +"+(xs.length-2):"");};
  // ── (4) SEMANTISCHER ZOOM ──
  function zoomAuf(s){if(!canvas)return;const cr=canvas.getBoundingClientRect(),wx=(cr.width/2-PAN.x)/PAN.s,wy=(cr.height/2-PAN.y)/PAN.s;
    PAN.s=s;PAN.x=cr.width/2-wx*s;PAN.y=cr.height/2-wy*s;applyPan();}
  // Knoten-Menge einpassen (minS: mindestens diese Zoomstufe).
  function passeEin(ids,maxS,minS){if(!canvas||!world||!ids||!ids.length)return;let x1=1e9,y1=1e9,x2=-1e9,y2=-1e9;
    // Karten der Fläche (ihre Id) oder Karten der Sicht (die Flächen-Karten, die sie zeigen).
    ids.forEach(id=>{const els=world.querySelectorAll('.gnode2[data-id="'+CSS.escape(id)+'"],.gnode2[data-karte="'+CSS.escape(id)+'"]');
      els.forEach(el=>{x1=Math.min(x1,el.offsetLeft);y1=Math.min(y1,el.offsetTop);x2=Math.max(x2,el.offsetLeft+el.offsetWidth);y2=Math.max(y2,el.offsetTop+el.offsetHeight);});});
    if(x1>x2)return;const cr=canvas.getBoundingClientRect(),pad=60;
    const s=Math.max(minS||0.08,Math.min(maxS||1,(cr.width-2*pad)/Math.max(1,x2-x1),(cr.height-2*pad)/Math.max(1,y2-y1)));
    PAN.s=s;PAN.x=cr.width/2-(x1+x2)/2*s;PAN.y=cr.height/2-(y1+y2)/2*s;applyPan();}
  // ══ DOMÄNEN-RAHMEN: rein visuelle Blöcke UM das bestehende Layout (Rollen-Spalten, Code/🤖 unter dem Besitzer bleiben).
  //   Domäne = Namespace, hierarchisch (Unterdomänen liegen im Rahmen ihrer Eltern-Domäne; packLayout sortiert danach).
  //   Der Rahmen folgt den ECHTEN Kartenpositionen (GEO) — zieht man Karten, wächst er mit. Kopf = kleine Menüleiste.
  //   Leere neue Domänen (VIEW.domNeu) und die Heimat namespace-loser Knoten (VIEW.heim) leben nur in der Ansicht.
  const RP=36, RK=46, SPK=34, SPAD=8;   // Domänen-Rand/-Kopf, Spalten-Kopf/-Rand (Welt-px) — packLayout reserviert RP/RK je Region, SPK je Spalte
  const OHNE_DOM="§ohne";
  // Domäne einer Karte (Rahmen) — vom Server (board.sicht): das Aggregat, zu dem sie laut Graph gehört (Projektion über ihre Events,
  //   Reader/Store/ReadModel über ihre Projektion, Ablauf-Einheiten dort, wo sie wirken, Datentypen bei ihren Nachbarn), sonst ihr Namespace;
  //   Clients und Pipelines haben eigene Rahmen, Zusagen stehen beim Akteur, ein Akteur in der ersten Domäne, in der er wirkt. Darüber nur die
  //   Ansicht: die Heimat namespace-loser Karten (VIEW.heim). Eine Karte, die es im Code noch nicht gibt, steht bis zum Board in ihrem Namespace.
  let FRGEO=new Map();   // FRGEO: Rahmen-Geometrie je Schlüssel (Domäne|Akteur, §vertrag:Akteur) — Ziel der Client-Leitungen
  // Die REGION einer Karte (Layout und Rahmen): ihr oberster Besitzer unter der Anwendung (board.sicht.besitz). Was (noch) keinen Besitz hat
  //   (Host, Pipeline-Knoten, neue Karten), fällt auf die Namespace-Domäne zurück.
  //   Eine in einem Block angelegte, noch unverdrahtete Karte (Begriffe ohne Nutzer bzw. noch nicht im Code) bleibt in der Region, in der sie
  //   angelegt wurde (VIEW.heimReg) — bis eine Signatur sie einem Besitzer gibt.
  function domKey(n){if(n.kind==="host")return HOST_KEY;
    if(n.kind==="rumpf")return domKey(n.own);
    const rg=regionVon(n),heim=(VIEW.heimReg||{})[n.id];
    if(heim&&(!rg||(rg===BEGR_KEY&&!mehrdeutig(n))))return heim;
    return rg||domNs(n);}
  // Die Namespace-Domäne einer Karte (Laden und Zählen im Start-Dialog): Graph-Zuordnung vom Server, sonst der eigene Namespace.
  function domNs(n){if(n.kind==="host")return HOST_KEY;
    // Der Rumpf einer Client-Implementierung steht in der Domäne des Client-Namespaces (der Client-Rahmen ist nur die Anschlussleiste).
    if(n.kind==="rumpf"&&n.own.kind==="client")return nsVon(n.own)||OHNE_DOM;
    const k=sk(n);if(k&&k.domaene)return k.domaene;
    if(!k){if(n.kind==="client")return CL_PRE+n.ref.name;
      if(n.kind==="rumpf")return domNs(n.own);}
    return (!k&&nsVon(n))||(VIEW.heim||{})[n.id]||OHNE_DOM;}
  const domLabel=ns=>ns===RAND_KEY?"🚪 Rand":ns===BEGR_KEY?"📚 Begriffe":istRegionKarte(ns)?(({agg:"▣ ",gr:"⬡ ",fb:"ƒ ",sto:"👁 "})[ns.split(":")[0]])+((NODEBY.get(ns)||{}).name||ns.replace(/^\w+:(\w+:)?/,""))
    :ns===OHNE_DOM?"⋯ ohne Domäne":istHostDom(ns)?"🖥 Host · "+(WB.zustand&&WB.zustand.aktiv||"Umgebung"):istClientDom(ns)?"🔌 "+clientAnzeige({name:ns.slice(CL_PRE.length)}):letztesSeg(ns);
  const bausteine=z=>z+(z===1?" Baustein":" Bausteine");
  // Rahmen aus GEO zeichnen (nach jeder Geometrie-Messung). Liefert die Ausdehnung für die Weltgröße.
  function zeichneRahmen(){if(!world)return {x2:0,y2:0};
    let ebene=world.querySelector(".grahmen-ebene");if(!ebene){ebene=h("div",{class:"grahmen-ebene"});world.insertBefore(ebene,world.firstChild);}
    ebene.textContent="";FRGEO=new Map();return flRahmen(ebene);}
  // Wie zeigeKnoten — liegt der Knoten in einer nicht geladenen Domäne, wird sie erst dazugeladen.
  function zeigeLadend(id){const n=NODEBY.get(id),ns=n&&nsVon(n);
    if(n&&LADEN&&!geladen(n)&&ns){LADEN=LADEN.concat([ns]);VIEW.geladen=LADEN;speichereAnsicht();render();setTimeout(()=>zeigeKnoten(id),0);return;}
    zeigeKnoten(id);}
  function zeigeKnoten(id){if(KOM){komKarte(id,true);return;}passeEin([id],1,0.4);waehle(id);}
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
  // Ansichts-Leiste über der Fläche.
  function ansichtLeiste(){
    const btn=(lbl,on,title,fn)=>h("button",{class:on?"on":"",title,onclick:fn},lbl);
    return h("div",{class:"gview"},
      h("b",{style:"color:#cbd3e1"},"Ansicht"),
      h("button",{title:"Geladene Domänen ändern",onclick:()=>deLaden()},"📂 "+(LADEN===null?"alles geladen":!LADEN.length?"leer":LADEN.map(letztesSeg).join(", "))),
      btn("⤢ Alles zeigen",false,"Ganze Fläche einpassen",()=>passeEin(flKarten().map(k=>k.id),1)),
      btn("🖥 Umgebung ▾",false,"Umgebung und Instanzen öffnen",e=>menue(e.clientX,e.clientY+20,"Umgebung "+(WB.umgName||""),
        hostKnoten().map(n=>({t:n.name,fn:()=>komKarte(n.id)})))),
      h("span",{id:"gfokus",class:"gfokus",style:"display:none"}),
      btn("👁 Ausblenden"+((VIEW.aus||[]).length?" ("+VIEW.aus.length+")":"")+" ▾",FILTER_OPEN,"Knotenarten und Domänen ein-/ausblenden",()=>{FILTER_OPEN=!FILTER_OPEN;render();}),
      h("span",{style:"margin-left:8px"},"Klick = Kette hervorheben · Doppelklick = öffnen und bearbeiten · vom Port ziehen = verbinden · Esc = fertig"));}

  // ══ FLÄCHE IN SECHS SPALTEN (docs/10 §10.9): was Akteure hineingeben · Quellen · Graphen · Aggregate · Projektionen · was zu
  //   Akteuren kommt. Karten, Ports, Bänder und Kanten rechnet der Server (board.sicht.flaeche); hier nur Anordnung und Zeichnen. Eine Karte der
  //   Fläche öffnet (Doppelklick) die Karte der Sicht dahinter im Modal, ein Port den Typ seiner Nachricht; ein Klick hebt die Kette hervor.
  const FL_SPALTE={ausgang:{t:"Client-Ausgang",u:"was Akteure hineingeben (darf)",neu:["akteur","client"]},quellen:{t:"Quellen",u:"lauschen am Kopf eines Graphen",neu:["quelle"]},
    graphen:{t:"Graphen",u:"rechnen vor dem Aggregat",neu:["graph"]},aggregate:{t:"Aggregate",u:"entscheiden, schreiben das Log",neu:["aggregate"]},
    projektionen:{t:"Projektionen",u:"Sichten je Store",neu:["store"]},eingang:{t:"Client-Eingang",u:"was zu Akteuren kommt (hört)",neu:["akteur"]}};
  const FL_SYM={akteur:"👤",quelle:"⚡",graph:"⬡",aggregate:"▣",projektion:"⇶",reader:"⌕",store:"🗄"};
  const FL_SORTE={command:"",event:"",hinweis:"✉ ",query:"? ",antwort:"↩ ",frist:"⏱ "};
  // Kantenarten: Farbe, Strich, leise (tritt zurück, bis eine ihrer Karten gewählt ist).
  const FL_KANTE={schreiben:{c:"#9aa6bb"},quelle:{c:"#9aa6bb"},lesen:{c:"#7aa2e0",d:"2 4",leise:1},kenntnis:{c:"#8a8f9c",d:"6 4",leise:1},
    arbeit:{c:"#4fb06a"},ruecklauf:{c:"#e08a3c",d:"8 5"},liest:{c:"#a07ad6",d:"1 4",leise:1}};
  const FL_LEER={spalten:["ausgang","quellen","graphen","aggregate","projektionen","eingang"],baender:[],gruppen:[],karten:[],kanten:[]};
  const FL=()=>SICHT().flaeche||FL_LEER;
  const FL_KOPF=96, FL_BAND_PAD=30, FL_GAP_Y=16, FL_GR_KOPF=22, FL_GR_PAD=10;
  let FLGEO={spalten:{},baender:[],gruppen:new Map()}, FLKARTE=new Map();
  // Die Panel-Karte einer Flächen-Karte: die Karte der Sicht dahinter; eine Katalog-Quelle öffnet ihren Graphen.
  function flPanelId(fk){if(fk.karte)return fk.karte;const k=FL().kanten.find(x=>x.von===fk.id&&x.art==="quelle");return k?k.nach:null;}
  // Der Graph einer Quelle bzw. ein Graph selbst (Name) — für seine Instanzen.
  function flGraphName(fk){if(fk.art==="graph")return fk.name;if(fk.art!=="quelle")return null;
    const k=FL().kanten.find(x=>x.von===fk.id&&x.art==="quelle"),g=k&&FLKARTE.get(k.nach);return g?g.name:null;}
  // Was gezeigt wird: die geladenen Domänen (Start-Dialog) — Akteure immer, sonst nach der Panel-Karte.
  function flSichtbar(fk){if(fk.art==="akteur"||LADEN===null)return true;const n=NODEBY.get(flPanelId(fk)||"");return !n||geladen(n);}
  function flKarten(){return FL().karten.filter(flSichtbar);}
  // ── Fläche: anschließen über „＋“ am Port (nie Pfeile von Hand). Der „＋“ hebt die Ports hervor, die passen; ein Klick verbindet, Esc bricht ab.
  //   Akteur links „＋ darf“ → Eingänge (Command, Query) von Graphen, Aggregaten, Readern; Akteur rechts „＋ hört“ → Ausgänge (Event, Hinweis,
  //   Command ohne Ziel); Projektion „＋ Event“ → Events der Aggregate. ✕ an einem Port löst (darf, hört, Auf der Projektion). ──
  let FLPORT=null;
  function flPassend(fk,p){if(!FLPORT)return false;const a=FLPORT;
    if(a.art==="darf")return p.richtung==="ein"&&(p.sorte==="command"||p.sorte==="query")&&["graphen","aggregate","projektionen"].includes(fk.spalte)&&!(a.akteur.darf||[]).includes(p.typ)
      &&MODEL.records.some(r=>r.name===komBasis(p.typ));
    if(a.art==="hoert")return p.richtung==="aus"&&["graphen","aggregate"].includes(fk.spalte)&&!(a.akteur.hoert||[]).includes(p.typ)
      &&(p.sorte==="event"||p.sorte==="hinweis"||(p.sorte==="command"&&!(MODEL.records.find(r=>r.name===komBasis(p.typ))||{}).aggregat));
    if(a.art==="event")return p.richtung==="aus"&&fk.art==="aggregate"&&p.sorte==="event"&&!(a.projektion.handles||[]).some(x=>x.event===p.typ);
    return false;}
  function flMarkiere(){if(!world)return;let n=0;world.classList.toggle("auswahl",!!FLPORT);
    world.querySelectorAll(".gnode2.fk").forEach(el=>{const fk=FLKARTE.get(el.dataset.id);if(!fk)return;let hat=false;
      el.querySelectorAll(".fk-port[data-port]").forEach(pe=>{const i=pe.dataset.port.indexOf("|"),p=fk.ports.find(q=>q.richtung===pe.dataset.port.slice(0,i)&&q.typ===pe.dataset.port.slice(i+1));
        const ja=!!p&&flPassend(fk,p);pe.classList.toggle("kand",ja);if(ja){hat=true;n++;}});
      el.classList.toggle("kand-karte",hat);el.classList.toggle("leise",!!FLPORT&&!hat&&el.dataset.id!==(FLPORT&&FLPORT.karte));});
    return n;}
  function flPlus(art,fk,ev){const a=art==="event"?{art,projektion:projektionVonKarte(fk),karte:fk.id}:{art,akteur:akteurVonKarte(fk),karte:fk.id};
    if(!a.akteur&&!a.projektion)return;FLPORT=a;const n=flMarkiere();
    deFlash(n?"ℹ "+n+" passende Ports hervorgehoben — anklicken verbindet · Esc bricht ab":"ℹ Kein passender Port auf der Fläche",true);if(!n)FLPORT=null,flMarkiere();}
  function flAnschliessen(fk,p){const a=FLPORT;FLPORT=null;if(!a)return;
    if(a.art==="darf"){a.akteur.darf=[...(a.akteur.darf||[]),p.typ];gTitel("👤 "+a.akteur.name+" darf "+p.typ);}
    else if(a.art==="hoert"){a.akteur.hoert=[...(a.akteur.hoert||[]),p.typ];gTitel("👤 "+a.akteur.name+" hört "+p.typ);}
    else if(a.art==="event"){a.projektion.handles=[...(a.projektion.handles||[]),{event:p.typ,effekt:""}];gTitel(a.projektion.name+": Auf("+p.typ+")");}
    render();}
  function flLoesen(fk,p){if(fk.art==="akteur"&&fk.spalte==="ausgang"){const a=akteurVonKarte(fk);if(a&&(a.darf||[]).includes(p.typ)){a.darf=ohneX(a.darf,p.typ);gTitel("👤 "+a.name+" darf "+p.typ+" nicht mehr");render();}}
    else if(fk.art==="akteur"){const a=akteurVonKarte(fk);if(a&&(a.hoert||[]).includes(p.typ)){a.hoert=ohneX(a.hoert,p.typ);gTitel("👤 "+a.name+" hört "+p.typ+" nicht mehr");render();}}
    else if(fk.art==="projektion"){const pr=projektionVonKarte(fk);if(pr){pr.handles=(pr.handles||[]).filter(x=>x.event!==p.typ);gTitel(pr.name+": Auf("+p.typ+") entfernt");render();}}}
  const flLoesbar=(fk,p)=>(fk.art==="akteur"&&fk.spalte==="ausgang"&&p.richtung==="aus")||(fk.art==="akteur"&&fk.spalte==="eingang"&&p.richtung==="ein"&&p.sorte!=="antwort")
    ||(fk.art==="projektion"&&p.richtung==="ein");
  document.addEventListener("keydown",e=>{if(e.key==="Escape"&&FLPORT&&!KOM){e.stopImmediatePropagation();FLPORT=null;flMarkiere();}},true);
  function flKarteEl(fk){
    const pid=flPanelId(fk),n=pid?NODEBY.get(pid):null;
    const dg=n?diagFuer(n.id):[],nurWarn=dg.length>0&&dg.every(x=>x.art==="warnung");
    const el=h("div",{class:"gnode2 fk fk-"+fk.art+" n-"+(fk.art==="quelle"?"funktion":fk.art)+(dg.length?(nurWarn?" diagwarn":" diag"):"")});
    el.dataset.id=fk.id;el.dataset.karte=pid||"";
    const loeschbar=n&&fk.art!=="quelle"&&fk.spalte!=="eingang";
    const head=h("div",{class:"ghead"},
      h("span",{class:"gtitle hatname",title:(NODELABEL[n?n.kind:fk.art]||fk.art)+" · "+fk.name+(pid?" — Klick: Kette · Doppelklick: öffnen":"")},
        h("span",{class:"gk"},(FL_SYM[fk.art]||"")+" "),h("span",{class:"gn"},fk.name)),
      dg.length?h("span",{class:"gdiag"+(nurWarn?" warn":""),title:dg.map(d=>d.text).join("\n")},(nurWarn?"⚠ ":"⛔ ")+dg.length):null,
      fk.art==="graph"||fk.art==="aggregate"||fk.gruppe?h("span",{class:"gx fk-kom",title:fk.art==="graph"?"Komposition öffnen (Doppelklick)":"Von innen öffnen (Doppelklick)",
        onclick:e=>{e.stopPropagation();flInnen(fk);}},"⧉"):null,
      loeschbar?h("span",{class:"gx",title:"Löschen",onclick:e=>{e.stopPropagation();delNode(n);}},"✕"):null);
    el.onclick=e=>{if(e.target.classList.contains("gx")||e.target.closest(".fk-port,.fk-chip"))return;e.stopPropagation();if(FLPORT){FLPORT=null;flMarkiere();return;}if(pid)waehle(pid);};
    // Doppelklick: ein Graph öffnet seine Komposition, eine Quelle die ihres Graphen mit ihrem Knoten gewählt.
    el.ondblclick=e=>{e.stopPropagation();if(fk.art!=="quelle"){flInnen(fk);return;}
      else if(fk.art==="quelle"&&fk.id.startsWith("q:graph:")){const r=fk.id.slice(2),i=r.lastIndexOf(":");komOeffne("gr:"+r.slice(0,i),r.slice(i+1));}};
    el.append(head);
    // Instanzen (graphen/<instanz>.json) an Graph und Quelle — Klick öffnet die Instanz im Panel.
    const gname=flGraphName(fk);
    if(gname){const inst=Object.keys(WB.instanzen||{}).filter(i=>((WB.instanzen[i]||{}).Graph||"")===gname).sort();
      if(inst.length)el.append(h("div",{class:"fk-chips"},...inst.map(i=>h("button",{class:"fk-chip",title:"Instanz "+i+" — Klick: Panel",
        onclick:e=>{e.stopPropagation();komKarte("host:inst:"+i);}},"⬡ "+i))));}
    const ports=h("div",{class:"fk-ports"});
    const zeile=p=>{const rid="rec:"+String(p.typ).split("<")[0],hat=NODEBY.has(rid);
      const r=h("div",{class:"fk-port "+p.richtung+" s-"+p.sorte+(p.eigen?" eigen":""),
        title:(p.eigen?"deklariert hier · ":"")+p.sorte+" "+p.typ+(hat?" — Klick: seine Kanten · Doppelklick: öffnen":""),
        onclick:e=>{e.stopPropagation();if(FLPORT){if(r.classList.contains("kand"))flAnschliessen(fk,p);else{FLPORT=null;flMarkiere();}return;}if(hat)waehle(rid);},
        ondblclick:e=>{e.stopPropagation();if(hat)komKarte(rid);}},
        h("span",{class:"fk-dot"}),h("span",{class:"fk-pl"},(FL_SORTE[p.sorte]||"")+p.typ),
        flLoesbar(fk,p)?h("button",{class:"ag-los",title:"Verbindung lösen",onclick:e=>{e.stopPropagation();flLoesen(fk,p);}},"✕"):null);
      r.dataset.port=p.richtung+"|"+p.typ;return r;};
    // „＋“ an der Stelle des Ports: was hier angeschlossen werden kann, leuchtet auf der Fläche auf.
    const plus=(ri,label,art)=>{const r=h("div",{class:"fk-port "+ri+" plus",title:"Klick: passende Ports hervorheben, dann einen anklicken",
      onclick:e=>{e.stopPropagation();flPlus(art,fk,e);}},h("span",{class:"fk-dot"}),h("span",{class:"fk-pl"},label));return r;};
    fk.ports.filter(p=>p.richtung==="ein").forEach(p=>ports.append(zeile(p)));
    if(fk.art==="akteur"&&fk.spalte==="eingang")ports.append(plus("ein","＋ hört","hoert"));
    if(fk.art==="projektion")ports.append(plus("ein","＋ Event","event"));
    fk.ports.filter(p=>p.richtung==="aus").forEach(p=>ports.append(zeile(p)));
    if(fk.art==="akteur"&&fk.spalte==="ausgang")ports.append(plus("aus","＋ darf","darf"));
    if(ports.childNodes.length)el.append(ports);
    return el;
  }
  // Anordnung: Spalten nebeneinander (Breite = breiteste Karte), Domänen als Bänder über Quellen bis Projektionen, die Akteure links und rechts
  //   über die ganze Höhe, Projektionen und Reader je Store gruppiert. In jeder Zelle rücken Karten zur mittleren Höhe ihrer Nachbarn (zwei Durchgänge).
  function packSpalten(){
    if(!world)return;
    const ks=flKarten(),els=new Map([...world.querySelectorAll(".gnode2.fk")].map(e=>[e.dataset.id,e]));
    const sz=id=>{const e=els.get(id);return {w:e?e.offsetWidth:240,h:e?e.offsetHeight:80};};
    const SP=FL().spalten,GAP_X=150;
    FLGEO={spalten:{},baender:[],gruppen:new Map()};
    let x=0;SP.forEach(s=>{const w=Math.max(230,...ks.filter(k=>k.spalte===s).map(k=>sz(k.id).w));FLGEO.spalten[s]={x,w};x+=w+GAP_X;});
    const nb=new Map(),dazu=(a,b)=>{let l=nb.get(a);if(!l)nb.set(a,l=[]);l.push(b);};
    FL().kanten.forEach(k=>{dazu(k.von,k.nach);dazu(k.nach,k.von);});
    const baender=FL().baender.map(b=>b.id).filter(b=>ks.some(k=>k.band===b));
    if(ks.some(k=>k.band==null&&k.art!=="akteur"))baender.push(null);
    const zellen=new Map(),zk=(b,s)=>(s==="ausgang"||s==="eingang"?"§akt":String(b))+"|"+s;
    ks.forEach(k=>{const key=zk(k.band,k.spalte);let l=zellen.get(key);if(!l)zellen.set(key,l=[]);l.push(k);});
    const Y=new Map(),setze=(k,x,y)=>{const e=els.get(k.id);if(e){e.style.left=x+"px";e.style.top=y+"px";}Y.set(k.id,y+sz(k.id).h/2);};
    const MITTE=["quellen","graphen","aggregate","projektionen"];
    const lege=()=>{let y=FL_KOPF;FLGEO.baender=[];FLGEO.gruppen=new Map();
      baender.forEach(b=>{const y0=y;let hmax=0;
        MITTE.forEach(s=>{let cy=y0+FL_BAND_PAD;const zelle=zellen.get(zk(b,s))||[],sx=FLGEO.spalten[s].x;
          if(s!=="projektionen")zelle.forEach(k=>{setze(k,sx,cy);cy+=sz(k.id).h+FL_GAP_Y;});
          else{const gr=[];zelle.forEach(k=>{const g=k.gruppe||"§"+k.id;if(!gr.includes(g))gr.push(g);});
            gr.forEach(g=>{const drin=zelle.filter(k=>(k.gruppe||"§"+k.id)===g),mit=!g.startsWith("§");const gy=cy;if(mit)cy+=FL_GR_KOPF;
              drin.forEach(k=>{setze(k,sx+(mit?FL_GR_PAD:0),cy);cy+=sz(k.id).h+FL_GAP_Y;});
              if(mit){FLGEO.gruppen.set(g,{x1:sx-FL_GR_PAD,y1:gy,x2:sx+FLGEO.spalten[s].w+3*FL_GR_PAD,y2:cy-FL_GAP_Y+FL_GR_PAD});cy+=FL_GR_PAD+FL_GAP_Y;}});}
          hmax=Math.max(hmax,cy-y0);});
        const hg=Math.max(hmax+FL_BAND_PAD-FL_GAP_Y,140);FLGEO.baender.push({id:b,y:y0,h:hg});y+=hg+44;});
      ["ausgang","eingang"].forEach(s=>{let cy=FL_KOPF+FL_BAND_PAD;(zellen.get(zk(null,s))||[]).forEach(k=>{setze(k,FLGEO.spalten[s].x,cy);cy+=sz(k.id).h+FL_GAP_Y;});});};
    lege();
    // Reihenfolge je Zelle (bei Projektionen je Store-Gruppe): zur mittleren Höhe der Nachbarn in anderen Zellen.
    for(let i=0;i<2;i++){
      zellen.forEach(l=>{const w=new Map(l.map(k=>{const ys=(nb.get(k.id)||[]).filter(o=>Y.has(o)&&FLKARTE.get(o)&&FLKARTE.get(o).spalte!==k.spalte).map(o=>Y.get(o));
          return [k.id,ys.length?ys.reduce((a,b)=>a+b,0)/ys.length:Y.get(k.id)];}));
        const gw=new Map();l.forEach(k=>{const g=k.gruppe||"§"+k.id;gw.set(g,Math.min(gw.has(g)?gw.get(g):1e9,w.get(k.id)));});
        l.sort((a,b)=>gw.get(a.gruppe||"§"+a.id)-gw.get(b.gruppe||"§"+b.id)||w.get(a.id)-w.get(b.id));});
      lege();}
  }
  // Rahmen der Fläche: Spaltenköpfe (mit ＋ und den Clients), Bänder der Domänen, Store-Gruppen.
  function flRahmen(ebene){const aus={x2:0,y2:0};
    FL().spalten.forEach(s=>{const g=FLGEO.spalten[s];if(!g)return;const d=FL_SPALTE[s];
      const neu=e=>{e.stopPropagation();if(d.neu[0]==="store"){stoNeuAnlegen(e);return;}if(d.neu.length===1)neuerKnoten(d.neu[0]);else menue(e.clientX,e.clientY,d.t,d.neu.map(k=>({t:"＋ "+(NODELABEL[k]||k),fn:()=>neuerKnoten(k)})));};
      const k=h("div",{class:"fl-kopf"},h("div",{},h("b",{},d.t),h("button",{title:"Neu in dieser Spalte",onclick:neu},"＋")),h("div",{class:"fl-u"},d.u),
        s==="ausgang"&&MODEL.clients.length?h("div",{class:"fk-chips"},...MODEL.clients.map(c=>h("button",{class:"fk-chip",title:"Client "+c.name+" verkörpert "+(c.verkoerpert||[]).join(", ")+" — Klick: Panel",
          onclick:e=>{e.stopPropagation();komKarte("cl:"+c._id);}},"🔌 "+clientAnzeige(c)))):null);
      Object.assign(k.style,{left:g.x+"px",top:"0px",width:g.w+"px"});ebene.append(k);aus.x2=Math.max(aus.x2,g.x+g.w);});
    const sq=FLGEO.spalten.quellen,sp=FLGEO.spalten.projektionen;
    if(sq&&sp)FLGEO.baender.forEach(b=>{const x1=sq.x-40,x2=sp.x+sp.w+40,name=b.id?(FL().baender.find(x=>x.id===b.id)||{}).name||b.id:"ohne Domäne";
      const fr=h("div",{class:"fl-band"},h("span",{class:"fl-band-t",title:b.id||""},name));fr.style.setProperty("--dc","hsl("+domHue(b.id||"§")+" 45% 55%)");
      Object.assign(fr.style,{left:x1+"px",top:b.y+"px",width:(x2-x1)+"px",height:b.h+"px"});ebene.append(fr);aus.y2=Math.max(aus.y2,b.y+b.h);});
    FLGEO.gruppen.forEach((r,id)=>{const g=FL().gruppen.find(x=>x.id===id)||{name:id};
      const fr=h("div",{class:"fl-gruppe"},h("span",{class:"fl-gruppe-t",title:"Store "+g.name+" — Klick: von innen öffnen",onclick:e=>{e.stopPropagation();komOeffne(id);}},"⧉ Store "+g.name));
      Object.assign(fr.style,{left:r.x1+"px",top:r.y1+"px",width:(r.x2-r.x1)+"px",height:(r.y2-r.y1)+"px"});ebene.append(fr);});
    return aus;}
  // Der Fokus auf der Fläche: bei gewählter Karte sie und ihre Nachbarn über die Kanten (bei einem gewählten Typ nur dessen Kanten); sonst
  //   (markierte Menge, Spur) die Karten, deren Panel-Karte im Fokus liegt.
  function flFokus(){if(!FOCUS)return null;const K=flKarten(),karten=new Set(),kanten=new Set();
    if(FOKUS&&FOKUS.art==="kette"&&SEL){const typ=SEL.startsWith("rec:")?SEL.slice(4):null;
      const sel=new Set(K.filter(k=>flPanelId(k)===SEL||(typ&&k.ports.some(p=>String(p.typ).split("<")[0]===typ))).map(k=>k.id));
      sel.forEach(i=>karten.add(i));
      FL().kanten.forEach((k,i)=>{if(!(sel.has(k.von)||sel.has(k.nach)))return;if(typ&&String(k.typ).split("<")[0]!==typ)return;karten.add(k.von);karten.add(k.nach);kanten.add(i);});
      return {karten,kanten};}
    K.forEach(k=>{if(FOCUS.has(flPanelId(k)||""))karten.add(k.id);});
    FL().kanten.forEach((k,i)=>{if(karten.has(k.von)&&karten.has(k.nach))kanten.add(i);});
    return {karten,kanten};}
  // Kanten von Port zu Port. Vorwärts direkt (über mehr als eine Spalte durch die Gasse oben im Band), Rücklauf unten durch das Band zurück,
  //   „liest“ vom Graphen zur Store-Gruppe.
  function flKanten(frag){
    const ELS=new Map([...world.querySelectorAll(".gnode2.fk")].map(e=>[e.dataset.id,e])),SP=FL().spalten;
    const punkt=(id,ri,typ)=>{const e=ELS.get(id);if(!e)return null;const p=e.querySelector('[data-port="'+CSS.escape(ri+"|"+typ)+'"]');
      return {x:e.offsetLeft+(ri==="aus"?e.offsetWidth:0),y:e.offsetTop+(p?p.offsetTop+p.offsetHeight/2:14)};};
    const band=id=>{const k=FLKARTE.get(id);return k&&k.band!==undefined?FLGEO.baender.find(b=>b.id===(k.band||null)):null;};
    const ix=id=>{const k=FLKARTE.get(id);return k?SP.indexOf(k.spalte):-1;};
    FL().kanten.forEach((k,i)=>{const st=FL_KANTE[k.art]||FL_KANTE.schreiben;let d;
      if(k.art==="liest"){const e=ELS.get(k.von),r=FLGEO.gruppen.get(k.nach);if(!e||!r)return;
        const A={x:e.offsetLeft+e.offsetWidth/2,y:e.offsetTop+e.offsetHeight},B={x:(r.x1+r.x2)/2,y:r.y1};
        d="M"+A.x+","+A.y+" C"+A.x+","+(A.y+120)+" "+B.x+","+(B.y-120)+" "+B.x+","+B.y;}
      else{const A=punkt(k.von,"aus",k.typ),B=punkt(k.nach,"ein",k.typ);if(!A||!B)return;
        const sa=ix(k.von),sb=ix(k.nach),bb=band(k.nach)||band(k.von);
        if(sb>sa+1&&bb){const gy=bb.y+FL_BAND_PAD/2;
          d="M"+A.x+","+A.y+" C"+(A.x+50)+","+A.y+" "+(A.x+10)+","+gy+" "+(A.x+60)+","+gy+" L"+(B.x-60)+","+gy+" C"+(B.x-10)+","+gy+" "+(B.x-50)+","+B.y+" "+B.x+","+B.y;}
        else if(sb<sa&&bb){const gy=bb.y+bb.h-FL_BAND_PAD/2;
          d="M"+A.x+","+A.y+" C"+(A.x+60)+","+A.y+" "+(A.x+30)+","+gy+" "+(A.x-10)+","+gy+" L"+(B.x+10)+","+gy+" C"+(B.x-30)+","+gy+" "+(B.x-60)+","+B.y+" "+B.x+","+B.y;}
        else{const c=Math.min(160,Math.max(50,Math.abs(B.x-A.x)*0.45));
          d="M"+A.x+","+A.y+" C"+(A.x+c)+","+A.y+" "+(B.x-c)+","+B.y+" "+B.x+","+B.y;}}
      const p=document.createElementNS(SVGNS,"path");p.setAttribute("class","glink fl-"+k.art+(st.leise?" leise":""));p.setAttribute("fill","none");
      p.setAttribute("stroke",st.c);p.setAttribute("stroke-width","1.8");if(st.d)p.setAttribute("stroke-dasharray",st.d);p.setAttribute("d",d);
      p.dataset.i=i;p.dataset.a=k.von;p.dataset.b=k.nach;frag.append(p);
      const hit=document.createElementNS(SVGNS,"path");hit.setAttribute("class","glink fl-hit");hit.setAttribute("d",d);hit.setAttribute("fill","none");hit.dataset.i=i;
      hit.addEventListener("pointerdown",e=>e.stopPropagation());hit.addEventListener("click",e=>{e.stopPropagation();flKanteMenue(k,e);});
      hit.append(Object.assign(document.createElementNS(SVGNS,"title"),{textContent:k.typ+" · "+k.art+" — Klick: lösen / öffnen"}));frag.append(hit);});}
  // ══ KOMPOSITIONS-MODAL (docs/10 §10.9): ein Graph von innen — links der Baukasten, in der Mitte die Leinwand (Knoten nach ihrer
  //   Tiefe in der Kette, je [Von] eine Kante, links die Köpfe, rechts was den Graphen verlässt), rechts das Panel des Gewählten; dazu der Reiter
  //   Instanzen. Alles aus board.sicht.komposition und board.sicht.baukasten; ein Graph im Graphen öffnet eine Ebene tiefer (Brotkrumen).
  let KOM=null;   // {pfad:[Graph-Karten-Id …], reiter:"komposition"|"instanzen", wahl:{art:"knoten",name}|{art:"baustein",gruppe,name}|null}
  const komDaten=id=>(SICHT().komposition||{})[id];
  const KOM_LEER={funktionen:[],graphen:[],katalog:[],technik:[],konfig:[],nachrichten:[]};
  const komBaukasten=()=>SICHT().baukasten||KOM_LEER;
  const komBasis=t=>String(t||"").split("<")[0];
  function komSorte(t){const r=MODEL.records.find(x=>x.name===komBasis(t)),k=r&&r.kind;
    return k==="event"?"event":k==="rejection"?"hinweis":k==="query"?"query":k==="queryresponse"?"antwort":"command";}
  const KOM_SYM={funktion:"ƒ",graph:"⬡",katalog:"⚡",uebersetzung:"↦",fehlt:"⚠"};
  // Der Namespace der Abläufe zu einem Namespace der Domäne (<Wurzel>.<Bereich> → <Wurzel><Suffix der Abläufe>.<Bereich>, Projektstruktur): dort
  //   liegen Graphen, Funktionen der Anwendung und die Nachrichten der Clients.
  function ablaufNs(ns){const suf=(MODEL.rahmen||{}).ablaeufeSuffix||"",v=Object.keys((MODEL.rahmen||{}).verzeichnisse||{});
    if(!suf)return ns;const wurzeln=v.filter(k=>k.endsWith(suf)).sort((a,b)=>b.length-a.length);
    for(const p of wurzeln){const w=p.slice(0,-suf.length);if(ns===p||ns.startsWith(p+"."))return ns;if(ns===w)return p;if(ns.startsWith(w+"."))return p+ns.slice(w.length);}
    return wurzeln[0]||ns;}
  // Wo zuletzt gezeigt wurde — für Namensfragen, die eine Geste ohne eigenes Ereignis stellt (＋ an einem Port im Modal).
  let ZEIGER={x:240,y:160};document.addEventListener("pointerdown",e=>{ZEIGER={x:e.clientX,y:e.clientY};},true);
  // Ein Graph, der gerade angelegt wurde und sich öffnen soll, sobald der Server seine Komposition liefert.
  let KOM_NACH=null;
  // Öffnen: der Pfad von der obersten Graph-Karte hinab (über die Eltern), optional ein Knoten gewählt.
  function komOeffne(grId,knoten){const pfad=[grId];
    for(let i=0;i<8;i++){const d=komDaten(pfad[0]);if(!d||!d.eltern.length||FLKARTE.has(pfad[0]))break;pfad.unshift(d.eltern[0]);}
    KOM={pfad,reiter:"komposition",wahl:knoten?{art:"knoten",name:knoten}:null};komZeige();}
  // Von innen öffnen: ein Graph seine Komposition, ein Aggregat sich selbst, Projektion und Reader ihren Store (mit ihnen gewählt).
  function flInnen(fk){if(fk.art==="graph"){komOeffne(fk.id);return;}const pid=flPanelId(fk);if(pid)komKarte(pid);}
  function komSchliesse(){KOM=null;const m=document.querySelector("#de .kom");if(m)m.remove();}
  document.addEventListener("keydown",e=>{if(e.key==="Escape"&&KOM){e.stopImmediatePropagation();if(KOM.port||KOM.feld){KOM.port=null;KOM.feld=null;komZeige();}else komSchliesse();}},true);
  // Woher ein Kopf kommt und wohin ein Ausgang geht: die Kanten der obersten Graph-Karte auf der Fläche.
  function komOben(grId){let id=grId;for(let i=0;i<8;i++){if(FLKARTE.has(id))return id;const d=komDaten(id);if(!d||!d.eltern.length)return null;id=d.eltern[0];}return null;}
  const komName=id=>(FLKARTE.get(id)||{name:id}).name;
  function komZiele(grId,typ){const o=komOben(grId);return o?[...new Set(FL().kanten.filter(k=>k.von===o&&k.typ===typ).map(k=>komName(k.nach)))]:[];}
  function komHerkunft(grId,typ){const o=komOben(grId);return o?[...new Set(FL().kanten.filter(k=>k.nach===o&&k.typ===typ).map(k=>komName(k.von)))]:[];}
  const komInstanzen=name=>Object.keys(WB.instanzen||{}).filter(i=>((WB.instanzen[i]||{}).Graph||"")===name).sort();
  function komWaehle(w){KOM.wahl=w;komZeige();}
  // Einen Knoten öffnen: ein Graph eine Ebene tiefer, eine Funktion ihr Formular (Signatur, Konstruktor, Rümpfe).
  function komKnotenOeffnen(k){if(k.art==="uebersetzung"){deFlash("ℹ "+k.name+" übersetzt "+k.typ.replace(/^Uebersetze</,"").replace(/>$/,"").replace(", "," ↦ ")+" — die Zuordnung steht auf der Karte",true);return;}
    if(k.art==="graph"&&k.karte){KOM.pfad.push(k.karte);KOM.wahl=null;komZeige();}
    else if(k.karte)komKarte(k.karte,true);else deFlash("ℹ "+k.typ+" ist eine Katalogfunktion des Frameworks — ihre Werte stehen in der Instanz",true);}
  function komKnotenMenue(id,k,e){menueFix(e.clientX,e.clientY,k.name+" : "+k.typ,[
    k.art==="graph"&&k.karte?{t:"⬡ eine Ebene tiefer",fn:()=>komKnotenOeffnen(k)}:k.karte?{t:"✎ bearbeiten",fn:()=>komKnotenOeffnen(k)}:null,
    {t:"✕ aus dem Graphen (die Funktion bleibt)",gefahr:1,fn:()=>komKnotenEntfernen(id,k.name)}]);}
  // Ein Baustein öffnen: sein Formular bzw. ein Graph seine Komposition; der Katalog gehört dem Framework.
  function komBausteinOeffnen(gr,x){
    if(gr==="graphen"){komKarte(x.karte,true);return;}
    if(gr==="funktionen"){komKarte(x.karte,true);return;}
    if(gr==="technik"){komKarte(x.karte,true,true);return;}
    if(gr==="konfig"||gr==="nachrichten"){komKarte("rec:"+(x.name||x.typ),true,true);return;}
    deFlash("ℹ "+x.name+" (Katalog) meldet "+(x.meldet||[]).join(", ")+" — ziehen Sie sie auf die Leinwand",true);}
  // Brotkrumen über alle Ansichten des Pfads: Graph, Aggregat, Store, Formular einer Karte.
  function komKrumenText(g){if(g.startsWith("kat:"))return "📚 "+(g.slice(4).replace(/\.Katalog$/,"")||"Kataloge");if(g.startsWith("form:")){const n=NODEBY.get(g.slice(5));return "✎ "+(n?(NODELABEL[n.kind]||n.kind)+" "+(n.name||""):g.slice(5));}
    if(g.startsWith("gr:"))return "⬡ "+((komDaten(g)||{}).name||g);const n=NODEBY.get(g);
    if(g.startsWith("fb:"))return "ƒ "+(n?n.name:g);return (g.startsWith("agg:")?"▣ ":"🗄 ")+(n?n.name:g);}
  function komKrumen(){return h("div",{class:"kom-krumen"},...KOM.pfad.flatMap((g,i)=>{const b=h("button",{class:"kom-krume"+(i===KOM.pfad.length-1?" an":""),
      onclick:()=>{KOM.pfad=KOM.pfad.slice(0,i+1);KOM.wahl=null;komZeige();}},komKrumenText(g));return i?[h("span",{class:"kom-sep"},"▸"),b]:[b];}));}
  // Zoom in jedem Modal: Tastatur (＋/＝ hinein, − hinaus, 0 zurück — auch mit Strg/⌘) und Strg/⌘ + Mausrad bzw. Zwei-Finger-Zoom; je Ansicht gemerkt.
  //   Gezoomt wird mit CSS-zoom, so bleiben Ports, Kanten und Bildlauf in einem Maß.
  const komAnsichtId=()=>KOM?KOM.pfad[KOM.pfad.length-1]:null;
  function komZoom(f){if(!KOM)return;KOM.zoom=KOM.zoom||{};const id=komAnsichtId(),z=f===0?1:Math.min(3,Math.max(0.25,(KOM.zoom[id]||1)*f));KOM.zoom[id]=z;komZoomAnwenden();}
  function komZoomAnwenden(){const m=document.querySelector("#de .kom");if(!m||!KOM)return;const z=(KOM.zoom||{})[komAnsichtId()]||1;
    const t=m.querySelector(".kom-welt")||m.querySelector(".kom-form");if(t)t.style.zoom=z;const a=m.querySelector(".kom-zoom-wert");if(a)a.textContent=Math.round(z*100)+"%";}
  function komZoomLeiste(){return h("div",{class:"kom-zoom",title:"Zoom: ＋ / − / 0 (auch mit Strg/⌘), Strg/⌘ + Mausrad"},
    h("button",{onclick:()=>komZoom(1/1.2)},"−"),h("span",{class:"kom-zoom-wert",onclick:()=>komZoom(0)},"100%"),h("button",{onclick:()=>komZoom(1.2)},"＋"));}
  const tippt=t=>!!t&&(/^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName)||t.isContentEditable);
  document.addEventListener("keydown",e=>{if(tippt(e.target)||e.altKey)return;
    const rein=e.key==="+"||e.key==="="||e.key==="Add",raus=e.key==="-"||e.key==="_"||e.key==="Subtract",null0=e.key==="0";if(!rein&&!raus&&!null0)return;
    if(KOM){e.preventDefault();e.stopImmediatePropagation();komZoom(rein?1.2:raus?1/1.2:0);return;}
    if(canvas&&document.getElementById("de").classList.contains("on")){e.preventDefault();zoomAuf(null0?1:Math.min(2,Math.max(0.08,PAN.s*(rein?1.2:1/1.2))));}},true);
  document.addEventListener("wheel",e=>{if(!KOM||!(e.ctrlKey||e.metaKey))return;if(!e.target.closest||!e.target.closest("#de .kom"))return;
    e.preventDefault();komZoom(e.deltaY<0?1.1:1/1.1);},{passive:false,capture:true});
  // Neu gezeichnet wird bei jedem Klick; der Bildlauf der Fläche bleibt, solange es dieselbe Ansicht ist — sonst spränge sie nach links oben.
  function komZeige(){const de=document.getElementById("de");let m=de.querySelector(".kom");
    const scroll=m&&m.querySelector(".kom-panel")?m.querySelector(".kom-panel").scrollTop:0,alt=m&&m.querySelector(".kom-lw");
    const lauf=alt?{id:m.dataset.ansicht,x:alt.scrollLeft,y:alt.scrollTop}:null;if(m)m.remove();if(!KOM)return;
    const id=KOM.pfad[KOM.pfad.length-1];
    if(id.startsWith("form:"))formZeige(de,id.slice(5));else if(id.startsWith("kat:"))katZeige(de,id.slice(4));else if(id.startsWith("fb:"))funZeige(de,id);else if(!id.startsWith("gr:"))blockZeige(de,id);else grZeige(de,id);
    const neu=de.querySelector(".kom .kom-panel");if(neu&&scroll)neu.scrollTop=scroll;komZoomAnwenden();
    const km=de.querySelector(".kom");if(km)km.dataset.ansicht=id;
    const lw=km&&km.querySelector(".kom-lw");if(lw&&lauf&&lauf.id===id){lw.scrollLeft=lauf.x;lw.scrollTop=lauf.y;}}
  function komRahmen(unter){const m=h("div",{class:"kom"});
    ["pointerdown","wheel","dblclick","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>e.stopPropagation()));
    m.append(h("div",{class:"kom-kopf"},komKrumen(),h("div",{class:"kom-u"},unter),
      h("div",{class:"kom-reiter"},komZoomLeiste(),h("button",{class:"kom-x",title:"Schließen (Esc)",onclick:komSchliesse},"✕"))));return m;}
  function grZeige(de,id){const d=komDaten(id);if(!d){KOM=null;return;}
    const m=h("div",{class:"kom"});
    ["pointerdown","wheel","dblclick","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>e.stopPropagation()));
    const krumen=komKrumen();
    const inst=komInstanzen(d.name);
    m.append(h("div",{class:"kom-kopf"},krumen,
      h("div",{class:"kom-u"},"Kopf nimmt "+(d.koepfe.map(k=>k.typ).join(", ")||"nichts von außen (Quelle)")+" · verlässt ihn: "+(d.ausgaenge.map(a=>a.typ).join(", ")||"nichts")),
      h("div",{class:"kom-reiter"},komZoomLeiste(),...[["komposition","Komposition"],["instanzen","Instanzen ("+inst.length+")"]].map(([k,t])=>
        h("button",{class:KOM.reiter===k?"on":"",onclick:()=>{KOM.reiter=k;komZeige();}},t)),
        h("button",{class:"kom-x",title:"Schließen (Esc)",onclick:komSchliesse},"✕"))));
    if(KOM.reiter==="instanzen"){m.append(komInstanzenAnsicht(d,inst));de.append(m);return;}
    const lw=komLeinwand(id,d);
    m.append(h("div",{class:"kom-leib"},komBaukastenEl(id,d),lw));de.append(m);komLege(lw,id,d);}

  // ── Leinwand: Knoten wie auf der Fläche (Kopf + Ports), Technik und Konfig als Chips; Ränder für Köpfe und Ausgänge ──
  //   (docs/plan-katalog-und-graphen.md, Schritt 4) Die Ports eines Funktionsknotens stehen je Auf: sein Eingang, darunter seine Ausgänge mit
  //   Vielfachheit (1 · 0..1 · 0..n). „＋“ an einem Eingang hebt die Knoten hervor, die den Typ ausgeben (dazu „von außen“ = [Kopf]), „＋“ an einem
  //   Ausgang die Knoten, die ihn nehmen ([Von] am Nehmer); gestrichelt steht, was neu entstehen kann: eine Funktion davor bzw. dahinter. Eine Antwort
  //   (Event auf einen eigenen Command an ein Aggregat) kommt vom Aggregat — sie hat kein ＋.
  function komPort(ri,typ,sorte,titel,klick,anzeige){const r=h("div",{class:"fk-port "+ri+" s-"+sorte,title:titel||(sorte+" "+typ),onclick:e=>{e.stopPropagation();if(klick)klick();}},
      h("span",{class:"fk-dot"}),h("span",{class:"fk-pl"},(FL_SORTE[sorte]||"")+(anzeige||typ)));r.dataset.port=ri+"|"+typ;return r;}
  const komTypKlick=t=>()=>{if(NODEBY.has("rec:"+komBasis(t)))komKarte("rec:"+komBasis(t),true,true);};
  // Kann Knoten m an den Eingang t von Ziel z anschließen ([Von])? Gleicher Typ, eine generische Ausgabe mit dem Typargument von m.
  function komPasst(m,z,t){if(m.name===z.name||(z.von||[]).some(v=>v.knoten===m.name))return null;
    const args=x=>{const i=String(x).indexOf("<");return i>0&&String(x).endsWith(">")?String(x).slice(i+1,-1):"";};
    return m.aus.find(a=>a===t||(komBasis(a)===komBasis(t)&&args(a)&&args(t)===args(m.typ)))||null;}
  // Nimmt Knoten z, was Knoten q als t ausgibt? (der Eingang von z, an dem es ankäme)
  const komNimmt=(q,z,t)=>z.ein.find(e=>komPasst(q,z,e)===t)||null;
  const komIstAntwort=t=>(recByName(komBasis(t))||{}).kind==="event";
  // Die Ports eines Knotens je Auf: [{ein, aus:[{typ, mult}]}] — bei einer Funktion aus ihrer Signatur (Typen wie im Knoten, auch generisch), sonst flach.
  function komGruppen(k){if(k.art==="uebersetzung")return [{ein:k.ein[0],aus:k.aus.map(t=>({typ:t,mult:"1"}))}];
    const f=k.karte&&k.karte.startsWith("fb:")?funModell(k.karte):null,b=x=>komBasis(x);
    const imKnoten=(liste,t)=>liste.find(x=>x===t)||liste.find(x=>b(x)===b(t))||t;
    if(!f)return [{ein:null,aus:k.aus.map(t=>({typ:t,mult:""}))},...k.ein.map(t=>({ein:t,aus:[]}))];
    const g=(f.eingaenge||[]).map(e=>({ein:imKnoten(k.ein,e.eingang),aus:(e.ausgaben||[]).map(o=>({typ:imKnoten(k.aus,o),mult:funMult(e)}))}));
    if((f.lauscht||[]).length)g.unshift({ein:null,aus:f.lauscht.map(o=>({typ:imKnoten(k.aus,o),mult:"0..n"}))});
    return g;}
  function komKnotenEl(id,k){
    const gewaehlt=KOM.wahl&&KOM.wahl.art==="knoten"&&KOM.wahl.name===k.name,P=KOM.port;
    const d=komDaten(id),bezug=P&&d?d.knoten.find(x=>x.name===P.knoten):null;
    const passt=!bezug?null:P.art==="von"?komPasst(k,bezug,P.typ):komNimmt(bezug,k,P.typ);
    const el=h("div",{class:"gnode2 fk kom-k n-"+(k.art==="graph"?"graph":k.art==="uebersetzung"?"uebersetzung":k.art==="katalog"||k.quelle?"funktion":"bausteinfunktion")+(gewaehlt?" sel":"")+(passt?" kand":"")+(P&&!passt&&k.name!==P.knoten?" leise":""),
      title:passt?(P.art==="von"?"Klick: "+P.knoten+" bekommt von "+k.name+" ("+passt+")":"Klick: "+k.name+" bekommt "+P.typ+" von "+P.knoten)
        :"Klick: Kanten hervorheben · Doppelklick: "+(k.art==="graph"?"eine Ebene tiefer":k.karte?"von innen":"—")+" · Rechtsklick: Menü",
      onclick:()=>{if(passt){KOM.port=null;if(P.art==="von")komVerbinde(id,k.name,P.knoten);else komVerbinde(id,P.knoten,k.name);return;}
        if(P){KOM.port=null;komZeige();return;}komWaehle(gewaehlt?null:{art:"knoten",name:k.name});},
      ondblclick:()=>{if(!P)komKnotenOeffnen(k);},
      oncontextmenu:e=>{e.preventDefault();komKnotenMenue(id,k,e);}});
    el.dataset.knoten=k.name;
    el.append(h("div",{class:"ghead"},h("span",{class:"gtitle hatname",title:k.typ},h("span",{class:"gk"},(KOM_SYM[k.art]||"")+" "+k.name+" "),h("span",{class:"gn"},k.typ)),
      k.kopf?h("span",{class:"kom-mark",title:"[Kopf] — nimmt von außen"},"Kopf"):null,k.quelle?h("span",{class:"kom-mark",title:"Quelle — lauscht"},"Quelle"):null,
      h("span",{class:"gx",title:"Löschen: aus dem Graphen bzw. samt Funktion",onclick:e=>{e.stopPropagation();komKnotenLoeschen(id,k,e);}},"✕")));
    const chips=(k.konstruktor||[]).filter(c=>c.art!=="logger");
    const funktion=k.art==="funktion"&&k.karte;
    if(chips.length||(k.liest||[]).length||funktion)el.append(h("div",{class:"fk-chips"},
      ...chips.map(c=>h("span",{class:"fk-chip kc-"+c.art,title:c.art+(c.umsetzungen?" · Umsetzungen: "+c.umsetzungen.join(", "):"")},(c.art==="technik"?"⚙ ":c.art==="konfig"?"▤ ":"◉ ")+c.typ)),
      ...(k.liest||[]).map(t=>h("span",{class:"fk-chip kc-liest",title:"Lese-Fähigkeit"},"⌕ "+t)),
      funktion?h("button",{class:"fk-chip ag-plus",title:"Technik oder Konfig in den Konstruktor nehmen",onclick:e=>{e.stopPropagation();komKonstruktorPlus(id,k,e);}},"＋ Technik/Konfig"):null));
    const ports=h("div",{class:"fk-ports"});
    const plus=(art,t,titel)=>{const aktiv=P&&P.art===art&&P.knoten===k.name&&P.typ===t;
      return h("button",{class:"ag-plus kom-einplus"+(aktiv?" aktiv":""),title:titel,onclick:e=>{e.stopPropagation();KOM.port=aktiv?null:{art,knoten:k.name,typ:t,karte:id};komZeige();}},"＋");};
    komGruppen(k).forEach(gr=>{
      if(gr.ein){const antwort=komIstAntwort(gr.ein),pe=komPort("ein",gr.ein,komSorte(gr.ein),antwort?"Antwort vom Aggregat auf einen eigenen Command — kommt von dort, nicht über eine Kante":null,
          komTypKlick(gr.ein),antwort?"◀ "+gr.ein:null);
        if(!antwort)pe.append(plus("von",gr.ein,"Anschließen: wer gibt "+gr.ein+"?"));ports.append(pe);}
      gr.aus.forEach(o=>{const pa=komPort("aus",o.typ,komSorte(o.typ),o.mult?o.typ+" — "+(o.mult==="1"?"genau eine je Aufruf":o.mult==="0..1"?"keine oder eine (Wahl)":"keine bis viele (Strom)"):null,
          komTypKlick(o.typ),o.typ+(o.mult?"  "+o.mult:""));
        if(komSorte(o.typ)==="command"&&!(recByName(komBasis(o.typ))||{}).aggregat)pa.append(plus("nach",o.typ,"Anschließen: wer nimmt "+o.typ+"?"));ports.append(pa);});});
    if(ports.childNodes.length)el.append(ports);
    if(k.uebersetzung)el.append(komUebersetzungEl(id,k));
    return el;}
  // Die ↦-KARTE einer Übersetzung (docs/04 §4.4): links die Felder von TEin, rechts die Parameter von TAus mit ihrer Regel — „=“ nach Namen,
  //   „⇐“ ausdrücklich ([Feld]), „∅“ Standardwert, „⚠“ offen. ＋ an einem Ziel hebt die Felder hervor, die passen (der Server rechnet sie), ein Klick
  //   schreibt [Feld]; ✕ nimmt es zurück.
  function komUebersetzungEl(id,k){const u=k.uebersetzung,F=KOM.feld&&KOM.feld.knoten===k.name?KOM.feld:null,ziel=F&&(u.ziele||[]).find(z=>z.name===F.ziel);
    const links=h("div",{class:"kom-ue-sp"},h("div",{class:"kom-ue-t",title:u.ein},u.ein));
    (u.quellen||[]).forEach(q=>{const passt=!!ziel&&(ziel.passend||[]).includes(q.name);
      links.append(h("div",{class:"kom-ue-f"+(passt?" kand":"")+(F&&!passt?" leise":""),title:q.typ+(passt?" — Klick: "+F.ziel+" ← "+q.name:""),
        onclick:e=>{e.stopPropagation();if(passt)komFeldSetze(id,k.name,F.ziel,q.name);}},q.name+" ",h("span",{class:"gn"},q.typ)));});
    if(!(u.quellen||[]).length)links.append(h("div",{class:"kom-ue-f leise"},"— keine Felder"));
    const rechts=h("div",{class:"kom-ue-sp"},h("div",{class:"kom-ue-t",title:u.aus},u.aus));
    const R={feld:["⇐","ausdrücklich ([Feld])"],name:["=","nach Namen"],standard:["∅","Standardwert des Parameters"]};
    (u.ziele||[]).forEach(z=>{const [sym,txt]=R[z.regel]||["⚠","offen — ＋ nennt die Quelle"],an=!!F&&F.ziel===z.name;
      rechts.append(h("div",{class:"kom-ue-f r-"+(z.regel||"offen")+(an?" aktiv":"")},h("span",{class:"kom-ue-r",title:txt},sym),
        z.name+(z.quelle?" ← "+z.quelle:"")+" ",h("span",{class:"gn"},z.typ),
        z.regel==="feld"?h("span",{class:"gx",title:"[Feld] lösen",onclick:e=>{e.stopPropagation();komFeldLoese(id,k.name,z.name);}},"✕")
          :h("button",{class:"ag-plus kom-einplus"+(an?" aktiv":""),title:(z.passend||[]).length?"Quelle nennen: "+z.passend.join(", "):"kein Feld von "+u.ein+" passt",
            onclick:e=>{e.stopPropagation();KOM.feld=an?null:{knoten:k.name,ziel:z.name};KOM.port=null;komZeige();}},"＋")));});
    const box=h("div",{class:"kom-ue"},links,h("div",{class:"kom-ue-pfeil"},"↦"),rechts);
    return h("div",{},box,(u.fehler||[]).length?h("div",{class:"kom-ue-fehler"},...u.fehler.map(f=>h("div",{},"⚠ "+f))):null);}
  function komFeldSetze(id,knoten,ziel,quelle){const g=komModell(id),k=g&&(g.knoten||[]).find(x=>x.name===knoten);if(!k)return;
    k.felder=[...(k.felder||[]).filter(f=>f.ziel!==ziel),{ziel,quelle}];KOM.feld=null;gTitel(g.name+": "+knoten+" ["+ziel+" ← "+quelle+"]");render();}
  function komFeldLoese(id,knoten,ziel){const g=komModell(id),k=g&&(g.knoten||[]).find(x=>x.name===knoten);if(!k)return;
    k.felder=(k.felder||[]).filter(f=>f.ziel!==ziel);gTitel(g.name+": "+knoten+" ["+ziel+"] gelöst");render();}
  // Gestrichelt beim Anschließen: was neu entstehen kann — eine Funktion davor (gibt den Typ aus) bzw. dahinter (nimmt ihn).
  function komGeister(id,d){const P=KOM.port;if(!P)return [];const q=d.knoten.find(x=>x.name===P.knoten);if(!q)return [];
    return P.art==="von"?[{tiefe:Math.max(0,q.tiefe-1),t:"＋ neue Funktion davor",u:"gibt "+P.typ+" aus — und nimmt eine neue Nachricht von außen",fn:e=>komFunktionNeu(id,"davor",q,P.typ,e)}]
      :[{tiefe:q.tiefe+1,t:"＋ neue Funktion dahinter",u:"nimmt "+P.typ+" — sie öffnet sich danach von innen",fn:e=>komFunktionNeu(id,"dahinter",q,P.typ,e)},
        ...(q.art==="uebersetzung"?[]:[{tiefe:q.tiefe+1,t:"↦ ▣ Command an …",u:"übersetzt "+P.typ+" in einen Command an ein Aggregat (vorhanden oder neu samt Decide)",
          fn:e=>komCommandAn(id,q,P.typ,e)}])];}
  // Wo eine neue Funktion liegen darf: in der Anwendung (wenn der Graph dort liegt) und in jedem Katalog, der den Typ kennt (Typ aus dem Katalog
  //   oder vom Framework) — ein Katalog kennt keine Domäne.
  function komOrte(g,typ){const gk=funKatalog(g),r=recByName(komBasis(typ)),rk=r?funKatalog(r):null,bk=komBaukasten();
    if(gk)return [{katalog:gk}];
    return [{katalog:null},...(bk.kataloge||[]).filter(k=>!r||rk===k).map(k=>({katalog:k}))];}
  function komFunktionNeu(id,art,q,typ,ev){const g=komModell(id);if(!g)return;KOM.port=null;
    const anlegen=ort=>{const stamm=komBasis(typ).replace(/(Gefunden|Erstellt|Eingelesen|Verkleinert)$/,"");
      const weiter=bereich=>frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion "+art+" "+q.name,art==="dahinter"?"Verarbeite"+komBasis(typ):komBasis(typ)+"Bildung",
        art==="dahinter"?"Nimmt "+typ+"; Ausgänge danach im Modal":"Gibt "+typ+" aus; nimmt eine neue Nachricht von außen",n=>{
        const fe=nameFehler(n);if(fe&&!/gibt es schon/.test(fe))return fe;if(MODEL.bausteinFunktionen.some(x=>x.name===n))return n+" gibt es schon";
        const ns=ort.katalog?ort.katalog+"."+bereich:g.namespace;
        const f={_id:"funktion:"+n,name:n,namespace:ns,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[]};
        if(art==="dahinter")f.eingaenge.push({eingang:typ,ausgaben:[]});
        else{const auftrag=uniq(n+"Auftrag");funRecordNeu(f,auftrag,"command");f.eingaenge.push({eingang:auftrag,ausgaben:[typ]});}
        MODEL.bausteinFunktionen.push(f);
        let kn=n,i=2;while((g.knoten||[]).some(x=>x.name===kn))kn=n+(i++);
        const knoten={typ:n,name:kn,kopf:art==="davor",von:art==="dahinter"?[q.name]:[],verlierbar:[]};
        (g.knoten=g.knoten||[]).push(knoten);
        if(art==="davor"){const z=g.knoten.find(x=>x.name===q.name);if(z)z.von=[...(z.von||[]),kn];}
        gTitel(g.name+": ƒ "+n+" "+art+" "+q.name);render();
        if(art==="dahinter")setTimeout(()=>komKarte("fb:"+f._id,true),0);});
      if(!ort.katalog){weiter(null);return;}
      frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion in "+ort.katalog+" — Bereich","Allgemein","Der Ordner im Katalog",b=>{
        if(!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(b))return "Ein Bereich: groß beginnen, nur Buchstaben und Ziffern";setTimeout(()=>weiter(b),0);});};
    const orte=komOrte(g,typ);
    if(orte.length===1){anlegen(orte[0]);return;}
    menueFix(ev.clientX,ev.clientY,"Wo liegt die neue Funktion?",orte.map(o=>({t:o.katalog?"📚 "+o.katalog:"ƒ in der Anwendung ("+g.namespace+")",fn:()=>anlegen(o)})));}
  function komLeinwand(id,d){
    const welt=h("div",{class:"kom-welt"}),svgK=document.createElementNS(SVGNS,"svg");svgK.setAttribute("class","kom-kanten");welt.append(svgK);
    const P=KOM.port&&KOM.port.art==="von"?KOM.port:null,zk=P?d.knoten.find(x=>x.name===P.knoten):null,kopfMoeglich=!!zk&&!zk.kopf;
    const g=komModell(id),hatQuelle=d.knoten.some(k=>k.quelle);
    const links=h("div",{class:"gnode2 fk kom-rand links"+(kopfMoeglich?" kand":"")+(KOM.port&&!kopfMoeglich?" leise":""),title:kopfMoeglich?"Klick: "+P.knoten+" nimmt "+P.typ+" von außen ([Kopf])":"",
      onclick:()=>{if(kopfMoeglich){KOM.port=null;komVerbinde(id,null,P.knoten);}}},h("div",{class:"ghead"},h("span",{class:"gtitle"},"◀ von außen")));
    const lp=h("div",{class:"fk-ports"});
    d.koepfe.forEach(k=>{const her=komHerkunft(id,k.typ),pe=komPort("aus",k.typ,komSorte(k.typ),"Kopf "+k.typ+(her.length?" — von "+her.join(", "):""),komTypKlick(k.typ));
      lp.append(pe);
      lp.append(h("div",{class:"kom-wo"},her.length?"von "+her.join(", "):"⚠ niemand gibt es hinein"));});
    if(!d.koepfe.length)lp.append(h("div",{class:"kom-wo"},hatQuelle?"— eine Quelle lauscht":"— noch kein Kopf"));
    // Eine Quelle steht nur in einem Graphen ohne Kopf (sie ist dann sein einziger Kopf).
    if(!hatQuelle&&g)lp.append(h("button",{class:"ag-plus",title:"Ein Eingang an den Kopf: eine Quelle (sie lauscht) oder ein Client (ein Akteur gibt eine Nachricht hinein)",
      onclick:e=>{e.stopPropagation();komEingangPlus(id,g,d,e);}},"＋ Eingang"));
    links.append(lp);welt.append(links);
    const rechts=h("div",{class:"gnode2 fk kom-rand rechts"},h("div",{class:"ghead"},h("span",{class:"gtitle"},"verlässt ▶")));
    const rp=h("div",{class:"fk-ports"});
    d.ausgaenge.forEach(a=>{const zu=komZiele(id,a.typ),r=recByName(komBasis(a.typ)),ohneZiel=a.sorte==="command"&&!(r&&r.aggregat);
      const pe=komPort("ein",a.typ,a.sorte,a.typ+(zu.length?" → "+zu.join(", "):" — hört niemand"),komTypKlick(a.typ));
      if(ohneZiel&&g)pe.append(h("button",{class:"ag-plus kom-einplus",title:"Wer nimmt "+a.typ+"? — der Kopf eines anderen Graphen (Verbindung)",
        onclick:e=>{e.stopPropagation();komEmpfaengerPlus(id,g,a.typ,e);}},"＋"));
      rp.append(pe);
      rp.append(h("div",{class:"kom-wo"},zu.length?"→ "+zu.join(", "):ohneZiel?"⚠ kein Empfänger":"hört niemand"));});
    if(!d.ausgaenge.length)rp.append(h("div",{class:"kom-wo"},"— nichts"));
    rechts.append(rp);welt.append(rechts);
    d.knoten.forEach(k=>welt.append(komKnotenEl(id,k)));
    komGeister(id,d).forEach((gs,i)=>{const el=h("div",{class:"gnode2 fk kom-k ag-neu kand",title:gs.u,onclick:e=>{e.stopPropagation();gs.fn(e);}},
        h("div",{class:"ghead"},h("span",{class:"gtitle"},h("span",{class:"gk"},gs.t))),h("div",{class:"ag-kurz"},gs.u));
      el.dataset.geist=i;el.dataset.tiefe=gs.tiefe;welt.append(el);});
    if(!d.knoten.length)welt.append(h("div",{class:"kom-leer"},"Leerer Graph — „＋ Eingang“ links oder „＋“ an einem Baustein im Baukasten setzt den ersten Knoten ein."));
    return h("div",{class:"kom-lw"},welt);}
  // Anordnen nach Tiefe (Spalten), dann die Kanten: Kopf → Knoten, je [Von] Knoten → Knoten (verlierbar gestrichelt), Knoten → Ausgang.
  //   Gestrichelte Karten stehen unten in der Spalte ihrer Tiefe.
  function komLege(lw,id,d){const welt=lw.querySelector(".kom-welt"),svgK=welt.querySelector("svg");
    const el=n=>welt.querySelector('.kom-k[data-knoten="'+CSS.escape(n)+'"]'),links=welt.querySelector(".kom-rand.links"),rechts=welt.querySelector(".kom-rand.rechts");
    const geister=[...welt.querySelectorAll(".kom-k[data-geist]")];
    const GX=110,GY=26,X0=40,Y0=30;let x=X0;
    links.style.left=x+"px";links.style.top=Y0+"px";x+=links.offsetWidth+GX;
    const tiefen=[...new Set(d.knoten.map(k=>k.tiefe).concat(geister.map(e=>+e.dataset.tiefe)))].sort((a,b)=>a-b);let yMax=Y0+links.offsetHeight;
    tiefen.forEach(t=>{const ks=d.knoten.filter(k=>k.tiefe===t).map(k=>el(k.name)).filter(Boolean).concat(geister.filter(e=>+e.dataset.tiefe===t));let y=Y0,w=0;
      ks.forEach(e=>{e.style.left=x+"px";e.style.top=y+"px";y+=e.offsetHeight+GY;w=Math.max(w,e.offsetWidth);});yMax=Math.max(yMax,y);x+=w+GX;});
    rechts.style.left=x+"px";rechts.style.top=Y0+"px";x+=rechts.offsetWidth+X0;yMax=Math.max(yMax,Y0+rechts.offsetHeight)+Y0;
    welt.style.width=x+"px";welt.style.height=yMax+"px";svgK.setAttribute("width",x);svgK.setAttribute("height",yMax);
    const punkt=(e,ri,typ)=>{if(!e)return null;const p=e.querySelector('[data-port="'+CSS.escape(ri+"|"+typ)+'"]');
      return {x:e.offsetLeft+(ri==="aus"?e.offsetWidth:0),y:e.offsetTop+(p?p.offsetTop+p.offsetHeight/2:14)};};
    const wahl=KOM.wahl&&KOM.wahl.art==="knoten"?KOM.wahl.name:null;
    const linie=(A,B,verl,sorte,klick,an)=>{if(!A||!B)return;const hell=wahl&&(an||[]).includes(wahl);const c=Math.max(40,Math.abs(B.x-A.x)*0.45),d2="M"+A.x+","+A.y+" C"+(A.x+c)+","+A.y+" "+(B.x-c)+","+B.y+" "+B.x+","+B.y;
      const p=document.createElementNS(SVGNS,"path");p.setAttribute("d",d2);p.setAttribute("fill","none");
      p.setAttribute("stroke",sorte==="hinweis"?"#cf6f68":sorte==="event"?"#57b673":"#8fa6c8");p.setAttribute("stroke-width",hell?"3.2":"1.8");if((wahl&&!hell)||KOM.port)p.setAttribute("opacity",".35");if(verl)p.setAttribute("stroke-dasharray","6 4");svgK.append(p);
      if(klick){const hit=document.createElementNS(SVGNS,"path");hit.setAttribute("d",d2);hit.setAttribute("fill","none");hit.setAttribute("class","kom-hit");hit.addEventListener("click",klick);svgK.append(hit);}};
    d.koepfe.forEach(k=>k.knoten.forEach(n=>linie(punkt(links,"aus",k.typ),punkt(el(n),"ein",k.typ),false,komSorte(k.typ),e=>komKanteMenue(id,"kopf",null,n,e),[n])));
    d.kanten.forEach(k=>{const a=el(k.von),b=el(k.nach),A=a&&punkt(a,"aus",(d.knoten.find(x=>x.name===k.von)||{aus:[]}).aus.find(t=>komBasis(t)===komBasis(k.typ))||k.typ);
      linie(A,punkt(b,"ein",k.typ),k.verlierbar,komSorte(k.typ),e=>komKanteMenue(id,"von",k.von,k.nach,e),[k.von,k.nach]);});
    d.ausgaenge.forEach(a=>a.knoten.forEach(n=>{const k=d.knoten.find(x=>x.name===n)||{aus:[]};
      linie(punkt(el(n),"aus",k.aus.find(t=>komBasis(t)===komBasis(a.typ))||a.typ),punkt(rechts,"ein",a.typ),false,a.sorte,null,[n]);}));}
  // „＋ Quelle“: eine Quelle aus dem Katalog des Frameworks (Dateiwächter, Taktgeber, Webhook) oder eine eigene Funktion, die lauscht.
  function komQuellePlus(id,g,ev){const bk=komBaukasten();
    menueFix(ev.clientX,ev.clientY,"Quelle an den Kopf von "+g.name,[{kopf:"Katalog des Frameworks"},...bk.katalog.map(k=>({t:"⚡ "+k.name+" → "+(k.meldet||[]).join(", "),fn:()=>neuerGraphKnoten(g,k.name)})),
      ...(bk.funktionen.some(f=>f.quelle)?[{kopf:"eigene"},...bk.funktionen.filter(f=>f.quelle).map(f=>({t:"⚡ "+f.name,fn:()=>neuerGraphKnoten(g,f.name)}))]:[])]);}
  // „＋“ an einem Ausgang am rechten Rand: der Kopf eines anderen Graphen nimmt ihn (Verbindung, eigene Instanz) — ein vorhandener Knoten wird Kopf,
  //   oder ein neuer Graph entsteht mit einer Funktion, die ihn nimmt.
  function komEmpfaengerPlus(id,g,typ,ev){const items=[],t=komBasis(typ);
    (MODEL.graphen||[]).filter(x=>x!==g).forEach(x=>(x.knoten||[]).forEach(k=>{const f=MODEL.bausteinFunktionen.find(y=>y.name===komBasis(k.typ));
      if(f&&(f.eingaenge||[]).some(e=>komBasis(e.eingang)===t))items.push({t:"⬡ "+x.name+" · "+k.name+(k.kopf?" (schon Kopf)":" → [Kopf]"),
        fn:()=>{if(k.kopf)return;k.kopf=true;gTitel(x.name+": "+k.name+" ist Kopf für "+t);render();}});}));
    const nimmt=MODEL.bausteinFunktionen.filter(f=>(f.eingaenge||[]).some(e=>komBasis(e.eingang)===t));
    items.push({trenn:1},{t:"＋ neuer Graph mit Kopf für "+t+" …",fn:()=>frageFix(ev.clientX,ev.clientY,"⬡ Neuer Graph",t.replace(/(Zu|Abzu)\w+$/,"")+"Ablauf",
      "Ein Graph, dessen Kopf "+t+" nimmt — mit einer vorhandenen oder neuen Funktion",n=>{const fe=nameFehler(n);if(fe)return fe;
        if((MODEL.graphen||[]).some(x=>x.name===n))return n+" gibt es schon";
        const mit=fn=>{(MODEL.graphen=MODEL.graphen||[]).push({_id:"graph:"+n,name:n,namespace:g.namespace,knoten:[{typ:fn,name:fn,kopf:true,von:[],verlierbar:[]}]});
          gTitel("⬡ "+n+" mit Kopf "+fn);render();};
        setTimeout(()=>menueFix(ev.clientX,ev.clientY,"Kopf von "+n,[...nimmt.map(f=>({t:"ƒ "+f.name,fn:()=>mit(f.name)})),
          {t:"＋ neue Funktion …",fn:()=>frageFix(ev.clientX,ev.clientY,"ƒ Kopf von "+n,"Verarbeite"+t,"Nimmt "+t,fn=>{const fe2=nameFehler(fn);if(fe2&&!/gibt es schon/.test(fe2))return fe2;
            if(MODEL.bausteinFunktionen.some(x=>x.name===fn))return fn+" gibt es schon";
            MODEL.bausteinFunktionen.push({_id:"funktion:"+fn,name:fn,namespace:g.namespace,klasse:true,eingaenge:[{eingang:typ,ausgaben:[]}],konstruktor:[],umsetzungen:[]});mit(fn);})}]),0);})});
    menueFix(ev.clientX,ev.clientY,"Wer nimmt "+t+"?",items);}
  // ✕ an einem Knoten: aus dem Graphen — und, steht die Funktion in keinem anderen Knoten, auf Wunsch samt Funktion.
  function komKnotenLoeschen(id,k,ev){const g=komModell(id),b=komBasis(k.typ);
    if(k.art==="uebersetzung"){menueFix(ev.clientX,ev.clientY,"✕ "+k.name,[{t:"✕ Übersetzung "+k.name+" aus dem Graphen",gefahr:1,fn:()=>komKnotenEntfernen(id,k.name)}]);return;}
    const knoten=(MODEL.graphen||[]).flatMap(x=>(x.knoten||[]).filter(n=>komBasis(n.typ)===b)),f=k.karte&&NODEBY.get(k.karte);
    const items=[{t:"✕ "+k.name+" aus dem Graphen (die Funktion bleibt)",gefahr:1,fn:()=>komKnotenEntfernen(id,k.name)}];
    if(f&&k.art==="funktion"&&knoten.length===1)items.push({t:"✕ samt Funktion "+b+" (steht in keinem anderen Knoten)",gefahr:1,
      fn:()=>{komKnotenEntfernen(id,k.name);delNode(f);gTitel(g.name+": "+b+" gelöscht");render();}});
    else if(knoten.length>1)items.push({hinweis:b+" steht noch in "+(knoten.length-1)+" anderem Knoten — die Funktion bleibt."});
    menueFix(ev.clientX,ev.clientY,"✕ "+k.name,items);}

  // ── Baukasten: woraus man komponiert — „hier“ markiert, was dieser Graph schon nutzt; „＋“ setzt einen Baustein als Knoten ein ──
  function komBaukastenEl(id,d){const bk=komBaukasten(),box=h("div",{class:"kom-bk"},h("div",{class:"kom-t"},"Baukasten"),
      h("div",{class:"kom-u"},"Klick: öffnen · ＋: als Knoten einsetzen"));
    const hierFb=new Set(d.knoten.map(k=>k.karte).filter(Boolean)),hierTyp=new Set(d.kanten.map(k=>komBasis(k.typ)).concat(d.ausgaenge.map(a=>komBasis(a.typ))));
    const g=komModell(id);
    const einsetzen=(gr,x,e)=>{if(!g)return;const name=x.name||x.typ;
      if((x.typargumente||[]).length){menueFix(e.clientX,e.clientY,name+"‹…› — mit welchem Typargument?",x.typargumente.map(t=>({t:name+"<"+t+">",fn:()=>neuerGraphKnoten(g,name+"<"+t+">")})));return;}
      neuerGraphKnoten(g,name);};
    const gruppe=(titel,liste,name,hier,sym,gr,einsetzbar)=>{box.append(h("div",{class:"kom-sec"},titel));
      if(!liste.length){box.append(h("div",{class:"kom-wo"},"—"));return;}
      liste.forEach(x=>{const w=KOM.wahl&&KOM.wahl.art==="baustein"&&KOM.wahl.gruppe===gr&&KOM.wahl.name===name(x);
        const b=h("button",{class:"kom-b"+(w?" on":"")+(hier(x)?" hier":""),title:"Klick: öffnen",
          onclick:()=>komBausteinOeffnen(gr,x)},sym+" "+name(x),hier(x)?h("span",{class:"kom-hier"}," · hier"):null);
        box.append(einsetzbar&&g?h("div",{class:"kom-bz"},b,h("button",{class:"ag-plus",title:"Als Knoten in "+g.name+" einsetzen",onclick:e=>{e.stopPropagation();einsetzen(gr,x,e);}},"＋")):b);});};
    box.append(h("div",{class:"kom-akte"},h("button",{class:"kom-akt",title:"Einen Funktionskatalog anlegen: ein eigenes Projekt <Name>.Katalog, domänenneutral",
      onclick:e=>katalogNeu(e)},"＋ 📚 Katalog"),h("button",{class:"kom-akt",title:"Die Kataloge: Funktionen mit Input und Outputs, Command-Listen",onclick:()=>katOeffne(null,null)},"📚 Kataloge")));
    const fname=x=>x.name+((x.typargumente||[]).length?"‹…›":"");
    gruppe("Funktionen der Anwendung",bk.funktionen.filter(x=>!x.katalog),fname,x=>hierFb.has(x.karte),"ƒ","funktionen",true);
    (bk.kataloge||[]).forEach(k=>{gruppe("📚 "+k,bk.funktionen.filter(x=>x.katalog===k),fname,x=>hierFb.has(x.karte),"ƒ","funktionen",true);
      box.append(h("button",{class:"kom-b",title:"Eine neue Funktion in "+k+" (Bereich und Name)",onclick:e=>katalogFunktion(k,e,fb=>komKarte("fb:"+fb._id,true))},"＋ Funktion"));});
    gruppe("Graphen (schachteln)",bk.graphen.filter(x=>x.karte!==id&&!(d.eltern||[]).includes(x.karte)),x=>x.name,x=>hierFb.has(x.karte),"⬡","graphen",true);
    gruppe("Katalog (Quellen)",bk.katalog,x=>x.name,x=>d.knoten.some(k=>komBasis(k.typ)===x.name),"⚡","katalog",!d.koepfe.length&&!d.knoten.some(k=>k.quelle));
    gruppe("Technik",bk.technik,x=>x.name,x=>d.knoten.some(k=>(k.konstruktor||[]).some(c=>c.karte===x.karte)),"⚙","technik",false);
    gruppe("Konfig",bk.konfig,x=>x.name,x=>d.knoten.some(k=>(k.konstruktor||[]).some(c=>c.typ===x.name)),"▤","konfig",false);
    gruppe("Nachrichten im Graphen",bk.nachrichten,x=>x.typ,x=>hierTyp.has(x.typ),"✦","nachrichten",false);
    return box;}

  // ── Reiter Instanzen: je graphen/<instanz>.json des Graphen eine Zeile; bearbeitet wird im Panel ──
  function komInstanzenAnsicht(d,inst){const box=h("div",{class:"kom-inst"});
    box.append(h("div",{class:"kom-u"},"Instanzen von ⬡ "+d.name+" in der Umgebung „"+(WB.umgName||"?")+"“ (graphen/<instanz>.json)."));
    if(!inst.length)box.append(h("div",{class:"kom-wo"},d.eltern.length?"Läuft als Teil von "+d.eltern.map(g=>(komDaten(g)||{}).name||g).join(", ")+" — Instanzen dort.":"Noch keine Instanz."));
    inst.forEach(i=>{const v=(WB.instanzen||{})[i]||{};
      box.append(h("div",{class:"kom-inst-z"},h("b",{},"⬡ "+i),v.Akteur?" · 👤 "+v.Akteur:"",v.Rechner?" · auf "+v.Rechner:"",
        h("button",{class:"kom-akt",onclick:()=>komKarte("host:inst:"+i,true,true)},"✎ bearbeiten")));});
    if(!d.eltern.length)box.append(h("div",{class:"kom-akte"},h("button",{class:"kom-akt",onclick:()=>komKarte("host:inst-neu",true,true)},"＋ Instanz anlegen")));
    return box;}
  // Eine Karte im Modal: Graph, Aggregat und Store von innen; ein Glied (Decide, Apply, Auf, Store-Funktion, Projektion, Reader, eine Nachricht
  //   des Aggregats) in seinem Block gewählt; alles andere (Akteur, Client, Instanz, Umgebung, Funktion …) als Formular. push: an den Pfad hängen.
  function komAnsicht(id,alsFormular){if(alsFormular)return {pfad:"form:"+id,wahl:null};
    if(id.startsWith("gr:")||id.startsWith("agg:")||id.startsWith("sto:"))return {pfad:id,wahl:null};
    if(id.startsWith("fb:")&&funModell(id)&&!istGerufen(funModell(id)))return {pfad:id,wahl:null};
    const gruppe=x=>{const k=FL().karten.find(f=>f.karte===x&&f.gruppe);return k?k.gruppe:null;};
    if(gruppe(id))return {pfad:gruppe(id),wahl:{art:"glied",id}};
    const bes=SICHT().besitz||{};let o=id;
    for(let i=0;i<6;i++){o=(bes[o]||"").replace(/!$/,"");if(!o||o.startsWith("§"))break;
      if(o.startsWith("agg:")||o.startsWith("sto:"))return {pfad:o,wahl:{art:"glied",id}};
      if(gruppe(o))return {pfad:gruppe(o),wahl:{art:"glied",id}};}
    return {pfad:"form:"+id,wahl:null};}
  function komKarte(id,push,alsFormular){if(!NODEBY.has(id)&&!komDaten(id))return;const a=komAnsicht(id,alsFormular);
    if(push&&KOM)KOM.pfad.push(a.pfad);else KOM={pfad:[a.pfad],reiter:"komposition",wahl:null};KOM.wahl=a.wahl;komZeige();}
  // Das Formular einer Karte im Modal (dasselbe wie früher im Panel neben der Fläche), dazu woher und wohin.
  function formZeige(de,id){const n=NODEBY.get(id);if(!n){KOM.pfad.pop();if(KOM.pfad.length)komZeige();else KOM=null;return;}
    const m=komRahmen((NODELABEL[n.kind]||n.kind)+" bearbeiten — jede Änderung ist eine Geste am Code (↶ nimmt sie zurück)");
    const p=h("div",{class:"kom-panel kom-form"});
    p.append(h("div",{class:"kom-t"},(NODELABEL[n.kind]||n.kind)+(n.name?" · "+n.name:"")));
    const dg=diagFuer(id);if(dg.length)p.append(h("div",{class:"gi-diag"},...dg.map(d=>h("div",{class:"find "+(d.art==="warnung"?"warning":"error")},d.text))));
    const b=h("div",{class:"gbody"});INSP=true;try{fuelleKoerper(b,n);}finally{INSP=false;}p.append(b);
    // Die Rümpfe der Karte als Code-Kacheln mit ✎ und 🤖 — nur, wo das Formular sie nicht schon selbst zeigt (Decide, Apply, Auf zeigen ihren).
    if(!b.querySelector(".rumpfport"))[...NODEBY.values()].filter(r=>r.kind==="rumpf"&&r.own&&r.own.id===id).forEach(r=>{const rb=h("div",{class:"gbody"});
      INSP=true;try{fuelleKoerper(rb,r);}finally{INSP=false;}p.append(h("div",{class:"kom-sec"},"Rumpf · "+(r.name||"")),rb);});
    const innen=komAnsicht(id,false);
    if(innen.pfad!=="form:"+id)p.append(h("div",{class:"kom-akte"},h("button",{class:"kom-akt",onclick:()=>komKarte(id,true)},"⧉ von innen: "+komKrumenText(innen.pfad))));
    p.append(komNachbarn(id));
    m.append(h("div",{class:"kom-leib form"},p));de.append(m);}
  function komNachbarn(id){const rel=h("div",{class:"gi-rel"});
    const ids=l=>[...new Set((l||[]).flatMap(y=>VERTRETER.get(y)||[y]))].filter(x=>x!==id&&NODEBY.has(x));
    const liste=(t,l)=>{if(!l.length)return;rel.append(h("h5",{},t));l.forEach(x=>{const k=NODEBY.get(x);
      rel.append(h("a",{title:(NODELABEL[k.kind]||k.kind)+" — öffnen",onclick:()=>komKarte(x,true)},
        h("i",{class:"kc-"+k.kind,style:"display:inline-block;width:7px;height:7px;border-radius:50%;margin-right:5px"}),k.name||NODELABEL[k.kind]||k.kind));});};
    liste("◀ kommt von",ids(ADJ.inn.get(id)));liste("geht an ▶",ids(ADJ.out.get(id)));return rel;}
  // ══ INNENANSICHT eines Aggregats bzw. Stores (docs/10 §10.9): eine Mechanik für beide (innenZeige) — Karten mit Ports, drei Detailstufen,
  //   Paare auf gleicher Höhe, Sortierung, Anlegen ohne Zuordnung. Aggregat: Commands und Queries → Decide (und Fragen) → Ergebnisse → Apply →
  //   Zustand; wer dazugehört, sagt der Besitz (board.sicht.besitz), die Kanten sind die der Sicht. Store: zwei Bänder (Schreiben, Lesen), je Band
  //   Eingang → Auf bzw. Frage → Fähigkeit → Umsetzung, dazu die Read-Models und die Antworten; gerechnet aus dem Modell (stoInnen).
  const INNEN_SPALTE={decider:1,aggregate:1,applier:3,state:4};
  function blockInnen(id){const S=SICHT(),bes=S.besitz||{},art=x=>(NODEBY.get(x)||{}).kind;
    const gehoert=x=>{let o=x;for(let i=0;i<6;i++){if(o===id&&o!==x)return true;o=(bes[o]||"").replace(/!$/,"");if(!o||o.startsWith("§"))return false;}return false;};
    const glieder=new Map();
    NODEBY.forEach((n,x)=>{if(INNEN_SPALTE[n.kind]!=null&&(gehoert(x)||(x===id&&(n.ref.fragen||[]).length)))glieder.set(x,INNEN_SPALTE[n.kind]);});
    // Kanten der Sicht zwischen Gliedern und den Nachrichten an ihnen; Strukturkanten (Glied → Besitzer) fallen weg, Apply faltet in den Zustand.
    const st=[...glieder.keys()].find(x=>art(x)==="state"),kanten=[],nachrichten=new Map(),erzeugt=new Set();
    (S.kanten||[]).forEach(([a,b])=>{
      if(b===id&&art(a)==="applier"&&st)b=st;else if(b===id&&(art(a)==="decider"||art(a)==="state"))return;
      const ga=glieder.has(a),gb=glieder.has(b);if(!ga&&!gb)return;
      if(ga&&gb){kanten.push([a,b]);return;}
      // Nur Nachrichten stehen an den Rändern — ein Wertobjekt aus einem Parameter gehört ins Formular, nicht auf die Leinwand.
      const r=ga?b:a;if(!r.startsWith("rec:")||["valueobject","konfig"].includes(art(r)))return;
      nachrichten.set(r,0);if(ga)erzeugt.add(r);kanten.push([a,b]);});
    // Was ein Glied erzeugt, steht hinter ihm (in der Mitte vor dem Apply); was nur hineinkommt, links. Ein Event des Aggregats steht immer
    //   bei den Ergebnissen — auch wenn bisher nur sein Apply es nimmt und noch kein Decide es ergibt.
    nachrichten.forEach((s,r)=>glieder.set(r,erzeugt.has(r)||art(r)==="event"?2:0));
    return {typ:"agg",glieder,kanten};}
  function innenTitel(n){const r=n.ref||{};
    switch(n.kind){case "decider":return ["Decide",r.command||n.name];case "applier":return ["Apply",r.event||n.name];case "state":return ["Zustand",n.name];
      case "aggregate":return ["Fragen",(r.fragen||[]).map(f=>f.query).join(", ")];
      case "handle":return [n.own&&n.own.kind==="reader"?"Frage":"Auf",(istBuendel(r)?buendelName(r):n.name||"")+" · "+((n.own&&(n.own.name||(n.own.ref||{}).name))||"")];
      case "fn":return [r.faehigkeit||"Fähigkeit",n.name];case "readmodel":return ["Read-Model",n.name];
      case "umsetzung":return ["Umsetzung",((n.st&&n.st.impl&&n.st.impl.name)||"neu")+"."+n.name];
      case "buendel":return ["⋈ Bündel",n.name];
      case "auf":return ["Auf",r.eingang||n.name];case "lausche":return ["Lausche","Quelle"];
      case "zustand":return ["Zustand",n.name];case "logger":return ["Logger",n.name];case "param":return ["Konstruktor",n.name];
      case "bausteinfunktion":return [istGerufen(r)?"⚙ Technik":"ƒ Funktion",n.name];
      default:return [NODELABEL[n.kind]||n.kind,n.name];}}
  function blockZeige(de,id){const n0=NODEBY.get(id);if(!n0){KOM=null;return;}
    if(id.startsWith("sto:")){stoZeige(de,id,n0.ref);return;}
    const A=MODEL.aggregate.find(a=>"agg:"+a.name===id);if(!A){KOM=null;return;}
    aggZeige(de,id,blockInnen(id),A);}
  function innenKante(svgK,A,B,sorte,dick,blass){const zurueck=B.x<A.x,c=Math.max(40,Math.abs(B.x-A.x)*0.45),p=document.createElementNS(SVGNS,"path");
    p.setAttribute("d",zurueck?"M"+A.x+","+A.y+" C"+(A.x+80)+","+A.y+" "+(B.x-80)+","+B.y+" "+B.x+","+B.y:"M"+A.x+","+A.y+" C"+(A.x+c)+","+A.y+" "+(B.x-c)+","+B.y+" "+B.x+","+B.y);
    p.setAttribute("fill","none");p.setAttribute("stroke",sorte==="event"?"#57b673":sorte==="hinweis"?"#cf6f68":sorte==="query"||sorte==="antwort"?"#9678d6":"#8fa6c8");
    p.setAttribute("stroke-width",dick?"3":"1.6");if(blass)p.setAttribute("opacity",".15");svgK.append(p);}

  // ── Aggregat von innen: Karten mit Ports — links die Eingänge, rechts die Ausgänge. Verbunden wird nie von Hand: „＋“ an einem Port hebt die
  //   Karten hervor, die dort angeschlossen werden können (alles andere tritt zurück), ein Klick auf eine von ihnen verbindet (Esc bricht ab); ✕ an
  //   einem Port löst. Je Karte drei Stufen: nur Kopf · Kopf mit Ports (Standard) · Bearbeiten — Doppelklick schaltet Kopf ⇄ Ports, ✎ das Bearbeiten. ──
  // Die Ports einer Karte: {key, label, sorte, loese (✕), plus (＋, startet die Auswahl bzw. legt an)}.
  function aggPorts(A,n,x){const ein=[],aus=[],agg=A.name,decs=MODEL.decider.filter(d=>d.aggregat===agg);
    const s=t=>komSorte(t);
    if(n.kind==="decider"){const d=n.ref;ein.push({key:"cmd",label:d.command,sorte:"command"});
      (d.ergibt||[]).forEach(o=>aus.push({key:"r:"+o.event,label:o.event,sorte:s(o.event),loese:()=>{d.ergibt=d.ergibt.filter(z=>z.event!==o.event);gTitel("▣ "+agg+": "+d.command+" ergibt nicht mehr "+o.event);render();}}));
      aus.push({key:"+",label:"＋ Ergebnis",plus:{art:"ergebnis",dec:d}});}
    else if(n.kind==="applier"){ein.push({key:"evt",label:n.ref.event,sorte:"event"});aus.push({key:"st",label:"▶ Zustand"});}
    else if(n.kind==="state")ein.push({key:"in",label:"◀ von Apply"});
    else if(n.kind==="aggregate")(A.fragen||[]).forEach(f=>{ein.push({key:"q:"+f.query,label:f.query,sorte:"query"});
      (f.antworten||[]).forEach(a=>aus.push({key:"a:"+a,label:a,sorte:"antwort",loese:()=>{f.antworten=ohneX(f.antworten,a);gTitel("▣ "+agg+": "+f.query+" antwortet nicht mehr "+a);render();}}));
      aus.push({key:"+:"+f.query,label:"＋ Antwort zu "+f.query,plus:{art:"antwort",frage:f}});});
    else if(x.startsWith("rec:")){const r=n.ref||{},nm=n.name;
      if(r.kind==="command"&&r.aggregat===agg){aus.push(decs.some(d=>d.command===nm)?{key:"aus",label:"▶ Decide",sorte:"command"}
        :{key:"aus",label:"＋ Decide",plus:{art:"eigenesDecide",rec:nm}});
        // Wer ihn schickt: die Graphen, die ihn ausgeben (über eine Übersetzung oder eine Funktion), und die Akteure, die ihn dürfen.
        (MODEL.graphen||[]).filter(g=>(g.ausgaenge||[]).some(t=>komBasis(t)===nm)).forEach(g=>ein.push({key:"gr:"+g.name,label:"◀ ⬡ "+g.name,sorte:"command"}));
        MODEL.akteure.filter(a=>(a.darf||[]).includes(nm)).forEach(a=>ein.push({key:"ak:"+a.name,label:"◀ 👤 "+a.name,sorte:"command"}));
        ein.push({key:"+graph",label:"＋ Graph, der ihn schickt",plus:{art:"graphSchickt",rec:nm},ein:true});}
      else if(r.kind==="query")aus.push({key:"aus",label:"▶ Frage",sorte:"query"});
      else if(r.kind==="queryresponse"){(A.fragen||[]).filter(f=>(f.antworten||[]).includes(nm)).forEach(f=>ein.push({key:"von:"+f.query,label:"◀ "+f.query,sorte:"antwort",
          loese:()=>{f.antworten=ohneX(f.antworten,nm);gTitel("▣ "+agg+": "+f.query+" antwortet nicht mehr "+nm);render();}}));
        ein.push({key:"+",label:"＋ Frage",plus:{art:"frage",rec:nm}});}
      else{decs.filter(d=>(d.ergibt||[]).some(o=>o.event===nm)).forEach(d=>ein.push({key:"von:"+d.command,label:"◀ "+d.command,sorte:s(nm),
          loese:()=>{d.ergibt=d.ergibt.filter(z=>z.event!==nm);gTitel("▣ "+agg+": "+d.command+" ergibt nicht mehr "+nm);render();}}));
        ein.push({key:"+",label:"＋ Decide",plus:{art:"decide",rec:nm},ein:true});
        if(r.kind==="event"){const ap=MODEL.applier.some(a=>a.aggregat===agg&&a.event===nm);
          aus.push(ap?{key:"apply",label:"▶ Apply",sorte:"event"}:{key:"apply",label:"＋ Apply",plus:{art:"apply",rec:nm}});}}}
    return {ein,aus};}
  // Welcher Port an welcher Kante hängt (Kanten der Sicht zwischen Gliedern).
  function aggKantenPorts(A,a,b){const na=NODEBY.get(a),nb=NODEBY.get(b);if(!na||!nb)return null;
    if(nb.kind==="decider")return ["aus","cmd"];
    if(nb.kind==="aggregate")return ["aus","q:"+na.name];
    if(na.kind==="decider")return ["r:"+nb.name,"von:"+na.ref.command];
    if(na.kind==="aggregate"){const f=(A.fragen||[]).find(f=>(f.antworten||[]).includes(nb.name));return ["a:"+nb.name,"von:"+(f?f.query:"")];}
    if(nb.kind==="applier")return ["apply","evt"];
    if(na.kind==="applier")return ["st","in"];
    return null;}
  // Wer an einen „＋“ angeschlossen werden kann (vorhandene Karten im Bild; Neues und Vorhandenes von außerhalb: aggGeister).
  function aggAuswahl(A,I,p){const k=new Set();if(!p)return k;
    I.glieder.forEach((s,x)=>{const n=NODEBY.get(x);if(!n)return;
      if(p.art==="ergebnis"&&x.startsWith("rec:")&&s===2&&["event","rejection"].includes(n.kind)&&!(p.dec.ergibt||[]).some(o=>o.event===n.name))k.add(x);
      if(p.art==="decide"&&n.kind==="decider"&&!(n.ref.ergibt||[]).some(o=>o.event===p.rec))k.add(x);
      if(p.art==="antwort"&&n.kind==="queryresponse"&&!(p.frage.antworten||[]).includes(n.name))k.add(x);
      if(p.art==="frage"&&x.startsWith("rec:")&&n.kind==="query"&&(A.fragen||[]).some(f=>f.query===n.name&&!(f.antworten||[]).includes(p.rec)))k.add(x);});
    return k;}
  function aggAnschliessen(A,p,x){const n=NODEBY.get(x);if(!n)return;
    if(p.art==="ergebnis"){p.dec.ergibt=[...(p.dec.ergibt||[]),{event:n.name}];aggApplySichern(A,n.name);gTitel("▣ "+A.name+": "+p.dec.command+" ergibt "+n.name);}
    else if(p.art==="decide"){n.ref.ergibt=[...(n.ref.ergibt||[]),{event:p.rec}];aggApplySichern(A,p.rec);gTitel("▣ "+A.name+": "+n.ref.command+" ergibt "+p.rec);}
    else if(p.art==="antwort"){p.frage.antworten=[...(p.frage.antworten||[]),n.name];gTitel("▣ "+A.name+": "+p.frage.query+" antwortet "+n.name);}
    else if(p.art==="frage"){const f=(A.fragen||[]).find(f=>f.query===n.name);if(f){f.antworten=[...(f.antworten||[]),p.rec];gTitel("▣ "+A.name+": "+f.query+" antwortet "+p.rec);}}
    KOM.port=null;render();}
  // Ein Event, das ein Decide des Aggregats ergibt, braucht sein Apply (CQRS079) — es entsteht mit dem Anschließen (Platzhalter-Rumpf).
  function aggApplySichern(A,nm){const r=recNamens(nm);if(!r||r.kind!=="event"||MODEL.applier.some(a=>a.aggregat===A.name&&a.event===nm))return;
    MODEL.applier.push({_id:"a"+(NID++),aggregat:A.name,event:nm});}
  // Hat eine Nachricht schon einen Erzeuger (Decide, Funktion, Frage, Reader)? Ohne ihn kann ein Decide bzw. eine Frage sie übernehmen.
  const aggHatErzeuger=nm=>MODEL.decider.some(d=>(d.ergibt||[]).some(o=>o.event===nm))
    ||MODEL.bausteinFunktionen.some(f=>(f.eingaenge||[]).some(e=>(e.ausgaben||[]).includes(nm)))
    ||MODEL.aggregate.some(a=>(a.fragen||[]).some(f=>(f.antworten||[]).includes(nm)))||MODEL.reader.some(r=>(r.handles||[]).some(x=>(x.responses||[]).includes(nm)));
  // Beim Anschließen steht, was es noch nicht gibt bzw. was nicht im Bild ist, als gestrichelte Karte in seiner Spalte: Neues anlegen (Name
  //   abfragen) oder ein Event bzw. einen Hinweis ohne Erzeuger von außerhalb übernehmen — die Auswahl bleibt nicht aufs Sichtbare beschränkt.
  //   (Eine fremde Antwort nicht: sie liegt bei der Leseseite, die Antwort einer Frage an ein Aggregat bei der Schreibseite.)
  function aggGeister(A,I,p){if(!p)return [];const g=[],t="▣ "+A.name,drin=nm=>I.glieder.has("rec:"+nm);
    const neu=(spalte,titel,u,vorschlag,anlegen)=>g.push({spalte,t:titel,u,fn:e=>frageFix(e.clientX,e.clientY,titel,vorschlag,u,n=>{const f=nameFehler(n);if(f)return f;
      KOM.port=null;anlegen(n);render();})});
    const uebernimm=(spalte,nm,sorte,verbinde)=>g.push({spalte,t:(FL_SORTE[sorte]||"")+nm,u:"vorhanden, noch ohne Erzeuger — Klick übernimmt",fn:()=>{KOM.port=null;verbinde();render();}});
    const rec=(n,kind)=>aggRecordNeu(A,n,kind);
    if(p.art==="ergebnis"){const d=p.dec,ergibt=n=>{d.ergibt=[...(d.ergibt||[]),{event:n}];aggApplySichern(A,n);gTitel(t+": "+d.command+" ergibt "+n);};
      neu(2,"＋ neues Event","Fakt im Log",A.name+"Geaendert",n=>{rec(n,"event");ergibt(n);});
      neu(2,"＋ neuer Hinweis","flüchtig, an Akteure",d.command+"Abgelehnt",n=>{rec(n,"rejection");ergibt(n);});
      MODEL.decider.filter(x=>x.aggregat===A.name&&x.command).map(x=>"Frist<"+x.command+">").filter(f=>!(d.ergibt||[]).some(o=>o.event===f))
        .forEach(f=>g.push({spalte:2,t:"⏱ "+f,u:"Frist auf einen eigenen Command",fn:()=>{KOM.port=null;ergibt(f);render();}}));
      MODEL.records.filter(r=>(r.kind==="event"||r.kind==="rejection")&&!drin(r.name)&&!aggHatErzeuger(r.name)).slice(0,12)
        .forEach(r=>uebernimm(2,r.name,komSorte(r.name),()=>ergibt(r.name)));}
    else if(p.art==="decide")neu(1,"＋ neuer Command mit Decide","Command an "+A.name+" — sein Decide ergibt "+p.rec,"",n=>aggCommandAnlegen(A,n,[{event:p.rec}]));
    else if(p.art==="antwort"){const f=p.frage,antw=n=>{f.antworten=[...(f.antworten||[]),n];gTitel(t+": "+f.query+" antwortet "+n);};
      neu(2,"＋ neue Antwort","Antwort auf "+f.query,f.query.replace(/^Hole/,"")+"Antwort",n=>{rec(n,"queryresponse");antw(n);});}
    else if(p.art==="frage")neu(0,"＋ neue Query","Frage an "+A.name+" — antwortet "+p.rec,"Hole"+A.name+"Stand",n=>aggQueryAnlegen(A,n,[p.rec]));
    return g;}
  // „＋“ an einem Port: Apply bzw. Decide eines eigenen Commands legt direkt an; sonst heben sich die anschließbaren Karten hervor (dazu die
  //   gestrichelten für Neues), ein zweiter Klick auf denselben „＋“ bricht ab.
  function aggPlus(A,I,p,x){
    if(p.art==="apply"){MODEL.applier.push({_id:"a"+(NID++),aggregat:A.name,event:p.rec});gTitel("▣ "+A.name+": Apply "+p.rec);render();return;}
    if(p.art==="eigenesDecide"){MODEL.decider.push({_id:"d"+(NID++),aggregat:A.name,command:p.rec,ergibt:[]});gTitel("▣ "+A.name+": Decide "+p.rec);render();return;}
    if(p.art==="graphSchickt"){aggGraphSchickt(A,p.rec);return;}
    innenPortWahl(p,x);}
  // Ein Port einer Karte; „＋“ ruft die Geste des Blocks (C.plus).
  const portZiel=p=>p&&(p.dec||p.frage||p.rec||p.hd||p.fn);
  // ══ LÖSCHEN im Modal (Aggregat und Store von innen): ✕ im Kopf jeder Karte, die man hier anlegen kann. Ein Löschplan sagt, was mitgeht
  //   (folgen) oder warum es nicht geht (grund: wo es außerhalb noch verwendet wird); nach dem Schreiben meldet der Editor neue Build-Befunde. ══
  // Wo ein Typ verwendet wird — je Stelle Art, Text und Besitzer (Aggregat bzw. Projektion/Reader/Store), damit ein Plan entscheidet, was mitgeht.
  function verwendungen(nm){const v=[],P=(art,wo,o)=>v.push({art,wo,...(o||{})});
    MODEL.decider.forEach(d=>{if(d.command===nm)P("decide","Decide("+nm+") in ▣ "+d.aggregat,{agg:d.aggregat,ref:d});
      if((d.ergibt||[]).some(o=>o.event===nm))P("ergebnis","Ergebnis von Decide("+d.command+") in ▣ "+d.aggregat,{agg:d.aggregat,ref:d});});
    MODEL.applier.forEach(a=>{if(a.event===nm)P("apply","Apply("+nm+") in ▣ "+a.aggregat,{agg:a.aggregat,ref:a});});
    MODEL.aggregate.forEach(A=>(A.fragen||[]).forEach(f=>{if(f.query===nm)P("frage","Frage("+nm+") in ▣ "+A.name,{agg:A.name});
      if((f.antworten||[]).includes(nm))P("antwort","Antwort der Frage("+f.query+") in ▣ "+A.name,{agg:A.name});}));
    MODEL.projektionen.forEach(o=>(o.handles||[]).forEach(hd=>{if(hdEvents(hd).includes(nm))P("auf",(istBuendel(hd)?"Bündel "+buendelName(hd):"Auf("+nm+")")+" in Projektion "+o.name,{own:o,ref:hd});}));
    MODEL.reader.forEach(o=>(o.handles||[]).forEach(hd=>{if(hd.query===nm)P("rfrage","Frage("+nm+") in Reader "+o.name,{own:o,ref:hd});
      if((hd.responses||[]).includes(nm))P("rantwort","Antwort der Frage("+hd.query+") in Reader "+o.name,{own:o,ref:hd});}));
    MODEL.bausteinFunktionen.forEach(f=>{(f.eingaenge||[]).forEach(e=>{if(e.eingang===nm)P("funktion","Auf("+nm+") in Funktion "+f.name);
        if((e.ausgaben||[]).includes(nm))P("funktion","Ausgabe von "+f.name+".Auf("+e.eingang+")");});
      if((f.lauscht||[]).includes(nm))P("funktion","Quelle "+f.name);});
    MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(nm))P("darf","IDarf<"+nm+"> am Akteur "+a.name);if((a.hoert||[]).includes(nm))P("hoert","IHoert<"+nm+"> am Akteur "+a.name);});
    const typNennt=t=>new RegExp("(^|[^A-Za-z0-9_])"+nm+"($|[^A-Za-z0-9_])").test(t||"");
    MODEL.records.forEach(r=>{if(r.name!==nm&&(r.felder||[]).some(f=>typNennt(f.typ)))P("feld","Feld in "+r.name);});
    MODEL.readModels.forEach(r=>{if((r.felder||[]).some(f=>typNennt(f.typ)))P("feld","Feld in Read-Model "+r.name);});
    return v;}
  // Ein Löschplan für einen Record: was davon mitgeht (mit), darf mitgehen; alles andere sperrt mit seinen Stellen als Grund. IDarf/IHoert gehen immer mit.
  function loeschPlanRecord(nm,was,mit,tu){const v=verwendungen(nm),geht=u=>u.art==="darf"||u.art==="hoert"||mit(u),fremd=v.filter(u=>!geht(u));
    return {was,folgen:v.filter(geht).map(u=>u.wo),grund:fremd.length?"noch verwendet: "+fremd.map(u=>u.wo).join(" · ")+" — dort zuerst lösen":null,tu};}
  // Den Record selbst aus dem Modell nehmen (delNode nimmt ihn auch aus IDarf/IHoert der Akteure und der Kenntnis der Clients).
  function recordWeg(nm){const n=NODEBY.get("rec:"+nm),r=recNamens(nm);if(n)delNode(n);else if(r)MODEL.records.splice(MODEL.records.indexOf(r),1);}
  let G_LOESCH=null;   // die laufende Lösch-Geste: was gelöscht wurde und welche Befunde vorher bestanden
  const diagSchluessel=x=>(x.d&&x.d.code?x.d.code+"|"+x.d.text:x.text);
  function loeschKnopf(plan,x){if(!plan)return null;
    return h("button",{class:"ag-loesch"+(plan.grund?" gesperrt":""),
      title:plan.grund?"Nicht löschbar — "+plan.grund:"Löschen: "+plan.was+(plan.folgen.length?" (dazu: "+plan.folgen.join(" · ")+")":""),
      onclick:e=>{e.stopPropagation();if(KOM.port){KOM.port=null;komZeige();return;}
        if(plan.grund){deFlash("⚠ "+plan.was+" nicht gelöscht — "+plan.grund,false);
          menueFix(e.clientX,e.clientY,"✕ "+plan.was,[{hinweis:"Nicht löschbar — "+plan.grund}]);return;}
        menueFix(e.clientX,e.clientY,"✕ "+plan.was+" löschen?",[...plan.folgen.map(f=>({hinweis:"dazu: "+f})),
          {t:"✕ löschen",gefahr:1,fn:()=>{KOM.edit.delete(x);KOM.zu.delete(x);if(KOM.wahl&&KOM.wahl.id===x)KOM.wahl=null;
            G_LOESCH={was:plan.was,vorher:new Set(wbDiagListe().map(diagSchluessel))};gTitel("✕ "+plan.was);plan.tu();render();}}]);}},"✕");}
  // Nach dem Schreiben einer Lösch-Geste: neue Fehler im Build (ein Verweis, den der Editor nicht kannte) als Meldung — dazu stehen sie an ihrer Karte.
  function loeschBefunde(){if(!G_LOESCH)return;const L=G_LOESCH;G_LOESCH=null;
    const neu=wbDiagListe().filter(x=>x.art==="fehler"&&!L.vorher.has(diagSchluessel(x)));
    if(neu.length)deFlash("⚠ Nach dem Löschen von "+L.was+": "+neu.length+(neu.length===1?" neuer Fehler":" neue Fehler")+" im Build — "+((neu[0].d&&neu[0].d.text)||neu[0].text),false);}
  function innenPortZeile(C,p,richtung,x){const P=KOM.port,aktiv=!!p.plus&&!!P&&P.karte===x&&P.art===p.plus.art&&portZiel(P)===portZiel(p.plus);
    const r=h("div",{class:"fk-port "+richtung+" s-"+(p.sorte||"command")+(p.plus?" plus":"")+(aktiv?" aktiv":""),
      title:p.plus?(aktiv?"Klick: abbrechen":"Klick: anschließen bzw. anlegen"):p.loese?"✕ löst die Verbindung":""},h("span",{class:"fk-dot"}),h("span",{class:"fk-pl"},(p.plus?"":(FL_SORTE[p.sorte]||""))+p.label),
      p.loese?h("button",{class:"ag-los",title:"Verbindung lösen",onclick:e=>{e.stopPropagation();p.loese();}},"✕"):null);
    if(p.plus)r.onclick=e=>{e.stopPropagation();C.plus(p.plus,x,e);};
    r.dataset.port=richtung+"|"+p.key;return r;}
  // „＋“ an einem Port ohne eigene Geste: die anschließbaren Karten heben sich hervor (dazu die gestrichelten für Neues); ein zweiter Klick auf
  //   denselben „＋“ bricht ab.
  function innenPortWahl(p,x){const gleich=KOM.port&&KOM.port.karte===x&&KOM.port.art===p.art&&portZiel(KOM.port)===portZiel(p);
    KOM.port=gleich?null:{...p,karte:x};komZeige();}
  // Die Kette einer gewählten Karte: was vor ihr und was nach ihr kommt (je Richtung transitiv über die Kanten der Innenansicht).
  function innenKette(kanten,start){const k=new Set([start]);
    [[0,1],[1,0]].forEach(([a,b])=>{const offen=[start],da=new Set([start]);
      while(offen.length){const x=offen.pop();kanten.forEach(e=>{if(e[a]===x&&!da.has(e[b])){da.add(e[b]);k.add(e[b]);offen.push(e[b]);}});}});
    return k;}
  // Die Innenansicht eines Blocks. C sagt, was der Block ist: I (glieder: Id → Spalte, kanten), knoten(x), ports, kantenPorts, auswahl,
  //   anschliessen, geister, plus, kurz, bearbeiten, zeit/name/ein (Sortierung), band(x), fest(x) (kein ✎, dafür festKnopf), marke(x), fremd(x),
  //   unverbunden (+ Text), koepfe() je Spalte [Titel, ＋, Hinweis, führt-Wert], legen (Spalten, Bänder, freie Spalten, folgende Spalten) und
  //   ansicht (welche Seite führt, wonach sortiert, B = der führt-Wert der rechten Paare).
  function innenZeige(de,id,C){KOM.edit=KOM.edit||new Set();KOM.zu=KOM.zu||new Set();const I=C.I,kn=x=>C.knoten(x),unverbunden=C.unverbunden||new Set();
    const m=komRahmen(KOM.port?"Anschließen: eine hervorgehobene Karte anklicken — gestrichelt: neu anlegen bzw. übernehmen · Esc bricht ab"
        :"Klick: Kette hervorheben · Doppelklick: nur Kopf ⇄ Ports · ✎: bearbeiten · ＋ am Port: anschließen · ＋ im Spaltenkopf: neu anlegen");
    const alleZu=I.glieder.size>0&&[...I.glieder.keys()].every(x=>KOM.zu.has(x)),fuehrt=C.ansicht.fuehrt(),sortierung=C.ansicht.sortierung();
    m.querySelector(".kom-reiter").prepend(
      h("button",{title:"Alle Karten nur mit Kopf bzw. mit Ports",
        onclick:()=>{if(alleZu)KOM.zu.clear();else{I.glieder.forEach((s,x)=>KOM.zu.add(x));KOM.edit.clear();}komZeige();}},alleZu?"☰ alle mit Ports":"▭ alle nur Kopf"));
    const welt=h("div",{class:"kom-welt"}),svgK=document.createElementNS(SVGNS,"svg");svgK.setAttribute("class","kom-kanten");welt.append(svgK);
    const wahl=KOM.wahl&&KOM.wahl.art==="glied"&&I.glieder.has(KOM.wahl.id)?KOM.wahl.id:null;
    const auswahl=C.auswahl(KOM.port),kette=wahl&&!KOM.port?innenKette(I.kanten,wahl):null;
    // Erst die gestrichelten Karten (oben in ihrer Spalte), dann die Glieder.
    C.geister(KOM.port).forEach((g,i)=>{const el=h("div",{class:"gnode2 fk kom-k ag-neu kand",title:g.u||"",onclick:e=>{e.stopPropagation();g.fn(e);}},
        h("div",{class:"ghead"},h("span",{class:"gtitle"},h("span",{class:"gk"},g.t))));
      if(g.u)el.append(h("div",{class:"ag-kurz"},g.u));el.dataset.glied="neu:"+i;el.dataset.spalte=g.spalte;el.dataset.band=g.band||"";welt.append(el);});
    I.glieder.forEach((s,x)=>{const n=kn(x);if(!n)return;const [k,t]=innenTitel(n),rec=x.startsWith("rec:"),fest=!!(C.fest&&C.fest(x)),
        offen=!fest&&KOM.edit.has(x),zu=!offen&&KOM.zu.has(x),kand=auswahl.has(x);
      const leise=KOM.port?!kand&&x!==KOM.port.karte:!!kette&&!kette.has(x);
      const el=h("div",{class:"gnode2 fk kom-k n-"+n.kind+(wahl===x?" sel":"")+(offen?" offen":"")+(zu?" zu":"")+(kand?" kand":"")+(leise?" leise":"")+(C.fremd&&C.fremd(x)?" fremd":""),
        title:kand?"Klick: anschließen":"Klick: Kette hervorheben · Doppelklick: "+(zu?"Ports zeigen":"nur Kopf")+(fest?"":" · ✎: bearbeiten"),
        onclick:e=>{if(kand){C.anschliessen(KOM.port,x);return;}if(KOM.port){KOM.port=null;komZeige();return;}
          if(e.detail<=1)KOM.klick={wahl:KOM.wahl};komWaehle(wahl===x?null:{art:"glied",id:x});},
        // Dem Doppelklick gehen zwei Klicks voraus, die die Wahl umgeschaltet haben — er stellt sie wieder her, wie sie vor dem ersten war.
        ondblclick:e=>{e.stopPropagation();if(KOM.port)return;if(KOM.zu.has(x))KOM.zu.delete(x);else{KOM.zu.add(x);KOM.edit.delete(x);}
          if(KOM.klick)KOM.wahl=KOM.klick.wahl;KOM.klick=null;komZeige();}},
        h("div",{class:"ghead"},h("span",{class:"gtitle hatname",title:(NODELABEL[n.kind]||n.kind)+" · "+t},
          h("span",{class:"gk"},(rec?(FL_SORTE[komSorte(n.name)]||"")+n.name:k)+" "),h("span",{class:"gn"},rec?"":t)),
          unverbunden.has(x)?h("span",{class:"kom-mark",title:C.unverbundenText||""},"unverbunden"):null,
          C.marke?C.marke(x):null,
          kand?h("span",{class:"ag-stift"},"⊕"):fest?(C.festKnopf?C.festKnopf(n,x):null):h("button",{class:"ag-stift"+(offen?" an":""),title:offen?"Bearbeiten schließen":"Bearbeiten",
            onclick:e=>{e.stopPropagation();if(KOM.port){KOM.port=null;komZeige();return;}if(offen)KOM.edit.delete(x);else{KOM.edit.add(x);KOM.zu.delete(x);}komZeige();}},offen?"▾":"✎"),
          !kand&&!fest&&C.loeschen?loeschKnopf(C.loeschen(n,x),x):null));
      if(!zu){const P=C.ports(n,x),ports=h("div",{class:"fk-ports"});
        P.ein.forEach(p=>ports.append(innenPortZeile(C,p,"ein",x)));P.aus.forEach(p=>ports.append(innenPortZeile(C,p,"aus",x)));
        if(P.ein.length||P.aus.length)el.append(ports);
        el.append(C.kurz(n,x));}
      if(diagFuer(x).length)el.append(h("div",{class:"ag-diag",title:diagFuer(x).map(d=>d.text).join("\n")},zu?"⛔":"⛔ "+diagFuer(x).map(d=>d.text).join(" · ")));
      if(offen)el.append(C.bearbeiten(n,x));
      el.dataset.glied=x;el.dataset.spalte=s;el.dataset.band=C.band?C.band(x):"";el.dataset.zeit=C.zeit(x);el.dataset.name=C.name(x);el.dataset.ein=C.ein(x);welt.append(el);});
    const lw=h("div",{class:"kom-lw"},welt);m.append(h("div",{class:"kom-leib innen"},lw));de.append(m);
    const {xs,baender}=innenLegenPaare(welt,I.kanten,64,C.legen,fuehrt===C.ansicht.B,sortierung);
    baender.forEach(b=>{const l=h("div",{class:"kom-band"},b.titel);l.style.top=b.y+"px";welt.append(l);});
    C.koepfe().forEach(([titel,fn,u,sortiert],s)=>{const p=xs.get(s);if(!p)return;
      const kopf=h("div",{class:"ag-spkopf"},h("b",{},titel),
        sortiert?h("button",{class:"ag-sort"+(fuehrt===sortiert?" an":""),
          title:(fuehrt===sortiert?"Sortiert nach dieser Spalte: "+AGG_SORTIERUNG[sortierung][1]:"Nach dieser Spalte sortieren")+" — Klick: neu, A–Z oder Eingänge",
          onclick:e=>{e.stopPropagation();menueFix(e.clientX,e.clientY,"Sortieren nach "+titel,Object.entries(AGG_SORTIERUNG).map(([k,[txt,tt]])=>({
            t:(fuehrt===sortiert&&sortierung===k?"✓ ":"   ")+txt,title:tt,fn:()=>{C.ansicht.setze(sortiert,k);komZeige();}})));}},
          "⇅"+(fuehrt===sortiert?" "+AGG_SORTIERUNG[sortierung][0]:"")):null,
        fn?h("button",{class:"ag-plus",onclick:e=>{e.stopPropagation();fn(e);}},u):h("span",{class:"kom-u"}," "+u));
      kopf.style.left=p.x+"px";kopf.style.width=p.w+"px";welt.append(kopf);});
    const el=x=>welt.querySelector('.kom-k[data-glied="'+CSS.escape(x)+'"]');
    const punkt=(e,ri,key)=>{const p=e.querySelector('[data-port="'+CSS.escape(ri+"|"+key)+'"]');return {x:e.offsetLeft+(ri==="aus"?e.offsetWidth:0),y:e.offsetTop+(p?p.offsetTop+p.offsetHeight/2:14)};};
    I.kanten.forEach(([a,b])=>{const ea=el(a),eb=el(b),pk=C.kantenPorts(a,b);if(!ea||!eb||!pk)return;const an=!!kette&&kette.has(a)&&kette.has(b);
      innenKante(svgK,punkt(ea,"aus",pk[0]),punkt(eb,"ein",pk[1]),komSorte((kn(a.startsWith("rec:")?a:b)||{}).name),an,!!KOM.port||(!!kette&&!an));});}
  // Das Aggregat von innen: Commands und Queries · Decide und Fragen · Ergebnisse · Apply · Zustand; Command ↔ Decide und Event ↔ Apply als Paare.
  function aggZeige(de,id,I,A){const unverbunden=aggUnverbunden(A,I);unverbunden.forEach(x=>I.glieder.set(x,2));
    innenZeige(de,id,{I,knoten:x=>NODEBY.get(x),unverbunden,
      unverbundenText:"Liegt beim Aggregat, noch von keinem Decide bzw. keiner Frage genommen — anschließen über ＋ am Port",
      ports:(n,x)=>aggPorts(A,n,x),kantenPorts:(a,b)=>aggKantenPorts(A,a,b),auswahl:p=>aggAuswahl(A,I,p),anschliessen:(p,x)=>aggAnschliessen(A,p,x),
      geister:p=>aggGeister(A,I,p),plus:(p,x)=>aggPlus(A,I,p,x),kurz:(n,x)=>aggKurz(A,n,x),bearbeiten:(n,x)=>aggBearbeiten(A,n,x),
      loeschen:(n,x)=>aggLoeschPlan(A,n,x),
      zeit:aggGliedZeit,name:aggGliedName,ein:x=>aggEingaenge(A,I,x),koepfe:()=>aggSpaltenKoepfe(A,I,id),
      legen:{spalten:[0,1,2,3,4],baender:[""],frei:[4],folgen:[]},
      ansicht:{fuehrt:aggFuehrt,sortierung:aggSortierung,B:"event",
        setze:(f,s)=>{ANSICHT.aggFuehrt=f;ANSICHT.aggSortierung=s;delete ANSICHT.aggOrdnung;ansichtSpeichern();}}});}
  // Die Spaltenköpfe: „＋“ für alles, was frei entstehen kann — so lässt sich jedes Glied des Aggregats im Editor anlegen. Decide und Apply
  //   kommen 1:1 mit ihrem Command bzw. Event; fehlt eines im Bestand, legt es „＋“ am Port der Karte an.
  function aggSpaltenKoepfe(A,I,id){const t="▣ "+A.name,menue=(titel,items)=>ev=>menueFix(ev.clientX,ev.clientY,titel,items);
    const neuCmd={t:"＋ Command mit Decide …",fn:e=>aggNeuerCommand(A,e)},neuQuery={t:"＋ Query mit Frage und Antwort …",fn:e=>aggNeueQuery(A,e)};
    return [
      ["Commands",menue("Neu in "+A.name,[neuCmd,neuQuery]),"＋ ▾","command"],
      ["Decide",null,"je Command eines"],
      ["Ergebnisse",menue("Neu in "+A.name,[
        {t:"＋ Event (Fakt im Log) …",fn:e=>aggFreiAnlegen(A,"event","＋ Event",A.name+"Geaendert",e)},
        {t:"＋ Hinweis (flüchtig, an Akteure) …",fn:e=>aggFreiAnlegen(A,"rejection","＋ Hinweis",A.name+"Abgelehnt",e)},
        {t:"＋ Antwort …",fn:e=>aggFreiAnlegen(A,"queryresponse","＋ Antwort",A.name+"Antwort",e)},
        {trenn:1},{hinweis:"Steht unverbunden in der Spalte (ein Event gleich mit seinem Apply) — anschließen später über ＋ am Port (＋ Decide bzw. ＋ Frage)."}]),"＋ ▾","event"],
      ["Apply",null,"je Event eines"],
      ["Zustand",()=>{A.state=A.state||[];A.state.push({_id:"f"+(NID++),name:uniqFeldName(A.state,"Feld"),typ:"string"});const st=[...I.glieder.keys()].find(x=>x.startsWith("st:"));
        if(st){KOM.edit.add(st);KOM.zu.delete(st);}gTitel(t+": Zustand ＋ Feld");render();},"＋ Feld"]];}
  // Löschen im Aggregat: ein Record mit allem, was im Aggregat an ihm hängt (Decide, Ergebnis, Apply, Frage, Antwort); ein Decide mit seinem
  //   Command (1:1); ein Apply allein. Zustand und Fragen-Karte gehören dem Aggregat (die Felder haben ihr ✕).
  function aggLoeschPlan(A,n,x){const hier=u=>u.agg===A.name&&["decide","ergebnis","apply","frage","antwort"].includes(u.art);
    const recPlan=(nm,was)=>loeschPlanRecord(nm,was,hier,()=>{
      MODEL.decider.filter(d=>d.aggregat===A.name&&d.command===nm).forEach(d=>MODEL.decider.splice(MODEL.decider.indexOf(d),1));
      MODEL.decider.filter(d=>d.aggregat===A.name).forEach(d=>{d.ergibt=(d.ergibt||[]).filter(o=>o.event!==nm);});
      MODEL.applier.filter(a=>a.aggregat===A.name&&a.event===nm).forEach(a=>MODEL.applier.splice(MODEL.applier.indexOf(a),1));
      A.fragen=(A.fragen||[]).filter(f=>f.query!==nm);(A.fragen||[]).forEach(f=>{f.antworten=ohneX(f.antworten,nm);});
      recordWeg(nm);});
    if(x.startsWith("rec:")){const r=n.ref||{},art={command:r.aggregat===A.name?"Command":"Command ohne Ziel",event:"Event",rejection:"Hinweis",query:"Query",queryresponse:"Antwort"}[r.kind]||"Typ";
      return recPlan(r.name,art+" "+r.name);}
    if(n.kind==="decider"){const d=n.ref;
      return recNamens(d.command)?recPlan(d.command,"Command "+d.command+" samt Decide")
        :{was:"Decide("+d.command+")",folgen:[],grund:null,tu:()=>{MODEL.decider.splice(MODEL.decider.indexOf(d),1);}};}
    if(n.kind==="applier"){const a=n.ref;
      return {was:"Apply("+a.event+")",folgen:["das Event bleibt — ohne Apply warnt der Build (CQRS079)"],grund:null,tu:()=>{MODEL.applier.splice(MODEL.applier.indexOf(a),1);}};}
    return null;}
  // ── Aggregat von innen: anlegen und verbinden (docs/10 §10.9) ──
  const aggIdFeld=()=>ID_FELD()||"AggregateId";
  const recNamens=n=>MODEL.records.find(r=>r.name===n);
  // Name abfragen (über dem Modal): Eingabe, Enter legt an; ok liefert einen Fehlertext oder nichts.
  function frageFix(cx,cy,titel,vorschlag,hinweis,ok){document.querySelectorAll("#de .gmenue.fix").forEach(p=>p.remove());
    const m=h("div",{class:"gpick gmenue fix"}),inp=h("input",{value:vorschlag||"",spellcheck:"false"}),fehler=h("div",{class:"gm-fehler"});
    const los=()=>{const f=ok(inp.value.trim());if(f){fehler.textContent=f;return;}m.remove();};
    inp.onkeydown=e=>{e.stopPropagation();if(e.key==="Enter")los();if(e.key==="Escape")m.remove();};
    m.append(h("div",{class:"gpick-t"},titel),inp,hinweis?h("div",{class:"gm-hint"},hinweis):null,fehler,
      h("div",{style:"display:flex;gap:4px"},h("button",{onclick:los},"Anlegen"),h("button",{onclick:()=>m.remove()},"Abbrechen")));
    m.style.left=Math.max(4,Math.min(cx,innerWidth-300))+"px";m.style.top=Math.max(4,Math.min(cy,innerHeight-200))+"px";
    ["pointerdown","dblclick","wheel","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>e.stopPropagation()));
    document.getElementById("de").append(m);setTimeout(()=>{inp.focus();inp.select();},0);}
  const nameFehler=n=>!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(n)?"Ein Typname: groß beginnen, nur Buchstaben und Ziffern":recNamens(n)?n+" gibt es schon":null;
  // Ein Command an das Aggregat samt Decide (ergibt: die Glieder seines OneOf) bzw. eine Query samt Frage (antworten: ihre Antworten).
  function aggCommandAnlegen(A,n,ergibt){
    MODEL.records.push({name:n,kind:"command",namespace:A.namespace,felder:[{_id:"f"+(NID++),name:aggIdFeld(),typ:"Id<"+A.name+">"}],istErzeugung:false,aggregat:A.name});aggStempel(n);
    MODEL.decider.push({_id:"d"+(NID++),aggregat:A.name,command:n,ergibt:ergibt||[]});gTitel("▣ "+A.name+": Command "+n);}
  function aggQueryAnlegen(A,n,antworten){
    MODEL.records.push({name:n,kind:"query",namespace:A.namespace,felder:[{_id:"f"+(NID++),name:A.name+"Id",typ:"Id<"+A.name+">"}],istErzeugung:false});aggStempel(n);
    A.fragen=[...(A.fragen||[]),{query:n,antworten}];gTitel("▣ "+A.name+": Frage "+n);}
  function aggNeuerCommand(A,ev){frageFix(ev.clientX,ev.clientY,"＋ Command an "+A.name,"","Legt den Command und sein Decide an",n=>{const f=nameFehler(n);if(f)return f;
    aggCommandAnlegen(A,n,[]);render();});}
  function aggNeueQuery(A,ev){frageFix(ev.clientX,ev.clientY,"＋ Query an "+A.name,"Hole"+A.name+"Stand","Legt Query, Frage und eine Antwort an",n=>{const f=nameFehler(n);if(f)return f;
    const ant=n.replace(/^Hole/,"")+"Antwort";if(recNamens(ant))return ant+" gibt es schon";
    MODEL.records.push({name:ant,kind:"queryresponse",namespace:A.namespace,felder:[],istErzeugung:false});aggStempel(ant);aggQueryAnlegen(A,n,[ant]);render();});}
  // Eine neue Nachricht im Aggregat: sie liegt bei seiner Schreibseite (amAggregat), auch solange noch kein Decide bzw. keine Frage sie nimmt.
  function aggRecordNeu(A,n,kind){MODEL.records.push({name:n,kind,namespace:A.namespace,felder:[],istErzeugung:false,amAggregat:A.name});aggStempel(n);}
  // Im Spaltenkopf „Ergebnisse“ angelegt: steht unverbunden in der Spalte, angeschlossen wird später über ＋ am Port.
  function aggFreiAnlegen(A,kind,titel,vorschlag,ev){frageFix(ev.clientX,ev.clientY,titel+" in "+A.name,vorschlag,"Steht danach unverbunden in „Ergebnisse“",n=>{
    const f=nameFehler(n);if(f)return f;aggRecordNeu(A,n,kind);aggApplySichern(A,n);gTitel("▣ "+A.name+": "+n+" angelegt");render();});}
  // Unverbunden, aber beim Aggregat: eine Nachricht in seinem Namespace (WO sie liegt, W2) bzw. im Bild, die kein Decide ergibt und keine Frage
  //   beantwortet — so bleibt sie nach dem Anlegen in „Ergebnisse“, bis sie angeschlossen ist (ein Event hat sein Apply schon).
  function aggUnverbunden(A,I){return new Set(MODEL.records.filter(r=>["event","rejection","queryresponse"].includes(r.kind)
      &&NODEBY.has("rec:"+r.name)&&(I.glieder.has("rec:"+r.name)?I.glieder.get("rec:"+r.name)===2:r.namespace===A.namespace)&&!aggHatErzeuger(r.name)).map(r=>"rec:"+r.name));}
  // Zeitstempel beim Anlegen im Editor (in der Ansicht, nicht im Code); was keinen hat, zählt nach seiner Stelle im Modell — älter als alles Gestempelte.
  function aggStempel(n){(ANSICHT.angelegt=ANSICHT.angelegt||{})[n]=Date.now();ansichtSpeichern();}
  function aggStempelUmbenennen(alt,neu){const z=ANSICHT.angelegt;if(!z||z[alt]==null)return;z[neu]=z[alt];delete z[alt];ansichtSpeichern();}
  const aggZeit=nm=>{const z=(ANSICHT.angelegt||{})[nm];return z!=null?z:Math.max(0,MODEL.records.findIndex(r=>r.name===nm));};
  // Ein Decide zählt mit seinem Command, ein Apply mit seinem Event, die Fragen mit ihrer jüngsten Query.
  function aggGliedZeit(x){const n=NODEBY.get(x)||{},r=n.ref||{};
    switch(n.kind){case "decider":return aggZeit(r.command);case "applier":return aggZeit(r.event);
      case "aggregate":return Math.max(0,...(r.fragen||[]).map(f=>aggZeit(f.query)));default:return x.startsWith("rec:")?aggZeit(n.name):0;}}
  // Welche Spalte führt und wonach sie steht (⇅ im Spaltenkopf, gemerkt in der Ansicht): die Commands oder die Ergebnisse — nach Neuheit,
  //   nach dem Namen oder nach der Zahl der Eingänge (wenige zuerst: Neues und Unverbundenes oben); bei Gleichstand das Neueste zuerst.
  const aggFuehrt=()=>ANSICHT.aggFuehrt==="event"?"event":"command";
  const AGG_SORTIERUNG={neu:["neu","das Neueste oben"],alphabet:["A–Z","nach dem Namen"],eingaenge:["Eingänge","wenige Eingänge zuerst"]};
  const aggSortierung=()=>AGG_SORTIERUNG[ANSICHT.aggSortierung]?ANSICHT.aggSortierung:"neu";
  function aggGliedName(x){const n=NODEBY.get(x)||{},r=n.ref||{};
    switch(n.kind){case "decider":return r.command||"";case "applier":return r.event||"";case "aggregate":return ((r.fragen||[])[0]||{}).query||"";default:return n.name||"";}}
  // Eingänge einer Karte: die Kanten, die in ihr enden; bei einem Command bzw. einer Query dazu, wer sie von außen hineingibt.
  function aggEingaenge(A,I,x){const n=NODEBY.get(x)||{},rein=I.kanten.filter(([a,b])=>b===x).length;
    if(!x.startsWith("rec:")||I.glieder.get(x)!==0)return rein;
    return rein+new Set(FL().kanten.filter(k=>k.nach==="agg:"+A.name&&k.typ===n.name).map(k=>k.von)).size;}
  // Legen im Aggregat: ein Decide steht neben seinem Command, ein Apply neben seinem Event (1:1, oben bündig — die nächste Zeile beginnt unter
  //   beiden). Die führende Spalte steht nach dem gewählten Schlüssel; die andere Seite folgt ihren Kanten (oben bündig mit dem ersten,
  //   der sie erzeugt bzw. den sie ergibt), Unverbundenes darunter. Gestrichelte Karten (Neues beim Anschließen) stehen oben in ihrer Spalte.
  // Legen in der Innenansicht: je Band zwei Paare von Spalten (0·1 und 2·3), jede Karte oben bündig mit ihrem Partner (die nächste Zeile beginnt
  //   unter beiden). Die führende Seite steht nach dem gewählten Schlüssel; die andere folgt ihren Kanten (oben bündig mit dem ersten, der sie
  //   erzeugt bzw. den sie ergibt), Unverbundenes darunter. Folgende Spalten (L.folgen: [Spalte, Quelle]) folgen ihrer Quelle; freie Spalten
  //   (L.frei) stehen über alle Bänder von oben. Gestrichelte Karten (Neues beim Anschließen) stehen oben in ihrer Spalte.
  function innenLegenPaare(welt,kanten,y0,L,fuehrtB,sortierung){const els=[...welt.querySelectorAll(".kom-k")],G=16,Y=new Map(),xs=new Map(),c=new Map(L.spalten.map(s=>[s,y0]));
    const id=e=>e.dataset.glied,sp=e=>+e.dataset.spalte,neu=e=>id(e).startsWith("neu:"),zeit=e=>+e.dataset.zeit||0,byId=new Map(els.map(e=>[id(e),e]));
    const frei=new Set(L.frei||[]),gebunden=L.spalten.filter(s=>!frei.has(s)),band=e=>e.dataset.band||"";let B="";
    const echt=s=>els.filter(e=>sp(e)===s&&!neu(e)&&(frei.has(s)||band(e)===B)),juenger=(a,b)=>zeit(b)-zeit(a),juengste=l=>[...l].sort(juenger);
    const vor=sortierung==="alphabet"?(a,b)=>a.dataset.name.localeCompare(b.dataset.name,"de")||juenger(a,b)
      :sortierung==="eingaenge"?(a,b)=>(+a.dataset.ein)-(+b.dataset.ein)||juenger(a,b):juenger,geordnet=l=>[...l].sort(vor);
    const nach=(e,s)=>kanten.filter(k=>k[0]===id(e)).map(k=>byId.get(k[1])).filter(o=>o&&sp(o)===s);
    const von=(e,s)=>kanten.filter(k=>k[1]===id(e)).map(k=>byId.get(k[0])).filter(o=>o&&sp(o)===s);
    const setze=(e,y)=>{Y.set(id(e),y);const s=sp(e);c.set(s,Math.max(c.get(s),y+e.offsetHeight+G));};
    const oben=l=>{const v=l.map(o=>Y.get(id(o))).filter(y=>y!=null);return v.length?Math.min(...v):null;};
    // Spalte a in der Reihenfolge reihe, je Karte ihre noch freien Partner in Spalte b daneben.
    const paare=(a,b,reihe,partner,wunsch)=>reihe.forEach(e=>{if(Y.has(id(e)))return;const ps=partner(e).filter(p=>!Y.has(id(p))),w=wunsch?wunsch(e):null;
      const top=Math.max(c.get(a),w==null?-Infinity:w,ps.length?c.get(b):-Infinity);setze(e,top);let yb=top;ps.forEach(p=>{setze(p,yb);yb+=p.offsetHeight+G;});});
    const folgt=(s,quelle)=>echt(s).map(e=>[e,oben(quelle(e))]).sort((p,q)=>((p[1]==null?1e12:p[1])-(q[1]==null?1e12:q[1]))||vor(p[0],q[0])).map(p=>p[0]);
    const rest=s=>geordnet(echt(s).filter(e=>!Y.has(id(e)))).forEach(e=>setze(e,c.get(s)));
    const baender=[];let start=y0;
    (L.baender||[""]).forEach((b,i)=>{B=b;gebunden.forEach(s=>c.set(s,start));
      if(i>0)baender.push({y:start-30,titel:(L.bandTitel||{})[b]||b});
      els.filter(e=>neu(e)&&!frei.has(sp(e))&&band(e)===b).forEach(e=>setze(e,c.get(sp(e))));
      if(fuehrtB){paare(2,3,geordnet(echt(2)),e=>nach(e,3));rest(3);
        paare(1,0,folgt(1,e=>nach(e,2)),e=>juengste(von(e,0)),e=>oben(nach(e,2)));rest(0);}
      else{paare(0,1,geordnet(echt(0)),e=>nach(e,1));rest(1);
        paare(2,3,folgt(2,e=>von(e,1)),e=>nach(e,3),e=>oben(von(e,1)));rest(3);}
      (L.folgen||[]).forEach(([s,q])=>folgt(s,e=>von(e,q)).forEach(e=>{if(Y.has(id(e)))return;const w=oben(von(e,q));setze(e,Math.max(c.get(s),w==null?-Infinity:w));}));
      gebunden.forEach(rest);
      if(L.baender&&L.baender.length>1&&i<L.baender.length-1)start=Math.max(start,...gebunden.map(s=>c.get(s)))+48;});
    frei.forEach(s=>{c.set(s,y0);els.filter(e=>neu(e)&&sp(e)===s).forEach(e=>setze(e,c.get(s)));rest(s);});
    let x=40,hoehe=0;L.spalten.forEach(s=>{const sp_=els.filter(e=>sp(e)===s),w=Math.max(220,...sp_.map(e=>e.offsetWidth));
      sp_.forEach(e=>{e.style.left=x+"px";e.style.top=Y.get(id(e))+"px";});xs.set(s,{x,w});hoehe=Math.max(hoehe,c.get(s));x+=w+120;});
    const svgK=welt.querySelector("svg");welt.style.width=x+"px";welt.style.height=(hoehe+30)+"px";svgK.setAttribute("width",x);svgK.setAttribute("height",hoehe+30);
    return {xs,baender};}
  // Kurzfassung unter dem Kopf: woher ein Command kommt, wohin ein Ergebnis geht, die Felder.
  function aggKurz(A,n,x){const box=h("div",{class:"ag-kurz"}),agg="agg:"+A.name,FLK=FL().kanten,nm=id=>(FLKARTE.get(id)||{name:id}).name;
    if(n.kind==="decider"||n.kind==="applier")return box;
    if(n.kind==="state"){box.append(A.state.map(f=>f.name).join(", ")||"— noch keine Felder");return box;}
    if(n.kind==="aggregate")return box;
    const r=n.ref||{},von=[...new Set(FLK.filter(k=>k.nach===agg&&k.typ===n.name).map(k=>nm(k.von)))],
      nach=[...new Set(FLK.filter(k=>k.von===agg&&k.typ===n.name).map(k=>nm(k.nach)))];
    const felder=(r.felder||[]).map(f=>f.name).join(", ");
    if(felder)box.append(h("div",{},felder));
    if(r.istErzeugung)box.append(h("div",{class:"ag-tag"},"⊕ erzeugt das Aggregat"));
    if(von.length)box.append(h("div",{class:"ag-tag"},"von "+von.join(", ")));
    if(nach.length)box.append(h("div",{class:"ag-tag"},"→ "+nach.join(", ")));
    return box;}
  // Felder bearbeiten: je Zeile Name und Typ (Vorschläge aus den Typen der Anwendung), ✕; ＋ Feld. Ein fester Ziel-Parameter bleibt gesperrt.
  function aggFelder(liste,fest,titel){const box=h("div",{class:"ag-felder"});
    liste.forEach((f,i)=>{const gesperrt=fest&&fest(f);
      const nameI=h("input",{value:f.name||"",spellcheck:"false",title:"Name",onchange:e=>{f.name=e.target.value.trim();gTitel(titel+": Feld "+f.name);render();}});
      const typI=h("input",{value:f.typ||"",list:"de-typen",spellcheck:"false",title:"Typ",onchange:e=>{f.typ=e.target.value.trim();gTitel(titel+": "+f.name+" : "+f.typ);render();}});
      if(gesperrt){nameI.disabled=true;typI.disabled=true;}
      box.append(h("div",{class:"ag-feld"},nameI,typI,gesperrt?h("span",{class:"ag-fest",title:"Ziel des Commands — fest"},"fest"):
        h("button",{class:"rm",title:"Feld entfernen",onclick:()=>{liste.splice(i,1);gTitel(titel+": Feld "+f.name+" entfernt");render();}},"✕")));});
    box.append(h("button",{class:"ag-plus",onclick:()=>{liste.push({_id:"f"+(NID++),name:uniqFeldName(liste,"Feld"),typ:"string"});gTitel(titel+": ＋ Feld");render();}},"＋ Feld"));
    return box;}
  // Ein Record entfernen, wenn ihn niemand mehr nennt (Decide, Apply, Frage, Projektion, Akteur, Funktion).
  function aggRecordAufraeumen(nm){const genutzt=MODEL.decider.some(d=>d.command===nm||(d.ergibt||[]).some(o=>o.event===nm))||MODEL.applier.some(a=>a.event===nm)
      ||MODEL.aggregate.some(a=>(a.fragen||[]).some(f=>f.query===nm||(f.antworten||[]).includes(nm)))||MODEL.projektionen.some(p=>(p.handles||[]).some(x=>hdEvents(x).includes(nm)))
      ||MODEL.reader.some(r=>(r.handles||[]).some(x=>x.query===nm||(x.responses||[]).includes(nm)))||MODEL.akteure.some(a=>(a.darf||[]).includes(nm)||(a.hoert||[]).includes(nm))
      ||MODEL.bausteinFunktionen.some(f=>(f.eingaenge||[]).some(e=>e.eingang===nm||(e.ausgaben||[]).includes(nm))||(f.lauscht||[]).includes(nm));
    if(!genutzt){const r=recNamens(nm);if(r)MODEL.records.splice(MODEL.records.indexOf(r),1);}}
  // Einen noch nicht geschriebenen Record umbenennen (ein geschriebener geht über den Renamer): alle Verweise im Modell ziehen mit.
  function recUmbenennenLokal(r,neu){const alt=r.name;r.name=neu;
    MODEL.decider.forEach(d=>{if(d.command===alt)d.command=neu;(d.ergibt||[]).forEach(o=>{if(o.event===alt)o.event=neu;});});MODEL.applier.forEach(a=>{if(a.event===alt)a.event=neu;});
    MODEL.aggregate.forEach(a=>(a.fragen||[]).forEach(f=>{if(f.query===alt)f.query=neu;f.antworten=(f.antworten||[]).map(v=>v===alt?neu:v);}));
    MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===alt)hd.event=neu;buendelUmbenennen(hd,alt,neu);}));
    MODEL.reader.forEach(o=>(o.handles||[]).forEach(hd=>{if(hd.query===alt)hd.query=neu;hd.responses=(hd.responses||[]).map(v=>v===alt?neu:v);}));}
  // Der Bearbeiten-Modus einer Karte (✎ im Kopf) — er trägt alles, was früher das Formular der Karte hatte.
  function aggBearbeiten(A,n,x){const box=h("div",{class:"ag-edit"}),t="▣ "+A.name;
    // Eingaben im Bearbeiten-Modus wählen die Karte nicht ab (sonst zeichnete der Klick neu und nähme den Fokus).
    ["click","dblclick"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));
    const ruempfe=()=>[...NODEBY.values()].filter(r=>r.kind==="rumpf"&&r.own&&r.own.id===x).forEach(r=>{const b=h("div",{class:"gbody"});
      INSP=true;try{fuelleKoerper(b,r);}finally{INSP=false;}box.append(b);});
    const weg=(text,fn)=>box.append(h("button",{class:"ag-weg",onclick:()=>{fn();render();}},text));
    if(x.startsWith("rec:")){const r=n.ref;
      box.append(h("div",{class:"ag-feld"},h("input",{value:r.name,spellcheck:"false",title:"Name",onchange:e=>{const neu=e.target.value.trim(),f=nameFehler(neu);if(neu===r.name)return;
        if(f){deFlash("⚠ "+f,false);render();return;}aggStempelUmbenennen(r.name,neu);if(r._ort)gUmbenennen(r,neu,r.name);else{recUmbenennenLokal(r,neu);render();}}})));
      box.append(aggFelder(r.felder=r.felder||[],f=>r.kind==="command"&&r.aggregat===A.name&&f.name===aggIdFeld(),t+": "+r.name));
      if(r.kind==="command"&&r.aggregat===A.name){const c=h("input",{type:"checkbox",onchange:e=>{r.istErzeugung=e.target.checked;gTitel(t+": "+r.name+(r.istErzeugung?" erzeugt":" erzeugt nicht mehr"));render();}});c.checked=!!r.istErzeugung;
        box.append(h("label",{class:"ag-flag"},c," erzeugt das Aggregat (ICreationCommand)"));}
      else if(r.kind==="event"||r.kind==="rejection"){
        box.append(h("button",{class:"ag-plus",title:"Fakt im Log (Event) oder flüchtig an Akteure (Hinweis) — der Modellierer entscheidet",
          onclick:()=>{r.kind=r.kind==="event"?"rejection":"event";if(r.kind==="rejection")MODEL.applier.filter(a=>a.event===r.name).forEach(a=>MODEL.applier.splice(MODEL.applier.indexOf(a),1));
            else aggApplySichern(A,r.name);
            gTitel(t+": "+r.name+(r.kind==="event"?" ist ein Event":" ist ein Hinweis"));render();}},r.kind==="event"?"⇄ zum Hinweis machen":"⇄ zum Event machen"));}
      if(I_ERGEBNIS(A,r.name))weg("✕ aus allen Decides nehmen",()=>{MODEL.decider.filter(d=>d.aggregat===A.name).forEach(d=>{d.ergibt=(d.ergibt||[]).filter(o=>o.event!==r.name);});
        gTitel(t+": "+r.name+" entfernt");KOM.edit.delete(x);aggRecordAufraeumen(r.name);});
      if(r.kind==="queryresponse"&&(A.fragen||[]).some(f=>(f.antworten||[]).includes(r.name)))weg("✕ aus allen Fragen nehmen",()=>{(A.fragen||[]).forEach(f=>{f.antworten=ohneX(f.antworten,r.name);});
        gTitel(t+": Antwort "+r.name+" entfernt");KOM.edit.delete(x);aggRecordAufraeumen(r.name);});
      return box;}
    if(n.kind==="decider"){ruempfe();return box;}
    if(n.kind==="applier"){ruempfe();return box;}
    if(n.kind==="state"){box.append(aggFelder(A.state=A.state||[],null,t+": Zustand"));return box;}
    if(n.kind==="aggregate"){ruempfe();return box;}
    return box;}
  // ══ STORE VON INNEN (docs/konzept-projektionen-im-editor.md): zwei Bänder — Schreiben: Event · Auf, Schreib-Fähigkeit · Umsetzung; Lesen:
  //   Query · Frage, Lese-Fähigkeit · Umsetzung, dazu die Antworten. Gebunden ist nur, was die Regeln binden (Event ↔ Auf, Query ↔ Frage,
  //   Fähigkeit ↔ Umsetzung); die Fähigkeit wählt man am Port des Auf bzw. der Frage wie das Ergebnis am Decide. Die Read-Models stehen in der
  //   Mitte beider Bänder, ihre Kanten nur aus der Signatur einer Fähigkeit. Events stammen aus dem Aggregat und sind hier fix.
  const stoFns=st=>[...(st.writeFns||[]).map(f=>[f,false]),...(st.readFns||[]).map(f=>[f,true])];
  // ── BÜNDEL (docs/04 §4.3.1): ein Auf über Buendel<A, B, …> — es heißt nach seinen Events, der Schlüssel folgt aus ihnen (Server, Buendelform). ──
  const BUENDEL_HOECHSTENS=16;
  const istBuendel=hd=>!!hd&&(!!hd.buendel||/^Buendel</.test(hd.event||""));
  const buendelEvents=hd=>hd.buendel&&hd.buendel.events?hd.buendel.events:((hd.event||"").match(/^Buendel<(.*)>$/)||[,""])[1].split(",").map(x=>x.trim()).filter(Boolean);
  const buendelName=hd=>buendelEvents(hd).join(" ⋈ ");
  // Die Events, die ein Auf hört: das Event selbst bzw. die seines Bündels.
  const hdEvents=hd=>istBuendel(hd)?buendelEvents(hd):hd&&hd.event?[hd.event]:[];
  // Ein Bündel neu fassen: der Eingang heißt Buendel<…> nach seinen Events; steht es schon im Code, merkt input den alten Eingang (er wird umgeschrieben).
  function buendelSetze(hd,events,ueber){if(hd.sig&&!hd.input)hd.input=hd.event;
    hd.buendel={...(hd.buendel||{}),events:[...events],ueber:ueber===undefined?((hd.buendel||{}).ueber||null):ueber,glieder:null,schluessel:null,fehler:null,neu:true};
    hd.event="Buendel<"+events.join(", ")+">";}
  function buendelUmbenennen(hd,alt,neu){if(!istBuendel(hd))return;const ev=buendelEvents(hd);if(!ev.includes(alt))return;buendelSetze(hd,ev.map(e=>e===alt?neu:e));}
  const stoTypen=f=>[...(f.params||[]).map(p=>p.typ),f.rueckgabe,(f.sig||{}).rueckgabe].filter(Boolean).join(" ");
  const nennt=(text,name)=>new RegExp("(^|[^A-Za-z0-9_])"+name+"($|[^A-Za-z0-9_])").test(text||"");
  const stoNenntIrgendwer=nm=>MODEL.stores.some(s=>stoFns(s).some(([f])=>nennt(stoTypen(f),nm)));
  const flGruppe=x=>{const k=FL().karten.find(f=>f.karte===x&&f.gruppe);return k?k.gruppe:null;};
  const stoEvent=x=>x.startsWith("rec:")&&(NODEBY.get(x)||{}).kind==="event";
  // Wer zum Store gehört: Projektionen und Reader seiner Gruppe (aus den Fähigkeiten), eben hier angelegte (amStore) und solche ohne jede
  //   Fähigkeit in seinem Namespace (WO sie liegen, W2); dazu Reader anderer Stores, die hier mitlesen (nur deren Fragen, leise).
  function stoMitglieder(id,st){const fnIds=new Set(stoFns(st).map(([f])=>f._id));
    const ohneFns=o=>(o.handles||[]).every(hd=>!(hd.fns||[]).length);
    const eigen=(o,pre)=>{const g=flGruppe(pre+":"+o._id);return g===id||o.amStore===st.name||(!g&&ohneFns(o)&&o.namespace===st.namespace);};
    const prj=MODEL.projektionen.filter(p=>eigen(p,"prj")),rdr=MODEL.reader.filter(r=>eigen(r,"rdr"));
    const fremd=MODEL.reader.filter(r=>!rdr.includes(r)&&(r.handles||[]).some(hd=>(hd.fns||[]).some(f=>fnIds.has(f))));
    return {prj,rdr,fremd,fnIds};}
  function stoInnen(id,st){const M=stoMitglieder(id,st),glieder=new Map(),kanten=[],band=new Map(),knoten=new Map(),fremd=new Set(),unverbunden=new Set();
    const setze=(x,s,b)=>{glieder.set(x,s);band.set(x,b);};
    M.prj.forEach(p=>{const ids=handleIds(p,"prj:"+p._id);(p.handles||[]).forEach((hd,i)=>{if(!hd.event)return;const hx=ids[i],e="rec:"+hd.event;
      // Ein Bündel: im Band „Bündeln“ — links die Bündel-Karte (seine Events als Ports), dann das Auf wie gewohnt.
      if(istBuendel(hd)){const bx="bu:"+hx;setze(hx,1,"b");setze(bx,0,"b");kanten.push([bx,hx]);
        knoten.set(bx,{id:bx,kind:"buendel",name:buendelName(hd),ref:hd,own:NODEBY.get(hx),prj:p});
        (hd.fns||[]).forEach(f=>{if(M.fnIds.has(f))kanten.push([hx,"fn:"+f]);});return;}
      setze(hx,1,"s");
      if(NODEBY.has(e)){setze(e,0,"s");kanten.push([e,hx]);}
      (hd.fns||[]).forEach(f=>{if(M.fnIds.has(f))kanten.push([hx,"fn:"+f]);});});});
    [...M.rdr.map(r=>[r,false]),...M.fremd.map(r=>[r,true])].forEach(([r,fr])=>{const ids=handleIds(r,"rdr:"+r._id);(r.handles||[]).forEach((hd,i)=>{
      if(!hd.query||(fr&&!(hd.fns||[]).some(f=>M.fnIds.has(f))))return;const hx=ids[i],q="rec:"+hd.query;setze(hx,1,"l");if(fr)fremd.add(hx);
      if(NODEBY.has(q)){setze(q,0,"l");kanten.push([q,hx]);}
      (hd.fns||[]).forEach(f=>{if(M.fnIds.has(f))kanten.push([hx,"fn:"+f]);});
      if(!fr)(hd.responses||[]).forEach(a=>{const ax="rec:"+a;if(NODEBY.has(ax)){setze(ax,5,"l");kanten.push([hx,ax]);}});});});
    const nurBuendel=fid=>{const nutzer=M.prj.flatMap(p=>(p.handles||[]).filter(hd=>(hd.fns||[]).includes(fid)));return nutzer.length>0&&nutzer.every(istBuendel);};
    stoFns(st).forEach(([f,lesen])=>{const fx="fn:"+f._id,ux="um:"+f._id,b=lesen?"l":nurBuendel(f._id)?"b":"s";setze(fx,2,b);setze(ux,3,b);kanten.push([fx,ux]);
      knoten.set(ux,{id:ux,kind:"umsetzung",name:f.name,ref:f,st,lesen,own:NODEBY.get(fx)});
      if(!kanten.some(([,z])=>z===fx))unverbunden.add(fx);
      const typen=stoTypen(f);MODEL.readModels.forEach(rm=>{if(nennt(typen,rm.name)){const rx="rm:"+rm._id;setze(rx,4,"");kanten.push([ux,rx]);}});});
    // Unverbunden, aber beim Store: ein Read-Model, das keine Fähigkeit nennt, eine Antwort ohne Frage — hier angelegt bzw. im Namespace des Stores.
    MODEL.readModels.forEach(rm=>{const rx="rm:"+rm._id;if(!glieder.has(rx)&&(rm.amStore===st.name||(rm.namespace===st.namespace&&!stoNenntIrgendwer(rm.name)))){setze(rx,4,"");unverbunden.add(rx);}});
    MODEL.records.forEach(r=>{const ax="rec:"+r.name;if(r.kind==="queryresponse"&&!glieder.has(ax)&&NODEBY.has(ax)&&(r.amStore===st.name||r.namespace===st.namespace)&&!aggHatErzeuger(r.name)){
      setze(ax,5,"l");unverbunden.add(ax);}});
    return {glieder,kanten,band,knoten,fremd,unverbunden,M};}
  function stoZeige(de,id,st){const I=stoInnen(id,st),kn=x=>I.knoten.get(x)||NODEBY.get(x);
    innenZeige(de,id,{I,knoten:kn,unverbunden:I.unverbunden,
      unverbundenText:"Liegt beim Store, noch von keinem Auf bzw. keiner Frage genommen bzw. von keiner Fähigkeit genannt — anschließen über ＋ am Port",
      band:x=>I.band.get(x)||"",fest:x=>stoEvent(x)||I.fremd.has(x),fremd:x=>I.fremd.has(x),
      marke:x=>I.fremd.has(x)?h("span",{class:"kom-mark",title:"Diese Frage liest hier mit, gehört aber zu einem anderen Store — bearbeitet wird sie dort"},
        "gehört zu "+((NODEBY.get(flGruppe(kn(x).own.id))||{}).name||"einem anderen Store")):null,
      festKnopf:(n,x)=>stoEvent(x)?h("button",{class:"ag-stift",title:"Events stammen aus dem Aggregat und sind hier fix — Klick: im Aggregat öffnen",
        onclick:e=>{e.stopPropagation();const ag=(SICHT().karten[x]||{}).aggregat;if(ag)komKarte("agg:"+ag,true);}},"▣"):null,
      ports:(n,x)=>stoPorts(st,I,n,x),kantenPorts:(a,b)=>stoKantenPorts(I,kn,a,b),auswahl:p=>stoAuswahl(I,kn,p),anschliessen:(p,x)=>stoAnschliessen(st,kn,p,x),
      geister:p=>stoGeister(st,p),plus:(p,x,e)=>p.art==="buendelEvent"?stoBuendelEvent(st,p.hd,e):innenPortWahl(p,x),kurz:(n,x)=>stoKurz(st,n,x),bearbeiten:(n,x)=>stoBearbeiten(st,I,n,x),
      loeschen:(n,x)=>stoLoeschPlan(st,I,n,x),
      // Ein Eingang ist im Store so neu wie seine Handhabung — das Event selbst ist älter (es stammt aus dem Aggregat).
      zeit:x=>{const hs=I.glieder.get(x)===0?I.kanten.filter(([a])=>a===x).map(([,b])=>stoZeit(st,kn(b))):[];return hs.length?Math.max(...hs):stoZeit(st,kn(x));},
      name:x=>stoName(kn(x)),ein:x=>I.kanten.filter(([,b])=>b===x).length,koepfe:()=>stoKoepfe(st),
      // Das Band „Bündeln“ steht nur, wenn der Store ein Bündel hat (das erste entsteht über den Kopf der Spalte Eingänge).
      legen:{spalten:[0,1,2,3,4,5],baender:[...I.band.values()].includes("b")?["s","b","l"]:["s","l"],bandTitel:{s:"Schreiben",b:"Bündeln",l:"Lesen"},frei:[4],folgen:[[5,1]]},
      ansicht:{fuehrt:()=>ANSICHT.stoFuehrt==="faehigkeit"?"faehigkeit":"eingang",sortierung:()=>AGG_SORTIERUNG[ANSICHT.stoSortierung]?ANSICHT.stoSortierung:"neu",
        B:"faehigkeit",setze:(f,s)=>{ANSICHT.stoFuehrt=f;ANSICHT.stoSortierung=s;ansichtSpeichern();}}});}
  // Ports im Store: Auf bzw. Frage nimmt links ihren Eingang und rechts ihre Fähigkeiten (＋ wählt bzw. legt an), eine Frage dazu ihre Antworten;
  //   eine Fähigkeit wird links genommen (＋ Auf bzw. Frage) und hat rechts ihre Umsetzung; die Umsetzung zeigt rechts die Read-Models ihrer Signatur.
  function stoPorts(st,I,n,x){const ein=[],aus=[],t="🗄 "+st.name,s=nm=>komSorte(nm);
    const fnName=fid=>{const k=NODEBY.get("fn:"+fid);return k?k.name:fid.split(".").pop();};
    if(n.kind==="buendel"){const hd=n.ref,ev=buendelEvents(hd);
      ev.forEach(e=>ein.push({key:"ev:"+e,label:e,sorte:"event",loese:ev.length>2?()=>{buendelSetze(hd,ev.filter(x=>x!==e));gTitel(t+": "+e+" nicht mehr im Bündel");render();}:null}));
      if(ev.length<BUENDEL_HOECHSTENS)ein.push({key:"+e",label:"＋ Event",plus:{art:"buendelEvent",hd}});
      aus.push({key:"aus",label:"▶ Auf",sorte:"event"});
      return {ein,aus};}
    if(n.kind==="handle"){const hd=n.ref,lesen=n.own.kind==="reader",typ=istBuendel(hd)?buendelName(hd):hd.event||hd.query,fr=I.fremd.has(x);
      ein.push({key:"in",label:typ,sorte:istBuendel(hd)?"event":s(typ)});
      (hd.fns||[]).filter(f=>I.M.fnIds.has(f)).forEach(f=>aus.push({key:"f:"+f,label:"▶ "+fnName(f),
        loese:fr?null:()=>{hd.fns=ohneX(hd.fns,f);gTitel(t+": "+typ+" nimmt "+fnName(f)+" nicht mehr");render();}}));
      if(!fr)aus.push({key:"+f",label:"＋ "+(lesen?"Lese":"Schreib")+"-Fähigkeit",plus:{art:"fn",hd,lesen}});
      if(lesen&&!fr){(hd.responses||[]).forEach(a=>aus.push({key:"r:"+a,label:a,sorte:"antwort",
          loese:()=>{hd.responses=ohneX(hd.responses,a);gTitel(t+": "+typ+" antwortet nicht mehr "+a);render();}}));
        aus.push({key:"+r",label:"＋ Antwort",plus:{art:"antwort",hd}});}}
    else if(n.kind==="fn"){const f=n.ref,lesen=!!n.lesen;
      I.kanten.filter(([,b])=>b===x).forEach(([a])=>{const hn=NODEBY.get(a);if(!hn)return;const typ=hn.ref.event||hn.ref.query;
        ein.push({key:"von:"+a,label:"◀ "+typ,sorte:s(typ),loese:I.fremd.has(a)?null:()=>{hn.ref.fns=ohneX(hn.ref.fns,f._id);gTitel(t+": "+typ+" nimmt "+f.name+" nicht mehr");render();}});});
      ein.push({key:"+",label:"＋ "+(lesen?"Frage":"Auf"),plus:{art:"handhabung",fn:f,lesen}});
      aus.push({key:"um",label:"▶ Umsetzung"});}
    else if(n.kind==="umsetzung"){ein.push({key:"in",label:"◀ "+(n.ref.faehigkeit||n.ref.name)});
      I.kanten.filter(([a])=>a===x).forEach(([,b])=>{const rm=NODEBY.get(b);if(rm)aus.push({key:"rm:"+b,label:"▶ "+rm.name});});}
    else if(n.kind==="readmodel")ein.push({key:"in",label:"◀ Umsetzung"});
    else if(x.startsWith("rec:")){const nm=n.name;
      if(n.kind==="event")aus.push({key:"aus",label:"▶ Auf",sorte:"event"});
      else if(n.kind==="query")aus.push({key:"aus",label:"▶ Frage",sorte:"query"});
      else if(n.kind==="queryresponse"){I.kanten.filter(([,b])=>b===x).forEach(([a])=>{const hn=NODEBY.get(a);if(!hn)return;
          ein.push({key:"von:"+a,label:"◀ "+hn.ref.query,sorte:"antwort",loese:()=>{hn.ref.responses=ohneX(hn.ref.responses,nm);gTitel(t+": "+hn.ref.query+" antwortet nicht mehr "+nm);render();}});});
        ein.push({key:"+",label:"＋ Frage",plus:{art:"frage",rec:nm}});}}
    return {ein,aus};}
  function stoKantenPorts(I,kn,a,b){const na=kn(a),nb=kn(b);if(!na||!nb)return null;
    if(nb.kind==="handle")return ["aus","in"];   // auch Bündel → Auf
    if(na.kind==="handle")return nb.kind==="fn"?["f:"+b.slice(3),"von:"+a]:["r:"+nb.name,"von:"+a];
    if(na.kind==="fn")return ["um","in"];
    if(na.kind==="umsetzung")return ["rm:"+b,"in"];
    return null;}
  // Wer an einen „＋“ angeschlossen werden kann: Fähigkeiten derselben Richtung, Antworten, Auf bzw. Frage (nie die Frage eines anderen Stores).
  function stoAuswahl(I,kn,p){const k=new Set();if(!p)return k;
    I.glieder.forEach((s,x)=>{const n=kn(x);if(!n||I.fremd.has(x))return;
      if(p.art==="fn"&&n.kind==="fn"&&!!n.lesen===p.lesen&&!(p.hd.fns||[]).includes(n.ref._id))k.add(x);
      if(p.art==="antwort"&&n.kind==="queryresponse"&&!(p.hd.responses||[]).includes(n.name))k.add(x);
      if(p.art==="handhabung"&&n.kind==="handle"&&(n.own.kind==="reader")===p.lesen&&!(n.ref.fns||[]).includes(p.fn._id))k.add(x);
      if(p.art==="frage"&&n.kind==="handle"&&n.own.kind==="reader"&&!(n.ref.responses||[]).includes(p.rec))k.add(x);});
    return k;}
  function stoAnschliessen(st,kn,p,x){const n=kn(x),t="🗄 "+st.name;if(!n)return;KOM.port=null;
    if(p.art==="fn"){p.hd.fns=[...(p.hd.fns||[]),n.ref._id];gTitel(t+": "+(p.hd.event||p.hd.query)+" nimmt "+n.name);}
    else if(p.art==="antwort"){p.hd.responses=[...(p.hd.responses||[]),n.name];gTitel(t+": "+p.hd.query+" antwortet "+n.name);}
    else if(p.art==="handhabung"){n.ref.fns=[...(n.ref.fns||[]),p.fn._id];gTitel(t+": "+(n.ref.event||n.ref.query)+" nimmt "+p.fn.name);}
    else if(p.art==="frage"){n.ref.responses=[...(n.ref.responses||[]),p.rec];gTitel(t+": "+n.ref.query+" antwortet "+p.rec);}
    render();}
  // Gestrichelt beim Anschließen: eine neue Fähigkeit (samt Umsetzung) am Auf bzw. an der Frage, eine neue Antwort an der Frage, eine neue Query
  //   mit Frage an einer Antwort. Wohin eine neue Frage gehört, fragt der Store, wenn er mehrere Reader hat.
  function stoGeister(st,p){if(!p)return [];const g=[],t="🗄 "+st.name;
    const neu=(spalte,band,titel,u,vorschlag,anlegen,vorher)=>g.push({spalte,band,t:titel,u,fn:e=>{const frag=o=>frageFix(e.clientX,e.clientY,titel,vorschlag,u,n=>{
      const f=anlegen(n,o);if(f)return f;KOM.port=null;render();});if(vorher)vorher(e,frag);else frag();}});
    if(p.art==="fn")neu(2,p.lesen?"l":"s","＋ neue "+(p.lesen?"Lese":"Schreib")+"-Fähigkeit","Methode — die Schnittstelle heißt I + Methode ohne Async; die Umsetzung entsteht mit",
      stoMethodenVorschlag(p.hd,p.lesen),n=>stoFaehigkeitAnlegen(st,n,p.lesen,p.hd));
    else if(p.art==="antwort")neu(5,"l","＋ neue Antwort","Antwort auf "+p.hd.query,p.hd.query.replace(/^(Hole|Get)/,"")+"Antwort",n=>{const f=nameFehler(n);if(f)return f;
      stoRecordNeu(st,n,"queryresponse");p.hd.responses=[...(p.hd.responses||[]),n];gTitel(t+": "+p.hd.query+" antwortet "+n);});
    else if(p.art==="frage")neu(0,"l","＋ neue Query mit Frage","Frage an "+st.name+" — antwortet "+p.rec,"Hole"+p.rec.replace(/Antwort$/,""),
      (n,o)=>stoQueryAnlegen(st,n,[p.rec],o),(e,frag)=>stoWaehleBesitzer(st,"reader",e,frag));
    return g;}
  const stoMethodenVorschlag=(hd,lesen)=>!hd?"":lesen?"Finde"+(hd.query||"").replace(/^(Hole|Get)/,"")+"Async":"Vermerke"+(istBuendel(hd)?buendelEvents(hd).join("Und"):hd.event||"")+"Async";
  // Default-Parameter einer neuen Fähigkeit (danach im ✎): zum Auf die Id des Aggregats, aus dem das Event stammt (bei Klasse 2 dazu die
  //   Herkunft, CQRS109); zur Frage die Id-Felder der Query.
  function stoParameterVorschlag(st,hd,lesen){const p=[];if(!hd)return p;
    if(!lesen){const b=istBuendel(hd)?hd.buendel||{}:null,ag=b?(b.ueber||b.schluessel):(SICHT().karten["rec:"+hd.event]||{}).aggregat,A=MODEL.aggregate.find(a=>a.name===ag);
      if(A)p.push({_id:"f"+(NID++),name:"id",typ:"Id<"+A.namespace+"."+A.name+">"});
      // Ein Bündel löst aus verschiedenen Streams aus: der Store braucht die Herkunft, um den jüngsten Stand zu halten (Herkunft.Stand).
      if((st.klassen||[])[0]===2||b)p.push({_id:"f"+(NID++),name:"h",typ:"Herkunft"});}
    else((recNamens(hd.query)||{}).felder||[]).filter(f=>/^Id</.test(f.typ||"")).forEach(f=>p.push({_id:"f"+(NID++),name:f.name.charAt(0).toLowerCase()+f.name.slice(1),typ:f.typ}));
    return p;}
  function stoFaehigkeitAnlegen(st,m,lesen,hd){if(!/^[A-Z][A-Za-z0-9_]*$/.test(m||""))return "Ein Methodenname: groß beginnen, nur Buchstaben und Ziffern";
    if(stoFns(st).some(([f])=>f.name===m))return m+" gibt es in "+st.name+" schon";
    const fa="I"+m.replace(/Async$/,"");if(MODEL.stores.some(s=>stoFns(s).some(([f])=>f.faehigkeit===fa)))return fa+" gibt es schon";
    const f={_id:(lesen?"rf:":"wf:")+st.name+"."+m,name:m,faehigkeit:fa,params:stoParameterVorschlag(st,hd,lesen),rueckgabe:""};
    (lesen?(st.readFns=st.readFns||[]):(st.writeFns=st.writeFns||[])).push(f);aggStempel("fn:"+st.name+"."+m);
    if(hd)hd.fns=[...(hd.fns||[]),f._id];gTitel("🗄 "+st.name+": "+(lesen?"Lese":"Schreib")+"-Fähigkeit "+fa+(hd?" für "+(hd.event||hd.query):""));}
  function stoRecordNeu(st,n,kind){MODEL.records.push({name:n,kind,namespace:st.namespace,felder:[],istErzeugung:false,amStore:st.name});aggStempel(n);}
  // Projektion bzw. Reader für Neues: hat der Store genau einen, diesen; keinen — er entsteht mit (Name nach dem Store); mehrere — Auswahl.
  //   Neu angelegt wird erst mit der Geste selbst ({neu:art}), damit ein Abbruch nichts Leeres zurücklässt.
  function stoWaehleBesitzer(st,art,ev,cb){const M=stoMitglieder("sto:"+st._id,st),liste=art==="reader"?M.rdr:M.prj;
    if(liste.length===1){cb(liste[0]);return;}if(!liste.length){cb({neu:art});return;}
    menueFix(ev.clientX,ev.clientY,art==="reader"?"In welchen Reader?":"In welche Projektion?",
      [...liste.map(o=>({t:o.name,fn:()=>cb(o)})),{trenn:1},{t:"＋ "+(art==="reader"?"neuer Reader":"neue Projektion"),fn:()=>cb({neu:art})}]);}
  function stoBesitzerName(st,art){const kern=st.name.replace(/^I(?=[A-Z])/,"").replace(/Store$/,"")||st.name,basis=kern+(art==="reader"?"Reader":"Projektion");
    let n=basis,i=2;while(MODEL.projektionen.some(p=>p.name===n)||MODEL.reader.some(r=>r.name===n)||recNamens(n))n=basis+(i++);return n;}
  function stoBesitzer(st,o){if(!o||!o.neu)return o;const n=stoBesitzerName(st,o.neu),neu={_id:(o.neu==="reader"?"reader:":"projektion:")+n,name:n,namespace:st.namespace,handles:[],amStore:st.name};
    if(o.neu!=="reader"&&(st.klassen||[])[0]===3)neu.mindestensEinmal=true;
    (o.neu==="reader"?MODEL.reader:MODEL.projektionen).push(neu);return neu;}
  function stoQueryAnlegen(st,n,antworten,o){const f=nameFehler(n);if(f)return f;
    MODEL.records.push({name:n,kind:"query",namespace:st.namespace,felder:[],istErzeugung:false,amStore:st.name});aggStempel(n);
    const r=stoBesitzer(st,o);r.handles=[...(r.handles||[]),{query:n,fns:[],responses:antworten}];aggStempel("hd:"+r.name+"."+n);
    gTitel("🗄 "+st.name+": Query "+n+" mit Frage in "+r.name);}
  // Ein Auf für ein Event, das die Projektion noch nicht hört — Events stammen aus dem Aggregat und werden hier nie angelegt.
  function stoNeuesAuf(st,ev){stoWaehleBesitzer(st,"projektion",ev,o=>{const hat=new Set(((o.neu?null:o)||{handles:[]}).handles.map(x=>x.event));
    const ag=nm=>(SICHT().karten["rec:"+nm]||{}).aggregat||"ohne Aggregat",evs=MODEL.records.filter(r=>r.kind==="event"&&!hat.has(r.name));
    const gruppen=[...new Set(evs.map(r=>ag(r.name)))].sort((a,b)=>a.localeCompare(b,"de"));
    menueFix(ev.clientX,ev.clientY,"Auf in "+(o.neu?stoBesitzerName(st,"projektion")+" (neu)":o.name)+" für …",evs.length?gruppen.flatMap(g=>[{kopf:"▣ "+g},
      ...evs.filter(r=>ag(r.name)===g).sort((a,b)=>a.name.localeCompare(b.name,"de")).map(r=>({t:r.name,fn:()=>{const p=stoBesitzer(st,o);
        if((st.klassen||[])[0]===3)p.mindestensEinmal=true;p.handles=[...(p.handles||[]),{event:r.name,fns:[]}];aggStempel("hd:"+p.name+"."+r.name);gTitel("🗄 "+st.name+": Auf "+r.name+" in "+p.name);render();}}))])
      :[{hinweis:"Die Projektion hört schon jedes Event — ein neues Event entsteht im Aggregat."}]);});}
  // Ein Event aus dem Log wählen (nach Aggregat gruppiert), ohne die schon gewählten.
  function stoEventWahl(ev,titel,ohne,cb,vorne){const ag=nm=>(SICHT().karten["rec:"+nm]||{}).aggregat||"ohne Aggregat",evs=MODEL.records.filter(r=>r.kind==="event"&&!ohne.includes(r.name));
    const gruppen=[...new Set(evs.map(r=>ag(r.name)))].sort((a,b)=>a.localeCompare(b,"de"));
    menueFix(ev.clientX,ev.clientY,titel,[...(vorne||[]),...(evs.length?gruppen.flatMap(g=>[{kopf:"▣ "+g},
      ...evs.filter(r=>ag(r.name)===g).sort((a,b)=>a.name.localeCompare(b.name,"de")).map(r=>({t:r.name,fn:()=>cb(r.name)}))]):[{hinweis:"Kein weiteres Event — ein neues entsteht im Aggregat."}])]);}
  // Ein neues Bündel: Projektion wählen, dann Event um Event (bis BUENDEL_HOECHSTENS) — ab zwei steht oben „✓ anlegen“. Weitere kommen später über
  //   „＋ Event“ an der Bündel-Karte.
  function stoNeuesBuendel(st,ev){stoWaehleBesitzer(st,"projektion",ev,o=>{
    const anlegen=evs=>{const p=stoBesitzer(st,o);if((st.klassen||[])[0]===3)p.mindestensEinmal=true;const hd={fns:[]};buendelSetze(hd,evs,null);
      if((p.handles||[]).some(x=>x.event===hd.event)){deFlash("ℹ "+p.name+" hört "+buendelName(hd)+" schon",true);return;}
      p.handles=[...(p.handles||[]),hd];aggStempel("hd:"+p.name+"."+hd.event);gTitel("🗄 "+st.name+": Bündel "+buendelName(hd)+" in "+p.name);render();};
    const weiter=evs=>{if(evs.length>=BUENDEL_HOECHSTENS){anlegen(evs);return;}
      stoEventWahl(ev,evs.length?"Bündel: "+evs.join(" ⋈ ")+" ⋈ …":"Bündel: erstes Event",evs,e=>weiter([...evs,e]),
        evs.length>=2?[{t:"✓ Bündel anlegen ("+evs.length+" Events)",title:evs.join(" ⋈ "),fn:()=>anlegen(evs)},{trenn:1},{kopf:"oder ein weiteres Event:"}]
        :evs.length===1?[{hinweis:"Mindestens zwei Events, höchstens "+BUENDEL_HOECHSTENS+"."}]:[]);};
    weiter([]);});}
  function stoBuendelEvent(st,hd,ev){const alt=buendelEvents(hd);
    stoEventWahl(ev||{clientX:200,clientY:200},"＋ Event im Bündel "+buendelName(hd),alt,e=>{buendelSetze(hd,[...alt,e]);gTitel("🗄 "+st.name+": "+e+" ins Bündel "+buendelName(hd));render();});}
  function stoNeueQuery(st,ev){stoWaehleBesitzer(st,"reader",ev,o=>frageFix(ev.clientX,ev.clientY,"＋ Query an "+st.name,"Hole"+st.name.replace(/^I(?=[A-Z])/,"").replace(/Store$/,""),
    "Legt Query, Frage und eine Antwort an — die Lese-Fähigkeit wählt man danach am Port der Frage",n=>{const ant=n.replace(/^(Hole|Get)/,"")+"Antwort";
      if(recNamens(ant))return ant+" gibt es schon";const f=nameFehler(n);if(f)return f;stoRecordNeu(st,ant,"queryresponse");return stoQueryAnlegen(st,n,[ant],o)||(render(),null);}));}
  // Ein Read-Model im Store: die Art entscheidet die Schreib-Regel je Dokument (docs/04 §4.3) — eigenes Dokument eines Aggregats (Id<T> Id,
  //   partial), Zeile eines Streams (Id mit dem Besitzer) oder geteilt (IGeteiltesReadModel). Es gehört zu den Dokument-Typen des Stores (amStore).
  function stoReadModelAnlegen(st,n,art){const f=nameFehler(n)||(MODEL.readModels.some(r=>r.name===n)?n+" gibt es schon":null);if(f)return f;
    const idf=art.agg?{name:"Id",typ:"Id<"+art.agg.namespace+"."+art.agg.name+">"}:{name:"Id",typ:"string"};
    MODEL.readModels.push({_id:"readmodel:"+n,name:n,namespace:st.namespace,felder:[{_id:"f"+(NID++),...idf}],geteilt:!!art.geteilt,amStore:st.name});aggStempel("rm:"+n);
    gTitel("🗄 "+st.name+": Read-Model "+n);render();}
  function stoKoepfe(st){const t="🗄 "+st.name,menue=(titel,items)=>ev=>menueFix(ev.clientX,ev.clientY,titel,items),klasse=(st.klassen||[])[0];
    const rm=(titel,vorschlag,art)=>e=>frageFix(e.clientX,e.clientY,titel,vorschlag,"Read-Model in "+st.name,n=>stoReadModelAnlegen(st,n,art));
    const kern=st.name.replace(/^I(?=[A-Z])/,"").replace(/Store$/,"");
    return [
      ["Eingänge",menue("Neu in "+st.name,[{t:"＋ Auf für ein Event …",title:"Events stammen aus dem Aggregat — hier wählt man, worauf die Projektion reagiert",fn:e=>stoNeuesAuf(st,e)},
        {t:"＋ Bündel aus Events …",title:"Ein Auf, das gerufen wird, sobald zum selben Schlüssel von jedem Event eines im Log steht (docs/04 §4.3.1)",fn:e=>stoNeuesBuendel(st,e)},
        {t:"＋ Query mit Frage und Antwort …",fn:e=>stoNeueQuery(st,e)}]),"＋ ▾","eingang"],
      ["Auf · Frage",null,"je Eingang eines"],
      ["Fähigkeiten",menue("Neu in "+st.name,[
        {t:"＋ Schreib-Fähigkeit …",fn:e=>frageFix(e.clientX,e.clientY,"＋ Schreib-Fähigkeit in "+st.name,"Schreibe"+kern+"Async","Methode — die Umsetzung entsteht mit",n=>stoFaehigkeitAnlegen(st,n,false,null)||(render(),null))},
        {t:"＋ Lese-Fähigkeit …",fn:e=>frageFix(e.clientX,e.clientY,"＋ Lese-Fähigkeit in "+st.name,"Finde"+kern+"Async","Methode — die Umsetzung entsteht mit",n=>stoFaehigkeitAnlegen(st,n,true,null)||(render(),null))},
        {trenn:1},{hinweis:"Steht unverbunden in der Spalte — anschließen über ＋ am Port (＋ Auf bzw. ＋ Frage)."}]),"＋ ▾","faehigkeit"],
      ["Umsetzung",null,"je Fähigkeit eine"+(klasse?" · Klasse "+klasse:"")],
      ["Read-Models",menue("Read-Model in "+st.name,[{kopf:"Eigenes Dokument eines Aggregats"},
        ...MODEL.aggregate.map(A=>({t:"＋ zu ▣ "+A.name+" …",fn:rm("＋ Read-Model zu "+A.name,A.name+"ReadModel",{agg:A})})),
        {trenn:1},{t:"＋ Zeile eines Streams …",title:"Die Id enthält den Besitzer, z. B. {DatensatzId}:{ImagePairId}",fn:rm("＋ Zeile",kern+"Zeile",{})},
        {t:"＋ geteiltes Read-Model …",title:"Mehrere Streams schreiben hinein (IGeteiltesReadModel, Versionsprüfung)",fn:rm("＋ geteiltes Read-Model",kern+"Uebersicht",{geteilt:true})}]),"＋ ▾"],
      ["Antworten",e=>frageFix(e.clientX,e.clientY,"＋ Antwort in "+st.name,kern+"Antwort","Steht unverbunden in der Spalte — anschließen über ＋ am Port (＋ Frage)",
        n=>{const f=nameFehler(n);if(f)return f;stoRecordNeu(st,n,"queryresponse");gTitel(t+": Antwort "+n);render();}),"＋"]];}
  function stoKurz(st,n,x){const box=h("div",{class:"ag-kurz"});
    if(n.kind==="buendel"){const b=n.ref.buendel||{};
      if(b.neu||!b.glieder)box.append(h("div",{class:"ag-tag"},"🔑 "+(b.ueber?"über "+String(b.ueber).split(".").pop():"wird aus den Events abgeleitet")+" — nach dem Schreiben"));
      else if(b.fehler)box.append(h("div",{class:"ag-diag",title:b.fehler},"⛔ "+b.fehler));
      else{box.append(h("div",{class:"ag-tag",title:"Der Schlüssel folgt aus den Events (docs/04 §4.3.1)"},"🔑 "+(b.schluessel?"Id<"+b.schluessel+">":"global — je Typ das neueste")+(b.ueber?" · [Ueber]":"")));
        if(b.schluessel)box.append(h("div",{},(b.glieder||[]).map(g=>g.event+": "+(g.art==="stream"?"Stream":g.art==="feld"?"Feld "+g.feld:"—")).join(" · ")));}
      return box;}
    if(n.kind==="fn"){const f=n.ref;box.append(h("div",{},"("+(f.params||[]).map(p=>p.typ+" "+p.name).join(", ")+")"+(n.lesen?" → "+(f.rueckgabe||(f.sig||{}).rueckgabe||"Task"):"")));}
    else if(n.kind==="umsetzung"){const k=(st.klassen||[])[0];box.append(h("div",{class:"ag-tag"},(st.impl&&st.impl.name||"neue Umsetzung")+(k?" · Klasse "+k:"")));}
    else if(n.kind==="readmodel"){const fs=(n.ref.felder||[]).map(f=>f.name).join(", ");if(fs)box.append(h("div",{},fs));if(n.ref.geteilt)box.append(h("div",{class:"ag-tag"},"geteilt"));}
    else if(x.startsWith("rec:")){const fs=((n.ref||{}).felder||[]).map(f=>f.name).join(", ");if(fs)box.append(h("div",{},fs));
      if(stoEvent(x)){const ag=(SICHT().karten[x]||{}).aggregat;if(ag)box.append(h("div",{class:"ag-tag"},"aus ▣ "+ag+" — hier fix"));}}
    return box;}
  function stoZeit(st,n){if(!n)return 0;const z=k=>(ANSICHT.angelegt||{})[k];
    switch(n.kind){case "buendel":return stoZeit(st,n.own);
      case "handle":{const k="hd:"+n.own.ref.name+"."+(n.ref.event||n.ref.query);return z(k)!=null?z(k):aggZeit(n.ref.event||n.ref.query);}
      case "fn":case "umsetzung":{const k="fn:"+st.name+"."+n.ref.name;return z(k)!=null?z(k):stoFns(st).findIndex(([f])=>f===n.ref);}
      case "readmodel":{const k="rm:"+n.name;return z(k)!=null?z(k):MODEL.readModels.indexOf(n.ref);}
      default:return aggZeit(n.name);}}
  const stoName=n=>!n?"":n.kind==="handle"?(istBuendel(n.ref)?buendelName(n.ref):n.ref.event||n.ref.query||""):n.name||"";
  // Löschen im Store: ein Auf (samt Bündel-Karte; die Projektion fällt mit ihrem letzten Auf weg), eine Frage samt Query, eine Fähigkeit samt
  //   Umsetzung (die Auf und Fragen, die sie nehmen, lassen sie los), ein Read-Model, eine Antwort (fällt aus allen Fragen). Events sind hier fix.
  function stoLoeschPlan(st,I,n,x){const ownerWeg=(o,kind)=>{if(!(o.handles||[]).length){const on=NODEBY.get((kind==="reader"?"rdr:":"prj:")+o._id);if(on)delNode(on);}};
    const handleName=hd=>istBuendel(hd)?"Bündel "+buendelName(hd):hd.event||hd.query;
    const handlePlan=(hd,o,lesen)=>{const letzte=(o.handles||[]).length===1;
      if(!lesen)return {was:"Auf("+handleName(hd)+") in "+o.name,folgen:letzte?["die Projektion "+o.name+" (ihr letztes Auf)"]:[],grund:null,
        tu:()=>{o.handles=(o.handles||[]).filter(z=>z!==hd);ownerWeg(o,"projektion");}};
      const q=hd.query,p=loeschPlanRecord(q,"Query "+q+" samt Frage in "+o.name,u=>u.art==="rfrage"&&u.ref===hd,()=>{
        o.handles=(o.handles||[]).filter(z=>z!==hd);recordWeg(q);(hd.responses||[]).forEach(aggRecordAufraeumen);ownerWeg(o,"reader");});
      if(letzte)p.folgen.push("der Reader "+o.name+" (seine letzte Frage)");return p;};
    if(I.fremd.has(x))return null;
    if(n.kind==="handle")return handlePlan(n.ref,n.own.ref,n.own.kind==="reader");
    if(n.kind==="buendel")return handlePlan(n.ref,n.prj,false);
    if(n.kind==="fn"||n.kind==="umsetzung"){const f=n.ref,fid=f._id,fn=NODEBY.get("fn:"+fid);
      const nutzer=[...MODEL.projektionen,...MODEL.reader].flatMap(o=>(o.handles||[]).filter(hd=>(hd.fns||[]).includes(fid)).map(hd=>handleName(hd)+" in "+o.name+" lässt sie los"));
      return {was:"Fähigkeit "+(f.faehigkeit||f.name)+" samt Umsetzung",folgen:nutzer,grund:fn?null:"die Fähigkeit ist im Modell nicht zu finden",tu:()=>{if(fn)delNode(fn);}};}
    if(n.kind==="readmodel"){const r=n.ref,fns=MODEL.stores.flatMap(s=>stoFns(s).filter(([f])=>nennt(stoTypen(f),r.name)).map(([f])=>s.name+"."+f.name));
      return {was:"Read-Model "+r.name,folgen:[],grund:fns.length?"eine Fähigkeit nennt es in ihrer Signatur: "+fns.join(" · ")+" — dort zuerst ändern":null,
        tu:()=>{const nn=NODEBY.get(x);if(nn)delNode(nn);}};}
    if(x.startsWith("rec:")){const r=n.ref||{};
      if(r.kind==="query"){const hds=MODEL.reader.flatMap(o=>(o.handles||[]).filter(hd=>hd.query===r.name).map(hd=>({hd,o})));
        return hds.length===1?handlePlan(hds[0].hd,hds[0].o,true):loeschPlanRecord(r.name,"Query "+r.name,()=>false,()=>recordWeg(r.name));}
      if(r.kind==="queryresponse")return loeschPlanRecord(r.name,"Antwort "+r.name,u=>u.art==="rantwort",()=>{
        MODEL.reader.forEach(o=>(o.handles||[]).forEach(hd=>{hd.responses=ohneX(hd.responses,r.name);}));recordWeg(r.name);});}
    return null;}
  // Bearbeiten im Store (✎): Handhabung (Rumpf, entfernen), Fähigkeit (Methode, Parameter, Rückgabe), Umsetzung (Rumpf), Read-Model (Name,
  //   Felder, geteilt), Query und Antwort (Name, Felder). Ein Event hat hier kein ✎ — es stammt aus dem Aggregat.
  function stoBearbeiten(st,I,n,x){const box=h("div",{class:"ag-edit"}),t="🗄 "+st.name;
    ["click","dblclick"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));
    const ruempfe=own=>{const rs=[...NODEBY.values()].filter(r=>r.kind==="rumpf"&&r.own&&r.own.id===own);
      rs.forEach(r=>{const b=h("div",{class:"gbody"});INSP=true;try{fuelleKoerper(b,r);}finally{INSP=false;}box.append(b);});
      if(!rs.length)box.append(h("div",{class:"ag-tag"},"Rumpf: Platzhalter — nach dem Schreiben hier ✎ bzw. 🤖"));};
    if(n.kind==="buendel"){const hd=n.ref,b=hd.buendel||{};
      box.append(h("div",{class:"ag-tag"},"Projektion "+n.prj.name+" · gerufen, sobald zum Schlüssel von jedem Event eines im Log steht — je Typ das neueste"));
      const sel=h("select",{title:"Schlüssel: abgeleitet aus den Events oder [Ueber<T>] am Parameter",onchange:e=>{const u=e.target.value||null;
        buendelSetze(hd,buendelEvents(hd),u);gTitel(t+": Bündel "+buendelName(hd)+(u?" über "+u:" mit abgeleitetem Schlüssel"));render();}},
        // Voll qualifiziert geschrieben: ein einfacher Name träfe in einem gleichnamigen Namespace den Namespace statt das Aggregat.
        h("option",{value:""},"🔑 abgeleitet"),...MODEL.aggregate.map(A=>h("option",{value:A.namespace+"."+A.name},"🔑 über "+A.name)));
      const kurz=x=>String(x||"").split(".").pop();sel.value=b.ueber?([...sel.options].find(o=>o.value&&kurz(o.value)===kurz(b.ueber))||{}).value||"":"";
      box.append(h("div",{class:"ag-feld"},sel));
      return box;}
    if(n.kind==="handle"){const hd=n.ref,o=n.own.ref,lesen=n.own.kind==="reader";
      box.append(h("div",{class:"ag-tag"},(lesen?"Reader ":"Projektion ")+o.name));ruempfe(x);
      return box;}
    if(n.kind==="fn"){const f=n.ref,neu=!f.sig;
      box.append(h("div",{class:"ag-feld"},h("input",{value:f.name,spellcheck:"false",disabled:!neu,title:neu?"Methode":"Methode — aus dem Code",
        onchange:e=>{const m=e.target.value.trim();if(!/^[A-Z][A-Za-z0-9_]*$/.test(m)||stoFns(st).some(([g])=>g!==f&&g.name===m)){deFlash("⚠ "+m+": kein freier Methodenname",false);render();return;}
          aggStempelUmbenennen("fn:"+st.name+"."+f.name,"fn:"+st.name+"."+m);f.name=m;f.faehigkeit="I"+m.replace(/Async$/,"");gTitel(t+": Fähigkeit "+f.faehigkeit);render();}})));
      box.append(h("div",{class:"ag-tag"},"Parameter"),aggFelder(f.params=f.params||[],null,t+": "+f.name));
      if(n.lesen)box.append(h("div",{class:"ag-feld"},h("input",{value:f.rueckgabe||((f.sig||{}).rueckgabe||"").replace(/^Task<(.*)>$/,"$1"),list:"de-typen",spellcheck:"false",
        title:"Rückgabe (ohne Task) — nennt sie ein Read-Model, hängt die Kante zu ihm",onchange:e=>{f.rueckgabe=e.target.value.trim();gTitel(t+": "+f.name+" → "+f.rueckgabe);render();}})));
      return box;}
    if(n.kind==="umsetzung"){const k=(st.klassen||[])[0];box.append(h("div",{class:"ag-tag"},(st.impl&&st.impl.name||"neue Umsetzung")+(k?" · Store-Klasse "+k:"")));
      ruempfe("fn:"+n.ref._id);return box;}
    if(n.kind==="readmodel"){const r=n.ref;
      box.append(h("div",{class:"ag-feld"},h("input",{value:r.name,spellcheck:"false",title:"Name",onchange:e=>{const neu=e.target.value.trim(),f=nameFehler(neu);if(neu===r.name)return;
        if(f||MODEL.readModels.some(o=>o.name===neu)){deFlash("⚠ "+(f||neu+" gibt es schon"),false);render();return;}
        aggStempelUmbenennen("rm:"+r.name,"rm:"+neu);if(r._ort)gUmbenennen(r,neu,r.name);else{r.name=neu;render();}}})));
      box.append(aggFelder(r.felder=r.felder||[],null,t+": "+r.name));
      const c=h("input",{type:"checkbox",onchange:e=>{r.geteilt=e.target.checked;gTitel(t+": "+r.name+(r.geteilt?" geteilt":" eigen"));render();}});c.checked=!!r.geteilt;
      box.append(h("label",{class:"ag-flag"},c," geteilt (mehrere Streams schreiben hinein)"));
      return box;}
    if(x.startsWith("rec:")){const r=n.ref;
      box.append(h("div",{class:"ag-feld"},h("input",{value:r.name,spellcheck:"false",title:"Name",onchange:e=>{const neu=e.target.value.trim(),f=nameFehler(neu);if(neu===r.name)return;
        if(f){deFlash("⚠ "+f,false);render();return;}aggStempelUmbenennen(r.name,neu);if(r._ort)gUmbenennen(r,neu,r.name);else{recUmbenennenLokal(r,neu);render();}}})));
      box.append(aggFelder(r.felder=r.felder||[],null,t+": "+r.name));
      return box;}
    return box;}
  // Ein neuer Store (Fläche, Kopf der Spalte Projektionen): erst die Store-Klasse (sie wählt die Basis der Umsetzung, Storeklassen), dann der
  //   Bereich und der Name; danach öffnet er sich von innen — Projektion und Reader entstehen dort mit dem ersten Auf bzw. der ersten Query.
  const STORE_KLASSE={1:["atomar","Wirkung und Fortschritt in einer Transaktion (Marten)"],2:["wiederholungsfest","jede Wirkung ergibt wiederholt dasselbe (Datei-Ziel)"],
    3:["mindestens einmal","ohne Zusage (Datei-Senke) — die Projektion trägt [MindestensEinmal]"]};
  function stoNeuAnlegen(ev){const x=ev.clientX,y=ev.clientY;
    menueFix(x,y,"Neuer Store — Klasse",Object.entries(STORE_KLASSE).map(([k,[t,u]])=>({t:k+" · "+t,title:u,fn:()=>{
      const nss=[...new Set(MODEL.aggregate.map(a=>a.namespace))].sort();
      const frei=ns=>{const b="I"+ns.split(".").pop();let n=b+"Store",i=2;while(MODEL.stores.some(s=>s.name===n))n=b+(i++)+"Store";return n;};
      const weiter=ns=>frageFix(x,y,"＋ Store (Klasse "+k+")",frei(ns),"Bündel der Fähigkeiten; die Umsetzung entsteht mit",n=>{
        if(!/^I[A-Z][A-Za-z0-9_]*$/.test(n))return "Ein Schnittstellenname: I und groß weiter";if(MODEL.stores.some(s=>s.name===n)||recNamens(n))return n+" gibt es schon";
        MODEL.stores.push({_id:"store:"+n,name:n,namespace:ns,writeFns:[],readFns:[],klassen:[+k]});gTitel("🗄 Store "+n+" (Klasse "+k+")");render();komKarte("sto:store:"+n,true);});
      if(nss.length<=1)weiter(nss[0]||defaultNsGlobal());else menueFix(x,y,"Bereich",nss.map(ns=>({t:ns,fn:()=>weiter(ns)})));}})));}
  const I_ERGEBNIS=(A,nm)=>MODEL.decider.some(d=>d.aggregat===A.name&&(d.ergibt||[]).some(o=>o.event===nm));
  // ══ GESTEN DER FLÄCHE UND DER KOMPOSITION (docs/10 §10.9): Ziehen von einem Port auf eine Karte bzw. einen Port,
  //   Klick auf eine Kante zum Lösen, Ziehen aus dem Baukasten. Jede Geste ändert das Modell; der Server schreibt den Code und schickt das neue Board.
  // Ein Menü an der Mausposition, über allem (auch über dem Kompositions-Modal).
  function menueFix(cx,cy,titel,items){document.querySelectorAll("#de .gmenue.fix").forEach(p=>p.remove());
    const m=h("div",{class:"gpick gmenue fix"});if(titel)m.append(h("div",{class:"gpick-t"},titel));
    items.filter(Boolean).forEach(it=>{if(it.trenn){m.append(h("div",{class:"gm-tr"}));return;}if(it.hinweis){m.append(h("div",{class:"gm-hint"},it.hinweis));return;}
      if(it.kopf){m.append(h("div",{class:"gpick-t"},it.kopf));return;}
      const b=h("button",{class:it.gefahr?"gm-gefahr":"",title:it.title||"",onclick:e=>{e.stopPropagation();m.remove();if(!it.aus)it.fn(e);}},it.t);if(it.aus)b.disabled=true;m.append(b);});
    // Lange Listen (etwa alle Events für ein Auf): oben ein Filter.
    const knoepfe=[...m.querySelectorAll("button")];
    if(knoepfe.length>14){const f=h("input",{placeholder:"filtern …",spellcheck:"false"});
      f.oninput=()=>{const q=f.value.trim().toLowerCase();knoepfe.forEach(b=>{b.style.display=!q||b.textContent.toLowerCase().includes(q)?"":"none";});
        m.querySelectorAll(".gpick-t").forEach((k,i)=>{if(i>0||!titel)k.style.display=q?"none":"";});};
      f.onkeydown=e=>{e.stopPropagation();if(e.key==="Escape")m.remove();if(e.key==="Enter"){const b=knoepfe.find(b=>b.style.display!=="none");if(b)b.click();}};
      m.insertBefore(f,titel?m.children[1]:m.firstChild);setTimeout(()=>f.focus(),0);}
    m.style.left=Math.max(4,Math.min(cx,innerWidth-280))+"px";m.style.top=Math.max(4,Math.min(cy,innerHeight-240))+"px";
    ["pointerdown","dblclick","wheel","click","contextmenu"].forEach(ev=>m.addEventListener(ev,e=>e.stopPropagation()));
    document.getElementById("de").append(m);
    setTimeout(()=>{const off=ev=>{if(!m.contains(ev.target)){m.remove();document.removeEventListener("pointerdown",off,true);}};document.addEventListener("pointerdown",off,true);},0);}
  const akteurVonKarte=fk=>MODEL.akteure.find(a=>"akt:"+a._id===fk.karte);
  const projektionVonKarte=fk=>MODEL.projektionen.find(p=>"prj:"+p._id===fk.karte);

  // Klick auf eine Kante: lösen, was der Code als Liste trägt (darf, hört, Auf einer Projektion); den Typ öffnen.
  function flKanteMenue(k,ev){const V=FLKARTE.get(k.von),N=FLKARTE.get(k.nach);if(!V||!N)return;const items=[];
    if(V.art==="akteur"&&V.spalte==="ausgang"){const a=akteurVonKarte(V);
      if(a&&(a.darf||[]).includes(k.typ))items.push({t:"✕ "+a.name+" darf "+k.typ+" nicht mehr",gefahr:1,fn:()=>{a.darf=ohneX(a.darf,k.typ);gTitel("👤 "+a.name+" darf "+k.typ+" nicht mehr");render();}});}
    if(N.art==="akteur"&&N.spalte==="eingang"&&(k.art==="kenntnis"||k.art==="arbeit")){const a=akteurVonKarte(N);
      if(a&&(a.hoert||[]).includes(k.typ))items.push({t:"✕ "+a.name+" hört "+k.typ+" nicht mehr",gefahr:1,fn:()=>{a.hoert=ohneX(a.hoert,k.typ);gTitel("👤 "+a.name+" hört "+k.typ+" nicht mehr");render();}});}
    if(N.art==="projektion"&&k.art==="schreiben"){const p=projektionVonKarte(N);
      if(p&&(p.handles||[]).some(x=>x.event===k.typ))items.push({t:"✕ "+p.name+" hört "+k.typ+" nicht mehr (Auf entfernen)",gefahr:1,fn:()=>{p.handles=p.handles.filter(x=>x.event!==k.typ);gTitel(p.name+": Auf("+k.typ+") entfernt");render();}});}
    if(!items.length)items.push({hinweis:"Folgt aus den Signaturen — ändern im Panel bzw. in der Komposition (⧉)."});
    items.push({trenn:1},NODEBY.has("rec:"+komBasis(k.typ))?{t:"◎ "+k.typ+" im Panel",fn:()=>waehle("rec:"+komBasis(k.typ))}:null);
    menueFix(ev.clientX,ev.clientY,k.typ+" · "+k.art+" · "+V.name+" → "+N.name,items);}

  // ══ KATALOG-MODAL (docs/konzept-funktionen-im-editor.md, Stufe 1): die Funktionswelt ohne Domäne. Links die Kataloge mit ihren Funktionen und
  //   Graphen, in der Mitte die gewählte Funktion als Reihe — Input-Command ─▶ ƒ ─▶ Output-Commands (mehrere = ein OneOf, je Fall ein Port), je
  //   Input ein Auf —, rechts die Command-Liste des Katalogs und die des allgemeinen Katalogs (Commands, die alle Kataloge teilen). Input und Output
  //   sind ein neuer Command oder einer aus diesen Listen; was neu entsteht, landet in der Liste. Ein Command aus dem allgemeinen Katalog setzt die
  //   Projektreferenz (der Katalog entsteht beim ersten Gebrauch). Pfad-Element „kat:<Katalog>“, die gewählte Funktion in KOM.katFn (Name).
  const katAllg=()=>(MODEL.rahmen||{}).allgemeinerKatalog||null;
  const katListe=()=>[...new Set([...(komBaukasten().kataloge||[]),...MODEL.bausteinFunktionen.map(funKatalog).filter(Boolean)])].sort();
  const katFunktionen=kat=>MODEL.bausteinFunktionen.filter(f=>!istGerufen(f)&&funKatalog(f)===kat).sort((a,b)=>a.name.localeCompare(b.name));
  const katCommands=kat=>MODEL.records.filter(r=>["command","rejection"].includes(r.kind)&&funKatalog(r)===kat).sort((a,b)=>a.name.localeCompare(b.name));
  // Die Commands, die die Quellen des Framework-Katalogs melden (DateiGefunden, Takt, Aufruf) — nur lesend, als Input wählbar.
  const katFramework=()=>[...new Set(komBaukasten().katalog.flatMap(k=>k.meldet||[]))].sort();
  const katBereich=(f,kat)=>{const ns=String(f&&f.namespace||"");return ns.startsWith(kat+".")?ns.slice(kat.length+1).split(".")[0]:null;};
  function katOeffne(kat,fn){const id="kat:"+(kat||katListe()[0]||"");
    if(KOM){if(KOM.pfad[KOM.pfad.length-1]!==id)KOM.pfad.push(id);}else KOM={pfad:[id],reiter:"katalog",wahl:null};
    KOM.katFn=fn||null;KOM.port=null;komZeige();}
  window.deKataloge=()=>katOeffne(null,null);
  // Wer einen Command nimmt bzw. gibt (über alle Funktionen) — für die Liste.
  function katNutzung(nm){let nimmt=0,gibt=0;MODEL.bausteinFunktionen.forEach(f=>{(f.eingaenge||[]).forEach(e=>{if(e.eingang===nm)nimmt++;if((e.ausgaben||[]).includes(nm))gibt++;});
    if((f.lauscht||[]).includes(nm))gibt++;});return {nimmt,gibt};}
  // Erst die Referenz auf den allgemeinen Katalog (Geste am Server, lädt kalt neu), dann die Änderung am frischen Modell: dran(f) bekommt die
  //   Funktion aus dem neuen Board. Liegt die Funktion im allgemeinen Katalog oder nutzt ihr Katalog ihn schon, sofort.
  async function katMitAllgemein(fname,kat,dran){const allg=katAllg(),frisch=()=>MODEL.bausteinFunktionen.find(x=>x.name===fname)||null;
    if(!allg)return;const nutzt=((MODEL.rahmen||{}).katalogNutzt||{})[kat]||[];
    if(kat===allg||(katListe().includes(allg)&&nutzt.includes(allg))){dran(frisch());return;}
    if(await gKatalog({name:allg,nutzer:kat},kat+" nutzt "+allg))dran(frisch());}
  // Der allgemeine Katalog für sich (ohne Nutzer): anlegen, wenn es ihn noch nicht gibt.
  async function katAllgemeinDa(){const allg=katAllg();if(!allg)return false;if(katListe().includes(allg))return true;return await gKatalog({name:allg},"Katalog "+allg);}
  function katRecordNeu(ns,n,kind,amFunktion){MODEL.records.push({name:n,kind,namespace:ns,felder:[],istErzeugung:false,...(amFunktion?{amFunktion}:{})});aggStempel(n);}
  // ＋ Input bzw. ＋ Output: ein neuer Command (hier oder im allgemeinen Katalog) oder einer aus den Listen; ein Output auch ein Hinweis.
  function katWahl(f,kat,ev,art,e){const t="ƒ "+f.name,allg=katAllg(),stamm=f.name.replace(/^I(?=[A-ZÄÖÜ])/,""),fname=f.name;
    const schon=nm=>art==="input"?(f.eingaenge||[]).some(x=>x.eingang===nm):(e.ausgaben||[]).includes(nm);
    const setze=(fx,nm)=>{if(!fx)return;if(art==="input"){(fx.eingaenge=fx.eingaenge||[]).push({eingang:nm,ausgaben:[]});gTitel(t+": Auf("+nm+")");}
      else{const ex=(fx.eingaenge||[]).find(x=>x.eingang===e.eingang);if(!ex)return;ex.ausgaben=[...(ex.ausgaben||[]),nm];gTitel(t+": Auf("+e.eingang+") gibt "+nm+" aus");}
      render();};
    const neu=(titel,vorschlag,kind,allgemein)=>({t:titel,fn:()=>setTimeout(()=>frageFix(ev.clientX,ev.clientY,titel.replace(/ …$/,""),vorschlag,
      allgemein?"Landet in "+allg+" — jeder Katalog darf ihn nutzen":"Landet in der Command-Liste von "+kat,n=>{const fe=nameFehler(n);if(fe)return fe;
        if(!allgemein){katRecordNeu(f.namespace,n,kind,f.name);setze(f,n);return;}
        const bereich=katBereich(f,kat)||"Gemeinsam";
        setTimeout(()=>katMitAllgemein(fname,kat,fx=>{katRecordNeu(allg+"."+bereich,n,kind);setze(fx,n);}),0);}),0)});
    const vorhanden=(liste,allgemein)=>liste.filter(r=>!schon(r.name)&&(art==="output"||r.kind==="command")).map(r=>({t:(r.kind==="rejection"?"✉ ":"▶ ")+r.name,
      fn:()=>allgemein?katMitAllgemein(fname,kat,fx=>setze(fx,r.name)):setze(f,r.name)}));
    const hier=vorhanden(katCommands(kat),false),dort=allg&&allg!==kat?vorhanden(katCommands(allg),true):[];
    const fw=art==="input"?katFramework().filter(nm=>!schon(nm)).map(nm=>({t:"⚡ "+nm,fn:()=>setze(f,nm)})):[];
    menueFix(ev.clientX,ev.clientY,(art==="input"?"Input von ":"Output von ")+f.name+(art==="output"?".Auf("+e.eingang+")":""),[
      neu("＋ neuer Command …",stamm+(art==="input"?"Auftrag":"Ergebnis"),"command",false),
      ...(art==="output"?[neu("＋ neuer Hinweis …",stamm+"Unmoeglich","rejection",false)]:[]),
      ...(allg&&allg!==kat?[neu("＋ neuer Command im allgemeinen Katalog …",stamm+(art==="input"?"Auftrag":"Ergebnis"),"command",true)]:[]),
      ...(hier.length?[{trenn:1},{kopf:"aus "+kat},...hier.slice(0,40)]:[]),
      ...(dort.length?[{trenn:1},{kopf:"aus dem allgemeinen Katalog"},...dort.slice(0,40)]:[]),
      ...(fw.length?[{trenn:1},{kopf:"vom Framework (Quellen)"},...fw]:[])]);}
  // Umbenennen: ein geschriebener Command über den Renamer (alle Verweise im Code), ein neuer im Modell.
  function katUmbenennen(r,neu){const fe=nameFehler(neu);if(neu===r.name)return;if(fe){deFlash("⚠ "+fe,false);render();return;}
    aggStempelUmbenennen(r.name,neu);if(r._ort)gUmbenennen(r,neu,r.name);else funRecUmbenennen(r,neu);}
  // Eine Command-Karte: Name, Felder; ✎ öffnet Name, Felder und Command ⇄ Hinweis; ✕ (loese) nimmt sie aus der Funktion bzw. löscht sie.
  function katCmdKarte(nm,kat,opt){const r=recByName(nm),key="kc:"+(opt.ort||"")+nm,offen=!!r&&KOM.edit.has(key),allg=katAllg(),rk=r?funKatalog(r):null;
    const herkunft=!r?"Framework":rk&&rk!==kat?(rk===allg?"allgemein":rk):"";
    const el=h("div",{class:"kat-cmd"+(r&&r.kind==="rejection"?" hinweis":"")+(offen?" offen":"")+(opt.leise?" leise":"")},
      h("div",{class:"kat-cmd-k"},h("span",{class:"kat-cmd-n",title:r?(r.kind==="rejection"?"Hinweis (flüchtig, an Akteure)":"Command ohne Ziel")+" · "+r.namespace:"vom Framework"},
          (r&&r.kind==="rejection"?"✉ ":"")+nm),
        herkunft?h("span",{class:"kat-herk"},herkunft):null,opt.mult?h("span",{class:"kat-mult",title:"Vielfachheit: 1 genau eins · 0..1 einer der Fälle · 0..n Strom"},opt.mult):null,
        opt.zusatz||null,
        r?h("button",{class:"ag-stift"+(offen?" an":""),title:offen?"Bearbeiten schließen":"Name und Felder bearbeiten",
          onclick:ev=>{ev.stopPropagation();if(offen)KOM.edit.delete(key);else KOM.edit.add(key);komZeige();}},offen?"▾":"✎"):null,
        opt.loese?h("button",{class:"ag-loesch",title:opt.loeseText||"Lösen",onclick:ev=>{ev.stopPropagation();opt.loese(ev);}},"✕"):null));
    if(r&&!offen&&(r.felder||[]).length)el.append(h("div",{class:"kat-felder"},r.felder.map(fe=>fe.name+" : "+fe.typ).join(" · ")));
    if(offen){const box=h("div",{class:"ag-edit"});["click","dblclick"].forEach(x=>box.addEventListener(x,e=>e.stopPropagation()));
      box.append(h("div",{class:"ag-feld"},h("input",{value:r.name,spellcheck:"false",title:"Name",onchange:e=>katUmbenennen(r,e.target.value.trim())})));
      box.append(aggFelder(r.felder=r.felder||[],null,"📚 "+r.name));
      box.append(h("button",{class:"ag-plus",title:"Command ohne Ziel (ein Knoten nimmt ihn) oder Hinweis (flüchtig, an Akteure)",
        onclick:()=>{r.kind=r.kind==="command"?"rejection":"command";gTitel("📚 "+r.name+(r.kind==="command"?" ist ein Command":" ist ein Hinweis"));render();}},
        r.kind==="command"?"⇄ zum Hinweis machen":"⇄ zum Command machen"));
      el.append(box);}
    return el;}
  // Die gewählte Funktion als Reihe: je Auf eine Zeile Input ─▶ │ƒ│ ─▶ Outputs; die Funktionskarte steht einmal über alle Zeilen.
  function katFunktionEl(f,kat){const t="ƒ "+f.name,aufs=f.eingaenge||[],zeilen=Math.max(1,aufs.length)+((f.lauscht||[]).length?1:0);
    const g=h("div",{class:"kat-reihe"});g.style.gridTemplateRows="repeat("+(zeilen+1)+",auto)";
    const ctor=ctorVon(f,null)||[],chip=p=>{const typ=String(p.typ||"").trim(),sym=gerufenByName(typ)?"⚙ ":/^IZustand</.test(typ)?"◫ ":/^ILogger</.test(typ)?"✎ ":recByName(komBasis(typ))?"▤ ":"";
      return h("span",{class:"kat-chip",title:"Konstruktor: "+typ},sym+typ);};
    const verwendet=(MODEL.graphen||[]).filter(x=>(x.knoten||[]).some(k=>komBasis(k.typ)===f.name));
    const fk=h("div",{class:"kat-fn"},h("div",{class:"kat-fn-n"},"ƒ "+f.name),h("div",{class:"kat-fn-u"},f.namespace),
      h("div",{class:"kat-chips"},...ctor.map(chip)),
      h("div",{class:"kat-fn-u"},verwendet.length?"in "+verwendet.map(x=>"⬡ "+x.name).join(", "):"in keinem Graphen"),
      h("div",{class:"kat-akte"},h("button",{class:"ag-plus",title:"Konfig, Technik, Logger im Konstruktor",onclick:ev=>menueFix(ev.clientX,ev.clientY,"Rahmen von "+f.name,funRahmenItems(f))},"＋ Rahmen ▾"),
        h("button",{class:"ag-plus",title:"Das ausführliche Funktions-Modal: Strom, asynchron, Herkunft, Rümpfe, Rahmen",onclick:()=>komKarte("fb:"+f._id,true)},"⤢ ausführlich")));
    fk.style.gridRow="1 / span "+zeilen;fk.style.gridColumn="3";g.append(fk);
    const zelle=(el,zeile,spalte)=>{el.style.gridRow=String(zeile);el.style.gridColumn=String(spalte);g.append(el);};
    aufs.forEach((e,i)=>{const z=i+1;
      zelle(katCmdKarte(e.eingang,kat,{ort:"i:",loeseText:"Diesen Input (sein Auf) aus "+f.name+" nehmen — der Command bleibt in der Liste",
        loese:()=>{f.eingaenge=(f.eingaenge||[]).filter(x=>x!==e);gTitel(t+": ohne Auf("+e.eingang+")");render();}}),z,1);
      zelle(h("div",{class:"kat-pfeil"},"─▶"),z,2);zelle(h("div",{class:"kat-pfeil"},"─▶"),z,4);
      const aus=h("div",{class:"kat-aus"}),m=funMult(e);
      (e.ausgaben||[]).forEach(o=>aus.append(katCmdKarte(o,kat,{ort:"o:"+e.eingang+":",mult:m,loeseText:"Diesen Output aus Auf("+e.eingang+") nehmen — der Command bleibt in der Liste",
        loese:()=>{e.ausgaben=ohneX(e.ausgaben,o);gTitel(t+": Auf("+e.eingang+") gibt "+o+" nicht mehr aus");render();}})));
      aus.append(h("button",{class:"kat-neu",title:"Ein Output: neuer Command oder Hinweis, oder einer aus den Listen — mehrere sind ein OneOf",
        onclick:ev=>katWahl(f,kat,ev,"output",e)},"＋ Output"));
      const schalter=(text,an,titel,setze)=>{const c=h("input",{type:"checkbox",onchange:ev=>{setze(ev.target.checked);render();}});c.checked=!!an;return h("label",{class:"ag-flag",title:titel},c," "+text);};
      aus.append(h("div",{class:"kat-form"},schalter("Strom (0..n)",e.strom,"Keine bis viele je Aufruf (IEnumerable) — sonst genau einer bzw. bei mehreren einer der Fälle (OneOf)",
          v=>{e.strom=v;gTitel(t+": Auf("+e.eingang+")"+(v?" als Strom":" ohne Strom"));}),
        schalter("asynchron",e.asynchron,"Task bzw. IAsyncEnumerable",v=>{e.asynchron=v;gTitel(t+": Auf("+e.eingang+")"+(v?" asynchron":" synchron"));})));
      zelle(aus,z,5);});
    if(!aufs.length){zelle(h("button",{class:"kat-neu gross",title:"Der Input der Funktion: ein neuer Command oder einer aus den Listen",onclick:ev=>katWahl(f,kat,ev,"input")},"＋ Input"),1,1);
      zelle(h("div",{class:"kat-pfeil"},"─▶"),1,2);zelle(h("div",{class:"kat-pfeil leise"},"─▶"),1,4);
      zelle(h("div",{class:"kat-aus"},h("div",{class:"kom-wo"},"Outputs folgen, sobald es einen Input gibt.")),1,5);}
    if((f.lauscht||[]).length){const z=Math.max(1,aufs.length)+1;zelle(h("div",{class:"kat-cmd leise"},h("div",{class:"kat-cmd-k"},"lauscht (Quelle)")),z,1);
      zelle(h("div",{class:"kat-pfeil"},"─▶"),z,4);zelle(h("div",{class:"kat-aus"},...f.lauscht.map(o=>katCmdKarte(o,kat,{ort:"l:",mult:"0..n"}))),z,5);}
    if(aufs.length)zelle(h("button",{class:"kat-neu",title:"Ein weiterer Input — ein eigenes Auf mit eigenen Outputs",onclick:ev=>katWahl(f,kat,ev,"input")},"＋ Input"),zeilen+1,1);
    return g;}
  // Die Command-Liste eines Katalogs: Nutzung je Command, ✎ zum Bearbeiten, ✕ mit Löschplan, ＋ Command.
  function katListeEl(titel,kat,f,allgemein){const box=h("div",{});box.append(h("div",{class:"kom-sec"},titel));const liste=kat?katCommands(kat):[];
    if(!liste.length)box.append(h("div",{class:"kom-wo"},allgemein&&!katListe().includes(kat)?"Entsteht beim ersten Gebrauch.":"Noch keine Commands."));
    liste.forEach(r=>{const n=katNutzung(r.name);
      box.append(katCmdKarte(r.name,kat,{ort:"l:",zusatz:h("span",{class:"kat-nutz",title:"nimmt: so viele Auf nehmen ihn · gibt: so viele geben ihn aus"},
          (n.nimmt?"nimmt "+n.nimmt:"")+(n.nimmt&&n.gibt?" · ":"")+(n.gibt?"gibt "+n.gibt:"")+(!n.nimmt&&!n.gibt?"unverbunden":"")),
        loeseText:"Den Command löschen (nur, wenn ihn keine Funktion mehr nimmt oder gibt)",
        loese:ev=>{const plan=loeschPlanRecord(r.name,(r.kind==="rejection"?"Hinweis ":"Command ")+r.name,()=>false,()=>recordWeg(r.name));
          if(plan.grund){menueFix(ev.clientX,ev.clientY,"✕ "+plan.was,[{hinweis:"Nicht löschbar — "+plan.grund}]);return;}
          menueFix(ev.clientX,ev.clientY,"✕ "+plan.was+" löschen?",[...plan.folgen.map(x=>({hinweis:"dazu: "+x})),{t:"✕ löschen",gefahr:1,fn:()=>{gTitel("✕ "+plan.was);plan.tu();render();}}]);}}));});
    // ＋ Command: in den Bereich der gewählten Funktion; ohne sie fragt der Bereich zuerst.
    box.append(h("button",{class:"kat-neu",title:"Ein neuer Command in "+kat+" — er landet in dieser Liste",onclick:ev=>{
      const anlegen=bereich=>setTimeout(()=>frageFix(ev.clientX,ev.clientY,"＋ Command in "+kat+"."+bereich,"","Ein Command ohne Ziel (Felder danach mit ✎)",n=>{
        const fe=nameFehler(n);if(fe)return fe;const tu=()=>{katRecordNeu(kat+"."+bereich,n,"command");gTitel("📚 ＋ "+n+" in "+kat);render();};
        if(allgemein)setTimeout(async()=>{if(await katAllgemeinDa())tu();},0);else tu();}),0);
      const b=katBereich(f,funKatalog(f||{})||"")||(liste[0]&&katBereich(liste[0],kat));
      if(b){anlegen(b);return;}
      frageFix(ev.clientX,ev.clientY,"＋ Command in "+kat+" — Bereich","Gemeinsam","Der Ordner im Katalog ("+kat+".<Bereich>)",x=>{
        if(!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(x))return "Ein Bereich: groß beginnen, nur Buchstaben und Ziffern";anlegen(x);});}},"＋ Command"));
    return box;}
  function katZeige(de,kat){KOM.edit=KOM.edit||new Set();KOM.zu=KOM.zu||new Set();const alle=katListe(),allg=katAllg();
    if(kat&&!alle.includes(kat)&&kat!==allg)kat=alle[0]||"";
    const fns=kat?katFunktionen(kat):[],f=fns.find(x=>x.name===KOM.katFn)||null;
    const m=komRahmen(kat?"Funktionen anlegen: Input ─▶ ƒ ─▶ Outputs · jeder neue Command landet rechts in der Liste · ✎ bearbeiten · ✕ lösen bzw. löschen"
      :"Noch kein Katalog — ＋ 📚 Katalog legt einen an (ein eigenes Projekt, ohne Domäne)");
    // Links: Kataloge, Funktionen und Graphen des gewählten.
    const links=h("div",{class:"kom-bk"},h("div",{class:"kom-sec"},"Kataloge"));
    alle.forEach(k=>links.append(h("button",{class:"kom-b"+(k===kat?" on":""),title:k===allg?"Der allgemeine Katalog: Commands, die alle Kataloge teilen":k,
      onclick:()=>{KOM.pfad[KOM.pfad.length-1]="kat:"+k;KOM.katFn=null;komZeige();}},"📚 "+k.replace(/\.Katalog$/,"")+(k===allg?" · allgemein":""))));
    links.append(h("div",{class:"kom-akte"},h("button",{class:"kom-akt",title:"Ein neuer Katalog: ein eigenes Projekt <Name>.Katalog, ohne Domäne",onclick:e=>katalogNeu(e)},"＋ 📚 Katalog")));
    if(kat){links.append(h("div",{class:"kom-sec"},"Funktionen"));
      if(!fns.length)links.append(h("div",{class:"kom-wo"},"Noch keine."));
      fns.forEach(x=>{const ein=(x.eingaenge||[]).map(e=>e.eingang),aus=[...new Set((x.eingaenge||[]).flatMap(e=>e.ausgaben||[]))];
        links.append(h("button",{class:"kom-b"+(x===f?" on":""),title:(ein.join(", ")||"kein Input")+" → "+(aus.join(" | ")||"kein Output"),
          onclick:()=>{KOM.katFn=x.name;komZeige();}},"ƒ "+x.name));});
      links.append(h("div",{class:"kom-akte"},h("button",{class:"kom-akt",title:"Eine neue Funktion in "+kat+" (Bereich und Name)",
        onclick:e=>katalogFunktion(kat,e,fb=>{KOM.katFn=fb.name;komZeige();})},"＋ ƒ Funktion")));
      const grs=(komBaukasten().graphen||[]).filter(x=>{const g=(MODEL.graphen||[]).find(y=>"gr:"+y._id===x.karte);return g&&funKatalog(g)===kat;});
      links.append(h("div",{class:"kom-sec"},"Graphen"));
      if(!grs.length)links.append(h("div",{class:"kom-wo"},"Noch keine."));
      grs.forEach(x=>links.append(h("button",{class:"kom-b",title:"Den Graphen öffnen",onclick:()=>{KOM.pfad.push(x.karte);KOM.wahl=null;komZeige();}},"⬡ "+x.name)));}
    // Mitte: die gewählte Funktion.
    const mitte=h("div",{class:"kat-mitte"});
    if(f)mitte.append(katFunktionEl(f,kat));
    else if(kat)mitte.append(h("div",{class:"kat-leer"},fns.length?"Links eine Funktion wählen — oder eine neue anlegen.":"Dieser Katalog hat noch keine Funktion.",
      h("div",{class:"kom-akte"},h("button",{class:"ag-plus",onclick:e=>katalogFunktion(kat,e,fb=>{KOM.katFn=fb.name;komZeige();})},"＋ ƒ Funktion"))));
    // Rechts: die Command-Liste des Katalogs und die des allgemeinen.
    const rechts=h("div",{class:"kom-bk kat-rechts"});
    if(kat){rechts.append(katListeEl("Commands · "+kat.replace(/\.Katalog$/,""),kat,f,kat===allg));
      if(allg&&allg!==kat)rechts.append(katListeEl("Allgemein · geteilt",allg,null,true));
      const fw=katFramework();if(fw.length){rechts.append(h("div",{class:"kom-sec"},"Framework · Quellen"));fw.forEach(nm=>rechts.append(katCmdKarte(nm,kat,{ort:"fw:"})));}}
    m.append(h("div",{class:"kom-leib kat"},links,mitte,rechts));de.append(m);}
  // ══ FUNKTION VON INNEN (docs/plan-katalog-und-graphen.md, Schritt 3): der Vertrag einer Funktion — Eingänge · Auf · Ausgänge · Rahmen — mit
  //   derselben Mechanik wie Aggregat und Store (innenZeige: Detailstufen, ✎, ＋ am Port, gestrichelte Karten, ✕ mit Löschplan). Eingang ↔ Auf ist 1:1
  //   (je Eingangstyp ein Auf, R2), Auf → Ausgänge frei; der Rahmen ist der Konstruktor (Konfig, Technik, Zustand, Logger). Die Form der Ausgabe folgt
  //   aus Strom und Zahl der Ausgänge (1 · 0..1 · 0..n), dazu „asynchron“. Was im Katalog liegt, kennt keine Domäne: dort gibt es keinen Command an
  //   ein Aggregat und keine Antwort; Lese-Fähigkeiten nimmt nur eine Funktion der Anwendung.
  const funModell=id=>MODEL.bausteinFunktionen.find(f=>"fb:"+f._id===id);
  const funKatalog=f=>{const ns=String(f.namespace||""),i=ns.indexOf(".Katalog");return i>0&&(ns.length===i+8||ns[i+8]===".")?ns.slice(0,i+8):null;};
  const funMult=e=>e.strom?"0..n":(e.ausgaben||[]).length>1?"0..1":"1";
  const funAufId=(f,e)=>"fa:"+f._id+":"+e.eingang;
  // Wer erzeugt eine Nachricht bzw. nimmt sie (über alle Funktionen, Decides, Fragen, Akteure)? — für „unverbunden“ und die Auswahl.
  const funGenommen=nm=>MODEL.bausteinFunktionen.some(f=>(f.eingaenge||[]).some(e=>e.eingang===nm))||MODEL.decider.some(d=>d.command===nm)
    ||MODEL.akteure.some(a=>(a.hoert||[]).includes(nm))||(MODEL.graphen||[]).some(g=>(g.koepfe||[]).includes(nm));
  function funInnen(id,f){const glieder=new Map(),knoten=new Map(),kanten=[],unverbunden=new Set(),rec=nm=>"rec:"+nm;
    (f.eingaenge||[]).forEach(e=>{const a=funAufId(f,e);knoten.set(a,{id:a,kind:"auf",name:e.eingang,ref:e,f});glieder.set(a,1);
      if(NODEBY.has(rec(e.eingang))){glieder.set(rec(e.eingang),0);kanten.push([rec(e.eingang),a]);}
      (e.ausgaben||[]).forEach(o=>{if(!NODEBY.has(rec(o)))return;if(!glieder.has(rec(o)))glieder.set(rec(o),2);kanten.push([a,rec(o)]);});});
    if((f.lauscht||[]).length){const a="fl:"+f._id;knoten.set(a,{id:a,kind:"lausche",name:"Lausche",ref:f,f});glieder.set(a,1);
      f.lauscht.forEach(o=>{if(NODEBY.has(rec(o))){glieder.set(rec(o),2);kanten.push([a,rec(o)]);}});}
    (f.konstruktor||[]).forEach((p,i)=>{const t=String(p.typ||"").trim(),g=gerufenByName(t),r=!g&&recByName(komBasis(t));
      const x=g?"fb:"+g._id:r?rec(r.name):"fk:"+f._id+":"+i;
      if(!g&&!r)knoten.set(x,{id:x,kind:/^IZustand</.test(t)?"zustand":/^ILogger/.test(t)?"logger":"param",name:t,ref:p,f});glieder.set(x,3);});
    // Unverbunden, aber bei der Funktion: eine Nachricht, die hier angelegt wurde (bzw. in ihrem Namespace liegt) und die niemand erzeugt oder nimmt.
    MODEL.records.forEach(r=>{const x=rec(r.name);if(glieder.has(x)||!NODEBY.has(x)||!["command","rejection"].includes(r.kind)||r.aggregat)return;
      if((r.amFunktion===f.name||r.namespace===f.namespace)&&!aggHatErzeuger(r.name)&&!funGenommen(r.name)){glieder.set(x,2);unverbunden.add(x);}});
    return {glieder,kanten,knoten,unverbunden};}
  function funZeige(de,id){const f=funModell(id);if(!f||istGerufen(f)){KOM.pfad[KOM.pfad.length-1]="form:"+id;komZeige();return;}
    const I=funInnen(id,f),kn=x=>I.knoten.get(x)||NODEBY.get(x),kat=funKatalog(f);
    innenZeige(de,id,{I,knoten:kn,unverbunden:I.unverbunden,
      unverbundenText:"Liegt bei der Funktion, noch von keinem Auf ausgegeben bzw. genommen — anschließen über ＋ am Port",
      // Fix: was dem Aggregat gehört (ein Command an es, eine Antwort) — bearbeitet wird es dort.
      fest:x=>{const r=I.glieder.get(x)!==3&&x.startsWith("rec:")&&recByName(x.slice(4));return !!r&&(!!r.aggregat||r.kind==="event");},
      festKnopf:(n,x)=>{const r=recByName(x.slice(4))||{},ag=r.aggregat||(SICHT().karten[x]||{}).aggregat;
        return h("button",{class:"ag-stift",title:"Gehört dem Aggregat — Klick: dort öffnen",onclick:e=>{e.stopPropagation();if(ag)komKarte("agg:"+ag,true);}},"▣");},
      ports:(n,x)=>funPorts(f,I,n,x),kantenPorts:(a,b)=>funKantenPorts(I,kn,a,b),auswahl:p=>funAuswahl(f,I,kn,p),anschliessen:(p,x)=>funAnschliessen(f,kn,p,x),
      geister:p=>funGeister(f,kat,p),plus:(p,x)=>innenPortWahl(p,x),kurz:(n,x)=>funKurz(f,n,x),bearbeiten:(n,x)=>funBearbeiten(f,kat,n,x),
      loeschen:(n,x)=>funLoeschPlan(f,I,n,x),
      zeit:x=>{const n=kn(x)||{};return n.kind==="auf"?aggZeit(n.ref.eingang):x.startsWith("rec:")?aggZeit(n.name):0;},
      name:x=>(kn(x)||{}).name||"",ein:x=>I.kanten.filter(([,b])=>b===x).length,koepfe:()=>funKoepfe(f,kat),
      legen:{spalten:[0,1,2,3],baender:[""],frei:[3],folgen:[]},
      ansicht:{fuehrt:()=>ANSICHT.funFuehrt==="ausgang"?"ausgang":"eingang",sortierung:()=>AGG_SORTIERUNG[ANSICHT.funSortierung]?ANSICHT.funSortierung:"neu",
        B:"ausgang",setze:(fu,s)=>{ANSICHT.funFuehrt=fu;ANSICHT.funSortierung=s;ansichtSpeichern();}}});
    // Im Kopf: wo die Funktion liegt und wo sie läuft (Graphen, in denen sie Knoten ist); das Formular für alles Übrige (Umsetzungen, Methoden).
    const reiter=de.querySelector(".kom .kom-reiter"),verwendet=(MODEL.graphen||[]).filter(g=>(g.knoten||[]).some(k=>komBasis(k.typ)===f.name));
    if(reiter)reiter.prepend(h("span",{class:"kom-u",title:f.namespace},(kat?"📚 "+kat:"Anwendung")+(verwendet.length?" · in "+verwendet.map(g=>"⬡ "+g.name).join(", "):" · in keinem Graphen")),
      h("button",{title:"Das bisherige Formular der Funktion",onclick:()=>komKarte(id,true,true)},"✎ Formular"));}
  // Die Ports: ein Auf nimmt links seinen Eingang und gibt rechts seine Ausgänge (mit Vielfachheit) aus; eine Nachricht hat ihren Auf-Anschluss.
  function funPorts(f,I,n,x){const ein=[],aus=[],t="ƒ "+f.name,s=nm=>komSorte(nm);
    if(n.kind==="auf"){const e=n.ref,m=funMult(e);ein.push({key:"in",label:e.eingang,sorte:s(e.eingang)});
      (e.ausgaben||[]).forEach(o=>aus.push({key:"o:"+o,label:o+"  "+m,sorte:s(o),loese:()=>{e.ausgaben=ohneX(e.ausgaben,o);gTitel(t+": Auf("+e.eingang+") gibt "+o+" nicht mehr aus");render();}}));
      aus.push({key:"+",label:"＋ Ausgang",plus:{art:"ausgang",e}});}
    else if(n.kind==="lausche"){(f.lauscht||[]).forEach(o=>aus.push({key:"o:"+o,label:o+"  0..n",sorte:s(o),loese:()=>{f.lauscht=ohneX(f.lauscht,o);gTitel(t+": meldet "+o+" nicht mehr");render();}}));
      aus.push({key:"+",label:"＋ meldet",plus:{art:"ausgang",lausche:true}});}
    else if(x.startsWith("rec:")){const nm=n.name,sp=I.glieder.get(x);
      if(sp===0)aus.push({key:"aus",label:"▶ Auf",sorte:s(nm)});
      else if(sp===2){I.kanten.filter(([,b])=>b===x).forEach(([a])=>{const an=I.knoten.get(a);if(!an)return;
          ein.push({key:"von:"+a,label:"◀ "+(an.kind==="lausche"?"Lausche":"Auf("+an.ref.eingang+")"),sorte:s(nm),loese:()=>{
            if(an.kind==="lausche")f.lauscht=ohneX(f.lauscht,nm);else an.ref.ausgaben=ohneX(an.ref.ausgaben,nm);gTitel(t+": "+nm+" nicht mehr ausgegeben");render();}});});
        ein.push({key:"+",label:"＋ Auf",plus:{art:"auf",rec:nm}});
        const r=recByName(nm);if(r&&r.aggregat)aus.push({key:"agg",label:"▶ ▣ "+r.aggregat,sorte:"command"});}}
    return {ein,aus};}
  function funKantenPorts(I,kn,a,b){const na=kn(a),nb=kn(b);if(!na||!nb)return null;
    if(nb.kind==="auf")return ["aus","in"];
    if(na.kind==="auf"||na.kind==="lausche")return ["o:"+nb.name,"von:"+a];
    return null;}
  function funAuswahl(f,I,kn,p){const k=new Set();if(!p)return k;
    I.glieder.forEach((s,x)=>{const n=kn(x);if(!n)return;
      if(p.art==="ausgang"&&s===2&&x.startsWith("rec:")&&!(p.lausche?f.lauscht||[]:p.e.ausgaben||[]).includes(n.name))k.add(x);
      if(p.art==="auf"&&n.kind==="auf"&&!(n.ref.ausgaben||[]).includes(p.rec))k.add(x);});
    return k;}
  function funGibAus(f,p,nm){const t="ƒ "+f.name;
    if(p.lausche){f.lauscht=[...(f.lauscht||[]),nm];gTitel(t+": meldet "+nm);}
    else{p.e.ausgaben=[...(p.e.ausgaben||[]),nm];gTitel(t+": Auf("+p.e.eingang+") gibt "+nm+" aus");}}
  function funAnschliessen(f,kn,p,x){const n=kn(x);if(!n)return;KOM.port=null;
    if(p.art==="ausgang")funGibAus(f,p,n.name);
    else if(p.art==="auf"){n.ref.ausgaben=[...(n.ref.ausgaben||[]),p.rec];gTitel("ƒ "+f.name+": Auf("+n.ref.eingang+") gibt "+p.rec+" aus");}
    render();}
  // Eine neue Nachricht der Funktion: in ihrem Namespace (im Katalog bleibt sie im Katalog), ohne Felder — die bekommt sie im ✎.
  function funRecordNeu(f,n,kind){MODEL.records.push({name:n,kind,namespace:f.namespace,felder:[],istErzeugung:false,amFunktion:f.name});aggStempel(n);}
  // Gestrichelt beim Anschließen eines Ausgangs: eine neue Nachricht, ein neuer Hinweis, (in der Anwendung) ein Command an ein Aggregat; dazu
  //   vorhandene Nachrichten ohne Erzeuger. Ein Katalog kennt keine Domäne — dort nur seine eigenen Nachrichten.
  function funGeister(f,kat,p){if(!p||p.art!=="ausgang")return [];const g=[],stamm=f.name.replace(/^I(?=[A-ZÄÖÜ])/,"");
    const neu=(titel,u,vorschlag,kind)=>g.push({spalte:2,t:titel,u,fn:e=>frageFix(e.clientX,e.clientY,titel,vorschlag,u,n=>{const fe=nameFehler(n);if(fe)return fe;
      KOM.port=null;funRecordNeu(f,n,kind);funGibAus(f,p,n);render();})});
    neu("＋ neue Nachricht","Command ohne Ziel — ein Knoten oder der Kopf eines Graphen nimmt sie",stamm+"Ergebnis","command");
    if(!p.lausche)neu("＋ neuer Hinweis","flüchtig, an die Akteure, die ihn hören",stamm+"Unmoeglich","rejection");
    if(!kat&&!p.lausche)g.push({spalte:2,t:"→ Command an ▣ …",u:"an ein Aggregat — vorhanden oder neu samt Decide",fn:e=>funCommandAnAggregat(f,p,e)});
    const imScope=r=>kat?funKatalog(r)===kat:!funKatalog(r)||true;
    MODEL.records.filter(r=>["command","rejection"].includes(r.kind)&&!r.aggregat&&imScope(r)&&!aggHatErzeuger(r.name)&&!(p.lausche?f.lauscht||[]:p.e.ausgaben||[]).includes(r.name))
      .slice(0,12).forEach(r=>g.push({spalte:2,t:(FL_SORTE[komSorte(r.name)]||"")+r.name,u:"vorhanden, noch ohne Erzeuger — Klick übernimmt",fn:()=>{KOM.port=null;funGibAus(f,p,r.name);render();}}));
    return g;}
  // „→ Command an ▣ …“: je Aggregat seine Commands und „neuer Command“ (entsteht beim Aggregat samt Decide, wie im Aggregat-Modal).
  function funCommandAnAggregat(f,p,ev){const items=[];
    MODEL.aggregate.forEach(A=>{items.push({kopf:"▣ "+A.name});
      MODEL.records.filter(r=>r.kind==="command"&&r.aggregat===A.name&&!(p.e.ausgaben||[]).includes(r.name))
        .forEach(r=>items.push({t:"▶ "+r.name,fn:()=>{KOM.port=null;funGibAus(f,p,r.name);render();}}));
      items.push({t:"＋ neuer Command an "+A.name+" …",fn:()=>frageFix(ev.clientX,ev.clientY,"＋ Command an "+A.name,"","Entsteht beim Aggregat samt Decide",n=>{
        const fe=nameFehler(n);if(fe)return fe;KOM.port=null;aggCommandAnlegen(A,n,[]);funGibAus(f,p,n);render();})});});
    menueFix(ev.clientX,ev.clientY,"Command an ein Aggregat",items);}
  function funKoepfe(f,kat){const t="ƒ "+f.name,menue=(titel,items)=>ev=>menueFix(ev.clientX,ev.clientY,titel,items);
    const neuerEingang=ev=>frageFix(ev.clientX,ev.clientY,"＋ Eingang von "+f.name,f.name.replace(/^I(?=[A-ZÄÖÜ])/,"")+"Auftrag","Eine neue Nachricht (Command ohne Ziel) samt Auf",n=>{
      const fe=nameFehler(n);if(fe)return fe;funRecordNeu(f,n,"command");(f.eingaenge=f.eingaenge||[]).push({eingang:n,ausgaben:[]});gTitel(t+": Auf("+n+")");render();});
    const hat=nm=>(f.eingaenge||[]).some(e=>e.eingang===nm);
    const vorhanden=MODEL.records.filter(r=>r.kind==="command"&&!r.aggregat&&!hat(r.name)&&(kat?funKatalog(r)===kat:true))
      .map(r=>({t:"▶ "+r.name,fn:()=>{(f.eingaenge=f.eingaenge||[]).push({eingang:r.name,ausgaben:[]});gTitel(t+": Auf("+r.name+")");render();}}));
    // Antworten: die Events, die die Decides der Commands ergeben, die diese Funktion an Aggregate gibt (CQRS108) — nur in der Anwendung.
    const antworten=kat?[]:[...new Set((f.eingaenge||[]).flatMap(e=>e.ausgaben||[]).filter(o=>(recByName(o)||{}).aggregat)
      .flatMap(o=>MODEL.decider.filter(d=>d.command===o).flatMap(d=>(d.ergibt||[]).map(x=>x.event))).filter(ev=>(recByName(ev)||{}).kind==="event"&&!hat(ev)))]
      .map(ev=>({t:"◀ "+ev,title:"Antwort auf einen eigenen Command an ein Aggregat",fn:()=>{f.eingaenge.push({eingang:ev,ausgaben:[]});gTitel(t+": Auf("+ev+") — Antwort");render();}}));
    return [
      ["Eingänge",menue("Eingang von "+f.name,[{t:"＋ neue Nachricht mit Auf …",fn:neuerEingang},
        ...(vorhanden.length?[{trenn:1},{kopf:"vorhandene Nachricht"},...vorhanden.slice(0,30)]:[]),
        ...(antworten.length?[{trenn:1},{kopf:"Antwort auf einen eigenen Command"},...antworten]:[])]),"＋ ▾","eingang"],
      ["Auf",null,"je Eingang eines"],
      ["Ausgänge",null,"＋ am Port eines Auf","ausgang"],
      ["Rahmen",menue("Konstruktor von "+f.name,funRahmenItems(f)),"＋ ▾"]];}
  // Der Rahmen einer Funktion (Konstruktor): neue oder vorhandene Konfig, neue oder vorhandene Technik, der Logger — im Funktions- und im Katalog-Modal.
  function funRahmenItems(f){const t="ƒ "+f.name;
    const ctor=ctorVon(f,null),hatTyp=x=>ctor.some(p=>komBasis(String(p.typ).trim())===x);
    const technik=MODEL.bausteinFunktionen.filter(g=>istGerufen(g)&&!hatTyp(g.name)).map(g=>({t:"⚙ "+g.name,fn:()=>{nimmGerufene(ctor,g,f);gTitel(t+" nimmt "+g.name);render();}}));
    const konfigs=MODEL.records.filter(r=>r.kind==="konfig"&&!hatTyp(r.name)).map(r=>({t:"▤ "+r.name,fn:()=>{ctor.push({typ:r.name,name:paramName(r.name,ctor.map(p=>p.name))});gTitel(t+" nimmt "+r.name);render();}}));
    const logger=ctor.some(p=>/^ILogger</.test(String(p.typ)));
    return [
        {t:"＋ neue Konfig …",fn:ev=>frageFix(ev.clientX,ev.clientY,"＋ Konfig von "+f.name,f.name.replace(/^I(?=[A-ZÄÖÜ])/,"")+"Konfig","Ein Record — seine Werte stehen am Knoten der Instanz",n=>{
          const fe=nameFehler(n);if(fe)return fe;MODEL.records.push({name:n,kind:"konfig",namespace:f.namespace,felder:[{_id:"f"+(NID++),name:"Wert",typ:"string"}],istErzeugung:false});
          aggStempel(n);ctor.push({typ:n,name:paramName(n,ctor.map(p=>p.name))});gTitel(t+" nimmt "+n);render();})},
        {t:"＋ neue Technik …",fn:ev=>frageFix(ev.clientX,ev.clientY,"＋ Technik für "+f.name,"I"+f.name.replace(/^I(?=[A-ZÄÖÜ])/,"")+"Technik","Eine Schnittstelle mit einer Umsetzung (Methoden im Formular)",n=>{
          if(!/^I[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(n))return "Eine Schnittstelle: I + Name";if(MODEL.bausteinFunktionen.some(x=>x.name===n))return n+" gibt es schon";
          const g=neueGerufene(f.namespace);g.name=n;g.umsetzungen[0].name=uniq(n.slice(1));nimmGerufene(ctor,g,f);gTitel(t+" nimmt "+n);render();})},
        ...(konfigs.length?[{trenn:1},{kopf:"vorhandene Konfig"},...konfigs.slice(0,20)]:[]),
        ...(technik.length?[{trenn:1},{kopf:"vorhandene Technik"},...technik.slice(0,20)]:[]),
        {trenn:1},{t:(logger?"✓ ":"   ")+"Logger (ILogger<"+f.name+">)",fn:()=>{if(logger)f.konstruktor=ctor.filter(p=>!/^ILogger</.test(String(p.typ)));
          else ctor.push({typ:"ILogger<"+f.name+">",name:"logger"});gTitel(t+(logger?" ohne":" mit")+" Logger");render();}},
        {hinweis:"Zustand (IZustand<T>): im Formular — die Zustandsklasse legt das Modal noch nicht an."}];}
  function funKurz(f,n,x){const box=h("div",{class:"ag-kurz"});
    if(n.kind==="auf"){const e=n.ref,tags=[funMult(e)==="0..n"?"Strom":(e.ausgaben||[]).length>1?"Wahl":"Folge"];
      if(e.asynchron)tags.push("asynchron");if(e.herkunft)tags.push("Herkunft");(e.faehigkeiten||[]).forEach(p=>tags.push("⌕ "+p.typ));
      if((recByName(e.eingang)||{}).kind==="event")tags.push("Antwort");
      tags.forEach(tg=>box.append(h("span",{class:"ag-tag"},tg)));return box;}
    if(n.kind==="lausche"){box.append(h("span",{class:"ag-tag"},"Quelle — je Element eine Kette"));return box;}
    if(["zustand","logger","param"].includes(n.kind)){box.append(n.name);return box;}
    if(n.kind==="bausteinfunktion"){box.append("Umsetzungen: "+((n.ref.umsetzungen||[]).map(u=>u.name).join(", ")||"—"));return box;}
    const r=n.ref||{};if((r.felder||[]).length)box.append(h("div",{},r.felder.map(fe=>fe.name).join(", ")));
    if(r.aggregat)box.append(h("div",{class:"ag-tag"},"→ ▣ "+r.aggregat));return box;}
  // Eine Nachricht (noch nicht geschrieben) umbenennen: alle Verweise der Funktionen ziehen mit (eine geschriebene geht über den Renamer).
  function funRecUmbenennen(r,neu){const alt=r.name;recUmbenennenLokal(r,neu);
    MODEL.bausteinFunktionen.forEach(g=>{(g.eingaenge||[]).forEach(e=>{if(e.eingang===alt)e.eingang=neu;e.ausgaben=(e.ausgaben||[]).map(v=>v===alt?neu:v);});
      g.lauscht=(g.lauscht||[]).map(v=>v===alt?neu:v);});render();}
  function funBearbeiten(f,kat,n,x){const box=h("div",{class:"ag-edit"}),t="ƒ "+f.name;
    ["click","dblclick"].forEach(ev=>box.addEventListener(ev,e=>e.stopPropagation()));
    const schalter=(text,an,titel,setze)=>{const c=h("input",{type:"checkbox",onchange:e=>{setze(e.target.checked);render();}});c.checked=!!an;
      return h("label",{class:"ag-flag",title:titel},c," "+text);};
    const ruempfe=pred=>[...NODEBY.values()].filter(r=>r.kind==="rumpf"&&r.own&&r.own.id===("fb:"+f._id)&&pred(r)).forEach(r=>{const b=h("div",{class:"gbody"});
      INSP=true;try{fuelleKoerper(b,r);}finally{INSP=false;}box.append(b);});
    if(n.kind==="auf"){const e=n.ref;
      box.append(schalter("Strom — keine bis viele je Aufruf (IEnumerable)",e.strom,"Sonst genau eine Ausgabe bzw. bei mehreren eine Wahl (OneOf: keine oder eine)",v=>{e.strom=v;gTitel(t+": Auf("+e.eingang+")"+(v?" als Strom":" ohne Strom"));}));
      box.append(schalter("asynchron (Task bzw. IAsyncEnumerable)",e.asynchron,"",v=>{e.asynchron=v;gTitel(t+": Auf("+e.eingang+")"+(v?" asynchron":" synchron"));}));
      box.append(schalter("Herkunft h (Stream, Zeit, Akteur, NeueId)",e.herkunft,"",v=>{e.herkunft=v;gTitel(t+": Auf("+e.eingang+")"+(v?" mit":" ohne")+" Herkunft");}));
      if(!kat){const alle=MODEL.stores.flatMap(st=>(st.readFns||[]).map(fn=>fn.faehigkeit).filter(Boolean)),hat=new Set((e.faehigkeiten||[]).map(p=>p.typ));
        if(alle.length)box.append(h("div",{class:"ag-sek"},"liest (Lese-Fähigkeiten)"),...[...new Set(alle)].map(fa=>schalter("⌕ "+fa,hat.has(fa),"Eine Lese-Fähigkeit als Parameter des Auf",v=>{
          e.faehigkeiten=v?[...(e.faehigkeiten||[]),{typ:fa,name:paramName(fa,(e.faehigkeiten||[]).map(p=>p.name).concat(["e","h"]))}]:(e.faehigkeiten||[]).filter(p=>p.typ!==fa);
          gTitel(t+": Auf("+e.eingang+")"+(v?" liest ":" liest nicht mehr ")+fa);})));}
      else box.append(h("div",{class:"kom-wo"},"Im Katalog: keine Lese-Fähigkeit — ein Katalog kennt keinen Store der Anwendung."));
      ruempfe(r=>String(r.name||"").includes("("+e.eingang+")"));return box;}
    if(n.kind==="lausche"){ruempfe(r=>/Lausche/.test(String(r.name||"")));return box;}
    if(x.startsWith("rec:")){const r=n.ref;
      box.append(h("div",{class:"ag-feld"},h("input",{value:r.name,spellcheck:"false",title:"Name",onchange:e=>{const neu=e.target.value.trim(),fe=nameFehler(neu);if(neu===r.name)return;
        if(fe){deFlash("⚠ "+fe,false);render();return;}aggStempelUmbenennen(r.name,neu);if(r._ort)gUmbenennen(r,neu,r.name);else funRecUmbenennen(r,neu);}})));
      box.append(aggFelder(r.felder=r.felder||[],null,t+": "+r.name));
      if(r.kind==="command"||r.kind==="rejection")box.append(h("button",{class:"ag-plus",title:"Command ohne Ziel (ein Knoten bzw. Kopf nimmt ihn) oder Hinweis (flüchtig, an Akteure)",
        onclick:()=>{r.kind=r.kind==="command"?"rejection":"command";gTitel(t+": "+r.name+(r.kind==="command"?" ist eine Nachricht":" ist ein Hinweis"));render();}},
        r.kind==="command"?"⇄ zum Hinweis machen":"⇄ zur Nachricht machen"));
      return box;}
    if(["zustand","logger","param"].includes(n.kind)){box.append(h("div",{class:"kom-wo"},n.name+" — im Konstruktor; ✕ im Kopf nimmt es heraus."));return box;}
    if(n.kind==="bausteinfunktion")box.append(h("button",{class:"ag-plus",title:"Methoden und Umsetzungen der Technik",onclick:()=>komKarte(x,true,true)},"⚙ Methoden und Umsetzungen"));
    return box;}
  // Löschen in der Funktion: ein Auf (sein Eingang geht mit, wenn ihn sonst niemand nennt), eine Nachricht (aus allen Auf dieser Funktion), ein
  //   Konstruktor-Parameter (nur aus dem Konstruktor; Konfig und Technik bleiben als Typen).
  function funLoeschPlan(f,I,n,x){const t="ƒ "+f.name;
    const hier=u=>u.art==="funktion"&&new RegExp("(^|[^A-Za-z0-9_])"+f.name+"([.(]|$)").test(u.wo);
    if(n.kind==="auf"){const e=n.ref,nur=verwendungen(e.eingang).every(u=>hier(u)||u.art==="darf"||u.art==="hoert"),r=recByName(e.eingang),eigen=r&&!r.aggregat&&r.kind!=="event"&&nur;
      return {was:"Auf("+e.eingang+")",folgen:eigen?["die Nachricht "+e.eingang+" (sonst nennt sie niemand)"]:[],grund:null,
        tu:()=>{f.eingaenge=(f.eingaenge||[]).filter(z=>z!==e);if(eigen)recordWeg(e.eingang);}};}
    if(n.kind==="lausche")return {was:"Lausche",folgen:["die Funktion ist danach keine Quelle mehr"],grund:null,tu:()=>{f.lauscht=[];}};
    // Im Rahmen: nur aus dem Konstruktor — Konfig-Record und Technik bleiben als Typen.
    if(I.glieder.get(x)===3){const p=n.kind==="bausteinfunktion"?ctorVon(f,null).find(q=>gerufenByName(q.typ)===n.ref)
        :x.startsWith("rec:")?ctorVon(f,null).find(q=>komBasis(String(q.typ).trim())===n.name):n.ref;
      return p?{was:String(p.typ)+" aus dem Konstruktor",folgen:[],grund:null,tu:()=>{f.konstruktor=ctorVon(f,null).filter(q=>q!==p);}}:null;}
    if(x.startsWith("rec:")){const r=n.ref||{};if(r.aggregat||r.kind==="event")return null;
      return loeschPlanRecord(r.name,(r.kind==="rejection"?"Hinweis ":"Nachricht ")+r.name,hier,()=>{
        (f.eingaenge||[]).forEach(e=>{e.ausgaben=ohneX(e.ausgaben,r.name);});f.eingaenge=(f.eingaenge||[]).filter(e=>e.eingang!==r.name);
        f.lauscht=ohneX(f.lauscht,r.name);recordWeg(r.name);});}
    return null;}
  // ── Komposition: Knoten aus dem Baukasten, [Von] und [Kopf] durch Ziehen, Konstruktor durch Ablegen, Kanten lösen ──
  const komModell=id=>(MODEL.graphen||[]).find(g=>"gr:"+g._id===id);
  function komVerbinde(id,von,nach){const g=komModell(id);if(!g)return;const k=(g.knoten||[]).find(x=>x.name===nach);if(!k)return;
    if(von===null){if(k.kopf){deFlash("ℹ "+nach+" ist schon Kopf",true);return;}k.kopf=true;gTitel(g.name+": "+nach+" ist Kopf");render();return;}
    if(von===nach)return;if((k.von||[]).includes(von)){deFlash("ℹ "+nach+" bekommt schon von "+von,true);return;}
    k.von=[...(k.von||[]),von];gTitel(g.name+": "+nach+" von "+von);render();}
  // „＋ Technik/Konfig“ am Knoten: was der Baukasten an Technik und Konfig hat und der Konstruktor noch nicht nimmt.
  function komKonstruktorPlus(id,k,ev){const bk=komBaukasten(),hat=new Set((k.konstruktor||[]).map(c=>c.typ));
    const t=bk.technik.filter(x=>!hat.has(x.name)).map(x=>({t:"⚙ "+x.name+(x.umsetzungen.length?" ("+x.umsetzungen.join(", ")+")":""),fn:()=>komAblegen(id,{gruppe:"technik",name:x.name},k.name,ev)}));
    const c=bk.konfig.filter(x=>!hat.has(x.name)).map(x=>({t:"▤ "+x.name,fn:()=>komAblegen(id,{gruppe:"konfig",name:x.name},k.name,ev)}));
    menueFix(ev.clientX,ev.clientY,k.name+" nimmt …",[...(t.length?[{kopf:"Technik"},...t]:[]),...(c.length?[{kopf:"Konfig"},...c]:[]),
      !t.length&&!c.length?{hinweis:"Nichts mehr frei — neue Technik oder Konfig im Formular der Funktion (Doppelklick)."}:null]);}
  function komKanteMenue(id,art,von,nach,ev){const g=komModell(id),k=g&&(g.knoten||[]).find(x=>x.name===nach);if(!k)return;
    if(art==="kopf"){menueFix(ev.clientX,ev.clientY,"Kopf "+nach,[{t:"✕ "+nach+" nimmt nicht mehr von außen ([Kopf] weg)",gefahr:1,fn:()=>{k.kopf=false;gTitel(g.name+": "+nach+" nicht mehr Kopf");render();}}]);return;}
    const vl=(k.verlierbar||[]).includes(von);
    menueFix(ev.clientX,ev.clientY,von+" → "+nach,[
      {t:vl?"gesichert machen (der Absender wartet auf „fertig“)":"⇢ verlierbar machen (einmal, ohne Antwort)",fn:()=>{k.verlierbar=vl?ohneX(k.verlierbar,von):[...(k.verlierbar||[]),von];gTitel(g.name+": "+von+" → "+nach+(vl?" gesichert":" verlierbar"));render();}},
      {t:"✕ [Von("+von+")] lösen",gefahr:1,fn:()=>{k.von=ohneX(k.von,von);k.verlierbar=ohneX(k.verlierbar,von);gTitel(g.name+": "+nach+" nicht mehr von "+von);render();}}]);}
  function komKnotenEntfernen(id,name){const g=komModell(id);if(!g)return;g.knoten=(g.knoten||[]).filter(x=>x.name!==name);
    g.knoten.forEach(x=>{x.von=ohneX(x.von,name);x.verlierbar=ohneX(x.verlierbar,name);});gTitel(g.name+": Knoten "+name+" entfernt");KOM.wahl=null;render();}
  // ══ EINGÄNGE UND ÜBERSETZUNGEN (docs/plan-katalog-und-graphen.md, Schritt 5): nur Clients und Quellen stoßen Funktionen an. Eine Übersetzung
  //   (Uebersetze<TEin, TAus>) verbindet die Nachricht eines Clients mit dem Eingang eines Katalogs und die Ausgabe einer Funktion mit einem Command
  //   an ein Aggregat — Feld für Feld; was nicht nach Namen passt, nennt die ↦-Karte ausdrücklich ([Feld]). ══
  const komUeTyp=(ein,aus)=>"Uebersetze<"+ein+", "+aus+">";
  const komKnotenName=(g,stamm)=>{let n=stamm,i=2;while((g.knoten||[]).some(x=>x.name===n))n=stamm+(i++);return n;};
  // Die Felder einer Nachricht als neue Felder (eine Kopie: gleiche Namen und Typen — so passt die Übersetzung nach Namen).
  // ns: wohin die Kopie kommt — heißt dort ein Namespace-Teil wie ein Aggregat im Typ (Domain.Pipeline.Modell und Id<Modell>), meinte der einfache
  //   Name den Namespace; dann steht das Aggregat mit vollem Namen.
  const komFelderVon=(t,ohne,ns)=>((recByName(komBasis(t))||{}).felder||[]).filter(f=>!(ohne&&ohne(f))).map(f=>({_id:"f"+(NID++),name:f.name,
    typ:String(f.typ).replace(/\b([A-ZÄÖÜ]\w*)\b/g,(x,n)=>{const A=MODEL.aggregate.find(a=>a.name===n);
      return A&&ns&&ns.split(".").includes(n)&&!String(f.typ).includes(A.namespace+"."+n)?A.namespace+"."+n:x;})}));
  // „＋ Eingang“ am linken Rand: eine QUELLE (nur ohne Kopf — sie ist dann der einzige) oder ein CLIENT: eine Nachricht der Anwendung, die ein Akteur
  //   hineingibt (IDarf). Vorhanden ist, was im Bereich des Graphen liegt, keine Funktion ausgibt und noch kein Graph am Kopf nimmt.
  function komEingangPlus(id,g,d,ev){const items=[],bk=komBaukasten();
    if(!d.koepfe.length)items.push({kopf:"Quelle — lauscht"},...bk.katalog.map(k=>({t:"⚡ "+k.name+" → "+(k.meldet||[]).join(", "),fn:()=>neuerGraphKnoten(g,k.name)})),
      ...bk.funktionen.filter(f=>f.quelle).map(f=>({t:"⚡ "+f.name,fn:()=>neuerGraphKnoten(g,f.name)})));
    const genommen=new Set((MODEL.graphen||[]).flatMap(x=>(x.koepfe||[]).map(komBasis))),gegeben=new Set(MODEL.bausteinFunktionen.flatMap(f=>(f.eingaenge||[]).flatMap(e=>e.ausgaben||[])).map(komBasis));
    const frei=MODEL.records.filter(r=>r.kind==="command"&&!r.aggregat&&r.namespace===g.namespace&&!genommen.has(r.name)&&!gegeben.has(r.name));
    items.push({kopf:"Client — ein Akteur gibt eine Nachricht hinein"},{t:"＋ neue Nachricht …",fn:()=>komClientKopf(id,g,d,null,ev)},
      ...frei.map(r=>({t:"▶ "+r.name,fn:()=>komClientKopf(id,g,d,r.name,ev)})));
    menueFix(ev.clientX,ev.clientY,"Eingang an "+g.name,items);}
  // Der Kopf für die Nachricht m eines Clients (m null: neu, nach der Wahl benannt): eine Übersetzung in den Eingang eines Knotens dieses Graphen
  //   (die neue Nachricht bekommt dessen Felder) oder eine neue Funktion der Anwendung, die sie nimmt. Danach: wer sie hineingibt.
  function komClientKopf(id,g,d,m,ev){
    const ziele=d.knoten.filter(k=>k.art!=="uebersetzung").flatMap(k=>k.ein.filter(t=>!komIstAntwort(t)).map(t=>({k,t})));
    const nachricht=(vorschlag,felder,weiter)=>{if(m){weiter(m);return;}
      frageFix(ev.clientX,ev.clientY,"▶ Neue Nachricht eines Clients",vorschlag,"Ein Command ohne Ziel in "+g.namespace+" — ein Akteur gibt ihn hinein",n=>{const f=nameFehler(n);if(f)return f;
        MODEL.records.push({name:n,kind:"command",namespace:g.namespace,felder:felder(g.namespace),istErzeugung:false});aggStempel(n);setTimeout(()=>weiter(n),0);});};
    menueFix(ev.clientX,ev.clientY,"Kopf für "+(m||"die neue Nachricht"),[
      ...(ziele.length?[{kopf:"↦ übersetzen in den Eingang von …"}]:[]),
      ...ziele.map(({k,t})=>({t:"↦ "+t+" ("+k.name+")",fn:()=>nachricht(komBasis(t).replace(/(Auftrag)?$/,"")+"Angefordert",ns=>komFelderVon(t,null,ns),n=>{
        const gm=komModell(id),z=(gm.knoten||[]).find(x=>x.name===k.name);if(!z)return;
        const un=komKnotenName(gm,"Hinein");gm.knoten.push({typ:komUeTyp(n,t),name:un,kopf:true,von:[],verlierbar:[],felder:[]});
        z.von=[...(z.von||[]),un];if(z.kopf&&k.ein.filter(e=>!komIstAntwort(e)).length<=1)z.kopf=false;
        gTitel(gm.name+": Eingang "+n+" ↦ "+t);render();setTimeout(()=>komAkteurWahl(n,ev),0);})})),
      {trenn:1},{t:"ƒ neue Funktion der Anwendung, die sie nimmt …",fn:()=>nachricht(g.name+"Angefordert",()=>[],n=>
        frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion für "+n,n.replace(/Angefordert$/,"")+"Annahme","Nimmt "+n+" am Kopf; Ausgänge danach im Modal",fn=>{
          const fe=nameFehler(fn);if(fe&&!/gibt es schon/.test(fe))return fe;if(MODEL.bausteinFunktionen.some(x=>x.name===fn))return fn+" gibt es schon";
          const f={_id:"funktion:"+fn,name:fn,namespace:g.namespace,klasse:true,eingaenge:[{eingang:n,ausgaben:[]}],konstruktor:[],umsetzungen:[]};
          MODEL.bausteinFunktionen.push(f);const gm=komModell(id);gm.knoten.push({typ:fn,name:komKnotenName(gm,fn),kopf:true,von:[],verlierbar:[]});
          gTitel(gm.name+": Eingang "+n+" → ƒ "+fn);render();setTimeout(()=>komAkteurWahl(n,ev),0);}))}]);}
  // Wer die Nachricht m hineingibt: ein Akteur bekommt IDarf<m> (vorhanden oder neu) — oder später.
  function komAkteurWahl(m,ev){
    menueFix(ev.clientX,ev.clientY,"Wer gibt "+m+" hinein? (IDarf)",[
      ...MODEL.akteure.map(a=>({t:"👤 "+a.name+((a.darf||[]).includes(m)?" (darf schon)":""),fn:()=>{if((a.darf||[]).includes(m))return;
        a.darf=[...(a.darf||[]),m];gTitel("👤 "+a.name+" darf "+m);render();}})),
      {trenn:1},{t:"＋ neuer Akteur …",fn:()=>frageFix(ev.clientX,ev.clientY,"👤 Neuer Akteur","","Er gibt "+m+" hinein (IDarf)",n=>{const f=nameFehler(n);if(f)return f;
        MODEL.akteure.push({_id:"ak"+(NID++),name:n,namespace:haeufigsterNs(MODEL.akteure)||defaultNsGlobal(),darf:[m],hoert:[]});gTitel("👤 "+n+" darf "+m);render();})},
      {hinweis:"Ohne Akteur gibt niemand "+m+" hinein — der Rand zeigt es an."}]);}
  // „↦ ▣ Command an …“ am Ausgang eines Knotens: ein vorhandener Command an ein Aggregat oder ein neuer samt Decide; dazwischen die Übersetzung.
  function komCommandAn(id,q,typ,ev){KOM.port=null;
    const cmds=MODEL.records.filter(r=>r.kind==="command"&&r.aggregat).sort((a,b)=>(a.aggregat+a.name).localeCompare(b.aggregat+b.name));
    menueFix(ev.clientX,ev.clientY,typ+" ↦ Command an …",[
      {t:"＋ neuer Command an ein Aggregat …",fn:()=>menueFix(ev.clientX,ev.clientY,"An welches Aggregat?",MODEL.aggregate.map(A=>({t:"▣ "+A.name,
        fn:()=>frageFix(ev.clientX,ev.clientY,"＋ Command an "+A.name,"Vermerke"+komBasis(typ),"Mit Decide und einem Event; Felder: die Id von "+A.name+" und die von "+komBasis(typ),n=>{
          const f=nameFehler(n);if(f)return f;
          // Ein Decide ergibt sein Event (ein leeres baute nicht): dieselben Felder ohne die Id, mit seinem Apply.
          let e=komBasis(typ)+"Vermerkt",i=2;while(recNamens(e))e=komBasis(typ)+"Vermerkt"+(i++);
          const ohne=x=>/^(System\.)?Guid$/.test(x.typ)||x.name===aggIdFeld();
          aggRecordNeu(A,e,"event");recNamens(e).felder=komFelderVon(typ,ohne);aggCommandAnlegen(A,n,[{event:e}]);aggApplySichern(A,e);
          const r=recNamens(n);r.felder=[...r.felder,...komFelderVon(typ,ohne)];
          komUebersetzungAn(id,q,typ,n);})})))},
      ...(cmds.length?[{kopf:"vorhanden"}]:[]),...cmds.map(r=>({t:"▣ "+r.aggregat+" · "+r.name,fn:()=>komUebersetzungAn(id,q,typ,r.name)}))]);}
  // Die Übersetzung typ ↦ cmd hinter dem Knoten q; die Id des Ziels nennt sie ausdrücklich, wenn genau ein Feld dafür in Frage kommt
  //   (eine Id dieses Aggregats, sonst ein Guid) — sonst bleibt sie offen, die ↦-Karte zeigt es.
  function komUebersetzungAn(id,q,typ,cmd){const g=komModell(id),r=recNamens(cmd);if(!g||!r)return;
    const idFeld=aggIdFeld(),quelle=(recByName(komBasis(typ))||{}).felder||[],felder=[];
    if(!quelle.some(f=>f.name===idFeld)){const ids=quelle.filter(f=>f.typ==="Id<"+r.aggregat+">"),guids=quelle.filter(f=>/^(System\.)?Guid$/.test(f.typ));
      const w=ids.length===1?ids[0]:!ids.length&&guids.length===1?guids[0]:null;if(w)felder.push({ziel:idFeld,quelle:w.name});}
    const un=komKnotenName(g,"Zu"+r.aggregat);
    g.knoten.push({typ:komUeTyp(komBasis(typ),cmd),name:un,kopf:false,von:[q.name],verlierbar:[],felder});
    gTitel(g.name+": "+q.name+" ↦ "+cmd);render();}
  // Aggregat von innen, „＋ Graph, der ihn schickt“: ein neuer Graph im Bereich des Aggregats (bei den Abläufen) mit der Übersetzung als Kopf — aus
  //   einer neuen Nachricht eines Clients mit den Feldern des Commands; Funktionen kommen danach über „＋“ davor dazwischen. Er öffnet sich.
  function aggGraphSchickt(A,cmd){const ns=ablaufNs(A.namespace);
    frageFix(ZEIGER.x,ZEIGER.y,"⬡ Graph, der "+cmd+" schickt",cmd+"Ablauf","Neu in "+ns+": Kopf ist eine Übersetzung aus "+cmd+"Auftrag (ein Client gibt ihn hinein)",n=>{
      const f=nameFehler(n);if(f)return f;if((MODEL.graphen||[]).some(x=>x.name===n))return n+" gibt es schon";
      let m=cmd+"Auftrag",i=2;while(recNamens(m))m=cmd+"Auftrag"+(i++);
      // Die Felder des Commands; seine Ziel-Id heißt hier <Aggregat>Id (AggregateId trägt nur ein Command an ein Aggregat, CQRS013) — die Übersetzung
      //   nennt sie ausdrücklich.
      const felder=komFelderVon(cmd,null,ns),idFeld=felder.find(f=>f.name===aggIdFeld()),feld=[];
      if(idFeld){idFeld.name=A.name+"Id";feld.push({ziel:aggIdFeld(),quelle:idFeld.name});}
      MODEL.records.push({name:m,kind:"command",namespace:ns,felder,istErzeugung:false});aggStempel(m);
      (MODEL.graphen=MODEL.graphen||[]).push({_id:"graph:"+n,name:n,namespace:ns,knoten:[{typ:komUeTyp(m,cmd),name:"Senden",kopf:true,von:[],verlierbar:[],felder:feld}]});
      KOM_NACH="gr:graph:"+n;gTitel("⬡ "+n+" schickt "+cmd);render();});}
  // Ein Graph entsteht mit seinem ersten Knoten (ein leerer baut nicht, CQRS115): eine Funktion (Anwendung oder Katalog), eine Quelle oder ein Graph.
  function graphNeuMitKnoten(ev,pos){const bk=komBaukasten();
    frageFix(ev.clientX,ev.clientY,"⬡ Neuer Graph",uniq("NeuerGraph"),"Danach: sein erster Knoten",n=>{const f=nameFehler(n);if(f)return f;
      if((MODEL.graphen||[]).some(x=>x.name===n))return n+" gibt es schon";
      const ns=ablaufNs(defaultNsGlobal());
      const mit=(typ,quelle)=>{(MODEL.graphen=MODEL.graphen||[]).push({_id:"graph:"+n,name:n,namespace:ns,
        knoten:[{typ,name:typBasis(typ).replace(/^I(?=[A-ZÄÖÜ])/,""),kopf:!quelle,von:[],verlierbar:[]}],...pos});KOM_NACH="gr:graph:"+n;gTitel("⬡ "+n+" mit "+typ);render();};
      // Eine neue Funktion als erster Knoten: in der Anwendung oder in einem Katalog (dort in einem Bereich), mit Auftrag und Ergebnis.
      const neueFn=katalog=>{const weiter=fns=>frageFix(ev.clientX,ev.clientY,"ƒ Erste Funktion von "+n,n.replace(/(Graph|Ablauf)$/,"")+"Rechnung",
          "Nimmt einen neuen Auftrag und gibt ein Ergebnis — beides danach im Modal der Funktion",fn=>{
          const fe=nameFehler(fn);if(fe&&!/gibt es schon/.test(fe))return fe;if(MODEL.bausteinFunktionen.some(x=>x.name===fn))return fn+" gibt es schon";
          const f={_id:"funktion:"+fn,name:fn,namespace:fns,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[]};
          const auftrag=uniq(fn+"Auftrag"),ergebnis=uniq(fn+"Ergebnis");funRecordNeu(f,auftrag,"command");funRecordNeu(f,ergebnis,"command");
          f.eingaenge.push({eingang:auftrag,ausgaben:[ergebnis]});MODEL.bausteinFunktionen.push(f);mit(fn,false);});
        if(!katalog){setTimeout(()=>weiter(ns),0);return;}
        setTimeout(()=>frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion in "+katalog+" — Bereich","Allgemein","Der Ordner im Katalog",b=>{
          if(!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(b))return "Ein Bereich: groß beginnen, nur Buchstaben und Ziffern";setTimeout(()=>weiter(katalog+"."+b),0);}),0);};
      setTimeout(()=>menueFix(ev.clientX,ev.clientY,"Erster Knoten von "+n,[
        {t:"＋ ƒ neue Funktion in der Anwendung …",fn:()=>neueFn(null)},...(bk.kataloge||[]).map(k=>({t:"＋ ƒ neue Funktion in 📚 "+k+" …",fn:()=>neueFn(k)})),
        ...(bk.funktionen.length?[{kopf:"vorhanden"}]:[]),
        ...bk.funktionen.filter(x=>!x.quelle).map(x=>({t:(x.katalog?"📚 "+x.katalog+" · ":"ƒ ")+x.name,fn:()=>mit(x.name,false)})),
        ...(bk.katalog.length||bk.funktionen.some(x=>x.quelle)?[{kopf:"Quelle"}]:[]),
        ...bk.katalog.map(k=>({t:"⚡ "+k.name,fn:()=>mit(k.name,true)})),...bk.funktionen.filter(x=>x.quelle).map(x=>({t:"⚡ "+x.name,fn:()=>mit(x.name,true)})),
        ...(bk.graphen.length?[{kopf:"Graph (Schachtel)"}]:[]),...bk.graphen.map(x=>({t:"⬡ "+x.name,fn:()=>mit(x.name,false)}))]),0);});}
  // Aus dem Baukasten abgelegt: Funktion, Graph oder Katalog auf der Leinwand = ein neuer Knoten (generisch: mit Typargument);
  //   Technik oder Konfig auf einem Knoten = ein Konstruktor-Parameter seiner Funktion.
  function komAblegen(id,daten,zielKnoten,ev){const g=komModell(id);if(!g)return;const bk=komBaukasten();
    if(daten.gruppe==="technik"||daten.gruppe==="konfig"){if(!zielKnoten){deFlash("ℹ "+daten.name+" auf einen Knoten ziehen — es kommt in seinen Konstruktor",true);return;}
      const kn=(g.knoten||[]).find(x=>x.name===zielKnoten),f=kn&&MODEL.bausteinFunktionen.find(x=>x.name===komBasis(kn.typ));
      if(!f||istGerufen(f)){deFlash("ℹ Nur eine Funktion des Graphen nimmt Technik und Konfig",true);return;}
      if(f.klasse===false){deFlash("ℹ "+f.name+" ist eine Schnittstelle — Technik nehmen ihre Umsetzungen (im Panel)",true);return;}
      const liste=ctorVon(f,null);
      if(daten.gruppe==="technik")nimmGerufene(liste,MODEL.bausteinFunktionen.find(x=>x.name===daten.name),f);
      else if(!liste.some(p=>p.typ===daten.name))liste.push({typ:daten.name,name:paramName(daten.name,liste.map(p=>p.name))});
      gTitel(f.name+" nimmt "+daten.name);render();return;}
    if(daten.gruppe==="funktionen"||daten.gruppe==="graphen"||daten.gruppe==="katalog"){
      const x=(bk[daten.gruppe]||[]).find(e=>e.name===daten.name)||{};
      if((x.typargumente||[]).length)menueFix(ev.clientX,ev.clientY,daten.name+"‹…› — mit welchem Typargument?",x.typargumente.map(t=>({t:daten.name+"<"+t+">",fn:()=>neuerGraphKnoten(g,daten.name+"<"+t+">")})));
      else neuerGraphKnoten(g,daten.name);return;}
    deFlash("ℹ Nachrichten entstehen an der Signatur ihrer Funktion (Panel)",true);}
  function renderGraph(){
    const root=document.getElementById("de-form");
    const altI=root.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    root.innerHTML="";
    root.append(datalistEl());
    ISLE=inselMenge();       // einsame Inseln (unverbundene Knoten) — vom Server
    berechneSicht();         // sichtbar / eingeklappte Details / zusammengezogene Kanten
    if(SEL&&!NODEBY.has(SEL))SEL=null;fokusNeu();
    root.append(ansichtLeiste());
    SLOTS={};SYNC={};   // Code-Sync-Registry je Render neu aufbauen (nur sichtbare Knoten pollen)
    canvas=h("div",{class:"gcanvas ablauf"});   // die Karten-Darstellung (Kopf ohne Körper; bearbeitet wird im Panel)
    world=h("div",{class:"gworld"+(VIEW.kompakt?" kompakt":"")});
    svg=document.createElementNS(SVGNS,"svg");svg.setAttribute("class","gedges");world.append(svg);
    // Die Fläche in sechs Spalten (board.sicht.flaeche) — neu angelegte Karten erscheinen, sobald der Code sie trägt (Board vom Server).
    const vis=flKarten();FLKARTE=new Map(FL().karten.map(k=>[k.id,k]));FLPORT=null;
    vis.forEach(k=>world.append(flKarteEl(k)));
    svgTop=document.createElementNS(SVGNS,"svg");svgTop.setAttribute("class","gedges top");world.append(svgTop);
    if(!vis.length)world.append(h("div",{class:"gempty"},LADEN&&!LADEN.length?"Leeres Board — über die Palette neu entwerfen oder „🗂 Domänen laden“."
      :"Nichts sichtbar — nichts geladen (🗂 Domänen laden), alles ausgeblendet (👁) oder leeres Modell."));
    canvas.append(world);root.append(canvas);
    beobachteCanvas();
    buildMinimap(canvas);
    renderFilterPanel();
    packSpalten();  // jetzt ist world im Dokument → echte Kartengrößen messbar
    messeWelt();   // Geometrie-Cache (Minimap, Culling) + Weltgröße = Inhalt
    applyPan();
    drawMinimap();
    canvas.onpointerdown=e=>{const t=e.target;
      if(t.closest&&(t.closest(".ghead")||t.closest("input,textarea,select,button,.slot")))return;
      startPan(e);};
    canvas.ondblclick=e=>{if(e.target.closest&&e.target.closest(".gnode2"))return;
      const w=toWorld(e.clientX,e.clientY),r=canvas.getBoundingClientRect();
      showPicker(canvas,e.clientX-r.left,e.clientY-r.top,w.x,w.y);};
    canvas.onwheel=e=>{e.preventDefault();const r=canvas.getBoundingClientRect(),mx=e.clientX-r.left,my=e.clientY-r.top;
      const ns=Math.min(2,Math.max(0.08,PAN.s*(e.deltaY<0?1.1:0.9))),wx=(mx-PAN.x)/PAN.s,wy=(my-PAN.y)/PAN.s;
      PAN.s=ns;PAN.x=mx-wx*ns;PAN.y=my-wy*ns;applyPan();};
    // Klick ins Leere (ohne Pannen) = Auswahl/Fokus aufheben.
    canvas.onclick=e=>{if(PANNED)return;const t=e.target;if(FLPORT&&!(t.closest&&t.closest(".fk-port"))){FLPORT=null;flMarkiere();return;}
      if(t.closest&&t.closest(".gnode2,.ginsp,.gfilter,.gminimap,.gpick"))return;if(SEL)waehle(null);};
    drawEdges();requestAnimationFrame(drawEdges);
    zeigeInspector();
    zeigeStart();
    if(VB)vbZeige(false);
    if(KOM_NACH&&komDaten(KOM_NACH)){if(KOM){KOM.pfad.push(KOM_NACH);KOM.wahl=null;KOM.port=null;}else KOM={pfad:[KOM_NACH],reiter:"komposition",wahl:null};KOM_NACH=null;}
    if(KOM)komZeige();   // ein offenes Kompositions-Modal folgt dem neuen Board
  }
  function showPicker(canvas,sx,sy,wx,wy){
    canvas.querySelectorAll(".gpick").forEach(p=>p.remove());
    const pick=h("div",{class:"gpick"});pick.style.left=sx+"px";pick.style.top=sy+"px";
    const opt=(kind,label)=>h("button",{onclick:()=>{pick.remove();neuerKnoten(kind,wx,wy);}},label);
    pick.append(h("div",{class:"gpick-t"},"Was soll hier entstehen?"),
      opt("aggregate","Aggregat"),h("button",{onclick:e=>{pick.remove();graphNeuMitKnoten(e,{x:wx,y:wy});}},"⬡ Graph"),opt("funktion","ƒ Funktion"),opt("quelle","📥 Quelle"),opt("gerufen","⚙ Technik"),
      opt("akteur","👤 Akteur"),opt("client","🔌 Client"),opt("projektion","Projektion"),
      h("button",{onclick:e=>{pick.remove();katalogNeu(e);}},"📚 Katalog"));
    canvas.append(pick);
    setTimeout(()=>{const off=ev=>{if(!pick.contains(ev.target)){pick.remove();document.removeEventListener("pointerdown",off);}};document.addEventListener("pointerdown",off);},0);
  }

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
  const GSAMMLUNGEN=["records","enums","aggregate","decider","applier","graphen","bausteinFunktionen","akteure","clients","readModels","stores","projektionen","reader"];
  // Natürliche Schlüssel — dieselbe Regel wie der Server (GraphExtractor.KartenSchluessel).
  const GKEY=c=>c==="decider"?(d=>(d.aggregat||"")+"|"+(d.command||"")):c==="applier"?(a=>(a.aggregat||"")+"|"+(a.event||""))
    :["readModels","stores","projektionen","reader"].includes(c)?(x=>x.name||""):(x=>(x.namespace||"")+"|"+(x.name||""));
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
      if(!a.ok){deFlash("⚠ "+(a.fehler||"Geste nicht geschrieben"),false);G_LOESCH=null;G_BOARD=a.board;gBoardAnwenden();return;}
      // Neu Angelegtes bleibt sichtbar: seine Domäne zählt ab jetzt als geladen.
      if(LADEN&&delta.neueNs.length)delta.neueNs.forEach(ns=>{if(!LADEN.some(g=>drinNs(g,ns)))LADEN.push(ns);});
      gMeldung(a);GSTAND=jetzt;G_BOARD=a.board;await wbLadeDiagnosen();loeschBefunde();
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
  // Kataloge (docs/plan-katalog-und-graphen.md, Schritt 2): anlegen (Projekt, Solution, Referenz der Abläufe) bzw. nutzen — eine Geste am Server, der
  //   danach kalt neu lädt; das neue Board kommt mit der Antwort.
  async function gKatalog(body,titel){await gesteFlush();G_LAEUFT=true;
    try{const r=await fetch("/api/werkbank/katalog",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(body)});
      const a=await r.json();
      if(!a.ok){deFlash("⚠ "+(a.fehler||titel+" nicht geschrieben"),false);return false;}
      gMeldung(a);await wbLadeDiagnosen();G_BOARD=a.board;gBoardAnwenden();return true;}
    catch(e){badge(false);deFlash("⚠ Werkbank nicht erreichbar — "+titel+" nicht geschrieben",false);return false;}
    finally{G_LAEUFT=false;}}
  function katalogNeu(ev){frageFix(ev.clientX,ev.clientY,"📚 Neuer Katalog","Bildverarbeitung",
    "Ein eigenes Projekt <Name>.Katalog: Funktionen und ihre Nachrichten, ohne Domäne — die Abläufe der Anwendung nutzen es.",n=>{
      if(!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*(\.[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*)*$/.test(n.replace(/\.Katalog$/,"")))
        return "Ein Namespace: groß beginnen, nur Buchstaben und Ziffern, Teile durch Punkte";
      gKatalog({name:n},"Katalog "+n);});}
  // (Die Id einer neuen Funktion ist die, die der Server ihr gibt — „funktion:<Name>“ —, so bleibt ein offenes Modal nach dem Schreiben offen.)
  // Eine neue Funktion im Katalog: erst der Bereich (Ordner im Katalog), dann der Name — sie entsteht als Klasse ohne Auf (unfertig, baut nicht: W1);
  //   die Eingänge und Ausgaben bekommt sie im Formular.
  function katalogFunktion(katalog,ev,danach){const bereiche=[...new Set(MODEL.bausteinFunktionen.map(f=>f.namespace||"")
      .filter(ns=>ns.startsWith(katalog+".")).map(ns=>ns.slice(katalog.length+1).split(".")[0]))];
    frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion in "+katalog+" — Bereich",bereiche[0]||"Allgemein","Der Ordner im Katalog (Namespace "+katalog+".<Bereich>).",b=>{
      if(!/^[A-ZÄÖÜ][A-Za-zÄÖÜäöüß0-9_]*$/.test(b))return "Ein Bereich: groß beginnen, nur Buchstaben und Ziffern";
      setTimeout(()=>frageFix(ev.clientX,ev.clientY,"ƒ Neue Funktion in "+katalog+"."+b,"","Eine Klasse : IFunktion — Eingänge und Ausgaben danach im Formular.",n=>{
        const f=nameFehler(n);if(f&&!/gibt es schon/.test(f))return f;if(MODEL.bausteinFunktionen.some(x=>x.name===n))return n+" gibt es schon";
        const fb={_id:"funktion:"+n,name:n,namespace:katalog+"."+b,klasse:true,eingaenge:[],konstruktor:[],umsetzungen:[]};
        MODEL.bausteinFunktionen.push(fb);gTitel("ƒ "+n+" in "+katalog);render();if(danach)danach(fb);}),0);});}
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

  function maxId(m){let mx=0;JSON.stringify(m||{}).replace(/"_id":"[a-z_]*?(\d+)"/g,(_,n)=>{mx=Math.max(mx,+n);return _;});return mx;}
  function uebernimm(code,alt){
    NID=Math.max(NID,maxId(alt)+1);
    const neu=normalize(code?JSON.parse(JSON.stringify(code)):null);
    GSAMMLUNGEN.forEach(col=>(neu[col]||[]).forEach(x=>{x.ausCode=true;}));
    if(!alt)return neu;
    alt=normalize(alt);
    GSAMMLUNGEN.forEach(col=>{const key=GKEY(col),altBy=new Map((alt[col]||[]).map(x=>[key(x),x]));
      (neu[col]||[]).forEach(l=>{const s=altBy.get(key(l));if(!s)return;if(s.x!=null){l.x=s.x;l.y=s.y;}});});
    // Abgeleitete Knoten: State-Knoten (Lage); die Rumpf-Kacheln sind abgeleitet (Lage über ihre Id in der Ansicht).
    const altStates=new Map((alt.states||[]).map(x=>[x.aggregat,x]));
    neu.states.forEach(x=>{const a=altStates.get(x.aggregat);if(a&&a.x!=null){x.x=a.x;x.y=a.y;}});
    return neu;}
  window.deDownload=function(){const blob=new Blob([JSON.stringify(MODEL,null,2)],{type:"application/json"});
    const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download="domain-model.json";a.click();};
  // Das MODEL als Gesten-Stand (Schnappschuss): Rümpfe aus dem Code, ein leerer Rumpf bleibt leer.
  function payload(){const m=JSON.parse(JSON.stringify(MODEL));
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
  const HOST_ORDNUNG=["Umgebung","EventStore","VersionIndex","Cluster","Schreibpfad","Lebenszyklus","Konsum","Transport","Identitaeten","Quellen","Funktionen","Konfig"];
  const HOST_SYM={Umgebung:"🏷",EventStore:"🗄",VersionIndex:"⚡",Cluster:"🕸",Schreibpfad:"✍",Lebenszyklus:"⏳",Konsum:"📥",Transport:"🔗",Identitaeten:"🪪",Quellen:"📁",Funktionen:"ƒ",Konfig:"⚙",Uhr:"🕰"};
  function hostKnoten(){if(!WB.schema)return [];
    const ab=[...HOST_ORDNUNG.filter(k=>WB.schema[k]),...Object.keys(WB.schema).filter(k=>!HOST_ORDNUNG.includes(k)),"Uhr"];
    return [...ab.map(k=>({id:"host:"+k,name:(HOST_SYM[k]||"")+" "+k,kind:"host",ref:WB.refs[k]||(WB.refs[k]={abschnitt:k})})),
      // Instanzen der Graphen (docs/10 §10.3.5): je Datei graphen/<instanz>.json eine Karte, dazu eine zum Anlegen.
      ...Object.keys(WB.instanzen||{}).sort().map(i=>({id:"host:inst:"+i,name:"⬡ "+i,kind:"host",ref:WB.refs["inst:"+i]||(WB.refs["inst:"+i]={instanz:i})})),
      {id:"host:inst-neu",name:"＋ Instanz",kind:"host",ref:WB.refs["inst-neu"]||(WB.refs["inst-neu"]={instanzNeu:true})}];}
  const wbJson=async(url,opt)=>{const r=await fetch(url,opt);if(!r.ok)throw new Error("HTTP "+r.status);return r.json();};
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
    // Schlüssel aus dem Code (eintraege: je gerufene Funktion/Konfig-Record sein eigenes Schema) — das Formular kommt fertig vom Server.
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
    if(n.ref.instanzNeu){instanzNeuPanel(body);return;}
    if(n.ref.instanz){instanzPanel(body,n.ref.instanz);return;}
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
  // Umgebungs-Werte sind eine Geste (Verlauf, ↶, auf main gesperrt).
  async function wbSpeichern(){try{const a=await wbJson("/api/werkbank/umgebung/"+encodeURIComponent(WB.umgName),{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(WB.umg)});
      if(!a.ok){deFlash("⚠ nicht geschrieben: "+(a.fehler||"?"),false);return;}
      WB.dirty=false;deFlash("💾 umgebung."+WB.umgName+".json geschrieben (↶ nimmt es zurück) — wirksam nach ⟳",true);await wbZustand();render();}
    catch(e){deFlash("⚠ nicht geschrieben: "+e.message,false);}}

  // ── Instanzen (docs/10 §10.3.5): eine Datei je Instanz — Graph, bei einer Quelle der Akteur, Rechner, je Knoten Rahmenfelder, Konfig-Werte und die
  //    Wahl der Technik. Das Formular leitet der Server aus dem Graphen ab (/api/werkbank/instanzschema); geschrieben wird als Geste (↶). ──
  async function instanzGeste(pfad,methode,inhalt,titel){
    try{const r=await fetch(pfad,{method:methode,headers:{"content-type":"application/json"},body:inhalt===undefined?undefined:JSON.stringify(inhalt)});const a=await r.json();
      if(!a.ok){deFlash("⚠ "+(a.fehler||"nicht geschrieben"),false);return false;}
      deFlash(titel+" · "+(a.dateien||[]).join(", ")+" (↶ nimmt es zurück)",true);await wbLadeUmgebung();await wbLadeDiagnosen();G_BOARD=a.board;gBoardAnwenden();return true;}
    catch(e){deFlash("⚠ nicht geschrieben: "+e.message,false);return false;}}
  const instanzPfad=n=>"/api/werkbank/instanz/"+encodeURIComponent(WB.umgName||"")+"/"+encodeURIComponent(n);
  async function instanzAnlegen(graph,name){name=name||prompt("Name der Instanz (Dateiname in umgebungen/"+(WB.umgName||"?")+"/graphen/):",graph.replace(/^./,c=>c.toLowerCase()));
    if(!name||!/^[A-Za-z0-9_-]+$/.test(name))return;
    if(await instanzGeste(instanzPfad(name),"POST",{Graph:graph},"⬡ Instanz "+name+" angelegt"))waehle("host:inst:"+name);}
  function instanzNeuPanel(body){
    body.append(h("div",{class:"gi-hint"},"Welcher Graph soll in „"+(WB.umgName||"?")+"“ laufen? Ein Graph mit Quelle beliebig oft (je Linie, je Verzeichnis …), einer mit Command-Kopf genau einmal."));
    const box=h("div",{style:"display:flex;flex-wrap:wrap;gap:4px"});
    (MODEL.graphen||[]).filter(g=>g.datei).forEach(g=>box.append(h("button",{class:"akt-dom",onclick:()=>instanzAnlegen(g.name)},"⬡ "+g.name)));
    body.append(box);}
  function instanzPanel(body,name){const wert=JSON.parse(JSON.stringify((WB.instanzen||{})[name]||{}));
    body.append(h("div",{class:"gi-hint"},"umgebungen/"+(WB.umgName||"?")+"/graphen/"+name+".json — leer = Standard; ${branch} und ${env:NAME} werden beim Start ersetzt. Wirksam nach ⟳."));
    const box=h("div",{},h("div",{class:"gi-hint"},"lädt …"));body.append(box);
    (async()=>{let sch;try{sch=await wbJson("/api/werkbank/instanzschema?graph="+encodeURIComponent(wert.Graph||""));}catch(e){sch={fehler:e.message};}
      box.innerHTML="";
      const g=(MODEL.graphen||[]).find(x=>x.name===wert.Graph);
      box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Graph"),h("button",{class:"akt-dom",onclick:()=>g&&waehle("gr:"+g._id)},"⬡ "+(wert.Graph||"?")),
        sch.commandKopf?h("span",{class:"slotlbl",style:"opacity:.7"},"Command-Kopf: je Umgebung genau eine Instanz"):null));
      if(sch.fehler){box.append(h("div",{class:"gi-hint",style:"color:#cf6f68"},sch.fehler));return;}
      if(sch.quelle||wert.Akteur){const r=h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px;cursor:default;white-space:normal"},
          h("span",{class:"slotlbl"},"Akteur"+(wert.Akteur?"":" ⚠ fehlt — eine Quelle gibt für einen Akteur hinein")));
        MODEL.akteure.forEach(a=>r.append(h("button",{class:"akt-dom"+(wert.Akteur===a.name?" an":""),onclick:()=>{if(wert.Akteur===a.name)delete wert.Akteur;else wert.Akteur=a.name;
          [...r.querySelectorAll("button")].forEach(b=>b.classList.toggle("an",b.textContent==="👤 "+wert.Akteur));}},"👤 "+a.name)));box.append(r);}
      box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Rechner"),h("input",{value:wert.Rechner||"",placeholder:sch.rechnerPflicht?"Pflicht im Cluster (Zustand oder Quelle)":"ohne = auf jedem Host",
        oninput:e=>{const v=e.target.value.trim();if(v)wert.Rechner=v;else delete wert.Rechner;}})));
      const setze=(w,f,v)=>{if(v===""||v==null)delete w[f];else w[f]=v;};
      const feld=(w,f,typ,d)=>{d=d||{};
        if(d.werte||typ==="bool"){const werte=d.werte||["true","false"],s=h("select",{onchange:e=>setze(w,f,e.target.value===""?null:typ==="bool"?e.target.value==="true":e.target.value)},
            h("option",{value:""},"— Standard"+(d.standard!=null?" ("+d.standard+")":"")));werte.forEach(x=>{const o=h("option",{value:x},x);if(String(w[f])===x)o.selected=true;s.append(o);});return s;}
        return h("input",{value:w[f]??"",placeholder:(typ==="TimeSpan"?"hh:mm:ss":typ)+(d.standard!=null?" · Standard "+d.standard:""),
          oninput:e=>{const v=e.target.value;setze(w,f,v===""?null:/^(int|long)$/.test(typ)&&/^-?\d+$/.test(v)?Number(v):v);}});};
      (sch.knoten||[]).forEach(kn=>{const w=wert[kn.pfad]=wert[kn.pfad]||{};
        box.append(h("div",{class:"gsec",title:kn.typ},(kn.quelle?"⚡ ":"ƒ ")+kn.pfad+" · "+kn.funktion+(kn.zustand?" · Zustand "+kn.zustand:"")+(kn.bekannt?"":" ⚠ unbekannt")));
        (sch.rahmenfelder||[]).forEach(r=>box.append(h("div",{class:"frow"},h("span",{class:"slotlbl",style:"min-width:110px"},r.feld),feld(w,r.feld,r.typ,{standard:r.typ==="bool"?"false":r.typ==="TimeSpan"?"ohne":"Standard"}))));
        (kn.technik||[]).forEach(t=>{w.Technik=w.Technik||{};const s=h("select",{onchange:e=>setze(w.Technik,t.schnittstelle,e.target.value||null)},h("option",{value:""},"— wählen"));
          t.umsetzungen.forEach(u=>{const o=h("option",{value:u},u);if(w.Technik[t.schnittstelle]===u)o.selected=true;s.append(o);});
          box.append(h("div",{class:"frow"},h("span",{class:"slotlbl",style:"min-width:110px"},"⚙ "+t.schnittstelle),s));});
        (kn.konfigs||[]).forEach(c=>(c.felder||[]).forEach(f=>box.append(h("div",{class:"frow",title:c.record+(c.umsetzung?" ("+c.umsetzung+")":"")+" · "+f.typ},
          h("span",{class:"slotlbl",style:"min-width:110px"},f.feld),feld(w,f.feld,f.typ.replace(/\?$/,""),{standard:f.standard,werte:f.werte})))));});
      const sauber=o=>{if(Array.isArray(o))return o;if(o&&typeof o==="object"){const a={};for(const [k,v] of Object.entries(o)){const b=sauber(v);
        if(b==null||(typeof b==="object"&&!Array.isArray(b)&&!Object.keys(b).length))continue;a[k]=b;}return a;}return o;};
      box.append(h("div",{class:"frow"},h("button",{class:"act go",onclick:()=>instanzGeste(instanzPfad(name),"POST",sauber(wert),"⬡ Instanz "+name)},"💾 In "+name+".json schreiben"),
        h("button",{class:"act",onclick:async()=>{if(!confirm("Instanz "+name+" entfernen?"))return;if(await instanzGeste(instanzPfad(name),"DELETE",undefined,"⬡ Instanz "+name+" entfernt"))waehle(null);}},"Entfernen")));})();}
  // ── Befunde an der Karte: Compiler/Analyzer (Typ + Member aus dem Syntaxbaum) und Boot-Fehler (Karte im Wirker-Format) ──
  // Die Karte zu einer stabilen Id (Werkbank P4.8): Wirker-Ids und natürliche Schlüssel liefert der Server als _id (decide:A/C, fluss:P#k,
  //   prozess:P#A→C, funktion:F, projektion:P …); Karten mit Namens-Id (rec:/agg:/enum:/saga:/host:) sind schon die Karten-Id.
  //   Eine Quelle ist ihr Nachrichten-Record.
  function karteVonId(id){if(!id)return null;if(NODEBY.has(id))return id;
    if(id.startsWith("quelle:"))return recByName(id.slice(7))?"rec:"+id.slice(7):null;
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
    try{WB.umg=await wbJson("/api/werkbank/umgebung/"+encodeURIComponent(n));WB.umgName=n;WB.dirty=false;}catch(e){WB.umg=null;}
    try{WB.instanzen=await wbJson("/api/werkbank/instanzen/"+encodeURIComponent(n));}catch(e){WB.instanzen={};}}
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
