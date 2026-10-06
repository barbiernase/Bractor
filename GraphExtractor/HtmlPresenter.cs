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
/* Render-Architektur (Zoom/Pan): die Welt ist KEINE Dauer-GPU-Ebene. will-change nur während eines Zieh-Pans (reines
   Verschieben → Raster wiederverwendbar); danach rastert der Browser in der echten Zoomstufe neu. Dauerhaft gesetzt
   rasterte er die ganze Welt (bis ~7000×11000 px) in der Start-Zoomstufe → beim Rauszoomen Kachel-Budget gesprengt =
   Knoten laden nicht nach / flackern / verschwinden (auch Inspector + Minimap, die sich das GPU-Budget teilen).
   Größe = Inhalt (messeWelt), nicht fest 6000×4000 — sonst liegen Knoten/Kanten außerhalb der Ebenen-Grenzen. */
#de .gworld{position:absolute;left:0;top:0;width:6000px;height:4000px;transform-origin:0 0}
#de .gcanvas.bewegt .gworld{will-change:transform}
/* Außerhalb des Sichtfensters (+ Rand): nicht malen (Layout bleibt → Maße/Anker/Kanten stimmen weiter). */
#de .gnode2.weg{visibility:hidden}
/* Weit rausgezoomt: die teuren weichen Schatten weglassen (unsichtbar klein, aber das Teuerste beim Rastern). */
#de .gcanvas.fern{--gsch:none}
#de svg.gedges{position:absolute;left:0;top:0;width:6000px;height:4000px;pointer-events:none;overflow:visible}
#de .glink{pointer-events:none}
#de .glink.internal{stroke:#7f8aa0;stroke-width:1.5;opacity:.4;pointer-events:stroke;transition:opacity .1s,stroke-width .1s}
#de .glink.internal:hover,#de .glink.internal.hot{stroke:#cbb8ff;stroke-width:3;opacity:1}
/* Minimap (klickbar, zeigt Viewport) + Domänen-Filter */
#de .gminimap{position:absolute;right:10px;bottom:10px;width:212px;height:150px;background:#0b0e15cc;border:1px solid #2c3547;border-radius:8px;overflow:hidden;z-index:20;cursor:pointer;box-shadow:0 6px 20px #0009;contain:strict;transform:translateZ(0)}
#de .gminimap svg{display:block;width:100%;height:100%}
#de .gminimap .mmvp{fill:#7fb0e61f;stroke:#8fc0ff;stroke-width:1.5}
#de .gfilter{position:absolute;left:10px;top:10px;width:216px;max-height:calc(100% - 20px);overflow:auto;background:#0d1119f2;border:1px solid #2c3547;border-radius:8px;z-index:22;padding:9px;font-size:12px;box-shadow:0 10px 28px #000b;contain:layout paint;transform:translateZ(0)}
#de .gfilter h4{margin:0 0 8px;font-size:12px;color:#cbd3e1;display:flex;justify-content:space-between;align-items:center}
#de .gfilter label{display:flex;align-items:center;gap:6px;padding:3px 3px;color:#aab3c5;cursor:pointer;border-radius:4px}
#de .gfilter label:hover{background:#1a2130}
#de .gfilter .mm-q{display:flex;gap:6px;margin-bottom:7px}
#de .gfilter .mm-q button{flex:1;font-size:11px;padding:4px;background:#1a2130;color:#cbd3e1;border:1px solid #2c3547;border-radius:5px;cursor:pointer}
#de .gfilter .mm-q button:hover{background:#222c3d}
/* Code-Knoten: Vorschau im Knoten + anklickbares Modal mit vollem Code */
#de .gcodeprev{margin:4px 0;padding:7px 9px;background:#0d1119;border:1px solid #263041;border-radius:6px;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:11px;line-height:1.45;color:#c7d0df;white-space:pre;overflow:hidden;max-height:130px;cursor:default}
#de .gnode2{position:absolute;width:250px;background:#161b27;border:1px solid #2c3547;border-radius:9px;box-shadow:var(--gsch,0 4px 14px #0008)}
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
#de .gnode2.n-akteur{border-color:#b04f86;width:240px}
#de .gnode2.n-client{border-color:#5aa0c8;width:420px}
#de .gnode2.n-client .ghead{background:#5aa0c8}
#de .gnode2.n-client .gsum.gleiste{display:block;font:11px ui-monospace,monospace;color:#c4cde0;padding:6px 10px 9px}
#de .gleiste .lz{white-space:normal;line-height:1.55}
#de .gleiste .lk{color:#8fb7d6;margin-right:6px;white-space:nowrap}
#de .gleiste .lz.lt .lk{color:#e7a3cb}
#de .gleiste .lz.lwarn .lk{color:#e0b46a}
#de .gleiste .lz.lleer{color:#7f8aa0}
#de .gleiste .lp{cursor:pointer;color:#e6ebf5;border-bottom:1px dotted #5a6a85}
#de .gleiste .lp:hover,#de .gleiste .lp.an{color:#fff;background:#2c3d5c;border-bottom-color:#9cc3ff}
#de .gleiste .lp.lp-fehlt{color:#e0b46a;cursor:default}
#de .grahmen.client{border-style:dashed;border-width:calc(2px * var(--lz));background:color-mix(in srgb,var(--dc) 6%,#0b0f17)}
#de .grahmen.client>.grahmen-k .akt-n{cursor:pointer}
#de .grahmen-k .cl-stecker{display:flex;align-items:center;gap:3px;font-size:11px;color:#8fb7d6;white-space:nowrap;overflow:hidden;min-width:0}
#de .grahmen-k .cl-stecker .akt-ref{padding:0 6px;font-size:11px;background:transparent;border-color:#5aa0c8;color:#cfe6ff}
#de .glink.gbuendel{stroke-width:7px;opacity:.55;pointer-events:stroke;cursor:pointer}
#de .glink.gbuendel:hover{opacity:.95}
#de .glink.gbuendel-t{fill:#cfe6ff;font:12px ui-monospace,monospace;stroke:#0b0f17;stroke-width:3px;paint-order:stroke;pointer-events:none}
#de .glink.gauf{stroke-width:2.5px}
#de .gnode2.n-auf{border-color:#b04f86;border-style:dashed;width:240px}
#de .gnode2.n-event{border-color:#3f9d5a}
#de .gnode2.n-rejection{border-color:#b5504a}
#de .gnode2.n-valueobject{border-color:#2f8f7d}
#de .gnode2.n-enum{border-color:#6a6a86}
#de .gnode2.n-aggregate{border-color:#2f9d95;width:274px}
#de .gnode2.n-decider{border-color:#7a5cc0;width:258px}
#de .gnode2.n-applier{border-color:#c08a3e;width:258px}
#de .gnode2.n-saga{border-color:#8a5cc0;width:300px}
#de .gnode2.n-transition{border-color:#8a6fc8;width:276px}
#de .gnode2.n-funktion{border-color:#c08a2e;width:290px}
#de .gnode2.n-auftrag{border-color:#c08a2e}
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
#de .gnode2.n-akteur .ghead{background:#e07ab4}
#de .gnode2.n-auf .ghead{background:#eaa0c9}
#de .gnode2.n-event .ghead{background:#57b673}
#de .gnode2.n-rejection .ghead{background:#cf6f68}
#de .gnode2.n-valueobject .ghead{background:#49a996}
#de .gnode2.n-konfig .ghead{background:#8fa3b8}
#de .gnode2.n-enum .ghead{background:#8a8aa0}
#de .gnode2.n-aggregate .ghead{background:#3fb0a6}
#de .gnode2.n-decider .ghead{background:#9678d6}
#de .gnode2.n-handle{border-color:#4f7fa8}#de .gnode2.n-handle .ghead{background:#5d8fbf}
#de .gnode2.n-fn{border-color:#2f8f88}#de .gnode2.n-fn .ghead{background:#2f9d95}
#de .ghl{display:block;cursor:pointer;color:#9fb6d6;font-size:11.5px;padding:1px 0;text-decoration:none}#de .ghl:hover{color:#dbe6f5;text-decoration:underline}
#de .gaus{font-size:10.5px;color:#8f9bb0;margin:-2px 0 4px 18px;text-align:right;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
#de .gaus.tot{color:#d08a6a}#de .gaus.offen{color:#d0b35a}
/* Nachricht mit zwei Seiten (§12): ◀ kommt aus · geht an ▶ — Partner-Zeilen, „↗ außerhalb" = Domäne nicht geladen */
#de .gp-row{display:flex;align-items:center;gap:6px;font-size:11.5px;color:#c9d4e4;padding:2px 2px 2px 6px;border-radius:5px;cursor:pointer}
#de .gp-row:hover{background:#ffffff10}#de .gp-row .rm{margin-left:auto}
#de .gp-t{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}#de .gp-abg{color:#8f9bb0;font-style:italic}
#de .gp-mark{font-size:10px;color:#d0b35a;white-space:nowrap}#de .gp-welt{font-style:normal;color:#8f9bb0;flex:none}
#de .gp-aussen{opacity:.75;border:1px dashed #6b7690}#de .gp-leer{color:#8f9bb0;cursor:default}
#de .gmodus{font-size:10.5px;padding:0 2px;max-width:92px}
#de .gsig{font-family:ui-monospace,Menlo,monospace;font-size:10.5px;color:#b9c4d8;margin:2px 0 6px;word-break:break-all}
#de .gnode2.n-applier .ghead{background:#d0a35a}
#de .gnode2.n-saga .ghead{background:#9d78d6}
#de .gnode2.n-transition .ghead{background:#a48fd6}
#de .gnode2.n-funktion .ghead{background:#c08a2e}
#de .gnode2.n-auftrag .ghead{background:#d6a85a}
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
#de .slot.s-darf{background:#e07ab4}#de .slot.s-auftrag{background:#b05fd0}
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
/* Domänen-Rahmen: visuelle Blöcke um das Layout, hierarchisch (Unterdomäne im Eltern-Rahmen); nur der Kopf ist klickbar. */
#de .grahmen-ebene{position:absolute;left:0;top:0;width:0;height:0;--rz:1;--lz:1}
#de .gcanvas.lod-karte .grahmen-ebene{display:none}
#de .gworld.fokus .grahmen-ebene{opacity:.5}
#de .grahmen{position:absolute;box-sizing:border-box;border:calc(1.5px * var(--lz)) solid var(--dc);border-radius:14px;background:color-mix(in srgb,var(--dc) 7%,transparent);pointer-events:none}
#de .grahmen.unter{border-style:dashed}
#de .grahmen.mitakt{border-width:calc(2.5px * var(--lz));border-radius:18px;background:color-mix(in srgb,var(--dc) 10%,#0b0f17)}
#de .grahmen.mitakt>.grahmen-k{background:color-mix(in srgb,var(--dc) 22%,#0b0f17);border-bottom:1px solid color-mix(in srgb,var(--dc) 60%,transparent);border-radius:16px 16px 0 0}
#de .grahmen.mitakt>.grahmen-k .gr-t{font-size:calc(18px * var(--rz))}
#de .grahmen.akt{border-radius:12px;border-width:calc(1.5px * var(--lz));background:color-mix(in srgb,var(--dc) 7%,#0d121c)}
#de .grahmen.akt>.grahmen-k{height:calc(32px * var(--rz))}
#de .grahmen.akt.kette{border-style:dashed}
#de .grahmen.akt.ohne{background:color-mix(in srgb,#d0584f 5%,transparent)}
#de .grahmen.akt.leer{background:transparent;border-style:dotted}
#de .grahmen-k.akt-k .akt-n{cursor:pointer;font-size:calc(14px * var(--rz))}
#de .grahmen-k.akt-k .akt-n:hover{text-decoration:underline}
#de .grahmen-k .akt-auch{display:flex;align-items:center;gap:4px;font-size:11px;color:#9aa3b7;white-space:nowrap;overflow:hidden;min-width:0}
#de .grahmen-k .akt-auch .akt-ref{padding:0 6px;font-size:11px;background:transparent;border-style:dashed}
#de button.akt-dom{background:#1d2638;color:#d6e4ff;border:1px solid #35507a;border-radius:6px;padding:1px 8px;cursor:pointer;font:12px system-ui}
#de .grahmen-k{position:absolute;left:0;right:0;top:0;height:calc(38px * var(--rz));transform-origin:0 0;display:flex;align-items:center;gap:8px;padding:0 10px 0 14px;pointer-events:auto;cursor:default;
  background:linear-gradient(90deg,color-mix(in srgb,var(--dc) 24%,transparent),transparent 75%);border-radius:12px 12px 0 0;border-bottom:1px solid color-mix(in srgb,var(--dc) 30%,transparent)}
#de .grahmen-k .gr-t{font:700 calc(16px * var(--rz)) ui-monospace,monospace;color:#e6ebf5;white-space:nowrap}
#de .grahmen-k .gr-p{font-size:11px;color:#7f8aa0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0}
#de .grahmen-k .gr-z{font-size:11px;color:#9aa3b7;margin-left:auto;white-space:nowrap}
#de .grahmen-k button{background:#1d2638;color:#d6e4ff;border:1px solid #35507a;border-radius:6px;padding:2px 8px;cursor:pointer;font:12px system-ui;flex:none}
#de .grahmen-k button:hover{background:#2c3d5c}
#de .gr-leer{position:absolute;left:24px;top:62px;color:#6b7690;font-size:13px;font-style:italic}
/* Spalten-Rahmen: die senkrechten Rollen-Spalten innerhalb einer Domäne; nur der Kopf ist klickbar. */
#de .gspalte{position:absolute;box-sizing:border-box;border:calc(1px * var(--lz)) solid #ffffff22;border-radius:10px;background:#ffffff05;pointer-events:none}
#de .gspalte.insel{border-style:dashed;border-color:#e0844d66}
#de .gspalte.vertrag{border:calc(2px * var(--lz)) solid #e07ab4aa;background:#e07ab40d}
#de .gspalte.vertrag .gspalte-k{background:#e07ab42a;border-bottom-color:#e07ab455;cursor:pointer}
#de .gspalte.vertrag .gspalte-k .gs-t{color:#ffd3ea}
#de .gspalte-k{position:absolute;left:0;right:0;top:0;height:calc(28px * min(var(--rz),1.15));display:flex;align-items:center;gap:6px;padding:0 6px 0 9px;pointer-events:auto;
  background:#ffffff0a;border-bottom:1px solid #ffffff14;border-radius:9px 9px 0 0;overflow:hidden}
#de .gspalte-k i{width:8px;height:8px;border-radius:50%;flex:none;display:inline-block}
#de .gspalte-k .gs-t{font:600 calc(11.5px * var(--rz)) system-ui;color:#cbd3e1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;min-width:0}
#de .gspalte-k .gs-z{font-size:11px;color:#7f8aa0;margin-left:auto}
#de .gspalte-k button{background:#1d2638;color:#d6e4ff;border:1px solid #35507a;border-radius:5px;padding:0 6px;cursor:pointer;font:12px system-ui;line-height:18px;flex:none}
#de .gspalte-k button:hover{background:#2c3d5c}
#de .gmenue{z-index:45;max-height:min(70vh,560px);overflow:auto;min-width:220px}
#de .gmenue .gm-tr{height:1px;background:#2c3547;margin:3px 2px}
#de .gmenue button.gm-gefahr{background:#2a1b20;border-color:#6b3540;color:#ffc9cf}
#de .gmenue button.gm-gefahr:hover{background:#3a2028}
#de .gmenue button:disabled{opacity:.45;cursor:not-allowed}
#de .gmenue input{background:#0d1119;color:#e6ebf5;border:1px solid #35507a;border-radius:5px;padding:6px 8px;font:13px ui-monospace,monospace}
#de .gmenue .gm-hint{font-size:11px;color:#7f8aa0;padding:2px}
#de .gmenue .gm-fehler{font-size:11px;color:#ff9aa5;padding:0 2px;min-height:0}
#de .gmenue .gm-fehler:empty{display:none}
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
#de .gworld{--inv:4}
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
/* Start-Dialog (Blank-Start): Domänen laden */
#de .gstart{position:absolute;left:50%;top:40px;transform:translateX(-50%);z-index:40;width:min(560px,92%);max-height:80%;overflow:auto;
  background:#141a26;border:1px solid #8fa3c8;border-radius:12px;padding:16px 18px;box-shadow:0 10px 40px #000c;color:#cbd3e1;font:13px system-ui}
#de .gstart h3{margin:0 0 6px;font-size:17px;color:#fff}
#de .gstart-t{color:#9aa3b7;line-height:1.4;margin-bottom:10px}
#de .gstart-k{display:flex;gap:8px;margin:10px 0}
#de .gstart-l{display:flex;flex-direction:column;gap:2px;border-top:1px solid #2a3244;border-bottom:1px solid #2a3244;padding:8px 0}
#de .gstart-l label{display:flex;gap:8px;align-items:center;cursor:pointer;padding:3px 4px;border-radius:5px}
#de .gstart-l label:hover{background:#1c2434}
#de .gstart-n{flex:1;font-weight:600}#de .gstart-z{color:#6b7488;font-size:11.5px}
#de .gworld.vbmodus .gnode2.vb-gesperrt{opacity:.6!important;box-shadow:0 0 0 2px #b5504a;cursor:not-allowed}
#de .gworld.vbmodus .gnode2.vb-gesperrt .ghead::after{content:"✕ Regel";margin-left:auto;font:600 10px system-ui;color:#ffd0cc;padding-left:6px}
/* Verbinden-Modus (Hybrid): passende Knoten leuchten, Rest abgeblendet; ✓ = schon verbunden */
#de .gworld.vbmodus .gnode2{opacity:.14!important}
#de .gworld.vbmodus .glink{opacity:.05!important}
#de .gworld.vbmodus .gnode2.vb-kand,#de .gworld.vbmodus .gnode2.vb-quelle{opacity:1!important}
#de .gworld.vbmodus .gnode2.vb-kand{box-shadow:0 0 0 3px #3fae7f,0 0 18px #3fae7faa;cursor:pointer}
#de .gworld.vbmodus .gnode2.vb-kand:hover{box-shadow:0 0 0 4px #7ee0b0,0 0 24px #3fae7f}
#de .gworld.vbmodus .gnode2.vb-verb{box-shadow:0 0 0 3px #7c5cff,0 0 18px #7c5cffaa}
#de .gworld.vbmodus .gnode2.vb-verb .ghead::after{content:"✓ verbunden";margin-left:auto;font:600 10px system-ui;color:#d9ccff;padding-left:6px}
#de .gworld.vbmodus .gnode2.vb-quelle{box-shadow:0 0 0 3px #fff}
#de .gcanvas.vbaktiv .gminimap{display:none}
#de .ginsp .gi-hint{margin:6px 10px 0;padding:5px 8px;border-radius:6px;background:#16261e;border:1px solid #2f6b50;color:#9fe0bf;font:11px/1.35 system-ui}
#de .gvbwahl{position:absolute;z-index:31;display:flex;flex-direction:column;gap:3px;background:#15202c;border:1px solid #3fae7f;border-radius:8px;padding:6px;min-width:180px;box-shadow:0 6px 24px #000a}
#de .gvbwahl button{text-align:left}
/* Ports im Panel = ⊕-Knöpfe (auf der Fläche gibt es keine Ports mehr) */
#de .ginsp .slot{margin:0 8px 0 0!important;width:22px!important;height:22px!important;cursor:pointer;position:relative;flex:none;
  border:2px solid #3fae7f!important;background:#16261e!important;box-shadow:0 0 8px #3fae7f66}
#de .ginsp .slot::after{content:"⊕";position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font:700 15px/1 system-ui;color:#7ee0b0}
#de .ginsp .slot:hover{background:#3fae7f!important}#de .ginsp .slot:hover::after{color:#0b0e15}
#de .ginsp .slotrow.o{justify-content:flex-end}
#de .ginsp .slot.o{order:-1;margin:0 8px 0 0!important}
#de .ginsp .slot.vb-aktiv{box-shadow:0 0 0 3px #fff,0 0 0 6px #3fae7f}
#de .glink.kontrakt{opacity:.75}
/* Inspector: Bearbeiten rechts statt Formular im Knoten */
#de .ginsp{position:absolute;right:10px;top:10px;bottom:170px;width:min(380px,calc(100% - 40px));overflow:auto;background:#10141df7;border:1px solid #2c3547;border-radius:10px;z-index:25;box-shadow:0 10px 30px #000c;font-size:12px;contain:layout paint;transform:translateZ(0)}
#de .ginsp .gi-h{position:sticky;top:0;z-index:2;display:flex;align-items:center;gap:6px;padding:8px 10px;background:#161c2a;border-bottom:1px solid #2c3547}
#de .ginsp .gi-h .gi-k{font-size:10px;padding:1px 7px;border-radius:8px;color:#0d0f14;font-weight:700}
#de .ginsp .gi-h .gi-n{flex:1;font:700 13px ui-monospace,monospace;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#de .ginsp .gi-h button{background:#1a2130;color:#cbd3e1;border:1px solid #2c3547;border-radius:5px;cursor:pointer;padding:2px 7px;font-size:11px}
#de .ginsp .gi-sec{margin:10px 10px 0;border:1px solid #262f40;border-radius:8px;overflow:hidden}
#de .ginsp .gi-st{padding:4px 9px;font-size:10.5px;font-weight:700;color:#0d0f14;cursor:pointer}
#de .ginsp .gbody{padding:8px 10px 10px}
/* Ports im Panel sind die ⊕-Knöpfe des Verbinden-Modus (siehe oben) — nicht mehr ausgeblendet */
#de .ginsp .gcodeprev{max-height:none}
#de .gnode2.llmvor{box-shadow:0 0 0 2px #3f9a64,0 0 14px rgba(63,154,100,.35)}
#de .gcodeprev.vorschlag{border-color:#3f9a64;background:#0e1d15;color:#c9f2d9}
#de .llmrun{background:#2f6fd0;color:#fff;border:0;border-radius:5px;cursor:pointer;font:600 11px system-ui;padding:3px 9px}
#de .llmrun:disabled{opacity:.45;cursor:default}
#de .llmok{background:#2e8a57;color:#fff;border:0;border-radius:5px;cursor:pointer;font:600 11px system-ui;padding:3px 9px}
#de .llmmeld{font:11px/1.4 system-ui;margin:4px 0;white-space:normal;color:#aab4c4}
#de .llmmeld.ok{color:#9be3bf}#de .llmmeld.warn{color:#e0b46a}#de .llmmeld.err{color:#ffb3c1}
#de .llmrow{display:flex;gap:6px;align-items:center;flex-wrap:wrap;margin:4px 0}
#de .llmrow input{flex:1;min-width:120px}
#de .llmchat{display:flex;flex-direction:column;gap:5px;margin:4px 0 6px;max-height:220px;overflow:auto}
#de .llmmsg{align-self:flex-end;max-width:92%;background:#2a2f45;border:1px solid #5b4f8a;color:#e3ddf7;border-radius:9px 9px 2px 9px;padding:5px 8px;font:12px/1.4 system-ui;white-space:pre-wrap;word-break:break-word}
#de .llmleer{font:11px system-ui;color:#8a93a6;font-style:italic}
#de .ginsp .gi-rel{margin:10px;font-size:11px}
#de .ginsp .gi-rel h5{margin:8px 0 4px;font-size:10px;text-transform:uppercase;letter-spacing:.5px;color:#7f8aa0}
#de .ginsp .gi-rel a{display:inline-block;margin:0 4px 4px 0;padding:1px 7px;border-radius:7px;background:#1a2130;border:1px solid #2c3547;color:#cbd3e1;cursor:pointer;font-family:ui-monospace,monospace}
#de .ginsp .gi-rel a:hover{border-color:#7c5cff}
/* Kind-Farben (Kopf) für Inspector-Chips */
#de .kc-command{background:#5b8fd0}#de .kc-event{background:#57b673}#de .kc-rejection{background:#cf6f68}#de .kc-valueobject{background:#49a996}
#de .kc-konfig{background:#8fa3b8}#de .kc-enum{background:#8a8aa0}#de .kc-aggregate{background:#3fb0a6}#de .kc-decider{background:#9678d6}
#de .kc-applier{background:#d0a35a}#de .kc-handle{background:#5d8fbf}#de .kc-fn{background:#2f9d95}#de .kc-saga{background:#9d78d6}#de .kc-transition{background:#a48fd6}#de .kc-state{background:#d4b45f}
#de .kc-readmodel{background:#d0a45a}#de .kc-store{background:#3fb0a6}#de .kc-projektion{background:#57b673}#de .kc-reaktion{background:#d0885a}
#de .kc-pipeline{background:#e08a44}#de .kc-trigger{background:#d29a4a}#de .kc-reader{background:#9678d6}#de .kc-query{background:#5b8fd0}
#de .kc-queryresponse{background:#49a996}#de .kc-codenode{background:#9aa0aa}#de .kc-llmnode{background:#a48fd6}
#de .kc-frist,#de .kc-dienst,#de .kc-hostsetting{background:#c9a24b}
#de .kc-akteur{background:#e07ab4}#de .kc-auf{background:#eaa0c9}
/* Semantischer Zoom: Landkarte (<0.4) · Ablauf (<0.75) · Detail */
#de .gcanvas.lod-ablauf .gnode2 .gbody,#de .gcanvas.lod-ablauf .gnode2 .gsum{display:none}
#de .gcanvas.lod-ablauf .gnode2{width:230px}
#de .gcanvas.lod-ablauf .ghead{padding:6px 10px;border-radius:8px}
#de .gcanvas.lod-ablauf .ghead .gtitle{font-size:12px;white-space:normal;overflow-wrap:anywhere;line-height:1.15}
#de .gcanvas.lod-ablauf .ghead .gtitle.hatname .gk{display:none}
#de .gcanvas.lod-ablauf .ghead .gcol,#de .gcanvas.lod-ablauf .ghead .gx{display:none}
#de .gcanvas.lod-ablauf .gnode2.ungeschrieben .ghead::before,#de .gcanvas.lod-ablauf .gnode2.entwurf .ghead::before,#de .gcanvas.lod-ablauf .gnode2.island .ghead::after{display:none}
#de .gcanvas.lod-karte .gnode2{visibility:hidden}
/* Client (docs/konzept-akteure.md §4): die Anschlussleiste IST der Vertrag — in jeder Ansicht lesbar, Breite fest. */
#de .gcanvas .gworld .gnode2.n-client{width:420px}
#de .gcanvas .gnode2.n-client .gsum.gleiste{display:block}
#de .gcanvas.lod-ablauf .gnode2.n-client .ghead .gtitle.hatname .gk{display:inline}
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
    <button class="act" onclick="deLaden()" title="Domänen laden: wählen, welche Domänen auf dem Board erscheinen (leer, einzelne, alle)">🗂 Domänen laden</button>
    <button class="act" onclick="deValidate()">✓ Prüfen</button>
    <button class="act" onclick="deGrammatik()" title="Die Grammatik: was sich womit verbinden lässt, und je Regel ihr Gegenstück im Build">📐 Grammatik</button>
    <button class="act" onclick="deCompile()">⚙ Kompilieren</button>
    <button class="act" id="de-llmalle" onclick="deLlmAlle()" title="Alle 🤖-Knoten mit Auftrag nacheinander ausführen — jeder Vorschlag wartet auf ✓ Übernehmen">🤖 Alle ausführen</button>
    <button class="act run" id="de-simbtn" onclick="deSim()">▶ Simulation</button>
    <button class="act" onclick="deVorschau()" title="Trockenlauf: was „C# schreiben“ anlegen/ändern würde — nichts wird geschrieben">👁 Vorschau</button>
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
  const KINDINFO={command:["Command","cmd"],event:["Event","evt"],rejection:["Ablehnung","rej"],valueobject:["Value Object","vo"],konfig:["Konfiguration","vo"],query:["Query","qry"],queryresponse:["Response","qrsp"],auftrag:["Auftrag","cmd"]};
  let MODEL={schemaVersion:"2",akteure:[],clients:[],records:[],enums:[],aggregate:[],decider:[],applier:[],sagas:[],states:[],transitions:[],readModels:[],stores:[],projektionen:[],reader:[],reaktionen:[],pipelines:[],triggers:[],frists:[],dienste:[],hostSettings:[],codeNodes:[],llmNodes:[]};
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
    // Akteur (docs/konzept-akteure.md): wer von außen hineingibt — darf[] = die Typen seiner IDarf<T> (Command/Query/Trigger).
    m.akteure=m.akteure||[];m.akteure.forEach(a=>{if(!a._id)a._id="ak"+(NID++);a.darf=a.darf||[];});   /* art/dienste bewusst nicht vorbelegen (Herkunfts-Hash) */
    // Client (docs/konzept-akteure.md §4): die Software an der Leitung — traegt[] = Akteur-Vertrags-Teile, sendet/fragt/kenntnis = der Rand.
    // Katalog-Funktion (IFunktion): Auftrag → Ergebnis-Events; ein Prozess ruft sie mit Rufe<F> (Dann ƒ an der Regel).
    m.funktionen=m.funktionen||[];m.funktionen.forEach(f=>{if(!f._id)f._id="fk"+(NID++);f.ergebnisse=f.ergebnisse||[];});
    m.clients=m.clients||[];m.clients.forEach(c=>{if(!c._id)c._id="cl"+(NID++);c.traegt=c.traegt||[];c.sendet=c.sendet||[];c.fragt=c.fragt||[];c.kenntnis=c.kenntnis||[];});
    // Reaktion = emittierender Konsument (ISubscriber → IAsyncEnumerable<OneOf<Cmd>>): Trigger-Event → Handle → OneOf-Commands.
    m.reaktionen.forEach(r=>{if(!r._id)r._id="rk"+(NID++);r.handles=r.handles||[];r.handles.forEach(hd=>{hd.sends=hd.sends||[];hd.publishes=hd.publishes||[];});});
    // Pipeline = 4. durabler Konsument (IPipelineHandler): Trigger-Msg ODER Event → Handle → OneOf-Command(s).
    m.pipelines.forEach(p=>{if(!p._id)p._id="pl"+(NID++);p.handles=p.handles||[];p.handles.forEach(hd=>{hd.sends=hd.sends||[];hd.emits=hd.emits||[];hd.schedules=hd.schedules||[];   /* publishes/fristen bewusst NICHT vorbelegen: sonst gälte ein gespeichertes Board als „geändert" (Herkunfts-Hash) */
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
          sendeAusdruck:st.sendeAusdruck,kompensationAusdruck:st.kompensationAusdruck,kompensationJe:!!st.kompensationJe,
          ...(st.rufe?{rufe:st.rufe}:{}),...(st.zeitlimit?{zeitlimit:st.zeitlimit}:{})}]}));s.schritte=[];});
    return m;}
  const aggSelect=(val,on)=>{const s=h("select",{onchange:e=>on(e.target.value)});
    if(!val||!MODEL.aggregate.some(a=>a.name===val)){const o=h("option",{value:val||""},val||"— Aggregat —");o.selected=true;s.append(o);}
    MODEL.aggregate.forEach(a=>{const o=h("option",{value:a.name},a.name);if(a.name===val)o.selected=true;s.append(o);});return s;};
  // Standard-Namespace für neue Knoten: der eines vorhandenen Aggregats/Records; sonst eine Projekt-Wurzel aus dem Code (Rahmen).
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
  let TEIL=null;   // Verbinden-Modus: statt Voll-Render nur die betroffenen Karten neu (Set der Knoten-Ids)
  function render(){ if(TEIL){TEIL.gerufen=true;return;} ELEM=null; AKM=null; deriveMembership(); renderGraph(); autosave(); if(window.simNachRender)simNachRender(); }
  // Teil-Neuaufbau: betroffene Karten ersetzen, Kurzfassungen/Kanten/Panel auffrischen — Fläche, Zoom und Layout bleiben stehen.
  function teilNeu(ids){if(!world||!canvas){render();return;}
    ELEM=null;deriveMembership();ISLE=islandInfo().ids;berechneSicht();
    // Knoten entstanden/entfallen (z. B. ein neuer Handle durch „+ Query andocken"): dann voll neu zeichnen (Raster packt neu).
    const gezeichnet=new Set([...world.querySelectorAll(".gnode2")].map(e=>e.dataset.id));
    if(gezeichnet.size!==VIS.size||[...VIS].some(id=>!gezeichnet.has(id))){render();return;}
    ids.forEach(id=>{const alt=world.querySelector('[data-id="'+id+'"]'),n=NODEBY.get(id);if(!alt||!n)return;
      for(const k in SLOTS)if(SLOTS[k]&&alt.contains(SLOTS[k]))delete SLOTS[k];
      alt.replaceWith(nodeEditor(n));});
    world.querySelectorAll(".gnode2").forEach(el=>{const n=NODEBY.get(el.dataset.id),alt=el.querySelector(".gsum");if(!n||!alt)return;
      const neu=kurzfassung(n);if(neu.innerHTML!==alt.innerHTML)alt.replaceWith(neu);});   // nur wo sich wirklich etwas ändert
    const altI=canvas.querySelector(".ginsp");INSP_SCROLL=altI?{id:altI.dataset.sel,top:altI.scrollTop}:null;
    drawEdges();FOCUS=SEL?sliceVon(SEL):null;wendeFokusAn();zeigeInspector();autosave();if(window.simNachRender)simNachRender();}
  // Eine Modell-Änderung aus dem Verbinden-Modus ausführen: ohne Voll-Render, danach nur die betroffenen Karten.
  function imModus(ids,aendern){TEIL={gerufen:false};try{aendern();}finally{const g=TEIL.gerufen;TEIL=null;if(g)teilNeu(ids.filter(Boolean));}}

  // Eindeutiger Name — Namen sind der Referenzschlüssel für Kanten/Decider/Applier.
  function uniq(base){const all=new Set([...MODEL.records.map(r=>r.name),...MODEL.aggregate.map(a=>a.name),...MODEL.enums.map(e=>e.name),...MODEL.sagas.map(s=>s.name),...MODEL.readModels.map(x=>x.name),...MODEL.stores.map(x=>x.name),...MODEL.projektionen.map(x=>x.name),...MODEL.reader.map(x=>x.name),...MODEL.reaktionen.map(x=>x.name),...MODEL.pipelines.map(x=>x.name),...MODEL.triggers.map(x=>x.name),...MODEL.frists.map(x=>x.name),...MODEL.dienste.map(x=>x.name),...MODEL.hostSettings.map(x=>x.name),...MODEL.codeNodes.map(x=>x.name),...MODEL.llmNodes.map(x=>x.name),...MODEL.akteure.map(x=>x.name),...MODEL.clients.map(x=>x.name),...MODEL.funktionen.map(x=>x.name)]);
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
  function neuerKnoten(kind,x,y,opt){
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
    else if(kind==="pipeline")MODEL.pipelines.push({_id:"pl"+(NID++),name:uniq("NeuePipeline"),namespace:defaultNs(),pipelineId:"",handles:[],...pos});
    else if(kind==="trigger")MODEL.triggers.push({_id:"tg"+(NID++),name:uniq("NeuTrigger"),namespace:defaultNs(),msgName:uniq("NeuTriggerMsg"),felder:[],...pos});
    else if(kind==="frist")MODEL.frists.push({_id:"fr"+(NID++),name:uniq("NeueFrist"),kontext:"",dauerSetting:"",plant:[],storniert:[],sendet:"",aggregat:"",...pos});
    else if(kind==="dienst")MODEL.dienste.push({_id:"di"+(NID++),name:uniq("NeuerDienst"),vertrag:"IDienst",extern:false,codeSrc:null,...pos});
    else if(kind==="funktion"){const nm=uniq("INeueFunktion"),ns=defaultNs(),an=uniq(nm.replace(/^I/,"")+"Auftrag");
      MODEL.records.push({name:an,kind:"auftrag",funktion:nm,namespace:ns,felder:[]});
      MODEL.funktionen.push({_id:"fk"+(NID++),name:nm,namespace:ns,auftrag:an,ergebnisse:[],...pos});}
    else if(kind==="hostsetting")MODEL.hostSettings.push({_id:"hs"+(NID++),name:uniq("NeuSetting"),typ:"string",default:"",envKey:"",...pos});
    else if(kind==="akteur")MODEL.akteure.push({_id:"ak"+(NID++),name:uniq("NeuerAkteur"),namespace:defaultNs(),darf:[],...pos});
    else if(kind==="client")MODEL.clients.push({_id:"cl"+(NID++),name:uniq("INeuerClient"),namespace:defaultNs(),traegt:[],sendet:[],fragt:[],kenntnis:[],...pos});
    else if(kind==="codenode")MODEL.codeNodes.push({_id:"cn"+(NID++),name:uniq("Code"),text:"",...pos});
    else if(kind==="llmnode")MODEL.llmNodes.push({_id:"ln"+(NID++),name:uniq("LLM"),intent:"",...pos});
    else MODEL.records.push({name:uniq("Neu"+kindLabel(kind).replace(/\s/g,"")),kind,namespace:defaultNs(),felder:kind==="command"&&ID_FELD()?[{_id:"f"+(NID++),name:ID_FELD(),typ:"Guid"}]:[],...pos});
    // Heimat-Domäne in der Ansicht merken: trägt Namespace-lose Arten (📝/🤖, Dienst, HostSetting …) und hält Abhängige
    //   (Decider/Applier/State/Regel) in ihrer Domäne, auch wenn ihr Besitzer gelöscht wird. Der Code-Namespace geht vor (domKey).
    if(opt&&opt.ns){const neu=graphNodes().find(n=>!vorher.has(n.id));if(neu){VIEW.heim={...(VIEW.heim||{}),[neu.id]:opt.ns};if(opt.blk)VIEW.heimBlk={...(VIEW.heimBlk||{}),[neu.id]:opt.blk};speichereAnsicht();}}
    // Im Akteur-Rahmen angelegt: der Akteur darf den neuen Eingang sofort (IDarf) — so bleibt er auch in seinem Rahmen.
    if(opt&&opt.akt){const neu=graphNodes().find(n=>!vorher.has(n.id)),ak=MODEL.akteure.find(a=>a.name===opt.akt);
      if(neu&&ak){const nm=neu.kind==="trigger"?trigName(neu.ref):neu.ref.name;if(nm&&!(ak.darf=ak.darf||[]).includes(nm))ak.darf.push(nm);
        VIEW.heimAkteur={...(VIEW.heimAkteur||{}),[neu.id]:opt.akt};speichereAnsicht();}}
    render();
    if(!(opt&&opt.still))fokussiereNeu(vorher,true);
  }
  const addRecord=k=>neuerKnoten(k);
  const addAggregat=()=>neuerKnoten("aggregate");
  const addEnum=()=>neuerKnoten("enum");
  const addSaga=()=>neuerKnoten("saga");

  // ══ NACHRICHT MIT ZWEI SEITEN (docs/konzept-editor-pipelines.md §12): „◀ kommt aus" = wer diesen Typ erzeugt, „geht an ▶" = wer
  //   ihn konsumiert. KEINE zweite Liste: beide Seiten sind die umgekehrte Sicht auf die Ausgänge/Eingänge der Bausteine (eine
  //   Wahrheit = die Signatur des Erzeugers). Je Partner: Knoten-Id (springen), Text, Markierung, optional „lösen".
  const hdId=(kind,o,hd)=>{const ids=handleIds(o,HANDLE_ART[kind][1]+":"+o._id);return ids[(o.handles||[]).indexOf(hd)];};
  const ohneX=(arr,x)=>(arr||[]).filter(v=>v!==x);
  const trigName=t=>t.msgName||t.name||"";
  function partnerVon(nm,trig){
    const ein=[],aus=[],P=(arr,id,text,mark,los)=>arr.push({id,text,mark:mark||"",los});
    const r=trig?null:recByName(nm),k=trig?"trigger":(r?r.kind:"");if(!nm)return {ein,aus,k};
    const HD=(kind,coll,f)=>(MODEL[coll]||[]).forEach(o=>(o.handles||[]).forEach(hd=>f(o,hd,hdId(kind,o,hd))));
    const hText=(o,hd)=>o.name+".Handle("+handleDisc(hd)+")";
    if(k==="command"){
      MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{
        if(d.sende===nm)P(ein,"tr:"+t._id,"Regel · "+(t.prozess||"Prozess"));
        if(d.kompensation===nm)P(ein,"tr:"+t._id,"Regel · "+(t.prozess||"Prozess"),"↩ Kompensation");}));
      HD("reaktion","reaktionen",(o,hd,id)=>{if((hd.sends||[]).includes(nm))P(ein,id,hText(o,hd),"",()=>{hd.sends=ohneX(hd.sends,nm);});});
      HD("pipeline","pipelines",(o,hd,id)=>{
        if((hd.sends||[]).includes(nm))P(ein,id,hText(o,hd),hd.akteur?"im Auftrag 👤 "+hd.akteur:"",()=>{hd.sends=ohneX(hd.sends,nm);});
        (hd.fristen||[]).filter(f=>f.command===nm).forEach(f=>P(ein,id,hText(o,hd),f.art==="storno"?"✕⏳ Storno":"⏳ per Frist",
          ()=>{hd.fristen=(hd.fristen||[]).filter(x=>x!==f);}));});
      MODEL.frists.forEach(f=>{if(f.sendet===nm)P(ein,"fr:"+f._id,"Frist "+(f.name||""),"⏳");});
      akteurePartner(ein,nm,P);
      // Zusage im Akteur-Vertrag (Akteur-Konzept §3): der Akteur antwortet draußen auf ein Event mit diesem Command.
      vertragsZusagen().forEach(({a,r,id})=>{if((r.ausgaenge||[]).includes(nm))P(ein,id,a.name+" · Zusage auf "+(r.eingang||"?"),"Vertrag",()=>{r.ausgaenge=ohneX(r.ausgaenge,nm);});});
      if(!ein.length)P(ein,null,"Außenwelt (Client)","abgeleitet: kein interner Erzeuger · kein Akteur");
      MODEL.decider.forEach(d=>{if(d.command===nm)P(aus,"dec:"+d._id,"Decide @ "+(d.aggregat||"— kein Aggregat"));});}
    else if(k==="event"||k==="rejection"){
      MODEL.decider.forEach(d=>{if((d.ergibt||[]).some(o=>o.event===nm))P(ein,"dec:"+d._id,"Decide("+(d.command||"?")+") @ "+(d.aggregat||"—"));});
      [["projektion","projektionen"],["reaktion","reaktionen"],["pipeline","pipelines"]].forEach(([kind,coll])=>{
        HD(kind,coll,(o,hd,id)=>{if((hd.publishes||[]).includes(nm))P(ein,id,hText(o,hd),"veröffentlicht",()=>{hd.publishes=ohneX(hd.publishes,nm);});
          if(hd.event===nm&&(kind!=="pipeline"||(hd.inputKind||"event")==="event"))P(aus,id,hText(o,hd));});});
      MODEL.applier.forEach(a=>{if(a.event===nm)P(aus,"app:"+a._id,"Apply @ "+(a.aggregat||"—"));});
      MODEL.sagas.forEach(sg=>{if(sg.triggerEvent===nm)P(aus,"saga:"+sg.name,"Prozess "+sg.name,"Auslöser");});
      MODEL.transitions.forEach(t=>{if((t.wenn||[]).includes(nm)||t.sammelEvent===nm)P(aus,"tr:"+t._id,"Regel · "+(t.prozess||"Prozess"),t.sammelEvent===nm?"Σ sammelt":"");});
      vertragsZusagen().forEach(({a,r,id})=>{if(r.eingang===nm)P(aus,id,a.name+" · Zusage",((r.ausgaenge||[]).length?"Vertrag → "+r.ausgaenge.join(", "):"Vertrag · zur Kenntnis")
        +(()=>{const cs=clientTraeger(r.teil||vertragsTyp(a));return cs.length?" · 🔌 "+cs.map(clientAnzeige).join(", "):"";})(),
        ()=>{a.vertrag=(a.vertrag||[]).filter(x=>x!==r);});});
      MODEL.clients.forEach(c=>{if((c.kenntnis||[]).includes(nm))P(aus,"cl:"+c._id,"Client "+clientAnzeige(c),"Kenntnis",()=>{c.kenntnis=ohneX(c.kenntnis,nm);});});}
    else if(k==="query"){akteurePartner(ein,nm,P);if(!ein.length)P(ein,null,"Außenwelt (Client)","abgeleitet · kein Akteur");
      HD("reader","reader",(o,hd,id)=>{if(hd.query===nm)P(aus,id,hText(o,hd));});}
    else if(k==="queryresponse"){const qs=[];HD("reader","reader",(o,hd,id)=>{if((hd.responses||[]).includes(nm)){P(ein,id,hText(o,hd));qs.push(hd.query);}});
      // Verschachtelte Antwort (Element einer Listen-Antwort): sie kommt als Teil ihres Trägers heraus.
      if(!ein.length)alleFelder().forEach(({owner,f})=>{if(innerTyp(f.typ)===nm&&recByName(owner))P(ein,"rec:"+owner,"Teil von "+owner,"Feld "+f.name);});
      // Die Antwort geht an den, der fragen darf (Akteure der Query) — sonst an die anonyme Außenwelt.
      MODEL.akteure.forEach(a=>{if(qs.some(q=>(a.darf||[]).includes(q)))P(aus,"akt:"+a._id,"Akteur "+a.name,"fragt");});
      if(!aus.length)P(aus,null,"Außenwelt (Client)","abgeleitet · kein Akteur");}
    else if(k==="trigger"){const t=trig;
      if(t.modus)P(ein,null,"Ingress · "+t.modus+(t.route||t.intervall||t.pfad?" "+(t.route||t.intervall||t.pfad):""),"Code-Fakt");
      HD("pipeline","pipelines",(o,hd,id)=>{if((hd.emits||[]).includes(nm))P(ein,id,hText(o,hd),"",()=>{hd.emits=ohneX(hd.emits,nm);});
        if((hd.inputKind==="trigger")&&(hd.input===nm||hd.trigId===t._id||(hd.prod&&hd.prod.k==="tg"&&hd.prod.id===t._id)))P(aus,id,hText(o,hd));});
      akteurePartner(ein,nm,P);
      if(!ein.length)P(ein,null,"Außenwelt (Client)","abgeleitet: kein interner Erzeuger · kein Akteur");}
    return {ein,aus,k};}
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
  // Vertrags-Teil (Interface-Name) → Akteur: sein Haupt-Vertrag (vertragsTyp) oder ein weiterer Teil an einer Zusage (r.teil).
  function teilAkteur(teil){return MODEL.akteure.find(a=>(a.vertrag||[]).length&&(vertragsTyp(a)===teil||(a.vertrag||[]).some(r=>r.teil===teil)));}
  function teilZusagen(teil){const a=teilAkteur(teil);if(!a)return [];
    return (a.vertrag||[]).map((r,i)=>({a,r,i,id:aufId(a,i)})).filter(x=>(x.r.teil||vertragsTyp(a))===teil);}
  const darfHalter=nm=>MODEL.akteure.filter(a=>(a.darf||[]).includes(nm)).map(a=>a.name);
  // Verkörpert (abgeleitet wie AkteurRechteGenerator): Akteure der getragenen Teile, dazu je Sendet/Fragt die IDarf-Halter — falls
  //   keiner der Teil-Akteure den Typ schon darf.
  function clientAkteure(c){const basis=new Set((c.traegt||[]).map(teilAkteur).filter(Boolean).map(a=>a.name)),s=new Set(basis);
    [...(c.sendet||[]),...(c.fragt||[])].forEach(nm=>{const hs=darfHalter(nm);if(!hs.some(x=>basis.has(x)))hs.forEach(x=>s.add(x));});
    return [...s].sort();}
  const clientTraeger=teil=>MODEL.clients.filter(c=>(c.traegt||[]).includes(teil));
  // Die Ports der Anschlussleiste: Richtung, Nachricht, in wessen Namen (Akteur), Ziel-Knoten.
  function clientPorts(c){const P=[],ver=clientAkteure(c);
    const node=nm=>{if(recByName(nm))return "rec:"+nm;const t=MODEL.triggers.find(x=>trigName(x)===nm);return t?"tg:"+t._id:null;};
    const als=nm=>{const hs=darfHalter(nm),drin=hs.filter(x=>ver.includes(x));return drin.length?drin:hs;};
    (c.traegt||[]).forEach(t=>{const rs=teilZusagen(t);if(!rs.length)P.push({ri:"zusage",msg:"?",teil:t,akteure:[],aus:[],node:null,fehlt:true});
      rs.forEach(({a,r,id})=>P.push({ri:"zusage",msg:r.eingang||"?",aus:r.ausgaenge||[],strom:!!r.strom,akteure:[a.name],teil:t,node:id}));});
    (c.sendet||[]).forEach(nm=>P.push({ri:"sendet",msg:nm,akteure:als(nm),node:node(nm)}));
    (c.fragt||[]).forEach(nm=>P.push({ri:"fragt",msg:nm,akteure:als(nm),node:node(nm)}));
    (c.kenntnis||[]).forEach(nm=>P.push({ri:"kenntnis",msg:nm,akteure:[],node:node(nm)}));
    return P;}
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
    FOCUS=ids;drawEdges();wendeFokusAn();}
  window.deClients=()=>MODEL.clients.map(c=>({name:c.name,handshake:clientAnzeige(c),akteure:clientAkteure(c),
    buendel:clientBuendel(c).map(b=>({ziel:b.key,durabel:b.durabel,text:buendelText(b)}))}));
  // Akteure, die diesen Typ hineingeben dürfen — als Erzeuger auf der „◀ kommt aus"-Seite; „lösen" entzieht das Recht.
  function akteurePartner(ein,nm,P){MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(nm))P(ein,"akt:"+a._id,"Akteur "+a.name,"darf",()=>{a.darf=ohneX(a.darf,nm);});});
    // Clients, deren Vertrag die Nachricht sendet bzw. fragt (der Rand; der Akteur oben ist die Befugnis dahinter).
    MODEL.clients.forEach(c=>{if((c.sendet||[]).includes(nm))P(ein,"cl:"+c._id,"Client "+clientAnzeige(c),"sendet",()=>{c.sendet=ohneX(c.sendet,nm);});
      if((c.fragt||[]).includes(nm))P(ein,"cl:"+c._id,"Client "+clientAnzeige(c),"fragt",()=>{c.fragt=ohneX(c.fragt,nm);});});}
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
  window.deHerkunft=()=>{const r=[];NODEBY.forEach(n=>{let nm=null,trig=null;
    if(["command","query","event","rejection","queryresponse"].includes(n.kind))nm=n.name;else if(n.kind==="trigger"){trig=n.ref;nm=trigName(n.ref);}else return;
    const pv=partnerVon(nm,trig),m=akteurMengen().get(nm);
    r.push({kind:n.kind,name:nm,ein:pv.ein.map(x=>x.text+(x.mark?" ["+x.mark+"]":"")),aus:pv.aus.map(x=>x.text),
      direkt:m?[...m.direkt]:[],kette:m?[...m.kette]:[]});});return r;};
  window.deHandles=()=>{const r=[];Object.entries(HANDLE_ART).forEach(([k,[coll]])=>(MODEL[coll]||[]).forEach(o=>(o.handles||[]).forEach(hd=>
    r.push({art:k,owner:o.name,eingang:k==="pipeline"?pipeEingang(hd):(hd.event||hd.query||""),kind:hd.inputKind||"",akteure:[...(handleAkteure(k,hd)||[])]}))));return r;};
  // „Wer?" (§12.8 D): von welchem Akteur kommt die Nachricht — direkt (IDarf, oben unter „kommt aus") und über die Kette
  //   (abgeleitet, read-only, mit Pfad). Ein Eingang ohne beides ist eine Lücke (GR-HERKUNFT).
  function wer(sek,nm){if(!MODEL.akteure.length)return;const m=akteurMengen().get(nm),kette=m?[...m.kette].filter(a=>!m.direkt.has(a)).sort():[];
    if(kette.length){sek.append(h("div",{class:"gsec",title:"abgeleitet: eine Pipeline, ein Prozess, ein Reader oder Decide erzeugt sie aus einer Nachricht dieses Akteurs — kein IDarf nötig"},"👤 über die Kette"));
      kette.forEach(a=>{const ak=MODEL.akteure.find(x=>x.name===a),pfad=kettenPfad(nm,a);
        const row=h("div",{class:"gp-row",title:pfad.join(" → ")},h("span",{class:"gp-t"},(ART_SYM[ak&&ak.art]||"👤 ")+a),h("span",{class:"gp-mark"},pfad.join(" → ")));
        if(ak)row.onclick=()=>waehle("akt:"+ak._id);sek.append(row);});}
    else if(istEingangName(nm)&&!(m&&m.direkt.size))sek.append(h("div",{class:"llmmeld err",title:"GR-HERKUNFT"},"⚠ kommt von keinem Akteur — ⊕ darf oder über eine Kette erzeugen"));}
  // Ein Pfad, auf dem nm im Namen von akt entsteht (rückwärts bis zu dessen direktem Eingang bzw. Dienst-Wechsel).
  function kettenPfad(nm,akt){const R=flussRegeln(),seen=new Set();
    const zurueck=(x,tiefe)=>{if(tiefe>10||seen.has(x))return null;seen.add(x);
      const m=akteurMengen().get(x);if(m&&m.direkt.has(akt))return [x];
      for(const [ein,aus,w] of R){if(!aus.includes(x))continue;
        if(w===akt)return ["⚙ "+akt,x];
        for(const e of ein){if(!mengeVon(e).has(akt))continue;const p=zurueck(e,tiefe+1);if(p)return [...p,x];}}
      return null;};
    return zurueck(nm,0)||[nm];}
  // Die zwei Seiten einer Nachricht: ◀ kommt aus (+ ⊕ Erzeuger) · geht an ▶ (+ ⊕ Konsument). Ports: die bestehenden Slot-Schlüssel.
  function zweiSeiten(body,nm,trig,einPort,ausPort){
    const pv=partnerVon(nm,trig),sorte=trig?"trigger":grNachrichtSorte(nm),kard=grKardinalitaet(sorte);
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
    if(r.kind==="command"||r.kind==="event"||r.kind==="rejection"){const ag=recordAgg(r.name);
      body.append(h("div",{class:"gsec",title:"abgeleitet aus Decider/Applier"},"Aggregat: "+(ag||"— keinem (Decider/Applier verdrahten)")));}
    if(r.kind==="command")body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>r.istErzeugung=e.target.checked||undefined,...(r.istErzeugung?{checked:"checked"}:{})}),"Erzeugung (ICreationCommand)"));
    // Zwei Seiten je Nachricht (§12): ◀ kommt aus (⊕ Erzeuger) · geht an ▶ (⊕ Konsument). Auf der Fläche nur die Ports (Kanten),
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
     if(r.kind==="command"||r.kind==="query"){const dp=darfPort(r.name);ein=ein?h("div",{},ein,dp):dp;}
     if(ein||aus){if(INSP)zweiSeiten(body,r.name,null,ein,aus);else{if(ein)body.append(ein);if(aus)body.append(aus);}}}
    if(r.kind==="valueobject")body.append(slotRow("ftype","als Feldtyp ▶","r",{type:"ftype",dir:"out",typeName:r.name},"ftype:out:rec:"+r.name));
    if(r.kind==="auftrag"){const a=anchorDot("command");a.classList.add("o");reg("auf:out:"+r.name,a,null);
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"Eingang von ƒ "+(r.funktion||"— (an der Funktion wählen)")+" ▶"),a));}
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
    body.append(codePort("dec:rumpf:"+d._id,{k:"decider",dec:d._id},d.codeSrc,"Decide-Logik"));
  }
  // Applier: gespiegelt zum Decider — Event rein (rechts), Aggregat OBEN. Körper = Code.
  function applierCard(body,a){
    body.append(topSlot("appagg","▲ Aggregat: "+(a.aggregat||"— ⊕ Aggregat wählen"),{type:"appAgg",dir:"out",app:a._id},"app:aggout:"+a._id));
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
  // 🤖 LLM-Knoten an einen Code-Block andocken — EIN Klick (auch in der Detail-Sicht/im Inspector). Hängt schon einer
  //   dran, wird nur dessen Prompt-Feld fokussiert. Der Slot-Schlüssel wird mitgemerkt (stabil über Neu-Einlesen).
  let LLM_FOKUS=null;
  function llmAndocken(codeId){let l=MODEL.llmNodes.find(x=>x.promptZiel===codeId);
    if(!l){l={_id:"ln"+(NID++),name:uniq("LLM"),intent:"",promptZiel:codeId};const k=konsolenId(codeId);if(k)l.promptSlot=k;MODEL.llmNodes.push(l);}
    LLM_FOKUS=l._id;render();}
  // Ein-Klick am leeren Rumpf-Port: 📝 Code-Block erzeugen und andocken; bei 🤖 zusätzlich den LLM-Knoten an den Block.
  function addCode(target,kind){const id="cn"+(NID++);MODEL.codeNodes.push({_id:id,name:uniq("Code"),text:""});
    setCodeSrc(target,id);if(kind==="llm")llmAndocken(id);else render();}
  // Code-Eingang (Rumpf): getippter Slot `code`. Leer → deutlicher „⚙ …fehlt"-Marker + Ein-Klick-Knöpfe.
  function codePort(key,target,codeSrc,label){const s=port("code");s.classList.add("i");reg(key,s,{type:"code",dir:"in",target});
    const src=codeSrc?nodeName(codeSrc):null;const lbl=label||"Logik";
    // Passiv: der gefüllte Rumpf zeigt nur, welcher Code-Block hängt (kein Editier-Modal am Konsumenten).
    if(src)return h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"color:#9be3bf"},"◀ "+lbl+": "+src));
    if(target&&codeOwnerLeer(target))return h("div",{class:"slotrow"},s,h("span",{class:"slotlbl",style:"opacity:.6",title:"Im Code bewusst leer (No-op) — kein Platzhalter"},"∅ "+lbl+": bewusst leer"));
    return h("div",{class:"slotrow codeempty"},s,
      h("span",{class:"slotlbl codemiss",style:"flex:1"},"⚙ "+lbl+" fehlt"),
      h("button",{class:"codeadd",title:"Code-Block erzeugen und hier andocken",onclick:()=>addCode(target,"code")},"＋📝"),
      h("button",{class:"codeadd",title:"Code-Block + 🤖 LLM-Knoten in einem Klick erzeugen und andocken — dann Prompt schreiben und ▶",onclick:()=>addCode(target,"llm")},"＋🤖"));}
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
    // Impl-Klasse: aus dem Code (fest) oder — bei einem neuen Store — benennbar (leer = Vorschlag: Name ohne I-Präfix).
    if(st.impl&&st.impl.datei)body.append(h("div",{class:"gsig",title:"Die Implementierungs-Klasse aus dem Code — neue Fns bekommen dort einen Platzhalter"},"Impl: "+st.impl.name));
    else if(st.datei)body.append(h("div",{class:"gsec",style:"opacity:.7"},"Impl: keine eindeutige Klasse im Code — Methoden werden nicht ergänzt"));
    else body.append(h("div",{class:"frow",title:"Implementierungs-Klasse, die „C# schreiben“ anlegt"},h("span",{class:"slotlbl"},"Impl"),
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
    else box.append(h("div",{class:"frow",title:"Name des Fähigkeits-Interfaces, das „C# schreiben“ anlegt (leer = Vorschlag)"},h("span",{class:"slotlbl"},"Fähigkeit"),
      inp(fn.faehigkeit,v=>{fn.faehigkeit=v.trim()||undefined;},faehigkeitVorschlag(st,fn))));
    box.append(codePort("impl:in:"+st._id+":"+fn._id,{k:isRead?"readFn":"writeFn",store:st._id,fn:fn._id},fn.codeSrc,"Impl-Logik"));
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
    const ds=derivedStores(p);
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
      body.append(codePort("ctrl:in:"+p._id+":"+hi,{k:"pjHandle",proj:p._id,hi:hi},hd.codeSrc,"Controller-Logik"));
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
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"reakHandle",reaktion:r._id,hi:hi},hd.codeSrc,"Controller-Logik"));
  }
  function plHandleBody(body,p,hd,hi){

      const kind=hd.inputKind||"event", isTrig=kind==="trigger", isSelf=kind==="self";
      const sl=port(isTrig?"trigmsg":(isSelf?"self":"event"));sl.classList.add("i");
      reg("pl:in:"+p._id+":"+hi,sl,{type:isTrig?"trigmsg":(isSelf?"self":"evtUse"),dir:"in",pipeline:p._id,handleIdx:hi});
      const tlabel=(hd.prod&&hd.prod.k==="tg")?(trigMsgLabel(hd.prod.id)||hd.input):hd.input;
      const lbl=isTrig?("◀ Trigger "+(tlabel||"?")):(isSelf?("◀ Self "+(hd.selfName||"?")):("◀ Auf "+(hd.event||"?")));
      body.append(h("div",{class:"slotrow"},sl,h("span",{class:"slotlbl",style:"flex:1"},lbl),h("button",{class:"rm",onclick:()=>{p.handles.splice(hi,1);render();}},"✕")));
      // ── Ausgänge NACH TYP (§12): EIN Port „+ Ausgang ▶" — die Art folgt aus der angeklickten Karte (Command → sends,
      //    transientes Event → publishes, Trigger-Karte → emits; persistentes Event gesperrt: GR-KEIN-EVENT-AUS-PIPELINE).
      //    Je Command: sofort · ⏳ Frist<T> · ✕⏳ FristStorno<T> (eine Frist ist ein Ausgang, kein Knoten).
      const ausPort=(key,extra)=>{const so=port("command");so.classList.add("o");reg(key,so,Object.assign({type:"aus",dir:"out",pipeline:p._id,handleIdx:hi},extra||{}));return so;};
      const modus=(c,akt)=>{const sel=h("select",{class:"gmodus",title:"Wie wird der Command ausgelöst?",onchange:e=>{const v=e.target.value;
          hd.sends=ohneX(hd.sends,c);hd.fristen=(hd.fristen||[]).filter(f=>!(f.command===c&&f.art===akt));
          if(v==="sofort"){if(!hd.sends.includes(c))hd.sends.push(c);}
          else if(!hd.fristen.some(f=>f.command===c&&f.art===v))hd.fristen.push({command:c,art:v});render();}});
        [["sofort","▶ sofort"],["frist","⏳ per Frist"],["storno","✕⏳ Storno"]].forEach(([v,l])=>{const o=h("option",{value:v},l);if(v===akt)o.selected=true;sel.append(o);});return sel;};
      const ausZeile=(label,so,los,extra)=>body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{los();render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},label),...(extra?[extra]:[]),so));
      (hd.sends||[]).forEach(c=>ausZeile("Command "+(c||"?")+" ▶",ausPort("pl:send:"+p._id+":"+hi+":"+c,{sofort:true}),()=>{hd.sends=ohneX(hd.sends,c);},modus(c,"sofort")));
      (hd.fristen||[]).forEach(f=>ausZeile((f.art==="storno"?"storniert Frist ":"Frist → ")+(f.command||"?")+" ▶",
          ausPort("pl:frist:"+p._id+":"+hi+":"+f.art+":"+f.command,{fristArt:f.art}),()=>{hd.fristen=(hd.fristen||[]).filter(x=>x!==f);},modus(f.command,f.art)));
      (hd.publishes||[]).forEach(ev=>ausZeile("veröffentlicht "+(ev||"?")+" ▶",ausPort("pl:pub:"+p._id+":"+hi+":"+ev),()=>{hd.publishes=ohneX(hd.publishes,ev);}));
      // Trigger → die Trigger-Karte (sie trägt Felder + Ingress); von dort geht er an genau eine Pipeline. Umbenennen bleibt möglich.
      (hd.emits||[]).forEach((nm,ei)=>{const eo=ausPort("pl:emit:"+p._id+":"+hi+":"+nm,{msgName:nm});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.emits.splice(ei,1);render();}},"✕"),
          h("span",{class:"slotlbl"},"Trigger"),h("input",{value:nm,oninput:e=>hd.emits[ei]=e.target.value,onchange:()=>render(),placeholder:"TriggerMsg",style:"flex:1"}),h("span",{class:"slotlbl"},"▶"),eo));});
      body.append(h("div",{class:"slotrow o",title:"Klick auf ⊕: alle Nachrichten leuchten, die diese Pipeline erzeugen darf — die Art folgt aus dem Typ"},
        h("span",{class:"slotlbl"},"+ Ausgang ▶ (Command · Trigger · transientes Event)"),ausPort("pl:aus:"+p._id+":"+hi+":open")));
      // Im Auftrag eines Akteur-Dienstes (z. B. der KI): ein Parameter wie eine Fähigkeit — der Handle entscheidet für ihn,
      //   sendet nur, was er darf (CQRS060), und der Command trägt ihn als Urheber. ⊕ → Dienst-Akteure leuchten.
      {const ak=port("auftrag");ak.classList.add("i");reg("auftrag:in:"+p._id+":"+hi,ak,{type:"auftrag",dir:"in",pipeline:p._id,handleIdx:hi});
       const a=hd.akteur&&MODEL.akteure.find(x=>x.name===hd.akteur),fremd=a?(hd.sends||[]).filter(c=>!(a.darf||[]).includes(c)):[];
       body.append(h("div",{class:"slotrow",title:"Akteur-Dienst als Handle-Parameter (CQRS060)"},ak,
         h("span",{class:"slotlbl",style:"flex:1"},hd.akteur?"◀ im Auftrag von 👤 "+hd.akteur:"◀ im Auftrag von … ⊕ (Dienst-Akteur)"),
         hd.akteur?h("button",{class:"rm",title:"Auftrag lösen",onclick:()=>{delete hd.akteur;render();}},"✕"):null));
       if(fremd.length)body.append(h("div",{class:"llmmeld err"},"⚠ "+hd.akteur+" darf "+fremd.join(", ")+" nicht (CQRS060)"));}
      // Fähigkeiten (Read-Fns als Handle-Parameter) — wie am Reader-Handle.
      (hd.fns||[]).forEach((fid,fj)=>{const rr=fnById(fid);const s=port("store");s.classList.add("o");reg("rcall:out:pl:"+p._id+":"+hi+":"+fid,s,{type:"rcall",dir:"out",pipeline:p._id,handleIdx:hi});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.fns.splice(fj,1);render();}},"✕"),
          h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"darf "+(rr?rr.store.name+"."+rr.fn.name:"?")+" ▶"),s));});
      {const ro=port("store");ro.classList.add("o");reg("rcall:out:pl:"+p._id+":"+hi+":open",ro,{type:"rcall",dir:"out",pipeline:p._id,handleIdx:hi});
       body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Read-Fn ▶"),ro));}
      // Selbst<T> → interner Tick/Timeout (Self-Message kommt als eigener ◀ Self-Handle zurück). Die Verzögerung ist Rumpf.
      (hd.schedules||[]).forEach((sc,si)=>{const ss=port("self");ss.classList.add("o");reg("pl:sched:"+p._id+":"+hi+":"+sc.name,ss,{type:"self",dir:"out",pipeline:p._id,handleIdx:hi,name:sc.name});
        body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{hd.schedules.splice(si,1);render();}},"✕"),
          h("input",{value:sc.name,oninput:e=>sc.name=e.target.value,onchange:()=>render(),placeholder:"SelfMsg",style:"flex:1"}),h("span",{class:"slotlbl"},"↺"),ss));});
      if(!grSelbstErlaubt(hd))body.append(h("div",{class:"slotrow o",title:grText("GR-SELBST-OHNE-EVENT")},h("span",{class:"slotlbl",style:"opacity:.6"},"↺ Selbst nur ohne Event-Eingang")));
      else body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"opacity:.8"},"+ plant Self-Tick ↺"),
        h("button",{class:"codeadd",title:"Selbst<T>-Ausgang + Self-Handle anlegen",onclick:()=>{const nm=uniq("Tick");(hd.schedules=hd.schedules||[]).push({name:nm});if(!p.handles.some(x=>x.inputKind==="self"&&x.selfName===nm))p.handles.push({inputKind:"self",selfName:nm,sends:[],emits:[],schedules:[]});render();}},"＋")));
      body.append(codePort("plctrl:in:"+p._id+":"+hi,{k:"plHandle",pipeline:p._id,hi:hi},hd.codeSrc,"Pipeline-Logik"));
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
      body.append(codePort("ctrl:in:"+r._id+":"+hi,{k:"rdHandle",reader:r._id,hi:hi},hd.codeSrc,"Controller-Logik"));
  }

  // ══ HANDLE- und FN-KNOTEN: jeder Handle einer Projektion/Reader/Reaktion/Pipeline und jede Store-Funktion ist eine
  //   EIGENE Karte (wie der Decider je Command) — mit ihren Ports, ihrem Rumpf und dem Vertrag aus dem Code (was sie
  //   erzeugen KANN und welche Store-Fns sie rufen DARF — nur Signatur). Kein neues Modell-Objekt: die Karte zeigt owner.handles[i] bzw. die Fn.
  const HANDLE_ART={projektion:["projektionen","prj"],reader:["reader","rdr"],reaktion:["reaktionen","rk"],pipeline:["pipelines","pl"]};
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
    else if(n.own.kind==="reaktion")rkHandleBody(body,o,hd,hi);
    else if(n.own.kind==="pipeline")plHandleBody(body,o,hd,hi);}
  function fnCard(body,n){const st=n.own.ref;
    body.append(h("a",{class:"ghl",onclick:()=>{const m=NODEBY.get(n.own.id);if(m){waehle(m.id);centerOn(m);}}},"▲ Store: "+(st.name||"?")));
    body.append(h("div",{class:"gsec"},n.lesen?"Lese-Fähigkeit — ◀ Parameter von Reader-/Pipeline-Handles":"Schreib-Fähigkeit — ◀ Parameter von Projektions-Handles"));
    body.append(fnBlock(st,n.ref,n.lesen));
    // Wer diese Fähigkeit als Parameter verlangt (Signatur-Fakt: darf rufen — nicht „ruft wann").
    const rufer=[];MODEL.projektionen.concat(MODEL.reader,MODEL.pipelines||[]).forEach(o=>(o.handles||[]).forEach(hd=>{if((hd.fns||[]).includes(n.ref._id))rufer.push({o,hd});}));
    if(rufer.length){body.append(h("div",{class:"gsec"},"Fähigkeit von"));
      rufer.forEach(({o,hd})=>body.append(h("div",{class:"gaus"},o.name+".Handle("+handleDisc(hd)+")")));}}
  // Trigger-Msg eines Handles anzeigen (aktueller msgName des verdrahteten Triggers, rename-fest über trigId).
  const trigMsgLabel=id=>{const t=MODEL.triggers.find(x=>x._id===id);return t?(t.msgName||t.name||"Trigger"):null;};
  // ══ INGRESS/PIPELINE: Trigger (Timer/Webhook/FileWatch/Frist) → Trigger-Msg → Pipeline → OneOf-Command(s).
  // Trigger = Ingress-WECKER. Modus + Config; erzeugt EINE IPipelineTrigger-Nachricht (Name + Felder) → Pipeline.
  function triggerCard(body,t){
    body.append(nameInp(t,"name","Trigger","trigger"));
    const modi=[["timer","⏱ Timer (Intervall)"],["webhook","🔗 Webhook (HTTP)"],["filewatch","📁 FileWatch (Datei)"]];
    const sel=h("select",{onchange:e=>{t.modus=e.target.value||undefined;render();}});
    // Ohne Bindung im Code (Composition Root) ist der Modus UNBESTIMMT — nicht geraten.
    const leer=h("option",{value:""},"— Modus (im Code nicht gebunden)");if(!t.modus)leer.selected=true;sel.append(leer);
    modi.forEach(([v,l])=>{const o=h("option",{value:v},l);if(t.modus===v)o.selected=true;sel.append(o);});
    body.append(sel);
    if(t.modus==="webhook"){body.append(h("div",{class:"gsec"},"Route · Request-Typ"));
      body.append(inp(t.route,v=>t.route=v,"/webhooks/x"));body.append(tinp(t.reqTyp,v=>t.reqTyp=v));}
    else if(t.modus==="filewatch"){body.append(h("div",{class:"gsec"},"Pfad · Muster"));
      body.append(inp(t.pfad,v=>t.pfad=v,"/data/incoming"));body.append(inp(t.muster,v=>t.muster=v,"*.png"));}
    else {body.append(h("div",{class:"gsec"},"Intervall"));body.append(inp(t.intervall,v=>t.intervall=v,"30s"));}
    body.append(h("div",{class:"gsec"},"Trigger-Nachricht (IPipelineTrigger)"));
    body.append(inp(t.msgName,v=>{t.msgName=v;},"z. B. DateiErkannt"));
    (t.felder||[]).forEach((f,fi)=>body.append(feldRow(f,()=>{t.felder.splice(fi,1);render();})));
    body.append(h("button",{class:"add",onclick:()=>{(t.felder=t.felder||[]).push({_id:"f"+(NID++),name:uniqFeldName(t.felder,"feld"),typ:"Guid"});render();}},"+ Feld"));
    // Zwei Seiten (§12): ◀ kommt aus = Ingress-Bindung + Pipelines, die den Trigger erzeugen · geht an ▶ = die eine Pipeline.
    const tin=slotRow("trigmsg","+ Erzeuger ⊕ (Pipeline)","l",{type:"trgIn",dir:"in",trigId:t._id},"trg:in:"+t._id);
    const tout=slotRow("trigmsg","+ Konsument ⊕ (Pipeline) ▶","r",{type:"trigmsg",dir:"out",trigId:t._id,msgName:t.msgName},"trg:msg:"+t._id);
    {const dp=darfPort(trigName(t));
     if(INSP)zweiSeiten(body,trigName(t),t,h("div",{},tin,dp),tout);else body.append(tin,dp,tout);}
  }
  // Pipeline = 4. durabler Konsument (IPipelineHandler): je Handle EIN Eingang (Trigger/Event/Self) →
  //   yield ICommand UND/ODER yield IPipelineTrigger (→ andere Pipeline) UND/ODER ScheduleSelf (Tick/Timeout).
  //   Ein Handle kann auch reiner Seiteneffekt sein (kein yield). Das WIE macht der 📝 Rumpf.
  function pipelineCard(body,p){
    body.append(nameInp(p,"name","Pipeline","pipeline"));
    body.append(h("input",{value:p.namespace??"",oninput:e=>p.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec"},"PipelineId"));
    body.append(inp(p.pipelineId,v=>p.pipelineId=v,"z. B. bildverarbeitung"));
    // Konfigurations-Records (Konstruktor-Injektion). Geschrieben wird der Konstruktor nur bei einer NEUEN Pipeline.
    body.append(h("div",{class:"gsec"},"Konfigs (Konstruktor)"+(p.code?" — bestehend: Konstruktor ist Handcode":"")));
    (p.konfigs=p.konfigs||[]).forEach((k,i)=>body.append(h("div",{class:"slotrow"},h("span",{class:"slotlbl",style:"flex:1"},"◀ "+k),
      h("button",{class:"rm",onclick:()=>{p.konfigs.splice(i,1);render();}},"✕"))));
    {const sel=h("select",{onchange:e=>{if(e.target.value&&!p.konfigs.includes(e.target.value))p.konfigs.push(e.target.value);render();}});
     sel.append(h("option",{value:""},"+ Konfig-Record …"));
     MODEL.records.filter(r=>r.kind==="konfig"&&!p.konfigs.includes(r.name)).forEach(r=>sel.append(h("option",{value:r.name},r.name)));
     body.append(sel);}
    body.append(h("div",{class:"gsec"},"Handle: Trigger/Event/Self ◀ → yield Command · yield Trigger · ScheduleSelf. Das WIE macht der Rumpf."));
    body.append(handleListe(p,"pl:"+p._id,"je Handle eine eigene Karte"));
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
    body.append(handleListe(r,"rdr:"+r._id,"Query → Handle → Read-Fn(s) → OneOf-Responses · je Handle eine eigene Karte"));
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

  // KATALOG-FUNKTION (IFunktion): nur die Signatur — EIN Auftrag hinein, OneOf-Ergebnis-Events heraus. Ein Prozess ruft sie mit
  //   Rufe<F> (Regel: „Dann ƒ"); die Implementierung (C#, Python, extern) ist Bindung im Host, kein Teil des Graphen.
  function funktionCard(body,f){
    body.append(topAnchor("command","◀ gerufen von Regeln (Rufe<"+(f.name||"F")+">)","fk:in:"+f.name));
    body.append(nameInp(f,"name","Funktion","funktion"));
    body.append(h("input",{value:f.namespace??"",oninput:e=>f.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    body.append(h("div",{class:"gsec"},"◀ Auftrag (der eine Eingang)"));
    const ai=anchorDot("command");ai.classList.add("i");reg("fk:auftrag:"+f._id,ai,null);
    const aSel=recSelect(f.auftrag,v=>{const alt=recByName(f.auftrag);if(alt&&alt.kind==="auftrag"&&alt.funktion===f.name)delete alt.funktion;
      f.auftrag=v;const neu=recByName(v);if(neu)neu.funktion=f.name;render();},["auftrag"]);
    body.append(h("div",{class:"slotrow"},ai,aSel));
    body.append(h("button",{class:"add",onclick:()=>{const an=uniq((f.name||"F").replace(/^I/,"")+"Auftrag");
      MODEL.records.push({name:an,kind:"auftrag",funktion:f.name,namespace:f.namespace,felder:[]});f.auftrag=an;render();}},"+ neuer Auftrag"));
    body.append(h("div",{class:"gsec"},"Ergebnisse ▶ (OneOf, persistente Events)"));
    (f.ergebnisse||[]).forEach((e,i)=>{const o=anchorDot("event");o.classList.add("o");reg("fk:out:"+f._id+":"+e,o,null);
      body.append(h("div",{class:"slotrow o"},h("button",{class:"rm",onclick:()=>{f.ergebnisse.splice(i,1);render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},e+" ▶"),o));});
    const eSel=recSelect("",v=>{if(v&&!(f.ergebnisse=f.ergebnisse||[]).includes(v))f.ergebnisse.push(v);render();},["event"]);
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"+ Ergebnis"),eSel));
    body.append(h("button",{class:"add",onclick:()=>{const en=uniq((f.name||"F").replace(/^I/,"")+"Erledigt");
      MODEL.records.push({name:en,kind:"event",namespace:f.namespace,felder:[]});(f.ergebnisse=f.ergebnisse||[]).push(en);render();}},"+ neues Ergebnis-Event"));
    body.append(h("div",{class:"gsec",style:"opacity:.6"},"Implementierung = Bindung im Host (AddFunktion<"+(f.name||"F")+", …>) — C#, Python oder extern; nicht im Graph."));
  }

  // Dienst-Bindung = Vertrag (Interface) → Impl (📝-Insel ODER externer Adapter). Gibt dem freistehenden
  //   Domain-Service (SplitZuteiler, ImagePairName) UND den Handler-Dependencies (IImageResizer …) ein Zuhause.
  function dienstCard(body,d){
    body.append(nameInp(d,"name","Dienst","dienst"));
    body.append(h("div",{class:"gsec"},"Vertrag (Interface)"));
    body.append(inp(d.vertrag,v=>d.vertrag=v,"z. B. IImageResizer"));
    const vo=port("store");vo.classList.add("o");reg("di:vertrag:"+d._id,vo,{type:"dienst",dir:"out",dienst:d._id});
    body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},"Vertrag "+(d.vertrag||"?")+" ▶"),vo));
    body.append(h("label",{class:"cbx"},h("input",{type:"checkbox",onchange:e=>{d.extern=e.target.checked||undefined;render();},...(d.extern?{checked:"checked"}:{})}),"Externer Dienst (HTTP/ML) — Impl außerhalb"));
    if(!d.extern){body.append(h("div",{class:"gsec"},"Impl-Logik"));
      body.append(codePort("di:impl:"+d._id,{k:"dienst",dienst:d._id},d.codeSrc,"Impl-Logik"));}
    else body.append(h("div",{class:"gsec",style:"opacity:.6"},"Impl = externer Adapter (nicht im Editor)"));
  }

  // HostSetting = operativer Config-Wert (Name/Typ/Default/EnvKey) — die Editor-Repräsentation von
  //   appsettings/env für DOMÄNEN-relevante Werte (Pfad/Intervall/Timeout). Speist Trigger/Frist.
  // ══ AKTEURE: von wem kommt was? (docs/konzept-akteure.md §2, docs/konzept-domaenen-editor.md §12) ══
  //   Deklariert ist nur IDarf (direkt). Was eine Pipeline/Reaktion/ein Reader/Prozess/Decide aus einer Nachricht erzeugt,
  //   trägt deren Akteure (Kette); ein Handle mit dem Dienst eines Akteurs (IAkteurDienst<A>) wechselt zu A. Spiegel von
  //   DomainEditor/AkteurAnteile.cs — Parität: deAkteurParitaet() gegen rahmen.akteurMengen.
  const ART_SYM={Mensch:"👤 ",Maschine:"⚙ ",Ki:"🤖 "}, ART_RANG={Maschine:0,Ki:1,Mensch:2};
  const artBasis=a=>a==="Mensch"?"IMensch":a==="Maschine"?"IMaschine":a==="Ki"?"IKi":"IAkteur";
  const OHNE_AKT="§ohneAkteur";
  let AKM=null,AKTSET=new Map(),AKTORD=new Map(),AKTDOM=null;
  function istEingangName(nm){const r=recByName(nm);return r?(r.kind==="command"||r.kind==="query"||r.kind==="trigger"):MODEL.triggers.some(t=>trigName(t)===nm);}
  // Eingang eines Pipeline-Handles als Nachrichten-Name (Event, Trigger, Selbst-Nachricht).
  function pipeEingang(hd){const k=hd.inputKind||"event";
    if(k==="event")return hd.event||"";
    if(k==="self")return hd.selfName||"";
    if(hd.input)return hd.input;
    const tid=hd.trigId||(hd.prod&&hd.prod.k==="tg"?hd.prod.id:null),t=tid&&MODEL.triggers.find(x=>x._id===tid);
    return t?trigName(t):"";}
  // Frist<T> liefert einen Command; FristStorno<T> löscht nur eine Frist — kein Nachrichtenfluss (wie Fluss.cs).
  const fristAus=hd=>(hd.fristen||[]).filter(f=>f.art!=="storno").map(f=>f.command);
  // Die Regeln des Flusses: [Eingänge, Ausgänge, Akteur-Wechsel|null] — dieselben Kanten wie Fluss.cs.
  function flussRegeln(){const R=[];
    MODEL.decider.forEach(d=>{if(d.command)R.push([[d.command],(d.ergibt||[]).map(o=>o.event).filter(Boolean),null]);});
    const dienstAkteur=nm=>MODEL.akteure.some(a=>a.name===nm&&(a.dienste||[]).length)?nm:null;
    MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{const e=pipeEingang(hd);
      R.push([e?[e]:[],[...(hd.sends||[]),...fristAus(hd),...(hd.emits||[]),...(hd.publishes||[]),...(hd.schedules||[]).map(x=>x.name)].filter(Boolean),
        hd.akteur?dienstAkteur(hd.akteur):null]);}));
    MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>R.push([[hd.event].filter(Boolean),[...(hd.sends||[]),...(hd.publishes||[])],null])));
    MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>R.push([[hd.event].filter(Boolean),[...(hd.publishes||[])],null])));
    MODEL.reader.forEach(r=>(r.handles||[]).forEach(hd=>R.push([[hd.query].filter(Boolean),[...(hd.responses||[])],null])));
    // Vertrag (Akteur-Konzept §3): Event → Akteur → Commands; die Ausgänge gehören dem Akteur (Wechsel wie beim Dienst).
    vertragsZusagen().forEach(({a,r})=>R.push([[r.eingang].filter(Boolean),[...(r.ausgaenge||[])],a.name]));
    MODEL.sagas.forEach(sg=>{const ts=MODEL.transitions.filter(t=>t.prozess===sg.name);
      R.push([[sg.triggerEvent,...ts.flatMap(t=>t.wenn||[])].filter(Boolean),ts.flatMap(t=>(t.dann||[]).flatMap(d=>[d.sende,d.kompensation])).filter(Boolean),null]);});
    return R;}
  // Nachricht → {direkt:Set, kette:Set}; Fixpunkt über Mengen (ein Command kann von mehreren Akteuren kommen).
  function akteurMengen(){if(AKM)return AKM;const M=new Map();
    const g=nm=>{let m=M.get(nm);if(!m)M.set(nm,m={direkt:new Set(),kette:new Set()});return m;};
    MODEL.akteure.forEach(a=>(a.darf||[]).forEach(nm=>g(nm).direkt.add(a.name)));
    const alle=nm=>{const m=M.get(nm);return m?[...m.direkt,...m.kette]:[];};
    const R=flussRegeln();
    for(let ge=true;ge;){ge=false;R.forEach(([ein,aus,w])=>{const q=w?[w]:ein.flatMap(alle);
      aus.forEach(o=>{const z=g(o).kette;q.forEach(x=>{if(!z.has(x)){z.add(x);ge=true;}});});});}
    return AKM=M;}
  const mengeVon=nm=>{const m=akteurMengen().get(nm);return m?new Set([...m.direkt,...m.kette]):new Set();};
  // Parität mit der C#-Ableitung (nur sinnvoll für ein unverändertes Board).
  window.deAkteurParitaet=()=>{const soll=(MODEL.rahmen&&MODEL.rahmen.akteurMengen)||{},ist=akteurMengen(),diff=[];
    const s=x=>[...x].sort().join(",");
    new Set([...Object.keys(soll),...[...ist.keys()].filter(k=>ist.get(k).direkt.size||ist.get(k).kette.size)]).forEach(k=>{
      const a=soll[k]||{direkt:[],kette:[]},b=ist.get(k)||{direkt:new Set(),kette:new Set()};
      if(s(a.direkt)!==s(b.direkt)||s(a.kette)!==s(b.kette))diff.push(k+": C# "+s(a.direkt)+"|"+s(a.kette)+" ≠ JS "+s(b.direkt)+"|"+s(b.kette));});
    return {gleich:diff.length===0,anzahl:Object.keys(soll).length,diff};};
  // Akteur-Menge einer KARTE (Domäne × Akteur-Rahmen): Nachrichten aus der Ableitung, Bausteine über das, was sie tragen.
  function akteurSet(n){let s=AKTSET.get(n.id);if(s)return s;AKTSET.set(n.id,s=new Set());const r=n.ref,add=x=>x&&x.forEach(v=>s.add(v));
    switch(n.kind){
      case "akteur":s.add(r.name);break;
      case "auf":s.add(n.own.ref.name);break;
      case "command":case "query":case "event":case "rejection":case "queryresponse":add(mengeVon(r.name));
        if(n.kind==="query"&&!s.size)MODEL.reader.forEach(rd=>{if((rd.handles||[]).some(h=>h.query===r.name))add(akteurSet({id:"rdr:"+rd._id,kind:"reader",ref:rd}));});
        break;
      case "trigger":add(mengeVon(trigName(r)));break;
      case "decider":add(mengeVon(r.command));break;
      case "applier":add(mengeVon(r.event));break;
      case "aggregate":MODEL.decider.filter(d=>d.aggregat===r.name).forEach(d=>add(mengeVon(d.command)));break;
      case "state":MODEL.decider.filter(d=>d.aggregat===r.aggregat).forEach(d=>add(mengeVon(d.command)));break;
      case "reader":(r.handles||[]).forEach(hd=>add(mengeVon(hd.query)));break;
      case "pipeline":case "reaktion":(r.handles||[]).forEach(hd=>add(handleAkteure(n.kind,hd)));break;
      case "handle":{const o=n.own;if(o.kind==="projektion")add(projektionAkteure(o.ref));else if(o.kind==="reader")add(mengeVon(r.query));
        else{add(handleAkteure(o.kind,r));if(!s.size)add(akteurSet(o));}   // Start-/Tick-Handle ohne Kette: wie seine Pipeline
        break;}
      case "saga":MODEL.transitions.filter(t=>t.prozess===r.name).forEach(t=>(t.dann||[]).forEach(d=>add(mengeVon(d.sende))));
        add(mengeVon(r.triggerEvent));break;
      case "transition":(r.dann||[]).forEach(d=>add(mengeVon(d.sende)));(r.wenn||[]).forEach(e=>add(mengeVon(e)));break;
      case "frist":add(mengeVon(r.sendet));break;
      case "projektion":add(projektionAkteure(r));break;
      case "store":add(storeAkteure(r));break;
      case "fn":{const l=fnLeser(r._id);add(l.size?l:storeAkteure(n.own.ref));break;}
      case "readmodel":{const st=MODEL.stores.find(x=>x.name===r.store);if(st)add(storeAkteure(st));break;}
      case "dienst":{const a=MODEL.akteure.find(x=>(x.dienste||[]).includes(r.vertrag||r.name));
        if(a)s.add(a.name);else MODEL.pipelines.filter(p=>(p.dienste||[]).includes(r.vertrag||r.name)).forEach(p=>add(akteurSet({id:"pl:"+p._id,kind:"pipeline",ref:p})));break;}
      case "codenode":case "llmnode":{const cid=n.kind==="llmnode"?r.promptZiel:r._id,o=cid&&codeBesitzerKnoten(cid);if(o&&o.id!==n.id)add(akteurSet(o));break;}
      default:{   // VO/Enum/Konfig/HostSetting …: folgen ihren Nachbarn (ohne Daten-Daten-Ketten)
        const nb=[...(ADJ.inn.get(n.id)||[]),...(ADJ.out.get(n.id)||[])].map(x=>NODEBY.get(x)).filter(m=>m&&m.kind!=="akteur");
        nb.filter(m=>!DATEN_ARTEN.has(m.kind)).forEach(m=>add(akteurSet(m)));
        if(!s.size)nb.filter(m=>DATEN_ARTEN.has(m.kind)).forEach(m=>add(akteurSet(m)));}}   // VO in VO: über den Träger
    // Datentypen ohne eigene Kette (verschachtelte Response, Read Model ohne Store …) gehen mit ihren Nachbarn. Eingänge NIE —
    //   ein Command/eine Query/ein Trigger ohne Akteur ist eine Lücke (GR-HERKUNFT) und steht sichtbar „ohne Akteur".
    if(!s.size&&(n.kind==="queryresponse"||n.kind==="readmodel")){
      [...(ADJ.inn.get(n.id)||[]),...(ADJ.out.get(n.id)||[])].map(x=>NODEBY.get(x)).filter(m=>m&&m.kind!=="akteur").forEach(m=>add(akteurSet(m)));}
    return s;}
  const DATEN_ARTEN=new Set(["valueobject","enum","konfig","hostsetting"]);
  // Arten, die mit ihrem Träger mitgehen, aber keinen Akteur-Rahmen begründen.
  const NUR_MIT=new Set(["valueobject","enum","konfig","hostsetting","dienst","codenode","llmnode","queryresponse"]);
  // Diagnose: je Domäne die Akteur-Reihenfolge und je Rahmen die Bausteine mit ihrer Akteur-Menge.
  window.deAkteure=()=>{const r={};NODEBY.forEach(n=>{if(!VIS.has(n.id))return;const d=domKey(n),o=akteurOrdnung(d),a=o.length?akteurVon(n):"—";
    const x=r[d]||(r[d]={ordnung:o,rahmen:{}});(x.rahmen[a]=x.rahmen[a]||[]).push(n.kind+":"+n.name+" {"+[...akteurSet(n)].join(",")+"}");});return r;};
  // Pipeline-/Reaktions-Handle: Dienst-Wechsel → dieser Akteur; sonst die Akteure seines Eingangs; ohne Kette (Ingress,
  //   Selbst-Tick ohne Akteur) die Akteure seiner Ausgaben (FileWatch → DateiErkannt → KameraSystem).
  function handleAkteure(art,hd){const s=new Set();
    if(art==="projektion"||art==="reader"){return null;}
    if(art==="pipeline"&&hd.akteur&&MODEL.akteure.some(a=>a.name===hd.akteur&&(a.dienste||[]).length))return new Set([hd.akteur]);
    mengeVon(art==="pipeline"?pipeEingang(hd):hd.event).forEach(x=>s.add(x));
    if(!s.size)[...(hd.sends||[]),...fristAus(hd),...(hd.emits||[]),...(hd.publishes||[])].forEach(o=>mengeVon(o).forEach(x=>s.add(x)));
    return s;}
  // Wer liest eine Store-Fn? Reader- und Pipeline-Handles, die sie als Fähigkeit nehmen.
  function fnLeser(fid){const s=new Set();
    MODEL.reader.forEach(rd=>(rd.handles||[]).forEach(hd=>{if((hd.fns||[]).includes(fid))mengeVon(hd.query).forEach(x=>s.add(x));}));
    MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{if((hd.fns||[]).includes(fid))(handleAkteure("pipeline",hd)||[]).forEach(x=>s.add(x));}));
    return s;}
  function storeAkteure(st){const s=new Set();(st.readFns||[]).forEach(f=>fnLeser(f._id).forEach(x=>s.add(x)));return s;}
  // Projektion: wer sie liest — über ihre Reader und die Stores, in die sie schreibt.
  function projektionAkteure(p){const s=new Set();
    MODEL.reader.filter(rd=>rd.projektion===p.name).forEach(rd=>(rd.handles||[]).forEach(hd=>mengeVon(hd.query).forEach(x=>s.add(x))));
    derivedStores(p).forEach(nm=>{const st=MODEL.stores.find(x=>x.name===nm);if(st)storeAkteure(st).forEach(x=>s.add(x));});
    return s;}
  // Die Karte, an der ein Rumpf (📝) hängt — Decider/Applier/Handle/Fn/Dienst.
  function codeBesitzerKnoten(cid){for(const n of NODEBY.values()){const r=n.ref;if(n.kind!=="codenode"&&n.kind!=="llmnode"&&r&&r.codeSrc===cid)return n;}return null;}
  // Reihenfolge der Akteur-Rahmen einer Domäne (§12.3): wer das Ding erschafft, dann wer worauf reagiert (Dienst-Wechsel),
  //   dann Art (Maschine → KI → Mensch), dann Name. Die Ansicht kann sie umsortieren (VIEW.akteurOrdnung[dom]).
  function akteurOrdnung(dom){let o=AKTORD.get(dom);if(o)return o;
    // Wer wirkt hier? Nur fachliche Bausteine zählen — ein Datentyp/Dienst/Code, den viele berühren, macht keinen eigenen Rahmen auf.
    const da=new Set();NODEBY.forEach(n=>{if(n.kind!=="akteur"&&!NUR_MIT.has(n.kind)&&VIS.has(n.id)&&!istInselLage(n)&&domKey(n)===dom)akteurSet(n).forEach(x=>da.add(x));});
    const art=nm=>(MODEL.akteure.find(a=>a.name===nm)||{}).art;
    const schoepfer=new Set();MODEL.records.forEach(r=>{if(r.kind==="command"&&r.istErzeugung){const d=MODEL.decider.find(x=>x.command===r.name),
      ag=d&&MODEL.aggregate.find(a=>a.name===d.aggregat);if(ag&&ag.namespace===dom)mengeVon(r.name).forEach(x=>schoepfer.add(x));}});
    const basis=[...da].sort((a,b)=>(schoepfer.has(a)?0:1)-(schoepfer.has(b)?0:1)||(ART_RANG[art(a)]??3)-(ART_RANG[art(b)]??3)||a.localeCompare(b));
    // Reagiert B per Dienst-Wechsel auf ein Event von A → A vor B.
    const vor=new Map();MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{const w=handleAkteure("pipeline",hd);
      if(!(hd.akteur&&w&&w.has(hd.akteur)))return;mengeVon(pipeEingang(hd)).forEach(a=>{if(a!==hd.akteur){if(!vor.has(hd.akteur))vor.set(hd.akteur,new Set());vor.get(hd.akteur).add(a);}});}));
    vertragsZusagen().forEach(({a,r})=>mengeVon(r.eingang).forEach(x=>{if(x!==a.name){if(!vor.has(a.name))vor.set(a.name,new Set());vor.get(a.name).add(x);}}));
    const aus=[],rest=[...basis];
    while(rest.length){const i=rest.findIndex(b=>[...(vor.get(b)||[])].every(a=>!rest.includes(a)||a===b));const k=i<0?0:i;aus.push(rest.splice(k,1)[0]);}
    const wunsch=((VIEW.akteurOrdnung||{})[dom]||[]).filter(x=>aus.includes(x));
    o=[...wunsch,...aus.filter(x=>!wunsch.includes(x))];AKTORD.set(dom,o);return o;}
  // Akteur-Rahmen einer Karte: der ERSTE Akteur der Domäne (Reihenfolge), zu dem sie gehört — Doppelungen stehen dort, wo
  //   sie zuerst auftreten. Ohne Akteur (bzw. Domäne ohne Akteure): OHNE_AKT.
  function akteurVon(n){const ord=akteurOrdnung(domKey(n));if(!ord.length)return OHNE_AKT;
    const s=akteurSet(n),h=(VIEW.heimAkteur||{})[n.id];
    if(!s.size&&h&&ord.includes(h))return h;
    return ord.find(a=>s.has(a))||OHNE_AKT;}
  // Domänen, in denen ein Akteur wirkt (sortiert wie die Domänen-Rahmen).
  function akteurDomaenen(name){if(!AKTDOM){AKTDOM=new Map();NODEBY.forEach(n=>{if(n.kind==="akteur"||NUR_MIT.has(n.kind))return;const d=domKey(n);
      akteurSet(n).forEach(a=>{if(!AKTDOM.has(a))AKTDOM.set(a,new Set());AKTDOM.get(a).add(d);});});}
    return [...(AKTDOM.get(name)||[])].sort(domOrd);}
  // 👤 AKTEUR: Name, Namespace, und EINE Liste — was er darf (IDarf<T>). ⊕ „+ darf" → passende Commands/Queries/Trigger
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
    (a.darf||[]).forEach(nm=>{const r=recByName(nm),t=!r&&MODEL.triggers.find(x=>trigName(x)===nm);
      const art=r?kindLabel(r.kind):t?"Trigger":"⚠ unbekannt";
      const s=anchorDot("darf");s.classList.add("o");reg("akt:darf:"+a._id+":"+nm,s,{type:"darf",dir:"out",akt:a._id,anker:true});
      body.append(h("div",{class:"slotrow o"},
        h("span",{class:"slotlbl",style:"flex:1;text-align:right"},nm+" · "+art),
        h("button",{class:"rm",title:"Recht entziehen",onclick:()=>{a.darf=a.darf.filter(x=>x!==nm);render();}},"✕"),s));});
    body.append(slotRow("darf","+ darf ⊕ (Command · Query · Trigger) ▶","r",{type:"darf",dir:"out",akt:a._id},"akt:darf:"+a._id+":open"));
    // Was in seinem Namen über eine Kette entsteht (abgeleitet, nicht deklariert): Pipeline/Prozess/Frist aus seinen Events.
    {const km=akteurMengen(),bew=[...km.entries()].filter(([nm,m])=>m.kette.has(a.name)&&!m.direkt.has(a.name)&&istEingangName(nm)).map(([nm])=>nm).sort();
      body.append(h("div",{class:"gsec",title:"abgeleitet: Commands/Trigger, die eine Pipeline, ein Prozess oder eine Frist aus Events dieses Akteurs erzeugt — kein IDarf nötig, am Tor nicht erlaubt"},"bewirkt über die Kette (abgeleitet)"));
      body.append(h("div",{class:"gp-row gp-leer",style:"cursor:default;white-space:normal"},bew.length?bew.join(", "):"— nichts"));}
    const wirkt=akteurDomaenen(a.name);
    if(wirkt.length){body.append(h("div",{class:"gsec"},"wirkt in"));
      body.append(h("div",{class:"gp-row",style:"display:flex;flex-wrap:wrap;gap:4px"},...wirkt.map(d=>h("button",{class:"akt-dom",title:"Rahmen "+domLabel(d)+" · "+a.name+" einpassen",
        onclick:()=>{const fr=world&&[...world.querySelectorAll(".grahmen.akt")].find(x=>x.dataset.ns===d&&x.dataset.akt===a.name);
          if(fr)einpassenRahmen({x1:fr.offsetLeft,y1:fr.offsetTop,x2:fr.offsetLeft+fr.offsetWidth,y2:fr.offsetTop+fr.offsetHeight});}},domLabel(d)))));}
    // Dienste (IAkteurDienst<A>, Code-Fakt): Werkzeug, mit dem der Akteur DRINNEN entscheidet — sie sind keine Akteure.
    if((a.dienste||[]).length){body.append(h("div",{class:"gsec",title:"interface X : IAkteurDienst<"+a.name+"> — der Dienst gehört diesem Akteur"},"⚙ Dienst: "+a.dienste.join(", ")));
      body.append(h("div",{class:"gsec",title:"Pipeline-Handles, die den Dienst als Parameter nehmen — sie entscheiden im Auftrag von "+a.name+" (CQRS060)"},"entscheidet in ▶"));
      const im=[];MODEL.pipelines.forEach(p=>(p.handles||[]).forEach((hd,hi)=>{if(hd.akteur===a.name)im.push([p,hd,hi]);}));
      im.forEach(([p,hd,hi])=>{const s=anchorDot("auftrag");s.classList.add("o");reg("akt:auftrag:"+a._id+":"+p._id+":"+hi,s,{type:"auftrag",dir:"out",akt:a._id,anker:true});
        body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right"},p.name+".Handle("+handleDisc(hd)+")"),s));});
      body.append(slotRow("auftrag","+ Handle ⊕ (Pipeline) ▶","r",{type:"auftrag",dir:"out",akt:a._id},"akt:auftrag:"+a._id+":open"));}
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
    (c.traegt||[]).forEach(t=>{const a=teilAkteur(t),s=anchorDot("traegt");s.classList.add("o");reg("cl:traegt:"+c._id+":"+t,s,{type:"traegt",dir:"out",client:c._id,anker:true});
      const n=teilZusagen(t).length;
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
    // Die Leitungen dieses Clients (je Ziel-Rahmen ein Bündel) — Klick fächert auf.
    const bs=clientBuendel(c);
    if(bs.length){body.append(h("div",{class:"gsec"},"🔌 Leitungen (je Ziel-Rahmen)"));
      bs.forEach(b=>{const row=h("div",{class:"gp-row",title:buendelText(b)},h("span",{class:"gp-t"},rahmenLabel(b.key)),
        h("span",{class:"gp-mark"},(b.durabel?"durabel · ":"")+buendelText(b)));row.onclick=()=>auffaechern(c,{keys:[b.key]});body.append(row);});}
  }
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
  }
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
  async function codeGet(a){if(a.kind==="slot")return fetch("/api/llm/rumpf?id="+encodeURIComponent(a.disc)).then(r=>r.json());
    const q=new URLSearchParams({kind:a.kind,namespace:a.namespace,disc:a.disc,datei:a.datei||""});
    return fetch("/api/editor/code?"+q).then(r=>r.json());}
  async function codeOpen(a){return fetch("/api/editor/open",{method:"POST",
    headers:{"content-type":"application/json"},body:JSON.stringify(a)}).then(r=>r.json());}
  // Poll-Registry (Datei→Browser, das Double-Binding): je sichtbarem Knoten ein Updater; bei Hash-Wechsel anwenden.
  let SYNC={}, WATCH=new Set();
  const ankerKey=a=>a?a.kind+":"+a.namespace+":"+a.disc:"";
  function syncReg(id,anker,apply,gid){if(INSP&&SYNC[id])return;SYNC[id]={anker,apply,hash:null,gid};}
  function imBlick(gid){if(!canvas||!world||!gid)return true;const g=GEO.get(gid);return !g||!g.weg;}
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
    const kid=konsolenId(c._id), v=kid?VOR[kid]:null;
    const voll=INSP;   // im Inspector: der ganze Rumpf statt der Kurzvorschau
    const prev=h("pre",{class:"gcodeprev"+(v?" vorschlag":""),style:"cursor:default",
      title:v?"🤖 Vorschlag — noch NICHT in der Datei; Kompilieren + Simulation nutzen ihn schon":(anker||kid)?"Spiegel der echten .cs (passiv)":"passiv"},
      codePreview(v?v.rumpf:c.text,undefined,voll));
    body.append(prev);
    if(v){   // 🤖 Vorschlag: sichtbar im Block, testbar in der Simulation, in die Datei erst mit ✓ Übernehmen
      const gut=v.ok&&!(v.befunde||[]).length;
      body.append(h("div",{class:"llmmeld "+(gut?"ok":"warn")},gut?"🤖 Vorschlag · ✓ geprüft · noch nicht in der Datei":"🤖 Vorschlag · "+(v.befunde||[]).length+" Befund(e) · noch nicht in der Datei"));
      const lauf=LAUF[kid];
      const ok=h("button",{class:"llmok",title:"In die echte .cs schreiben (mit „// 🤖 Prompt:“), Projekt bauen (Generatoren), Code + Kontexte neu einlesen",onclick:()=>llmUebernehmen(kid)},"✓ Übernehmen");
      const weg=h("button",{class:"codeadd",title:"Vorschlag verwerfen — der Datei-Rumpf gilt wieder",onclick:()=>llmVerwerfen(kid)},"✗ Verwerfen");
      if(lauf||EINLESEN){ok.disabled=true;weg.disabled=true;}
      body.append(h("div",{class:"llmrow"},ok,weg));}
    else if(kid&&ZULETZT[kid])body.append(h("div",{class:"llmrow"},h("span",{class:"llmmeld ok",style:"flex:1"},"🤖 übernommen"),
      h("button",{class:"codeadd",title:"Den alten Rumpf wörtlich wiederherstellen (nur solange die Datei seitdem unverändert ist)",onclick:()=>llmRueckgaengig(kid)},"↶ Rückgängig")));
    // Eingang: 🤖 LLM-Prompt-Node andocken (an JEDEM Code-Block, unabhängig vom Konsumenten-Typ).
    const pin=port("prompt");pin.classList.add("i");reg("code:prin:"+c._id,pin,{type:"prompt",dir:"in",codeBlock:c._id});
    const llm=MODEL.llmNodes.find(x=>x.promptZiel===c._id);
    const prow=h("div",{class:"slotrow"},pin,h("span",{class:"slotlbl",style:"flex:1"},llm?("◀ 🤖 "+(llm.name||"LLM")):"◀ 🤖 Prompt (bei Bedarf)"));
    body.append(prow);
    if(!llm)body.append(h("div",{class:"llmrow"},h("button",{class:"llmrun",style:"flex:1",title:"🤖 LLM-Knoten erzeugen und an diesen Code-Block andocken — dann Prompt schreiben und ▶",
      onclick:()=>llmAndocken(c._id)},"🤖 LLM-Knoten hinzufügen")));
    // ✎ IMMER (allgemeines Pattern) — ohne echte Datei: klare Meldung statt fehlendem Knopf.
    body.append(h("div",{class:"frow"},h("button",{class:"codeadd",
      title:anker?"Echte .cs im Editor öffnen":"Datei folgt — Scaffolder deckt diesen Rumpf noch nicht",
      onclick:async()=>{if(!anker){deFlash("◦ Datei folgt — Scaffolder deckt diesen Rumpf noch nicht",false);return;}
        WATCH.add(ankerKey(anker));   // ab jetzt diese eine Datei kontinuierlich spiegeln (Datei→Browser)
        const r=await codeOpen(anker).catch(()=>null);
        deFlash(r&&r.ok?"✎ geöffnet: "+r.pfad:"⚠ "+((r&&r.grund)||"SimHost offline"),!!(r&&r.ok));}},"✎ Im Editor öffnen")));
    // Datei→Browser: die Vorschau spiegelt den echten Rumpf — für JEDE Slot-Art (Decide/Apply über den Code-Anker,
    //   alle übrigen über den Slot-Schlüssel der Kontexte). Ein offener Vorschlag bleibt sichtbar, bis er entschieden ist.
    const spiegel=anker||(kid?{kind:"slot",namespace:"",disc:kid}:null);
    if(spiegel)syncReg(c._id,spiegel,d=>{c.text=d.body;if(!VOR[kid])prev.textContent=codePreview(d.body,undefined,voll);},"cn:"+c._id);
    body.append(slotRow("code","code ▶","r",{type:"code",dir:"out",codeNode:c._id},"code:out:"+c._id));
  }
  // 🤖 LLM-Knoten: Intent-Text; Vertrag wird aus dem verdrahteten Ziel abgeleitet; Ausgang code ▶.
  // 🤖 LLM-Node = PROMPT-QUELLE. `Prompt ▶` an den Eingang eines Code-Blocks; hier tippst du den Prompt.
  //   Wo der Block eine echte Datei hat, wird der Prompt als `// 🤖 Prompt:`-Kommentar in den Rumpf geschrieben
  //   (die einzige Code-Mutation, bei Bedarf). Double-Binding: extern geänderter Prompt spiegelt zurück.
  // 🤖 LLM-Knoten = CHAT an seinen Code-Block: der Verlauf zeigt die gesendeten Prompts (sonst nichts), darunter die Eingabe.
  //   Die 1. Nachricht ist der Auftrag, jede weitere passt den aktuellen Rumpf an. Jede Nachricht = ein Durchlauf
  //   (claude -p → Prüfung → .cs → Generatoren → neu einlesen). Der Verlauf lebt im Knoten (Board/Browser).
  function llmVerlauf(l){if(!Array.isArray(l.verlauf))l.verlauf=(l.intent||"").trim()?[{text:l.intent.trim()}]:[];return l.verlauf;}
  function llmNodeCard(body,l){
    body.append(nameInp(l,"name","LLM"));
    const block=l.promptZiel?MODEL.codeNodes.find(c=>c._id===l.promptZiel):null;
    const anker=block?codeAnker(block._id):null;
    const verlauf=llmVerlauf(l);
    body.append(h("div",{class:"gsec"},"Chat"+(block?" → 📝 "+(block.name||"Code"):" (an einen Code-Block andocken)")));
    const liste=h("div",{class:"llmchat"});
    if(!verlauf.length)liste.append(h("div",{class:"llmleer"},"Noch keine Prompts."));
    verlauf.forEach(e=>liste.append(h("div",{class:"llmmsg"},e.text)));
    body.append(liste);
    // Steht im Code schon ein „// 🤖 Prompt:“ und der Verlauf ist leer, ist das der erste (frühere) Prompt.
    if(anker)syncReg(l._id,anker,d=>{if(!l.verlauf.length&&(d.prompt||"").trim()){l.verlauf.push({text:d.prompt.trim()});l.intent=d.prompt.trim();neuZeichnen();}},"ln:"+l._id);
    const kid=block?konsolenId(block._id):null;
    const ta=h("textarea",{class:"code",rows:2,placeholder:verlauf.length?"Nächster Prompt, z. B. „lehne auch ab, wenn Betrag > 1000“":"Prompt, z. B. „Bestätige nur, wenn Betrag > 0.“"});
    body.append(ta);
    if(LLM_FOKUS===l._id)setTimeout(()=>{if(!ta.isConnected)return;LLM_FOKUS=null;ta.focus({preventScroll:true});
      const insp=ta.closest(".ginsp");if(insp){const d=ta.getBoundingClientRect().top-insp.getBoundingClientRect().top;insp.scrollTop+=d-insp.clientHeight/3;}
      else{const n=NODEBY.get("ln:"+l._id);if(n){centerOn(n);pulseNode(n);}}},60);
    if(!kid){body.append(h("div",{class:"llmmeld warn"},block?"Dieser Code-Block hat (noch) keinen Slot im Code.":"Erst an einen Code-Block andocken."));}
    else{
      const lauf=LAUF[kid], m=MELD[kid];
      const ohneKontext=!LLM_STATUS||!LLM_STATUS.index;
      // Senden: Prompt in den Verlauf, dann derselbe Durchlauf — 1. Prompt = Auftrag, jeder weitere = Anpassung des Rumpfs.
      const senden=async()=>{const text=ta.value.trim();if(!text||LAUF[kid]||EINLESEN)return;
        const erster=!verlauf.length;verlauf.push({text,zeit:new Date().toISOString()});if(erster)l.intent=text;ta.value="";neuZeichnen();
        await llmAusfuehren(kid,l.intent||text,erster?null:text,block.text);};
      ta.onkeydown=e=>{if(e.key==="Enter"&&(e.metaKey||e.ctrlKey)){e.preventDefault();senden();}};
      const knopf=h("button",{class:"llmrun",title:"Senden (Cmd/Ctrl+Enter): claude -p schreibt den Rumpf → Prüfung → .cs → Generatoren → neu einlesen",onclick:senden},"▶ Senden");
      if(lauf||EINLESEN||ohneKontext)knopf.disabled=true;
      body.append(h("div",{class:"llmrow"},knopf,h("a",{class:"codeadd",href:"/konsole#id="+encodeURIComponent(kid),target:"_blank",
        title:"Runden, Prompt und Token dieses Blocks",style:"text-decoration:none"},"Details ↗")));
      const zeile=lauf?{k:"",t:"● "+lauf.was+" …",lauf:lauf.seit}
        :EINLESEN?{k:"",t:"↻ Code + Kontexte werden neu eingelesen …"}
        :ohneKontext?{k:"warn",t:LLM_STATUS&&LLM_STATUS.aktualisierungLaeuft?"↻ Kontexte werden erzeugt (≈ 1 min) …":LLM_STATUS?"Keine Kontexte — „↻ Vom Graph laden“":"SimHost offline"}
        :m?m:null;
      if(zeile){const el=h("div",{class:"llmmeld "+(zeile.k||"")},zeile.t);if(zeile.lauf)el.dataset.lauf=zeile.lauf;body.append(el);}}
    body.append(slotRow("prompt","Prompt ▶","r",{type:"prompt",dir:"out",llm:l._id},"llm:prout:"+l._id));
  }
  // ══ 🤖 LLM-CODE-BLÖCKE — der ganze Weg am 🤖-Knoten, EIN Klick (▶):
  //   claude -p (Abo, keine API) füllt den Rumpf → Prüfung (Syntax; Decide/Apply: In-Memory-Kompilat mit den echten
  //   Generatoren, bis zu 3 Reparaturrunden) → GEPRÜFT: sofort in die echte .cs → Build des Laufzeit-Projekts (alle Code-
  //   Generatoren inkl. Proto-Prepass) → Code + Kontexte neu eingelesen. Hat der Block noch keine Methode (Decide/Apply),
  //   wird sie vorher als Platzhalter geschrieben („C# schreiben“). Nur ein UNGEPRÜFTER Kandidat bleibt Vorschlag (✓ am Block).
  //   Wahrheit ist die Datei; ein Vorschlag hängt am SLOT-Schlüssel (stabil über Neu-Einlesen) und ist lokal gesichert.
  const LLM_VKEY="bractor-llm-vorschlaege";
  let VOR={};try{VOR=JSON.parse(localStorage.getItem(LLM_VKEY)||"{}")||{};}catch(e){}
  const saveVor=()=>{try{localStorage.setItem(LLM_VKEY,JSON.stringify(VOR));}catch(e){}};
  const LAUF={}, MELD={}, ZULETZT={};   // Slot → läuft seit / letzte Meldung / gerade übernommen (Rückgängig anbieten)
  let EINLESEN=false, LLM_STATUS=null;
  function vorschlagFuer(codeId){const k=konsolenId(codeId);return k?VOR[k]:null;}
  const istSimulierbar=kid=>/^(decide|apply)\|/.test(kid);
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
    catch(e){r={ok:false,grund:"SimHost offline"};}
    // Noch keine Methode im Code (neu gezeichneter Decider/Applier): erst die Struktur schreiben (Platzhalter) + einlesen,
    //   dann derselbe Durchlauf. Leseseiten-Blöcke deckt der Scaffolder (noch) nicht — dort bleibt die Meldung.
    if(r&&!r.ok&&r.unbekannt&&istSimulierbar(kid)){
      LAUF[kid].was="legt die Methode an (C# schreiben) · liest ein";neuZeichnen();
      await deWrite();
      LAUF[kid].was="LLM schreibt · prüft · baut";neuZeichnen();
      try{r=await postLlm("/api/llm/ausfuehren",anfrage);}catch(e){r={ok:false,grund:"SimHost offline"};}
      if(r&&!r.ok&&r.unbekannt)r.grund="Methode konnte nicht angelegt werden — Decider/Applier vollständig verdrahtet (Command/Event, Aggregat, Ausgänge)?";}
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
      MELD[kid]={k:"warn",t:"Nicht geschrieben — Vorschlag mit "+(ende.befunde||[]).length+" Befund(en) nach "+info+": "+(ende.befunde||[])[0]+" · ↻ Anpassen oder ✓ trotzdem übernehmen"};
      if(istSimulierbar(kid))deCompile();}
    neuZeichnen();}

  async function llmUebernehmen(kid){const v=VOR[kid];if(!v||LAUF[kid])return;
    LAUF[kid]={seit:Date.now(),was:"schreibt + baut"};neuZeichnen();
    let r;try{r=await postLlm("/api/llm/uebernehmen",{id:kid,rumpf:v.rumpf,auftrag:v.auftrag,basisHash:v.basisHash,bauen:true});}
    catch(e){r={ok:false,grund:"SimHost offline"};}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+r.grund+(/Hash/.test(r.grund||"")?" → ▶ Neu ausführen":"")};neuZeichnen();return;}
    delete VOR[kid];saveVor();ZULETZT[kid]=true;
    MELD[kid]=r.bau&&r.bau.ok?{k:"ok",t:"✓ geschrieben: "+r.datei+":"+r.zeile+" · Build grün (Generatoren gelaufen)"}
      :{k:"err",t:"⚠ geschrieben, aber Build rot: "+(((r.bau&&r.bau.fehler)||[])[0]||"")+" — ↶ Rückgängig am Block"};
    await neuEinlesen();}

  async function llmRueckgaengig(kid){if(LAUF[kid])return;LAUF[kid]={seit:Date.now(),was:"stellt wieder her + baut"};neuZeichnen();
    let r;try{r=await postLlm("/api/llm/rueckgaengig",{id:kid,bauen:true});}catch(e){r={ok:false,grund:"SimHost offline"};}
    delete LAUF[kid];
    if(!r.ok){MELD[kid]={k:"err",t:"⚠ "+r.grund};neuZeichnen();return;}
    delete ZULETZT[kid];MELD[kid]={k:"",t:"↶ alter Rumpf wiederhergestellt"+(r.bau&&!r.bau.ok?" · Build rot":"")};await neuEinlesen();}

  function llmVerwerfen(kid){delete VOR[kid];saveVor();MELD[kid]={k:"",t:"Vorschlag verworfen — der Datei-Rumpf gilt."};neuZeichnen();if(istSimulierbar(kid))deCompile();}

  // Nach jedem Schreiben: EIN GraphExtractor-Lauf liest Modell + Kontexte neu → Spiegel, Nachbar-Kontexte und Simulation
  //   stehen auf dem neuen Code; solange sind ▶/✓ gesperrt (sonst sähe der nächste Block veraltete Nachbarn).
  async function neuEinlesen(){EINLESEN=true;neuZeichnen();try{await deReload();}finally{EINLESEN=false;await llmStatus();neuZeichnen();}}

  // 🤖 Alle ausführen: jeden 🤖-Knoten mit Auftrag nacheinander — jeder geprüfte Rumpf wird geschrieben + gebaut + eingelesen.
  window.deLlmAlle=async function(){const btn=document.getElementById("de-llmalle");
    const liste=MODEL.llmNodes.map(l=>({l,kid:l.promptZiel?konsolenId(l.promptZiel):null})).filter(x=>x.kid&&((llmVerlauf(x.l)[0]||{}).text||"").trim());
    if(!liste.length){deFlash("Kein 🤖-Knoten mit Auftrag",false);return;}
    btn.disabled=true;
    for(let i=0;i<liste.length;i++){btn.textContent="🤖 "+(i+1)+"/"+liste.length+" …";await llmAusfuehren(liste[i].kid,llmVerlauf(liste[i].l)[0].text,null);}
    btn.disabled=false;btn.textContent="🤖 Alle ausführen";
    const offen=Object.keys(VOR).length;deFlash("🤖 fertig"+(offen?" · "+offen+" ungeprüfte(r) Vorschlag/Vorschläge warten auf ✓":" · alles geschrieben"),!offen);};

  // Slot-Schlüssel der LLM-Konsole (wie GraphExtractor --kontexte ihn vergibt): Art|Besitzer|Disc.
  function konsolenId(codeId,m){const o=findCodeOwner(codeId,m);if(!o)return null;const r=o.ref;
    if(o.kind==="decider")return "decide|"+r.aggregat+"|"+r.command;
    if(o.kind==="applier")return "apply|"+r.aggregat+"|"+r.event;
    if(o.kind==="projektion"){const hd=(r.handles||[]).find(x=>x.codeSrc===codeId);return hd?"projektion|"+r.name+"|"+hd.event:null;}
    if(o.kind==="reader"){const hd=(r.handles||[]).find(x=>x.codeSrc===codeId);return hd?"reader|"+r.name+"|"+hd.query:null;}
    if(o.kind==="pipeline"){const hd=(r.handles||[]).find(x=>x.codeSrc===codeId);return hd?"pipeline|"+r.name+"|"+(hd.input||hd.event):null;}
    if(o.kind==="reaktion"){const hd=(r.handles||[]).find(x=>x.codeSrc===codeId);return hd?"reaktion|"+r.name+"|"+hd.event:null;}
    if(o.kind==="store"){const f=(r.writeFns||[]).concat(r.readFns||[]).find(x=>x.codeSrc===codeId);return f?"store|"+r.name+"|"+f.name:null;}
    return null;}
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
  const NODELABEL={akteur:"Akteur",client:"Client",auf:"Zusage",command:"Command",event:"Event",rejection:"Ablehnung",valueobject:"Value Object",enum:"Enum",aggregate:"Aggregat",decider:"Decider",applier:"Applier",saga:"Prozess",transition:"Regel",query:"Query",queryresponse:"Response",readmodel:"Read Model",store:"Store",projektion:"Projektion",reader:"Reader",reaktion:"Zusage",pipeline:"Pipeline",trigger:"Trigger",frist:"Frist",dienst:"Dienst",hostsetting:"HostSetting",codenode:"Code",llmnode:"LLM",state:"State",konfig:"Konfiguration",handle:"Handle",fn:"Store-Fn",funktion:"Funktion",auftrag:"Auftrag"};
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
      if(!d.rufe){const sa=argListe(d.sende,d.sendeArgs);if(sa.length)st.sendeArgumente=sa;}
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
    if(kind==="funktion"){MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{if(d.rufe===old)d.rufe=nv;}));
      MODEL.records.forEach(r=>{if(r.kind==="auftrag"&&r.funktion===old)r.funktion=nv;});}
    if(kind==="akteur"){MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.akteur===old)hd.akteur=nv;}));
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
        MODEL.hostSettings.forEach(hs=>{if(hs.konfig===old)hs.konfig=nv;});
        MODEL.pipelines.forEach(p=>{p.konfigs=(p.konfigs||[]).map(k=>k===old?nv:k);});
        MODEL.transitions.forEach(t=>(t.dann||[]).forEach(d=>{if(d.sende===old)d.sende=nv;if(d.kompensation===old)d.kompensation=nv;
          if(d.sendeAusdruck)d.sendeAusdruck=d.sendeAusdruck.replace(rx,nv);if(d.kompensationAusdruck)d.kompensationAusdruck=d.kompensationAusdruck.replace(rx,nv);}));
        // Reaktion-Handles: ausgelöste Commands mitziehen.
        MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{hd.sends=(hd.sends||[]).map(x=>x===old?nv:x);}));
        MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{hd.sends=(hd.sends||[]).map(x=>x===old?nv:x);(hd.fristen||[]).forEach(f=>{if(f.command===old)f.command=nv;});}));
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
        MODEL.pipelines.forEach(p=>(p.handles||[]).forEach(hd=>{if(hd.event===old)hd.event=nv;hd.publishes=(hd.publishes||[]).map(x=>x===old?nv:x);}));
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

  function graphNodes(){
    return [...MODEL.akteure.map(a=>({id:"akt:"+a._id,name:a.name,kind:"akteur",ref:a})),
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
            ...MODEL.llmNodes.map(l=>({id:"ln:"+l._id,name:l.name,kind:"llmnode",ref:l})),
            ...handleKnoten(),...fnKnoten()];
  }
  // ── AGGREGATSWEISE ANORDNUNG: jedes Aggregat ein gekachelter Block (eigenes Rollen-Mini-Layout),
  //    aggregat-übergreifende Knoten (Sagas/Pipelines/Trigger/Reaktionen/geteilte Typen) im „Geteilt"-Band.
  const SHARED_KEY="§geteilt";
  // Das AKTEUR-BAND (docs/konzept-akteure.md §8): ein eigener Block je Domäne — ganz links, auf der Eingangsseite, vor den
  //   Aggregat-Blöcken. Kommt DAZU, ersetzt keine Spalte. Domäne aus dem Graphen (alle IDarf-Ziele in einer Domäne → dort).
  const AKTEUR_KEY="§akteure";
  const ROLE_AKTEUR={akteur:0,auf:1};   // Spalte „Zusage" (Vertrag, Akteur-Konzept §3) neben dem Akteur
  // Rollen-Spalten innerhalb eines Aggregat-Blocks (links→rechts = Schreibfluss, dann Leseseite).
  //   Leseseite: Projektion (Hub) → ihre Handles → Store-Fns → Store/ReadModel; Query → Reader-Handle → Response, Reader (Hub).
  const ROLE_AGG={command:0,decider:1,aggregate:2,state:2,event:3,rejection:3,applier:4,valueobject:5,enum:5,projektion:6,"handle:projektion":7,"handle:reaktion":7,
    fn:8,store:9,readmodel:9,query:10,"handle:reader":11,queryresponse:12,reader:13};
  // Rollen-Spalten im Geteilt-Band.
  const ROLE_SHARED={saga:0,transition:1,auftrag:2,funktion:3,reaktion:2,"handle:reaktion":3,pipeline:4,"handle:pipeline":5,trigger:6,frist:6,dienst:7,hostsetting:7,konfig:7,valueobject:8,enum:8,command:9,event:9,rejection:9,
    fn:10,store:11,readmodel:11,"handle:projektion":12,projektion:13,"handle:reader":14,reader:15,query:14,queryresponse:16,codenode:17,llmnode:17};
  // Rolle eines Knotens: Handles je Besitzer-Art (Projektions- vs. Reader-Handle liegen in verschiedenen Spalten).
  const rolle=(n,map)=>{const k=n.kind==="handle"?"handle:"+n.own.kind:n.kind;return map[k]!==undefined?map[k]:99;};
  const rollenVon=blk=>blk===SHARED_KEY?ROLE_SHARED:blk===AKTEUR_KEY?ROLE_AKTEUR:ROLE_AGG;

  // Wer besitzt diesen 📝/🤖-Knoten? (Rumpf-Ziel) — für die Gruppen-Zuordnung.
  function findCodeOwner(id,m){m=m||MODEL;
    for(const d of m.decider||[]) if(d.codeSrc===id) return {kind:"decider",ref:d};
    for(const a of m.applier||[]) if(a.codeSrc===id) return {kind:"applier",ref:a};
    for(const p of m.projektionen||[]) if((p.handles||[]).some(h=>h.codeSrc===id)) return {kind:"projektion",ref:p};
    for(const r of m.reader||[]) if((r.handles||[]).some(h=>h.codeSrc===id)) return {kind:"reader",ref:r};
    for(const p of m.pipelines||[]) if((p.handles||[]).some(h=>h.codeSrc===id)) return {kind:"pipeline",ref:p};
    for(const s of m.stores||[]){if(((s.writeFns||[]).concat(s.readFns||[])).some(f=>f.codeSrc===id))return {kind:"store",ref:s};}
    for(const r of m.reaktionen||[]) if((r.handles||[]).some(h=>h.codeSrc===id)) return {kind:"reaktion",ref:r};
    for(const d of m.dienste||[]) if(d.codeSrc===id) return {kind:"dienst",ref:d};
    return null;
  }
  // Die Code-EINGÄNGE eines Knotens (je Eingang die codeSrc oder leer) — was der Knoten an Rumpf-Ports hat, auch unbelegt.
  //   null = der Knoten hat keinen Code-Eingang.
  function codeEingaenge(n){const r=n.ref;
    switch(n.kind){
      case "decider":case "applier":return [r.codeSrc||null];
      case "dienst":return r.extern?(r.codeSrc?[r.codeSrc]:null):[r.codeSrc||null];
      case "handle":case "fn":return [r.codeSrc||null];}   // Projektion/Reader/Reaktion/Pipeline/Store: der Code hängt an Handle/Fn
    return null;}
  // Aggregat-Zugehörigkeit eines Knotens (oder SHARED_KEY). Ableitung über Namespace + Verdrahtung.
  // ══ GRAPH-BASIS: alles liegt als Graph vor — Partition nach ECHTER Verbundenheit, nicht nach Namespace. ══
  // Eine einzige Kanten-Quelle auf KNOTEN-Ebene (Node-Id → Node-Id), gespiegelt zu drawEdges' Beziehungen.
  function boardEdges(){
    const E=[]; const rec=n=>"rec:"+n;
    const codeId=id=>MODEL.codeNodes.some(c=>c._id===id)?"cn:"+id:(MODEL.llmNodes.some(l=>l._id===id)?"ln:"+id:null);
    const push=(a,b)=>{if(a&&b)E.push([a,b]);};
    // Akteur → was er darf (Command/Query über die Record-Karte, Trigger über die Trigger-Karte).
    MODEL.akteure.forEach(a=>(a.darf||[]).forEach(nm=>{if(recByName(nm))push("akt:"+a._id,rec(nm));
      else{const t=MODEL.triggers.find(x=>trigName(x)===nm);if(t)push("akt:"+a._id,"tg:"+t._id);}}));
    // Vertrag (Akteur-Konzept §3): Event → Zusage → Commands; die Zusage hängt an ihrem Akteur (Hub-Kante wie Handle → Besitzer).
    vertragsZusagen().forEach(({a,r,id})=>{push(id,"akt:"+a._id);if(recByName(r.eingang))push(rec(r.eingang),id);
      (r.ausgaenge||[]).forEach(c=>{if(recByName(c))push(id,rec(c));});});
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
      (t.dann||[]).forEach(d=>{if(recByName(d.sende))push("tr:"+t._id,rec(d.sende));if(recByName(d.kompensation))push("tr:"+t._id,rec(d.kompensation));
        const fk=d.rufe&&MODEL.funktionen.find(f=>f.name===d.rufe);if(fk)push("tr:"+t._id,"fk:"+fk._id);});});
    MODEL.funktionen.forEach(f=>{if(recByName(f.auftrag))push(rec(f.auftrag),"fk:"+f._id);
      (f.ergebnisse||[]).forEach(e=>{if(recByName(e))push("fk:"+f._id,rec(e));});});
    MODEL.readModels.forEach(rm=>{const st=MODEL.stores.find(s=>s.name===rm.store);if(st)push("rm:"+rm._id,"sto:"+st._id);});
    // Store-Fns: eigene Knoten am Store (Hub-Kante Fn → Store), ihr Impl-Rumpf hängt an der Fn.
    MODEL.stores.forEach(st=>(st.writeFns||[]).concat(st.readFns||[]).forEach(fn=>{push("fn:"+fn._id,"sto:"+st._id);if(fn.codeSrc)push(codeId(fn.codeSrc),"fn:"+fn._id);}));
    // Handles: eigene Knoten am Besitzer (Hub-Kante Handle → Besitzer); Eingang → Handle → Ausgänge; Rumpf → Handle.
    const HID=new Map();Object.entries(HANDLE_ART).forEach(([k,[coll,pre]])=>(MODEL[coll]||[]).forEach(o=>{const ids=handleIds(o,pre+":"+o._id);(o.handles||[]).forEach((hd,hi)=>HID.set(hd,ids[hi]));}));
    const H=hd=>HID.get(hd);
    MODEL.projektionen.forEach(p=>(p.handles||[]).forEach(hd=>{push(H(hd),"prj:"+p._id);
      if(recByName(hd.event))push(rec(hd.event),H(hd));
      (hd.fns||[]).forEach(fid=>{if(fnById(fid))push(H(hd),"fn:"+fid);});
      (hd.publishes||[]).forEach(ev=>{if(recByName(ev))push(H(hd),rec(ev));});
      if(hd.codeSrc)push(codeId(hd.codeSrc),H(hd));}));
    MODEL.reader.forEach(r=>{const p=r.projektion&&MODEL.projektionen.find(x=>x.name===r.projektion);if(p)push("rdr:"+r._id,"prj:"+p._id);
      (r.handles||[]).forEach(hd=>{push(H(hd),"rdr:"+r._id);
        if(recByName(hd.query))push(rec(hd.query),H(hd));
        (hd.fns||[]).forEach(fid=>{if(fnById(fid))push(H(hd),"fn:"+fid);});
        (hd.responses||[]).forEach(resp=>{if(recByName(resp))push(H(hd),rec(resp));});
        if(hd.codeSrc)push(codeId(hd.codeSrc),H(hd));});});
    MODEL.reaktionen.forEach(r=>(r.handles||[]).forEach(hd=>{push(H(hd),"rk:"+r._id);
      if(recByName(hd.event))push(rec(hd.event),H(hd));
      (hd.sends||[]).forEach(c=>{if(recByName(c))push(H(hd),rec(c));});
      (hd.publishes||[]).forEach(ev=>{if(recByName(ev))push(H(hd),rec(ev));});
      if(hd.codeSrc)push(codeId(hd.codeSrc),H(hd));}));
    MODEL.pipelines.forEach(p=>{(p.handles||[]).forEach(hd=>{push(H(hd),"pl:"+p._id);
        if(hd.inputKind==="event"&&recByName(hd.event))push(rec(hd.event),H(hd));
        if(hd.inputKind==="trigger"){const pr=hd.prod||(hd.trigId?{k:"tg",id:hd.trigId}:null);
          if(pr&&pr.k==="tg")push("tg:"+pr.id,H(hd));
          else if(pr&&pr.k==="pl"){const q=MODEL.pipelines.find(x=>x._id===pr.plId),qh=q&&(q.handles||[])[pr.hi];if(qh)push(H(qh),H(hd));}}
        // Ausgänge nach Typ: Command (sofort oder per Frist), veröffentlichtes Event — je über die Nachrichten-Karte.
        (hd.sends||[]).forEach(c=>{if(recByName(c))push(H(hd),rec(c));});
        (hd.fristen||[]).forEach(f=>{if(recByName(f.command))push(H(hd),rec(f.command));});
        (hd.publishes||[]).forEach(ev=>{if(recByName(ev))push(H(hd),rec(ev));});
        (hd.fns||[]).forEach(fid=>{if(fnById(fid))push(H(hd),"fn:"+fid);});
        // ge-yieldeter Trigger → die Trigger-Karte (von dort an die eine Pipeline); ohne Karte (Entwurf) direkt an den Handle.
        (hd.emits||[]).forEach(tn=>{const t=MODEL.triggers.find(x=>trigName(x)===tn);if(t){push(H(hd),"tg:"+t._id);return;}
          MODEL.pipelines.forEach(q=>{if(q._id!==p._id)(q.handles||[]).forEach(qh=>{if(qh.inputKind==="trigger"&&qh.input===tn)push(H(hd),H(qh));});});});
        // Selbst<T> → der Self-Handle derselben Pipeline (Tick/Timeout-Schleife).
        (hd.schedules||[]).forEach(sc=>(p.handles||[]).forEach(qh=>{if(qh.inputKind==="self"&&qh.selfName===sc.name&&qh!==hd)push(H(hd),H(qh));}));
        if(hd.codeSrc)push(codeId(hd.codeSrc),H(hd));
        {const a=hd.akteur&&MODEL.akteure.find(x=>x.name===hd.akteur);if(a)push("akt:"+a._id,H(hd));}});
      (p.dienste||[]).forEach(dn=>{const d=MODEL.dienste.find(x=>(x.vertrag||x.name)===dn);if(d)push("di:"+d._id,"pl:"+p._id);});});
    MODEL.frists.forEach(f=>{(f.plant||[]).forEach(ev=>{if(recByName(ev))push(rec(ev),"fr:"+f._id);});
      (f.storniert||[]).forEach(ev=>{if(recByName(ev))push(rec(ev),"fr:"+f._id);});
      if(recByName(f.sendet))push("fr:"+f._id,rec(f.sendet));
      if(f.dauerSetting){const hs=MODEL.hostSettings.find(x=>x.name===f.dauerSetting);if(hs)push("hs:"+hs._id,"fr:"+f._id);}});
    MODEL.dienste.forEach(d=>{if(d.codeSrc)push(codeId(d.codeSrc),"di:"+d._id);});
    // 🤖 → 📝: der LLM-Knoten hängt an seinem Code-Block (Prompt-Kante, wie drawEdges sie zeichnet).
    MODEL.llmNodes.forEach(l=>{if(l.promptZiel&&MODEL.codeNodes.some(c=>c._id===l.promptZiel))push("ln:"+l._id,"cn:"+l.promptZiel);});
    // Betrieb: HostSetting → Konfigurations-Record (Feld) → Pipeline, die ihn per Konstruktor injiziert.
    MODEL.hostSettings.forEach(hs=>{if(hs.konfig&&recByName(hs.konfig))push("hs:"+hs._id,rec(hs.konfig));});
    MODEL.pipelines.forEach(p=>(p.konfigs||[]).forEach(k=>{if(recByName(k))push(rec(k),"pl:"+p._id);}));
    // Store-API: Records in Fn-Parametern/-Rückgaben (Transfer-Typen wie ImagePairStatistik) gehören an ihre Fn.
    MODEL.stores.forEach(st=>(st.writeFns||[]).concat(st.readFns||[]).forEach(fn=>{
      [fn.rueckgabe,...(fn.params||[]).map(x=>x.typ)].forEach(t=>((t||"").match(/[A-Za-z_]\w*/g)||[]).forEach(n=>{
        const r=recByName(n);if(r&&r.kind!=="command")push(rec(n),"fn:"+fn._id);}));}));
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
    byC.forEach(nodes=>{const fach=nodes.filter(n=>n.kind!=="codenode"&&n.kind!=="llmnode"&&n.kind!=="client");   // ein Client hängt über seinen Vertrag, nicht über Kanten
      if(fach.length<=ISLE_MAX && !fach.some(n=>n.kind==="aggregate"))fach.forEach(n=>ids.add(n.id));});
    return {comp,byC,ids};
  }
  // Aggregat-Untergruppe (oder null = Brücke: cross-cutting, gehört keinem Aggregat).
  function subGroupOf(n){const g=groupKeyOf(n);return g===SHARED_KEY?null:g;}

  function groupKeyOf(n){
    const k=n.kind, r=n.ref;
    if(k==="akteur"||k==="auf") return AKTEUR_KEY;
    if(k==="aggregate") return r.name;
    if(k==="handle"||k==="fn") return groupKeyOf(n.own);   // Handle/Fn gehören zu ihrem Besitzer
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
    if(k==="codenode"){const o=findCodeOwner(r._id);return o?groupKeyOf(o):SHARED_KEY;}
    if(k==="llmnode"){const o=r.promptZiel&&findCodeOwner(r.promptZiel);return o?groupKeyOf(o):SHARED_KEY;}
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
    // Knoten ohne Position (neu angelegt / neu sichtbar): im Raster hat jeder Knoten seine feste Zeile → neu packen.
    if(all.some(n=>!positioned(n)))force=true;
    // Raster-Signatur (sichtbare Knoten + Zahl ihrer Code-Eingänge): ändert sie sich (Handle/Fn dazu, Art aus-/eingeblendet),
    //   stimmen die reservierten Zeilen nicht mehr → neu packen. Gleiche Signatur = Handanordnung bleibt.
    const LAY=KPOS[VIEW.details?"d":"k"]||(KPOS[VIEW.details?"d":"k"]={});
    // Der Akteur-Rahmen gehört zur Lage (Domäne × Akteur): wandert eine Karte in einen anderen Akteur-Rahmen, neu packen.
    const sig=hash(all.map(n=>n.id+":"+((codeEingaenge(n)||[]).length)+":"+domKey(n)+":"+akteurVon(n)).join("|")+"#"+(VIEW.aus||[]).join(",")+"#"+(VIEW.domNeu||[]).join(",")
      +"#"+JSON.stringify(VIEW.akteurOrdnung||{})
      // Clients stapeln sich nach ihrer Höhe (die Anschlussleiste wächst mit dem Vertrag) → ändert sie sich, neu packen.
      +"#"+all.filter(n=>n.kind==="client").map(n=>n.id+":"+Math.round(sz(n).h/20)).join(","));
    if(LAY.__sig!==sig)force=true;
    // Wie viele Knotenpaare überlappen aktuell deutlich? (früher Abbruch, sobald „viele").
    const overlaps=()=>{const b=all.map(n=>{const s=sz(n),p=P(n);return {x:p.x||0,y:p.y||0,w:s.w,h:s.h};});let c=0;
      for(let i=0;i<b.length;i++)for(let j=i+1;j<b.length;j++){const A=b[i],B=b[j];
        if(Math.min(A.x+A.w,B.x+B.w)-Math.max(A.x,B.x)>16 && Math.min(A.y+A.h,B.y+B.h)-Math.max(A.y,B.y)>16){if(++c>all.length)return c;}}
      return c;};
    // Ein arrangiertes Board (Handanordnung) NICHT neu würfeln — AUSSER es überlappt grob (alter/kaputter
    // Stand aus localStorage/board-model.json → heilen). „▦ Neu anordnen" ruft mit force=true.
    if(!force && all.some(positioned) && overlaps() < Math.max(3, Math.floor(all.length*0.12))) return;

    const COLGAP=48,ROWGAP=26,SHELFGAP=150,COMPGAP=240,KGAP=90,LEER_W=640,LEER_H=110;   // RP/RK: Rahmen-Rand/-Kopf (zeichneRahmen)
    const isCode=n=>n.kind==="codenode"||n.kind==="llmnode";
    // ── ZEILENRASTER: jede Karte belegt genau eine Rasterzeile (Höhe = höchste sichtbare Karte + Abstand), alles fluchtet.
    //   Ein Knoten mit Code-Eingängen (Decider, Applier, Projektion, Reader, Reaktion, Pipeline, Store, Dienst) belegt
    //   1 + je Eingang GENAU 2 Zeilen: 📝 Code-Block und 🤖 LLM-Platz — der LLM-Platz ist immer reserviert (auch ohne
    //   LLM-Knoten), ein leerer Code-Eingang ebenso. Dadurch sind alle Besitzer gleich getaktet; Command/Event/Ablehnung/
    //   Query … stehen in der Zeile ihres Partners (Decider, Applier, Reader …). Ausgeblendete Arten reservieren nichts.
    const EINZUG=16, ZGAP=12;
    const codeAn=!istAus("codenode"), llmAn=codeAn&&!istAus("llmnode");
    const ZEILE=Math.max(28,...all.filter(n=>n.kind!=="client").map(n=>sz(n).h))+ZGAP;   // die (hohe) Anschlussleiste eines Clients taktet nicht mit
    const layoutBlock=(nodes,roleMap)=>{
      const rest=nodes.filter(n=>!isCode(n)), codes=nodes.filter(n=>n.kind==="codenode"), llms=nodes.filter(n=>n.kind==="llmnode");
      const codeById=new Map(codes.map(c=>[c.ref._id,c]));
      const llmByCode=new Map(), orphanLlm=[];
      llms.forEach(l=>{const z=l.ref.promptZiel;if(z&&codeById.has(z)){if(!llmByCode.has(z))llmByCode.set(z,[]);llmByCode.get(z).push(l);}else orphanLlm.push(l);});
      // Code-Plätze je Besitzer in Eingangs-Reihenfolge (Handle/Fn); null = leerer Eingang (Platz bleibt reserviert).
      const genutzt=new Set();
      const plaetze=n=>{if(!codeAn)return [];
        const src=codeEingaenge(n);if(!src)return [];
        const ps=src.map(id=>{const c=id&&codeById.get(id);if(c)genutzt.add(c.ref._id);return c||null;});
        codes.forEach(c=>{if(genutzt.has(c.ref._id))return;const o=findCodeOwner(c.ref._id);if(o&&o.ref===n.ref){genutzt.add(c.ref._id);ps.push(c);}});
        return ps;};
      const PL=new Map();rest.forEach(n=>{const p=plaetze(n);if(p.length)PL.set(n.id,p);});
      const hoehe=n=>1+(PL.get(n.id)||[]).reduce((s,c)=>s+(llmAn?1+Math.max(1,c?(llmByCode.get(c.ref._id)||[]).length:1):1),0);
      const rawOf=n=>rolle(n,roleMap);
      const colRoles=[...new Set(rest.map(rawOf))].sort((a,b)=>a-b);
      const byRole=new Map(colRoles.map(r=>[r,[]]));rest.forEach(n=>byRole.get(rawOf(n)).push(n));
      // Belegung je Spalte (Zeilen-Set) + Zeile je Knoten.
      const belegt=new Map(colRoles.map(r=>[r,new Set()])), zeile=new Map();
      const frei=(role,z,len)=>{const b=belegt.get(role);for(let i=0;i<len;i++)if(b.has(z+i))return false;return true;};
      const setzeZ=(role,n,z,len)=>{const b=belegt.get(role);for(let i=0;i<len;i++)b.add(z+i);zeile.set(n.id,z);};
      const ab=(role,z,len)=>{while(!frei(role,z,len))z++;return z;};
      const imBlock=new Set(rest.map(n=>n.id));
      const nachbarZ=id=>[...(ADJ.inn.get(id)||[]),...(ADJ.out.get(id)||[])].filter(x=>imBlock.has(x)&&zeile.has(x)).map(x=>zeile.get(x));
      // Wunschzeile: Event → sein Applier (sonst der erzeugende Decider); sonst die oberste Zeile eines platzierten Nachbarn.
      const wunsch=n=>{if(n.kind==="event"){const ap=(ADJ.out.get(n.id)||[]).filter(x=>x.startsWith("app:")&&zeile.has(x)).map(x=>zeile.get(x));if(ap.length)return Math.min(...ap);}
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
      // Spalten → x; Code/LLM eingerückt unter dem Besitzer.
      let cx=0,maxZ=0;const placed=[];
      colRoles.forEach(role=>{let w=120;
        byRole.get(role).forEach(n=>{const z=zeile.get(n.id),s=sz(n);placed.push({n,rx:cx,ry:z*ZEILE});
          // Besitzer: Breite für die Einrückung von Code UND LLM immer reservieren — ein neuer 🤖 verschiebt keine Spalte.
          w=Math.max(w,s.w+(PL.has(n.id)?(llmAn?2:1)*EINZUG:0));
          let zz=z+1;(PL.get(n.id)||[]).forEach(c=>{
            if(c){placed.push({n:c,rx:cx+EINZUG,ry:zz*ZEILE});w=Math.max(w,sz(c).w+EINZUG);}
            zz++;if(!llmAn)return;
            const ls=c?(llmByCode.get(c.ref._id)||[]):[];
            ls.forEach((l,i)=>{placed.push({n:l,rx:cx+2*EINZUG,ry:(zz+i)*ZEILE});w=Math.max(w,sz(l).w+2*EINZUG);});
            zz+=Math.max(1,ls.length);});
          maxZ=Math.max(maxZ,zz);});
        cx+=w+COLGAP;});
      // Code-Blöcke ohne Besitzer (frei angelegt) + LLM-Knoten ohne Block: eigene Spalte, ebenfalls im Raster.
      const orphan=codes.filter(c=>!genutzt.has(c.ref._id));
      if(orphan.length||orphanLlm.length){let w=120,z=0;
        orphan.forEach(c=>{placed.push({n:c,rx:cx,ry:z*ZEILE});w=Math.max(w,sz(c).w);z++;
          (llmByCode.get(c.ref._id)||[]).forEach(l=>{placed.push({n:l,rx:cx+EINZUG,ry:z*ZEILE});w=Math.max(w,sz(l).w+EINZUG);z++;});});
        orphanLlm.forEach(l=>{placed.push({n:l,rx:cx,ry:z*ZEILE});w=Math.max(w,sz(l).w);z++;});
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
    const region=ns=>{const eigen=proDom.get(ns)||[],kinder=(BAUM.kinder.get(ns)||[]).map(region);
      const placed=[],leer=[],aktLeer=[];let y=RK+RP,w=0;
      const insel=eigen.filter(istInselLage),haupt=eigen.filter(n=>!istInselLage(n));
      // Domäne × Akteur (§12): je Akteur (Reihenfolge der Domäne) ein eigener Rahmen mit dem bewährten Block-Layout darin,
      //   untereinander; „ohne Akteur" zuletzt. Eine Domäne ohne Akteure bleibt, wie sie war (ein Block-Layout).
      const ord=akteurOrdnung(ns);
      if(haupt.length&&!ord.length){const c=layoutComp(haupt);c.placed.forEach(p=>placed.push({n:p.n,rx:RP+p.rx,ry:y+p.ry}));y+=c.h;w=c.w;}
      else if(ord.length){const gr=new Map();haupt.forEach(n=>{const a=akteurVon(n);if(!gr.has(a))gr.set(a,[]);gr.get(a).push(n);});
        let erst=true;
        [...ord,OHNE_AKT].forEach(a=>{const g=gr.get(a);if(!g&&a===OHNE_AKT)return;if(!erst)y+=AGAP;erst=false;
          if(!g){aktLeer.push({ns,akt:a,rx:RP,ry:y});y+=AK;w=Math.max(w,AKLEER_W);return;}   // leer: nur Kopf + „↥ auch“
          const c=layoutComp(g);c.placed.forEach(p=>placed.push({n:p.n,rx:RP+AP+p.rx,ry:y+AK+AP+p.ry}));
          y+=AK+2*AP+c.h;w=Math.max(w,c.w+2*AP);});}
      if(insel.length){if(haupt.length)y+=SHELFGAP/2;const IW=Math.max(700,w);let ix=0,iy=0,rh=0;
        insel.forEach(n=>{const s=sz(n);if(ix>0&&ix+s.w>IW){iy+=rh+ROWGAP;ix=0;rh=0;}placed.push({n,rx:RP+ix,ry:y+iy});ix+=s.w+COLGAP;rh=Math.max(rh,s.h);w=Math.max(w,ix-COLGAP);});
        y+=iy+rh;}
      if(!eigen.length&&!kinder.length){leer.push({ns,rx:0,ry:0});w=LEER_W;y+=LEER_H;}
      if(kinder.length){if(eigen.length||aktLeer.length)y+=KGAP;const d=shelf(kinder,KGAP,1.2);
        kinder.forEach(k=>{k.placed.forEach(p=>placed.push({n:p.n,rx:RP+k.rx+p.rx,ry:y+k.ry+p.ry}));k.leer.forEach(l=>leer.push({ns:l.ns,rx:RP+k.rx+l.rx,ry:y+k.ry+l.ry}));
          k.aktLeer.forEach(l=>aktLeer.push({ns:l.ns,akt:l.akt,rx:RP+k.rx+l.rx,ry:y+k.ry+l.ry}));});
        y+=d.h;w=Math.max(w,d.w);}
      return {placed,leer,aktLeer,w:w+2*RP,h:y+RP};};
    const regions=BAUM.oben.filter(ns=>!istClientDom(ns)).map(region);
    shelf(regions,COMPGAP,1.05);
    // Clients (docs/konzept-akteure.md §4): je Client ein Rahmen, untereinander RECHTS neben allen Domänen (Außenwelt).
    {const CLGAP=520;let rechts=0,cy=0;regions.forEach(d=>rechts=Math.max(rechts,d.rx+d.w));
      BAUM.oben.filter(istClientDom).forEach(ns=>{const d=region(ns);d.rx=rechts+(regions.length?CLGAP:0);d.ry=cy;
        // Höhe aus der echten Karte (die Leiste ist höher als eine Rasterzeile).
        const hh=Math.max(d.h,...(proDom.get(ns)||[]).map(n=>sz(n).h+RK+2*RP+SPK));cy+=hh+KGAP;regions.push(d);});}
    regions.forEach(d=>{d.placed.forEach(p=>setze(p.n,d.rx+p.rx,d.ry+p.ry));
      d.leer.forEach(l=>{LAY["§leer:"+l.ns]={x:d.rx+l.rx,y:d.ry+l.ry};});
      d.aktLeer.forEach(l=>{LAY["§aktleer:"+l.ns+"|"+l.akt]={x:d.rx+l.rx,y:d.ry+l.ry};});});
    LAY.__sig=sig;if(VIEW.kompakt)speichereKpos();
  }
  // Karten auf der Fläche sind immer kompakt (Kopf + Kurzfassung); das Formular lebt im Panel.
  const istZu=()=>true;   // Karten sind immer kompakt — bearbeitet wird im Panel
  function nodeEditor(n){
    const zu=istZu(n);
    const el=h("div",{class:"gnode2 n-"+n.kind+(zu?" collapsed":"")+(ISLE.has(n.id)?" island":"")
      +(n.kind==="codenode"&&vorschlagFuer(n.ref._id)?" llmvor":"")+((n.own?n.own.ref:n.ref).ungeschrieben?" ungeschrieben":((m=>m.ausCode===false||(m.ausCode===undefined&&MERGE_KEYS[kollektionVon(n.own?n.own.kind:n.kind)]))(n.own?n.own.ref:n.ref)?" entwurf":""))});el.dataset.id=n.id;
    const pos=P(n);el.style.left=(pos.x||0)+"px";el.style.top=(pos.y||0)+"px";
    const title=NODELABEL[n.kind]||n.kind;
    const head=h("div",{class:"ghead"},
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
    else if(n.kind==="funktion")funktionCard(body,n.ref);
    else if(n.kind==="hostsetting")hostSettingCard(body,n.ref);
    else if(n.kind==="akteur")akteurCard(body,n.ref);
    else if(n.kind==="client")clientCard(body,n.ref);
    else if(n.kind==="auf")aufCard(body,n);
    else if(n.kind==="codenode")codeNodeCard(body,n.ref);
    else if(n.kind==="llmnode")llmNodeCard(body,n.ref);
    else if(n.kind==="handle")handleCard(body,n);
    else if(n.kind==="fn")fnCard(body,n);
    else recordCard(body,n.ref);
  }
  function delNode(n){const k=n.kind,ref=n.ref;
    // Ein gelöschter Eingang (Command/Query/Trigger) verschwindet auch aus den Befugnissen (IDarf) der Akteure.
    {const nm=k==="trigger"?trigName(ref):(MODEL.records.includes(ref)?ref.name:null);if(nm)MODEL.akteure.forEach(a=>{if((a.darf||[]).includes(nm))a.darf=ohneX(a.darf,nm);
      // … und aus den Verträgen: als Eingang fällt die ganze Zusage weg, als Ausgang nur der Ausgang.
      if((a.vertrag||[]).length)a.vertrag=a.vertrag.filter(r=>r.eingang!==nm).map(r=>(r.ausgaenge||[]).includes(nm)?{...r,ausgaenge:ohneX(r.ausgaenge,nm)}:r);});
      // … und aus dem Rand der Clients (sendet/fragt/kenntnis).
      MODEL.clients.forEach(c=>{["sendet","fragt","kenntnis"].forEach(f=>{if((c[f]||[]).includes(nm))c[f]=ohneX(c[f],nm);});});}
    {const nm=MODEL.records.includes(ref)&&(ref.kind==="event"||ref.kind==="rejection")?ref.name:null;
      if(nm)MODEL.clients.forEach(c=>{if((c.kenntnis||[]).includes(nm))c.kenntnis=ohneX(c.kenntnis,nm);});}
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
    else if(k==="pipeline")MODEL.pipelines.splice(MODEL.pipelines.indexOf(ref),1);
    else if(k==="trigger")MODEL.triggers.splice(MODEL.triggers.indexOf(ref),1);
    else if(k==="frist")MODEL.frists.splice(MODEL.frists.indexOf(ref),1);
    else if(k==="dienst")MODEL.dienste.splice(MODEL.dienste.indexOf(ref),1);
    else if(k==="funktion")MODEL.funktionen.splice(MODEL.funktionen.indexOf(ref),1);
    else if(k==="hostsetting")MODEL.hostSettings.splice(MODEL.hostSettings.indexOf(ref),1);
    else if(k==="akteur")MODEL.akteure.splice(MODEL.akteure.indexOf(ref),1);
    else if(k==="client"){MODEL.clients.splice(MODEL.clients.indexOf(ref),1);AUF=null;}
    else if(k==="auf"){const v=n.own.ref.vertrag||[];v.splice(v.indexOf(ref),1);}
    else if(k==="codenode")MODEL.codeNodes.splice(MODEL.codeNodes.indexOf(ref),1);
    else if(k==="llmnode")MODEL.llmNodes.splice(MODEL.llmNodes.indexOf(ref),1);
    else if(k==="handle"){const hs=n.own.ref.handles||[];hs.splice(hs.indexOf(ref),1);}
    else if(k==="fn"){const st=n.own.ref;["writeFns","readFns"].forEach(a=>{const i=(st[a]||[]).indexOf(ref);if(i>=0)st[a].splice(i,1);});
      MODEL.projektionen.concat(MODEL.reader,MODEL.pipelines).forEach(o=>(o.handles||[]).forEach(hd=>{hd.fns=(hd.fns||[]).filter(x=>x!==ref._id);}));}
    else MODEL.records.splice(MODEL.records.indexOf(ref),1);
    render();}

  // Koordinaten: Welt = unskaliert; canvas = Bildschirm.
  // Nur der Transform wird sofort geschrieben (billig, der Browser bündelt ihn ohnehin je Frame). Alles daraus Abgeleitete
  //   (Minimap-Rahmen, Culling, Schatten-LOD) läuft gebündelt EINMAL pro Frame (sichtFrame) — nie je Wheel-/Pointer-Event,
  //   und ohne Layout-Lesen: die Canvas-Größe kommt aus dem ResizeObserver-Cache (CV), die Knoten-Geometrie aus GEO.
  function applyPan(){if(!world)return;world.style.transform="translate("+PAN.x+"px,"+PAN.y+"px) scale("+PAN.s+")";pruefeLod();planeSicht();}
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
    const arten=Object.keys(NODELABEL).filter(k=>zahl.has(k)||k==="codenode"||k==="llmnode").concat([...zahl.keys()].filter(k=>!NODELABEL[k]));
    const MENU={codenode:"📝 Code",llmnode:"🤖 LLM"};
    p.append(h("h4",{},h("span",{},"👁 Knotenarten"),
      h("span",{style:"cursor:pointer;color:#8a93a7",title:"Schließen",onclick:()=>{FILTER_OPEN=false;renderFilterPanel();}},"✕")));
    p.append(h("div",{class:"mm-q"},
      h("button",{onclick:()=>{VIEW.aus=[];speichereAnsicht();render();}},"Alle"),
      h("button",{title:"Nur die Fachknoten — 📝 Code und 🤖 LLM ausblenden",onclick:()=>{VIEW.aus=["codenode","llmnode"];speichereAnsicht();render();}},"Ohne Code")));
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

  // „aus" = der EINE Ausgangs-Port eines Pipeline-Handles: passt an jeden Nachrichten-Eingang („◀ kommt aus" von Command,
  //   Event/transientem Event, Trigger-Karte) — welche Sorte erlaubt ist, entscheidet danach die Grammatik (grPruefe).
  const AUS_ZIEL=new Set(["sagaCmd","evtOut","trgIn"]);
  function compatible(a,b){const A=a.__slot,B=b.__slot;if(!A||!B||A.anker||B.anker)return false;if(A.dir===B.dir)return false;
    if(A.type==="aus"||B.type==="aus"){const o=A.type==="aus"?A:B,i=o===A?B:A;return o.dir==="out"&&i.dir==="in"&&AUS_ZIEL.has(i.type);}
    if(A.type!==B.type)return false;
    if((A.kind==="arg")!==(B.kind==="arg"))return false;              // Argument-Pin nur an Argument-Pin
    if(A.kind==="arg"&&A.trans!==B.trans)return false;                 // und nur innerhalb derselben Transition
    if(A.type==="field"){const src=A.dir==="out"?A:B,snk=A.dir==="out"?B:A;   // Feld-Quelle muss den Wunsch des Eingangs erfüllen
      if(snk.want==="count")return istColl(src.ftyp)||istGanzzahl(src.ftyp);
      if(snk.want==="collection")return istColl(src.ftyp);
      return true;}
    return true;}
  function applyLink(a,b){
    const O=a.__slot.dir==="out"?a.__slot:b.__slot, I=a.__slot.dir==="out"?b.__slot:a.__slot;
    if(O.type==="darf"&&O.client){const c=MODEL.clients.find(x=>x._id===O.client);if(c&&I.rec){const f=(recByName(I.rec)||{}).kind==="query"?"fragt":"sendet";
        if(!(c[f]=c[f]||[]).includes(I.rec))c[f].push(I.rec);if(!darfHalter(I.rec).length)deFlash("⚠ kein Akteur darf "+I.rec+" — GR-CLIENT-BEFUGT (erst darf ⊕ beim Akteur)",false);}}
    else if(O.type==="traegt"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);
      if(c&&a){const t=vertragsTyp(a);if(!(c.traegt=c.traegt||[]).includes(t))c.traegt.push(t);}}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak&&I.rec&&!(ak.darf=ak.darf||[]).includes(I.rec))ak.darf.push(I.rec);}
    else if(O.type==="auftrag"){const ak=MODEL.akteure.find(x=>x._id===O.akt),p=MODEL.pipelines.find(x=>x._id===I.pipeline),hd=p&&p.handles[I.handleIdx];
      if(ak&&hd){hd.akteur=ak.name;const fremd=(hd.sends||[]).filter(c=>!(ak.darf||[]).includes(c));
        if(fremd.length)deFlash("⚠ "+ak.name+" darf "+fremd.join(", ")+" nicht — CQRS060 (ergänze darf ⊕ oder nimm den Auftrag weg)",false);}}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d)d.command=O.rec;}
    else if(O.type==="evtOut"){
      if(O.dec){const d=dec(O.dec);if(d&&!(d.ergibt||[]).some(x=>x.event===I.rec))(d.ergibt=d.ergibt||[]).push({event:I.rec});}
      // Konsument veröffentlicht ein reaktives Event (HandlerOutputRouter: yield IEvent → Broker-Re-Publish).
      else if(O.proj){const p=MODEL.projektionen.find(x=>x._id===O.proj);const hd=p&&p.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}
      else if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.publishes=hd.publishes||[];if(!hd.publishes.includes(I.rec))hd.publishes.push(I.rec);}}}
    else if(O.type==="prozess"){const t=MODEL.transitions.find(x=>x._id===O.trans);if(t)t.prozess=I.saga;}
    else if(O.type==="evtUse"){
      if(I.client){const c=MODEL.clients.find(x=>x._id===I.client);if(c&&!(c.kenntnis=c.kenntnis||[]).includes(O.rec))c.kenntnis.push(O.rec);}
      else if(I.app){const p=app(I.app);if(p)p.event=O.rec;}
      // Event → Frist: plant / storniert (die Drei-End-Relation der Composition-Root-Frist).
      else if(I.frist){const f=MODEL.frists.find(x=>x._id===I.frist);if(f){const arr=I.role==="storniert"?(f.storniert=f.storniert||[]):(f.plant=f.plant||[]);if(!arr.includes(O.rec))arr.push(O.rec);}}
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
        else r.handles[I.handleIdx].event=O.rec;}}
      // Event → Pipeline-Handle (die „Reaktion IST eine Pipeline"-Naht).
      else if(I.pipeline){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p){p.handles=p.handles||[];
        if(I.handleIdx==="openevt"||I.handleIdx==="open"){if(!p.handles.some(x=>x.event===O.rec&&x.inputKind==="event"))p.handles.push({inputKind:"event",event:O.rec,sends:[]});}
        else{p.handles[I.handleIdx].event=O.rec;p.handles[I.handleIdx].inputKind="event";delete p.handles[I.handleIdx].trigId;}}}}
    // Pipeline-Ausgang nach TYP: Command → sends (eine Frist-Zeile bleibt Frist), transientes Event → publishes, Trigger-Karte → emits.
    else if(O.type==="aus"){const p=MODEL.pipelines.find(x=>x._id===O.pipeline);const hd=p&&p.handles[O.handleIdx];if(hd){
      const dazu=(feld,x)=>{hd[feld]=hd[feld]||[];if(x&&!hd[feld].includes(x))hd[feld].push(x);};
      if(I.trgIn)dazu("emits",trigMsgLabel(I.trgIn));
      else{const r=recByName(I.rec),k=r&&r.kind;
        if(k==="command"){if(O.fristArt){hd.fristen=hd.fristen||[];if(!hd.fristen.some(f=>f.command===I.rec&&f.art===O.fristArt))hd.fristen.push({command:I.rec,art:O.fristArt});}
          else dazu("sends",I.rec);}
        else if(k==="rejection"||k==="event")dazu("publishes",I.rec);}}}
    else if(O.type==="sagaCmd"){
      if(O.akt&&typeof O.aufIdx==="number"){const ak=MODEL.akteure.find(x=>x._id===O.akt),r=ak&&(ak.vertrag||[])[O.aufIdx];
        if(r){r.ausgaenge=r.ausgaenge||[];if(!r.ausgaenge.includes(I.rec))r.ausgaenge.push(I.rec);}}
      else if(O.reaktion){const r=MODEL.reaktionen.find(x=>x._id===O.reaktion);const hd=r&&r.handles[O.handleIdx];if(hd){hd.sends=hd.sends||[];if(!hd.sends.includes(I.rec))hd.sends.push(I.rec);}}
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
    else if(O.type==="rcall"){const r=O.pipeline?MODEL.pipelines.find(x=>x._id===O.pipeline):MODEL.reader.find(x=>x._id===O.reader);const hd=r&&r.handles[O.handleIdx];if(hd){hd.fns=hd.fns||[];if(!hd.fns.includes(I.fn))hd.fns.push(I.fn);}}
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

  // Verbindung LÖSEN — das Gegenstück zu applyLink (gleiche Port-Typen, gleiche Modell-Felder, nur rückwärts).
  function loeseLink(a,b){
    const O=a.__slot.dir==="out"?a.__slot:b.__slot, I=a.__slot.dir==="out"?b.__slot:a.__slot;
    const ohne=(arr,x)=>(arr||[]).filter(v=>v!==x);
    const handle=(liste,id,idx)=>{const o=liste.find(x=>x._id===id);return o&&typeof idx==="number"?(o.handles||[])[idx]:null;};
    if(O.type==="darf"&&O.client){const c=MODEL.clients.find(x=>x._id===O.client);if(c){c.sendet=ohne(c.sendet,I.rec);c.fragt=ohne(c.fragt,I.rec);}}
    else if(O.type==="traegt"){const c=MODEL.clients.find(x=>x._id===O.client),a=MODEL.akteure.find(x=>x._id===I.akt);if(c&&a)c.traegt=ohne(c.traegt,vertragsTyp(a));}
    else if(O.type==="darf"){const ak=MODEL.akteure.find(x=>x._id===O.akt);if(ak)ak.darf=ohne(ak.darf,I.rec);}
    else if(O.type==="auftrag"){const p=MODEL.pipelines.find(x=>x._id===I.pipeline),hd=p&&p.handles[I.handleIdx];if(hd)delete hd.akteur;}
    else if(O.type==="cmd"){const d=dec(I.dec);if(d&&d.command===O.rec)d.command="";}
    else if(O.type==="evtOut"){
      if(O.dec){const d=dec(O.dec);if(d)d.ergibt=(d.ergibt||[]).filter(x=>x.event!==I.rec);}
      else{const hd=handle(O.proj?MODEL.projektionen:MODEL.reaktionen,O.proj||O.reaktion,O.handleIdx);if(hd)hd.publishes=ohne(hd.publishes,I.rec);}}
    else if(O.type==="prozess"){const t=MODEL.transitions.find(x=>x._id===O.trans);if(t)t.prozess="";}
    else if(O.type==="evtUse"){
      if(I.client){const c=MODEL.clients.find(x=>x._id===I.client);if(c)c.kenntnis=ohne(c.kenntnis,O.rec);}
      else if(I.app){const p=app(I.app);if(p)p.event="";}
      else if(I.frist){const f=MODEL.frists.find(x=>x._id===I.frist);if(f){if(I.role==="storniert")f.storniert=ohne(f.storniert,O.rec);else f.plant=ohne(f.plant,O.rec);}}
      else if(I.trigger){const sg=MODEL.sagas.find(x=>x.name===I.saga);if(sg)sg.triggerEvent="";}
      else if(I.akt&&I.aufIdx!=null){const ak=MODEL.akteure.find(x=>x._id===I.akt);if(ak)ak.vertrag=(ak.vertrag||[]).filter(r=>r.eingang!==O.rec);}
      else if(I.trans){const t=MODEL.transitions.find(x=>x._id===I.trans);if(t)t.wenn=ohne(t.wenn,O.rec);}
      else if(I.proj){const p=MODEL.projektionen.find(x=>x._id===I.proj);if(p)p.handles=(p.handles||[]).filter(h=>h.event!==O.rec);}
      else if(I.reaktion){const r=MODEL.reaktionen.find(x=>x._id===I.reaktion);if(r)r.handles=(r.handles||[]).filter(h=>h.event!==O.rec);}
      else if(I.pipeline){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p)p.handles=(p.handles||[]).filter(h=>!(h.inputKind==="event"&&h.event===O.rec));}}
    else if(O.type==="aus"){const hd=handle(MODEL.pipelines,O.pipeline,O.handleIdx);if(hd){
      if(I.trgIn)hd.emits=ohne(hd.emits,trigMsgLabel(I.trgIn));
      // Eine Zeile löst nur sich selbst (sofort ODER diese Frist); der offene Port löst alles zu dieser Nachricht.
      else{if(!O.fristArt)hd.sends=ohne(hd.sends,I.rec);if(!O.fristArt&&!O.sofort)hd.publishes=ohne(hd.publishes,I.rec);
        if(!O.sofort)hd.fristen=(hd.fristen||[]).filter(f=>!(f.command===I.rec&&(!O.fristArt||f.art===O.fristArt)));}}}
    else if(O.type==="sagaCmd"){
      if(O.akt&&typeof O.aufIdx==="number"){const ak=MODEL.akteure.find(x=>x._id===O.akt),r=ak&&(ak.vertrag||[])[O.aufIdx];if(r)r.ausgaenge=ohne(r.ausgaenge,I.rec);}
      else if(O.reaktion||O.pipeline){const hd=handle(O.reaktion?MODEL.reaktionen:MODEL.pipelines,O.reaktion||O.pipeline,O.handleIdx);if(hd)hd.sends=ohne(hd.sends,I.rec);}
      else if(O.frist){const f=MODEL.frists.find(x=>x._id===O.frist);if(f)f.sendet="";}
      else{const t=MODEL.transitions.find(x=>x._id===O.trans);const d=t&&(t.dann||[])[O.dannIdx];
        if(d){if(O.role==="komp"){delete d.kompensation;delete d.kompensationAusdruck;}else{d.sende="";delete d.sendeAusdruck;}}}}
    else if(O.type==="trigmsg"){const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p&&typeof I.handleIdx==="number")p.handles.splice(I.handleIdx,1);}
    else if(O.type==="self"){const hd=handle(MODEL.pipelines,I.pipeline,I.handleIdx);if(hd)hd.selfName="";}
    else if(O.type==="decAgg"){const d=dec(O.dec);if(d)d.aggregat="";}
    else if(O.type==="appAgg"){const p=app(O.app);if(p)p.aggregat="";}
    else if(O.type==="readmodel"){const rm=MODEL.readModels.find(x=>x._id===O.rm);if(rm)rm.store="";}
    else if(O.type==="wcall"){const hd=handle(MODEL.projektionen,O.proj,O.handleIdx);if(hd)hd.fns=ohne(hd.fns,I.fn);}
    else if(O.type==="rcall"){const hd=O.pipeline?handle(MODEL.pipelines,O.pipeline,O.handleIdx):handle(MODEL.reader,O.reader,O.handleIdx);if(hd)hd.fns=ohne(hd.fns,I.fn);}
    else if(O.type==="projref"){const r=MODEL.reader.find(x=>x._id===O.reader);if(r)r.projektion="";}
    else if(O.type==="query"){const r=MODEL.reader.find(x=>x._id===I.reader);if(r)r.handles=(r.handles||[]).filter(h=>h.query!==O.rec);}
    else if(O.type==="qrsp"){const hd=handle(MODEL.reader,O.reader,O.handleIdx);if(hd)hd.responses=ohne(hd.responses,I.rec);}
    else if(O.type==="code"){setCodeSrc(I.target,null);}
    else if(O.type==="prompt"){const l=MODEL.llmNodes.find(x=>x._id===O.llm);if(l){l.promptZiel=null;delete l.promptSlot;}}
    else if(O.type==="setting"){if(I.frist){const f=MODEL.frists.find(x=>x._id===I.frist);if(f)f.dauerSetting="";}}
    else if(O.type==="dienst"){const dn=MODEL.dienste.find(x=>x._id===O.dienst)||{};const p=MODEL.pipelines.find(x=>x._id===I.pipeline);if(p)p.dienste=ohne(p.dienste,dn.vertrag||dn.name);}
    else if(O.type==="state"){const st=MODEL.states.find(x=>x._id===O.state);if(st)st.aggregat="";}
    else{deFlash("Diese Verbindung lässt sich hier nicht lösen — im Formular ändern.",false);return;}
    render();
  }

  // ══ GRAMMATIK (Phase 1, docs/konzept-editor-komposition.md §3): EINE Quelle — DomainEditor.Grammatik, vom Extractor als
  //   rahmen.grammatik ins Board gelegt. Hier ist nichts davon hart kodiert: Sorten, Tabelle, Regeln und die Abbildung der
  //   Editor-Ports auf die Sprache kommen aus dem Board. Der Verbinden-Modus bietet nur erlaubte Ziele an; ein gesperrtes Ziel
  //   nennt die Regel beim Namen (derselbe Satz wie der Validator).
  const GR=()=>(MODEL.rahmen&&MODEL.rahmen.grammatik)||null;
  const grRegel=id=>{const g=GR();return (g&&(g.regeln||[]).find(r=>r.id===id))||{id,name:id,build:"?"};};
  const grText=id=>{const r=grRegel(id);return "Regel »"+r.name+"« ("+r.id+"; Build: "+r.build+")";};
  const grName=(liste,id)=>{const g=GR();const x=g&&(g[liste]||[]).find(y=>y.id===id);return x?x.name:id;};
  const grSymbol=s=>{const g=GR();const x=g&&(g.sorten||[]).find(y=>y.id===s);return x?x.symbol:"•";};
  const grVerlierbar=s=>{const g=GR();const x=g&&(g.sorten||[]).find(y=>y.id===s);return !!x&&x.garantie==="verlierbar";};
  const grKonsum=(s,b)=>(GR().konsume||[]).find(k=>k.sorte===s&&k.baustein===b)||null;
  const grErzeugung=(b,s)=>(GR().erzeugungen||[]).find(e=>e.baustein===b&&e.sorte===s)||null;
  const grRegelSorte=s=>((GR().konsume||[]).find(k=>k.sorte===s)||{}).regel||"GR-AUSGANG-GESCHLOSSEN";
  const grRegelBaustein=b=>((GR().erzeugungen||[]).find(e=>e.baustein===b)||{}).regel||"GR-AUSGANG-GESCHLOSSEN";
  // Welcher Baustein steckt hinter einem Editor-Port (Info-Felder dec/proj/pipeline …)?
  function grBaustein(I){for(const [k,b] of (GR().portBaustein||[]))if(I[k]!=null&&I[k]!=="")return b;return null;}
  // Nachricht (Name) → Sorte: Record-Art, Trigger-Karte, Selbst-Nachricht.
  function grNachrichtSorte(name){const g=GR(),r=recByName(name);if(r)return (g.recordSorte||{})[r.kind]||null;
    if(MODEL.triggers.some(t=>(t.msgName||t.name)===name))return "trigger";
    if((MODEL.selbstNachrichten||[]).some(x=>x.name===name))return "selbst";return null;}
  // Wer konsumiert die Nachricht schon (für die Kardinalität „genau eins")?
  function grKonsumenten(name,sorte,baustein){
    if(baustein==="aggregat"&&sorte==="command")return MODEL.decider.filter(d=>d.command===name).map(d=>({dec:d._id,name:"Decider ("+(d.aggregat||"?")+")"}));
    if(baustein==="aggregat"&&sorte==="event")return MODEL.applier.filter(a=>a.event===name).map(a=>({app:a._id,name:"Applier ("+(a.aggregat||"?")+")"}));
    if(baustein==="reader")return MODEL.reader.filter(r=>(r.handles||[]).some(x=>x.query===name)).map(r=>({reader:r._id,name:"Reader "+r.name}));
    if(baustein==="pipeline"&&sorte==="trigger")return MODEL.pipelines.filter(p=>(p.handles||[]).some(x=>x.inputKind==="trigger"&&x.input===name)).map(p=>({pipeline:p._id,name:"Pipeline "+p.name}));
    return [];}
  const grSelbeStelle=(x,I)=>(x.dec&&x.dec===I.dec)||(x.app&&x.app===I.app)||(x.reader&&x.reader===I.reader)||(x.pipeline&&x.pipeline===I.pipeline);
  const grVerstoss=(regel,text)=>({regel,text:text+" — "+grText(regel)});
  // Eine Verbindung Ausgang O → Eingang I gegen die Grammatik prüfen. null = erlaubt, sonst {regel, text}.
  function grPruefe(O,I){const g=GR();if(!g)return null;
    // (a) Nachricht → Baustein-Eingang: Sorte × Eingang, Kardinalität je Nachricht.
    if(O.rec&&!I.rec){const s=grNachrichtSorte(O.rec),b=grBaustein(I);if(!s||!b)return null;
      const k=grKonsum(s,b);if(!k)return grVerstoss(grRegelSorte(s),grName("sorten",s)+" '"+O.rec+"' passt nicht in einen "+grName("bausteine",b)+"-Eingang");
      if(k.kardinalitaet==="eins"){const andere=grKonsumenten(O.rec,s,b).filter(x=>!grSelbeStelle(x,I));
        if(andere.length)return grVerstoss(k.regel,grName("sorten",s)+" '"+O.rec+"' hat schon einen Konsumenten: "+andere.map(x=>x.name).join(", "));}
      return null;}
    // (b) Baustein-Ausgang → Nachricht: wer darf welche Sorte erzeugen.
    if(I.rec&&!O.rec){const b=grBaustein(O);let s=grNachrichtSorte(I.rec);if(!b||!s)return null;
      Object.keys(g.ausgangSorte||{}).forEach(k=>{if(O[k]!=null&&O[k]!=="")s=g.ausgangSorte[k];});
      if(!grErzeugung(b,s))return grVerstoss(b==="pipeline"&&s==="event"?"GR-KEIN-EVENT-AUS-PIPELINE":grRegelBaustein(b),
        grName("bausteine",b)+" erzeugt kein "+grName("sorten",s)+" ('"+I.rec+"')");
      return null;}
    // (c) Baustein → Baustein: Trigger-Kette, Ingress → Pipeline, Selbst-Schleife.
    const s=(g.portSorte||{})[O.type];if(!s)return null;
    const bo=grBaustein(O),bi=grBaustein(I);
    if(bo&&!grErzeugung(bo,s))return grVerstoss(grRegelBaustein(bo),grName("bausteine",bo)+" erzeugt kein "+grName("sorten",s));
    if(!bi)return null;const k=grKonsum(s,bi);
    if(!k)return grVerstoss(grRegelSorte(s),grName("sorten",s)+" passt nicht in einen "+grName("bausteine",bi)+"-Eingang");
    if(k.kardinalitaet==="dieselbe"&&O.pipeline!==I.pipeline)return grVerstoss(k.regel,"Selbst '"+(O.name||"?")+"' kommt nur in der planenden Pipeline an");
    if(s==="selbst"){const p=MODEL.pipelines.find(x=>x._id===O.pipeline),hd=p&&(p.handles||[])[O.handleIdx];
      if(hd&&!grSelbstErlaubt(hd))return grVerstoss("GR-SELBST-OHNE-EVENT","Handle "+(hd.event||"?")+" hat einen Event-Eingang (keine Mailbox)");}
    if(k.kardinalitaet==="eins"&&s==="trigger"){const nm=O.msgName||(O.trigId?trigMsgLabel(O.trigId):"");
      const andere=grKonsumenten(nm,"trigger","pipeline").filter(x=>x.pipeline!==I.pipeline);
      if(nm&&andere.length)return grVerstoss(k.regel,"Trigger '"+nm+"' behandelt schon "+andere.map(x=>x.name).join(", "));}
    return null;}
  // Darf dieser Pipeline-Handle ein Selbst planen? Nur ohne Event-Eingang (GR-SELBST-OHNE-EVENT).
  const grSelbstErlaubt=hd=>(hd.inputKind||"event")!=="event";
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
  //   Reine Editor-Sicht: das Modell bleibt vollständig (Prüfen, Vorschau, C# schreiben sehen alles) — nicht Geladenes wird nur nicht
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
    if(k==="codenode"){const o=findCodeOwner(r._id);return o?nsVon(o):null;}
    if(k==="llmnode"){const o=r.promptZiel&&findCodeOwner(r.promptZiel);return o?nsVon(o):null;}
    if(k==="frist"){const c=recByName(r.sendet);return c?c.namespace:null;}
    if(k==="dienst"){const ps=MODEL.pipelines.filter(p=>(p.dienste||[]).includes(r.vertrag||r.name));return ps.length===1?ps[0].namespace:null;}
    if(k==="hostsetting"){const c=recByName(r.konfig);return c?c.namespace:null;}
    return r.namespace||null;}
  // Stammt der Knoten aus dem Code (sonst: Entwurf, immer sichtbar)? Abgeleitete Knoten folgen ihrem Besitzer.
  function ausCodeVon(n){const k=n.kind,r=n.ref;
    if(k==="handle"||k==="fn"||k==="auf")return ausCodeVon(n.own);
    if(k==="state"){const a=MODEL.aggregate.find(x=>x.name===r.aggregat);return !!(a&&a.ausCode);}
    if(k==="transition"){const sg=MODEL.sagas.find(x=>x.name===r.prozess);return sg?!!sg.ausCode:r.ausCode===true;}
    if(k==="codenode"){const o=findCodeOwner(r._id);return !!(o&&ausCodeVon(o));}
    if(k==="llmnode"){const o=r.promptZiel&&findCodeOwner(r.promptZiel);return !!(o&&ausCodeVon(o));}
    return r.ausCode===true;}
  // Geladen, wenn seine DOMÄNE (Graph-Zuordnung, domKey — z. B. ImagePairProjection → ImagePair) ODER sein eigener Namespace
  //   (z. B. „Projections“ ausdrücklich gewählt) in der Auswahl liegt. Die Domänen-Gehörigkeit steht über dem Namespace.
  function geladen(n){if(n.kind==="client")return LADEN===null||clientPorts(n.ref).some(p=>{const m=p.node&&NODEBY.get(p.node);return m&&m.kind!=="client"&&geladen(m);});
    if(LADEN===null||!ausCodeVon(n))return true;const x=nsVon(n),d=domKey(n);
    return LADEN.some(g=>(x&&drinNs(g,x))||(d!==OHNE_DOM&&drinNs(g,d)));}
  window.deLaden=function(){START_OFFEN=true;render();};
  // Start-Dialog: leer starten, Domänen wählen (Häkchen am Eltern-Namespace lädt alles darunter) oder alles laden.
  function zeigeStart(){if(!canvas)return;const alt=canvas.querySelector(".gstart");if(alt)alt.remove();if(!START_OFFEN)return;
    const nsAlle=new Set();["records","enums","aggregate","sagas","readModels","stores","projektionen","reader","reaktionen","pipelines","triggers"]
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
      h("div",{class:"gstart-t"},"Das Board startet leer. Wähle die Domänen, die geladen werden — der Rest bleibt im Code unberührt (Prüfen und „C# schreiben“ sehen weiter alles). Neues entsteht als Entwurf und ist immer sichtbar."),
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
  function basisZaehl(alle,zahl){graphNodes().forEach(n=>{if(["handle","fn","codenode","llmnode","state","decider","applier","transition"].includes(n.kind)||!ausCodeVon(n))return;
      const x=nsVon(n),d=domKey(n);alle.forEach(m=>{if((x&&drinNs(m,x))||(d!==OHNE_DOM&&drinNs(m,d)))zahl.set(m,(zahl.get(m)||0)+1);});});}

  // ══ VERBINDEN-MODUS (Hybrid, docs/konzept-editor-panel-bearbeitung.md §3.3): Port im Panel anklicken → alle PASSENDEN
  //   Knoten leuchten auf dem Graphen (Ablauf-Ansicht, Rest abgeblendet) → Karte anklicken = verbinden, ✓-Karte = lösen.
  //   Einfache Ports enden nach einem Klick, Listen-Ports bleiben offen bis Esc/Fertig. Typregel: compatible().
  let VB=null;   // {key, label, einzel, filter}
  function vbQuelle(){const i=canvas&&canvas.querySelector(".ginsp");return VB&&i?[...i.querySelectorAll(".slot")].find(x=>x.__key===VB.key)||null:null;}
  // Kandidaten: typgleich (compatible) UND von der Grammatik erlaubt; typgleich, aber verboten → VB.G (gesperrt, mit Regel).
  function vbKandidaten(q){const m=new Map(),gs=new Map();Object.values(SLOTS).forEach(x=>{if(!x||!compatible(q,x))return;const id=knotenIdVon(x);if(!id)return;
    const O=q.__slot.dir==="out"?q.__slot:x.__slot,I=q.__slot.dir==="out"?x.__slot:q.__slot;
    const v=vbPartner(q,x)?null:grPruefe(O,I);   // bestehende Verbindungen bleiben lösbar
    if(v){if(!gs.has(id))gs.set(id,[]);gs.get(id).push({x,v});return;}
    if(!m.has(id))m.set(id,[]);m.get(id).push(x);});if(VB)VB.G=gs;return m;}
  // Verbunden? Eine gezeichnete Kante zwischen dem Kandidaten-Port und einem Port DIESES Knotens mit demselben Typ.
  //   Genau dieser Port (gleicher Schlüssel) — nur „offene" Sammel-Ports (…:open, z. B. „+ Ausgang") zählen knotenweit.
  function vbPartner(q,x){const offen=/:open\b|open(evt|trg)?$/.test(q.__key);
    for(const [k1,k2] of KANTEN){const o=k1===x.__key?k2:(k2===x.__key?k1:null);if(!o)continue;
      if(o===q.__key)return SLOTS[o]||q;
      const e=SLOTS[o];if(offen&&e&&knotenIdVon(e)===SEL&&e.__slot.type===q.__slot.type&&e.__slot.dir===q.__slot.dir&&e.__slot.handleIdx===q.__slot.handleIdx)return e;}
    return null;}
  // Lesbarer Name eines Ports: Beschriftung + Werte der Eingabefelder seiner Zeile (z. B. der Name einer Store-Funktion).
  const slotLabel=x=>{const r=x.closest(".slotrow,.gtopfield,.frow");if(!r)return x.__slot.type;
    const werte=[...r.querySelectorAll("input,select")].map(i=>i.value).filter(Boolean);
    const text=(r.textContent||"").replace(/[✕▶◀▲]/g," ");
    return (werte.join(" ")+" "+text).replace(/\s+/g," ").trim().slice(0,60)||x.__slot.type;};
  // Hält der Port EINEN Wert (Decider→Command, Applier→Event …)? Dann endet der Modus nach einem Klick.
  function istEinzel(I){const t=I.type,d=I.dir;
    if(t==="cmd")return d==="in";
    if(t==="auftrag")return d==="in";   // ein Handle handelt im Auftrag höchstens EINES Akteurs
    if(t==="prozess"||t==="decAgg"||t==="appAgg"||t==="projref"||t==="readmodel"||t==="state"||t==="prompt")return d==="out";
    if(t==="code"||t==="setting"||t==="ftype"||t==="self")return d==="in";
    if(t==="evtUse")return d==="in"&&(!!I.app||!!I.trigger||typeof I.handleIdx==="number"||typeof I.wennIdx==="number"||typeof I.aufIdx==="number");
    if(t==="sagaCmd")return d==="out"&&(!!I.frist||!!I.trans);
    if(t==="trigmsg")return d==="in"&&typeof I.handleIdx==="number";
    return false;}
  // ⊕ anklicken = Modus an (derselbe ⊕ nochmal = aus). Beenden auch mit Esc oder Klick ins Leere.
  function vbStart(x){if(!x.__key)return;if(VB&&VB.key===x.__key){vbEnde();return;}
    if(VIEW.lod==="karte")setzeLod("ablauf");VB={key:x.__key,label:slotLabel(x),einzel:istEinzel(x.__slot)};vbZeige(true);}
  function vbEnde(){VB=null;if(!world)return;world.classList.remove("vbmodus");if(canvas)canvas.classList.remove("vbaktiv");
    world.querySelectorAll(".vb-kand,.vb-verb,.vb-quelle,.vb-gesperrt").forEach(e=>e.classList.remove("vb-kand","vb-verb","vb-quelle","vb-gesperrt"));
    canvas.querySelectorAll(".gvbwahl").forEach(e=>e.remove());canvas.querySelectorAll(".ginsp .slot.vb-aktiv").forEach(e=>e.classList.remove("vb-aktiv"));}
  function vbZeige(einpassen){if(!VB||!world||!canvas)return;const q=vbQuelle();if(!q){vbEnde();return;}
    const K=vbKandidaten(q);VB.K=K;
    world.classList.add("vbmodus");canvas.classList.add("vbaktiv");q.classList.add("vb-aktiv");
    // Aktiven Port im (verkleinerten) Panel sichtbar halten.
    const insp=q.closest(".ginsp");if(insp){const d=q.getBoundingClientRect().top-insp.getBoundingClientRect().top;if(d<0||d>insp.clientHeight-30)insp.scrollTop+=d-40;}
    world.querySelectorAll(".gnode2").forEach(el=>{const id=el.dataset.id,xs=K.get(id);
      const passt=!!xs;
      el.classList.toggle("vb-kand",passt);el.classList.toggle("vb-verb",passt&&xs.some(x=>vbPartner(q,x)));el.classList.toggle("vb-quelle",id===SEL);
      const gs=!passt&&VB.G&&VB.G.get(id);el.classList.toggle("vb-gesperrt",!!gs);el.title=gs?"✕ "+gs[0].v.text:"";});
    if(!K.size)deFlash("Keine passenden Knoten für diesen Anschluss"+(VB.G&&VB.G.size?" — "+VB.G.size+" gesperrt: "+[...VB.G.values()][0][0].v.text:"."),false);
    if(einpassen){const cr=canvas.getBoundingClientRect(),imBild=[...K.keys()].some(id=>{const el=world.querySelector('[data-id="'+id+'"]');if(!el)return false;
      const r=el.getBoundingClientRect();return r.right>cr.left&&r.left<cr.right&&r.bottom>cr.top&&r.top<cr.bottom;});
      if(!imBild)vbEinpassen([...K.keys()].filter(id=>VIS.has(id)));}}
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
  // Ein NEUER, bisher unverbundener Knoten (Gruppe „geteilt") zieht beim ersten Verbinden in die Spalte seiner Gruppe um.
  //   Etablierte Knoten bleiben stehen (kein Springen beim Umschalten); kein Voll-Render — nur neu platzieren + Kanten.
  function mitUmzug(ids,aendern){const gruppe=id=>{const n=graphNodes().find(m=>m.id===id);return n?groupKeyOf(n):null;};
    const vorher=ids.map(gruppe);aendern();
    const weg=ids.filter((id,i)=>id&&vorher[i]===SHARED_KEY&&gruppe(id)!==SHARED_KEY);if(!weg.length)return;
    const L=KPOS[VIEW.details?"d":"k"]||{};weg.forEach(id=>{delete L[id];const n=graphNodes().find(m=>m.id===id);if(n){delete n.ref.x;delete n.ref.y;delete n.ref._kpos;}});
    packLayout();drawEdges();drawMinimap();}
  function vbSchalte(q,x){if(!q||!x)return;const partner=vbPartner(q,x),ziel=knotenIdVon(x);
    // Bei Einzel-Ports hängt der BISHERIGE Partner mit dran (dessen Karte verliert ihr ✓ / ihre Kante).
    const alt=VB&&VB.einzel?[...(VB.K||new Map()).entries()].filter(([,xs])=>xs.some(y=>vbPartner(q,y))).map(([id])=>id):[];
    mitUmzug([SEL,ziel],()=>imModus([SEL,ziel,...alt],()=>{if(partner)loeseLink(partner,x);else applyLink(q,x);}));
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
      (c.traegt||[]).forEach(t=>{const a=teilAkteur(t);if(a&&SLOTS["cl:traegt:"+c._id+":"+t]&&SLOTS["akt:traegt:"+a._id])KANTEN.push(["cl:traegt:"+c._id+":"+t,"akt:traegt:"+a._id]);});});
    // Dienst-Akteur → Pipeline-Handle, der in seinem Auftrag entscheidet (gestrichelt violett).
    MODEL.pipelines.forEach(p=>(p.handles||[]).forEach((hd,hi)=>{const a=hd.akteur&&MODEL.akteure.find(x=>x.name===hd.akteur);
      if(a)add("akt:auftrag:"+a._id+":"+p._id+":"+hi,"auftrag:in:"+p._id+":"+hi,"#b05fd0",true);}));
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
        if(d.rufe)add("tr:rufe:"+t._id+":"+di,"fk:in:"+d.rufe,"#c08a2e");
        if(d.sende)add("tr:sende:"+t._id+":"+di,"cmd:in:"+d.sende,"#4a86d6");
        if(d.kompensation)add("tr:komp:"+t._id+":"+di,"cmd:in:"+d.kompensation,"#cf6f68",true);
      });
    });
    // Katalog-Funktion: Auftrag (Record) → Funktion (gestrichelt), Funktion → ihre Ergebnis-Events.
    MODEL.funktionen.forEach(f=>{if(f.auftrag)add("auf:out:"+f.auftrag,"fk:auftrag:"+f._id,"#c08a2e",true);
      (f.ergebnisse||[]).forEach(e=>{if(e)add("fk:out:"+f._id+":"+e,"evt:in:"+e,"#4fb06a");});});
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
      // Command per Frist (⏳, durabel über den Fristplan) bzw. deren Storno (✕⏳) — gestrichelt, eigene Farbe.
      (hd.fristen||[]).forEach(f=>{if(f.command)add("pl:frist:"+p._id+":"+hi+":"+f.art+":"+f.command,"cmd:in:"+f.command,f.art==="storno"?"#cf6f68":"#c98a3a",true);});
      // Veröffentlichtes (transientes) Event → Broker: verlierbar, gestrichelt teal (wie an Projektion/Reaktion).
      (hd.publishes||[]).forEach(ev=>{if(ev)add("pl:pub:"+p._id+":"+hi+":"+ev,"evt:in:"+ev,"#2fd6b0",true);});
      // Fähigkeit (Read-Fn als Parameter) → Store-Fn.
      (hd.fns||[]).forEach(fid=>{const f=fnById(fid);if(f)add("rcall:out:pl:"+p._id+":"+hi+":"+fid,"rcall:in:"+f.store._id+":"+fid,"#c08a3e");});
      // yield IPipelineTrigger → verbrauchende Pipeline (die Kette, z. B. FileWatch → ImageProcessing).
      (hd.emits||[]).forEach(tn=>{const t=MODEL.triggers.find(x=>trigName(x)===tn);if(t){add("pl:emit:"+p._id+":"+hi+":"+tn,"trg:in:"+t._id,"#f0883e");return;}
        MODEL.pipelines.forEach(q=>{if(q._id!==p._id)(q.handles||[]).forEach((qh,qi)=>{if(qh.inputKind==="trigger"&&qh.input===tn)add("pl:emit:"+p._id+":"+hi+":"+tn,"pl:in:"+q._id+":"+qi,"#f0883e");});});});
      // Selbst<T> → passender Self-Handle (Name-Match), gestrichelter Loop.
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
    KONTRAKT.forEach(([u,v])=>{const eu=ELS.get(u),ev=ELS.get(v);if(!eu||!ev)return;
      const kv=(NODEBY.get(v)||{}).kind,col=kv==="event"?"#4fb06a":(kv==="command"?"#4a86d6":"#8a8f9c");
      const p=mkE({el:eu},{el:ev},col,false,"kontrakt");p.dataset.a=u;p.dataset.b=v;});
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
  function startPan(e){e.preventDefault();canvas.classList.add("panning");bewegtAn();const sx=e.clientX,sy=e.clientY,ox=PAN.x,oy=PAN.y;PANNED=false;
    const mv=ev=>{if(Math.abs(ev.clientX-sx)+Math.abs(ev.clientY-sy)>3)PANNED=true;PAN.x=ox+(ev.clientX-sx);PAN.y=oy+(ev.clientY-sy);applyPan();};
    const up=()=>{canvas.classList.remove("panning");bewegtAus();window.removeEventListener("pointermove",mv);window.removeEventListener("pointerup",up);};
    window.addEventListener("pointermove",mv);window.addEventListener("pointerup",up);}

  // ══ ANSICHT (reine Darstellung — Modell, Scaffolder und Round-trip bleiben unberührt) ══════════════════════
  //   (1) Kompakt-Karten: Knoten standardmäßig eingeklappt (Kopf + Kurzfassung), bearbeitet wird im INSPECTOR.
  //   (2) Details eingeklappt: Decider/Applier/State/Code/LLM/Ablehnung/VO/Enum/Response liegen IN ihrem
  //       sichtbaren Besitzer (Chips + Inspector-Sektionen); Pfade durch sie werden als Kopf-Kanten zusammengezogen.
  //   (4) Semantischer Zoom: Landkarte (<0.4, Aggregat-Kacheln + gebündelte Kanten) · Ablauf (<0.75, nur Titel) · Detail.
  //   (5) Slice-Fokus: Klick auf einen Knoten → sein vertikaler Schnitt bleibt hell, der Rest tritt zurück.
  // details: Decider/Applier/State/Code/LLM/Ablehnung/Typen sind IMMER eigene Knoten auf dem Graphen (nie eingeklappt).
  const KPOS_VERSION=8;   // hochzählen, wenn sich Kartengrößen ändern → gespeicherte Anordnung wird einmalig neu gepackt
  let VIEW={kompakt:true,details:true,lod:"ablauf",aus:[],domNeu:[],heim:{},heimBlk:{},heimAkteur:{},akteurOrdnung:{}}, VKEY="cqrs-ansicht", KPKEY="cqrs-kpos", KPOS={k:{},d:{}};
  function ladeAnsicht(k){VKEY="cqrs-ansicht"+(k?":"+k:"");KPKEY="cqrs-kpos"+(k?":"+k:"");
    try{const v=JSON.parse(localStorage.getItem(VKEY)||"null");if(v)VIEW={...VIEW,...v};}catch(e){}
    VIEW.details=true;VIEW.kompakt=true;
    try{KPOS=JSON.parse(localStorage.getItem(KPKEY)||"{}")||{};}catch(e){KPOS={};}
    if(!KPOS.k||!KPOS.d||KPOS.v!==KPOS_VERSION)KPOS={k:{},d:{},v:KPOS_VERSION};}
  // Ausgeblendete Knotenarten (Auswahl-Menü „👁 Ausblenden"): pro Browser + Solution gespeichert (VIEW.aus).
  const istAus=k=>Array.isArray(VIEW.aus)&&VIEW.aus.includes(k);
  function setzeAus(k,aus){const a=new Set(VIEW.aus||[]);if(aus)a.add(k);else a.delete(k);VIEW.aus=[...a];speichereAnsicht();render();}
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
    VIS=new Set();VERTRETER=new Map();DOMKEY=new Map();EINHEIT=new Map();DETAILS=new Map();EINGEKLAPPT=new Set();
    AKM=null;AKTSET=new Map();AKTORD=new Map();AKTDOM=null;
    alle.forEach(n=>{const v=vert(n.id,new Set());VERTRETER.set(n.id,v);
      if(v.length===1&&v[0]===n.id){if(!HIDDEN.has(groupKeyOf(n))&&!istAus(n.kind)&&geladen(n))VIS.add(n.id);}
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
  const STOP=new Set(["akteur","aggregate","command","saga","transition","reaktion","pipeline","frist","valueobject","enum","dienst","hostsetting","trigger","konfig"]);
  const LESE_START=new Set(["projektion","reader","query","store","readmodel","queryresponse"]);
  // ── KETTE (§12): von einer Nachricht/einem Handle/Decider/Applier/einer Regel aus dem Nachrichtenfluss TRANSITIV folgen —
  //    vorwärts alle Konsumenten (Command → Decider → Events → Handles → Commands …), rückwärts alle Erzeuger. So erscheint die
  //    geschlossene Kette (z. B. FileWatch → DateiErkannt → ImageProcessing → ImagePair → ImagePairKomplett → ImageProcessing …).
  //    Besitzer (Aggregat/Pipeline/Projektion/Reaktion/Reader/Prozess/Store) werden gezeigt, aber nicht durchlaufen — sonst
  //    leuchteten über ihre übrigen Handles/Decider alle fremden Ketten mit. Typen/Betrieb (VO, Enum, Konfig, Dienst) gehören nicht dazu.
  const KETTE_HUB=new Set(["aggregate","pipeline","projektion","reaktion","reader","saga","store"]);
  const KETTE_FLUSS=new Set(["command","event","rejection","query","queryresponse","trigger","handle","decider","applier","transition","frist","fn",...KETTE_HUB]);
  const KETTE_START=new Set(["command","event","rejection","trigger","handle","decider","applier","transition","pipeline","reaktion"]);
  function ketteVon(start,nurVorwaerts){const N=id=>NODEBY.get(id)||{},k0=N(start).kind;
    // Pipeline/Reaktion als Start: ihre Handles sind die Startpunkte (der Besitzer selbst hat keinen Fluss).
    const starts=[start,...(k0==="pipeline"||k0==="reaktion"?(ADJ.inn.get(start)||[]).filter(y=>N(y).kind==="handle"):[])];
    const res=new Set(starts);
    const lauf=nach=>{const q=[...starts],seen=new Set();
      while(q.length){const id=q.shift();if(seen.has(id))continue;seen.add(id);
        if(!starts.includes(id)&&KETTE_HUB.has(N(id).kind))continue;
        nach(id).forEach(y=>{if(KETTE_FLUSS.has(N(y).kind)){res.add(y);q.push(y);}});}};
    lauf(id=>ADJ.out.get(id)||[]);if(!nurVorwaerts)lauf(id=>ADJ.inn.get(id)||[]);
    // Auch rückwärts erreichte Handles/Decider/Applier zeigen ihren Besitzer (Pipeline, Aggregat …).
    [...res].forEach(id=>{if(!["handle","decider","applier","fn"].includes(N(id).kind))return;
      (ADJ.out.get(id)||[]).forEach(y=>{if(KETTE_HUB.has(N(y).kind))res.add(y);});});
    // Leseseite nachziehen (wie der Schnitt unten): an Projektion/Store die Reader + Read Models.
    [...res].forEach(id=>{const k=N(id).kind;if(k!=="projektion"&&k!=="store")return;
      (ADJ.inn.get(id)||[]).forEach(y=>{const ky=N(y).kind;if(ky==="reader"||ky==="readmodel")res.add(y);});});
    // Rümpfe (📝 + 🤖) der beteiligten Handles/Decider/Applier/Fns als Blätter.
    [...res].forEach(id=>{if(!["handle","decider","applier","fn"].includes(N(id).kind))return;
      (ADJ.inn.get(id)||[]).forEach(y=>{if(N(y).kind!=="codenode")return;res.add(y);(ADJ.inn.get(y)||[]).forEach(z=>{if(N(z).kind==="llmnode")res.add(z);});});});
    return res;}
  function sliceVon(start){const res=new Set([start]),N=id=>NODEBY.get(id)||{};
    const inn=id=>ADJ.inn.get(id)||[],out=id=>ADJ.out.get(id)||[];
    const k0=N(start).kind,seeds=[start];
    // PERSONA-SICHT: ein Akteur zeigt, was er auslösen/fragen darf, und alles, was daraus FOLGT (nur vorwärts).
    if(k0==="akteur"){out(start).forEach(t=>{res.add(t);ketteVon(t,true).forEach(x=>res.add(x));});return res;}
    // Projektions-/Reader-Handles behalten den Leseseiten-Schnitt unten; alle Fluss-Knoten zeigen die ganze Kette.
    if(KETTE_START.has(k0)&&!(k0==="handle"&&["reader"].includes((N(start).own||{}).kind)))return ketteVon(start);
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
  function waehle(id){
    if(VB){if(id&&id!==SEL){vbKlick(id);return;}if(!id){vbEnde();return;}}
    if(AUF&&id!=="cl:"+AUF.client){AUF=null;if(world)drawEdges();}
    const cn=SEL!==id&&id&&NODEBY.get(id);
    SEL=id||null;FOCUS=SEL?(cn&&cn.kind==="client"?clientSlice(cn.ref):sliceVon(SEL)):null;wendeFokusAn();zeigeInspector();}
  // Slice eines Clients: er selbst + alles, woran seine Leitung steckt (Nachrichten-Karten, Zusagen, Akteure der Teile).
  function clientSlice(c){const ids=new Set(["cl:"+c._id]);clientPorts(c).forEach(p=>{if(p.node)ids.add(vertreterId(p.node));
      if(p.ri==="zusage"){const a=MODEL.akteure.find(x=>x.name===p.akteure[0]);if(a)ids.add("akt:"+a._id);}});return ids;}
  function wendeFokusAn(){if(!world)return;const an=!!FOCUS;world.classList.toggle("fokus",an);
    const selV=SEL?(VERTRETER.get(SEL)||[SEL]):[];
    world.querySelectorAll(".gnode2").forEach(el=>{const id=el.dataset.id;el.classList.toggle("inslice",an&&FOCUS.has(id));el.classList.toggle("sel",selV.includes(id));});
    if(!an)return;
    world.querySelectorAll(".glink").forEach(p=>p.classList.toggle("inslice",FOCUS.has(p.dataset.a)&&FOCUS.has(p.dataset.b)));
    world.querySelectorAll(".gtile").forEach(t=>t.classList.toggle("inslice",(t.dataset.ids||"").split("|").some(i=>FOCUS.has(i))));}
  document.addEventListener("keydown",e=>{if(e.key!=="Escape")return;if(VB){vbEnde();return;}if(SEL)waehle(null);});

  // ── (1) INSPECTOR: die Detail-Sicht GENAU EINES Knotens (sein Formular) + Nachbarn zum Springen.
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
    // NUR der gewählte Knoten — Decider, Ablehnung, Code, LLM … sind eigene Knoten auf dem Graphen mit eigener Detail-Sicht.
    p.append(h("div",{class:"gi-hint"},"⊕ anklicken → passende Knoten leuchten → anklicken = verbinden / lösen · Esc oder ⊕ nochmal = fertig"));
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
  // Alle Ausgänge eines Pipeline-Handles nach Typ (aus den Board-Listen — sie sind nach dem Bearbeiten die Wahrheit).
  const plAusgaenge=hd=>[...(hd.sends||[]),...(hd.fristen||[]).map(f=>(f.art==="storno"?"✕⏳":"⏳")+f.command),
    ...(hd.publishes||[]),...(hd.emits||[]).map(x=>"⚡"+x),...(hd.schedules||[]).map(x=>"↺"+x.name)];
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
    (c.traegt||[]).forEach(t=>{const a=teilAkteur(t);
      zeile("↺ trägt "+t+(a?" · als "+sym(a.name):" · ⚠ kein Akteur-Vertrag"),[],"lt");
      P.filter(p=>p.ri==="zusage"&&p.teil===t&&!p.fehlt).forEach(p=>zeile("   ◀",[name(p,p.msg+((p.aus||[]).length?" → "+p.aus.join(", ")+(p.strom?" (Strom)":""):" · Kenntnis"))],"lr"));});
    [["sendet","▶ sendet"],["fragt","? fragt"]].forEach(([ri,kopf])=>grp(ri).forEach((ps,k)=>
      zeile(kopf+(k?" als "+k.split(" / ").map(sym).join(" / "):" · ⚠ kein Akteur darf es"),ps.map(p=>name(p)),k?"":"lwarn")));
    if(P.some(p=>p.ri==="kenntnis"))zeile("◀ hört",P.filter(p=>p.ri==="kenntnis").map(p=>name(p)));
    if(!P.length)box.append(h("div",{class:"lz lleer"},"— leerer Vertrag: im Panel ⊕ trägt / sendet / hört"));
    return box;}
  function kurzfassung(n){if(n.kind==="client")return anschlussleiste(n);const r=n.ref,k=n.kind;let t="";
    if(k==="command"){const ev=MODEL.decider.filter(d=>d.command===r.name).flatMap(d=>(d.ergibt||[]).map(o=>o.event)).filter(e=>(recByName(e)||{}).kind!=="rejection");
      t=ev.length?"→ "+kurz(ev):"→ (kein Decider)";}
    else if(k==="event"){const c=MODEL.decider.filter(d=>(d.ergibt||[]).some(o=>o.event===r.name)).map(d=>d.command);t=c.length?"← "+kurz(c):"";}
    else if(k==="aggregate")t=MODEL.decider.filter(d=>d.aggregat===r.name).length+" Commands · "+MODEL.applier.filter(a=>a.aggregat===r.name).length+" Events · "+(r.state||[]).length+" State-Felder";
    else if(k==="projektion"||k==="reaktion")t=(r.handles||[]).length+" Handles · ← "+kurz((r.handles||[]).map(x=>x.event));
    else if(k==="reader")t=(r.handles||[]).length+" Handles · ? "+kurz((r.handles||[]).map(x=>x.query));
    else if(k==="pipeline")t=(r.handles||[]).length+" Handle · → "+kurz((r.handles||[]).flatMap(plAusgaenge));
    else if(k==="saga")t="Auslöser: "+(r.triggerEvent||"—")+" · "+transOf(r).length+" Regeln";
    else if(k==="transition")t="WENN "+kurz(r.wenn)+" → "+kurz((r.dann||[]).map(d=>d.rufe?"ƒ "+d.rufe+(d.zeitlimit?" ⏳":""):d.sende+(d.zeitlimit?" ⏳":"")));
    else if(k==="funktion")t=(r.auftrag||"?")+" → "+((r.ergebnisse||[]).length?kurz(r.ergebnisse):"(kein Ergebnis)");
    else if(k==="auftrag")t="Eingang von ƒ "+(r.funktion||"—");
    else if(k==="store")t=(r.writeFns||[]).length+" schreibend · "+(r.readFns||[]).length+" lesend";
    else if(k==="readmodel"&&r.geteilt)t="⇄ geteilt";
    else if(k==="auf")t="◀ "+(r.eingang||"?")+" → "+((r.ausgaenge||[]).length?kurz(r.ausgaenge)+(r.strom?" (Strom)":""):"zur Kenntnis");
    else if(k==="akteur")t=(ART_SYM[r.art]||"")+((r.dienste||[]).length?"⚙ "+r.dienste.join(", ")+" · ":"")+((r.darf||[]).length?"darf "+kurz(r.darf):"darf nichts")
      +((r.vertrag||[]).length?" · Zusagen auf "+kurz(r.vertrag.map(x=>x.eingang)):"");
    else if(k==="handle"){const aus=n.own.kind==="pipeline"?plAusgaenge(r):(r.ausgaenge||[]).length?(r.ausgaenge||[]).filter(a=>a.art!=="storefn"&&a.art!=="self").map(a=>a.typ)
        :[...(r.responses||[]),...(r.sends||[]),...(r.emits||[]),...(r.publishes||[])];
      const fns=(r.fns||[]).map(fid=>{const f=fnById(fid);return f?f.fn.name:null;}).filter(Boolean);
      t=(aus.length?"→ "+[...new Set(aus)].join(" | "):(fns.length?"":"→ nichts"))+(fns.length?(aus.length?" · ":"")+"ruft "+kurz(fns):"")+(r.signaturOffen?" · ⚠ offen":"");}
    else if(k==="fn")t="("+(r.params||[]).map(x=>x.typ).join(", ")+")"+(r.rueckgabe?" → "+r.rueckgabe:"");
    else if(k==="codenode"){const v=vorschlagFuer(r._id);t=v?"🤖 Vorschlag wartet auf ✓ Übernehmen":((r.text||"").trim().split("\n")[0]||"// leer");}
    else if(k==="llmnode"){const kid=r.promptZiel?konsolenId(r.promptZiel):null,m=kid&&MELD[kid];
      t=(kid&&LAUF[kid]?"● "+LAUF[kid].was+" … · ":kid&&VOR[kid]?"🤖 Vorschlag im Block · ":m?m.t.slice(0,40)+" · ":"")+(((llmVerlauf(r).slice(-1)[0]||{}).text||"").split("\n")[0]||"(noch kein Prompt)");}
    if(Array.isArray(r.felder)&&k!=="aggregate")t+=(t?" · ":"")+r.felder.length+" Felder";
    // Partner in nicht geladenen Domänen: die Kante endet nicht im Leeren, sie ist benannt (Panel: „↗ außerhalb", Klick lädt).
    {const za=aussenAnzahl(n);if(za)t+=(t?" · ":"")+"↗ "+za+" außerhalb";}
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
  // Ansicht Landkarte ⇄ Ablauf: NUR per Hand umgeschaltet (VIEW.lod) — der Zoom schaltet nichts mehr um.
  let LOD=null;
  function setzeLod(l){VIEW.lod=l==="karte"?"karte":"ablauf";speichereAnsicht();pruefeLod();}
  // --inv (CSS, .gworld): fester Größenfaktor der Landkarte (so groß wie früher bei Zoom 0,25) — Schrift und Linien zoomen mit, sehen also immer gleich aus.
  function pruefeLod(){if(!canvas||!world)return;
    const l=VIEW.lod==="karte"?"karte":"ablauf";if(l===LOD)return;const vorher=LOD;LOD=l;
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
      // Schrift in Welt-Einheiten, passend zur Kachelgröße: zoomt mit (sieht immer gleich aus) und füllt die Kachel lesbar.
      const W=t.x2-t.x1+2*PAD,H=t.y2-t.y1+2*PAD,fs=Math.round(Math.max(28,Math.min(W/9,H/3.2,220)));
      div.style.padding=Math.round(fs*0.45)+"px "+Math.round(fs*0.55)+"px";div.style.borderWidth=Math.max(3,Math.round(fs/12))+"px";div.style.borderRadius=Math.round(fs*0.5)+"px";
      div.firstChild.style.fontSize=fs+"px";div.lastChild.style.fontSize=Math.round(fs*0.55)+"px";div.lastChild.style.marginTop=Math.round(fs*0.25)+"px";
      div.onclick=()=>{if(PANNED)return;setzeLod("ablauf");passeEin(t.ids,1,0.45);};
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

  // ══ DOMÄNEN-RAHMEN: rein visuelle Blöcke UM das bestehende Layout (Rollen-Spalten, Code/🤖 unter dem Besitzer bleiben).
  //   Domäne = Namespace, hierarchisch (Unterdomänen liegen im Rahmen ihrer Eltern-Domäne; packLayout sortiert danach).
  //   Der Rahmen folgt den ECHTEN Kartenpositionen (GEO) — zieht man Karten, wächst er mit. Kopf = kleine Menüleiste.
  //   Leere neue Domänen (VIEW.domNeu) und die Heimat namespace-loser Knoten (VIEW.heim) leben nur in der Ansicht.
  const RP=36, RK=46, SPK=34, SPAD=8;   // Domänen-Rand/-Kopf, Spalten-Kopf/-Rand (Welt-px) — packLayout reserviert RP/RK je Region, SPK je Spalte
  const AP=22, AK=40, AGAP=28, AKLEER_W=560;   // Akteur-Rahmen (Domäne × Akteur, §12): Rand, Kopf, Abstand, Breite eines leeren
  const OHNE_DOM="§ohne";
  // Domänen-Zugehörigkeit steht über allem: gehört ein Baustein laut GRAPH zu einem Aggregat (groupKeyOf — Projektion über
  //   ihre Events, Reader/Store/ReadModel/Query/Response über ihre Projektion, Code über seinen Besitzer …), ist seine Domäne
  //   der Namespace dieses Aggregats — egal, in welchem Namespace die Klasse liegt (z. B. Domain.Projections). Nur
  //   aggregat-übergreifende Bausteine (Prozesse, Pipelines, geteilte Typen) fallen auf ihren eigenen Namespace zurück.
  //   Je Render einmal berechnet (DOMKEY, geleert in berechneSicht) — zeichneRahmen läuft auch beim Ziehen.
  let DOMKEY=new Map(), FRGEO=new Map();   // FRGEO: Rahmen-Geometrie je Schlüssel (Domäne|Akteur, §vertrag:Akteur) — Ziel der Client-Leitungen
  const aggDom=n=>{const g=groupKeyOf(n),a=g!==SHARED_KEY&&MODEL.aggregate.find(x=>x.name===g);return (a&&a.namespace)||null;};
  // JEDER Baustein ohne eigenes Aggregat (Feldtyp-VO/Enum, Response, aber auch Pipeline/Reaktion/Prozess samt Handles,
  //   Triggern, Code) folgt seinen Graph-Nachbarn, wenn die alle in GENAU EINER Aggregat-Domäne liegen. Die Suche läuft
  //   transitiv durch aggregat-lose Knoten (Pipeline ↔ Handle ↔ Trigger …) und stoppt an Aggregat-Domänen → eine
  //   Pipeline-Einheit landet geschlossen in ihrer Domäne. Berührt sie mehrere Domänen, bleibt sie bei ihrem Namespace.
  //   (a) EINHEIT = Ablauf-Bausteine, die zusammengehören: Pipeline/Reaktion ↔ Handles ↔ Trigger ↔ Code/🤖 ↔ Dienst,
  //       Prozess ↔ Regeln. Die Suche läuft NUR durch solche Bausteine (nicht durch Datentypen — die würden fremde Einheiten
  //       verkleben) und sammelt die Aggregat-Domänen ihrer Nachbarn. Genau eine → die. Sonst entscheidet, wohin die Einheit
  //       COMMANDS schickt (sie gehört dorthin, wo sie wirkt; Lesen woanders ist nur Abhängigkeit). Je Einheit einmal.
  //   (b) Datentypen (VO/Enum/Konfig/Response …) folgen schrittweise ihren Nachbarn (Enum → VO → ReadModel → Domäne).
  const EINHEIT_ARTEN=new Set(["saga","transition","reaktion","pipeline","handle","trigger","codenode","llmnode","dienst","hostsetting","frist"]);
  let EINHEIT=new Map();
  function einheitDom(n){if(EINHEIT.has(n.id))return EINHEIT.get(n.id);
    const seen=new Set([n.id]),q=[n.id],ds=new Set(),dc=new Set();
    const nachbarn=id=>{const m=NODEBY.get(id),r=[...(ADJ.inn.get(id)||[]),...(ADJ.out.get(id)||[])];
      if(m&&m.kind==="saga")MODEL.transitions.forEach(t=>{if(t.prozess===m.name)r.push("tr:"+t._id);});
      if(m&&m.kind==="transition"&&m.ref.prozess)r.push("saga:"+m.ref.prozess);return r;};
    while(q.length){nachbarn(q.pop()).forEach(x=>{const m=NODEBY.get(x);if(!m)return;const z=aggDom(m);
      if(z){ds.add(z);if(m.kind==="command")dc.add(z);return;}
      if(EINHEIT_ARTEN.has(m.kind)&&!seen.has(x)){seen.add(x);q.push(x);}});}
    const d=ds.size===1?[...ds][0]:dc.size===1?[...dc][0]:null;seen.forEach(id=>EINHEIT.set(id,d));return d;}
  function datenDom(n,besucht){if(besucht.has(n.id))return null;besucht.add(n.id);const ds=new Set();
    [...(ADJ.inn.get(n.id)||[]),...(ADJ.out.get(n.id)||[])].forEach(x=>{const m=NODEBY.get(x),z=m&&graphDom(m,besucht);if(z)ds.add(z);});
    return ds.size===1?[...ds][0]:null;}
  function graphDom(n,besucht){return aggDom(n)||(EINHEIT_ARTEN.has(n.kind)?einheitDom(n):datenDom(n,besucht||new Set()));}
  function domKey(n){let d=DOMKEY.get(n.id);if(d)return d;
    // Akteur-Karte: in der ERSTEN Domäne, in der er wirkt (dort steht sie in seinem Akteur-Rahmen; §12.5) — sonst wie gehabt.
    // Zusage: der Vertrag steht GESCHLOSSEN beim Akteur (ein Rahmen), die Kanten führen zu den Events hinüber.
    if(n.kind==="auf"){const x=domKey(NODEBY.get(n.own.id)||n.own);DOMKEY.set(n.id,x);return x;}
    // Client: ein eigener Rahmen außerhalb der Domänen (rechts daneben gepackt).
    if(n.kind==="client"){const x=CL_PRE+n.ref.name;DOMKEY.set(n.id,x);return x;}
    if(n.kind==="akteur"){const eig=[...new Set([...(n.ref.darf||[]),...(n.ref.vertrag||[]).map(r=>r.eingang)].map(nm=>NODEBY.get("rec:"+nm)||[...NODEBY.values()].find(x=>x.kind==="trigger"&&trigName(x.ref)===nm))
        .filter(Boolean).map(domKey))].sort(domOrd),ds=eig.length?eig:akteurDomaenen(n.ref.name);
      if(ds.length){DOMKEY.set(n.id,ds[0]);return ds[0];}}
    d=graphDom(n)||nsVon(n)||(VIEW.heim||{})[n.id]||OHNE_DOM;DOMKEY.set(n.id,d);return d;}
  const domLabel=ns=>ns===OHNE_DOM?"⋯ ohne Domäne":istClientDom(ns)?"🔌 "+clientAnzeige({name:ns.slice(CL_PRE.length)}):letztesSeg(ns);
  // Block (Aggregat bzw. Brücke) eines Knotens: aus dem Graphen; ein über eine Spalte angelegter, noch unverdrahteter Knoten
  //   behält bis zur Verdrahtung den Block dieser Spalte (VIEW.heimBlk) — statt im Inselkasten zu landen.
  const blockVon=n=>subGroupOf(n)||(VIEW.heimBlk||{})[n.id]||SHARED_KEY;
  const istInselLage=n=>ISLE.has(n.id)&&!(VIEW.heimBlk||{})[n.id];
  const domOrd=(a,b)=>istClientDom(a)!==istClientDom(b)?(istClientDom(a)?1:-1):a===OHNE_DOM?1:b===OHNE_DOM?-1:a.localeCompare(b);
  const bausteine=z=>z+(z===1?" Baustein":" Bausteine");
  // Gemeinsame Wurzel (z. B. „Domain“) — darunter beginnen die Domänen (wie im Start-Dialog).
  function domWurzel(){const alle=new Set();graphNodes().forEach(n=>{const x=domKey(n)===OHNE_DOM||istClientDom(domKey(n))?null:domKey(n);if(x)for(let p=x;p;p=elternNs(p))alle.add(p);});
    (VIEW.domNeu||[]).forEach(x=>{for(let p=x;p;p=elternNs(p))alle.add(p);});
    let top="";for(;;){const k=[...alle].filter(x=>elternNs(x)===top);if(k.length!==1||![...alle].some(y=>elternNs(y)===k[0]))break;top=k[0];}
    return top;}
  // Baum über die gegebenen Domänen (+ ihre Eltern-Namespaces bis unter die Wurzel).
  function domBaum(keys){const w=domWurzel(),alle=new Set();
    keys.forEach(ns=>{if(ns===OHNE_DOM||ns===w||istClientDom(ns)){alle.add(ns);return;}for(let p=ns;p&&p!==w;p=elternNs(p)){alle.add(p);if(!drinNs(w,p))break;}});
    const elternIn=ns=>{if(ns===OHNE_DOM||ns===w||istClientDom(ns))return null;const e=elternNs(ns);return alle.has(e)&&e!==w?e:null;};
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
    // Akteur-Rahmen (Domäne × Akteur, §12): nur in Domänen mit Akteuren; sonst null (ein Block-Layout wie bisher).
    const aktVon=n=>akteurOrdnung(domKey(n)).length?akteurVon(n):null;
    GEO.forEach((g,id)=>{const n=NODEBY.get(id);if(!n)return;const d=domKey(n);
      if(n.kind==="client")return;   // die Client-Karte IST die Anschlussleiste — keine Spalte darum
      if(istInselLage(n)){nimm(sp(d+"|§insel",{dom:d,blk:null,role:"insel",akt:null}),id,g,n.kind);return;}
      if(n.kind==="codenode"||n.kind==="llmnode"){codes.push([n,g,id]);return;}
      const blk=blockVon(n),role=rolle(n,rollenVon(blk)),akt=aktVon(n);
      nimm(sp(d+"|"+akt+"|"+blk+"|"+role,{dom:d,blk,role,akt}),id,g,n.kind==="handle"?"handle:"+n.own.kind:n.kind);});
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
      const kinds=[...c.kinds].sort((x,y)=>ix(x)-ix(y)),haupt=kinds[0]||"codenode";
      const titel=c.role==="insel"?"⚠ Inseln":c.role==="code"?"📝 Code":[...new Set(kinds.map(spaltenName))].join(" · ");
      const neu=spaltenNeu(c);
      const k=h("div",{class:"gspalte-k",title:titel+" — "+(c.blk===AKTEUR_KEY?"👤 Akteur-Band · ":c.blk&&c.blk!==SHARED_KEY?"Aggregat "+c.blk+" · ":"")+domLabel(c.dom)},
        h("i",{class:"kc-"+(haupt.startsWith("handle:")?"handle":haupt)}),h("span",{class:"gs-t"},titel),h("span",{class:"gs-z"},String(c.ids.length)),
        neu.length?h("button",{title:"In dieser Spalte anlegen",onclick:e=>{e.stopPropagation();
          if(neu.length===1)spalteAnlegen(c,neu[0]);else menue(e.clientX,e.clientY,titel,neu.map(kk=>({t:"＋ "+(NODELABEL[kk]||kk),fn:()=>spalteAnlegen(c,kk)})));}},"＋"):null,
        h("button",{title:"Spalte markieren (Slice)",onclick:e=>{e.stopPropagation();FOCUS=new Set(c.ids);wendeFokusAn();}},"◎"));
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
      h("button",{title:"Vertrag markieren (Slice)",onclick:e=>{e.stopPropagation();FOCUS=new Set(c.ids);wendeFokusAn();}},"◎"),
      h("button",{title:"Vertrag einpassen",onclick:e=>{e.stopPropagation();einpassenRahmen(r);}},"⤢"));
    k.ondblclick=e=>{e.stopPropagation();einpassenRahmen(r);};
    el.append(k);return el;}
  // Ein Akteur-Rahmen (Domäne × Akteur, §12.5): Kopf = Akteur (Klick → sein Panel) · Domäne · Zahl · „↥ auch“ (Doppelungen,
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
        onclick:e=>{e.stopPropagation();menue(e.clientX,e.clientY,"＋ für "+a.akt,["command","query","trigger"].map(kk=>({t:"＋ "+(NODELABEL[kk]||kk),fn:()=>fuerAkteurAnlegen(a,kk)})));}},"＋"):null,
      h("button",{title:"Rahmen markieren (Slice)",onclick:e=>{e.stopPropagation();FOCUS=new Set(a.ids);wendeFokusAn();}},"◎"),
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
  function akteurDirektIn(ak,dom){return (ak.darf||[]).some(nm=>{const n=NODEBY.get("rec:"+nm)||[...NODEBY.values()].find(x=>x.kind==="trigger"&&trigName(x.ref)===nm);return n&&domKey(n)===dom;});}
  // Doppelungen: Bausteine dieser Domäne, die auch dem Akteur gehören, aber im Rahmen eines früheren Akteurs stehen.
  function auchWoanders(name,dom){const r=[];const ART=["command","query","trigger","event","rejection","aggregate","reader","projektion","pipeline","reaktion","saga","handle","store","readmodel","queryresponse"];
    NODEBY.forEach(n=>{if(!VIS.has(n.id)||!ART.includes(n.kind)||domKey(n)!==dom||!akteurSet(n).has(name))return;const wo=akteurVon(n);if(wo!==name)r.push([n,wo]);});
    return r.sort((x,y)=>ART.indexOf(x[0].kind)-ART.indexOf(y[0].kind)||x[0].name.localeCompare(y[0].name));}
  function zeigeKnoten(id){const g=GEO.get(id);if(g)einpassenRahmen({x1:g.x-260,y1:g.y-160,x2:g.x+g.w+260,y2:g.y+g.h+160});waehle(id);}
  // ＋ im Akteur-Rahmen: der neue Eingang gehört sofort diesem Akteur (IDarf) — Entwerfen „für wen".
  function fuerAkteurAnlegen(a,kind){const ag=MODEL.aggregate.find(x=>x.namespace===a.dom);
    neuerKnoten(kind,undefined,undefined,{ns:a.dom===OHNE_DOM?null:a.dom,agg:ag&&ag.name,blk:kind==="trigger"?SHARED_KEY:(ag?ag.name:SHARED_KEY),akt:a.akt});}
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
  const spaltenName=k=>k==="handle:projektion"?"Projektion-Handle":k==="handle:reader"?"Reader-Handle":k==="handle:pipeline"?"Pipeline-Handle":k==="handle:reaktion"?"Reaktion-Handle":NODELABEL[k]||k;
  const NICHT_FREI=new Set(["fn","handle","auf"]);
  function spaltenNeu(c){if(c.role==="insel")return [];if(c.role==="code")return ["codenode","llmnode"];
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
    ["Typen",["valueobject","enum","konfig"]],["Abläufe",["saga","transition","funktion","reaktion","pipeline","trigger"]],
    ["Leseseite",["readmodel","store","projektion","query","queryresponse","reader"]],["Betrieb",["dienst","hostsetting"]],["Code",["codenode","llmnode"]]];
  function bausteinMenue(cx,cy,ns){const agg=MODEL.aggregate.find(a=>a.namespace===ns),sg=MODEL.sagas.find(s=>s.namespace===ns);
    const it=[];BAU_GRUPPEN.forEach(([g,ks])=>{it.push({kopf:g});ks.forEach(k=>it.push({t:"＋ "+(k==="codenode"?"📝 Code":k==="llmnode"?"🤖 LLM":NODELABEL[k]||k),
      fn:()=>neuerKnoten(k,undefined,undefined,{ns:ns===OHNE_DOM?null:ns,agg:agg&&agg.name,saga:sg&&sg.name})}));});
    menue(cx,cy,"＋ in "+domLabel(ns),it);}
  function domMenue(cx,cy,r){const neu=(VIEW.domNeu||[]).includes(r.ns);
    menue(cx,cy,r.ns===OHNE_DOM?domLabel(r.ns):r.ns,[
      {t:"＋ Baustein…",fn:()=>bausteinMenue(cx,cy,r.ns)},
      r.ns!==OHNE_DOM?{t:"＋ Unterdomäne…",fn:()=>neueDomaene(cx,cy,r.ns)}:null,
      {t:"⤢ Einpassen",fn:()=>einpassenRahmen(r)},
      r.alleIds.length?{t:"◎ Slice: alle Bausteine markieren",fn:()=>{FOCUS=new Set(r.alleIds);wendeFokusAn();}}:null,
      neu?{trenn:1}:null,
      neu?{t:"✕ Leere Domäne entfernen",gefahr:1,aus:!r.leer,title:r.leer?"":"Erst die Bausteine löschen — eine Domäne mit Inhalt entsteht aus dem Code",
        fn:()=>{VIEW.domNeu=(VIEW.domNeu||[]).filter(x=>x!==r.ns);speichereAnsicht();render();}}:null]);}
  // Rechtsklick auf die leere Fläche: neue Domäne.
  function flaechenMenue(e){e.preventDefault();menue(e.clientX,e.clientY,"Fläche",[{t:"＋ Neue Domäne…",fn:()=>neueDomaene(e.clientX,e.clientY,null)}]);}

  // Ansichts-Leiste über der Fläche.
  function ansichtLeiste(){
    const btn=(lbl,on,title,fn)=>h("button",{class:on?"on":"",title,onclick:fn},lbl);
    const lod=h("span",{class:"lod",title:"Ansicht umschalten: Landkarte (Bereiche) oder Ablauf (Karten) — der Zoom schaltet nicht um"},
      ...[["karte","Landkarte"],["ablauf","Ablauf"]].map(([l,t])=>{const sp=h("span",{onclick:()=>{setzeLod(l);if(l==="karte")passeEin([...VIS],0.35);}},t);sp.dataset.l=l;if((VIEW.lod||"ablauf")===l)sp.classList.add("on");return sp;}));
    return h("div",{class:"gview"},
      h("b",{style:"color:#cbd3e1"},"Ansicht"),
      btn("＋ Domäne",false,"Neue Domäne (Namespace) anlegen — Rechtsklick auf die Fläche geht auch",e=>neueDomaene(e.clientX,e.clientY+20,null)),
      h("button",{title:"Geladene Domänen ändern",onclick:()=>deLaden()},"📂 "+(LADEN===null?"alles geladen":!LADEN.length?"leer":LADEN.map(letztesSeg).join(", "))),
      btn("⤢ Alles zeigen",false,"Ganzes Board einpassen",()=>passeEin([...VIS],1)),
      btn("👁 Ausblenden"+((VIEW.aus||[]).length?" ("+VIEW.aus.length+")":"")+" ▾",FILTER_OPEN,"Knotenarten und Domänen ein-/ausblenden",()=>{FILTER_OPEN=!FILTER_OPEN;render();}),
      lod,
      h("span",{style:"margin-left:8px"},"Karte antippen = Panel · ⊕ im Panel = passende Knoten leuchten, anklicken = verbinden/lösen · Esc = fertig · Kopf ziehen = verschieben"));}

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
      tb("akteur","+ 👤 Akteur"),tb("command","+ Command"),tb("event","+ Event"),tb("rejection","+ Ablehnung"),
      tb("valueobject","+ Value Object"),tb("konfig","+ Konfig"),tb("enum","+ Enum"),tb("aggregate","+ Aggregat"),
      tb("state","+ State"),tb("decider","+ Decider"),tb("applier","+ Applier"),tb("saga","+ Prozess"),tb("transition","+ Regel"),
      tb("readmodel","+ Read Model"),tb("store","+ Store"),tb("projektion","+ Projektion"),tb("query","+ Query"),tb("queryresponse","+ Response"),tb("reader","+ Reader"),tb("reaktion","+ Reaktion"),
      tb("trigger","+ Trigger"),tb("pipeline","+ Pipeline"),
      tb("funktion","+ ƒ Funktion"),tb("dienst","+ Dienst"),tb("hostsetting","+ HostSetting"),
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
      if(t.closest&&t.closest(".gnode2,.ginsp,.gfilter,.gminimap,.gtile,.gpick"))return;if(SEL)waehle(null);};
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
      opt("akteur","👤 Akteur"),opt("command","Command"),opt("event","Event"),opt("rejection","Ablehnung"),
      opt("valueobject","Value Object"),opt("konfig","Konfiguration"),opt("enum","Enum"),opt("aggregate","Aggregat"),
      opt("state","State"),opt("decider","Decider"),opt("applier","Applier"),opt("saga","Prozess"),opt("transition","Regel"),
      opt("readmodel","Read Model"),opt("store","Store"),opt("projektion","Projektion"),opt("query","Query"),opt("queryresponse","Response"),opt("reader","Reader"),opt("reaktion","Reaktion"),
      opt("trigger","Trigger"),opt("pipeline","Pipeline"),
      opt("funktion","ƒ Funktion"),opt("dienst","Dienst"),opt("hostsetting","HostSetting"),
      opt("codenode","📝 Code"),opt("llmnode","🤖 LLM"));
    canvas.append(pick);
    setTimeout(()=>{const off=ev=>{if(!pick.contains(ev.target)){pick.remove();document.removeEventListener("pointerdown",off);}};document.addEventListener("pointerdown",off);},0);
  }

  // Prozess-HUB: Prozess<Auslöser>. Transitionen stecken sich HIER an (sichtbare Kante, keine Ableitung).
  function prozessHubCard(body,s){
    body.append(topSlot("event","Auslöser (startet): "+(s.triggerEvent||"— ⊕ Event wählen"),{type:"evtUse",dir:"in",saga:s.name,trigger:true},"saga:trigger:"+s.name));
    body.append(nameInp(s,"name","Prozess","saga"));
    body.append(h("input",{value:s.namespace??"",oninput:e=>s.namespace=e.target.value,onchange:()=>render(),placeholder:"Namespace"}));
    const aggs=sagaAggs(s);
    body.append(h("div",{class:"gsec"},"berührt (abgeleitet): "+(aggs.length?aggs.join(" · "):"—")));
    body.append(h("div",{class:"gsec"},"Regeln ◀ (anstecken)"));
    transOf(s).forEach(t=>{const p=anchorDot("prozess");p.classList.add("i");reg("hub:in:"+s.name+":"+t._id,p,null);
      body.append(h("div",{class:"slotrow"},p,h("span",{class:"slotlbl"},"WENN "+((t.wenn||[])[0]||"?")+((t.wenn||[]).length>1?" +"+((t.wenn.length-1)+(t.sammelEvent?1:0))+"":(t.sammelEvent?" +alle":""))+" → "+((t.dann||[]).map(d=>d.rufe?"ƒ "+d.rufe:(d.sende||"?")).join(", ")||"?"))));});
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
      if(d.rufe!==undefined){
        // Aufruf-Knoten: die Funktion steht ausdrücklich da (Rufe<F>), ihre Ergebnisse sind die nächsten Events im Graph.
        const fa=anchorDot("command");fa.classList.add("o");reg("tr:rufe:"+t._id+":"+di,fa,null);
        const sel=h("select",{onchange:e=>{d.rufe=e.target.value;delete d.sendeAusdruck;render();}});
        if(!d.rufe||!MODEL.funktionen.some(f=>f.name===d.rufe)){const o=h("option",{value:d.rufe||""},d.rufe||"— Funktion wählen —");o.selected=true;sel.append(o);}
        MODEL.funktionen.forEach(f=>{const o=h("option",{value:f.name},"ƒ "+f.name);if(f.name===d.rufe)o.selected=true;sel.append(o);});
        body.append(h("div",{class:"slotrow o"},
          h("button",{class:"rm",onclick:()=>{t.dann.splice(di,1);if(!t.dann.length)t.dann.push({});render();}},"✕"),
          h("span",{class:"slotlbl",style:"opacity:.75"},"Dann rufe"),sel,fa));
        const fk=MODEL.funktionen.find(f=>f.name===d.rufe);
        if(fk)body.append(h("div",{class:"gsec",style:"opacity:.7"},"Auftrag "+(fk.auftrag||"?")+" → "+((fk.ergebnisse||[]).join(" | ")||"(kein Ergebnis)")));
        if(d.sendeAusdruck)body.append(h("div",{class:"gsec",title:"aus dem Code gelesen — wird verbatim zurückgeschrieben",style:"font-family:monospace;opacity:.7;white-space:pre-wrap"},"λ "+d.sendeAusdruck));
        body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"⏳"),inp(d.zeitlimit,v=>{if(v)d.zeitlimit=v;else delete d.zeitlimit;},"Zeitlimit, z. B. TimeSpan.FromSeconds(30)")));
        const ko2=port("rejection");ko2.classList.add("o");ko2.title=d.kompensation||"(Kompensation)";reg("tr:komp:"+t._id+":"+di,ko2,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"komp"});
        body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.55"},"↩ "+(d.kompensation||"")),ko2));
        return;}
      const so=port("command");so.classList.add("o");so.title=d.sende||"(Command)";reg("tr:sende:"+t._id+":"+di,so,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"sende"});
      body.append(h("div",{class:"slotrow o"},
        h("button",{class:"rm",onclick:()=>{t.dann.splice(di,1);if(!t.dann.length)t.dann.push({});render();}},"✕"),
        h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.75"},"Dann "+(d.sende||"")),
        mk("×N","Fan-out (SendeJe) — N Commands je Element",!!d.sendeJe,()=>{d.sendeJe=d.sendeJe?undefined:true;render();}),so));
      if(d.sendeAusdruck)body.append(h("div",{class:"gsec",title:"aus dem Code gelesen — wird verbatim zurückgeschrieben",style:"font-family:monospace;opacity:.7;white-space:pre-wrap"},"λ "+d.sendeAusdruck));
      if(d.sende||d.zeitlimit)body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl"},"⏳"),inp(d.zeitlimit,v=>{if(v)d.zeitlimit=v;else delete d.zeitlimit;},"Zeitlimit (optional), z. B. TimeSpan.FromMinutes(5)")));
      const ko=port("rejection");ko.classList.add("o");ko.title=d.kompensation||"(Kompensation)";reg("tr:komp:"+t._id+":"+di,ko,{type:"sagaCmd",dir:"out",trans:t._id,dannIdx:di,role:"komp"});
      body.append(h("div",{class:"slotrow o"},h("span",{class:"slotlbl",style:"flex:1;text-align:right;opacity:.55"},"↩ "+(d.kompensation||"")),ko));
    });
    body.append(h("button",{class:"add",onclick:()=>{(t.dann=t.dann||[]).push({});render();}},"+ Dann (weiterer Command am selben Join)"));
    body.append(h("button",{class:"add",onclick:()=>{(t.dann=t.dann||[]).push({rufe:(MODEL.funktionen[0]||{}).name||""});render();}},"+ Dann ƒ (Katalog-Funktion rufen)"));
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
    // Blank-Start: leer, der Start-Dialog fragt, welche Domänen geladen werden (Vorauswahl: die letzte Wahl).
    LADEN=[];START_OFFEN=true;START_ERST=true;
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
    triggers:x=>x.msgName||x.name,frists:x=>x.name,dienste:x=>x.name,hostSettings:x=>x.name,akteure:x=>x.namespace+"|"+x.name,
    clients:x=>x.namespace+"|"+x.name,funktionen:x=>x.namespace+"|"+x.name};
  const LAYOUT=new Set(["x","y","ausCode","ungeschrieben","codeSrc","leer","rumpf","schritte","herkunft"]);
  function inhalt(o){return JSON.stringify(o,(k,v)=>(k.startsWith("_")||LAYOUT.has(k))?undefined:v);}
  // Leseseite: „steht die Änderung schon im Code?“ — Fn-Ids (je Einlesen neu nummeriert) über „Store.Fn“ vergleichen und die
  //   Code-Fakten, die erst der Code liefert (Signatur verbatim, Datei, Fähigkeits-Name, Vertrag), nicht mitzählen.
  const LESE_KOLL=new Set(["stores","projektionen","reaktionen","reader","pipelines","readModels","triggers"]);
  const CODEFAKT=new Set(["sig","code","impl","datei","doku","faehigkeit","form","signatur","signaturOffen","ausgaenge","istBuendel","bindung","input","delay","entwurf"]);
  function fnNamen(m){const x=new Map();(m&&m.stores||[]).forEach(st=>(st.writeFns||[]).concat(st.readFns||[]).forEach(f=>x.set(f._id,st.name+"."+f.name)));return x;}
  function leseInhalt(o,fnName){return JSON.stringify(o,(k,v)=>{if(k.startsWith("_")||LAYOUT.has(k)||CODEFAKT.has(k)||v===null)return undefined;
    if(k==="fns"&&Array.isArray(v))return v.map(id=>fnName.get(id)||id);return v;});}
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
    const fnAlt=fnNamen(alt),fnNeu=fnNamen(neu);
    const imCode=(col,s,l)=>LESE_KOLL.has(col)?leseInhalt(s,fnAlt)===leseInhalt(l,fnNeu):inhalt(s)===inhalt(l);
    for(const col in MERGE_KEYS){const key=MERGE_KEYS[col];
      const liveByKey=new Map((neu[col]||[]).map(x=>[x._codeKey,x]));
      const erg=[];const benutzt=new Set();
      (alt[col]||[]).forEach(s=>{
        const k=s.ausCode?s._codeKey:key(s);const l=liveByKey.get(k);
        if(l){benutzt.add(k);
          const geaendert=s.ausCode&&s._herkunft&&hash(inhalt(s))!==s._herkunft&&!imCode(col,s,l);
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
    // 🤖-Knoten: über den SLOT-Schlüssel (Art|Besitzer|Disc) wieder an „ihren" Code-Block hängen — für JEDE Slot-Art.
    //   Code-Knoten-Ids werden beim Einlesen neu vergeben; nur Decider/Applier ließen sich über den Besitzer umziehen.
    const neuBySlot=new Map();neu.codeNodes.forEach(c=>{const k=konsolenId(c._id,neu);if(k&&!neuBySlot.has(k))neuBySlot.set(k,c._id);});
    neu.llmNodes=(alt.llmNodes||[]).map(l=>{const slot=(l.promptZiel&&konsolenId(l.promptZiel,alt))||l.promptSlot||null;
      const ziel=(slot&&neuBySlot.get(slot))||codeUmzug.get(l.promptZiel)||l.promptZiel;
      const x={...l,promptZiel:ziel};if(slot)x.promptSlot=slot;return x;});
    // Transitionen (Saga-Regeln): unverändert → Code-Stand; im Board geändert/ergänzt → Board-Stand je Prozess.
    const altTr=(alt.transitions||[]);
    const proProzess=p=>altTr.filter(t=>t.prozess===p);
    neu.sagas.forEach(sg=>{const at=proProzess(sg.name);if(!at.length)return;
      const lt=neu.transitions.filter(t=>t.prozess===sg.name);
      const edit=at.some(t=>(!t.ausCode&&!altFormat)||(t._herkunft&&hash(inhalt(t))!==t._herkunft));
      if(edit){neu.transitions=neu.transitions.filter(t=>t.prozess!==sg.name).concat(at.map(t=>({...t,ungeschrieben:true})));MERGE_INFO.ungeschrieben++;}
      else{const sig=t=>JSON.stringify([t.wenn||[],t.sammelEvent||"",(t.dann||[]).map(d=>[d.sende||"",d.rufe||"",d.kompensation||""])]);
        const altBySig=new Map(at.map(t=>[sig(t),t]));
        lt.forEach(t=>{const a=altBySig.get(sig(t));if(a&&a.x!=null){t.x=a.x;t.y=a.y;}});}});
    // Entwurfs-Transitionen neuer (Entwurfs-)Prozesse behalten.
    altTr.filter(t=>!neu.sagas.some(sg=>sg.name===t.prozess)&&neu.transitions.indexOf(t)<0).forEach(t=>neu.transitions.push(t));
    return neu;}
  // Node-Art (graphNodes().kind) → Modell-Sammlung (für die Entwurf-Markierung: nur Sammlungen, die aus dem Code kommen).
  function kollektionVon(kind){return {command:"records",event:"records",rejection:"records",valueobject:"records",konfig:"records",query:"records",queryresponse:"records",
    enum:"enums",aggregate:"aggregate",decider:"decider",applier:"applier",saga:"sagas",readmodel:"readModels",store:"stores",
    projektion:"projektionen",reaktion:"reaktionen",reader:"reader",pipeline:"pipelines",trigger:"triggers",frist:"frists",dienst:"dienste",hostsetting:"hostSettings",akteur:"akteure",client:"clients",funktion:"funktionen",auftrag:"records"}[kind]||null;}
  function meldeMerge(){const u=MERGE_INFO.ungeschrieben,e=MERGE_INFO.entwuerfe;
    if(u||e)deFlash("↔ Code geladen · "+u+" ungeschrieben · "+e+" Entwurf/Entwürfe",true);}
  window.deDownload=function(){deriveMembership();prepareSaga();const blob=new Blob([JSON.stringify(MODEL,null,2)],{type:"application/json"});
    const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download="domain-model.json";a.click();};
  // Server-Payload: das MODEL + die Rümpfe (Code-Knoten-Text bzw. "" für bewusst leer) zurück an Decider/Applier.
  // mitVorschlag: offene 🤖-Vorschläge ersetzen den Datei-Rumpf — für Kompilieren + Simulation (echte Generatoren,
  //   in-memory), NIE für „C# schreiben“ (in die Datei kommt ein Vorschlag nur über ✓ Übernehmen).
  function payload(mitVorschlag){deriveMembership();prepareSaga();const m=JSON.parse(JSON.stringify(MODEL));
    const txt=id=>{const v=mitVorschlag?vorschlagFuer(id):null;if(v)return v.rumpf;const c=m.codeNodes.find(x=>x._id===id);return c?c.text:null;};
    [...m.decider,...m.applier].forEach(n=>{if(n.leer)n.rumpf="";else if(n.codeSrc){const t=txt(n.codeSrc);if(t!=null&&t.trim())n.rumpf=t;}});
    // Leseseite/Pipelines: der Code-/LLM-Entwurf einer NEUEN Methode (ohne Code-Signatur) wird ihr Rumpf („entwurf“);
    //   bestehende Rümpfe ändert „C# schreiben“ nicht (dafür: ✓ Übernehmen im LLM-Knoten).
    const entwurf=o=>{if(!o.sig&&o.codeSrc){const t=txt(o.codeSrc);if(t!=null&&t.trim())o.entwurf=t;}};
    [...(m.projektionen||[]),...(m.reaktionen||[]),...(m.reader||[]),...(m.pipelines||[])].forEach(o=>(o.handles||[]).forEach(entwurf));
    (m.stores||[]).forEach(st=>[...(st.writeFns||[]),...(st.readFns||[])].forEach(entwurf));
    return m;}
  async function post(path){const r=await fetch(path,{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(payload(path!=="/api/editor/write"))});
    if(!r.ok)throw new Error("HTTP "+r.status);return r;}
  // &lt;/&gt; C# schreiben: das Modell in die ECHTEN .cs-Dateien schreiben (chirurgisch/additiv über
  //   /api/editor/write). Neue Records/Methoden werden angehängt, Handcode nie überschrieben.
  //   Danach spiegelt der Code-Sync die Datei-Rümpfe zurück ins Board.
  // Bericht eines Schreib-/Vorschau-Laufs ins Ausgabe-Panel: neu, geändert, NICHT geschrieben (mit Grund), Build-Fehler.
  function schreibBericht(b,vorschau){const out=document.getElementById("de-out");if(!out)return;out.innerHTML="";
    const gut=(t)=>out.append(h("div",{class:"find",style:"background:#1f3a2a;color:#9be3bf"},t));
    const nicht=(b.uebersprungen||[]).filter(x=>!/unangetastet/.test(x));
    if(vorschau)out.append(h("div",{class:"find"},"👁 Vorschau — nichts geschrieben:"));
    (b.geschrieben||[]).forEach(x=>gut((vorschau?"würde anlegen: ":"＋ ")+x));
    (b.ergaenzt||[]).forEach(x=>gut((vorschau?"würde ändern: ":"✎ ")+x));
    nicht.forEach(x=>out.append(h("div",{class:"find warning"},"⚠ nicht geschrieben: "+x)));
    if((b.fehler||[]).length){out.append(h("div",{class:"find warning"},b.fehler.length+" Compiler-Fehler nach dem Schreiben — der Rumpf passt nicht mehr zur Signatur:"));
      b.fehler.forEach(f=>out.append(h("div",{class:"find error"},f)));}
    else if(!vorschau&&((b.geschrieben||[]).length||(b.ergaenzt||[]).length))gut("✓ Betroffene Projekte gebaut — 0 Fehler.");
    if(!(b.geschrieben||[]).length&&!(b.ergaenzt||[]).length&&!nicht.length)gut("✓ Code und Board stimmen überein — nichts zu schreiben.");}
  window.deVorschau=async function(){
    try{const r=await fetch("/api/editor/write?trocken=true",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify(payload(false))});
      if(!r.ok)throw 0;badge(true);schreibBericht(await r.json(),true);}
    catch(e){badge(false);deFlash("⚠ SimHost offline",false);}};
  window.deWrite=async function(){
    try{const r=await post("/api/editor/write");const b=await r.json();badge(true);
      const neu=(b.geschrieben||[]).length, erg=(b.ergaenzt||[]).length, ueb=(b.uebersprungen||[]).length;
      if(!neu&&!erg){deFlash("✓ Dateien aktuell — nichts zu schreiben ("+ueb+" unverändert)",true);}
      else{deFlash("✅ geschrieben: "+neu+" neu · "+erg+" ergänzt · "+ueb+" unverändert",true);}
      console.log("C# schreiben:",b);
      // Was geschrieben wurde, was NICHT (mit Grund) + der Bau der betroffenen Projekte.
      schreibBericht(b,false);
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
    try{const r=await postJson("/api/editor/sim/step",{model:payload(true),sessionId:SIM.sid,command:c,values:werte});const res=await r.json();badge(true);
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
          null)));
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
