// ── Client.Infrastructure/Abstractions/ConnectedComponent.cs ──

using Microsoft.AspNetCore.Components;

namespace Client.Infrastructure.Abstractions;

/// <summary>
/// Basisklasse für alle Sections (Smart Components).
///
/// Stellt drei Primitiven bereit:
///   Fx        → EffectScope (DI-injected, Transient)
///   Dispatch  → Event auf den Bus publizieren
///   Render    → StateHasChanged via InvokeAsync
///
/// Lifecycle:
///   OnInitialized → Connect() → Sections registrieren Subscriptions/Effects
///   Dispose       → Fx.Dispose() → alle Subscriptions aufgeräumt
///
/// Sections überschreiben Connect() und deklarieren:
///   Fx.OnChanged(store, Render)   — welche Stores ein Re-Render auslösen
///   Fx.On&lt;T&gt;(handler)             — welche Events Seiteneffekte auslösen
/// </summary>
public abstract class ConnectedComponent : ComponentBase, IDisposable
{
    [Inject] protected EffectScope Fx { get; set; } = null!;

    /// <summary>
    /// Override: Store-Subscriptions und Effects registrieren.
    /// </summary>
    protected virtual void Connect() { }

    protected override void OnInitialized() => Connect();

    /// <summary>Intent/Event auf den Bus dispatchen.</summary>
    protected void Dispatch(object message) => Fx.Dispatch(message);

    /// <summary>Re-Render auslösen (thread-safe).</summary>
    protected void Render() => InvokeAsync(StateHasChanged);

    public virtual void Dispose() => Fx.Dispose();
}
