using Abstractions;

namespace Core.Katalog;

/// <summary>
/// Die Konfig eines Webhooks (die Werte seiner Quelle): der Pfad, unter dem der Rechnerdienst annimmt (<c>POST /eingang/webhook/&lt;Pfad&gt;</c> auf dem
/// Port der App-Instanz, je Umgebung eindeutig), und wahlweise der Schlüssel, den ein Absender im Kopf <c>schluessel</c> mitgeben muss (ein Geheimnis — in der
/// Umgebung als <c>${env:NAME}</c>).
/// </summary>
public sealed record Webhookkonfig(string Pfad, string? Schluessel = null);

/// <summary>
/// Der WEBHOOK (Katalog des Frameworks, docs/04 §4.7): eine ANNEHMENDE Quellart (<see cref="IAnnahme{TMeldung}"/>) — er lauscht nicht; der
/// Rechnerdienst seiner App-Instanz nimmt <c>POST /eingang/webhook/&lt;Pfad&gt;</c> auf ihrem Port an, prüft den Schlüssel, macht daraus einen <see cref="Aufruf"/> (Kennung aus dem Kopf <c>kennung</c>),
/// übersetzt ihn mit der Übersetzung der Akteur-Seite und legt die Nachricht durabel ab — erst dann antwortet er 202. Wiederholt der Absender mit
/// derselben Kennung, entstehen dieselben Ids: der Aufruf wirkt einmal. Er hält nichts fest; die Klasse selbst rechnet nichts.
/// </summary>
public sealed class Webhook(Webhookkonfig konfig) : IAnnahme<Aufruf>
{
    /// <summary>Die Werte der Quelle (gelesen vom Rechnerdienst über <see cref="Annahmevertrag"/>).</summary>
    public Webhookkonfig Konfig { get; } = konfig;
}
