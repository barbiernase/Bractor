using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Scheibe A des Treiber-Folds (EM-1): der neue durable Ablehnungs-Marker <see cref="KommandoAbgelehnt"/>.
///
/// Store-freier CONTRACT-Guard (die behaviorale Fold-/Inbox-/Rehydrations-Kette wird gegen echtes Marten in
/// der Integration bewiesen — Entscheidung „Integration + store-freie Unit-Tests"). Der Marker trägt genau
/// die Typ-Eigenschaften, an denen die Korrektheit hängt:
///
/// - <see cref="IEvent"/> + <see cref="IProzessIntern"/>: der Prozess-Manager-Fold (<c>FaltMarkingAsync</c>)
///   unterscheidet „Wirkung" (ein Domänen-Event, kein <see cref="IProzessIntern"/> → kompensierbar) von der
///   ABLEHNUNG (ist <see cref="IProzessIntern"/> → NICHT WirkungDa, aber eigene Achse AbgelehntDa). Genau
///   dieser Diskriminator (<c>Payload is KommandoAbgelehnt</c> vs. <c>Payload is not IProzessIntern</c>)
///   entscheidet, ob der Fold den Marker fälschlich als kompensierbare Wirkung oder korrekt als Fehlschlag
///   liest. Fiele <see cref="IProzessIntern"/> weg, würde der Applier ihn beim Domänen-Falten anwenden
///   (Aggregat kaputt) UND der DtoMapper verlangte einen Proto-DTO.
/// - trägt <c>CommandId</c> (== Vorgang, Fold-Match über CausationId) + <c>Grund</c> (fließt in SchrittGescheitert).
/// </summary>
public class AblehnungsMarkeTests
{
    [Fact]
    public void KommandoAbgelehnt_ist_IEvent_und_IProzessIntern()
    {
        var marke = new KommandoAbgelehnt(Guid.NewGuid(), "DeckungReichtNicht");

        marke.Should().BeAssignableTo<IEvent>("der Marker wird ins Log co-committet");
        marke.Should().BeAssignableTo<IProzessIntern>(
            "der Domänen-Fold überspringt ihn (nur Version zählen), der DtoMapper schließt ihn aus, " +
            "und der Prozess-Fold liest ihn als Fehlschlag (NICHT als kompensierbare Wirkung)");
    }

    [Fact]
    public void KommandoAbgelehnt_traegt_CommandId_und_Grund()
    {
        var id = Guid.NewGuid();
        var marke = new KommandoAbgelehnt(id, "DeckungReichtNicht");

        marke.CommandId.Should().Be(id, "der Fold matcht über CausationId == Vorgang == CommandId");
        marke.Grund.Should().Be("DeckungReichtNicht", "der Grund fließt in das durable SchrittGescheitert");
    }

    [Fact]
    public void KommandoAbgelehnt_ist_KEIN_KommandoVerarbeitet()
    {
        // Die zwei Inbox-Mengen dürfen nie verwechselt werden: eine Ablehnung ist kein Erfolg. Der Fold und die
        // Rehydration trennen strikt über den Laufzeit-Typ (verarbeitet → Success:true, abgelehnt → Success:false).
        IEvent abgelehnt = new KommandoAbgelehnt(Guid.NewGuid(), "x");
        IEvent verarbeitet = new KommandoVerarbeitet(Guid.NewGuid());

        abgelehnt.Should().NotBeOfType<KommandoVerarbeitet>();
        verarbeitet.Should().NotBeOfType<KommandoAbgelehnt>();
    }
}
