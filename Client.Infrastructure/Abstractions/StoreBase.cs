using System.ComponentModel;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Basisklasse für alle Client-Stores.
///
/// Löst das Inkonsistenz-Problem bei mehreren Stores auf demselben Bus:
/// Während eines Dispatch-Zyklus werden PropertyChanged-Notifications
/// und Collection-Deltas gesammelt statt sofort gefeuert.
/// Nach DispatchCompleted werden alle Notifications atomar geflusht —
/// die View sieht ausschließlich den konsistenten Endzustand.
///
/// AttachToBus / DetachFromBus sind public, weil der WiringGenerator
/// in einem anderen Assembly (Domain.Client) diese Methoden aufruft.
/// Kein Reflection — der Generator erzeugt explizite Aufrufe.
///
/// Verwendung durch Store-Entwickler:
///
///   public partial class MyStore : StoreBase
///   {
///       private readonly TrackedMap&lt;Guid, MyRecord&gt; _items;
///       [ObservableProperty] private int _count;
///
///       public MyStore()
///       {
///           _items = CreateMap&lt;Guid, MyRecord&gt;(r => r.Id);
///       }
///
///       void Handle(MyEvent evt, MessageContext ctx)
///       {
///           _items.Put(new MyRecord(evt.Id, evt.Name));
///           Count = _items.Count;
///           // Kein PropertyChanged, kein Delta — alles wird nach Dispatch geflusht
///       }
///   }
/// </summary>
public abstract class StoreBase : ObservableObject
{
    private readonly HashSet<string> _pendingProperties = new();
    private readonly List<ITrackedCollection> _trackedCollections = new();
    private ClientBus? _bus;

    // ═══════════════════════════════════════════════════
    // BUS-ANBINDUNG
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Registriert diesen Store am Bus.
    ///
    /// Public weil der WiringGenerator (in Domain.Client) diesen Aufruf
    /// für jeden Store im Baum generiert — cross-assembly, kein Reflection.
    ///
    /// Wird einmal pro Circuit/Window aufgerufen, nach SubscribeAll.
    /// Doppelter Aufruf ist sicher — der alte Handler wird vorher deregistriert.
    /// </summary>
    public void AttachToBus(ClientBus bus)
    {
        if (_bus != null)
            _bus.DispatchCompleted -= Flush;

        _bus = bus;
        _bus.DispatchCompleted += Flush;
    }

    /// <summary>
    /// Trennt den Store vom Bus.
    ///
    /// Public aus demselben Grund wie AttachToBus — der Generator erzeugt
    /// UnsubscribeAll, das diese Methode auf allen Stores aufruft.
    ///
    /// Wird aufgerufen wenn der Circuit/Window schließt.
    /// Nach diesem Aufruf feuert kein Flush mehr.
    /// </summary>
    public void DetachFromBus()
    {
        if (_bus == null) return;
        _bus.DispatchCompleted -= Flush;
        _bus = null;
    }

    // ═══════════════════════════════════════════════════
    // PROPERTY-BATCHING
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Override von ObservableObject.
    ///
    /// Während eines Dispatch: Property-Name sammeln, nicht feuern.
    /// Außerhalb eines Dispatch (kein Bus attached, oder kein aktiver Dispatch):
    /// sofortiges Feuern — Normalfall für direkte Zuweisungen außerhalb von Handle.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_bus?.IsDispatching == true)
        {
            if (e.PropertyName != null)
                _pendingProperties.Add(e.PropertyName);
        }
        else
        {
            base.OnPropertyChanged(e);
        }
    }

    // ═══════════════════════════════════════════════════
    // COLLECTION-FACTORY
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Erzeugt eine TrackedMap und registriert sie für den Flush-Zyklus.
    ///
    /// Muss im Konstruktor aufgerufen werden — nach dem Konstruktor werden
    /// keine weiteren Collections mehr registriert.
    ///
    /// Kein Reflection — der Store-Entwickler ruft diese Methode explizit auf.
    /// </summary>
    protected TrackedMap<TKey, TValue> CreateMap<TKey, TValue>(Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        var map = new TrackedMap<TKey, TValue>(keySelector);
        _trackedCollections.Add(map);
        return map;
    }

    // ═══════════════════════════════════════════════════
    // DELTAS — für die View-Schicht
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Feuert nach jedem Flush mit allen Collection-Deltas dieses Stores.
    ///
    /// Rückgabetyp ist CollectionDelta — kein object-Boxing, kein Reflection.
    /// Die View reagiert per Pattern-Matching auf Added, Replaced, Removed, Reset.
    ///
    /// Feuert nur wenn mindestens ein Delta vorhanden ist.
    /// </summary>
    public event Action<IReadOnlyList<CollectionDelta>>? Deltas;

    // ═══════════════════════════════════════════════════
    // FLUSH — privat, nur via DispatchCompleted erreichbar
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Feuert alle gesammelten PropertyChanged-Events und Collection-Deltas atomar.
    /// Registriert auf ClientBus.DispatchCompleted — wird nie direkt aufgerufen.
    /// </summary>
    private void Flush()
    {
        // 1. Gesammelte PropertyChanged-Events feuern
        foreach (var propertyName in _pendingProperties)
            base.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        _pendingProperties.Clear();

        // 2. Collection-Deltas aller registrierten Maps einsammeln
        List<CollectionDelta>? allDeltas = null;

        foreach (var collection in _trackedCollections)
        {
            var deltas = collection.FlushDeltas();
            if (deltas.Count == 0) continue;

            allDeltas ??= new List<CollectionDelta>();
            allDeltas.AddRange(deltas);
        }

        // 3. Deltas als Batch feuern — nur wenn vorhanden
        if (allDeltas != null)
            Deltas?.Invoke(allDeltas);
    }
}
