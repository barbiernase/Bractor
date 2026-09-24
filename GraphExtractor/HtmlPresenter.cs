namespace GraphExtractor;

/// <summary>
/// Rendert die EINE Oberfläche: den Domänen-Editor (Route <c>SimHost /editor</c>) — Node-Editor, Code-Sync,
/// Prüfen/Kompilieren und die Simulation (Command → Decider → Events → Applier → Saga, animiert auf den Knoten).
/// Das frühere read-only Event-Modeling-Board ist darin aufgegangen.
/// </summary>
public static class HtmlPresenter
{
    /// <summary>
    /// Die Editor-Seite (Route <c>/editor</c>): self-contained HTML/CSS/JS. Bootet aus dem Code
    /// (<c>/api/editor/model</c>) und merged das gespeicherte Board (Layout, Entwürfe) darüber.
    /// </summary>
    public static string EditorPage() =>
        """
        <!doctype html>
        <html lang="de">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Domänen-Editor</title>
        <style>html,body{margin:0;height:100%;background:#0d1017}</style>
        </head>
        <body>
        __EDITOR__
        <script>
          // Standalone: Launch-/Schließen-Button weg, Editor sofort zeigen, aus dem Code booten.
          var ob=document.getElementById('de-open'); if(ob)ob.style.display='none';
          var cb=document.getElementById('de-close'); if(cb)cb.style.display='none';
          deShow(); deBoot();
        </script>
        </body>
        </html>
        """.Replace("__EDITOR__", EditorBlock.Replace("/*__MODEL_JSON__*/", "null"));

    private const string EditorBlock = """
<style>
#de-open{position:fixed;top:12px;right:12px;z-index:40;background:#7c5cff;color:#fff;border:0;
  border-radius:8px;padding:8px 14px;font:600 13px system-ui;cursor:pointer;box-shadow:0 2px 8px #0006}
#de-open:hover{background:#8f73ff}
#de{position:fixed;inset:0;z-index:50;background:#0d1017;color:#dfe4ee;display:none;
  font:13px/1.5 system-ui;flex-direction:column}
#de.on{display:flex}
#de header{display:flex;align-items:center;gap:8px;padding:10px 14px;border-bottom:1px solid #232a38;background:#141926}
#de header h2{font-size:15px;margin:0;font-weight:700}
#de header .sp{flex:1}
#de header .badge{font-size:11px;padding:2px 8px;border-radius:10px;background:#243}
#de header .badge.off{background:#422}
#de button.act{background:#233047;color:#cfe;border:1px solid #35507a;border-radius:6px;padding:6px 12px;cursor:pointer;font:600 12px system-ui}
#de button.act:hover{background:#2c3d5c}
#de button.act.go{background:#2e7d5b;border-color:#3fae7f}
#de button.act.go:hover{background:#369268}
#de button.act.run{background:#4a3a7a;border-color:#7c5cff;color:#e0d4ff}
#de button.act.run:hover{background:#5a4892}
#de .test .trow{display:flex;align-items:center;gap:8px;margin:3px 0}
#de .test .trow label{min-width:180px;color:#9aa3b7;font-size:12px}
#de .test .trow input{flex:1}
#de .test .tframe{padding:4px 8px;border-radius:5px;background:#12261c;color:#9be3bf;margin:3px 0;font:12px ui-monospace,monospace}
#de .test .tframe.rej{background:#3a1f28;color:#ffb3c1}
#de .test .tstate{padding:4px 8px;border-radius:5px;background:#141926;color:#cfe;margin:3px 0;font:12px ui-monospace,monospace}
#de .cols{flex:1;display:grid;grid-template-columns:1fr;overflow:hidden}
#de.simon .cols{grid-template-columns:1fr minmax(320px,400px)}
#de .sim{display:none}
@media (max-width:1100px){#de .cols{position:relative}#de.simon .cols{grid-template-columns:1fr}
  #de.simon .sim{position:absolute;top:0;right:0;bottom:0;width:min(360px,88vw);z-index:40;box-shadow:-8px 0 24px rgba(0,0,0,.5)}}
#de.simon .sim{display:flex;flex-direction:column;gap:10px;overflow:auto;border-left:1px solid #232a38;background:#10141d;padding:12px;font-size:12px}
#de .sim h4{margin:6px 0 2px;font-size:11px;letter-spacing:.06em;text-transform:uppercase;opacity:.7}
#de .sim .row{display:flex;gap:6px;flex-wrap:wrap;align-items:center}
#de .sim select,#de .sim input,#de .sim textarea{background:#0a0d13;color:#dfe4ee;border:1px solid #2a3344;border-radius:5px;padding:4px 6px;font:12px ui-monospace,monospace;min-width:0}
#de .sim .fld{display:grid;grid-template-columns:120px 1fr;gap:6px;align-items:center;margin:3px 0}
#de .sim .fld label{opacity:.8;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#de .sim .fld select,#de .sim .fld input,#de .sim .fld textarea{width:100%;box-sizing:border-box}
#de .sim .frame{border-left:3px solid #ffb86b;background:#151b27;border-radius:4px;padding:5px 8px;margin:4px 0;cursor:pointer}
#de .sim .frame.saga{border-left-color:#b48cff}
#de .sim .frame.rej{border-left-color:#ff6b81}
#de .sim .frame .ev{display:block;margin-left:10px;opacity:.9}
#de .sim .frame .ev.rej{color:#ff9aa9}
#de .sim .frame .warum{opacity:.6}
#de .sim .inst{background:#151b27;border-radius:4px;padding:5px 8px;margin:4px 0;cursor:pointer}
#de .sim .inst .f{display:block;margin-left:8px;opacity:.85}
#de .sim .inst .f.neu{color:#ffd76a;opacity:1}
#de .sim .hinweis{background:#2a2410;color:#ffd76a;border-radius:4px;padding:4px 8px}
#de .sim .fehler{background:#3a1a20;color:#ffb3c1;border-radius:4px;padding:4px 8px;margin:2px 0}
#de .gnode2.simhot{outline:3px solid #ffb86b;outline-offset:2px;box-shadow:0 0 28px #ffb86bcc;z-index:9}
#de .gnode2.simhot.simrej{outline-color:#ff6b81;box-shadow:0 0 28px #ff6b81cc}
#de .gnode2.simspur{outline:2px solid #ffb86b88;outline-offset:2px}
#de .gnode2.simspur.simrej{outline-color:#ff6b8188}
#de .gnode2.abd-voll{box-shadow:0 0 0 3px #3fb950}
#de .gnode2.abd-teil{box-shadow:0 0 0 3px #d29922}
#de .gnode2.abd-kalt{opacity:.4}
#de .ghead .simabd{font-size:9px;background:#0d1017;color:#dfe4ee;border-radius:3px;padding:0 4px;margin-left:auto}
#de .col{overflow:auto;padding:14px}
#de .col.left{border-right:1px solid #232a38}
#de .col.right{display:none}  /* Ausgabe als schwebendes Panel — erscheint, sobald Prüfen/Kompilieren/Testen etwas melden */
#de .col.right.zeigen{display:block;position:fixed;right:16px;bottom:16px;width:min(560px,calc(100vw - 32px));max-height:55vh;z-index:60;box-shadow:0 8px 32px rgba(0,0,0,.55)}
#de .col.right .outzu{position:sticky;top:0;float:right;background:#232a38;color:#cfd6e4;border:0;border-radius:4px;cursor:pointer;padding:2px 8px}
#de .card{background:#141926;border:1px solid #232a38;border-radius:8px;padding:10px 12px;margin:0 0 12px}
#de .card h3{margin:0 0 8px;font-size:13px;display:flex;align-items:center;gap:6px}
#de .card h3 .k{font-size:10px;text-transform:uppercase;letter-spacing:.5px;padding:1px 6px;border-radius:8px}
#de .k.agg{background:#4a3a1f;color:#fbd38d}
#de .k.saga{background:#3a2a4a;color:#e0b0ff}
#de .k.cmd{background:#20344f;color:#9cc9ff}
#de .k.evt{background:#4a3a1f;color:#fbd38d}
#de .k.rej{background:#3a1f28;color:#ffb3c1}
#de .k.vo{background:#26343a;color:#a0e0d0}
#de .kindsel{width:118px}
#de .cbx{font-size:11px;color:#9aa3b7;display:flex;align-items:center;gap:3px}
#de .arow{display:flex;gap:6px;align-items:center;margin:2px 0}
#de .arow select{width:200px}
#de .arow input{flex:1}
/* ── ComfyUI-Node-Editor (getippte Slots + Bézier-Kanten statt Dropdowns) ── */
#de .col.left{overflow:hidden;display:flex;flex-direction:column}
#de .gtoolbar{display:flex;gap:6px;flex-wrap:wrap;margin:0 0 8px}
#de .glegend2{display:flex;gap:12px;flex-wrap:wrap;font-size:11px;color:#9aa3b7;margin:0 2px 8px;align-items:center}
#de .glegend2 span{display:inline-flex;align-items:center;gap:5px}
#de .glegend2 i{width:11px;height:11px;border-radius:50%;display:inline-block}
#de .gcanvas{position:relative;flex:1;min-height:0;overflow:hidden;border:1px solid #232a38;border-radius:8px;
  background-color:#0b0e15;background-image:radial-gradient(#1a2436 1.1px,transparent 1.1px);background-size:22px 22px;cursor:grab;touch-action:none}
#de .gcanvas.panning{cursor:grabbing}
#de .gworld{position:absolute;left:0;top:0;width:6000px;height:4000px;transform-origin:0 0;will-change:transform}
#de svg.gedges{position:absolute;left:0;top:0;width:6000px;height:4000px;pointer-events:none;overflow:visible}
#de .glink{pointer-events:none}
#de .glink.internal{stroke:#7f8aa0;stroke-width:1.5;opacity:.4;pointer-events:stroke;transition:opacity .1s,stroke-width .1s}
#de .glink.internal:hover,#de .glink.internal.hot{stroke:#cbb8ff;stroke-width:3;opacity:1}
/* Minimap (klickbar, zeigt Viewport) + Domänen-Filter */
#de .gminimap{position:absolute;right:10px;bottom:10px;width:212px;height:150px;background:#0b0e15cc;border:1px solid #2c3547;border-radius:8px;overflow:hidden;z-index:20;cursor:pointer;box-shadow:0 6px 20px #0009}
#de .gminimap svg{display:block;width:100%;height:100%}
#de .gminimap .mmvp{fill:#7fb0e61f;stroke:#8fc0ff;stroke-width:1.5}
#de .gfilter{position:absolute;left:10px;top:10px;width:216px;max-height:calc(100% - 20px);overflow:auto;background:#0d1119f2;border:1px solid #2c3547;border-radius:8px;z-index:22;padding:9px;font-size:12px;box-shadow:0 10px 28px #000b}
#de .gfilter h4{margin:0 0 8px;font-size:12px;color:#cbd3e1;display:flex;justify-content:space-between;align-items:center}
#de .gfilter label{display:flex;align-items:center;gap:6px;padding:3px 3px;color:#aab3c5;cursor:pointer;border-radius:4px}
#de .gfilter label:hover{background:#1a2130}
#de .gfilter .mm-q{display:flex;gap:6px;margin-bottom:7px}
#de .gfilter .mm-q button{flex:1;font-size:11px;padding:4px;background:#1a2130;color:#cbd3e1;border:1px solid #2c3547;border-radius:5px;cursor:pointer}
#de .gfilter .mm-q button:hover{background:#222c3d}
/* Code-Knoten: Vorschau im Knoten + anklickbares Modal mit vollem Code */
#de .gcodeprev{margin:4px 0;padding:7px 9px;background:#0d1119;border:1px solid #263041;border-radius:6px;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:11px;line-height:1.45;color:#c7d0df;white-space:pre;overflow:hidden;max-height:130px;cursor:default}
#de .gnode2{position:absolute;width:250px;background:#161b27;border:1px solid #2c3547;border-radius:9px;box-shadow:0 4px 14px #0008}
#de .gnode2.dragging{box-shadow:0 14px 34px #000c;z-index:9}
#de .gnode2.typehi{outline:2px solid #ffd76a;box-shadow:0 0 0 2px #ffd76a55,0 0 20px #ffd76a66;z-index:7}
#de .gnode2.island{outline:1px dashed #e0844d;box-shadow:0 0 0 1px #e0844d44}
#de .gnode2.ungeschrieben{outline:2px dashed #e0b46a;outline-offset:2px}
#de .gnode2.ungeschrieben .ghead::before{content:"✎ ungeschrieben";font-size:9px;color:#3a2a00;background:#e0b46a;border-radius:3px;padding:0 4px;margin-right:4px}
#de .gnode2.entwurf{outline:2px dashed #6aa0e0;outline-offset:2px}
#de .gnode2.entwurf .ghead::before{content:"Entwurf";font-size:9px;color:#06203a;background:#6aa0e0;border-radius:3px;padding:0 4px;margin-right:4px}
#de .gnode2.island .ghead::after{content:"⚠ Insel";font-size:9px;color:#e0a06d;margin-left:6px;opacity:.85}
#de .gtoolbar button.island-btn{border-color:#7a4a2a;color:#e0a06d}
#de .gnode2.pulse{outline:3px solid #7dd3fc;box-shadow:0 0 26px #7dd3fccc;z-index:8;transition:box-shadow .15s}
#de .gtoolbar button.add.jumpable{cursor:pointer}
#de .gminimap svg rect.mmhi{fill:#ffd76a !important;opacity:1 !important;stroke:#fff3c9;stroke-width:.6}
#de .gnode2.n-command{border-color:#3b6fb0}
#de .gnode2.n-event{border-color:#3f9d5a}
#de .gnode2.n-rejection{border-color:#b5504a}
#de .gnode2.n-valueobject{border-color:#2f8f7d}
#de .gnode2.n-enum{border-color:#6a6a86}
#de .gnode2.n-aggregate{border-color:#2f9d95;width:274px}
#de .gnode2.n-decider{border-color:#7a5cc0;width:258px}
#de .gnode2.n-applier{border-color:#c08a3e;width:258px}
#de .gnode2.n-saga{border-color:#8a5cc0;width:300px}
#de .gnode2.n-transition{border-color:#8a6fc8;width:276px}
#de .gnode2.n-state{border-color:#c9a24b;width:250px}
#de .gnode2.n-readmodel{border-color:#b98a3c;width:250px}
#de .gnode2.n-store{border-color:#2f9d95;width:290px}
#de .gnode2.n-projektion{border-color:#3f9d5a;width:274px}
#de .gnode2.n-reaktion{border-color:#c8703a;width:274px}
#de .gnode2.n-pipeline{border-color:#d97b34;width:274px}
#de .gnode2.n-trigger{border-color:#c98a3a;width:262px}
#de .gnode2.n-reader{border-color:#7a5cc0;width:274px}
#de .gnode2.n-query{border-color:#3b6fb0}
#de .gnode2.n-queryresponse{border-color:#2f8f7d}
#de .gnode2.n-codenode{border-color:#6b7280;width:300px}
#de .gnode2.n-llmnode{border-color:#8a6fc8;width:280px}
#de .gnode2.collapsed .gbody{display:none}
#de .ghead{display:flex;align-items:center;gap:6px;padding:5px 9px;border-radius:8px 8px 0 0;cursor:grab;color:#0d0f14;font-weight:700;font-size:12px;user-select:none;touch-action:none}
#de .gnode2.n-command .ghead{background:#5b8fd0}
#de .gnode2.n-event .ghead{background:#57b673}
#de .gnode2.n-rejection .ghead{background:#cf6f68}
#de .gnode2.n-valueobject .ghead{background:#49a996}
#de .gnode2.n-konfig .ghead{background:#8fa3b8}
#de .gnode2.n-enum .ghead{background:#8a8aa0}
#de .gnode2.n-aggregate .ghead{background:#3fb0a6}
#de .gnode2.n-decider .ghead{background:#9678d6}
#de .gnode2.n-applier .ghead{background:#d0a35a}
#de .gnode2.n-saga .ghead{background:#9d78d6}
#de .gnode2.n-transition .ghead{background:#a48fd6}
#de .gnode2.n-state .ghead{background:#d4b45f}
#de .gnode2.n-readmodel .ghead{background:#d0a45a}
#de .gnode2.n-store .ghead{background:#3fb0a6}
#de .gnode2.n-projektion .ghead{background:#57b673}
#de .gnode2.n-reaktion .ghead{background:#d0885a}
#de .gnode2.n-pipeline .ghead{background:#e08a44}
#de .gnode2.n-trigger .ghead{background:#d29a4a}
#de .gnode2.n-reader .ghead{background:#9678d6}
#de .gnode2.n-query .ghead{background:#5b8fd0}
#de .gnode2.n-queryresponse .ghead{background:#49a996}
#de .gnode2.n-codenode .ghead{background:#9aa0aa}
#de .gnode2.n-llmnode .ghead{background:#a48fd6}
#de .ghead .gtitle{flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-family:ui-monospace,monospace}
#de .ghead .gcol{cursor:pointer;opacity:.8;padding:0 2px;font-size:11px}
#de .ghead .gcol:hover{opacity:1}
#de .ghead .gx{cursor:pointer;opacity:.7;padding:0 2px}
#de .ghead .gx:hover{opacity:1}
#de .gbody{padding:8px 10px 10px}
#de .gbody .card{background:none;border:0;box-shadow:none;padding:0;margin:0}
#de .gbody .card h3{margin-bottom:6px}
#de .gbody .card h3 .k{display:none}
#de .gbody input,#de .gbody textarea,#de .gbody select{width:100%;box-sizing:border-box}
#de .gbody .frow{display:flex;gap:4px;margin:2px 0;align-items:center}
#de .gbody .frow input:first-child{flex:1;width:auto}
#de .gbody .frow input:nth-child(2){width:92px;flex:none}
#de .slotrow{display:flex;align-items:center;gap:6px;margin:4px 0;min-height:15px}
#de .slotrow.o{justify-content:flex-end;text-align:right}
#de .slotlbl{font-size:10.5px;color:#b3bbcb;font-family:ui-monospace,monospace;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#de .slot{width:13px;height:13px;border-radius:50%;border:2px solid #0b0e15;cursor:crosshair;flex:none;box-shadow:0 0 0 1px #000a;touch-action:none}
#de .slot.i{margin-left:-17px}
#de .slot.o{margin-right:-17px}
#de .slot.t{margin-top:-16px}
#de .slot.ro{cursor:default;opacity:.9}
#de .slot:hover{filter:brightness(1.4)}
#de .slot.hot{box-shadow:0 0 0 3px #fff,0 0 0 6px #7c5cff}
#de .slot.cand{box-shadow:0 0 0 2px #0b0e15,0 0 0 5px #3fae7f}
#de .codeadd{background:#233047;color:#cfe;border:1px solid #35507a;border-radius:5px;cursor:pointer;font:600 11px system-ui;padding:1px 6px;margin-left:4px}
#de .codeadd:hover{background:#2c3d5c}
#de .slotrow.codeempty .codemiss{color:#e0b46a;font-weight:600}
#de .slotrow.codeempty .slot.s-code{box-shadow:0 0 0 2px #0b0e15,0 0 0 4px #e0b46a66}
#de .slot.s-command{background:#4a86d6}
#de .slot.s-event{background:#4fb06a}
#de .slot.s-rejection{background:#c25b52}
#de .slot.s-decagg{background:#33b1a6}
#de .slot.s-appagg{background:#d1953f}
#de .slot.s-field{background:#e0b64d}
#de .slot.s-open{background:#20293a;border-color:#46557a}
#de .slot.s-saga{background:#9d78d6}
#de .slot.s-sagacmd{background:#9d78d6}
#de .slot.s-arg{background:#e0c46a}
#de .slot.s-prozess{background:#9d78d6}
#de .slot.sm{width:10px;height:10px;box-shadow:0 0 0 1px #000a}
#de .slot.s-state{background:#e0b64d}
#de .slot.s-readmodel{background:#d0a45a}
#de .slot.s-store{background:#3fb0a6}
#de .slot.s-query{background:#5b8fd0}
#de .slot.s-qrsp{background:#49a996}
#de .slot.s-code{background:#9aa0aa}
#de .slot.s-ftype{background:#c58fd6}
#de .slot.s-trigmsg{background:#f0883e}
#de .slot.s-self{background:#c98a3a}
#de .slot.s-prompt{background:#a48fd6}
#de .gtoprow{display:flex;gap:16px;justify-content:center;flex-wrap:wrap;margin-bottom:5px}
#de .gtopfield{display:flex;flex-direction:column;align-items:center;gap:1px}
#de .aggwrap{display:flex;justify-content:space-between;gap:8px;margin:2px 0}
#de .aggwrap .col2{display:flex;flex-direction:column;gap:2px;min-width:92px}
#de .gsec{font-size:9.5px;text-transform:uppercase;letter-spacing:.5px;color:#7f8aa0;margin:6px 0 2px}
#de .gsep{margin:9px 0 3px;padding-top:7px;border-top:1px solid #3a3350;font-size:10px;color:#c9b6ee;text-transform:uppercase;letter-spacing:.5px;font-weight:700}
#de .gempty{position:absolute;left:60px;top:80px;color:#5c6577;font-size:12px;pointer-events:none}
#de .gpick{position:absolute;z-index:20;background:#141926;border:1px solid #7c5cff;border-radius:8px;padding:6px;display:flex;flex-direction:column;gap:3px;box-shadow:0 10px 28px #000b;min-width:150px}
#de .gpick .gpick-t{font-size:11px;color:#9aa3b7;padding:2px 6px 4px}
#de .gpick button{background:#233047;color:#cfe;border:1px solid #35507a;border-radius:5px;padding:5px 10px;cursor:pointer;text-align:left;font:12px system-ui}
#de .gpick button:hover{background:#2c3d5c}
#de .glegend{display:flex;gap:14px;margin:6px 2px 10px;font-size:11px;color:#9aa3b7;flex-wrap:wrap}
#de .glegend span{display:inline-flex;align-items:center;gap:5px}
#de .glegend i{width:20px;height:0;border-top:2px solid;display:inline-block}
#de .grip{cursor:grab;color:#6b7688;padding:0 6px 0 0;font-size:14px;user-select:none}
#de .grip:active{cursor:grabbing}
#de .dz{border:1px dashed #35507a;border-radius:6px;padding:6px 10px;margin:4px 0;color:#7f8aa0;font-size:11px;text-align:center;transition:all .12s}
#de .dz.over{border-color:#7c5cff;border-style:solid;background:#1b2233;color:#cfe}
#de .dz.nope{border-color:#a3435a;color:#ffb3c1}
#de .sub{color:#8b93a7;font-size:11px;margin:8px 0 4px;text-transform:uppercase;letter-spacing:.5px}
#de input,#de select,#de textarea{background:#0d1017;color:#dfe4ee;border:1px solid #2c3547;border-radius:5px;
  padding:4px 6px;font:12px ui-monospace,monospace;box-sizing:border-box}
#de input:focus,#de select:focus,#de textarea:focus{outline:1px solid #7c5cff;border-color:#7c5cff}
#de table{width:100%;border-collapse:collapse;margin-bottom:4px}
#de td{padding:2px 3px;vertical-align:top}
#de .rm{background:#3a1f28;color:#ffb3c1;border:0;border-radius:5px;cursor:pointer;padding:3px 8px;font-weight:700}
#de .add{background:#1f2f28;color:#9be3bf;border:1px dashed #35604c;border-radius:5px;cursor:pointer;padding:3px 10px;font-size:11px}
#de .name{font-weight:700;color:#cfe}
#de .out{font:12px ui-monospace,monospace;background:#0a0d13;border:1px solid #232a38;border-radius:8px;padding:12px;overflow:auto}
#de .out .f{color:#7c5cff;font-weight:700;margin:14px 0 4px;display:block}
#de .out .body{white-space:pre;display:block;margin:0 0 6px}
#de .find{padding:6px 10px;border-radius:6px;margin-bottom:6px;font-size:12px}
#de .find.error{background:#3a1f28;color:#ffb3c1}
#de .find.warning{background:#3a331f;color:#ffe1a0}
#de .hint{color:#8b93a7;font-size:11px;margin:6px 0}
#de .addbar{display:flex;gap:8px;margin-bottom:12px}
#de .sec{margin:0 0 20px}
#de .sectitle{font-size:12px;font-weight:700;color:#b9c2d6;text-transform:uppercase;letter-spacing:.5px;border-bottom:1px solid #232a38;padding-bottom:5px;margin-bottom:9px}
#de .chips{display:flex;flex-wrap:wrap;gap:6px;margin-bottom:7px}
#de .chip{display:flex;gap:4px;align-items:center;background:#141926;border:1px solid #4a3a1f;border-radius:8px;padding:4px 6px}
#de .chip input{width:118px}
#de .cmdbox{border-left:2px solid #35507a;padding-left:8px;margin:8px 0}
#de .cmdhead{display:flex;gap:2px;align-items:center;margin-bottom:3px}
#de .cmdhead .lbl{color:#8b93a7;font-size:11px}
#de .cmdhead select{width:130px}
#de table.grid td{padding:2px 3px}
#de .step{font-size:12px;font-weight:700;color:#cfd7e6;letter-spacing:.3px;margin:14px 0 6px;padding-bottom:3px;border-bottom:1px solid #2b3446}
#de .cmdn{color:#9be3ff;font-weight:700;font-family:ui-monospace,monospace}
#de .applybox{border-left:2px solid #3f6a4a;padding-left:8px;margin:8px 0}
#de .blab{color:#8b93a7;font-size:10px;margin:5px 0 2px}
#de textarea.code{width:100%;box-sizing:border-box;font:12px/1.45 ui-monospace,monospace;background:#0a0d13;tab-size:4}
/* ══ ANSICHT: Kompakt-Karten · Inspector · semantischer Zoom · Slice-Fokus (reine Darstellung) ══ */
#de .gview{display:flex;gap:6px;flex-wrap:wrap;align-items:center;margin:0 0 8px;font-size:11px;color:#9aa3b7}
#de .gview button{background:#1a2130;color:#cbd3e1;border:1px solid #2c3547;border-radius:6px;padding:4px 10px;cursor:pointer;font:600 11px system-ui}
#de .gview button:hover{background:#222c3d}
#de .gview button.on{background:#2b3a5c;border-color:#5b8fd0;color:#e6efff}
#de .gview .lod{display:inline-flex;border:1px solid #2c3547;border-radius:6px;overflow:hidden;margin-left:6px}
#de .gview .lod span{padding:4px 9px;cursor:pointer;color:#8b93a7}
#de .gview .lod span.on{background:#2b3a5c;color:#e6efff}
#de .gview .sep{width:1px;height:18px;background:#2c3547;margin:0 4px}
#de .gworld{--inv:1}
#de .gworld.kompakt .gnode2.collapsed{width:230px}
#de .gworld.kompakt .gnode2:not(.collapsed){z-index:4}
#de .gsum{display:none;padding:4px 9px 7px;font-size:11px;color:#aab3c5;cursor:pointer}
#de .gnode2.collapsed .gsum{display:block}
#de .gsum .gs-t{font-family:ui-monospace,monospace;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#de .gsum .gchips{display:flex;flex-wrap:wrap;gap:3px;margin-top:4px}
#de .gsum .gchip{font-size:9.5px;padding:0 5px;border-radius:7px;background:#232b3b;color:#b9c2d6;border:1px solid #313b50}
#de .gsum .gchip.warn{background:#3a2e14;color:#e0b46a;border-color:#6b5222}
#de .ghead .gtitle .gk{opacity:.75}
#de .gnode2.sel{outline:2px solid #f5f7ff;outline-offset:3px;z-index:6}
#de .gworld.fokus .gnode2:not(.inslice){opacity:.13}
#de .gworld.fokus .glink:not(.inslice){opacity:.04}
#de .gworld.fokus .glink.inslice{opacity:1;stroke-width:3}
#de .glink.kontrakt{opacity:.75}
/* Inspector: Bearbeiten rechts statt Formular im Knoten */
#de .ginsp{position:absolute;right:10px;top:10px;bottom:170px;width:min(380px,calc(100% - 40px));overflow:auto;background:#10141df7;border:1px solid #2c3547;border-radius:10px;z-index:25;box-shadow:0 10px 30px #000c;font-size:12px}
#de .ginsp .gi-h{position:sticky;top:0;z-index:2;display:flex;align-items:center;gap:6px;padding:8px 10px;background:#161c2a;border-bottom:1px solid #2c3547}
#de .ginsp .gi-h .gi-k{font-size:10px;padding:1px 7px;border-radius:8px;color:#0d0f14;font-weight:700}
#de .ginsp .gi-h .gi-n{flex:1;font:700 13px ui-monospace,monospace;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#de .ginsp .gi-h button{background:#1a2130;color:#cbd3e1;border:1px solid #2c3547;border-radius:5px;cursor:pointer;padding:2px 7px;font-size:11px}
#de .ginsp .gi-sec{margin:10px 10px 0;border:1px solid #262f40;border-radius:8px;overflow:hidden}
#de .ginsp .gi-st{padding:4px 9px;font-size:10.5px;font-weight:700;color:#0d0f14;cursor:pointer}
#de .ginsp .gbody{padding:8px 10px 10px}
#de .ginsp .slot{display:none}
#de .ginsp .gcodeprev{max-height:none}
#de .ginsp .gi-rel{margin:10px;font-size:11px}
#de .ginsp .gi-rel h5{margin:8px 0 4px;font-size:10px;text-transform:uppercase;letter-spacing:.5px;color:#7f8aa0}
#de .ginsp .gi-rel a{display:inline-block;margin:0 4px 4px 0;padding:1px 7px;border-radius:7px;background:#1a2130;border:1px solid #2c3547;color:#cbd3e1;cursor:pointer;font-family:ui-monospace,monospace}
#de .ginsp .gi-rel a:hover{border-color:#7c5cff}
/* Kind-Farben (Kopf) für Inspector-Chips */
#de .kc-command{background:#5b8fd0}#de .kc-event{background:#57b673}#de .kc-rejection{background:#cf6f68}#de .kc-valueobject{background:#49a996}
#de .kc-konfig{background:#8fa3b8}#de .kc-enum{background:#8a8aa0}#de .kc-aggregate{background:#3fb0a6}#de .kc-decider{background:#9678d6}
#de .kc-applier{background:#d0a35a}#de .kc-saga{background:#9d78d6}#de .kc-transition{background:#a48fd6}#de .kc-state{background:#d4b45f}
#de .kc-readmodel{background:#d0a45a}#de .kc-store{background:#3fb0a6}#de .kc-projektion{background:#57b673}#de .kc-reaktion{background:#d0885a}
#de .kc-pipeline{background:#e08a44}#de .kc-trigger{background:#d29a4a}#de .kc-reader{background:#9678d6}#de .kc-query{background:#5b8fd0}
#de .kc-queryresponse{background:#49a996}#de .kc-codenode{background:#9aa0aa}#de .kc-llmnode{background:#a48fd6}
#de .kc-frist,#de .kc-dienst,#de .kc-hostsetting{background:#c9a24b}
/* Semantischer Zoom: Landkarte (<0.4) · Ablauf (<0.75) · Detail */
#de .gcanvas.lod-ablauf .gnode2 .gbody,#de .gcanvas.lod-ablauf .gnode2 .gsum{display:none}
#de .gcanvas.lod-ablauf .gnode2{width:230px}
#de .gcanvas.lod-ablauf .ghead{padding:6px 10px;border-radius:8px}
#de .gcanvas.lod-ablauf .ghead .gtitle{font-size:calc(10px*var(--inv));white-space:normal;overflow-wrap:anywhere;line-height:1.15}
#de .gcanvas.lod-ablauf .ghead .gtitle.hatname .gk{display:none}
#de .gcanvas.lod-ablauf .ghead .gcol,#de .gcanvas.lod-ablauf .ghead .gx{display:none}
#de .gcanvas.lod-ablauf .gnode2.ungeschrieben .ghead::before,#de .gcanvas.lod-ablauf .gnode2.entwurf .ghead::before,#de .gcanvas.lod-ablauf .gnode2.island .ghead::after{display:none}
#de .gcanvas.lod-karte .gnode2{visibility:hidden}
#de .gcanvas.lod-karte svg.gedges{display:none}
#de .gtile,#de svg.gtilesvg{display:none}
#de .gcanvas.lod-karte .gtile,#de .gcanvas.lod-karte svg.gtilesvg{display:block}
#de .gtile{position:absolute;box-sizing:border-box;border:calc(2px*var(--inv)) solid;border-radius:calc(12px*var(--inv));background:#141a28d9;cursor:zoom-in;padding:calc(10px*var(--inv)) calc(12px*var(--inv));overflow:hidden}
#de .gtile:hover{background:#1b2336f0}
#de .gtile .gtl-t{font:700 calc(17px*var(--inv))/1.15 ui-monospace,monospace;color:#e6ebf5;overflow-wrap:anywhere}
#de .gtile .gtl-c{font-size:calc(11.5px*var(--inv));line-height:1.35;color:#9aa3b7;margin-top:calc(5px*var(--inv))}
#de .gworld.fokus .gtile:not(.inslice){opacity:.25}
#de svg.gtilesvg{position:absolute;left:0;top:0;width:10px;height:10px;overflow:visible;pointer-events:none}
#de svg.gtilesvg path{fill:none;stroke:#6b7a96;opacity:.7}
#de svg.gtilesvg text{fill:#cbd3e1;font-family:system-ui;font-weight:700;paint-order:stroke;stroke:#0b0e15}
</style>
<button id="de-open" onclick="deOpen()">✎ Editor</button>
<div id="de">
  <header>
    <h2>Domänen-Editor</h2>
    <span id="de-badge" class="badge off">offline</span>
    <span class="sp"></span>
    <button class="act" onclick="deReload()">↻ Vom Graph laden</button>
    <button class="act" onclick="deReflow()">▦ Neu anordnen</button>
    <button class="act" onclick="deFilter()">🗂 Domänen</button>
    <button class="act" onclick="deValidate()">✓ Prüfen</button>
    <button class="act" onclick="deCompile()">⚙ Kompilieren</button>
    <button class="act run" id="de-simbtn" onclick="deSim()">▶ Simulation</button>
    <button class="act go" onclick="deWrite()">&lt;/&gt; C# schreiben</button>
    <button class="act" onclick="deSave()">💾 Speichern</button>
    <button class="act" onclick="deDownload()">⬇ Modell</button>
    <button class="act" id="de-close" onclick="deClose()">✕ Schließen</button>
  </header>
  <div class="cols">
    <div class="col left" id="de-form"></div>
    <aside class="sim" id="de-sim"></aside>
    <div class="col right out" id="de-out"><div class="hint">„C# erzeugen" rendert den echten C#-Scaffolder (über SimHost). „Prüfen" zeigt die Struktur-Diagnosen. Ohne laufenden SimHost editierst du die Form und lädst das Modell-JSON herunter.</div></div>
  </div>
</div>
<script>
(function(){
  // Skalare = was der Wire trägt (aus dem Codegen, via rahmen.skalare) — keine eigene Liste im Editor.
  const SCALARS=()=>((MODEL.rahmen&&MODEL.rahmen.skalare)||[]);
  // Vertrags-Fakten aus dem Rahmen (vom Extractor aus dem Code gelesen).
  const ID_FELD=()=>((MODEL.rahmen&&MODEL.rahmen.aggregatIdFeld)||"");
  const KINDINFO={command:["Command","cmd"],event:["Event","evt"],rejection:["Ablehnung","rej"],valueobject:["Value Object","vo"],konfig:["Konfiguration","vo"],query:["Query","qry"],queryresponse:["Response","qrsp"]};
  let MODEL={schemaVersion:"2",records:[],enums:[],aggregate:[],decider:[],applier:[],sagas:[],states:[],transitions:[],readModels:[],stores:[],projektionen:[],reader:[],reaktionen:[],pipelines:[],triggers:[],frists:[],dienste:[],hostSettings:[],codeNodes:[],llmNodes:[]};
  let NID=1;
  const embedded=/*__MODEL_JSON__*/;
  // Domänen-Filter (welche Domänen ausgeblendet sind) — pro Browser UND pro Solution persistiert (rahmen.kennung),
  //   damit Zwischenstände verschiedener Repos sich nie vermischen.
  let HKEY="cqrs-hidden-domains", LSKEY="cqrs-board-model";
  let HIDDEN=new Set();
  function schluesselFuer(m){const k=(m&&m.rahmen&&m.rahmen.kennung)||"";
    HKEY="cqrs-hidden-domains"+(k?":"+k:"");LSKEY="cqrs-board-model"+(k?":"+k:"");
    HIDDEN=new Set();try{const s=localStorage.getItem(HKEY);if(s)HIDDEN=new Set(JSON.parse(s));}catch(e){}
    ladeAnsicht(k);}
  function saveHidden(){try{localStorage.setItem(HKEY,JSON.stringify([...HIDDEN]));}catch(e){}}
  let FILTER_OPEN=false, MM=null;
  function domHue(k){let h=0;for(let i=0;i<(k||"").length;i++)h=(h*31+k.charCodeAt(i))>>>0;return h%360;}

  const listeZuText=xs=>(xs||[]).join(", ");
  const textZuListe=t=>(t||"").split(",").map(s=>s.trim()).filter(Boolean);
  const kindLabel=k=>(KINDINFO[k]||["?","vo"])[0];
  const kindKlasse=k=>(KINDINFO[k]||["?","vo"])[1];
  function normalize(m){m=m||{};m.records=m.records||[];m.enums=m.enums||[];m.aggregate=m.aggregate||[];m.decider=m.decider||[];m.applier=m.applier||[];m.sagas=m.sagas||[];m.states=m.states||[];m.transitions=m.transitions||[];
    m.readModels=m.readModels||[];m.stores=m.stores||[];m.projektionen=m.projektionen||[];m.reader=m.reader||[];m.reaktionen=m.reaktionen||[];m.pipelines=m.pipelines||[];m.triggers=m.triggers||[];m.frists=m.frists||[];m.dienste=m.dienste||[];m.hostSettings=m.hostSettings||[];m.codeNodes=m.codeNodes||[];m.llmNodes=m.llmNodes||[];
    // Reaktion = emittierender Konsument (ISubscriber → IAsyncEnumerable<OneOf<Cmd>>): Trigger-Event → Handle → OneOf-Commands.
    m.reaktionen.forEach(r=>{if(!r._id)r._id="rk"+(NID++);r.handles=r.handles||[];r.handles.forEach(hd=>{hd.sends=hd.sends||[];hd.publishes=hd.publishes||[];});});
    // Pipeline = 4. durabler Konsument (IPipelineHandler): Trigger-Msg ODER Event → Handle → OneOf-Command(s).
    m.pipelines.forEach(p=>{if(!p._id)p._id="pl"+(NID++);p.handles=p.handles||[];p.handles.forEach(hd=>{hd.sends=hd.sends||[];hd.emits=hd.emits||[];hd.schedules=hd.schedules||[];
      hd.inputKind=hd.inputKind||(hd.trigId?"trigger":"event");
      if(hd.trigId&&!hd.prod){hd.prod={k:"tg",id:hd.trigId};hd.input=hd.input||"";}});});
    // Trigger = Ingress-Wecker (Timer/Webhook/FileWatch/Frist), erzeugt eine IPipelineTrigger-Nachricht.
    m.triggers.forEach(t=>{if(!t._id)t._id="tg"+(NID++);t.felder=t.felder||[];t.felder.forEach(f=>{if(f&&!f._id)f._id="f"+(NID++);});});
    // Jedes Objekt-Feld bekommt eine stabile _id → Feld-Ports eindeutig (auch bei gleichnamigen Feldern) und rename-fest.
    const stampFelder=arr=>(arr||[]).forEach(f=>{if(f&&!f._id)f._id="f"+(NID++);});
    m.records.forEach(r=>stampFelder(r.felder));m.aggregate.forEach(a=>stampFelder(a.state));m.states.forEach(s=>stampFelder(s.felder));m.readModels.forEach(rm=>stampFelder(rm.felder));
    m.decider.forEach(d=>{if(!d._id)d._id="d"+(NID++);});m.applier.forEach(a=>{if(!a._id)a._id="a"+(NID++);});m.states.forEach(s=>{if(!s._id)s._id="s"+(NID++);});m.transitions.forEach(t=>{if(!t._id)t._id="t"+(NID++);
      // Migration „ein Join, mehrere Dann": flaches sende/kompensation → dann[]. Jeder Dann = ein Command (+ eigene Kompensation).
      if(!t.dann)t.dann=t.sende?[{sende:t.sende,sendeJe:t.sendeJe,sendeJeCollection:t.sendeJeCollection,kompensation:t.kompensation}]:[{}];
      delete t.sende;delete t.sendeJe;delete t.sendeJeCollection;delete t.kompensation;delete t.sendeArgs;delete t.kompArgs;
      });
    m.readModels.forEach(rm=>{if(!rm._id)rm._id="rm"+(NID++);});
    m.stores.forEach(st=>{if(!st._id)st._id="st"+(NID++);st.writeFns=st.writeFns||[];st.readFns=st.readFns||[];st.writeFns.forEach(f=>{if(!f._id)f._id="wf"+(NID++);f.params=f.params||[];});st.readFns.forEach(f=>{if(!f._id)f._id="rf"+(NID++);f.params=f.params||[];});});
    // Projektion-Handle ruft je Event eine (oder mehrere) Write-Fn des Stores → fns[]. Der Store-Scope
    // wird daraus ABGELEITET (= die injizierten Stores), nicht mehr von Hand deklariert.
    m.projektionen.forEach(p=>{if(!p._id)p._id="pj"+(NID++);p.handles=p.handles||[];p.handles.forEach(hd=>{hd.fns=hd.fns||[];hd.publishes=hd.publishes||[];});delete p.stores;});
    m.reader.forEach(r=>{if(!r._id)r._id="rd"+(NID++);r.handles=r.handles||[];delete r.stores;
      // Reader liest genau eine Projektion (IReader<TProjection>) — expliziter Bindungs-Port.
      r.projektion=r.projektion||"";
      r.handles.forEach(hd=>{
        // Query-Handle ruft je Query eine/mehrere Read-Fn (auch über mehrere Stores) → fns[].
        hd.fns=hd.fns||[];
        // Symmetrie zur Schreibseite (decider.ergibt[]): je Query mehrere Responses als OneOf → responses[].
        hd.responses=hd.responses||(hd.response?[hd.response]:[]);delete hd.response;});});
    m.codeNodes.forEach(c=>{if(!c._id)c._id="cn"+(NID++);});m.llmNodes.forEach(l=>{if(!l._id)l._id="ln"+(NID++);});
    // Migration: eingebettete Decide/Apply-Rümpfe → 📝 Code-Knoten (Konsistenz: jede Code-Stelle = Port + Quelle).
    const mig=(node,nm)=>{if(node.rumpf===""&&!node.codeSrc){node.leer=true;delete node.rumpf;return;}
      if(node.rumpf&&!node.codeSrc){const cn={_id:"cn"+(NID++),name:nm||"Code",text:node.rumpf,x:(node.x||0)+320,y:node.y||0};m.codeNodes.push(cn);node.codeSrc=cn._id;delete node.rumpf;}};
    m.decider.forEach(d=>mig(d));m.applier.forEach(a=>mig(a));
    // Leseseiten-Rümpfe: Projektion-/Reader-/Pipeline-Handler + Store-Fn-Impls → eigene Code-Knoten.
    m.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>mig(hd,"Handle "+(hd.event||""))));
    m.reader.forEach(r=>(r.handles||[]).forEach(hd=>mig(hd,"Handle "+(hd.query||""))));
    m.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>mig(hd,"Handle "+(hd.input||hd.event||""))));
    m.stores.forEach(s=>{(s.writeFns||[]).forEach(f=>mig(f,f.name||"Store-Fn"));(s.readFns||[]).forEach(f=>mig(f,f.name||"Store-Fn"));});
    m.aggregate.forEach(a=>{if((a.state||[]).length&&!m.states.some(s=>s.aggregat===a.name))m.states.push({_id:"s"+(NID++),aggregat:a.name});});
    // Round-trip: geladene Saga.Schritte → Transition-Knoten (prozess = Saga-Name), Schritte werden vor Serveraufruf neu erzeugt.
    m.sagas.forEach(s=>{(s.schritte||[]).forEach(st=>m.transitions.push({_id:"t"+(NID++),prozess:s.name,wenn:(st.wenn||[]).slice(),
        ...(st.sammelEvent?{sammelEvent:st.sammelEvent,sammelAusdruck:st.sammelAusdruck,sammelAnzahl:st.sammelAnzahl}:{}),
        dann:[{sende:st.sende||"",sendeJe:!!st.sendeJe,sendeJeCollection:st.sendeJeCollection||"",kompensation:st.kompensation||"",
          sendeAusdruck:st.sendeAusdruck,kompensationAusdruck:st.kompensationAusdruck,kompensationJe:!!st.kompensationJe}]}));s.schritte=[];});
    return m;}
  const aggSelect=(val,on)=>{const s=h("select",{onchange:e=>on(e.target.value)});
    if(!val||!MODEL.aggregate.some(a=>a.name===val)){const o=h("option",{value:val||""},val||"— Aggregat —");o.selected=true;s.append(o);}
    MODEL.aggregate.forEach(a=>{const o=h("option",{value:a.name},a.name);if(a.name===val)o.selected=true;s.append(o);});return s;};
  // Standard-Namespace für neue Knoten: der eines vorhandenen Aggregats/Records; sonst eine Projekt-Wurzel aus dem Code (Rahmen).
  function defaultNs(){const w=Object.keys((MODEL.rahmen||{}).verzeichnisse||{}).sort((a,b)=>a.length-b.length)[0];
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
  // Record-Auswahl gefiltert nach Kind (für Decide/Apply/Saga-Verdrahtung).
  function recSelect(val,on,kinds){const s=h("select",{onchange:e=>on(e.target.value)});
    const opts=MODEL.records.filter(r=>kinds.includes(r.kind)).map(r=>r.name);
    if(!val||!opts.includes(val)){const o=h("option",{value:val||""},val||"— wählen —");o.selected=true;s.append(o);}
    opts.forEach(n=>{const o=h("option",{value:n},n);if(n===val)o.selected=true;s.append(o);});return s;}
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
  function render(){ ELEM=null; deriveMembership(); renderGraph(); autosave(); if(window.simNachRender)simNachRender(); }

  // Eindeutiger Name — Namen sind der Referenzschlüssel für Kanten/Decider/Applier.
  function uniq(base){const all=new Set([...MODEL.records.map(r=>r.name),...MODEL.aggregate.map(a=>a.name),...MODEL.enums.map(e=>e.name),...MODEL.sagas.map(s=>s.name),...MODEL.readModels.map(x=>x.name),...MODEL.stores.map(x=>x.name),...MODEL.projektionen.map(x=>x.name),...MODEL.reader.map(x=>x.name),...MODEL.reaktionen.map(x=>x.name),...MODEL.pipelines.map(x=>x.name),...MODEL.triggers.map(x=>x.name),...MODEL.frists.map(x=>x.name),...MODEL.dienste.map(x=>x.name),...MODEL.hostSettings.map(x=>x.name),...MODEL.codeNodes.map(x=>x.name),...MODEL.llmNodes.map(x=>x.name)]);
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
  function fokussiereNeu(vorher){requestAnimationFrame(()=>{
    const neu=graphNodes().find(n=>!vorher.has(n.id));if(!neu)return;
    if(HIDDEN.has(groupKeyOf(neu))){HIDDEN.delete(groupKeyOf(neu));saveHidden();render();
      requestAnimationFrame(()=>{centerOn(neu);pulseNode(neu);});}
    else{centerOn(neu);pulseNode(neu);}
    deFlash("+ "+(NODELABEL[neu.kind]||neu.kind)+(neu.name?" · "+neu.name:"")+" — hinzugefügt",true);});}
  function neuerKnoten(kind,x,y){
    const vorher=new Set(graphNodes().map(n=>n.id));
    let pos=(typeof x==="number")?{x:Math.round(x/GRID)*GRID,y:Math.round(y/GRID)*GRID}:spawnPos();
    if(VIEW.kompakt&&!pos._kpos)pos={...pos,_kpos:{x:pos.x,y:pos.y}};
    if(kind==="aggregate")MODEL.aggregate.push({name:uniq("NeuesAggregat"),namespace:defaultNs(),state:[],...pos});
    else if(kind==="decider")MODEL.decider.push({_id:"d"+(NID++),aggregat:"",command:"",ergibt:[],...pos});
    else if(kind==="applier")MODEL.applier.push({_id:"a"+(NID++),aggregat:"",event:"",...pos});
    else if(kind==="state")MODEL.states.push({_id:"s"+(NID++),aggregat:"",felder:[],...pos});
    else if(kind==="enum")MODEL.enums.push({name:uniq("NeuEnum"),namespace:defaultNs(),werte:["A","B"],...pos});
    else if(kind==="saga")MODEL.sagas.push({name:uniq("NeuerProzess"),namespace:defaultNs(),triggerEvent:"",schritte:[],extraUsings:[],...pos});
    else if(kind==="transition")MODEL.transitions.push({_id:"t"+(NID++),prozess:"",wenn:[],dann:[{}],...pos});
    else if(kind==="readmodel")MODEL.readModels.push({_id:"rm"+(NID++),name:uniq("NeuReadModel"),namespace:defaultNs(),felder:[{_id:"f"+(NID++),name:"Id",typ:"Guid"}],store:"",...pos});
    else if(kind==="store")MODEL.stores.push({_id:"st"+(NID++),name:uniq("NeuStore"),namespace:defaultNs(),writeFns:[],readFns:[],...pos});
    else if(kind==="projektion")MODEL.projektionen.push({_id:"pj"+(NID++),name:uniq("NeueProjektion"),namespace:defaultNs(),stores:[],append:false,pull:true,handles:[],...pos});
    else if(kind==="reader")MODEL.reader.push({_id:"rd"+(NID++),name:uniq("NeuReader"),namespace:defaultNs(),stores:[],trackDeps:true,handles:[],...pos});
    else if(kind==="reaktion")MODEL.reaktionen.push({_id:"rk"+(NID++),name:uniq("NeueReaktion"),namespace:defaultNs(),pull:true,handles:[],...pos});
    else if(kind==="pipeline")MODEL.pipelines.push({_id:"pl"+(NID++),name:uniq("NeuePipeline"),namespace:defaultNs(),pipelineId:"",handles:[],...pos});
    else if(kind==="trigger")MODEL.triggers.push({_id:"tg"+(NID++),name:uniq("NeuTrigger"),namespace:defaultNs(),msgName:uniq("NeuTriggerMsg"),felder:[],...pos});
    else if(kind==="frist")MODEL.frists.push({_id:"fr"+(NID++),name:uniq("NeueFrist"),kontext:"",dauerSetting:"",plant:[],storniert:[],sendet:"",aggregat:"",...pos});
    else if(kind==="dienst")MODEL.dienste.push({_id:"di"+(NID++),name:uniq("NeuerDienst"),vertrag:"IDienst",extern:false,codeSrc:null,...pos});
    else if(kind==="hostsetting")MODEL.hostSettings.push({_id:"hs"+(NID++),name:uniq("NeuSetting"),typ:"string",default:"",envKey:"",...pos});
    else if(kind==="codenode")MODEL.codeNodes.push({_id:"cn"+(NID++),name:uniq("Code"),text:"",...pos});
    else if(kind==="llmnode")MODEL.llmNodes.push({_id:"ln"+(NID++),name:uniq("LLM"),intent:"",...pos});
    else MODEL.records.push({name:uniq("Neu"+kindLabel(kind).replace(/\s/g,"")),kind,namespace:defaultNs(),felder:kind==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[],...pos});
    render();
    fokussiereNeu(vorher);
  }
  const addRecord=k=>neuerKnoten(k);
  const addAggregat=()=>neuerKnoten("aggregate");
  const addEnum=()=>neuerKnoten("enum");
  const addSaga=()=>neuerKnoten("saga");

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
    if(r.kind==="command"||r.kind==="event"||r.kind==="rejection"){const ag=recordAgg(r.name);
      body.append(h("div",{class:"gsec",title:"abgeleitet aus Decider/Applier"},"Aggregat: "+(ag||"— keinem (Decider/Applier verdrahten)")));}
    if(r.kind==="command")body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.istErzeugung=e.target.checked||undefined,...(r.istErzeugung?{checked:"checked"}:{})}),"Erzeugung (ICreationCommand)"));
    if(r.kind==="command"){body.append(slotRow("sagacmd","◀ ausgelöst von (Saga/Reaktion)","l",{type:"sagaCmd",dir:"in",rec:r.name},"cmd:in:"+r.name));
      body.append(slotRow("command","cmd ▶","r",{type:"cmd",dir:"out",rec:r.name},"cmd:out:"+r.name));}
    else if(r.kind==="event"){body.append(slotRow("event","◀ erzeugt von (Decider / reaktiv veröffentlicht)","l",{type:"evtOut",dir:"in",rec:r.name},"evt:in:"+r.name));
      body.append(slotRow("event","evt ▶","r",{type:"evtUse",dir:"out",rec:r.name},"evt:out:"+r.name));}
    else if(r.kind==="rejection")body.append(slotRow("rejection","◀ von Decider","l",{type:"evtOut",dir:"in",rec:r.name},"evt:in:"+r.name));
    else if(r.kind==="query")body.append(slotRow("query","query ▶","r",{type:"query",dir:"out",rec:r.name},"qry:out:"+r.name));
    else if(r.kind==="queryresponse")body.append(slotRow("qrsp","◀ von Reader","l",{type:"qrsp",dir:"in",rec:r.name},"qrsp:in:"+r.name));
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
    body.append(topSlot("decagg","▲ Aggregat: "+(d.aggregat||"— (ans Aggregat ziehen)"),{type:"decAgg",dir:"out",dec:d._id},"dec:aggout:"+d._id));
    body.append(slotRow("command","◀ Command: "+(d.command||"—"),"l",{type:"cmd",dir:"in",dec:d._id},"dec:cmdin:"+d._id));
    body.append(h("div",{class:"gsec"},"OneOf-Ausgänge — je mögliches Event ein Punkt (Punkt → Event ziehen). Das WANN macht der Decide-Rumpf."));
    (d.ergibt||[]).forEach((o,oi)=>{
      const s=port("event");s.classList.add("o");reg("dec:evtout:"+d._id+":"+o.event,s,{type:"evtOut",dir:"out",dec:d._id});
      body.append(h("div",{class:"slotrow o"},
        h("button",{class:"rm",onclick:()=>{d.ergibt.splice(oi,1);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},(o.event||"?")+" ▶"),s));
    });
    const os=port("open");os.classList.add("o");reg("dec:evtout:"+d._id+":open",os,{type:"evtOut",dir:"out",dec:d._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Ausgang ▶"),os));
    body.append(h("div",{class:"gsec"},"Decide-Rumpf"));
    body.append(codePort("dec:rumpf:"+d._id,{k:"decider",dec:d._id},d.codeSrc,"Decide-Logik"));
  }
  // Applier: gespiegelt zum Decider — Event rein (rechts), Aggregat OBEN. Körper = Code.
  function applierCard(body,a){
    body.append(topSlot("appagg","▲ Aggregat: "+(a.aggregat||"— (ans Aggregat ziehen)"),{type:"appAgg",dir:"out",app:a._id},"app:aggout:"+a._id));
    body.append(slotRow("event","Event: "+(a.event||"—")+" ◀","r",{type:"evtUse",dir:"in",app:a._id},"app:evtin:"+a._id));
    body.append(h("div",{class:"gsec"},"Apply-Rumpf"));
    body.append(codePort("app:rumpf:"+a._id,{k:"applier",app:a._id},a.codeSrc,"Apply-Logik"));
  }
  // State-Knoten: eigene State-Felder (Typ per Dropdown); per Kante rechts einem Aggregat zuweisen.
  function stateCard(body,s){
    const agg=s.aggregat?MODEL.aggregate.find(a=>a.name===s.aggregat):null;
    const felder=agg?(agg.state=agg.state||[]):(s.felder=s.felder||[]);
    body.append(slotRow("state","Aggregat ▶"+(s.aggregat?" ("+s.aggregat+")":" — frei"),"r",{type:"state",dir:"out",state:s._id},"state:out:"+s._id));
    body.append(h("div",{class:"gsec"},"State-Felder (Typ per Dropdown)"));
    felder.forEach((f,fi)=>body.append(stateFeldRow(f,()=>{felder.splice(fi,1);render();},agg?agg.name:s._id)));
    body.append(h("button",{class:"add",onclick:()=>{felder.push({_id:"f"+(NID++),name:uniqFeldName(felder,"feld"),typ:"decimal"});render();}},"+ Feld"));
    if(!agg)body.append(h("div",{class:"gsec",style:"color:#8b93a7"},"nicht zugewiesen — Punkt rechts auf ein Aggregat ziehen"));
  }

  // ══ LESESEITE: Read Model / Store (Interface = Querschnitt der Verdrahtung) / Projektion / Reader.
  //    ReadModel→Store · Projektion→Store · Event→Projektion(Handle) · Reader→Store · Query→Reader · Reader→Response.
  //    Effekt-/Query-Ops = STRUKTURIERTES Vokabular (Dropdown), kein Freicode — LLM nur an echten Logik-Stellen.
  function readModelCard(body,rm){
    body.append(slotRow("readmodel","Store ▶"+(rm.store?" ("+rm.store+")":" — frei"),"r",{type:"readmodel",dir:"out",rm:rm._id},"rm:out:"+rm._id));
    body.append(nameInp(rm,"name","ReadModel"));
    body.append(h("input",{value:rm.namespace??"",oninput:e=>rm.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec"},"Dokument-Felder (Typ per Dropdown)"));
    (rm.felder||[]).forEach((f,fi)=>body.append(stateFeldRow(f,()=>{rm.felder.splice(fi,1);render();},rm.name)));
    body.append(h("button",{class:"add",onclick:()=>{(rm.felder=rm.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(rm.felder,"feld"),typ:"string"});render();}},"+ Feld"));
  }

  // Verträge werden verdrahtet; CODE fließt als Wert von 📝 Code- / 🤖 LLM-Knoten in die Rumpf-Ports.
  const nodeName=id=>{const c=MODEL.codeNodes.find(x=>x._id===id);if(c)return "📝 "+(c.name||"Code");const l=MODEL.llmNodes.find(x=>x._id===id);if(l)return "🤖 "+(l.name||"LLM");return null;};
  // Getippter 📝-Text eines Code-Knotens (für kurze Ausdrücke wie den Count-Ausdruck; 🤖 = später generiert → leer).
  const codeText=id=>{const c=MODEL.codeNodes.find(x=>x._id===id);return c?(c.text||""):"";};
  // Ein Code-Eingang (Rumpf): getippter Slot `code`; zeigt die verdrahtete Quelle (📝/🤖) oder „— leer".
  // Setzt die Code-Quelle eines Rumpf-Ports (dieselbe Ziel-Logik wie beim Verdrahten einer code-Kante).
  function setCodeSrc(t,src){if(!t)return;
    if(t.k==="pjHandle"){const p=MODEL.projektionen.find(x=>x._id===t.proj);if(p&&p.handles[t.hi])p.handles[t.hi].codeSrc=src;}
    else if(t.k==="rdHandle"){const r=MODEL.reader.find(x=>x._id===t.reader);if(r&&r.handles[t.hi])r.handles[t.hi].codeSrc=src;}
    else if(t.k==="reakHandle"){const r=MODEL.reaktionen.find(x=>x._id===t.reaktion);if(r&&r.handles[t.hi])r.handles[t.hi].codeSrc=src;}
    else if(t.k==="plHandle"){const p=MODEL.pipelines.find(x=>x._id===t.pipeline);if(p&&p.handles[t.hi])p.handles[t.hi].codeSrc=src;}
    else if(t.k==="writeFn"){const st=MODEL.stores.find(x=>x._id===t.store);const fn=st&&(st.writeFns||[]).find(f=>f._id===t.fn);if(fn)fn.codeSrc=src;}
    else if(t.k==="readFn"){const st=MODEL.stores.find(x=>x._id===t.store);const fn=st&&(st.readFns||[]).find(f=>f._id===t.fn);if(fn)fn.codeSrc=src;}
    else if(t.k==="decider"){const d=dec(t.dec);if(d){d.codeSrc=src;if(src)delete d.leer;}}
    else if(t.k==="applier"){const a=app(t.app);if(a){a.codeSrc=src;if(src)delete a.leer;}}
    else if(t.k==="sagaCount"){const x=MODEL.transitions.find(z=>z._id===t.trans);if(x)x.sammelCodeSrc=src;}
    else if(t.k==="dienst"){const d=MODEL.dienste.find(x=>x._id===t.dienst);if(d)d.codeSrc=src;}}
  // Rumpf-Port eines Deciders/Appliers, dessen Methode im Code bewusst LEER ist (No-op, kein Platzhalter)?
  function codeOwnerLeer(t){if(t.k==="decider"){const d=dec(t.dec);return !!(d&&d.leer);}
    if(t.k==="applier"){const a=app(t.app);return !!(a&&a.leer);}return false;}
  // Ein-Klick: Code-/LLM-Knoten erzeugen UND sofort an diesen Rumpf-Port andocken (macht die „Code-Inseln" auffindbar).
  function addCode(target,kind){const pos=spawnPos();let id;
    if(kind==="llm"){id="ln"+(NID++);MODEL.llmNodes.push({_id:id,name:uniq("LLM"),intent:"",...pos});}
    else{id="cn"+(NID++);MODEL.codeNodes.push({_id:id,name:uniq("Code"),text:"",...pos});}
    setCodeSrc(target,id);render();}
  // Code-Eingang (Rumpf): getippter Slot `code`. Leer → deutlicher „⚙ …fehlt"-Marker + Ein-Klick-Knöpfe.
  function codePort(key,target,codeSrc,label){const s=port("code");s.classList.add("i");reg(key,s,{type:"code",dir:"in",target});
    const src=codeSrc?nodeName(codeSrc):null;const lbl=label||"Logik";
    // Passiv: der gefüllte Rumpf zeigt nur, welcher Code-Block hängt (kein Editier-Modal am Konsumenten).
    if(src)return h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"color:#9be3bf"},"◀ "+lbl+": "+src));
    if(target&&codeOwnerLeer(target))return h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"opacity:.6",title:"Im Code bewusst leer (No-op) — kein Platzhalter"},"∅ "+lbl+": bewusst leer"));
    return h("div",{class:"slotrow codeempty"},s,
      h("span",{class:"slotlbl codemiss",style:"flex:1"},"⚙ "+lbl+" fehlt"),
      h("button",{class:"codeadd",title:"Code-Block erzeugen und hier andocken",onclick:()=>addCode(target,"code")},"＋📝"));}
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
  // Abgeleiteter Store-Scope eines Konsumenten = die Stores, deren Funktionen seine Handles aufrufen.
  function derivedStores(node){const s=new Set();(node.handles||[]).forEach(hd=>(hd.fns||[]).forEach(id=>{const r=fnById(id);if(r)s.add(r.store.name);}));return[...s];}

  // Store-Knoten = INTERFACE-EDITOR: Write-/Read-Funktionen (Signatur = Name + Parameter [+ Rückgabe]),
  //   je Funktion ein Impl-Code-Port (der Rumpf kommt von 📝/🤖). Read Models docken oben an.
  function storeCard(body,st){
    body.append(topSlot("readmodel","Read Models ▲",{type:"readmodel",dir:"in",store:st.name},"sto:rm:"+st.name));
    body.append(nameInp(st,"name","Store"));
    body.append(h("input",{value:st.namespace??"",oninput:e=>st.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    const rms=MODEL.readModels.filter(x=>x.store===st.name);
    body.append(h("div",{class:"gsec"},"Dokumente: "+(rms.length?rms.map(x=>x.name).join(" · "):"— (ReadModel oben anschließen)")));
    body.append(h("div",{class:"gsep"},"Write-API — je Fn ◀ von Projektion-Handle aufgerufen"));
    (st.writeFns||[]).forEach(fn=>body.append(fnBlock(st,fn,false)));
    body.append(h("button",{class:"add",onclick:()=>{st.writeFns.push({_id:"wf"+(NID++),name:"NeuFunktion",params:[]});render();}},"+ Write-Funktion"));
    body.append(h("div",{class:"gsep"},"Read-API — je Fn ◀ von Reader-Handle aufgerufen"));
    (st.readFns||[]).forEach(fn=>body.append(fnBlock(st,fn,true)));
    body.append(h("button",{class:"add",onclick:()=>{st.readFns.push({_id:"rf"+(NID++),name:"HoleX",params:[],rueckgabe:""});render();}},"+ Read-Funktion"));
  }
  function fnBlock(st,fn,isRead){const arr=isRead?st.readFns:st.writeFns;
    const box=h("div",{style:"border-left:2px solid #2c3547;padding-left:7px;margin:6px 0"});
    // Aufruf-Ziel-Port: ein Projektion- (write) bzw. Reader-Handle (read) dockt hier an → er ruft diese Fn.
    const ct=isRead?"rcall":"wcall";const cp=port("store");cp.classList.add("i");
    reg(ct+":in:"+st._id+":"+fn._id,cp,{type:ct,dir:"in",store:st._id,fn:fn._id});
    box.append(h("div",{class:"slotrow"},cp,inp(fn.name,v=>fn.name=v,"funktionsName"),
      h("button",{class:"rm",onclick:()=>{arr.splice(arr.indexOf(fn),1);render();}},"✕")));
    box.append(paramRows(fn));
    if(isRead)box.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"→ Rückgabe"),tinp(fn.rueckgabe,v=>fn.rueckgabe=v)));
    box.append(codePort("impl:in:"+st._id+":"+fn._id,{k:isRead?"readFn":"writeFn",store:st._id,fn:fn._id},fn.codeSrc,"Impl-Logik"));
    return box;}

  // Projektion = Controller: Trigger-Events (je Handle) + Store-SCOPE (mehrere möglich). Der Handle-Rumpf
  //   (welche Store-Funktionen, Reihenfolge, Bedingung, Args) kommt als CODE über den Controller-Port.
  function projektionCard(body,p){
    body.append(nameInp(p,"name","Projektion","projektion"));
    body.append(h("input",{value:p.namespace??"",oninput:e=>p.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    // Transport-Achse: geordneter Pull (IPullSubscriber) vs. Signal (ISubscriber, best-effort). Default = Pull.
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{p.pull=e.target.checked;render();},...((p.pull!==false)?{checked:"checked"}:{})}),"Geordneter Pull (IPullSubscriber)"));
    // Garantie-Achse: append-artig ⇒ Co-Commit-Store ⇒ exactly-once (GA-1); sonst idempotenter Upsert ⇒ at-least-once genügt.
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{p.append=e.target.checked||undefined;render();},...(p.append?{checked:"checked"}:{})}),"Append-artig ⇒ Exactly-once (IAppendProjektion)"));
    body.append(h("div",{class:"gsec",style:"opacity:.6"},p.append?"Append ⇒ verlangt Co-Commit-Store (GA-1): Effekt + Marke in EINER Transaktion.":(p.pull!==false?"Idempotenter Upsert · geordneter Pull.":"Idempotenter Upsert · Signal (best-effort, verlierbar).")));
    // Als Projektion-Ziel (IReader<TProjection>) andockbar + abgeleiteter Store-Scope (aus den Handle-Aufrufen).
    const asp=port("query");asp.classList.add("i");reg("prj:asproj:"+p._id,asp,{type:"projref",dir:"in",proj:p._id});
    body.append(h("div",{class:"slotrow"},asp,h("span",{class:"slotlbl"},"◀ Reader bindet hier an")));
    const ds=derivedStores(p);
    body.append(h("div",{class:"gsec"},"Stores (abgeleitet): "+(ds.length?ds.join(" · "):"— (Handle → Write-Fn verdrahten)")));
    body.append(h("div",{class:"gsec"},"Trigger-Event → Handle (Controller) → Write-Fn(s)"));
    (p.handles||[]).forEach((hd,hi)=>{const sl=port("event");sl.classList.add("i");reg("prj:in:"+p._id+":"+hi,sl,{type:"evtUse",dir:"in",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},"◀ Auf "+(hd.event||"?")),h("button",{class:"rm",onclick:()=>{p.handles.splice(hi,1);render();}},"✕")));
      // Aufgerufene Write-Funktionen (je Aufruf ein Punkt → Store-Fn ziehen); Norm = genau eine.
      (hd.fns||[]).forEach((fid,fj)=>{const r=fnById(fid);const s=port("store");s.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":"+fid,s,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"ruft "+(r?r.store.name+"."+r.fn.name:"?")+" ▶"),s));});
      const wo=port("store");wo.classList.add("o");reg("wcall:out:"+p._id+":"+hi+":open",wo,{type:"wcall",dir:"out",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Write-Fn ▶"),wo));
      // Reaktives Event veröffentlichen (nach dem Schreiben): yield IEvent → Broker-Re-Publish (verlierbar, kein Log).
      (hd.publishes||[]).forEach((ev,ei)=>{const s=port("event");s.classList.add("o");reg("prj:pub:"+p._id+":"+hi+":"+ev,s,{type:"evtOut",dir:"out",proj:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.publishes.splice(ei,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"veröffentlicht "+(ev||"?")+" ▶"),s));});
      const po=port("event");po.classList.add("o");reg("prj:pub:"+p._id+":"+hi+":open",po,{type:"evtOut",dir:"out",proj:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.7"},"+ veröffentlicht Event ▶ (reaktiv)"),po));
      body.append(codePort("ctrl:in:"+p._id+":"+hi,{k:"pjHandle",proj:p._id,hi:hi},hd.codeSrc,"Controller-Logik"));});
    const oi=port("open");oi.classList.add("i");reg("prj:in:"+p._id+":open",oi,{type:"evtUse",dir:"in",proj:p._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Event andocken")));
  }

  // Reaktion = EMITTIERENDER Konsument (ISubscriber → IAsyncEnumerable<OneOf<Cmd>>): kein Store, keine
  //   Reset — Trigger-Event(s) ◀ → Handle → OneOf-Command-Ausgänge ▶ (das WAS ausgelöst wird ist verdrahtet,
  //   das WIE/mit-welchen-Werten macht die 📝 Controller-Logik). Der idiomatische Fan-in-Baustein.
  function reaktionCard(body,r){
    body.append(nameInp(r,"name","Reaktion","reaktion"));
    body.append(h("input",{value:r.namespace??"",oninput:e=>r.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.pull=e.target.checked,...((r.pull!==false)?{checked:"checked"}:{})}),"Geordneter Pull (IPullSubscriber)"));
    body.append(h("div",{class:"gsec"},"Trigger-Event → Handle → OneOf-Command(s) (emittiert). Das WANN/mit-WELCHEN-Werten macht der Rumpf."));
    (r.handles||[]).forEach((hd,hi)=>{
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
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"reakHandle",reaktion:r._id,hi:hi},hd.codeSrc,"Controller-Logik"));});
    const oi=port("open");oi.classList.add("i");reg("rk:in:"+r._id+":open",oi,{type:"evtUse",dir:"in",reaktion:r._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Event andocken")));
  }

  // Trigger-Msg eines Handles anzeigen (aktueller msgName des verdrahteten Triggers, rename-fest über trigId).
  const trigMsgLabel=id=>{const t=MODEL.triggers.find(x=>x._id===id);return t?(t.msgName||t.name||"Trigger"):null;};
  // ══ INGRESS/PIPELINE: Trigger (Timer/Webhook/FileWatch/Frist) → Trigger-Msg → Pipeline → OneOf-Command(s).
  // Trigger = Ingress-WECKER. Modus + Config; erzeugt EINE IPipelineTrigger-Nachricht (Name + Felder) → Pipeline.
  function triggerCard(body,t){
    body.append(nameInp(t,"name","Trigger","trigger"));
    const modi=[["timer","⏱ Timer (Intervall)"],["webhook","🔗 Webhook (HTTP)"],["filewatch","📁 FileWatch (Datei)"],["frist","⏳ Frist (Deadline)"]];
    const sel=h("select",{onchange:e=>{t.modus=e.target.value||undefined;render();}});
    // Ohne Bindung im Code (Composition Root) ist der Modus UNBESTIMMT — nicht geraten.
    const leer=h("option",{value:""},"— Modus (im Code nicht gebunden)");if(!t.modus)leer.selected=true;sel.append(leer);
    modi.forEach(([v,l])=>{const o=h("option",{value:v},l);if(t.modus===v)o.selected=true;sel.append(o);});
    body.append(sel);
    if(t.modus==="webhook"){body.append(h("div",{class:"gsec"},"Route · Request-Typ"));
      body.append(inp(t.route,v=>t.route=v,"/webhooks/x"));body.append(tinp(t.reqTyp,v=>t.reqTyp=v));}
    else if(t.modus==="filewatch"){body.append(h("div",{class:"gsec"},"Pfad · Muster"));
      body.append(inp(t.pfad,v=>t.pfad=v,"/data/incoming"));body.append(inp(t.muster,v=>t.muster=v,"*.png"));}
    else if(t.modus==="frist"){body.append(h("div",{class:"gsec"},"Dauer / Fälligkeit (IDbClock)"));
      body.append(inp(t.dauer,v=>t.dauer=v,"z. B. 24:00:00 oder aus Feld"));}
    else {body.append(h("div",{class:"gsec"},"Intervall"));body.append(inp(t.intervall,v=>t.intervall=v,"30s"));}
    body.append(h("div",{class:"gsec"},"Trigger-Nachricht (IPipelineTrigger)"));
    body.append(inp(t.msgName,v=>{t.msgName=v;},"z. B. DateiErkannt"));
    (t.felder||[]).forEach((f,fi)=>body.append(feldRow(f,()=>{t.felder.splice(fi,1);render();})));
    body.append(h("button",{class:"add",onclick:()=>{(t.felder=t.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(t.felder,"feld"),typ:"Guid"});render();}},"+ Feld"));
    body.append(slotRow("trigmsg","erzeugt "+(t.msgName||"Trigger-Msg")+" ▶","r",{type:"trigmsg",dir:"out",trigId:t._id,msgName:t.msgName},"trg:msg:"+t._id));
  }
  // Pipeline = 4. durabler Konsument (IPipelineHandler): je Handle EIN Eingang (Trigger/Event/Self) →
  //   yield ICommand UND/ODER yield IPipelineTrigger (→ andere Pipeline) UND/ODER ScheduleSelf (Tick/Timeout).
  //   Ein Handle kann auch reiner Seiteneffekt sein (kein yield). Das WIE macht der 📝 Rumpf.
  function pipelineCard(body,p){
    body.append(nameInp(p,"name","Pipeline","pipeline"));
    body.append(h("input",{value:p.namespace??"",oninput:e=>p.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec"},"PipelineId"));
    body.append(inp(p.pipelineId,v=>p.pipelineId=v,"z. B. bildverarbeitung"));
    body.append(h("div",{class:"gsec"},"Handle: Trigger/Event/Self ◀ → yield Command · yield Trigger · ScheduleSelf. Das WIE macht der Rumpf."));
    (p.handles||[]).forEach((hd,hi)=>{
      const kind=hd.inputKind||"event", isTrig=kind==="trigger", isSelf=kind==="self";
      const sl=port(isTrig?"trigmsg":(isSelf?"self":"event"));sl.classList.add("i");
      reg("pl:in:"+p._id+":"+hi,sl,{type:isTrig?"trigmsg":(isSelf?"self":"evtUse"),dir:"in",pipeline:p._id,handleIdx:hi});
      const tlabel=(hd.prod&&hd.prod.k==="tg")?(trigMsgLabel(hd.prod.id)||hd.input):hd.input;
      const lbl=isTrig?("◀ Trigger "+(tlabel||"?")):(isSelf?("◀ Self "+(hd.selfName||"?")):("◀ Auf "+(hd.event||"?")));
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},lbl),h("button",{class:"rm",onclick:()=>{p.handles.splice(hi,1);render();}},"✕")));
      // yield ICommand → Aggregat
      (hd.sends||[]).forEach((c,ci)=>{const so=port("command");so.classList.add("o");reg("pl:send:"+p._id+":"+hi+":"+c,so,{type:"sagaCmd",dir:"out",pipeline:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.sends.splice(ci,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"sendet "+(c||"?")+" ▶"),so));});
      const so=port("command");so.classList.add("o");reg("pl:send:"+p._id+":"+hi+":open",so,{type:"sagaCmd",dir:"out",pipeline:p._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Command ▶"),so));
      // yield IPipelineTrigger → an eine andere Pipeline (Verkettung, z. B. FileWatch → ImageProcessing)
      (hd.emits||[]).forEach((nm,ei)=>{const eo=port("trigmsg");eo.classList.add("o");reg("pl:emit:"+p._id+":"+hi+":"+nm,eo,{type:"trigmsg",dir:"out",pipeline:p._id,handleIdx:hi,msgName:nm});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.emits.splice(ei,1);render();}},"✕"),
          h("input",{value:nm,oninput:e=>hd.emits[ei]=e.target.value,onchange:()=>render(),placeholder:"TriggerMsg",style:"flex:1"}),h("span",{class:"slotlbl"},"▶"),eo));});
      // Persistenter, gleichwertiger Ausgangs-Port: eine Handle yieldet Command ODER Trigger (OneOf<…>).
      const eopen=port("trigmsg");eopen.classList.add("o");reg("pl:emit:"+p._id+":"+hi+":open",eopen,{type:"trigmsg",dir:"out",pipeline:p._id,handleIdx:hi,msgName:""});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ erzeugt Trigger ▶"),eopen));
      // ScheduleSelf → interner Tick/Timeout (Self-Message kommt als eigener ◀ Self-Handle zurück)
      (hd.schedules||[]).forEach((sc,si)=>{const ss=port("self");ss.classList.add("o");reg("pl:sched:"+p._id+":"+hi+":"+sc.name,ss,{type:"self",dir:"out",pipeline:p._id,handleIdx:hi,name:sc.name});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.schedules.splice(si,1);render();}},"✕"),
          h("input",{value:sc.name,oninput:e=>sc.name=e.target.value,onchange:()=>render(),placeholder:"SelfMsg",style:"flex:1"}),
          h("input",{value:sc.delay??"",oninput:e=>sc.delay=e.target.value,placeholder:"delay",style:"width:52px"}),h("span",{class:"slotlbl"},"↺"),ss));});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.8"},"+ plant Self-Tick ↺"),
        h("button",{class:"codeadd",title:"ScheduleSelf + Self-Handle anlegen",onclick:()=>{const nm=uniq("Tick");(hd.schedules=hd.schedules||[]).push({name:nm,delay:"30s"});if(!p.handles.some(x=>x.inputKind==="self"&&x.selfName===nm))p.handles.push({inputKind:"self",selfName:nm,sends:[],emits:[],schedules:[]});render();}},"＋")));
      body.append(codePort("plctrl:in:"+p._id+":"+hi,{k:"plHandle",pipeline:p._id,hi:hi},hd.codeSrc,"Pipeline-Logik"));
    });
    // Genutzte Dienste (Konstruktor-Injektion) — an „Vertrag ▶" eines Dienst-Knotens andocken.
    (p.dienste||[]).forEach((dn,i)=>{const s=port("store");s.classList.add("i");reg("pl:dienst:"+p._id+":"+dn,s,{type:"dienst",dir:"in",pipeline:p._id});
      body.append(h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"flex:1"},"◀ nutzt "+(dn||"?")),h("button",{class:"rm",onclick:()=>{p.dienste.splice(i,1);render();}},"✕")));});
    const dio=port("store");dio.classList.add("i");reg("pl:dienst:"+p._id+":open",dio,{type:"dienst",dir:"in",pipeline:p._id});
    body.append(h("div",{class:"slotrow"},dio,h("span",{class:"slotlbl"},"+ nutzt Dienst ◀")));
    const ot=port("trigmsg");ot.classList.add("i");reg("pl:in:"+p._id+":opentrg",ot,{type:"trigmsg",dir:"in",pipeline:p._id,handleIdx:"opentrg"});
    body.append(h("div",{class:"slotrow"},ot,h("span",{class:"slotlbl"},"+ Trigger andocken")));
    const oe=port("event");oe.classList.add("i");reg("pl:in:"+p._id+":openevt",oe,{type:"evtUse",dir:"in",pipeline:p._id,handleIdx:"openevt"});
    body.append(h("div",{class:"slotrow"},oe,h("span",{class:"slotlbl"},"+ Event andocken")));
  }

  // Reader = Controller: Queries (je Handle) + Store-SCOPE + Response; Handle-Rumpf kommt als Code.
  function readerCard(body,r){
    body.append(nameInp(r,"name","Reader"));
    body.append(h("input",{value:r.namespace??"",oninput:e=>r.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.trackDeps=e.target.checked,...((r.trackDeps!==false)?{checked:"checked"}:{})}),"TrackDeps (Redis-Deps)"));
    // IReader<TProjection>: der Reader liest GENAU EINE Projektion — expliziter Bindungs-Port.
    const pb=port("query");pb.classList.add("o");reg("rdr:proj:"+r._id,pb,{type:"projref",dir:"out",reader:r._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"liest Projektion: "+(r.projektion||"— andocken")+" ▶"),pb));
    const ds=derivedStores(r);
    body.append(h("div",{class:"gsec"},"Stores (abgeleitet): "+(ds.length?ds.join(" · "):"— (Handle → Read-Fn verdrahten)")));
    body.append(h("div",{class:"gsec"},"Query → Handle → Read-Fn(s) (auch mehrere Stores) → OneOf-Responses. Das WANN macht der Rumpf."));
    (r.handles||[]).forEach((hd,hi)=>{
      const qin=port("query");qin.classList.add("i");reg("rdr:qin:"+r._id+":"+hi,qin,{type:"query",dir:"in",reader:r._id,handleIdx:hi});
      body.append(h("div",{class:"slotrow"},qin,h("span",{class:"slotlbl",style:"flex:1"},"◀ Query: "+(hd.query||"?")),h("button",{class:"rm",onclick:()=>{r.handles.splice(hi,1);render();}},"✕")));
      // Aufgerufene Read-Funktionen (je Aufruf ein Punkt → Store-Read-Fn ziehen; mehrere Stores erlaubt).
      (hd.fns||[]).forEach((fid,fj)=>{const rr=fnById(fid);const s=port("store");s.classList.add("o");reg("rcall:out:"+r._id+":"+hi+":"+fid,s,{type:"rcall",dir:"out",reader:r._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"ruft "+(rr?rr.store.name+"."+rr.fn.name:"?")+" ▶"),s));});
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
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"rdHandle",reader:r._id,hi:hi},hd.codeSrc,"Controller-Logik"));
    });
    const oi=port("open");oi.classList.add("i");reg("rdr:qin:"+r._id+":open",oi,{type:"query",dir:"in",reader:r._id,handleIdx:"open"});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Query andocken")));
  }

  // ══ BETRIEB / HOST-BAND: die (bewusst unreine) Composition-Root-Naht — Frist, Dienst-Bindung, HostSetting.
  //    Sie leben real in Program.cs/DI. Der Editor macht die Naht sichtbar statt sie zu verstecken.

  // Frist = Drei-End-Relation (statt Ingress-Attrappe): plant ◀ Event · storniert ◀ Event ·
  //   Dauer ◀ HostSetting · fällig → Command @ Aggregat. Kontext = stabile Identität (AddDeadlines-Router).
  function fristCard(body,f){
    body.append(nameInp(f,"name","Frist","frist"));
    body.append(h("div",{class:"gsec"},"Kontext (stabile Identität)"));
    body.append(inp(f.kontext,v=>f.kontext=v,"z. B. training-timeout"));
    body.append(h("div",{class:"gsec"},"plant auf Event(s) · storniert auf Event(s) · Dauer aus HostSetting"));
    (f.plant||[]).forEach((ev,i)=>{const s=port("event");s.classList.add("i");reg("fr:plant:"+f._id+":"+ev,s,{type:"evtUse",dir:"in",frist:f._id,role:"plant"});
      body.append(h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"flex:1"},"◀ plant auf "+(ev||"?")),h("button",{class:"rm",onclick:()=>{f.plant.splice(i,1);render();}},"✕")));});
    const po=port("event");po.classList.add("i");reg("fr:plant:"+f._id+":open",po,{type:"evtUse",dir:"in",frist:f._id,role:"plant"});
    body.append(h("div",{class:"slotrow"},po,h("span",{class:"slotlbl"},"+ plant auf Event ◀")));
    (f.storniert||[]).forEach((ev,i)=>{const s=port("event");s.classList.add("i");reg("fr:cancel:"+f._id+":"+ev,s,{type:"evtUse",dir:"in",frist:f._id,role:"storniert"});
      body.append(h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"flex:1"},"◀ storniert auf "+(ev||"?")),h("button",{class:"rm",onclick:()=>{f.storniert.splice(i,1);render();}},"✕")));});
    const co=port("event");co.classList.add("i");reg("fr:cancel:"+f._id+":open",co,{type:"evtUse",dir:"in",frist:f._id,role:"storniert"});
    body.append(h("div",{class:"slotrow"},co,h("span",{class:"slotlbl"},"+ storniert auf Event ◀")));
    const ds=port("trigmsg");ds.classList.add("i");reg("fr:dauer:"+f._id,ds,{type:"setting",dir:"in",frist:f._id});
    body.append(h("div",{class:"slotrow"},ds,h("span",{class:"slotlbl"},"◀ Dauer: "+(f.dauerSetting||"— HostSetting andocken"))));
    const so=port("command");so.classList.add("o");reg("fr:send:"+f._id,so,{type:"sagaCmd",dir:"out",frist:f._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"fällig → "+(f.sendet||"Command")+(f.aggregat?" @ "+f.aggregat:"")+" ▶"),so));
  }

  // Dienst-Bindung = Vertrag (Interface) → Impl (📝-Insel ODER externer Adapter). Gibt dem freistehenden
  //   Domain-Service (SplitZuteiler, ImagePairName) UND den Handler-Dependencies (IClassifierService) ein Zuhause.
  function dienstCard(body,d){
    body.append(nameInp(d,"name","Dienst","dienst"));
    body.append(h("div",{class:"gsec"},"Vertrag (Interface)"));
    body.append(inp(d.vertrag,v=>d.vertrag=v,"z. B. IClassifierService"));
    const vo=port("store");vo.classList.add("o");reg("di:vertrag:"+d._id,vo,{type:"dienst",dir:"out",dienst:d._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"Vertrag "+(d.vertrag||"?")+" ▶"),vo));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{d.extern=e.target.checked||undefined;render();},...(d.extern?{checked:"checked"}:{})}),"Externer Dienst (HTTP/ML) — Impl außerhalb"));
    if(!d.extern){body.append(h("div",{class:"gsec"},"Impl-Logik"));
      body.append(codePort("di:impl:"+d._id,{k:"dienst",dienst:d._id},d.codeSrc,"Impl-Logik"));}
    else body.append(h("div",{class:"gsec",style:"opacity:.6"},"Impl = externer Adapter (nicht im Editor)"));
  }

  // HostSetting = operativer Config-Wert (Name/Typ/Default/EnvKey) — die Editor-Repräsentation von
  //   appsettings/env für DOMÄNEN-relevante Werte (Pfad/Intervall/Timeout). Speist Trigger/Frist.
  function hostSettingCard(body,s){
    body.append(nameInp(s,"name","HostSetting","hostsetting"));
    body.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Typ"),tinp(s.typ,v=>s.typ=v)));
    body.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"Default"),inp(s.default,v=>s.default=v,"z. B. 6h · /data/input")));
    body.append(h("div",{class:"frow"},h("span",{class:"slotlbl"},"EnvKey"),inp(s.envKey,v=>s.envKey=v,"Abschnitt:Schlüssel")));
    body.append(h("div",{class:"gsec",title:"aus dem Code verfolgt: GetValue → DI-Extension → Konfig-Feld"},
      s.konfig?"fließt in "+s.konfig+"."+(s.feld||"?")+" ▶":"— in keiner Konfiguration verwendet"));
    const wo=port("trigmsg");wo.classList.add("o");reg("hs:wert:"+s._id,wo,{type:"setting",dir:"out",hostSetting:s._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"Wert ▶"),wo));
  }

  // 📝 Code-Knoten: der C#-Rumpf. Vorschau im Knoten, KLICK → Modal mit vollem, editierbarem Code.
  // ── CODE-SYNC: Board ⇄ echte .cs-Datei (SimHost). Anker aus dem GRAPHEN abgeleitet: welcher
  //    Decider/Applier besitzt diesen 📝/🤖-Knoten → Namespace + Command/Event. Heute nur die
  //    Schreibseite (vom Scaffolder als Datei gedeckt); Leseseite-Rümpfe folgen.
  function codeAnker(nodeId){const o=findCodeOwner(nodeId);if(!o)return null;
    if(o.kind==="decider"){const d=o.ref,agg=MODEL.aggregate.find(a=>a.name===d.aggregat);
      return (agg&&d.command)?{kind:"decider",namespace:agg.namespace,disc:d.command,datei:d.datei||""}:null;}
    if(o.kind==="applier"){const a=o.ref,agg=MODEL.aggregate.find(x=>x.name===a.aggregat);
      return (agg&&a.event)?{kind:"applier",namespace:agg.namespace,disc:a.event,datei:a.datei||""}:null;}
    return null;}
  async function codeGet(a){const q=new URLSearchParams({kind:a.kind,namespace:a.namespace,disc:a.disc,datei:a.datei||""});
    return fetch("/api/editor/code?"+q).then(r=>r.json());}
  async function codeSetPrompt(a,prompt,baseHash){return fetch("/api/editor/code",{method:"POST",
    headers:{"content-type":"application/json"},body:JSON.stringify({...a,prompt,baseHash})}).then(r=>r.json());}
  async function codeOpen(a){return fetch("/api/editor/open",{method:"POST",
    headers:{"content-type":"application/json"},body:JSON.stringify(a)}).then(r=>r.json());}
  // Poll-Registry (Datei→Browser, das Double-Binding): je sichtbarem Knoten ein Updater; bei Hash-Wechsel anwenden.
  let SYNC={}, WATCH=new Set();
  const ankerKey=a=>a?a.kind+":"+a.namespace+":"+a.disc:"";
  function syncReg(id,anker,apply,gid){if(INSP&&SYNC[id])return;SYNC[id]={anker,apply,hash:null,gid};}
  function imBlick(gid){if(!canvas||!world||!gid)return true;const el=world.querySelector('[data-id="'+gid+'"]');
    if(!el)return true;const cr=canvas.getBoundingClientRect(),r=el.getBoundingClientRect();
    return !(r.right<cr.left||r.left>cr.right||r.bottom<cr.top||r.top>cr.bottom);}
  // Kontinuierlich gepollt wird NUR, was via ✎ geöffnet wurde (WATCH). Sichtbare Blöcke werden EINMAL
  //   gespiegelt (Vorschau füllen), dann Ruhe → im Leerlauf keine Requests, kein Terminal-Flut.
  async function syncTick(){const de=document.getElementById("de");if(!de||!de.classList.contains("on"))return;
    for(const id in SYNC){const s=SYNC[id];const beobachtet=WATCH.has(ankerKey(s.anker));
      if(!beobachtet){if(s.hash!==null)continue;if(!imBlick(s.gid))continue;}
      try{const d=await codeGet(s.anker);if(d&&d.ok&&d.hash!==s.hash){s.hash=d.hash;s.apply(d);}}catch(e){}}}
  setInterval(syncTick,2000);

  // 📝 Code-Block: PASSIV (Spiegel der echten Datei). ALLGEMEINES PATTERN — jeder Block hat:
  //   • Eingang „◀ 🤖 Prompt": bei Bedarf eine LLM-Node andocken (dort schreibst du den Prompt).
  //   • Ausgang „code ▶": in den Rumpf-Port des Konsumenten (Decider/Applier/Store-Fn/Handler …).
  //   • „✎ Im Editor öffnen" + Datei-Spiegel — nur, wo eine echte .cs existiert (heute Schreibseite).
  //   KEIN In-Browser-Editieren, KEIN Klick auf den Block.
  function codeNodeCard(body,c){
    body.append(nameInp(c,"name","Code"));
    const anker=codeAnker(c._id);
    const voll=INSP;   // im Inspector: der ganze Rumpf statt der Kurzvorschau
    const prev=h("pre",{class:"gcodeprev",style:"cursor:default",title:anker?"Spiegel der echten .cs (passiv)":"passiv"},codePreview(c.text,undefined,voll));
    body.append(prev);
    // Eingang: 🤖 LLM-Prompt-Node andocken (an JEDEM Code-Block, unabhängig vom Konsumenten-Typ).
    const pin=port("prompt");pin.classList.add("i");reg("code:prin:"+c._id,pin,{type:"prompt",dir:"in",codeBlock:c._id});
    const llm=MODEL.llmNodes.find(x=>x.promptZiel===c._id);
    const prow=h("div",{class:"slotrow"},pin,h("span",{class:"slotlbl",style:"flex:1"},llm?("◀ 🤖 "+(llm.name||"LLM")):"◀ 🤖 Prompt (bei Bedarf)"));
    if(!llm)prow.append(h("button",{class:"codeadd",title:"LLM-Prompt-Node erzeugen und andocken",
      onclick:()=>{const vorher=new Set(graphNodes().map(n=>n.id));
        MODEL.llmNodes.push({_id:"ln"+(NID++),name:uniq("LLM"),intent:"",promptZiel:c._id,...spawnPos()});render();fokussiereNeu(vorher);}},"＋🤖"));
    body.append(prow);
    // ✎ IMMER (allgemeines Pattern) — ohne echte Datei: klare Meldung statt fehlendem Knopf.
    body.append(h("div",{class:"frow"},h("button",{class:"codeadd",
      title:anker?"Echte .cs im Editor öffnen":"Datei folgt — Scaffolder deckt diesen Rumpf noch nicht",
      onclick:async()=>{if(!anker){deFlash("◦ Datei folgt — Scaffolder deckt diesen Rumpf noch nicht",false);return;}
        WATCH.add(ankerKey(anker));   // ab jetzt diese eine Datei kontinuierlich spiegeln (Datei→Browser)
        const r=await codeOpen(anker).catch(()=>null);
        deFlash(r&&r.ok?"✎ geöffnet: "+r.pfad:"⚠ "+((r&&r.grund)||"SimHost offline"),!!(r&&r.ok));}},"✎ Im Editor öffnen")));
    if(anker)syncReg(c._id,anker,d=>{c.text=d.body;prev.textContent=codePreview(d.body,undefined,voll);},"cn:"+c._id);   // Datei→Browser: Vorschau spiegelt den echten Rumpf
    body.append(slotRow("code","code ▶","r",{type:"code",dir:"out",codeNode:c._id},"code:out:"+c._id));
  }
  // 🤖 LLM-Knoten: Intent-Text; Vertrag wird aus dem verdrahteten Ziel abgeleitet; Ausgang code ▶.
  // 🤖 LLM-Node = PROMPT-QUELLE. `Prompt ▶` an den Eingang eines Code-Blocks; hier tippst du den Prompt.
  //   Wo der Block eine echte Datei hat, wird der Prompt als `// 🤖 Prompt:`-Kommentar in den Rumpf geschrieben
  //   (die einzige Code-Mutation, bei Bedarf). Double-Binding: extern geänderter Prompt spiegelt zurück.
  function llmNodeCard(body,l){
    body.append(nameInp(l,"name","LLM"));
    const block=l.promptZiel?MODEL.codeNodes.find(c=>c._id===l.promptZiel):null;
    const anker=block?codeAnker(block._id):null;
    body.append(h("div",{class:"gsec"},"Prompt"+(block?" → 📝 "+(block.name||"Code"):" (an einen Code-Block andocken)")));
    const ta=h("textarea",{class:"code",rows:3,placeholder:"z. B. Bestätige nur, wenn Betrag > 0."});
    ta.value=l.intent||"";body.append(ta);
    const status=h("div",{class:"gsec",style:"opacity:.6"},anker?"↔ echte Datei":(block?"angedockt · Datei folgt (Schreibseite)":"nicht angedockt"));
    body.append(status);
    let baseHash=null;
    ta.onchange=async()=>{l.intent=ta.value;if(!anker)return;
      const r=await codeSetPrompt(anker,ta.value,baseHash).catch(()=>null);
      if(r&&r.ok){baseHash=r.hash;status.textContent="✓ in Datei geschrieben";status.style.color="#9be3bf";}
      else if(r&&r.grund==="stale"){baseHash=r.hash;ta.value=r.prompt||"";l.intent=ta.value;status.textContent="↩ extern geändert — neu geladen";status.style.color="#e0b46a";}
      else{status.textContent="⚠ "+((r&&r.grund)||"SimHost offline");status.style.color="#ffb3c1";}};
    if(anker)syncReg(l._id,anker,d=>{baseHash=d.hash;if(document.activeElement!==ta){ta.value=d.prompt||"";l.intent=ta.value;}},"ln:"+l._id);
    body.append(slotRow("prompt","Prompt ▶","r",{type:"prompt",dir:"out",llm:l._id},"llm:prout:"+l._id));
  }
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
  const NODELABEL={command:"Command",event:"Event",rejection:"Ablehnung",valueobject:"Value Object",enum:"Enum",aggregate:"Aggregat",decider:"Decider",applier:"Applier",saga:"Prozess",transition:"Regel",query:"Query",queryresponse:"Response",readmodel:"Read Model",store:"Store",projektion:"Projektion",reader:"Reader",reaktion:"Reaktion",pipeline:"Pipeline",trigger:"Trigger",frist:"Frist",dienst:"Dienst",hostsetting:"HostSetting",codenode:"Code",llmnode:"LLM"};
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
  // ZUGEHÖRIGKEIT = BEZIEHUNG, nie Namespace oder Name: Decider/Applier tragen ihr Aggregat (aus dem Code bzw. per
  //   ▲-Verdrahtung ans Aggregat). Ein Record gehört dem Aggregat, dessen Decider/Applier ihn entscheidet/erzeugt/faltet;
  //   ein Wert-Typ (VO/Enum) dem Aggregat, dessen Records/State ihn referenzieren — jeweils EINDEUTIG oder keinem.
  const recByName=n=>MODEL.records.find(r=>r.name===n);
  const eindeutig=xs=>{const s=[...new Set(xs)];return s.length===1&&s[0]?s[0]:"";};
  function recordAgg(n){if(!n)return "";
    const a=MODEL.decider.filter(d=>d.command===n||(d.ergibt||[]).some(o=>o.event===n)).map(d=>d.aggregat)
      .concat(MODEL.applier.filter(x=>x.event===n).map(x=>x.aggregat));
    return a.length?eindeutig(a):"";}
  function typAgg(t){if(!t)return "";const a=[];
    MODEL.records.forEach(r=>{if((r.felder||[]).some(f=>innerTyp(f.typ)===t))a.push(recordAgg(r.name));});
    MODEL.aggregate.forEach(g=>{if((g.state||[]).some(f=>innerTyp(f.typ)===t))a.push(g.name);});
    return a.length?eindeutig(a):"";}
  // Records spiegeln ihre (abgeleitete) Zugehörigkeit — Decider/Applier werden NICHT überschrieben.
  function deriveMembership(){MODEL.records.forEach(r=>{const a=recordAgg(r.name);if(a)r.aggregat=a;else delete r.aggregat;});}
  const baseTyp=x=>(x||"").replace(/\s/g,"").replace(/\?$/,"");
  // Positionsweise vollständige Argumentliste (fehlende Parameter → "default", damit es kompiliert).
  function argListe(cmdName,args){const cmd=recByName(cmdName);
    if(!cmd)return (args||[]).filter(Boolean);
    return (cmd.felder||[]).map((_,i)=>((args||[])[i])||"default");}
  // Transitionen eines Prozesses (explizit angesteckt via t.prozess).
  const transOf=s=>MODEL.transitions.filter(t=>t.prozess===s.name);
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
  // Berührte Aggregate (abgeleitet aus den Namespaces der referenzierten Events/Commands der Transitionen).
  function sagaAggs(s){const ns=new Set();transOf(s).forEach(t=>{[...(t.wenn||[]),t.sammelEvent].forEach(e=>ns.add(recordAgg(e)));
    (t.dann||[]).forEach(d=>[d.sende,d.kompensation].forEach(c=>ns.add(recordAgg(c))));});
    return [...new Set([...ns].filter(Boolean))];}
  // Vor Serveraufruf: Transitionen → Saga.Schritte (inkl. UndAlle/SendeJe) + ExtraUsings; Argumente positionsvoll.
  function prepareSaga(){MODEL.sagas.forEach(s=>{const ts=transOf(s);
    // Ein Join, mehrere Dann → je Dann EINE Regel (gleiche Bedingung). flatMap fächert die Dann auf.
    s.schritte=ts.filter(t=>(t.wenn||[]).length).flatMap(t=>(t.dann||[]).filter(d=>d.sende).map(d=>{
      const st={wenn:(t.wenn||[]).slice(),sende:d.sende};
      // Aus dem Code gelesene Ausdrücke (verbatim) reisen unverändert zurück — Vorrang vor Stub-Argumenten.
      if(t.sammelEvent){st.sammelEvent=t.sammelEvent;if(t.sammelAusdruck)st.sammelAusdruck=t.sammelAusdruck;if(t.sammelAnzahl)st.sammelAnzahl=t.sammelAnzahl;}
      if(d.sendeAusdruck)st.sendeAusdruck=d.sendeAusdruck;
      if(d.kompensationAusdruck)st.kompensationAusdruck=d.kompensationAusdruck;
      if(d.kompensationJe)st.kompensationJe=true;
      // Count-Anzahl: Feld-Auswahl (D/S); ein 📝-Ausdruck (H-Fallback) hat Vorrang.
      if(d.sendeJe){st.sendeJe=true;if(d.sendeJeCollection)st.sendeJeCollection=d.sendeJeCollection;}
      const sa=argListe(d.sende,d.sendeArgs);if(sa.length)st.sendeArgumente=sa;
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
    if(kind==="aggregate"){
      MODEL.decider.forEach(d=>{if(d.aggregat===old)d.aggregat=nv;});
      MODEL.applier.forEach(a=>{if(a.aggregat===old)a.aggregat=nv;});
      MODEL.states.forEach(s=>{if(s.aggregat===old)s.aggregat=nv;});
    }else if(kind==="record"){
      if(obj.kind==="valueobject")retypeFelder(old,nv);   // Typ-Komposition: Feldtypen mitziehen
      if(obj.kind==="command"){
        MODEL.decider.forEach(d=>{if(d.command===old)d.command=nv;});
        const rx=new RegExp("\\b"+old.replace(/[.*+?^${}()|[\]\\]/g,"\\$&")+"\\b","g");
        MODEL.hostSettings.forEach(hs=>{if(hs.konfig===old)hs.konfig=nv;});
        MODEL.pipelines.forEach(p=>{p.konfigs=(p.konfigs||[]).map(k=>k===old?nv:k);});
        MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{if(d.sende===old)d.sende=nv;if(d.kompensation===old)d.kompensation=nv;
          if(d.sendeAusdruck)d.sendeAusdruck=d.sendeAusdruck.replace(rx,nv);if(d.kompensationAusdruck)d.kompensationAusdruck=d.kompensationAusdruck.replace(rx,nv);}));
        // Reaktion-Handles: ausgelöste Commands mitziehen.
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{hd.sends=(hd.sends||[]).map(x=>x===old?nv:x);}));
        MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{hd.sends=(hd.sends||[]).map(x=>x===old?nv:x);}));
        MODEL.frists.forEach(f=>{if(f.sendet===old)f.sendet=nv;});
      }else{
        MODEL.decider.forEach(d=>(d.ergibt||[]).forEach(o=>{if(o.event===old)o.event=nv;}));
        MODEL.applier.forEach(a=>{if(a.event===old)a.event=nv;});
        MODEL.sagas.forEach(s=>{if(s.triggerEvent===old)s.triggerEvent=nv;});
        MODEL.transitions.forEach(t=>{t.wenn=(t.wenn||[]).map(w=>w===old?nv:w);if(t.sammelEvent===old)t.sammelEvent=nv;if(t.sammelAnzahlFeld&&t.sammelAnzahlFeld.rec===old)t.sammelAnzahlFeld.rec=nv;});
        // Reader-Handles: Query (Eingang) und OneOf-Responses (Ausgänge) mitziehen.
        MODEL.reader.forEach(r=>(r.handles||[]).forEach(hd=>{if(hd.query===old)hd.query=nv;hd.responses=(hd.responses||[]).map(x=>x===old?nv:x);}));
        // Reaktion-Handles: Trigger-Event mitziehen.
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;}));
        MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;}));
        // Projektion-Handles: Trigger-Event (Abo) UND veröffentlichte reaktive Events mitziehen.
        MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;hd.publishes=(hd.publishes||[]).map(x=>x===old?nv:x);}));
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{hd.publishes=(hd.publishes||[]).map(x=>x===old?nv:x);}));
        MODEL.frists.forEach(f=>{f.plant=(f.plant||[]).map(x=>x===old?nv:x);f.storniert=(f.storniert||[]).map(x=>x===old?nv:x);});
      }
    }else if(kind==="hostsetting"){MODEL.frists.forEach(f=>{if(f.dauerSetting===old)f.dauerSetting=nv;});
    }else if(kind==="projektion"){
      // IReader<TProjection>-Bindung (per Name) mitziehen.
      MODEL.reader.forEach(r=>{if(r.projektion===old)r.projektion=nv;});
    }else if(kind==="enum"){retypeFelder(old,nv);}   // Typ-Komposition: Feldtypen mitziehen
  }
  function nameInp(o,p,ph,kind){const old=o[p];
    return h("input",{value:old??"",placeholder:ph||"Name",
      onchange:e=>{const nv=e.target.value;if(!nv){e.target.value=old??"";return;}
        if(nv!==old&&kind)renameRefs(kind,o,old,nv);o[p]=nv;render();}});}
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

  function port(color){const s=h("div",{class:"slot s-"+color});s.onpointerdown=e=>{e.stopPropagation();e.preventDefault();startLink(e,s);};return s;}
  function reg(key,el,info){el.__slot=info;if(!INSP)SLOTS[key]=el;return el;}   // Inspector-Kopien verdrahten nicht
  function slotRow(color,label,side,info,key){const s=port(color);s.classList.add(side==="l"?"i":"o");reg(key,s,info);
    return side==="l"?h("div",{class:"slotrow"},s,h("span",{class:"slotlbl"},label))
                     :h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},label),s);}
  function topSlot(color,label,info,key){const s=port(color);s.classList.add("t");reg(key,s,info);
    return h("div",{class:"gtopfield"},s,h("span",{class:"slotlbl"},label));}

  function graphNodes(){
    return [...MODEL.records.map(r=>({id:"rec:"+r.name,name:r.name,kind:r.kind,ref:r})),
            ...MODEL.aggregate.map(a=>({id:"agg:"+a.name,name:a.name,kind:"aggregate",ref:a})),
            ...MODEL.states.map(s=>({id:"st:"+s._id,name:s.aggregat||"frei",kind:"state",ref:s})),
            ...MODEL.decider.map(d=>({id:"dec:"+d._id,name:d.command||"",kind:"decider",ref:d})),
            ...MODEL.applier.map(a=>({id:"app:"+a._id,name:a.event||"",kind:"applier",ref:a})),
            ...MODEL.enums.map(e=>({id:"enum:"+e.name,name:e.name,kind:"enum",ref:e})),
            ...MODEL.sagas.map(s=>({id:"saga:"+s.name,name:s.name,kind:"saga",ref:s})),
            ...MODEL.transitions.map(t=>({id:"tr:"+t._id,name:((t.dann||[]).map(d=>d.sende).filter(Boolean).join(", ")),kind:"transition",ref:t})),
            ...MODEL.readModels.map(rm=>({id:"rm:"+rm._id,name:rm.name,kind:"readmodel",ref:rm})),
            ...MODEL.stores.map(s=>({id:"sto:"+s._id,name:s.name,kind:"store",ref:s})),
            ...MODEL.projektionen.map(p=>({id:"prj:"+p._id,name:p.name,kind:"projektion",ref:p})),
            ...MODEL.reader.map(r=>({id:"rdr:"+r._id,name:r.name,kind:"reader",ref:r})),
            ...MODEL.reaktionen.map(r=>({id:"rk:"+r._id,name:r.name,kind:"reaktion",ref:r})),
            ...MODEL.pipelines.map(p=>({id:"pl:"+p._id,name:p.name,kind:"pipeline",ref:p})),
            ...MODEL.triggers.map(t=>({id:"tg:"+t._id,name:t.name,kind:"trigger",ref:t})),
            ...MODEL.frists.map(f=>({id:"fr:"+f._id,name:f.name,kind:"frist",ref:f})),
            ...MODEL.dienste.map(d=>({id:"di:"+d._id,name:d.name,kind:"dienst",ref:d})),
            ...MODEL.hostSettings.map(s=>({id:"hs:"+s._id,name:s.name,kind:"hostsetting",ref:s})),
            ...MODEL.codeNodes.map(c=>({id:"cn:"+c._id,name:c.name,kind:"codenode",ref:c})),
            ...MODEL.llmNodes.map(l=>({id:"ln:"+l._id,name:l.name,kind:"llmnode",ref:l}))];
  }
  // ── AGGREGATSWEISE ANORDNUNG: jedes Aggregat ein gekachelter Block (eigenes Rollen-Mini-Layout),
  //    aggregat-übergreifende Knoten (Sagas/Pipelines/Trigger/Reaktionen/geteilte Typen) im „Geteilt"-Band.
  const SHARED_KEY="§geteilt";
  // Rollen-Spalten innerhalb eines Aggregat-Blocks (links→rechts = Schreibfluss, dann Leseseite).
  const ROLE_AGG={command:0,decider:1,aggregate:2,state:2,event:3,rejection:3,applier:4,valueobject:5,enum:5,projektion:6,query:7,reader:8,queryresponse:9,store:10,readmodel:10};
  // Rollen-Spalten im Geteilt-Band.
  const ROLE_SHARED={saga:0,transition:1,reaktion:2,pipeline:3,trigger:4,frist:4,dienst:5,hostsetting:5,valueobject:6,enum:6,command:7,event:7,rejection:7,codenode:8,llmnode:8};

  // Wer besitzt diesen 📝/🤖-Knoten? (Rumpf-Ziel) — für die Gruppen-Zuordnung.
  function findCodeOwner(id){
    for(const d of MODEL.decider) if(d.codeSrc===id) return {kind:"decider",ref:d};
    for(const a of MODEL.applier) if(a.codeSrc===id) return {kind:"applier",ref:a};
    for(const p of MODEL.projektionen) if((p.handles||[]).some(h=>h.codeSrc===id)) return {kind:"projektion",ref:p};
    for(const r of MODEL.reader) if((r.handles||[]).some(h=>h.codeSrc===id)) return {kind:"reader",ref:r};
    for(const p of MODEL.pipelines) if((p.handles||[]).some(h=>h.codeSrc===id)) return {kind:"pipeline",ref:p};
    for(const s of MODEL.stores){if(((s.writeFns||[]).concat(s.readFns||[])).some(f=>f.codeSrc===id))return {kind:"store",ref:s};}
    for(const d of MODEL.dienste) if(d.codeSrc===id) return {kind:"dienst",ref:d};
    return null;
  }
  // Aggregat-Zugehörigkeit eines Knotens (oder SHARED_KEY). Ableitung über Namespace + Verdrahtung.
  // ══ GRAPH-BASIS: alles liegt als Graph vor — Partition nach ECHTER Verbundenheit, nicht nach Namespace. ══
  // Eine einzige Kanten-Quelle auf KNOTEN-Ebene (Node-Id → Node-Id), gespiegelt zu drawEdges' Beziehungen.
  function boardEdges(){
    const E=[]; const rec=n=>"rec:"+n;
    const codeId=id=>MODEL.codeNodes.some(c=>c._id===id)?"cn:"+id:(MODEL.llmNodes.some(l=>l._id===id)?"ln:"+id:null);
    const push=(a,b)=>{if(a&&b)E.push([a,b]);};
    MODEL.decider.forEach(d=>{if(recByName(d.command))push(rec(d.command),"dec:"+d._id);
      if(d.aggregat)push("dec:"+d._id,"agg:"+d.aggregat);
      (d.ergibt||[]).forEach(o=>{if(recByName(o.event))push("dec:"+d._id,rec(o.event));});
      if(d.codeSrc)push(codeId(d.codeSrc),"dec:"+d._id);});
    MODEL.applier.forEach(a=>{if(recByName(a.event))push(rec(a.event),"app:"+a._id);
      if(a.aggregat)push("app:"+a._id,"agg:"+a.aggregat); if(a.codeSrc)push(codeId(a.codeSrc),"app:"+a._id);});
    MODEL.states.forEach(s=>{if(s.aggregat)push("st:"+s._id,"agg:"+s.aggregat);});
    MODEL.sagas.forEach(s=>{if(recByName(s.triggerEvent))push(rec(s.triggerEvent),"saga:"+s.name);});
    MODEL.transitions.forEach(t=>{if(t.prozess)push("tr:"+t._id,"saga:"+t.prozess);
      (t.wenn||[]).forEach(e=>{if(recByName(e))push(rec(e),"tr:"+t._id);});
      (t.dann||[]).forEach(d=>{if(recByName(d.sende))push("tr:"+t._id,rec(d.sende));if(recByName(d.kompensation))push("tr:"+t._id,rec(d.kompensation));});});
    MODEL.readModels.forEach(rm=>{const st=MODEL.stores.find(s=>s.name===rm.store);if(st)push("rm:"+rm._id,"sto:"+st._id);});
    MODEL.stores.forEach(st=>(st.writeFns||[]).concat(st.readFns||[]).forEach(fn=>{if(fn.codeSrc)push(codeId(fn.codeSrc),"sto:"+st._id);}));
    MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{if(recByName(hd.event))push(rec(hd.event),"prj:"+p._id);
      (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)push("prj:"+p._id,"sto:"+f.store._id);});
      (hd.publishes||[]).forEach(ev=>{if(recByName(ev))push("prj:"+p._id,rec(ev));});
      if(hd.codeSrc)push(codeId(hd.codeSrc),"prj:"+p._id);}));
    MODEL.reader.forEach(r=>{const p=r.projektion&&MODEL.projektionen.find(x=>x.name===r.projektion);if(p)push("rdr:"+r._id,"prj:"+p._id);
      (r.handles||[]).forEach(hd=>{if(recByName(hd.query))push(rec(hd.query),"rdr:"+r._id);
        (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)push("rdr:"+r._id,"sto:"+f.store._id);});
        (hd.responses||[]).forEach(resp=>{if(recByName(resp))push("rdr:"+r._id,rec(resp));});
        if(hd.codeSrc)push(codeId(hd.codeSrc),"rdr:"+r._id);});});
    MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{if(recByName(hd.event))push(rec(hd.event),"rk:"+r._id);
      (hd.sends||[]).forEach(c=>{if(recByName(c))push("rk:"+r._id,rec(c));});
      (hd.publishes||[]).forEach(ev=>{if(recByName(ev))push("rk:"+r._id,rec(ev));});
      if(hd.codeSrc)push(codeId(hd.codeSrc),"rk:"+r._id);}));
    MODEL.pipelines.forEach(p=>{(p.handles||[]).forEach(hd=>{
        if(hd.inputKind==="event"&&recByName(hd.event))push(rec(hd.event),"pl:"+p._id);
        if(hd.inputKind==="trigger"){const pr=hd.prod||(hd.trigId?{k:"tg",id:hd.trigId}:null);
          if(pr&&pr.k==="tg")push("tg:"+pr.id,"pl:"+p._id); else if(pr&&pr.k==="pl")push("pl:"+pr.plId,"pl:"+p._id);}
        (hd.sends||[]).forEach(c=>{if(recByName(c))push("pl:"+p._id,rec(c));});
        // ge-yieldeter Trigger → jede Pipeline, die diese Trigger-Nachricht als Eingang hat (die Kette).
        (hd.emits||[]).forEach(tn=>MODEL.pipelines.forEach(q=>{if(q._id!==p._id)(q.handles||[]).forEach(qh=>{if(qh.inputKind==="trigger"&&qh.input===tn)push("pl:"+p._id,"pl:"+q._id);});}));
        if(hd.codeSrc)push(codeId(hd.codeSrc),"pl:"+p._id);});
      (p.dienste||[]).forEach(dn=>{const d=MODEL.dienste.find(x=>(x.vertrag||x.name)===dn);if(d)push("di:"+d._id,"pl:"+p._id);});});
    MODEL.frists.forEach(f=>{(f.plant||[]).forEach(ev=>{if(recByName(ev))push(rec(ev),"fr:"+f._id);});
      (f.storniert||[]).forEach(ev=>{if(recByName(ev))push(rec(ev),"fr:"+f._id);});
      if(recByName(f.sendet))push("fr:"+f._id,rec(f.sendet));
      if(f.dauerSetting){const hs=MODEL.hostSettings.find(x=>x.name===f.dauerSetting);if(hs)push("hs:"+hs._id,"fr:"+f._id);}});
    MODEL.dienste.forEach(d=>{if(d.codeSrc)push(codeId(d.codeSrc),"di:"+d._id);});
    // Betrieb: HostSetting → Konfigurations-Record (Feld) → Pipeline, die ihn per Konstruktor injiziert.
    MODEL.hostSettings.forEach(hs=>{if(hs.konfig&&recByName(hs.konfig))push("hs:"+hs._id,rec(hs.konfig));});
    MODEL.pipelines.forEach(p=>(p.konfigs||[]).forEach(k=>{if(recByName(k))push(rec(k),"pl:"+p._id);}));
    // Store-API: Records in Fn-Parametern/-Rückgaben (Transfer-Typen wie ImagePairStatistik) gehören an ihren Store.
    MODEL.stores.forEach(st=>(st.writeFns||[]).concat(st.readFns||[]).forEach(fn=>{
      [fn.rueckgabe,...(fn.params||[]).map(x=>x.typ)].forEach(t=>((t||"").match(/[A-Za-z_]\w*/g)||[]).forEach(n=>{
        const r=recByName(n);if(r&&r.kind!=="command")push(rec(n),"sto:"+st._id);}));}));
    // Typ-Komposition: VO/Enum → Feld-Owner (Record/Aggregat/ReadModel/State).
    const voN=new Set(MODEL.records.filter(r=>r.kind==="valueobject").map(r=>r.name)), enN=new Set(MODEL.enums.map(e=>e.name));
    const ownerId=o=>MODEL.records.some(r=>r.name===o)?"rec:"+o:MODEL.aggregate.some(a=>a.name===o)?"agg:"+o
      :(MODEL.readModels.find(rm=>rm.name===o)?"rm:"+MODEL.readModels.find(rm=>rm.name===o)._id
      :(MODEL.states.some(s=>s._id===o)?"st:"+o:null));
    alleFelder().forEach(({owner,f})=>{const bt=innerTyp(f.typ),oid=ownerId(owner);if(!oid)return;
      if(voN.has(bt))push("rec:"+bt,oid); else if(enN.has(bt))push("enum:"+bt,oid);});
    return E;
  }
  // Diagnose/Prüf-Skripte: Knoten + Kanten des Boards (dieselbe Wahrheit wie Layout und Insel-Erkennung).
  window.deGraph=()=>({knoten:graphNodes().map(n=>({id:n.id,kind:n.kind,name:n.name})),kanten:boardEdges()});
  // Zusammenhangskomponenten (Union-Find) über graphNodes + boardEdges → Map(nodeId → Wurzel-Id).
  function components(){
    const p={}, find=x=>{while(p[x]!==x){p[x]=p[p[x]];x=p[x];}return x;};
    graphNodes().forEach(n=>p[n.id]=n.id);
    boardEdges().forEach(([a,b])=>{if(p[a]!==undefined&&p[b]!==undefined){const ra=find(a),rb=find(b);if(ra!==rb)p[ra]=rb;}});
    const m=new Map();graphNodes().forEach(n=>m.set(n.id,find(n.id)));return m;
  }
  // Inseln = Knoten in winzigen Komponenten (≤2) OHNE Aggregat — die „einsamen" / unverdrahteten.
  const ISLE_MAX=2;
  function islandInfo(){
    const comp=components(), all=graphNodes(), byC=new Map();
    all.forEach(n=>{const c=comp.get(n.id);if(!byC.has(c))byC.set(c,[]);byC.get(c).push(n);});
    const ids=new Set();
    byC.forEach(nodes=>{const fach=nodes.filter(n=>n.kind!=="codenode"&&n.kind!=="llmnode");
      if(fach.length<=ISLE_MAX && !fach.some(n=>n.kind==="aggregate"))fach.forEach(n=>ids.add(n.id));});
    return {comp,byC,ids};
  }
  // Aggregat-Untergruppe (oder null = Brücke: cross-cutting, gehört keinem Aggregat).
  function subGroupOf(n){const g=groupKeyOf(n);return g===SHARED_KEY?null:g;}

  function groupKeyOf(n){
    const k=n.kind, r=n.ref;
    if(k==="aggregate") return r.name;
    if(k==="state") return r.aggregat||SHARED_KEY;
    if(k==="decider"||k==="applier") return r.aggregat||SHARED_KEY;
    if(k==="command"||k==="event"||k==="rejection") return recordAgg(r.name)||SHARED_KEY;
    if(k==="valueobject"||k==="enum") return typAgg(r.name)||SHARED_KEY;
    if(k==="projektion"){const a=(r.handles||[]).map(h=>recordAgg(h.event));return (a.length&&eindeutig(a))||SHARED_KEY;}
    if(k==="reader"){const p=r.projektion&&MODEL.projektionen.find(x=>x.name===r.projektion);return (p&&groupKeyOf({kind:"projektion",ref:p}))||SHARED_KEY;}
    if(k==="query"){const rd=MODEL.reader.find(x=>(x.handles||[]).some(h=>h.query===r.name));return (rd&&groupKeyOf({kind:"reader",ref:rd}))||SHARED_KEY;}
    if(k==="queryresponse"){const rd=MODEL.reader.find(x=>(x.handles||[]).some(h=>(h.responses||[]).includes(r.name)));return (rd&&groupKeyOf({kind:"reader",ref:rd}))||SHARED_KEY;}
    if(k==="store"){const u=MODEL.projektionen.find(p=>derivedStores(p).includes(r.name))||MODEL.reader.find(rd=>derivedStores(rd).includes(r.name));
      if(u)return groupKeyOf({kind:MODEL.projektionen.includes(u)?"projektion":"reader",ref:u});
      return SHARED_KEY;} // kein Handle→Fn verdrahtet: gehört (noch) keinem Aggregat
    if(k==="readmodel"){const st=MODEL.stores.find(s=>s.name===r.store);return st?groupKeyOf({kind:"store",ref:st}):SHARED_KEY;}
    if(k==="codenode"||k==="llmnode"){const o=findCodeOwner(r._id);return o?groupKeyOf(o):SHARED_KEY;}
    return SHARED_KEY; // saga, transition, pipeline, trigger, reaktion
  }
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
    // Wenige Knoten ohne Position (neu sichtbar, z. B. Details eingeblendet): neben einen platzierten Nachbarn legen
    //   statt das ganze Board neu zu würfeln. Viele ohne Position → unten komplett neu packen.
    const ohne=all.filter(n=>!positioned(n));
    if(!force&&ohne.length&&ohne.length<all.length*0.3){let off=0;
      ohne.forEach(n=>{const nb=[...(ADJ.out.get(n.id)||[]),...(ADJ.inn.get(n.id)||[])].map(id=>NODEBY.get(vertreterId(id))).find(m=>m&&m!==n&&VIS.has(m.id)&&positioned(m));
        if(nb){const p=P(nb),s=sz(nb);setze(n,p.x+s.w+60,p.y+(off%4)*40);}else{const s=spawnPos();setze(n,s.x,s.y);}off++;});}
    // Wie viele Knotenpaare überlappen aktuell deutlich? (früher Abbruch, sobald „viele").
    const overlaps=()=>{const b=all.map(n=>{const s=sz(n),p=P(n);return {x:p.x||0,y:p.y||0,w:s.w,h:s.h};});let c=0;
      for(let i=0;i<b.length;i++)for(let j=i+1;j<b.length;j++){const A=b[i],B=b[j];
        if(Math.min(A.x+A.w,B.x+B.w)-Math.max(A.x,B.x)>16 && Math.min(A.y+A.h,B.y+B.h)-Math.max(A.y,B.y)>16){if(++c>all.length)return c;}}
      return c;};
    // Ein arrangiertes Board (Handanordnung) NICHT neu würfeln — AUSSER es überlappt grob (alter/kaputter
    // Stand aus localStorage/board-model.json → heilen). „▦ Neu anordnen" ruft mit force=true.
    if(!force && all.some(positioned) && overlaps() < Math.max(3, Math.floor(all.length*0.12))) return;

    const COLGAP=48,ROWGAP=26,SHELFGAP=150,COMPGAP=240;
    const isCode=n=>n.kind==="codenode"||n.kind==="llmnode";
    // Ein Aggregat-/Brücken-BLOCK: Rollen→Spalten (+ Code-Bänder rechts) → relative Positionen + Box.
    const layoutBlock=(nodes,roleMap)=>{
      const rest=nodes.filter(n=>!isCode(n)), codes=nodes.filter(isCode);
      const codeByOwner=new Map(); const orphan=[];
      codes.forEach(cn=>{const o=findCodeOwner(cn.ref._id);
        if(o){if(!codeByOwner.has(o.ref))codeByOwner.set(o.ref,[]);codeByOwner.get(o.ref).push(cn);}else orphan.push(cn);});
      const rawOf=n=>roleMap[n.kind]!==undefined?roleMap[n.kind]:99;
      const colRoles=[...new Set(rest.map(rawOf))].sort((a,b)=>a-b);
      const byRole=new Map(colRoles.map(r=>[r,[]]));
      rest.forEach(n=>{const s=sz(n);byRole.get(rawOf(n)).push({n,w:s.w,h:s.h});});
      let cx=0,hh=0;const placed=[];
      colRoles.forEach(role=>{const list=byRole.get(role);const w=Math.max(120,...list.map(e=>e.w));
        let y=0;const owners=[];
        list.forEach(e=>{placed.push({n:e.n,rx:cx,ry:y});owners.push({ref:e.n.ref,ry:y});y+=e.h+ROWGAP;});
        hh=Math.max(hh,y-ROWGAP);cx+=w+COLGAP;
        const sub=[];owners.forEach(o=>{const cs=codeByOwner.get(o.ref);if(cs)cs.forEach(cn=>sub.push({cn,wantY:o.ry,ch:sz(cn).h}));});
        if(sub.length){sub.sort((a,b)=>a.wantY-b.wantY);let cw=120,cursor=0;
          sub.forEach(it=>{cw=Math.max(cw,sz(it.cn).w);const yy=Math.max(it.wantY,cursor);
            placed.push({n:it.cn,rx:cx,ry:yy});cursor=yy+it.ch+ROWGAP;hh=Math.max(hh,yy+it.ch);});cx+=cw+COLGAP;}});
      if(orphan.length){let cw=120,y=0;orphan.forEach(cn=>{const s=sz(cn);cw=Math.max(cw,s.w);
        placed.push({n:cn,rx:cx,ry:y});y+=s.h+ROWGAP;hh=Math.max(hh,y-ROWGAP);});cx+=cw+COLGAP;}
      return {placed,w:Math.max(0,cx-COLGAP),h:Math.max(0,hh)};
    };
    // Shelf-Packing über {w,h,…}-Boxen; setzt rx/ry; liefert Gesamtmaße.
    const shelf=(boxes,gap,factor)=>{const area=boxes.reduce((s,b)=>s+b.w*b.h,0),maxW=Math.max(1,...boxes.map(b=>b.w));
      const ROWW=Math.max(maxW,Math.sqrt(area)*(factor||1.2));let cx=0,ry=0,rh=0,tw=0;
      boxes.forEach(b=>{if(cx>0&&cx+b.w>ROWW){ry+=rh+gap;cx=0;rh=0;}b.rx=cx;b.ry=ry;cx+=b.w+gap;rh=Math.max(rh,b.h);tw=Math.max(tw,cx-gap);});
      return {w:tw,h:ry+rh};};
    // Eine KOMPONENTE: Aggregat-Blöcke (ROLE_AGG) + EIN Brücken-Block (ROLE_SHARED, cross-cutting), intern gepackt.
    const layoutComp=(nodes)=>{
      const subs=new Map();nodes.forEach(n=>{const s=subGroupOf(n)||SHARED_KEY;if(!subs.has(s))subs.set(s,[]);subs.get(s).push(n);});
      const aggKeys=[...subs.keys()].filter(k=>k!==SHARED_KEY).sort();
      const blocks=aggKeys.map(k=>layoutBlock(subs.get(k),ROLE_AGG));
      if(subs.has(SHARED_KEY))blocks.push(layoutBlock(subs.get(SHARED_KEY),ROLE_SHARED));
      const d=shelf(blocks,SHELFGAP,1.35);
      const placed=[];blocks.forEach(b=>b.placed.forEach(p=>placed.push({n:p.n,rx:b.rx+p.rx,ry:b.ry+p.ry})));
      return {placed,w:d.w,h:d.h};
    };
    // Komponenten bilden; Inseln (winzige Komponenten ohne Aggregat) aussortieren.
    const {byC}=islandInfo();
    const islandNodes=[],mainComps=[];
    byC.forEach(alle=>{const nodes=alle.filter(n=>VIS.has(n.id));if(!nodes.length)return;
      if(alle.length<=ISLE_MAX && !alle.some(n=>n.kind==="aggregate")) islandNodes.push(...nodes); else mainComps.push(nodes); });
    // Jede Haupt-Komponente → Region-Box, nach Fläche absteigend (größtes Subsystem zuerst), dann shelf-gepackt.
    const regions=mainComps.map(nodes=>layoutComp(nodes)).sort((a,b)=>b.w*b.h-a.w*a.h);
    const total=shelf(regions,COMPGAP,1.05);
    // Region-Knoten platzieren (absolut = Region-Ursprung + Block-Offset + relative Position).
    regions.forEach(d=>d.placed.forEach(p=>setze(p.n,d.rx+p.rx,d.ry+p.ry)));
    // Inseln: kompakte Gitter-Zeile ganz unten (die „einsamen"/unverdrahteten Knoten).
    if(islandNodes.length){const IW=Math.max(700,total.w);let ix=0,iy=total.h+COMPGAP,rh=0;
      islandNodes.forEach(n=>{const s=sz(n);if(ix>0&&ix+s.w>IW){iy+=rh+ROWGAP;ix=0;rh=0;}
        setze(n,ix,iy);ix+=s.w+COLGAP;rh=Math.max(rh,s.h);});}
    if(VIEW.kompakt)speichereKpos();
  }
  // Eingeklappt? Kompakt-Ansicht: standardmäßig zu (nur `_offen` klappt auf); Voll-Ansicht: nur `_collapsed` klappt zu.
  const istZu=n=>VIEW.kompakt?!n.ref._offen:!!n.ref._collapsed;
  function nodeEditor(n){
    const zu=istZu(n);
    const el=h("div",{class:"gnode2 n-"+n.kind+(zu?" collapsed":"")+(ISLE.has(n.id)?" island":"")
      +(n.ref.ungeschrieben?" ungeschrieben":(n.ref.ausCode===false||(n.ref.ausCode===undefined&&MERGE_KEYS[kollektionVon(n.kind)])?" entwurf":""))});el.dataset.id=n.id;
    const pos=P(n);el.style.left=(pos.x||0)+"px";el.style.top=(pos.y||0)+"px";
    const title=NODELABEL[n.kind]||n.kind;
    const head=h("div",{class:"ghead"},
      h("span",{class:"gcol",title:"Ein-/Ausklappen",onclick:()=>{if(VIEW.kompakt)n.ref._offen=!n.ref._offen;else n.ref._collapsed=!n.ref._collapsed;render();}},zu?"▸":"▾"),
      h("span",{class:"gtitle"+(n.name?" hatname":""),title:title+(n.name?" · "+n.name:"")},h("span",{class:"gk"},title+(n.name?" · ":"")),h("span",{class:"gn"},n.name||"")),
      h("span",{class:"gx",title:"Löschen",onclick:()=>delNode(n)},"✕"));
    head.onpointerdown=e=>{if(e.target.classList.contains("gx")||e.target.classList.contains("gcol"))return;startMove(e,el,P(n),()=>waehle(n.id));};
    el.append(head);
    el.append(kurzfassung(n));
    const body=h("div",{class:"gbody"});
    fuelleKoerper(body,n);
    el.append(body);return el;
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
    else if(n.kind==="pipeline")pipelineCard(body,n.ref);
    else if(n.kind==="trigger")triggerCard(body,n.ref);
    else if(n.kind==="frist")fristCard(body,n.ref);
    else if(n.kind==="dienst")dienstCard(body,n.ref);
    else if(n.kind==="hostsetting")hostSettingCard(body,n.ref);
    else if(n.kind==="codenode")codeNodeCard(body,n.ref);
    else if(n.kind==="llmnode")llmNodeCard(body,n.ref);
    else recordCard(body,n.ref);
  }
  function delNode(n){const k=n.kind,ref=n.ref;
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
    else if(k==="pipeline")MODEL.pipelines.splice(MODEL.pipelines.indexOf(ref),1);
    else if(k==="trigger")MODEL.triggers.splice(MODEL.triggers.indexOf(ref),1);
    else if(k==="frist")MODEL.frists.splice(MODEL.frists.indexOf(ref),1);
    else if(k==="dienst")MODEL.dienste.splice(MODEL.dienste.indexOf(ref),1);
    else if(k==="hostsetting")MODEL.hostSettings.splice(MODEL.hostSettings.indexOf(ref),1);
    else if(k==="codenode")MODEL.codeNodes.splice(MODEL.codeNodes.indexOf(ref),1);
    else if(k==="llmnode")MODEL.llmNodes.splice(MODEL.llmNodes.indexOf(ref),1);
    else MODEL.records.splice(MODEL.records.indexOf(ref),1);
    render();}

  // Koordinaten: Welt = unskaliert; canvas = Bildschirm.
  function applyPan(){if(world)world.style.transform="translate("+PAN.x+"px,"+PAN.y+"px) scale("+PAN.s+")";updateMinimapViewport();pruefeLod();}

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
    p.append(h("h4",{},h("span",{},"Domänen anzeigen"),
      h("span",{style:"cursor:pointer;color:#8a93a7",title:"Schließen",onclick:()=>{FILTER_OPEN=false;renderFilterPanel();}},"✕")));
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
    const jump=ev=>{const r=box.getBoundingClientRect();
      const wx=MM.minx+(ev.clientX-r.left)/MM.scale, wy=MM.miny+(ev.clientY-r.top)/MM.scale;
      const cr=canvas.getBoundingClientRect();PAN.x=cr.width/2-wx*PAN.s;PAN.y=cr.height/2-wy*PAN.s;applyPan();};
    let drag=false;
    box.addEventListener("pointerdown",e=>{e.stopPropagation();e.preventDefault();drag=true;try{box.setPointerCapture(e.pointerId);}catch(x){}jump(e);});
    box.addEventListener("pointermove",e=>{if(drag)jump(e);});
    box.addEventListener("pointerup",()=>{drag=false;});
    box.addEventListener("dblclick",e=>e.stopPropagation());
  }
  function drawMinimap(){
    if(!MM||!MM.svg||!world)return;
    while(MM.svg.firstChild)MM.svg.removeChild(MM.svg.firstChild);
    const vis=graphNodes().filter(n=>VIS.has(n.id));
    const boxes=vis.map(n=>{const el=world.querySelector('[data-id="'+n.id+'"]'),p=P(n);
      return {x:p.x||0,y:p.y||0,w:(el&&el.offsetWidth)||250,h:(el&&el.offsetHeight)||120,key:groupKeyOf(n),kind:n.kind};});
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
    const cr=canvas.getBoundingClientRect();if(!cr.width)return;
    MM.vp.setAttribute("x",((-PAN.x/PAN.s-MM.minx)*MM.scale).toFixed(1));
    MM.vp.setAttribute("y",((-PAN.y/PAN.s-MM.miny)*MM.scale).toFixed(1));
    MM.vp.setAttribute("width",Math.max(3,(cr.width/PAN.s)*MM.scale).toFixed(1));
    MM.vp.setAttribute("height",Math.max(3,(cr.height/PAN.s)*MM.scale).toFixed(1));
  }
  function toWorld(cx,cy){const r=canvas.getBoundingClientRect();return {x:(cx-r.left-PAN.x)/PAN.s,y:(cy-r.top-PAN.y)/PAN.s};}
  function slotCenter(el){const wr=world.getBoundingClientRect(),r=el.getBoundingClientRect();
    return {x:(r.left+r.width/2-wr.left)/PAN.s,y:(r.top+r.height/2-wr.top)/PAN.s};}
  const bez=(x1,y1,x2,y2)=>{const dx=Math.max(46,Math.abs(x2-x1)*0.5);return "M"+x1+","+y1+" C"+(x1+dx)+","+y1+" "+(x2-dx)+","+y2+" "+x2+","+y2;};

  // Verbinden per Slot-Drag (Gummiband + Typprüfung, dann MODEL mutieren).
  function startLink(e,from){
    const path=document.createElementNS(SVGNS,"path");path.setAttribute("class","glink tmp");
    path.setAttribute("fill","none");path.setAttribute("stroke","#cbb8ff");path.setAttribute("stroke-width","2.4");path.setAttribute("stroke-dasharray","5 4");
    svg.append(path);const a=slotCenter(from);
    // Gültige Ziele sofort sichtbar: ALLE kompatiblen Slots grün ringeln (so sieht man, wohin ein Feld darf — und wohin nicht).
    const cands=Object.values(SLOTS).filter(s=>s&&s!==from&&compatible(from,s));cands.forEach(s=>s.classList.add("cand"));
    const clear=()=>{cands.forEach(s=>s.classList.remove("cand"));document.querySelectorAll("#de .slot.hot").forEach(x=>x.classList.remove("hot"));};
    const mv=ev=>{const b=toWorld(ev.clientX,ev.clientY);path.setAttribute("d",bez(a.x,a.y,b.x,b.y));
      document.querySelectorAll("#de .slot.hot").forEach(x=>x.classList.remove("hot"));
      const t=hitSlot(ev.clientX,ev.clientY);if(t&&compatible(from,t))t.classList.add("hot");};
    const up=ev=>{window.removeEventListener("pointermove",mv);window.removeEventListener("pointerup",up);
      path.remove();clear();
      const t=hitSlot(ev.clientX,ev.clientY);if(t&&compatible(from,t))applyLink(from,t);};
    window.addEventListener("pointermove",mv);window.addEventListener("pointerup",up);
  }
  function hitSlot(cx,cy){let el=document.elementFromPoint(cx,cy);while(el&&!el.__slot)el=el.parentElement;return el&&el.__slot?el:null;}
  function compatible(a,b){const A=a.__slot,B=b.__slot;if(!A||!B)return false;if(A.dir===B.dir)return false;if(A.type!==B.type)return false;
    if((A.kind==="arg")!==(B.kind==="arg"))return false;              // Argument-Pin nur an Argument-Pin
    if(A.kind==="arg"&&A.trans!==B.trans)return false;                 // und nur innerhalb derselben Transition
    if(A.type==="field"){const src=A.dir==="out"?A:B,snk=A.dir==="out"?B:A;   // Feld-Quelle muss den Wunsch des Eingangs erfüllen
      if(snk.want==="count")return istColl(src.ftyp)||istGanzzahl(src.ftyp);
      if(snk.want==="collection")return istColl(src.ftyp);
      return true;}
    return true;}
  function applyLink(a,b){
    const O=a.__slot.dir==="out"?a.__slot:b.__slot, I=a.__slot.dir==="out"?b.__slot:a.__slot;
    if(O.type==="cmd"){const d=dec(I.dec);if(d)d.command=O.rec;}
    else if(O.type==="evtOut"){
      if(O.dec){const d=dec(O.dec);if(d&&!(d.ergibt||[]).some(x=>x.event===I.rec))(d.ergibt=d.ergibt||[]).push({event:I.rec});}
      // Konsument veröffentlicht ein reaktives Event (HandlerOutputRouter: yield IEvent → Broker-Re-Publish).
      else if(O.proj){const p=MODEL.projektionen.find(x=>x._id===O.proj);const hd=p&&p.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}
      else if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}}
    else if(O.type==="prozess"){const t=MODEL.transitions.find(x=>x._id===O.trans);if(t)t.prozess=I.saga;}
    else if(O.type==="evtUse"){
      if(I.app){const p=app(I.app);if(p)p.event=O.rec;}
      // Event → Frist: plant / storniert (die Drei-End-Relation der Composition-Root-Frist).
      else if(I.frist){const f=MODEL.frists.find(x=>x._id===I.frist);if(f){const arr=I.role==="storniert"?(f.storniert=f.storniert||[]):(f.plant=f.plant||[]);if(!arr.includes(O.rec))arr.push(O.rec);}}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p){p.handles=p.handles||[];
        if(I.handleIdx==="open"){if(!p.handles.some(x=>x.event===O.rec))p.handles.push({event:O.rec,effekt:""});}
        else p.handles[I.handleIdx].event=O.rec;}}
      else if(I.trigger){const sg=MODEL.sagas.find(x=>x.name===I.saga);if(sg)sg.triggerEvent=O.rec;}
      else if(I.trans){const t=MODEL.transitions.find(x=>x._id===I.trans);if(t){
        t.wenn=t.wenn||[];if(I.wennIdx==="open"){if(!t.wenn.includes(O.rec))t.wenn.push(O.rec);}else t.wenn[I.wennIdx]=O.rec;}}
      else if(I.reaktion){const r=MODEL.reaktionen.find(x=>x._id===I.reaktion);if(r){r.handles=r.handles||[];
        if(I.handleIdx==="open"){if(!r.handles.some(x=>x.event===O.rec))r.handles.push({event:O.rec,sends:[]});}
        else r.handles[I.handleIdx].event=O.rec;}}
      // Event → Pipeline-Handle (die „Reaktion IST eine Pipeline"-Naht).
      else if(I.pipeline){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p){p.handles=p.handles||[];
        if(I.handleIdx==="openevt"||I.handleIdx==="open"){if(!p.handles.some(x=>x.event===O.rec&&x.inputKind==="event"))p.handles.push({inputKind:"event",event:O.rec,sends:[]});}
        else{p.handles[I.handleIdx].event=O.rec;p.handles[I.handleIdx].inputKind="event";delete p.handles[I.handleIdx].trigId;}}}}
    else if(O.type==="sagaCmd"){
      if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.sends=hd.sends||[];if(!hd.sends.includes(I.rec))hd.sends.push(I.rec);}}
      else if(O.pipeline){const p=MODEL.pipelines.find(x=>x._id===O.pipeline);const hd=p&&p.handles[O.handleIdx];if(hd){hd.sends=hd.sends||[];if(!hd.sends.includes(I.rec))hd.sends.push(I.rec);}}
      // Frist fällig → genau ein Command @ Aggregat (der AddDeadlines-Router: Kontext → Command).
      else if(O.frist){const f=MODEL.frists.find(x=>x._id===O.frist);if(f){f.sendet=I.rec;f.aggregat=recordAgg(I.rec)||f.aggregat;}}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans);const d=t&&(t.dann||[])[O.dannIdx];if(d){
        if(O.role==="komp"){if(d.kompensation!==I.rec)delete d.kompensationAusdruck;d.kompensation=I.rec;}
        else{if(d.sende!==I.rec)delete d.sendeAusdruck;d.sende=I.rec;}}}}
    // Trigger-Msg → Pipeline-Handle: Quelle = Trigger-Ingress-Node (O.trigId) ODER eine andere Pipeline, die
    //   den Trigger yieldet (O.pipeline). Producer-Ref bleibt rename-fest.
    else if(O.type==="trigmsg"){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p){p.handles=p.handles||[];
      let msgName=O.msgName;
      // Quelle = offener Pipeline-Emit-Port (noch unbenannt) → Trigger-Ausgang am Quell-Handle materialisieren.
      if(O.pipeline&&!msgName){const sp=MODEL.pipelines.find(x=>x._id===O.pipeline);const shd=sp&&sp.handles[O.handleIdx];if(shd){msgName=uniq("NeuTriggerMsg");(shd.emits=shd.emits||[]).push(msgName);}}
      const prod=O.trigId?{k:"tg",id:O.trigId}:(O.pipeline?{k:"pl",plId:O.pipeline,hi:O.handleIdx,name:msgName}:null);
      const nm=msgName||(prod&&prod.k==="tg"?trigMsgLabel(prod.id):"")||"Trigger";
      if(I.handleIdx==="opentrg"||I.handleIdx==="open"){if(!p.handles.some(x=>x.inputKind==="trigger"&&x.input===nm))p.handles.push({inputKind:"trigger",input:nm,prod,sends:[],emits:[],schedules:[]});}
      else{const hd=p.handles[I.handleIdx];hd.inputKind="trigger";hd.input=nm;hd.prod=prod;delete hd.event;delete hd.selfName;delete hd.trigId;}}}
    // ScheduleSelf-Ausgang → Self-Handle derselben Pipeline (interner Tick/Timeout-Loop).
    else if(O.type==="self"){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);const hd=p&&p.handles[I.handleIdx];if(hd&&hd.inputKind==="self")hd.selfName=O.name;}
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
    else if(O.type==="code"){setCodeSrc(I.target,O.codeNode);}
    // 🤖 LLM-Prompt-Node → Code-Block-Eingang: die LLM-Node wird zur Prompt-Quelle dieses Blocks.
    else if(O.type==="prompt"){const l=MODEL.llmNodes.find(x=>x._id===O.llm);if(l&&I.codeBlock)l.promptZiel=I.codeBlock;}
    // HostSetting → Frist-Dauer (später auch Trigger-Config): der Wert speist die Fälligkeit.
    else if(O.type==="setting"){const nm=(MODEL.hostSettings.find(x=>x._id===O.hostSetting)||{}).name;if(I.frist){const f=MODEL.frists.find(x=>x._id===I.frist);if(f)f.dauerSetting=nm;}}
    // Dienst-Vertrag → Konsument (Konstruktor-Injektion): den Vertrag der Dienst-Liste hinzufügen.
    else if(O.type==="dienst"){const dn=MODEL.dienste.find(x=>x._id===O.dienst)||{};const nm=dn.vertrag||dn.name;if(I.pipeline){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p){p.dienste=p.dienste||[];if(!p.dienste.includes(nm))p.dienste.push(nm);}}}
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

  // Kanten aus dem MODEL zeichnen (Slot-Mitte → Slot-Mitte; eingeklappt → an den Kopf).
  //   Eingeklappt: an die passende KOPF-SEITE (Ausgang rechts, Eingang links, oben mittig) statt in die Kopfmitte.
  function anchor(el){if(el.offsetParent!==null)return slotCenter(el);const nd=el.closest(".gnode2");const hd=nd&&nd.querySelector(".ghead");
    if(!hd)return slotCenter(el);const d=sdir(el),y=nd.offsetTop+hd.offsetHeight/2;
    if(d>0)return {x:nd.offsetLeft+nd.offsetWidth,y};if(d<0)return {x:nd.offsetLeft,y};return {x:nd.offsetLeft+nd.offsetWidth/2,y:nd.offsetTop};}
  // Anschlussseite eines Slots: rechts (.o)=+1, links (.i)=-1, oben/sonst=0.
  const sdir=el=>el&&el.classList.contains("o")?1:(el&&el.classList.contains("i")?-1:0);
  const knotenIdVon=el=>{const g=el&&el.closest(".gnode2");return g?g.dataset.id:"";};
  function drawEdges(){
    if(!svg)return;[...svg.querySelectorAll(".glink:not(.tmp)")].forEach(p=>p.remove());
    if(svgTop)[...svgTop.querySelectorAll(".glink")].forEach(p=>p.remove());
    // Kurve mit Anschlussrichtung je Ende (da/db: +1 tritt nach rechts aus, -1 nach links).
    const mk=(A,B,da,db,color,dash,cls,ds,tgt)=>{const dx=Math.max(46,Math.abs(B.x-A.x)*0.5);
      const p=document.createElementNS(SVGNS,"path");p.setAttribute("class","glink"+(cls?" "+cls:""));p.setAttribute("fill","none");
      p.setAttribute("stroke",color);p.setAttribute("stroke-width","2.2");if(dash)p.setAttribute("stroke-dasharray","5 4");
      if(ds){p.dataset.dec=ds[0];p.dataset.app=ds[1];}
      p.setAttribute("d","M"+A.x+","+A.y+" C"+(A.x+da*dx)+","+A.y+" "+(B.x+db*dx)+","+B.y+" "+B.x+","+B.y);(tgt||svg).append(p);return p;};
    const add=(k1,k2,color,dash)=>{const a=SLOTS[k1],b=SLOTS[k2];if(!a||!b)return;const A=anchor(a),B=anchor(b);
      let da=sdir(a),db=sdir(b);if(!da)da=B.x>=A.x?1:-1;if(!db)db=A.x>=B.x?1:-1;
      const p=mk(A,B,da,db,color,dash);p.dataset.a=knotenIdVon(a);p.dataset.b=knotenIdVon(b);};
    MODEL.decider.forEach(d=>{
      if(d.command)add("cmd:out:"+d.command,"dec:cmdin:"+d._id,"#4a86d6");
      if(d.aggregat)add("dec:aggout:"+d._id,"agg:left:"+d.aggregat+":"+d._id,"#33b1a6",true);
      (d.ergibt||[]).forEach(o=>{if(o.event)add("dec:evtout:"+d._id+":"+o.event,"evt:in:"+o.event,"#4fb06a");});
      if(d.codeSrc)add("code:out:"+d.codeSrc,"dec:rumpf:"+d._id,"#8a8f9c",true);
    });
    MODEL.applier.forEach(p=>{
      if(p.event)add("evt:out:"+p.event,"app:evtin:"+p._id,"#4fb06a");
      if(p.aggregat)add("app:aggout:"+p._id,"agg:right:"+p.aggregat+":"+p._id,"#d1953f",true);
      if(p.codeSrc)add("code:out:"+p.codeSrc,"app:rumpf:"+p._id,"#8a8f9c",true);
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
        if(d.sende)add("tr:sende:"+t._id+":"+di,"cmd:in:"+d.sende,"#4a86d6");
        if(d.kompensation)add("tr:komp:"+t._id+":"+di,"cmd:in:"+d.kompensation,"#cf6f68",true);
      });
    });
    // ── Leseseite: ReadModel→Store, Projektion/Reader→Store-Scope, Event→Projektion, Query→Reader,
    //    Reader→Response, und CODE (📝/🤖) → Rumpf-Ports (gestrichelt). ──
    const CODE="#8a8f9c";
    MODEL.readModels.forEach(rm=>{if(rm.store)add("rm:out:"+rm._id,"sto:rm:"+rm.store,"#c9a24b");});
    MODEL.stores.forEach(st=>{
      (st.writeFns||[]).forEach(fn=>{if(fn.codeSrc)add("code:out:"+fn.codeSrc,"impl:in:"+st._id+":"+fn._id,CODE,true);});
      (st.readFns||[]).forEach(fn=>{if(fn.codeSrc)add("code:out:"+fn.codeSrc,"impl:in:"+st._id+":"+fn._id,CODE,true);});
    });
    MODEL.projektionen.forEach(p=>{
      (p.handles||[]).forEach((hd,hi)=>{if(hd.event)add("evt:out:"+hd.event,"prj:in:"+p._id+":"+hi,"#4fb06a");
        // Handle → aufgerufene Write-Fn (Store-Scope ist damit sichtbar-abgeleitet).
        (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)add("wcall:out:"+p._id+":"+hi+":"+fid,"wcall:in:"+f.store._id+":"+fid,"#2f9d95");});
        // veröffentlichtes reaktives Event (gestrichelt/teal = Broker, verlierbar).
        (hd.publishes||[]).forEach(ev=>{if(ev)add("prj:pub:"+p._id+":"+hi+":"+ev,"evt:in:"+ev,"#2fd6b0",true);});
        if(hd.codeSrc)add("code:out:"+hd.codeSrc,"ctrl:in:"+p._id+":"+hi,CODE,true);});
    });
    MODEL.reader.forEach(r=>{
      // IReader<TProjection>: Reader → Projektion (der gemeinsame Store ist der Treffpunkt).
      if(r.projektion){const p=MODEL.projektionen.find(x=>x.name===r.projektion);if(p)add("rdr:proj:"+r._id,"prj:asproj:"+p._id,"#c08a3e",true);}
      (r.handles||[]).forEach((hd,hi)=>{if(hd.query)add("qry:out:"+hd.query,"rdr:qin:"+r._id+":"+hi,"#9678d6");
        // Handle → aufgerufene Read-Fn(s), auch über mehrere Stores.
        (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)add("rcall:out:"+r._id+":"+hi+":"+fid,"rcall:in:"+f.store._id+":"+fid,"#c08a3e");});
        (hd.responses||[]).forEach(resp=>{if(resp)add("rdr:rout:"+r._id+":"+hi+":"+resp,"qrsp:in:"+resp,"#9d78d6");});
        if(hd.codeSrc)add("code:out:"+hd.codeSrc,"ctrl:in:"+r._id+":"+hi,CODE,true);});
    });
    // Reaktion (emittierend): Trigger-Event → Handle, Handle → OneOf-Command(s), 📝 → Controller-Rumpf.
    MODEL.reaktionen.forEach(r=>{
      (r.handles||[]).forEach((hd,hi)=>{
        if(hd.event)add("evt:out:"+hd.event,"rk:in:"+r._id+":"+hi,"#4fb06a");
        (hd.sends||[]).forEach(c=>{if(c)add("rk:send:"+r._id+":"+hi+":"+c,"cmd:in:"+c,"#4a86d6");});
        (hd.publishes||[]).forEach(ev=>{if(ev)add("rk:pub:"+r._id+":"+hi+":"+ev,"evt:in:"+ev,"#2fd6b0",true);});
        if(hd.codeSrc)add("code:out:"+hd.codeSrc,"ctrl:in:"+r._id+":"+hi,CODE,true);});
    });
    // ── Ingress/Pipeline: Eingang (Trigger-Node|Pipeline-yield|Event) → Handle; Ausgänge Command/Trigger/Self; 📝 → Rumpf. ──
    MODEL.pipelines.forEach(p=>{(p.handles||[]).forEach((hd,hi)=>{
      // Eingangs-Kante je nach Quelle.
      if(hd.inputKind==="trigger"){const pr=hd.prod||(hd.trigId?{k:"tg",id:hd.trigId}:null);
        if(pr&&pr.k==="pl")add("pl:emit:"+pr.plId+":"+pr.hi+":"+pr.name,"pl:in:"+p._id+":"+hi,"#f0883e");
        else if(pr)add("trg:msg:"+pr.id,"pl:in:"+p._id+":"+hi,"#f0883e");}
      else if(hd.inputKind==="event"&&hd.event)add("evt:out:"+hd.event,"pl:in:"+p._id+":"+hi,"#4fb06a");
      // yield ICommand.
      (hd.sends||[]).forEach(c=>{if(c)add("pl:send:"+p._id+":"+hi+":"+c,"cmd:in:"+c,"#4a86d6");});
      // yield IPipelineTrigger → verbrauchende Pipeline (die Kette, z. B. FileWatch → ImageProcessing).
      (hd.emits||[]).forEach(tn=>MODEL.pipelines.forEach(q=>{if(q._id!==p._id)(q.handles||[]).forEach((qh,qi)=>{if(qh.inputKind==="trigger"&&qh.input===tn)add("pl:emit:"+p._id+":"+hi+":"+tn,"pl:in:"+q._id+":"+qi,"#f0883e");});}));
      // ScheduleSelf → passender Self-Handle (Name-Match), gestrichelter Loop.
      (hd.schedules||[]).forEach(sc=>{const ti=(p.handles||[]).findIndex(x=>x.inputKind==="self"&&x.selfName===sc.name);
        if(ti>=0)add("pl:sched:"+p._id+":"+hi+":"+sc.name,"pl:in:"+p._id+":"+ti,"#c98a3a",true);});
      if(hd.codeSrc)add("code:out:"+hd.codeSrc,"plctrl:in:"+p._id+":"+hi,CODE,true);});});
    // ── Betrieb/Host (Composition Root): Frist-Drei-End-Relation + Dienst-Bindung. ──
    MODEL.frists.forEach(f=>{
      (f.plant||[]).forEach(ev=>{if(ev)add("evt:out:"+ev,"fr:plant:"+f._id+":"+ev,"#4fb06a");});
      (f.storniert||[]).forEach(ev=>{if(ev)add("evt:out:"+ev,"fr:cancel:"+f._id+":"+ev,"#cf6f68",true);});
      if(f.sendet)add("fr:send:"+f._id,"cmd:in:"+f.sendet,"#4a86d6");
      if(f.dauerSetting){const hs=MODEL.hostSettings.find(x=>x.name===f.dauerSetting);if(hs)add("hs:wert:"+hs._id,"fr:dauer:"+f._id,"#c98a3a",true);}
    });
    MODEL.pipelines.forEach(p=>(p.dienste||[]).forEach(dn=>{const d=MODEL.dienste.find(x=>(x.vertrag||x.name)===dn);if(d)add("di:vertrag:"+d._id,"pl:dienst:"+p._id+":"+dn,"#c9a24b");}));
    MODEL.dienste.forEach(d=>{if(d.codeSrc)add("code:out:"+d.codeSrc,"di:impl:"+d._id,CODE,true);});
    // 🤖 LLM-Prompt-Node → Code-Block (Eingang): der Prompt speist den Rumpf-Kommentar.
    MODEL.llmNodes.forEach(l=>{if(l.promptZiel)add("llm:prout:"+l._id,"code:prin:"+l.promptZiel,"#a48fd6",true);});
    // ── Typ-Komposition: VO/Enum → Feld (welches Feld benutzt diesen Typ), gestrichelt. ──
    const voNamen=new Set(MODEL.records.filter(r=>r.kind==="valueobject").map(r=>r.name));
    const enNamen=new Set(MODEL.enums.map(e=>e.name));
    alleFelder().forEach(({owner,f})=>{const bt=innerTyp(f.typ);
      if(voNamen.has(bt))add("ftype:out:rec:"+bt,"ftype:in:"+owner+":"+f._id,"#49a996",true);
      else if(enNamen.has(bt))add("ftype:out:enum:"+bt,"ftype:in:"+owner+":"+f._id,"#8a8aa0",true);});
    // ── Zusammengezogene Kanten: Pfade DURCH eingeklappte Details (Command →[Decider]→ Event …) Kopf an Kopf. ──
    const ELS=new Map([...world.querySelectorAll(".gnode2")].map(e=>[e.dataset.id,e]));
    const kopf=(e,seite)=>{const hd=e.querySelector(".ghead");return {x:e.offsetLeft+(seite>0?e.offsetWidth:0),y:e.offsetTop+(hd?hd.offsetHeight/2:12)};};
    KONTRAKT.forEach(([u,v])=>{const eu=ELS.get(u),ev=ELS.get(v);if(!eu||!ev)return;
      const kv=(NODEBY.get(v)||{}).kind,col=kv==="event"?"#4fb06a":(kv==="command"?"#4a86d6":"#8a8f9c");
      const p=mk(kopf(eu,1),kopf(ev,-1),1,-1,col,false,"kontrakt");p.dataset.a=u;p.dataset.b=v;});
    wendeFokusAn();
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
  function startPan(e){e.preventDefault();canvas.classList.add("panning");const sx=e.clientX,sy=e.clientY,ox=PAN.x,oy=PAN.y;PANNED=false;
    const mv=ev=>{if(Math.abs(ev.clientX-sx)+Math.abs(ev.clientY-sy)>3)PANNED=true;PAN.x=ox+(ev.clientX-sx);PAN.y=oy+(ev.clientY-sy);applyPan();};
    const up=()=>{canvas.classList.remove("panning");window.removeEventListener("pointermove",mv);window.removeEventListener("pointerup",up);};
    window.addEventListener("pointermove",mv);window.addEventListener("pointerup",up);}

  // ══ ANSICHT (reine Darstellung — Modell, Scaffolder und Round-trip bleiben unberührt) ══════════════════════
  //   (1) Kompakt-Karten: Knoten standardmäßig eingeklappt (Kopf + Kurzfassung), bearbeitet wird im INSPECTOR.
  //   (2) Details eingeklappt: Decider/Applier/State/Code/LLM/Ablehnung/VO/Enum/Response liegen IN ihrem
  //       sichtbaren Besitzer (Chips + Inspector-Sektionen); Pfade durch sie werden als Kopf-Kanten zusammengezogen.
  //   (4) Semantischer Zoom: Landkarte (<0.4, Aggregat-Kacheln + gebündelte Kanten) · Ablauf (<0.75, nur Titel) · Detail.
  //   (5) Slice-Fokus: Klick auf einen Knoten → sein vertikaler Schnitt bleibt hell, der Rest tritt zurück.
  let VIEW={kompakt:true,details:false}, VKEY="cqrs-ansicht", KPKEY="cqrs-kpos", KPOS={k:{},d:{}};
  function ladeAnsicht(k){VKEY="cqrs-ansicht"+(k?":"+k:"");KPKEY="cqrs-kpos"+(k?":"+k:"");
    try{const v=JSON.parse(localStorage.getItem(VKEY)||"null");if(v)VIEW={...VIEW,...v};}catch(e){}
    try{KPOS=JSON.parse(localStorage.getItem(KPKEY)||"{}")||{};}catch(e){KPOS={};}
    if(!KPOS.k||!KPOS.d)KPOS={k:{},d:{}};}
  function speichereAnsicht(){try{localStorage.setItem(VKEY,JSON.stringify(VIEW));}catch(e){}}
  function speichereKpos(){try{localStorage.setItem(KPKEY,JSON.stringify(KPOS));}catch(e){}}
  // Position eines Knotens in der AKTUELLEN Ansicht. Voll = x/y im Modell (wandert ins Board); Kompakt = eigenes,
  //   nur browser-lokales Layout (KPOS) — die kleineren Karten brauchen eine dichtere Anordnung.
  //   Je Detail-Stufe ein eigenes Layout (k = Details eingeklappt, d = Details als Knoten) — Umschalten verliert nichts.
  function P(n){if(!VIEW.kompakt)return n.ref;
    const L=KPOS[VIEW.details?"d":"k"]||(KPOS[VIEW.details?"d":"k"]={});
    let p=L[n.id];if(!p){p=L[n.id]={};if(n.ref._kpos){p.x=n.ref._kpos.x;p.y=n.ref._kpos.y;}}
    if(n.ref._kpos)delete n.ref._kpos;return p;}
  let INSP=false, INSP_SCROLL=null;   // INSP: gerade wird eine Inspector-Kopie gebaut (keine Slot-Registrierung)

  const DETAIL_KINDS=new Set(["decider","applier","state","codenode","llmnode","rejection","valueobject","enum","queryresponse"]);
  let VIS=new Set(), VERTRETER=new Map(), DETAILS=new Map(), EINGEKLAPPT=new Set(), NODEBY=new Map(), KONTRAKT=[];
  let ADJ={out:new Map(),inn:new Map()};
  const vertreterId=id=>(VERTRETER.get(id)||[id])[0];
  // Direkte Besitzer eines Detail-Knotens (Knoten-Ids) — aus der Verdrahtung, nie aus Namen.
  function direkteBesitzer(n){const r=n.ref,rec=x=>x&&recByName(x)?"rec:"+x:null,agg=x=>x&&MODEL.aggregate.some(a=>a.name===x)?"agg:"+x:null;
    switch(n.kind){
      case "decider":return [rec(r.command)||agg(r.aggregat)];
      case "applier":return [rec(r.event)||agg(r.aggregat)];
      case "llmnode":return r.promptZiel?["cn:"+r.promptZiel]:(ADJ.out.get(n.id)||[]);
      case "codenode":case "state":case "valueobject":case "enum":return ADJ.out.get(n.id)||[];
      // Nur die erzeugende Seite besitzt: Ablehnung ← Decider, Response ← Reader (nicht VO-Feldtyp-Kanten).
      case "rejection":return (ADJ.inn.get(n.id)||[]).filter(x=>x.startsWith("dec:"));
      case "queryresponse":return (ADJ.inn.get(n.id)||[]).filter(x=>x.startsWith("rdr:"));}
    return [];}
  function berechneSicht(){
    const alle=graphNodes();NODEBY=new Map(alle.map(n=>[n.id,n]));
    const push=(m,k,v)=>{let a=m.get(k);if(!a)m.set(k,a=[]);if(!a.includes(v))a.push(v);};
    ADJ={out:new Map(),inn:new Map()};
    boardEdges().forEach(([a,b])=>{if(a===b||!NODEBY.has(a)||!NODEBY.has(b))return;push(ADJ.out,a,b);push(ADJ.inn,b,a);});
    const einklappbar=n=>!VIEW.details&&DETAIL_KINDS.has(n.kind);
    // Vertreter = sichtbarer Besitzer, transitiv durch eingeklappte Besitzer (Code → Decider → Command).
    //   Ohne Besitzer bleibt ein Detail selbst sichtbar (neu angelegt / unverdrahtet → nichts verschwindet).
    const memo=new Map();
    const vert=(id,pfad)=>{if(memo.has(id))return memo.get(id);const n=NODEBY.get(id);if(!n)return [];
      if(!einklappbar(n)){memo.set(id,[id]);return [id];}
      if(pfad.has(id))return [];pfad.add(id);
      const res=[...new Set(direkteBesitzer(n).filter(Boolean).flatMap(o=>vert(o,pfad)))];pfad.delete(id);
      const out=res.length?res:[id];memo.set(id,out);return out;};
    VIS=new Set();VERTRETER=new Map();DETAILS=new Map();EINGEKLAPPT=new Set();
    alle.forEach(n=>{const v=vert(n.id,new Set());VERTRETER.set(n.id,v);
      if(v.length===1&&v[0]===n.id){if(!HIDDEN.has(groupKeyOf(n)))VIS.add(n.id);}
      else{EINGEKLAPPT.add(n.id);v.forEach(o=>push(DETAILS,o,n.id));}});
    // Zusammengezogene Kanten: sichtbar →(eingeklappt)*→ sichtbar. Ins Aggregat nicht (das zeigt der Block).
    KONTRAKT=[];if(VIEW.details)return;
    const direkt=new Set();ADJ.out.forEach((bs,a)=>bs.forEach(b=>{direkt.add(a+"\u0000"+b);direkt.add(b+"\u0000"+a);}));
    VIS.forEach(u=>{const ziele=new Set(),seen=new Set(),stack=(ADJ.out.get(u)||[]).filter(x=>EINGEKLAPPT.has(x));
      while(stack.length){const x=stack.pop();if(seen.has(x))continue;seen.add(x);
        (ADJ.out.get(x)||[]).forEach(y=>{if(VIS.has(y)){if(y!==u)ziele.add(y);}else if(EINGEKLAPPT.has(y))stack.push(y);});}
      ziele.forEach(v=>{if(NODEBY.get(v).kind==="aggregate"||direkt.has(u+"\u0000"+v))return;KONTRAKT.push([u,v]);});});
  }

  // ── (5) SLICE: der vertikale Schnitt durch einen Knoten — GERICHTET über die Board-Kanten:
  //    • vorwärts im Fluss (Command → [Decider] → Events → [Applier] / Projektion → Store …), an Knotenpunkten
  //      (Aggregat, fremde Commands, Prozess/Regel, Reaktion, Pipeline, Frist, Typen, Betrieb) wird angehalten;
  //    • Leseseite nachziehen: an Projektion/Store die Reader + Read Models, am Reader seine Queries;
  //    • rückwärts nur der Auslöser (durch eingeklappte Details bis zum ersten sichtbaren Knoten).
  //    Aggregat/Prozess als Start: ihre angesteckten Decider/Applier/State bzw. Regeln sind Mit-Startpunkte.
  const STOP=new Set(["aggregate","command","saga","transition","reaktion","pipeline","frist","valueobject","enum","dienst","hostsetting","trigger","konfig"]);
  const LESE_START=new Set(["projektion","reader","query","store","readmodel","queryresponse"]);
  function sliceVon(start){const res=new Set([start]),N=id=>NODEBY.get(id)||{};
    const inn=id=>ADJ.inn.get(id)||[],out=id=>ADJ.out.get(id)||[];
    const k0=N(start).kind,seeds=[start];
    if(k0==="aggregate"||k0==="saga")inn(start).forEach(x=>{res.add(x);seeds.push(x);});
    const zurueck=(id,seen)=>inn(id).forEach(x=>{if(seen.has(x))return;seen.add(x);res.add(x);if(EINGEKLAPPT.has(x))zurueck(x,seen);});
    seeds.forEach(s=>zurueck(s,new Set()));
    const q=[...seeds],fertig=new Set();
    const add=(x,weiter)=>{res.add(x);if(weiter&&!fertig.has(x))q.push(x);};
    while(q.length){const id=q.shift();if(fertig.has(id))continue;fertig.add(id);const k=N(id).kind;
      if(!seeds.includes(id)&&STOP.has(k))continue;
      out(id).forEach(y=>add(y,true));
      if(k==="projektion"||k==="store")inn(id).forEach(y=>{const ky=N(y).kind;
        if(ky==="reader"||ky==="readmodel")add(y,true);else if(k==="projektion"&&ky==="event"&&LESE_START.has(k0))add(y,false);});
      if(k==="reader")inn(id).forEach(y=>{if(N(y).kind==="query")add(y,false);});}
    return res;}
  let SEL=null, FOCUS=null;
  function waehle(id){SEL=id||null;FOCUS=SEL?sliceVon(SEL):null;wendeFokusAn();zeigeInspector();}
  function wendeFokusAn(){if(!world)return;const an=!!FOCUS;world.classList.toggle("fokus",an);
    const selV=SEL?(VERTRETER.get(SEL)||[SEL]):[];
    world.querySelectorAll(".gnode2").forEach(el=>{const id=el.dataset.id;el.classList.toggle("inslice",an&&FOCUS.has(id));el.classList.toggle("sel",selV.includes(id));});
    if(!an)return;
    world.querySelectorAll(".glink").forEach(p=>p.classList.toggle("inslice",FOCUS.has(p.dataset.a)&&FOCUS.has(p.dataset.b)));
    world.querySelectorAll(".gtile").forEach(t=>t.classList.toggle("inslice",(t.dataset.ids||"").split("|").some(i=>FOCUS.has(i))));}
  document.addEventListener("keydown",e=>{if(e.key==="Escape"&&SEL)waehle(null);});

  // ── (1) INSPECTOR: das Formular des gewählten Knotens + seiner eingeklappten Details + Nachbarn zum Springen.
  function zeigeInspector(){if(!canvas)return;const alt=canvas.querySelector(".ginsp");if(alt)alt.remove();if(!SEL)return;
    const n=NODEBY.get(SEL);if(!n){SEL=null;return;}
    const p=h("div",{class:"ginsp"});p.dataset.sel=SEL;
    ["pointerdown","dblclick","wheel","click"].forEach(ev=>p.addEventListener(ev,e=>e.stopPropagation()));
    const nm=n.name||NODELABEL[n.kind]||n.kind;
    p.append(h("div",{class:"gi-h"},h("span",{class:"gi-k kc-"+n.kind},NODELABEL[n.kind]||n.kind),h("span",{class:"gi-n",title:nm},nm),
      h("button",{title:"Slice einpassen",onclick:()=>passeEin([...(FOCUS||[])].map(vertreterId).filter(i=>VIS.has(i)),1)},"⤢"),
      h("button",{title:"Auf der Fläche zeigen",onclick:()=>{centerOn(n);pulseNode(n);}},"◎"),
      h("button",{title:"Schließen (Esc)",onclick:()=>waehle(null)},"✕")));
    const sektion=(m,titel)=>{const sec=h("div",{class:"gi-sec"});
      if(titel)sec.append(h("div",{class:"gi-st kc-"+m.kind},titel));
      const b=h("div",{class:"gbody"});INSP=true;try{fuelleKoerper(b,m);}finally{INSP=false;}sec.append(b);return sec;};
    p.append(sektion(n,null));
    const ORD=["decider","rejection","applier","state","codenode","llmnode","valueobject","enum","queryresponse"];
    const det=(DETAILS.get(n.id)||[]).map(id=>NODEBY.get(id)).filter(Boolean).sort((a,b)=>ORD.indexOf(a.kind)-ORD.indexOf(b.kind));
    const titelVon=m=>m.kind==="codenode"?"{ } Rumpf"+(m.name&&m.name!=="Code"?" · "+m.name:""):(NODELABEL[m.kind]||m.kind)+(m.name?" · "+m.name:"");
    det.forEach(m=>p.append(sektion(m,titelVon(m))));
    // Nachbarn (über eingeklappte Details hinweg, auf ihre sichtbaren Vertreter abgebildet).
    const eigen=new Set([n.id,...det.map(m=>m.id)]),rein=new Set(),raus=new Set();
    const sammle=(ids,ziel)=>(ids||[]).forEach(y=>(VERTRETER.get(y)||[y]).forEach(v=>{if(!eigen.has(v))ziel.add(v);}));
    eigen.forEach(x=>{sammle(ADJ.inn.get(x),rein);sammle(ADJ.out.get(x),raus);});
    const rel=h("div",{class:"gi-rel"});
    const liste=(titel,set)=>{if(!set.size)return;rel.append(h("h5",{},titel));
      [...set].map(id=>NODEBY.get(id)).filter(Boolean).forEach(m=>rel.append(h("a",{title:NODELABEL[m.kind]||m.kind,onclick:()=>{waehle(m.id);centerOn(m);pulseNode(m);}},
        h("i",{class:"kc-"+m.kind,style:"display:inline-block;width:7px;height:7px;border-radius:50%;margin-right:5px"}),m.name||NODELABEL[m.kind])));};
    liste("◀ Eingang",rein);liste("Ausgang ▶",raus);if(rel.childNodes.length)p.append(rel);
    canvas.append(p);
    if(INSP_SCROLL&&INSP_SCROLL.id===SEL)p.scrollTop=INSP_SCROLL.top;INSP_SCROLL=null;}

  // Kurzfassung auf der eingeklappten Karte: das Wesentliche in einer Zeile + Chips der eingeklappten Details.
  const kurz=xs=>{xs=[...new Set((xs||[]).filter(Boolean))];return xs.slice(0,2).join(", ")+(xs.length>2?" +"+(xs.length-2):"");};
  function kurzfassung(n){const r=n.ref,k=n.kind;let t="";
    if(k==="command"){const ev=MODEL.decider.filter(d=>d.command===r.name).flatMap(d=>(d.ergibt||[]).map(o=>o.event)).filter(e=>(recByName(e)||{}).kind!=="rejection");
      t=ev.length?"→ "+kurz(ev):"→ (kein Decider)";}
    else if(k==="event"){const c=MODEL.decider.filter(d=>(d.ergibt||[]).some(o=>o.event===r.name)).map(d=>d.command);t=c.length?"← "+kurz(c):"";}
    else if(k==="aggregate")t=MODEL.decider.filter(d=>d.aggregat===r.name).length+" Commands · "+MODEL.applier.filter(a=>a.aggregat===r.name).length+" Events · "+(r.state||[]).length+" State-Felder";
    else if(k==="projektion"||k==="reaktion")t="← "+kurz((r.handles||[]).map(x=>x.event));
    else if(k==="reader")t="? "+kurz((r.handles||[]).map(x=>x.query));
    else if(k==="pipeline")t=(r.handles||[]).length+" Handle · → "+kurz((r.handles||[]).flatMap(x=>x.sends||[]));
    else if(k==="saga")t="Auslöser: "+(r.triggerEvent||"—")+" · "+transOf(r).length+" Regeln";
    else if(k==="transition")t="WENN "+kurz(r.wenn)+" → "+kurz((r.dann||[]).map(d=>d.sende));
    else if(k==="store")t=(r.writeFns||[]).length+" schreibend · "+(r.readFns||[]).length+" lesend";
    else if(k==="codenode")t=((r.text||"").trim().split("\n")[0]||"// leer");
    if(Array.isArray(r.felder)&&k!=="aggregate")t+=(t?" · ":"")+r.felder.length+" Felder";
    const box=h("div",{class:"gsum",title:"Klick: im Inspector bearbeiten",onclick:()=>waehle(n.id)});
    if(t)box.append(h("div",{class:"gs-t",title:t},t));
    const det=(DETAILS.get(n.id)||[]).map(id=>NODEBY.get(id)).filter(Boolean);
    if(det.length){const chips=h("div",{class:"gchips"}),zahl={};det.forEach(m=>zahl[m.kind]=(zahl[m.kind]||0)+1);
      const LBL={decider:"⚖ Decider",applier:"↻ Applier",state:"▤ State",llmnode:"🤖 LLM",rejection:"✕ Ablehnung",valueobject:"◇ VO",enum:"◇ Enum",queryresponse:"↩ Response"};
      Object.keys(zahl).forEach(kk=>{let lbl=(LBL[kk]||kk)+(zahl[kk]>1?" ×"+zahl[kk]:"");
        if(kk==="codenode")lbl="{ } "+det.filter(m=>m.kind==="codenode").reduce((s,m)=>s+((m.ref.text||"").split("\n").length),0)+" Zeilen";
        chips.append(h("span",{class:"gchip"},lbl));});
      if(det.some(m=>(m.kind==="decider"||m.kind==="applier")&&!m.ref.codeSrc&&!m.ref.leer))
        chips.append(h("span",{class:"gchip warn",title:"Decide-/Apply-Rumpf fehlt"},"⚙ Logik fehlt"));
      box.append(chips);}
    return box;}

  // ── (4) SEMANTISCHER ZOOM ──
  let LOD=null;
  const lodVon=s=>s<0.4?"karte":(s<0.75?"ablauf":"detail");
  function pruefeLod(){if(!canvas||!world)return;world.style.setProperty("--inv",(1/PAN.s).toFixed(3));
    const l=lodVon(PAN.s);if(l===LOD)return;const vorher=LOD;LOD=l;
    canvas.classList.toggle("lod-karte",l==="karte");canvas.classList.toggle("lod-ablauf",l==="ablauf");
    if(l==="karte")baueKarte();else if(vorher!==null)drawEdges();   // Anker wandern (Körper ein/aus) → neu zeichnen
    document.querySelectorAll("#de .gview .lod span").forEach(s=>s.classList.toggle("on",s.dataset.l===l));}
  function zoomAuf(s){if(!canvas)return;const cr=canvas.getBoundingClientRect(),wx=(cr.width/2-PAN.x)/PAN.s,wy=(cr.height/2-PAN.y)/PAN.s;
    PAN.s=s;PAN.x=cr.width/2-wx*s;PAN.y=cr.height/2-wy*s;applyPan();}
  // Knoten-Menge einpassen (minS: mindestens diese Zoomstufe, z. B. Ablauf nach Klick auf eine Kachel).
  function passeEin(ids,maxS,minS){if(!canvas||!world||!ids||!ids.length)return;let x1=1e9,y1=1e9,x2=-1e9,y2=-1e9;
    ids.forEach(id=>{const n=NODEBY.get(id),el=world.querySelector('[data-id="'+id+'"]');if(!n||!el)return;const p=P(n);
      x1=Math.min(x1,p.x||0);y1=Math.min(y1,p.y||0);x2=Math.max(x2,(p.x||0)+el.offsetWidth);y2=Math.max(y2,(p.y||0)+el.offsetHeight);});
    if(x1>x2)return;const cr=canvas.getBoundingClientRect(),pad=60;
    const s=Math.max(minS||0.08,Math.min(maxS||1,(cr.width-2*pad)/Math.max(1,x2-x1),(cr.height-2*pad)/Math.max(1,y2-y1)));
    PAN.s=s;PAN.x=cr.width/2-(x1+x2)/2*s;PAN.y=cr.height/2-(y1+y2)/2*s;applyPan();}
  // Landkarte: je Aggregat (bzw. geteiltem Block / Inseln) eine Kachel mit Zählern; Kanten zwischen Kacheln gebündelt.
  function baueKarte(){if(!world)return;world.querySelectorAll(".gtile,svg.gtilesvg").forEach(x=>x.remove());
    const comp=components(),T=new Map();
    const schl=n=>{const g=subGroupOf(n);if(g)return g;if(ISLE.has(n.id))return "§inseln";return SHARED_KEY+"|"+comp.get(n.id);};
    VIS.forEach(id=>{const n=NODEBY.get(id),el=world.querySelector('[data-id="'+id+'"]');if(!n||!el)return;const p=P(n),x=p.x||0,y=p.y||0,k=schl(n);
      let t=T.get(k);if(!t)T.set(k,t={k,x1:1e9,y1:1e9,x2:-1e9,y2:-1e9,kinds:{},ids:[],namen:[]});
      t.x1=Math.min(t.x1,x);t.y1=Math.min(t.y1,y);t.x2=Math.max(t.x2,x+el.offsetWidth);t.y2=Math.max(t.y2,y+el.offsetHeight);
      t.kinds[n.kind]=(t.kinds[n.kind]||0)+1;t.ids.push(id);if(["saga","pipeline","reaktion","trigger"].includes(n.kind))t.namen.push(n.name);});
    const PAD=40,kOf=new Map();T.forEach(t=>t.ids.forEach(id=>kOf.set(id,t.k)));
    T.forEach(t=>{const geteilt=t.k.startsWith(SHARED_KEY),hue=domHue(geteilt?SHARED_KEY:t.k);
      const titel=t.k==="§inseln"?"⚠ Inseln":(geteilt?"⋯ "+(t.namen.length?kurz(t.namen):"Geteilt"):t.k);
      const zahlen=Object.keys(t.kinds).sort((a,b)=>t.kinds[b]-t.kinds[a]).map(k=>t.kinds[k]+" "+(NODELABEL[k]||k)).join(" · ");
      const div=h("div",{class:"gtile",title:"Klick: hineinzoomen"},h("div",{class:"gtl-t"},titel),h("div",{class:"gtl-c"},zahlen));
      div.style.left=(t.x1-PAD)+"px";div.style.top=(t.y1-PAD)+"px";div.style.width=(t.x2-t.x1+2*PAD)+"px";div.style.height=(t.y2-t.y1+2*PAD)+"px";
      div.style.borderColor="hsl("+hue+" 45% 55%)";div.dataset.ids=t.ids.join("|");
      div.onclick=()=>{if(!PANNED)passeEin(t.ids,1,0.45);};
      world.append(div);});
    const cnt=new Map();ADJ.out.forEach((bs,a)=>bs.forEach(b=>{const ka=kOf.get(vertreterId(a)),kb=kOf.get(vertreterId(b));if(!ka||!kb||ka===kb)return;
      const key=ka<kb?ka+"\u0000"+kb:kb+"\u0000"+ka;cnt.set(key,(cnt.get(key)||0)+1);}));
    const s=document.createElementNS(SVGNS,"svg");s.setAttribute("class","gtilesvg");
    cnt.forEach((c,key)=>{const [a,b]=key.split("\u0000"),A=T.get(a),B=T.get(b);
      const ax=(A.x1+A.x2)/2,ay=(A.y1+A.y2)/2,bx=(B.x1+B.x2)/2,by=(B.y1+B.y2)/2;
      const p=document.createElementNS(SVGNS,"path");p.setAttribute("d","M"+ax+","+ay+" L"+bx+","+by);
      p.style.strokeWidth="calc("+(1.5+Math.log2(c+1)*1.5).toFixed(1)+"px * var(--inv))";s.append(p);
      const tx=document.createElementNS(SVGNS,"text");tx.setAttribute("x",(ax+bx)/2);tx.setAttribute("y",(ay+by)/2);tx.setAttribute("text-anchor","middle");
      tx.style.fontSize="calc(12px * var(--inv))";tx.style.strokeWidth="calc(3px * var(--inv))";tx.textContent=String(c);s.append(tx);});
    world.insertBefore(s,world.firstChild);
    wendeFokusAn();}

  // Ansichts-Leiste über der Fläche.
  function ansichtLeiste(){
    const btn=(lbl,on,title,fn)=>h("button",{class:on?"on":"",title,onclick:fn},lbl);
    const alle=auf=>{graphNodes().forEach(n=>{if(!VIS.has(n.id))return;
      if(VIEW.kompakt){if(auf)n.ref._offen=true;else delete n.ref._offen;}else{if(auf)delete n.ref._collapsed;else n.ref._collapsed=true;}});render();};
    const lod=h("span",{class:"lod",title:"Semantischer Zoom — Mausrad oder hier klicken"},
      ...[["karte","Landkarte",0.25],["ablauf","Ablauf",0.55],["detail","Detail",1]].map(([l,t,s])=>{const sp=h("span",{onclick:()=>zoomAuf(s)},t);sp.dataset.l=l;if(LOD===l)sp.classList.add("on");return sp;}));
    const nDet=graphNodes().filter(n=>DETAIL_KINDS.has(n.kind)).length;
    return h("div",{class:"gview"},
      h("b",{style:"color:#cbd3e1"},"Ansicht"),
      btn("▣ Kompakt",VIEW.kompakt,"Karten eingeklappt, Bearbeiten im Inspector rechts (eigenes Layout)",()=>{VIEW.kompakt=!VIEW.kompakt;speichereAnsicht();render();}),
      btn("⚙ Details ("+nDet+")",VIEW.details,"Decider/Applier/State/Code/Typen/Ablehnungen als eigene Knoten zeigen — sonst eingeklappt in ihren Besitzer",()=>{VIEW.details=!VIEW.details;speichereAnsicht();render();}),
      h("span",{class:"sep"}),
      btn("⊟ Alle zu",false,"Alle Karten einklappen",()=>alle(false)),
      btn("⊞ Alle auf",false,"Alle Karten aufklappen",()=>alle(true)),
      btn("⤢ Alles zeigen",false,"Ganzes Board einpassen",()=>passeEin([...VIS],1)),
      lod,
      h("span",{style:"margin-left:8px"},"Kopf antippen = auswählen + Slice · Esc = Fokus aus · ▸ = auf-/zuklappen · Kopf ziehen = verschieben · Punkt ziehen = verbinden"));}

  function renderGraph(){
    const root=document.getElementById("de-form");
    const altI=root.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    root.innerHTML="";
    root.append(datalistEl());
    ISLE=islandInfo().ids;   // einsame Inseln (unverbundene Knoten) aus der Graph-Analyse
    berechneSicht();         // sichtbar / eingeklappte Details / zusammengezogene Kanten
    if(SEL&&!NODEBY.has(SEL))SEL=null;FOCUS=SEL?sliceVon(SEL):null;LOD=null;
    const tb=(kind,label)=>h("button",{class:"add jumpable",
      title:label.replace(/^\+\s*/,"")+" — Hover: alle hervorheben · Ctrl/Cmd+Klick: zum nächsten springen · Klick: neu",
      onmouseenter:()=>highlightKind(kind,true),onmouseleave:()=>highlightKind(kind,false),
      onclick:e=>{if(e.ctrlKey||e.metaKey){e.preventDefault();jumpNextOfKind(kind);}else neuerKnoten(kind);}},label);
    root.append(h("div",{class:"gtoolbar"},
      tb("command","+ Command"),tb("event","+ Event"),tb("rejection","+ Ablehnung"),
      tb("valueobject","+ Value Object"),tb("enum","+ Enum"),tb("aggregate","+ Aggregat"),
      tb("state","+ State"),tb("decider","+ Decider"),tb("applier","+ Applier"),tb("saga","+ Prozess"),tb("transition","+ Regel"),
      tb("readmodel","+ Read Model"),tb("store","+ Store"),tb("projektion","+ Projektion"),tb("query","+ Query"),tb("queryresponse","+ Response"),tb("reader","+ Reader"),tb("reaktion","+ Reaktion"),
      tb("trigger","+ Trigger"),tb("pipeline","+ Pipeline"),
      tb("frist","+ ⏳ Frist"),tb("dienst","+ Dienst"),tb("hostsetting","+ HostSetting"),
      tb("codenode","+ 📝 Code"),tb("llmnode","+ 🤖 LLM"),
      h("button",{class:"add island-btn",style:"margin-left:auto",
        title:"Einsame Inseln (unverbundene Knoten) — Hover: markieren · Klick: der Reihe nach anspringen",
        onmouseenter:()=>highlightIslands(true),onmouseleave:()=>highlightIslands(false),onclick:()=>jumpIslands()},
        (ISLE.size?"⚠ ":"✓ ")+ISLE.size+" Inseln")));
    root.append(ansichtLeiste());
    SLOTS={};SYNC={};   // Code-Sync-Registry je Render neu aufbauen (nur sichtbare Knoten pollen)
    canvas=h("div",{class:"gcanvas"});
    world=h("div",{class:"gworld"+(VIEW.kompakt?" kompakt":"")});
    svg=document.createElementNS(SVGNS,"svg");svg.setAttribute("class","gedges");world.append(svg);
    const nodes=graphNodes();
    // Domänen-Filter + eingeklappte Details: nicht rendern (Kanten zu ihnen entfallen bzw. werden zusammengezogen).
    const vis=nodes.filter(n=>VIS.has(n.id));
    vis.forEach(n=>world.append(nodeEditor(n)));
    svgTop=document.createElementNS(SVGNS,"svg");svgTop.setAttribute("class","gedges top");world.append(svgTop);
    if(!vis.length)world.append(h("div",{class:"gempty"},"Nichts sichtbar — alle Domänen ausgeblendet (🗂) oder leeres Modell."));
    canvas.append(world);root.append(canvas);
    buildMinimap(canvas);
    renderFilterPanel();
    packLayout();  // jetzt ist world im Dokument → echte Knotengrößen messbar → überlappungsfreies Packing
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
    canvas.onclick=e=>{if(PANNED)return;const t=e.target;
      if(t.closest&&t.closest(".gnode2,.ginsp,.gfilter,.gminimap,.gtile,.gpick"))return;if(SEL)waehle(null);};
    drawEdges();requestAnimationFrame(drawEdges);
    zeigeInspector();
  }
  function showPicker(canvas,sx,sy,wx,wy){
    canvas.querySelectorAll(".gpick").forEach(p=>p.remove());
    const pick=h("div",{class:"gpick"});pick.style.left=sx+"px";pick.style.top=sy+"px";
    const opt=(kind,label)=>h("button",{onclick:()=>{pick.remove();neuerKnoten(kind,wx,wy);}},label);
    pick.append(h("div",{class:"gpick-t"},"Was soll hier entstehen?"),
      opt("command","Command"),opt("event","Event"),opt("rejection","Ablehnung"),
      opt("valueobject","Value Object"),opt("enum","Enum"),opt("aggregate","Aggregat"),
      opt("state","State"),opt("decider","Decider"),opt("applier","Applier"),opt("saga","Prozess"),opt("transition","Regel"),
      opt("readmodel","Read Model"),opt("store","Store"),opt("projektion","Projektion"),opt("query","Query"),opt("queryresponse","Response"),opt("reader","Reader"),opt("reaktion","Reaktion"),
      opt("trigger","Trigger"),opt("pipeline","Pipeline"),
      opt("frist","⏳ Frist"),opt("dienst","Dienst"),opt("hostsetting","HostSetting"),
      opt("codenode","📝 Code"),opt("llmnode","🤖 LLM"));
    canvas.append(pick);
    setTimeout(()=>{const off=ev=>{if(!pick.contains(ev.target)){pick.remove();document.removeEventListener("pointerdown",off);}};document.addEventListener("pointerdown",off);},0);
  }

  // Prozess-HUB: Prozess<Auslöser>. Transitionen stecken sich HIER an (sichtbare Kante, keine Ableitung).
  function prozessHubCard(body,s){
    body.append(topSlot("event","Auslöser (startet): "+(s.triggerEvent||"— (Event hineinziehen)"),{type:"evtUse",dir:"in",saga:s.name,trigger:true},"saga:trigger:"+s.name));
    body.append(nameInp(s,"name","Prozess","saga"));
    body.append(h("input",{value:s.namespace??"",oninput:e=>s.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    const aggs=sagaAggs(s);
    body.append(h("div",{class:"gsec"},"berührt (abgeleitet): "+(aggs.length?aggs.join(" · "):"—")));
    body.append(h("div",{class:"gsec"},"Regeln ◀ (anstecken)"));
    transOf(s).forEach(t=>{const p=anchorDot("prozess");p.classList.add("i");reg("hub:in:"+s.name+":"+t._id,p,null);
      body.append(h("div",{class:"slotrow"},p,h("span",{class:"slotlbl"},"WENN "+((t.wenn||[])[0]||"?")+((t.wenn||[]).length>1?" +"+((t.wenn.length-1)+(t.sammelEvent?1:0))+"":(t.sammelEvent?" +alle":""))+" → "+((t.dann||[]).map(d=>d.sende||"?").join(", ")||"?"))));});
    const oi=port("prozess");oi.classList.add("i");reg("hub:in:"+s.name+":open",oi,{type:"prozess",dir:"in",saga:s.name});
    body.append(h("div",{class:"slotrow"},oi,h("span",{class:"slotlbl"},"+ Regel anstecken")));
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
      const so=port("command");so.classList.add("o");so.title=d.sende||"(Command)";reg("tr:sende:"+t._id+":"+di,so,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"sende"});
      body.append(h("div",{class:"slotrow o"},
        h("button",{class:"rm",onclick:()=>{t.dann.splice(di,1);if(!t.dann.length)t.dann.push({});render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.75"},"Dann "+(d.sende||"")),
        mk("×N","Fan-out (SendeJe) — N Commands je Element",!!d.sendeJe,()=>{d.sendeJe=d.sendeJe?undefined:true;render();}),so));
      if(d.sendeAusdruck)body.append(h("div",{class:"gsec",title:"aus dem Code gelesen — wird verbatim zurückgeschrieben",style:"font-family:monospace;opacity:.7;white-space:pre-wrap"},"λ "+d.sendeAusdruck));
      const ko=port("rejection");ko.classList.add("o");ko.title=d.kompensation||"(Kompensation)";reg("tr:komp:"+t._id+":"+di,ko,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"komp"});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.55"},"↩ "+(d.kompensation||"")),ko));
    });
    body.append(h("button",{class:"add",onclick:()=>{(t.dann=t.dann||[]).push({});render();}},"+ Dann (weiterer Command am selben Join)"));
    // Je Dann → eine Regel (gleiche Bedingung). Argument-Bau = mechanisches Feld-Mapping (D/S) → default-Stub.
    // KEIN Code/LLM-Port: eine Regel trägt keine freie Logik, nur die Verdrahtung Events→Command.
  }

  // ── Aktionen ──
  window.deOpen=function(){document.getElementById("de").classList.add("on");if(!MODEL.records.length&&!MODEL.aggregate.length)deBoot();else render();};
  window.deClose=function(){document.getElementById("de").classList.remove("on");};
  // Standalone (/editor): sofort zeigen; Boot lädt das gespeicherte Board (nicht mehr leer).
  window.deShow=function(){document.getElementById("de").classList.add("on");};
  window.deLeer=function(){MODEL=normalize(null);render();};
  window.dePing=async function(){try{const r=await fetch("/api/editor/model");badge(r.ok);}catch(e){badge(false);}};
  async function loadLive(){try{const r=await fetch("/api/editor/model");if(!r.ok)throw 0;const m=await r.json();badge(true);return m;}catch(e){badge(false);return null;}}
  function badge(live){const b=document.getElementById("de-badge");b.textContent=live?"SimHost live":"offline";b.className="badge"+(live?"":" off");}
  // ↻ Vom Graph laden: den Code NEU einlesen (GraphExtractor im SimHost) und mit dem Board MERGEN — der Code ist
  //   die Wahrheit; Layout, Entwürfe und ungeschriebene Änderungen bleiben (kein Ersetzen mehr).
  window.deReload=async function(opt){opt=opt||{};
    if(!opt.ohneExtract){deFlash("↻ lese Code neu ein …",true);
      try{const r=await fetch("/api/editor/extract",{method:"POST"});const x=await r.json();if(!x.ok)deFlash("⚠ Einlesen fehlgeschlagen: "+(x.grund||""),false);}
      catch(e){badge(false);}}
    const live=await loadLive();
    const code=live||(embedded&&(embedded.records||embedded.aggregate)?embedded:null);
    const vorher=MODEL&&(MODEL.records.length||MODEL.aggregate.length)?JSON.parse(JSON.stringify(MODEL)):null;
    MODEL=mergeBoard(code,vorher);render();meldeMerge();};
  // ▦ Neu anordnen: alle Positionen verwerfen → aggregatsweises Kachel-Layout neu rechnen.
  window.deReflow=function(){if(VIEW.kompakt){KPOS[VIEW.details?"d":"k"]={};speichereKpos();}else graphNodes().forEach(n=>{delete n.ref.x;delete n.ref.y;});render();};

  // ── DURABLE BOARD-PERSISTENZ: das VOLLE MODEL (alle Sammlungen + Layout) ist die Quelle. ──
  // Server (board-model.json via SimHost) = geteilte Wahrheit; localStorage = Browser-Sicherheitsnetz
  //   (schützt ungespeicherte Änderungen, auch offline). Boot-Reihenfolge: Server → localStorage → C#.
  let _asT=null;
  // Autosave (debounced) nach jeder Änderung — verliert nichts zwischen zwei „💾 Speichern".
  function autosave(){if(_asT)clearTimeout(_asT);_asT=setTimeout(saveLocal,600);}
  function saveLocal(){try{localStorage.setItem(LSKEY,JSON.stringify(MODEL));}catch(e){}}
  function loadLocal(){try{const s=localStorage.getItem(LSKEY);return s?JSON.parse(s):null;}catch(e){return null;}}
  async function loadBoard(){try{const r=await fetch("/api/editor/board");if(!r.ok)return null;badge(true);return await r.json();}catch(e){return null;}}
  function deFlash(txt,ok){const b=document.getElementById("de-badge");if(!b)return;const alt=b.textContent,ac=b.className;
    b.textContent=txt;b.className="badge"+(ok?"":" off");setTimeout(()=>{b.textContent=alt;b.className=ac;},1600);}
  // 💾 Speichern: das komplette MODEL roh auf den Server (board-model.json) + lokal sichern.
  window.deSave=async function(){deriveMembership();saveLocal();
    try{const r=await fetch("/api/editor/board",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(MODEL)});
      if(!r.ok)throw 0;deFlash("💾 Board gespeichert",true);}
    catch(e){badge(false);deFlash("⚠ Nur lokal gesichert (SimHost offline)",false);}};
  // Boot: der aus C# gelesene Stand ist die WAHRHEIT; das gespeicherte Board (Server, sonst localStorage)
  //   liefert nur Layout, Entwürfe und im Editor geänderte, noch nicht geschriebene Elemente (mergeBoard).
  window.deBoot=async function(){
    const live=await loadLive();
    const code=live||(embedded&&(embedded.records||embedded.aggregate)?embedded:null);
    schluesselFuer(code);
    const gespeichert=(await loadBoard())||loadLocal();
    MODEL=mergeBoard(code,gespeichert);render();meldeMerge();
  };

  // ── MERGE Code ⇄ Board ──────────────────────────────────────────────────────────────────────
  // Jedes aus dem Code stammende Element trägt ausCode + _codeKey (Identität im Code) + _herkunft (Hash des
  //   Inhalts beim Einlesen). Beim nächsten Laden gilt je Element:
  //     • im Code, im Board unverändert (Hash = _herkunft)      → Code-Stand gewinnt, Layout (x/y) bleibt
  //     • im Code, im Board geändert (Hash ≠ _herkunft)          → Board-Stand bleibt, markiert „ungeschrieben"
  //     • im Board als Entwurf (ohne ausCode), nicht im Code     → bleibt (Entwurf)
  //     • im Board ausCode, aber nicht mehr im Code               → entfällt (im Code gelöscht)
  const MERGE_KEYS={records:r=>r.kind+"|"+r.namespace+"|"+r.name,enums:e=>e.namespace+"|"+e.name,aggregate:a=>a.namespace+"|"+a.name,
    decider:d=>d.aggregat+"|"+d.command,applier:a=>a.aggregat+"|"+a.event,sagas:x=>x.namespace+"|"+x.name,
    readModels:x=>x.name,stores:x=>x.name,projektionen:x=>x.name,reaktionen:x=>x.name,reader:x=>x.name,pipelines:x=>x.name,
    triggers:x=>x.msgName||x.name,frists:x=>x.name,dienste:x=>x.name,hostSettings:x=>x.name};
  const LAYOUT=new Set(["x","y","ausCode","ungeschrieben","codeSrc","leer","rumpf","schritte"]);
  function inhalt(o){return JSON.stringify(o,(k,v)=>(k.startsWith("_")||LAYOUT.has(k))?undefined:v);}
  function hash(t){let h=5381;for(let i=0;i<t.length;i++)h=((h<<5)+h+t.charCodeAt(i))>>>0;return h.toString(36);}
  function maxId(m){let mx=0;JSON.stringify(m||{}).replace(/"_id":"[a-z_]*?(\d+)"/g,(_,n)=>{mx=Math.max(mx,+n);return _;});return mx;}
  let MERGE_INFO={ungeschrieben:0,entwuerfe:0};
  function mergeBoard(code,saved){
    // Gespeichertes zuerst normalisieren, dann Id-Zähler HINTER alle vergebenen Ids setzen → keine Kollisionen.
    const alt=saved?normalize(saved):null;
    NID=Math.max(NID,maxId(alt)+1);
    const neu=normalize(code?JSON.parse(JSON.stringify(code)):null);
    MERGE_INFO={ungeschrieben:0,entwuerfe:0};
    for(const col in MERGE_KEYS){const key=MERGE_KEYS[col];
      (neu[col]||[]).forEach(x=>{x.ausCode=true;x._codeKey=key(x);x._herkunft=hash(inhalt(x));});}
    neu.transitions.forEach(t=>{t.ausCode=true;t._herkunft=hash(inhalt(t));});
    if(!alt)return neu;
    // Alt-Board (vor der Herkunfts-Marke): Elemente in Namespaces, die der CODE nicht kennt (z. B. früher mitgezogene
    //   Framework-Records), entfallen ohne Code-Pendant; Entwürfe in den Namespaces der Domäne bleiben.
    const altFormat=!JSON.stringify(alt).includes('"ausCode":true');
    const codeNs=new Set();for(const col in MERGE_KEYS)(neu[col]||[]).forEach(x=>{if(x.namespace)codeNs.add(x.namespace);});
    const codeNodesNeu=new Map(neu.codeNodes.map(c=>[c._id,c]));
    for(const col in MERGE_KEYS){const key=MERGE_KEYS[col];
      const liveByKey=new Map((neu[col]||[]).map(x=>[x._codeKey,x]));
      const erg=[];const benutzt=new Set();
      (alt[col]||[]).forEach(s=>{
        const k=s.ausCode?s._codeKey:key(s);const l=liveByKey.get(k);
        if(l){benutzt.add(k);
          const geaendert=s.ausCode&&s._herkunft&&hash(inhalt(s))!==s._herkunft&&inhalt(s)!==inhalt(l);
          if(geaendert){s.ungeschrieben=true;s._codeKey=l._codeKey;
            // Rumpf-Quelle bleibt die echte Datei (der Code-Knoten des Code-Stands).
            if(l.codeSrc)s.codeSrc=l.codeSrc;else delete s.codeSrc;if(l.leer)s.leer=true;else delete s.leer;
            erg.push(s);MERGE_INFO.ungeschrieben++;}
          else{if(s.x!=null){l.x=s.x;l.y=s.y;}erg.push(l);}}
        else if(!s.ausCode&&!(altFormat&&s.namespace&&!codeNs.has(s.namespace))){erg.push(s);MERGE_INFO.entwuerfe++;}      // Entwurf: bleibt
        // else: ausCode, aber im Code verschwunden → entfällt
      });
      (neu[col]||[]).forEach(l=>{if(!benutzt.has(l._codeKey))erg.push(l);});
      neu[col]=erg;}
    // Abgeleitete Knoten: State-Knoten + Code-Knoten (Layout je Besitzer), Prompt-Knoten (Ziel umhängen).
    const altStates=new Map((alt.states||[]).map(x=>[x.aggregat,x]));
    neu.states.forEach(x=>{const a=altStates.get(x.aggregat);if(a&&a.x!=null){x.x=a.x;x.y=a.y;}});
    const altCode=new Map((alt.codeNodes||[]).map(c=>[c._id,c]));
    const ownerKey=(n,col)=>col+"|"+MERGE_KEYS[col](n);
    const codeUmzug=new Map();   // alte Code-Knoten-Id → neue (über den Besitzer)
    ["decider","applier"].forEach(col=>{const altBy=new Map((alt[col]||[]).map(n=>[ownerKey(n,col),n]));
      neu[col].forEach(n=>{const a=altBy.get(ownerKey(n,col));if(!a||!a.codeSrc||!n.codeSrc)return;
        const ac=altCode.get(a.codeSrc),nc=codeNodesNeu.get(n.codeSrc);if(ac&&nc){if(ac.x!=null){nc.x=ac.x;nc.y=ac.y;}codeUmzug.set(ac._id,nc._id);}});});
    // Übrige Code-Knoten (Leseseite, Store-Fns, Pipelines): Layout über (Name, Rumpf-Text) — sonst lägen sie alle auf
    //   der Normalize-Startposition, das Board überlappte grob und packLayout würfelte die Handanordnung neu.
    const altPos=new Map();(alt.codeNodes||[]).forEach(c=>{if(c.x!=null)altPos.set((c.name||"")+"\u0000"+(c.text||""),c);});
    neu.codeNodes.forEach(c=>{if([...codeUmzug.values()].includes(c._id))return;const a=altPos.get((c.name||"")+"\u0000"+(c.text||""));
      if(a){c.x=a.x;c.y=a.y;}});
    // Code-Knoten der Entwürfe/ungeschriebenen Leseseite mitnehmen (sonst hingen deren codeSrc ins Leere).
    const referenziert=new Set();JSON.stringify(neu,(k,v)=>{if(k==="codeSrc"&&v)referenziert.add(v);return v;});
    (alt.codeNodes||[]).forEach(c=>{if(referenziert.has(c._id)&&!codeNodesNeu.has(c._id))neu.codeNodes.push(c);});
    neu.llmNodes=(alt.llmNodes||[]).map(l=>({...l,promptZiel:codeUmzug.get(l.promptZiel)||l.promptZiel}));
    // Transitionen (Saga-Regeln): unverändert → Code-Stand; im Board geändert/ergänzt → Board-Stand je Prozess.
    const altTr=(alt.transitions||[]);
    const proProzess=p=>altTr.filter(t=>t.prozess===p);
    neu.sagas.forEach(sg=>{const at=proProzess(sg.name);if(!at.length)return;
      const lt=neu.transitions.filter(t=>t.prozess===sg.name);
      const edit=at.some(t=>(!t.ausCode&&!altFormat)||(t._herkunft&&hash(inhalt(t))!==t._herkunft));
      if(edit){neu.transitions=neu.transitions.filter(t=>t.prozess!==sg.name).concat(at.map(t=>({...t,ungeschrieben:true})));MERGE_INFO.ungeschrieben++;}
      else{const sig=t=>JSON.stringify([t.wenn||[],t.sammelEvent||"",(t.dann||[]).map(d=>[d.sende||"",d.kompensation||""])]);
        const altBySig=new Map(at.map(t=>[sig(t),t]));
        lt.forEach(t=>{const a=altBySig.get(sig(t));if(a&&a.x!=null){t.x=a.x;t.y=a.y;}});}});
    // Entwurfs-Transitionen neuer (Entwurfs-)Prozesse behalten.
    altTr.filter(t=>!neu.sagas.some(sg=>sg.name===t.prozess)&&neu.transitions.indexOf(t)<0).forEach(t=>neu.transitions.push(t));
    return neu;}
  // Node-Art (graphNodes().kind) → Modell-Sammlung (für die Entwurf-Markierung: nur Sammlungen, die aus dem Code kommen).
  function kollektionVon(kind){return {command:"records",event:"records",rejection:"records",valueobject:"records",konfig:"records",query:"records",queryresponse:"records",
    enum:"enums",aggregate:"aggregate",decider:"decider",applier:"applier",saga:"sagas",readmodel:"readModels",store:"stores",
    projektion:"projektionen",reaktion:"reaktionen",reader:"reader",pipeline:"pipelines",trigger:"triggers",frist:"frists",dienst:"dienste",hostsetting:"hostSettings"}[kind]||null;}
  function meldeMerge(){const u=MERGE_INFO.ungeschrieben,e=MERGE_INFO.entwuerfe;
    if(u||e)deFlash("↔ Code geladen · "+u+" ungeschrieben · "+e+" Entwurf/Entwürfe",true);}
  window.deDownload=function(){deriveMembership();prepareSaga();const blob=new Blob([JSON.stringify(MODEL,null,2)],{type:"application/json"});
    const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download="domain-model.json";a.click();};
  // Server-Payload: das MODEL + die Rümpfe (Code-Knoten-Text bzw. "" für bewusst leer) zurück an Decider/Applier.
  function payload(){deriveMembership();prepareSaga();const m=JSON.parse(JSON.stringify(MODEL));
    const txt=id=>{const c=m.codeNodes.find(x=>x._id===id);return c?c.text:null;};
    [...m.decider,...m.applier].forEach(n=>{if(n.leer)n.rumpf="";else if(n.codeSrc){const t=txt(n.codeSrc);if(t!=null)n.rumpf=t;}});
    return m;}
  async function post(path){const r=await fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(payload())});
    if(!r.ok)throw new Error("HTTP "+r.status);return r;}
  // &lt;/&gt; C# schreiben: das Modell in die ECHTEN .cs-Dateien schreiben (chirurgisch/additiv über
  //   /api/editor/write). Neue Records/Methoden werden angehängt, Handcode nie überschrieben.
  //   Danach spiegelt der Code-Sync die Datei-Rümpfe zurück ins Board.
  window.deWrite=async function(){
    try{const r=await post("/api/editor/write");const b=await r.json();badge(true);
      const neu=(b.geschrieben||[]).length, erg=(b.ergaenzt||[]).length, ueb=(b.uebersprungen||[]).length;
      if(!neu&&!erg){deFlash("✓ Dateien aktuell — nichts zu schreiben ("+ueb+" unverändert)",true);}
      else{deFlash("✅ geschrieben: "+neu+" neu · "+erg+" ergänzt · "+ueb+" unverändert",true);}
      console.log("C# schreiben:",b);
      if(neu||erg)await deReload();   // Code → Board: Geschriebenes wird Code-Stand (nicht mehr „ungeschrieben")
    }catch(e){badge(false);deFlash("⚠ SimHost offline — nicht geschrieben (dotnet run --project SimHost)",false);}};
  window.deValidate=async function(){const out=document.getElementById("de-out");
    try{const r=await post("/api/editor/validate");const fs=(await r.json()).concat(MODEL.diagnosen||[]);badge(true);
      if(!fs.length){out.innerHTML='<div class="find" style="background:#1f3a2a;color:#9be3bf">✓ Keine Befunde — die Form ist stimmig.</div>';return;}
      out.innerHTML="";fs.forEach(f=>out.append(h("div",{class:"find "+f.schweregrad},"["+f.code+"] "+f.meldung)));
    }catch(e){badge(false);out.innerHTML='<div class="find error">SimHost nicht erreichbar. Starte ihn: <b>dotnet run --project SimHost</b>.</div>';}};

  // Ausgabe-Panel: sobald Prüfen/Kompilieren/Testen hineinschreiben, einblenden (mit ✕ zum Schließen).
  (function(){const out=document.getElementById("de-out");if(!out||!window.MutationObserver)return;
    new MutationObserver(()=>{if(out.querySelector(".outzu"))return;out.classList.add("zeigen");
      const zu=h("button",{class:"outzu",title:"Ausgabe schließen",onclick:()=>out.classList.remove("zeigen")},"✕");out.prepend(zu);})
      .observe(out,{childList:true});})();
  const unreach='<div class="find error">SimHost nicht erreichbar. Starte ihn: <b>dotnet run --project SimHost</b>.</div>';
  async function postJson(path,obj){deriveMembership();prepareSaga();const r=await fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(obj)});if(!r.ok)throw new Error("HTTP "+r.status);return r;}
  const alleCommands=()=>MODEL.records.filter(r=>r.kind==="command");

  // ⚙ Kompilieren: das Modell IN-MEMORY mit den echten Domain-Generatoren übersetzen.
  window.deCompile=async function(){const out=document.getElementById("de-out");
    try{const r=await post("/api/editor/compile");const res=await r.json();badge(true);out.innerHTML="";
      if(res.ok){out.append(h("div",{class:"find",style:"background:#1f3a2a;color:#9be3bf"},"✓ Kompiliert (in-memory, echte Generatoren): "+res.aggregate+" Aggregate, "+res.sagas+" Sagas. Keine Fehler — bereit zum Testen."));}
      else{out.append(h("div",{class:"find warning"},res.fehler.length+" Compiler-Fehler — zurück in die Körper (Self-Repair-Schleife):"));
        res.fehler.forEach(f=>out.append(h("div",{class:"find error"},"["+f.code+"] "+(f.datei?f.datei+": ":"")+f.meldung)));}
    }catch(e){badge(false);out.innerHTML=unreach;}};

  // ══ SIMULATION (die EINE Laufzeit): Command mit Werten → Kaskade über das In-Memory-Kompilat des MODELLS ══
  //   (echte Generatoren + SagaLaufwerk). Die Kaskade läuft Knoten für Knoten über das Board:
  //   (Saga-Regel →) Command → Decider/Aggregat → Events/Ablehnungen → Applier/State. Ändert man das Modell,
  //   spielt der Server die bisherige Geschichte gegen die neue Logik nach (Hot-Reload).
  const SIM={an:false,sid:"editor-"+Math.random().toString(36).slice(2,10),frames:[],instanzen:[],sagas:[],abd:new Set(),
    abdAn:false,folgen:true,tempo:420,laeuft:false,cmd:null,werte:{},hinweis:null,fehler:[],cmdSig:""};
  const uuid=()=>crypto.randomUUID?crypto.randomUUID():"xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx".replace(/x/g,()=>(Math.random()*16|0).toString(16));
  const simCommands=()=>MODEL.records.filter(r=>r.kind==="command");
  const simZiel=c=>{const d=MODEL.decider.find(x=>x.command===c);return d?d.aggregat:null;};
  window.deSimSession=()=>SIM.sid;
  window.deSim=function(){SIM.an=!SIM.an;document.getElementById("de").classList.toggle("simon",SIM.an);
    document.getElementById("de-simbtn").textContent=SIM.an?"■ Simulation":"▶ Simulation";
    if(SIM.an){simPanel();simStand();}else simLeeren(true);render();};

  function simPanel(){const box=document.getElementById("de-sim");if(!box)return;box.innerHTML="";
    const kopf=h("div",{class:"row"},
      h("button",{class:"act",title:"Session verwerfen (alle Instanzen/Sagas)",onclick:simReset},"↺ Reset"),
      h("button",{class:"act",title:"Letztes Command als Regressionstest (Test-DSL)",onclick:simDsl},"📋 Als Test"),
      h("label",{class:"cbx",title:"Welche Zweige wurden in dieser Session gefahren?"},h("input",{type:"checkbox",...(SIM.abdAn?{checked:"checked"}:{}),onchange:e=>{SIM.abdAn=e.target.checked;simOverlay();}})," Abdeckung"),
      h("label",{class:"cbx",title:"Kamera folgt der Animation"},h("input",{type:"checkbox",...(SIM.folgen?{checked:"checked"}:{}),onchange:e=>SIM.folgen=e.target.checked})," Folgen"));
    const tempo=h("select",{title:"Animations-Tempo",onchange:e=>SIM.tempo=+e.target.value});
    [["700","langsam"],["420","normal"],["150","schnell"],["0","sofort"]].forEach(([v,l])=>{const o=h("option",{value:v},l);if(+v===SIM.tempo)o.selected=true;tempo.append(o);});
    kopf.append(tempo);
    box.append(kopf,h("h4",{},"Command senden"),h("div",{id:"sim-form"}),h("div",{id:"sim-meld"}),
      h("h4",{},"Verlauf"),h("div",{id:"sim-verlauf"}),h("h4",{},"Instanzen"),h("div",{id:"sim-inst"}),
      h("h4",{},"Laufende Sagas"),h("div",{id:"sim-sagas"}));
    simForm();simListen();}

  // ── Formular: Command wählen (nach Ziel-Aggregat gruppiert) + Felder typgerecht ──
  function simForm(){const box=document.getElementById("sim-form");if(!box)return;box.innerHTML="";
    const cmds=simCommands();SIM.cmdSig=cmds.map(c=>c.name+":"+(c.felder||[]).map(f=>f.name+"/"+f.typ).join(",")).join("|");
    if(!cmds.length){box.append(h("div",{class:"hint"},"Kein Command im Modell."));return;}
    if(!SIM.cmd||!cmds.some(c=>c.name===SIM.cmd))SIM.cmd=cmds[0].name;
    const sel=h("select",{style:"width:100%",onchange:e=>{SIM.cmd=e.target.value;simForm();}});
    const gruppen={};cmds.forEach(c=>{const g=simZiel(c.name)||"(kein Decider)";(gruppen[g]=gruppen[g]||[]).push(c);});
    Object.keys(gruppen).sort().forEach(g=>{const og=h("optgroup",{label:g});
      gruppen[g].sort((a,b)=>a.name.localeCompare(b.name)).forEach(c=>{const o=h("option",{value:c.name},c.name+(c.istErzeugung?" ✚":""));if(c.name===SIM.cmd)o.selected=true;og.append(o);});sel.append(og);});
    box.append(sel);
    const c=cmds.find(x=>x.name===SIM.cmd);const w=SIM.werte[c.name]=SIM.werte[c.name]||{};const ziel=simZiel(c.name);
    (c.felder||[]).forEach(f=>box.append(h("div",{class:"fld"},h("label",{title:f.typ},f.name+" : "+f.typ),simInput(f,w,ziel))));
    box.append(h("div",{class:"row",style:"margin-top:6px"},
      h("button",{class:"act run",onclick:simSenden},"▶ Senden"),
      ziel?null:h("span",{class:"hint"},"⚠ kein Aggregat entscheidet dieses Command")));}

  const enumWerte=t=>{const e=MODEL.enums.find(x=>x.name===t);return e?(e.werte||[]).map((v,i)=>{const m=/^(\w+)\s*=\s*(-?\d+)/.exec(v);return m?{n:m[1],v:+m[2]}:{n:v.trim(),v:i};}):null;};
  function simInput(f,w,ziel){const typ=(f.typ||"").trim(),basis=typ.replace(/\?$/,""),nullbar=typ.endsWith("?");
    const set=v=>{w[f.name]=v;};
    if(basis==="Guid"){ // Instanz wählen (Ziel-Aggregat zuerst) oder neue Id
      const s=h("select",{onchange:e=>set(e.target.value==="__neu"?uuid():e.target.value)});
      const eigene=SIM.instanzen.filter(i=>f.name===ID_FELD()?i.aggregat===ziel:true);
      if(w[f.name]===undefined)w[f.name]=(f.name===ID_FELD()&&eigene.length&&!(simCommands().find(c=>c.name===SIM.cmd)||{}).istErzeugung)?eigene[eigene.length-1].id:uuid();
      const bekannt=eigene.some(i=>i.id===w[f.name]);
      s.append(h("option",{value:w[f.name]},bekannt?"":"neu · "+String(w[f.name]).slice(0,8)));
      eigene.forEach(i=>{const o=h("option",{value:i.id},i.label+" · "+i.id.slice(0,8));if(i.id===w[f.name])o.selected=true;s.append(o);});
      s.append(h("option",{value:"__neu"},"＋ neue Id"));if(bekannt)s.firstChild.remove();return s;}
    if(basis==="bool"){const i=h("input",{type:"checkbox",onchange:e=>set(e.target.checked)});if(w[f.name])i.checked=true;if(w[f.name]===undefined)set(false);return i;}
    if(/^(int|long|decimal|double|float|short)$/.test(basis)){if(w[f.name]===undefined)set(nullbar?null:0);
      return h("input",{type:"number",value:w[f.name]??"",oninput:e=>set(e.target.value===""?(nullbar?null:0):Number(e.target.value))});}
    const ew=enumWerte(basis);
    if(ew){const s=h("select",{onchange:e=>set(e.target.value===""?null:+e.target.value)});
      if(nullbar)s.append(h("option",{value:""},"null"));
      ew.forEach(x=>{const o=h("option",{value:x.v},x.n);if(w[f.name]===x.v)o.selected=true;s.append(o);});
      if(w[f.name]===undefined)set(nullbar?null:(ew[0]||{}).v);return s;}
    if(basis==="DateTimeOffset"||basis==="DateTime"){if(w[f.name]===undefined)set(nullbar?null:new Date().toISOString());
      return h("input",{value:w[f.name]??"",placeholder:"ISO-Zeit",oninput:e=>set(e.target.value===""&&nullbar?null:e.target.value)});}
    if(basis==="string"){if(w[f.name]===undefined)set(nullbar?null:"");
      return h("input",{value:w[f.name]??"",placeholder:nullbar?"null":"",oninput:e=>set(e.target.value===""&&nullbar?null:e.target.value)});}
    // Komplex (Value Object, Collection): JSON.
    if(w[f.name]===undefined)set(/^(List|IReadOnlyList|IEnumerable|HashSet|.*\[\])/.test(basis)?[]:null);
    const t=h("textarea",{rows:2,placeholder:"JSON",oninput:e=>{try{set(e.target.value.trim()===""?null:JSON.parse(e.target.value));t.style.borderColor="";}catch(_){t.style.borderColor="#ff6b81";}}});
    t.value=JSON.stringify(w[f.name]);return t;}

  // ── Senden → Server-Kaskade → Animation ──
  async function simSenden(){if(SIM.laeuft)return;const c=SIM.cmd;const werte=SIM.werte[c]||{};SIM.laeuft=true;
    try{const r=await postJson("/api/editor/sim/step",{model:payload(),sessionId:SIM.sid,command:c,values:werte});const res=await r.json();badge(true);
      SIM.fehler=res.ok?[]:(res.fehler||[]);SIM.hinweis=res.hinweis||null;
      // Bei Übersetzungs-/Wertefehlern bleibt der letzte gute Stand (Instanzen, Abdeckung) stehen.
      if(res.ok){SIM.instanzen=res.instanzen||[];SIM.abd=new Set(res.abdeckung||[]);SIM.sagas=res.sagas||[];const start=SIM.frames.length;res.frames.forEach(f=>SIM.frames.push(f));
        // Nach dem Anlegen: dieselbe Instanz für Folge-Commands vorwählen.
        simListen();simOverlay();await simAnimiere(res.frames);simForm();}
      else{simListen();simOverlay();}}
    catch(e){badge(false);SIM.fehler=[{code:"OFFLINE",meldung:"SimHost nicht erreichbar (dotnet run --project SimHost)."}];simListen();}
    finally{SIM.laeuft=false;}}
  async function simReset(){try{await postJson("/api/editor/sim/reset",{sessionId:SIM.sid});}catch(e){}
    SIM.frames=[];SIM.instanzen=[];SIM.sagas=[];SIM.abd=new Set();SIM.werte={};SIM.hinweis=null;SIM.fehler=[];simLeeren(true);simForm();simListen();simOverlay();}
  async function simStand(){try{const r=await fetch("/api/editor/sim/state?sessionId="+SIM.sid);const res=await r.json();
    SIM.instanzen=res.instanzen||[];SIM.abd=new Set(res.abdeckung||[]);simListen();simOverlay();}catch(e){}}
  async function simDsl(){const out=document.getElementById("de-out");
    try{const t=await (await fetch("/api/editor/sim/dsl?sessionId="+SIM.sid)).text();out.innerHTML="";
      out.append(h("div",{class:"sub"},"Regressionstest (Test-DSL) — in Infrastructure.Pruefstand.Tests einfügen"),h("pre",{style:"white-space:pre-wrap"},t));
      try{await navigator.clipboard.writeText(t);deFlash("📋 Test in die Zwischenablage kopiert",true);}catch(_){}}
    catch(e){out.innerHTML=unreach;}}

  // ── Listen: Meldungen, Verlauf (klickbar = erneut abspielen), Instanzen, Sagas ──
  function simListen(){
    const meld=document.getElementById("sim-meld");if(meld){meld.innerHTML="";
      if(SIM.hinweis)meld.append(h("div",{class:"hinweis"},"↻ "+SIM.hinweis));
      SIM.fehler.forEach(f=>meld.append(h("div",{class:"fehler"},"["+f.code+"] "+(f.datei?f.datei+": ":"")+f.meldung)));}
    const v=document.getElementById("sim-verlauf");if(v){v.innerHTML="";
      if(!SIM.frames.length)v.append(h("div",{class:"hint"},"Noch nichts gesendet."));
      SIM.frames.slice().reverse().forEach(f=>{const rej=f.events.length&&f.events.every(e=>!e.persistent);
        const el=h("div",{class:"frame"+(f.herkunft==="saga"?" saga":"")+(rej||f.unrouted?" rej":""),title:"erneut abspielen",onclick:()=>simAnimiere([f])},
          h("b",{},(f.herkunft==="saga"?"⤷ "+f.saga+": ":"")+f.command),h("span",{style:"opacity:.6"}," → "+f.label+(f.werte?" ("+f.werte+")":"")));
        if(f.unrouted)el.append(h("span",{class:"ev rej"},"⚠ von keinem Aggregat behandelt (im Cluster ein Hang)"));
        f.events.forEach(e=>el.append(h("span",{class:"ev"+(e.persistent?"":" rej")},(e.persistent?"● ":"✗ ")+e.typ+(e.werte?" ("+e.werte+")":""),
          e.warum?h("span",{class:"warum"}," weil "+e.warum):null)));
        v.append(el);});}
    const ib=document.getElementById("sim-inst");if(ib){ib.innerHTML="";
      if(!SIM.instanzen.length)ib.append(h("div",{class:"hint"},"Keine Instanz angelegt."));
      SIM.instanzen.forEach(i=>{const el=h("div",{class:"inst",title:"als Ziel wählen",onclick:()=>{const c=simCommands().find(x=>x.name===SIM.cmd);
          if(c&&simZiel(c.name)===i.aggregat){(SIM.werte[c.name]=SIM.werte[c.name]||{})[ID_FELD()]=i.id;simForm();}
          const n=graphNodes().find(x=>x.id==="agg:"+i.aggregat);if(n){centerOn(n);pulseNode(n);}}},h("b",{},i.label),h("span",{style:"opacity:.5"}," "+i.id.slice(0,8)));
        i.felder.forEach(f=>el.append(h("span",{class:"f"+(f.geaendert?" neu":"")},f.name+" = "+f.wert)));ib.append(el);});}
    const sb=document.getElementById("sim-sagas");if(sb){sb.innerHTML="";
      const offen=SIM.sagas.filter(x=>x.wartend.length);
      if(!offen.length)sb.append(h("div",{class:"hint"},SIM.sagas.length?"Alle Saga-Instanzen abgeschlossen.":"Keine Saga gestartet."));
      offen.forEach(x=>{const el=h("div",{class:"inst"},h("b",{},x.prozess),h("span",{style:"opacity:.5"}," "+x.korrelation.slice(0,8)));
        el.append(h("span",{class:"f"},"angekommen: "+(x.angekommen.join(", ")||"—")));
        x.wartend.forEach(w=>el.append(h("span",{class:"f neu"},"wartet: "+w.bedingung.join(" ∧ ")+(w.fehlt.length?" — fehlt "+w.fehlt.join(", "):""))));sb.append(el);});}}

  // ── Animation: Frame → Stufen von Knoten-Ids ──
  const knotenId={
    rec:n=>MODEL.records.some(r=>r.name===n)?"rec:"+n:null,
    dec:(agg,cmd)=>{const d=MODEL.decider.find(x=>x.aggregat===agg&&x.command===cmd)||MODEL.decider.find(x=>x.command===cmd);return d?"dec:"+d._id:null;},
    app:(agg,evt)=>{const a=MODEL.applier.find(x=>x.aggregat===agg&&x.event===evt);return a?"app:"+a._id:null;},
    state:agg=>{const s=MODEL.states.find(x=>x.aggregat===agg);return s?"st:"+s._id:null;},
    regel:(saga,ri)=>{const sg=MODEL.sagas.find(x=>x.name===saga);if(!sg||ri==null)return null;
      const liste=transOf(sg).filter(t=>(t.wenn||[]).length).flatMap(t=>(t.dann||[]).filter(d=>d.sende).map(()=>t));const t=liste[ri];return t?"tr:"+t._id:null;}};
  function simStufen(f){const st=[];const rej=new Set();
    if(f.herkunft==="saga")st.push(["saga:"+f.saga,knotenId.regel(f.saga,f.regel)]);
    st.push([knotenId.rec(f.command)]);if(f.unrouted){rej.add(knotenId.rec(f.command));return {st,rej};}
    st.push([knotenId.dec(f.aggregat,f.command),"agg:"+f.aggregat]);
    st.push(f.events.map(e=>{const id=knotenId.rec(e.typ);if(!e.persistent&&id)rej.add(id);return id;}));
    const pers=f.events.filter(e=>e.persistent);
    if(pers.length)st.push([...pers.map(e=>knotenId.app(f.aggregat,e.typ)),knotenId.state(f.aggregat)]);
    return {st:st.map(x=>x.filter(Boolean)).filter(x=>x.length),rej};}
  const simEl=id=>world&&world.querySelector('[data-id="'+vertreterId(id)+'"]');   // eingeklappte Details → Besitzer
  function simLeeren(ganz){if(!world)return;world.querySelectorAll(".gnode2.simhot").forEach(e=>e.classList.remove("simhot"));
    if(ganz)world.querySelectorAll(".gnode2.simspur,.gnode2.simrej").forEach(e=>e.classList.remove("simspur","simrej"));}
  const warte=ms=>new Promise(r=>setTimeout(r,ms));
  async function simAnimiere(frames){simLeeren(true);
    for(const f of frames){const {st,rej}=simStufen(f);
      for(const ids of st){simLeeren(false);
        ids.forEach(id=>{const el=simEl(id);if(!el)return;el.classList.add("simhot","simspur");if(rej.has(id))el.classList.add("simrej");});
        if(SIM.folgen&&SIM.tempo>0){const n=graphNodes().find(x=>x.id===ids[0]);if(n)centerOn(n);}
        if(SIM.tempo>0)await warte(SIM.tempo);}}
    simLeeren(false);}

  // ── Abdeckung: welche Zweige diese Session gefahren hat (Decider: Ausgänge x/y, Applier, Regeln) ──
  function simOverlay(){if(!world)return;
    world.querySelectorAll(".gnode2").forEach(el=>{el.classList.remove("abd-voll","abd-teil","abd-kalt");const b=el.querySelector(".simabd");if(b)b.remove();});
    if(!SIM.an||!SIM.abdAn)return;
    const mark=(id,klasse,text)=>{const el=simEl(id);if(!el)return;el.classList.add(klasse);
      if(text){const hd=el.querySelector(".ghead");if(hd)hd.append(h("span",{class:"simabd"},text));}};
    MODEL.decider.forEach(d=>{const aus=d.ergibt||[];const n=aus.filter(o=>SIM.abd.has("out:"+d.command+">"+o.event)).length;
      mark("dec:"+d._id,n===0?"abd-kalt":(n===aus.length?"abd-voll":"abd-teil"),n+"/"+aus.length+" Zweige");});
    MODEL.applier.forEach(a=>mark("app:"+a._id,SIM.abd.has("app:"+a.aggregat+"|"+a.event)?"abd-voll":"abd-kalt"));
    MODEL.sagas.forEach(sg=>{const liste=transOf(sg).filter(t=>(t.wenn||[]).length).flatMap(t=>(t.dann||[]).filter(d=>d.sende).map(()=>t));
      liste.forEach((t,ri)=>mark("tr:"+t._id,SIM.abd.has("regel:"+sg.name+"#"+ri)?"abd-voll":"abd-kalt"));});}

  // Nach jedem Board-Render: Overlay neu anbringen; Formular nur neu bauen, wenn sich die Commands geändert haben.
  window.simNachRender=function(){if(!SIM.an)return;simOverlay();
    const sig=simCommands().map(c=>c.name+":"+(c.felder||[]).map(f=>f.name+"/"+f.typ).join(",")).join("|");if(sig!==SIM.cmdSig)simForm();};
})();
</script>
""";
}
