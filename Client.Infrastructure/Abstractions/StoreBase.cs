// ── Client.Infrastructure/Abstractions/StoreBase.cs ──

using System.ComponentModel;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Basisklasse für alle Client-Stores.
///
/// Redux-Analogie:
///   Store.Handle()  = Reducer (pure State-Mutation)
///   Store.Changed   = store.subscribe() (View-Notification)
///
/// Notification-Modell:
///   EIN Signal: Changed. Feuert nach jedem Dispatch-Zyklus
///   in dem sich irgendwas geändert hat — Properties ODER Collections.
///
///   Kein Deltas-Event, kein granulares PropertyChanged für Views.
///   Blazor macht ohnehin einen vollständigen Component-Diff —
///   feingranulare Notifications sind tote Komplexität.
///
/// Batching:
///   Während eines Dispatch-Zyklus (IsDispatching == true):
///     - [ObservableProperty]-Setter → _hasChanges = true (gesammelt)
///     - TrackedMap-Mutationen       → _hasChanges = true (gesammelt)
///   Nach DispatchCompleted → Flush:
///     - Changed feuert EINMAL wenn sich irgendwas geändert hat
///
/// AttachToBus / DetachFromBus sind public, weil der WiringGenerator
/// in einem anderen Assembly diese Methoden aufruft.
/// </summary>
public abstract class StoreBase : ObservableObject
{
    private readonly List<ITrackedCollection> _trackedCollections = new();
    private ClientBus? _bus;
    private bool _hasChanges;

    // ═══════════════════════════════════════════════════
    // HYDRATION — Start-Ladezustand (für IHydrationStore)
    // ═══════════════════════════════════════════════════

    private bool _isHydrated;

    /// <summary>
    /// True sobald die Start-Daten dieses Stores eingetroffen sind.
    /// Implementiert IHydrationStore.IsHydrated für Stores die das Interface
    /// deklarieren; für andere Stores schlicht ungenutzt.
    /// </summary>
    public bool IsHydrated => _isHydrated;

    /// <summary>Feuert, wenn IsHydrated von false auf true wechselt.</summary>
    public event Action? HydrationChanged;

    /// <summary>
    /// Markiert den Store als hydriert — vom Store aufgerufen, sobald die
    /// Start-Antwort eingetroffen ist (unabhängig von der Datenmenge).
    /// Idempotent: mehrfacher Aufruf feuert HydrationChanged nur beim Übergang.
    /// </summary>
    protected void MarkHydrated()
    {
        if (_isHydrated) return;
        _isHydrated = true;
        HydrationChanged?.Invoke();
    }

    /// <summary>Setzt Hydration zurück (z.B. bei Reconnect), damit erneut gewartet wird.</summary>
    public void ResetHydration() => _isHydrated = false;

    // ═══════════════════════════════════════════════════
    // NOTIFICATION — Ein Signal für die View
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Feuert nach jedem Dispatch-Zyklus in dem sich IRGENDWAS
    /// geändert hat — Properties ODER Collections.
    ///
    /// Die View subscribed hierauf via EffectScope:
    ///   fx.OnChanged(store, () => InvokeAsync(StateHasChanged));
    ///
    /// Feuert maximal einmal pro Dispatch-Zyklus (batched).
    /// </summary>
    public event Action? Changed;

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
    /// </summary>
    public void DetachFromBus()
    {
        if (_bus == null) return;
        _bus.DispatchCompleted -= Flush;
        _bus = null;
    }

    // ═══════════════════════════════════════════════════
    // PROPERTY-CHANGE INTERCEPTION
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Override von ObservableObject.
    ///
    /// Während eines Dispatch: Change-Flag setzen, nicht feuern.
    /// Außerhalb eines Dispatch: Changed sofort feuern.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_bus?.IsDispatching == true)
        {
            _hasChanges = true;
        }
        else
        {
            Changed?.Invoke();
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
    /// </summary>
    protected TrackedMap<TKey, TValue> CreateMap<TKey, TValue>(Func<TValue, TKey> keySelector)
        where TKey : notnull
    {
        var map = new TrackedMap<TKey, TValue>(keySelector);
        _trackedCollections.Add(map);
        return map;
    }

    /// <summary>
    /// Registriert eine bereits erstellte ITrackedCollection für den Flush-Zyklus
    /// und gibt sie zurück. Für Collections die nicht über CreateMap entstehen
    /// (z.B. PagedCollection, Cursor), typischerweise im Store-Konstruktor:
    ///
    ///   public MyStore()
    ///   {
    ///       Items  = Track(new PagedCollection&lt;...&gt;(r => r.Id));
    ///       Cursor = Track(new Cursor&lt;Guid&gt;());
    ///   }
    ///
    /// Muss im Konstruktor aufgerufen werden — nach dem Konstruktor werden
    /// keine weiteren Collections mehr registriert.
    /// </summary>
    protected T Track<T>(T collection) where T : ITrackedCollection
    {
        _trackedCollections.Add(collection);
        return collection;
    }

    // ═══════════════════════════════════════════════════
    // FLUSH — privat, nur via DispatchCompleted erreichbar
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Prüft ob sich in diesem Dispatch-Zyklus etwas geändert hat
    /// (Properties oder Collections) und feuert Changed genau einmal.
    /// </summary>
    private void Flush()
    {
        // Collection-Changes einsammeln
        foreach (var collection in _trackedCollections)
        {
            if (collection.ConsumeHasChanges())
                _hasChanges = true;
        }

        // EIN Signal wenn sich irgendwas geändert hat
        if (_hasChanges)
        {
            _hasChanges = false;
            Changed?.Invoke();
        }
    }
}
