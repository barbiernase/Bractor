using FluentAssertions;
using Infrastructure.Aggregate.ActorSystem;
using Xunit;
using E = Infrastructure.Aggregate.ActorSystem.CommandVorpruefung.Ergebnis;

namespace Infrastructure.Pruefstand.Dispatcher;

/// <summary>
/// T2b — die tragende Reihenfolge „Inbox-Dedup VOR OCC" (reine Entscheidung, Ebene-1).
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
        // Der T2b-Kern: der Client-Retry seines bereits erfolgreichen Commands (Version rückte auf 6)
        // darf NICHT als Konflikt lesen, sondern als Erfolg — weil die Inbox-Prüfung VOR OCC greift.
        CommandVorpruefung.Prüfe(istIdempotent: true, bereitsVerarbeitet: true, bereitsAbgelehnt: false,
                                 stateVersion: 6, expectedVersion: 5)
            .Should().Be(E.IdempotenterErfolg);
    }

    [Fact]
    public void Bereits_abgelehnt_liefert_konsistente_Ablehnung()
        => CommandVorpruefung.Prüfe(istIdempotent: true, bereitsVerarbeitet: false, bereitsAbgelehnt: true,
                                    stateVersion: 6, expectedVersion: 5)
            .Should().Be(E.IdempotenteAblehnung);

    [Fact]
    public void Nicht_idempotenter_Pfad_ignoriert_die_Inbox()
    {
        // Ohne Idempotenz (theoretischer Emittiert-Sonderfall) zählt nur die Version.
        CommandVorpruefung.Prüfe(istIdempotent: false, bereitsVerarbeitet: true, bereitsAbgelehnt: true,
                                 stateVersion: 5, expectedVersion: 5)
            .Should().Be(E.Fortfahren);
        CommandVorpruefung.Prüfe(istIdempotent: false, bereitsVerarbeitet: true, bereitsAbgelehnt: false,
                                 stateVersion: 6, expectedVersion: 5)
            .Should().Be(E.VersionsKonflikt);
    }
}
