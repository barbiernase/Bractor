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
/// </summary>
public interface IDienst { }
