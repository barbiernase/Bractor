namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Zentraler Nachrichtenbus für das Client-Framework.
/// 
/// Hybrid-Modell (E2):
///   Subscribe      → sync, depth-first, UI-Thread (für Stores + leichte Handler)
///   SubscribeAsync → fire-and-forget (für ConnectionModule, QueryBridge, schwere Handler)
/// 
/// Dispatch-Reihenfolge bei Publish:
///   1. Alle sync-Subscriber werden sofort aufgerufen (depth-first bei reentrantem Publish)
///   2. Alle async-Subscriber werden als fire-and-forget gestartet
///   3. Publish kehrt zurück — sync-State ist konsistent, async-I/O läuft im Hintergrund
/// </summary>
public interface IBus
{
    // ─── Generische API (Compile-Zeit-Typ bekannt) ───

    /// <summary>
    /// Publiziert eine Nachricht mit explizitem MessageContext.
    /// Sync-Subscriber werden sofort aufgerufen, async-Subscriber als fire-and-forget.
    /// </summary>
    void Publish<T>(T message, MessageContext context) where T : notnull;

    /// <summary>
    /// Publiziert eine Nachricht mit automatischem Local-Context.
    /// </summary>
    void Publish<T>(T message) where T : notnull;

    /// <summary>
    /// Registriert einen synchronen Subscriber.
    /// Wird auf dem aufrufenden Thread ausgeführt (UI-Thread bei PostToSyncContext).
    /// Verwendet für: Stores, sync Handler, VersioningModule.
    /// </summary>
    IDisposable Subscribe<T>(Action<T, MessageContext> handler) where T : notnull;

    /// <summary>
    /// Registriert einen asynchronen Subscriber.
    /// Wird als fire-and-forget gestartet — blockiert den Publish-Aufruf nicht.
    /// Fehler werden als BusError auf dem Bus publiziert.
    /// Verwendet für: ConnectionModule, QueryBridge, async Handler.
    /// </summary>
    IDisposable SubscribeAsync<T>(Func<T, MessageContext, Task> handler) where T : notnull;

    // ─── Nicht-generische API (Runtime-Typ, kein Reflection) ───

    /// <summary>
    /// Publiziert eine Nachricht deren Typ erst zur Laufzeit bekannt ist.
    /// Routet anhand von message.GetType() — kein Reflection, kein dynamic.
    /// 
    /// Verwendet von: ConnectionModule (Server-Events), QueryBridge (Responses).
    /// </summary>
    void Publish(object message, MessageContext context);

    /// <summary>
    /// Registriert einen synchronen Subscriber für einen Runtime-Typ.
    /// Der Handler bekommt object und castet selbst.
    /// 
    /// Verwendet von: VersioningModule (subscribt auf N Server-Event-Typen).
    /// </summary>
    IDisposable Subscribe(Type messageType, Action<object, MessageContext> handler);

    /// <summary>
    /// Registriert einen asynchronen Subscriber für einen Runtime-Typ.
    /// Der Handler bekommt object und castet selbst.
    /// 
    /// Verwendet von: ConnectionModule (Commands), QueryBridge (Queries).
    /// </summary>
    IDisposable SubscribeAsync(Type messageType, Func<object, MessageContext, Task> handler);
}