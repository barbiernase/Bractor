using FluentAssertions;
using Infrastructure.Aggregate.ActorSystem;
using Xunit;
using E = Infrastructure.Aggregate.ActorSystem.CommandVorpruefung.Ergebnis;

namespace Infrastructure.Pruefstand.Schreibseite;

/// <summary>
/// Zustellgarantie (docs/08 §8.4; früher T2b, docs/13 §13.6) — die tragende Reihenfolge „Inbox-Dedup VOR OCC" (reine Entscheidung, Ebene-1).
/// Der Kern-Beweis: ein bereits verarbeiteter Command an STALE Version → idempotenter Erfolg,
/// NICHT ein Concurrency-Konflikt. Ein frischer Command an stale Version → Konflikt.
/// </summary>
public class CommandVorpruefungTests
{
    [Fact]
    public void Frischer_Command_passende_Version_faehrt_fort()
        => CommandVorpruefung.Prüfe(istIdempotent: true, bereitsVerarbeitet: false, bereitsAbgelehnt: false,
                                    stateVersion: 5, expectedVersion: 5)
            .Should().Be(E.Fortfahren);

    [Fact]
    public void Frischer_Command_stale_Version_ist_Konflikt()
        => CommandVorpruefung.Prüfe(istIdempotent: true, bereitsVerarbeitet: false, bereitsAbgelehnt: false,
                                    stateVersion: 6, expectedVersion: 5)
            .Should().Be(E.VersionsKonflikt);

    [Fact]
    public void Bereits_verarbeitet_an_stale_Version_ist_idempotenter_Erfolg_NICHT_Konflikt()
    {
        // Der Kern: der Client-Retry seines bereits erfolgreichen Commands (Version rückte auf 6)
        // darf NICHT als Konflikt lesen, sondern als Erfolg — weil die Inbox-Prüfung VOR OCC greift.
        CommandVorpruefung.Prüfe(istIdempotent: true, bereitsVerarbeitet: true, bereitsAbgelehnt: false,
                                 stateVersion: 6, expectedVersion: 5)
            .Should().Be(E.IdempotenterErfolg);
    }
}
