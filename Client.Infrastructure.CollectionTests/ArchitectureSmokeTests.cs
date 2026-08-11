// ── Client.Infrastructure.CollectionTests/ArchitectureSmokeTests.cs ──
//
// ZWECK: Beweist, dass das Client-Framework (Client.Infrastructure) als
// ARCHITEKTUR produktiv trägt — unabhängig von der ImagePair-Domäne.
//
// Wir definieren hier eine winzige, FREMDE Wegwerf-Domäne ("Posten") und
// verdrahten sie exakt so, wie es der WiringGenerator zur Compile-Zeit täte
// (Subscribe + Dispatch-Switch + AttachToBus). Damit sind die Generatoren aus
// der Kette genommen — getestet wird die MASCHINE, nicht die Domäne noch der
// Codegen. Was ein zweites, echtes Frontend an Fähigkeiten bräuchte, steht hier
// als ausführbarer Vertrag:
//
//   1. Reaktive Schleife + Batching  — ein Changed-Signal pro Dispatch-Zyklus
//   2. Reentranter Publish           — Folge-Events falten sich in EINEN Zyklus
//   3. Intent → Command              — Handler reichert kontextfreien Intent an
//   4. Server-Event → Store-Patch    — Wahrheit kommt zurück, Store patcht
//   5. Hydration-Gate                — "Daten vor View", leere Menge = hydriert
//   6. Versioning-Signal             — Event trägt nur (Id, Version), monoton
//   7. Selektives Rendern            — EffectScope feuert nur bei Facetten-Bump
//   8. Wiring-Vertrag                — ohne AttachToBus kein Changed (negativ)
//
// (Die VirtualCollection — Live-Prepend mit stabilem Cursor — ist bereits in
//  VirtualCollectionTests.cs abgedeckt und wird hier nicht dupliziert.)

using Abstractions;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Collections;
using Client.Infrastructure.Versioning;

namespace Client.Infrastructure.CollectionTests;

// ═══════════════════════════════════════════════════════════════════
//  FREMDE WEGWERF-DOMÄNE — bewusst nicht ImagePair
// ═══════════════════════════════════════════════════════════════════

// Read-Model-Zeile
record PostenZeile(Guid Id, string Text);

// Server-Events (Fakten vom "Server") — tragen nur (Id, Version) im Context
record PostenAngelegt(Guid Id, string Text) : IEvent;
record PostenUmbenannt(Guid Id, string Text) : IEvent;

// Start-Daten als Query-Response-artige Nachricht (wie die QueryBridge sie publiziert)
record PostenGeladen(IReadOnlyList<PostenZeile> Alle) : IClientEvent;

// Command an den "Server" — mit Aggregat-ID
record PostenUmbenennen(Guid Id, string Text) : ICommand { public Guid AggregateId => Id; }

// Client-lokaler Intent — kontextfrei, kennt das Ziel-Aggregat NICHT
record UmbenennenAngefordert(string Text) : IClientEvent;

// ─── Store: Reducer über StoreBase, hydrierbar ───────────────────────
sealed class PostenStore : StoreBase, IHydrationStore
{
    public TrackedMap<Guid, PostenZeile> Posten { get; }
    public Cursor<Guid> Cursor { get; }

    public PostenStore()
    {
        Posten = CreateMap<Guid, PostenZeile>(p => p.Id);
        Cursor = Track(new Cursor<Guid>());
    }

    // void-Handle = Reducer (das erkennt der HandleMethodGenerator als Store)
    public void Handle(PostenGeladen e, MessageContext ctx)
    {
        Posten.ReplaceAll(e.Alle);
        if (e.Alle.Count > 0) Cursor.Select(0, e.Alle[0].Id);
        MarkHydrated();                       // auch bei leerer Menge: "hydriert, aber leer"
    }

    public void Handle(PostenAngelegt e, MessageContext ctx)
    {
        Posten.Put(new PostenZeile(e.Id, e.Text));
        if (Cursor.Id is null) Cursor.Select(0, e.Id);
    }

    public void Handle(PostenUmbenannt e, MessageContext ctx)
        => Posten.Update(e.Id, p => p with { Text = e.Text });

    // Das, was der WiringGenerator als Dispatch-Switch generieren würde.
    public void Dispatch(object msg, MessageContext ctx)
    {
        switch (msg)
        {
            case PostenGeladen e:   Handle(e, ctx); break;
            case PostenAngelegt e:  Handle(e, ctx); break;
            case PostenUmbenannt e: Handle(e, ctx); break;
        }
    }
}

// ─── IntentHandler: übersetzt Intent → Command (liest Store-Kontext) ──
sealed class PostenIntentHandler
{
    private readonly PostenStore _store;
    public PostenIntentHandler(PostenStore store) => _store = store;

    // IEnumerable<object>-Handle = Sync-Handler (erkennt der HandleMethodGenerator)
    public IEnumerable<object> Handle(UmbenennenAngefordert intent, MessageContext ctx)
    {
        if (_store.Cursor.Id is Guid id)                 // Kontext aus dem Store
            yield return new PostenUmbenennen(id, intent.Text);
    }

    // Was der Generator als Dispatch(..., publish) erzeugt.
    public void Dispatch(object msg, MessageContext ctx, Action<object> publish)
    {
        if (msg is UmbenennenAngefordert intent)
            foreach (var produced in Handle(intent, ctx))
                publish(produced);
    }
}

// ═══════════════════════════════════════════════════════════════════
//  TESTS
// ═══════════════════════════════════════════════════════════════════

public class ArchitectureSmokeTests
{
    // Verdrahtet Bus + Store exakt wie der generierte SubscribeAll.
    private static (ClientBus bus, PostenStore store) WireStore()
    {
        var bus = new ClientBus(null);
        var store = new PostenStore();

        store.AttachToBus(bus);                          // DispatchCompleted → Flush
        bus.Subscribe<PostenGeladen>((e, ctx) => store.Dispatch(e, ctx));
        bus.Subscribe<PostenAngelegt>((e, ctx) => store.Dispatch(e, ctx));
        bus.Subscribe<PostenUmbenannt>((e, ctx) => store.Dispatch(e, ctx));

        return (bus, store);
    }

    // 1 — Reaktive Schleife: Publish → Reducer → genau EIN Changed-Signal.
    [Fact]
    public void Publish_treibt_Reducer_und_feuert_genau_ein_Changed()
    {
        var (bus, store) = WireStore();
        var changed = 0;
        store.Changed += () => changed++;

        var id = Guid.NewGuid();
        bus.Publish(new PostenAngelegt(id, "Alpha"));

        Assert.Equal(1, store.Posten.Count);
        Assert.Equal("Alpha", store.Posten.Get(id)!.Text);
        Assert.Equal(id, store.Cursor.Id);
        Assert.Equal(1, changed);                        // EIN Signal, nicht pro Mutation
    }

    // 2 — Reentranter Publish: Folge-Event faltet sich in EINEN Root-Zyklus.
    [Fact]
    public void Reentranter_Publish_ergibt_ein_einziges_Changed()
    {
        var (bus, store) = WireStore();
        var changed = 0;
        store.Changed += () => changed++;

        var id = Guid.NewGuid();
        // Zusatz-Handler publiziert MITTEN im Dispatch ein Folge-Event.
        bus.Subscribe<PostenAngelegt>((e, ctx) =>
            bus.Publish(new PostenUmbenannt(e.Id, e.Text + "+")));

        bus.Publish(new PostenAngelegt(id, "Beta"));     // Root-Publish

        Assert.Equal("Beta+", store.Posten.Get(id)!.Text); // beide Effekte wirksam
        Assert.Equal(1, changed);                           // aber nur EIN Render-Signal
    }

    // 3 — Intent → Command: Handler reichert kontextfreien Intent aus dem Store an,
    //     der Command-Round-Trip patcht den Store über das Server-Event.
    [Fact]
    public void Intent_wird_zu_Command_und_ServerEvent_patcht_Store()
    {
        var (bus, store) = WireStore();
        var handler = new PostenIntentHandler(store);

        // Intent-Handler verdrahten (wie generierter Sync-Handler-Block).
        bus.Subscribe<UmbenennenAngefordert>((m, ctx) =>
            handler.Dispatch(m, ctx, produced => bus.Publish(produced, MessageContext.Local())));

        // "Server" fängt ausgehende Commands ab (Rolle des ConnectionModule).
        var gesendet = new List<PostenUmbenennen>();
        bus.Subscribe<PostenUmbenennen>((c, ctx) => gesendet.Add(c));

        var id = Guid.NewGuid();
        bus.Publish(new PostenAngelegt(id, "Alt"));       // Cursor zeigt jetzt auf id
        bus.Publish(new UmbenennenAngefordert("Neu"));    // kontextfreier Intent

        // Der Handler hat den Kontext (Cursor.Id) ergänzt und ein Command erzeugt.
        var cmd = Assert.Single(gesendet);
        Assert.Equal(id, cmd.AggregateId);
        Assert.Equal("Neu", cmd.Text);

        // Kein Optimistic Update: erst das Server-Event ändert den Store.
        Assert.Equal("Alt", store.Posten.Get(id)!.Text);
        bus.Publish(new PostenUmbenannt(id, "Neu"));      // "Server" antwortet
        Assert.Equal("Neu", store.Posten.Get(id)!.Text);
    }

    // 4 — Hydration-Gate: "alle hydriert" schaltet erst, wenn Start-Daten da sind;
    //     leere Menge zählt als hydriert; ResetHydration macht es rückgängig.
    [Fact]
    public void Hydration_Gate_schaltet_erst_nach_Start_Daten()
    {
        var (bus, store) = WireStore();
        IHydrationStore gate = store;

        var hydratedEvents = 0;
        gate.HydrationChanged += () => hydratedEvents++;

        Assert.False(gate.IsHydrated);                    // vor Start-Daten: Shell wartet

        bus.Publish(new PostenGeladen(new[] { new PostenZeile(Guid.NewGuid(), "X") }));

        Assert.True(gate.IsHydrated);                     // Start-Daten da → Ready möglich
        Assert.Equal(1, hydratedEvents);

        gate.ResetHydration();                            // Reconnect
        Assert.False(gate.IsHydrated);

        // Leere Ergebnismenge: "hydriert, aber leer" — NICHT "noch am Laden".
        bus.Publish(new PostenGeladen(Array.Empty<PostenZeile>()));
        Assert.True(gate.IsHydrated);
        Assert.Equal(0, store.Posten.Count);
    }

    // 4b — Der "alle Stores hydriert"-Prädikat-Kern des ClientStartupService.
    [Fact]
    public void Mehrere_HydrationStores_gaten_gemeinsam()
    {
        var (busA, a) = WireStore();
        var (busB, b) = WireStore();
        var stores = new IHydrationStore[] { a, b };

        bool AlleHydriert() => stores.All(s => s.IsHydrated);

        Assert.False(AlleHydriert());
        busA.Publish(new PostenGeladen(Array.Empty<PostenZeile>()));
        Assert.False(AlleHydriert());                     // erst EINER hydriert
        busB.Publish(new PostenGeladen(Array.Empty<PostenZeile>()));
        Assert.True(AlleHydriert());                      // jetzt ALLE → Shell mountet
    }

    // 5 — Versioning-Signal: Event trägt nur (Id, Version) im Context; der
    //     VersioningModule cached die höchste Version (monoton) und aus Query-Deps.
    [Fact]
    public void VersioningModule_spiegelt_das_Signal_Modell()
    {
        var bus = new ClientBus(null);
        var vm = new VersioningModule();
        vm.Activate(bus, new[] { typeof(PostenAngelegt), typeof(PostenUmbenannt) });

        var id = Guid.NewGuid();
        Assert.Null(vm.GetVersion(id));                   // unbekannt

        // Event als reines Versions-Signal — Payload egal, Context zählt.
        bus.Publish(new PostenAngelegt(id, "irrelevant"),
            new MessageContext { AggregateId = id, AggregateVersion = 1 });
        Assert.Equal(1, vm.GetVersion(id));

        bus.Publish(new PostenUmbenannt(id, "x"),
            new MessageContext { AggregateId = id, AggregateVersion = 2 });
        Assert.Equal(2, vm.GetVersion(id));

        // Out-of-order / niedrigere Version darf NICHT zurücksetzen (monoton).
        bus.Publish(new PostenUmbenannt(id, "x"),
            new MessageContext { AggregateId = id, AggregateVersion = 1 });
        Assert.Equal(2, vm.GetVersion(id));

        // Zweite Quelle: Versionen aus Query-Response-Deps.
        var id2 = Guid.NewGuid();
        vm.TrackFromDeps(new[] { new AggregateDep(id2, "Posten", 5) });
        Assert.Equal(5, vm.GetVersion(id2));
    }

    // 6 — Selektives Rendern: EffectScope feuert nur, wenn die Facetten-Version
    //     (hier: Cursor) steigt — nicht bei jedem unbeteiligten Store-Change.
    [Fact]
    public void EffectScope_rendert_nur_bei_Facetten_Bump()
    {
        var (bus, store) = WireStore();
        using var fx = new EffectScope(bus);

        var renders = 0;
        fx.OnChanged(store, store.Cursor, () => renders++);   // Facette = Cursor (IVersioned)

        var a = Guid.NewGuid();
        bus.Publish(new PostenAngelegt(a, "A"));   // erster Posten: Cursor.Select → Bump → render
        Assert.Equal(1, renders);

        bus.Publish(new PostenUmbenannt(a, "A2")); // nur Map-Update, Cursor unverändert → KEIN render
        Assert.Equal(1, renders);

        var b = Guid.NewGuid();
        bus.Publish(new PostenAngelegt(b, "B"));   // Cursor steht schon → KEIN Select → KEIN render
        Assert.Equal(1, renders);
    }

    // 7 — Wiring-Vertrag (negativ): ohne AttachToBus fließen zwar Events in den
    //     Reducer, aber es gibt kein Changed-Signal — genau deshalb erzeugt der
    //     WiringGenerator AttachToBus für jeden Store.
    [Fact]
    public void Ohne_AttachToBus_kein_Changed_Signal()
    {
        var bus = new ClientBus(null);
        var store = new PostenStore();
        // ABSICHTLICH kein store.AttachToBus(bus)
        bus.Subscribe<PostenAngelegt>((e, ctx) => store.Dispatch(e, ctx));

        var changed = 0;
        store.Changed += () => changed++;

        bus.Publish(new PostenAngelegt(Guid.NewGuid(), "A"));

        Assert.Equal(1, store.Posten.Count);   // Reducer lief
        Assert.Equal(0, changed);              // aber kein Flush → kein Render-Signal
    }
}
