namespace Abstractions;

/// <summary>
/// Marker eines DOMÄNEN-DIENSTES (docs/konzept-werkbank.md §12.3 E11): eine Schnittstelle, hinter der Technik steckt, die eine
/// Funktion benutzt (Bild skalieren, Histogramm ausgleichen, ein Modell laden …). Jede öffentliche Klasse, die sie implementiert,
/// ist eine Implementierung (Code-Fakt); welche läuft, wählt die Umgebung (<c>Dienste:&lt;IDienst&gt;:Implementierung</c>, Default: die
/// einzige). Gebunden wird generiert (<c>GeneratedDienste</c>), ohne Reflection.
///
/// Ein Dienst steht nur im Konstruktor einer Funktions- oder Dienst-Implementierung (CQRS089) — nie im Fachcode eines Aggregats oder einer
/// Projektion (dort sind es Fähigkeiten als Parameter). Was ein Konstruktor sonst nehmen darf: Konfig-Records (Werte aus
/// <c>Konfig:&lt;Record&gt;</c> der Umgebung) und <c>ILogger&lt;T&gt;</c> (CQRS090).
///
/// Ein Dienst ist eine <b>Ressource der Aktivierung</b> (Plan Pub/Sub-Neustrukturierung §2.9): je Aktivierung eines Funktions-Actors wird er in
/// einem eigenen Bereich neu gebaut und beim Stoppen, Passivieren oder Umziehen wieder freigegeben (<c>IAsyncDisposable</c>/<c>IDisposable</c>) —
/// eine Datenbank-Verbindung, ein geladenes Modell, ein Cache. Was über die Aktivierung hinaus gelten soll, ist Zustand (<c>IZustand&lt;T&gt;</c>).
/// </summary>
public interface IDienst { }
