using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Kuratieren;

namespace Domain.Client.Modules.Kuratieren;

/// <summary>
/// Header-Leiste „Sammle in: …" (Konzept datensatz-kuratierung §4.1) — der persistente,
/// erststellige Aufnahme-Modus (wie der Aufnahme-Knopf einer Kamera). Zeigt das aktive
/// Sammel-Ziel mit Größe + Live-Balance, erlaubt Wechsel (Dropdown), Neuanlage und Einfrieren.
///
/// Auto-entdeckt als <see cref="IHeaderModule"/>; erscheint unter der Status-Leiste (Order 1).
/// </summary>
public class SammelZielModule : IHeaderModule
{
    public string Id    => "sammel-ziel";
    public string Title => "Sammel-Ziel";
    public Type   ComponentType => typeof(SammelZielBar);
    public int    Order => 1;   // unter der StatusBar (0)
}
