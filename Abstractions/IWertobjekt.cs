namespace Abstractions;

/// <summary>
/// Marker für ein DDD-VALUE-OBJECT: ein unveränderlicher, per Wert vergleichbarer
/// Domänenwert (kein Aggregat, keine Entität, keine Nachricht). Leerer Marker — analog zu
/// <see cref="IState"/> / <c>IReadModel</c>.
///
/// Zweck ist die reflexionsfreie Auffindbarkeit zur Compile-Zeit: ein späterer Analyzer/
/// Generator enumeriert alle <c>: IWertobjekt</c> und kann daraus die Kapsel-Grenze
/// erzwingen (Konstruktion/<c>with</c> nur innerhalb des Typs selbst) oder die
/// Wertobjekt-Landkarte darstellen — ohne dass der Fachcode etwas davon weiß.
/// </summary>
public interface IWertobjekt { }
