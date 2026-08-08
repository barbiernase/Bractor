using Abstractions;
using Domain.Flug;
using Domain.Hotel;
using Domain.Reise;

namespace Domain.Reiseauftrag;

/// <summary>
/// Die Reisebuchungs-Saga als typisierte Event→Command-Regeln (Spec §3). Ein echter DIAMANT: aus EINEM
/// Auslöser laufen ZWEI unabhängige Zweige (Flug reservieren, Hotel reservieren), die sich an der Reise
/// wieder VEREINEN (Join: bestätigen erst, wenn BEIDE reserviert sind).
///
/// Das ist ALLES, was der Entwickler für die Orchestrierung schreibt — kein Manager, kein Treiber, keine
/// Zustandsmaschine, kein Wecken, keine Korrelation von Hand. Scheitert ein Zweig (z.B. Hotel ausgebucht),
/// gleicht das Framework die bereits erfolgreichen Zweige über ihr <c>RückgängigDurch</c> aus (reverse
/// Kausalität) und beendet den Prozess als Fehlgeschlagen — die Reise wird nie bestätigt.
/// </summary>
public sealed class ReiseProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<ReiseGebucht>.Definiere(p =>
    {
        // Zweig A — Flug reservieren; der Gegenzug gibt die Plätze wieder frei.
        p.Auf<ReiseGebucht>()
            .Sende<ReserviereFlug>(e => new ReserviereFlug(e.Flug, e.Plaetze))
            .RückgängigDurch<GebeFlugFrei>(e => new GebeFlugFrei(e.Flug, e.Plaetze));

        // Zweig B — Hotel reservieren; der Gegenzug gibt die Zimmer wieder frei.
        p.Auf<ReiseGebucht>()
            .Sende<ReserviereHotel>(e => new ReserviereHotel(e.Hotel, e.Zimmer))
            .RückgängigDurch<GebeHotelFrei>(e => new GebeHotelFrei(e.Hotel, e.Zimmer));

        // Vereinigung (Diamant-Join) — bestätigen erst, wenn Flug reserviert UND Hotel reserviert ist.
        p.Auf<ReiseGebucht>().Und<FlugReserviert>().Und<HotelReserviert>()
            .Sende<BestaetigeReise>((e, f, h) => new BestaetigeReise(e.Reise, e.Kunde));
    });
}
