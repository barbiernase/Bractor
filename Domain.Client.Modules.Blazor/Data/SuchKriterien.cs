using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
namespace Domain.Client.Modules;

/// <summary>
/// Immutable die EINE Quelle der Wahrheit für die vom Nutzer steuerbaren
/// Felder des <c>ImagePairFilter</c>. Der Store hält genau eine Instanz;
/// <c>Store.BaueFilter</c> liest ausschließlich hieraus. Damit gibt es keine
/// konkurrierenden Filter-Zustände mehr (früher: FilterWert-String +
/// NurNichtInspizierte + Tag-Modus verstreut).
///
/// Alle Felder sind optional/leer im Default → <see cref="Leer"/> heißt
/// "keine Einschränkung" (entspricht dem alten FilterWert == "alle").
/// </summary>
public record SuchKriterien(
    DateTimeOffset? Von = null,
    DateTimeOffset? Bis = null,
    Klassifikation? ProduktLabel = null,
    bool? HatMenschLabel = null,
    bool NurNichtInspizierte = false)
{
    /// <summary>Keine Einschränkung — alles wird gezeigt.</summary>
    public static readonly SuchKriterien Leer = new();
}

/// <summary>
/// Vom Suchpanel dispatcht, wenn der Nutzer neue Kriterien anwendet.
/// Rein client-lokal (IClientEvent) — geht nie an den Server.
///
/// Verarbeitung (exakt das bestehende Reset-Reload-Muster):
///   1. Store.Handle(SucheGeaendert): Kriterien setzen + VirtualImagePairs.Reset()
///   2. PaarlisteRefreshHandler.Handle(SucheGeaendert): Cursor→0 + ersten Chunk laden
/// </summary>
public record SucheGeaendert(SuchKriterien Kriterien) : IClientEvent;
