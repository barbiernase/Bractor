using System;
using System.Linq;
using Infrastructure.Mapping;
using Infrastructure.Komposition;

namespace Infrastructure.Serialization;

/// <summary>
/// Start-Abbruch-Prüfung für den Wire-Serializer (Roadmap K1: „jeder interne Typ hat einen
/// Serializer → sonst Start-Abbruch"). Single-Node lässt Serialisierungslücken UNSICHTBAR — ohne
/// diesen Check würde eine fehlende <c>[JsonSerializable]</c>-Zeile erst cross-node zur Laufzeit
/// auffliegen (still verschluckter faulted RequestAsync-Task). Deshalb hart beim Boot.
///
/// Zwei Achsen:
/// (a) jede Top-Level-Hülle (<c>IWireMessage</c>) ist vom Serializer wählbar;
/// (b) jeder polymorphe Payload-Typ (alle <c>ICommand</c>/<c>IEvent</c> aus
///     <see cref="GeneratedTypeRegistry"/>) hat eine <c>JsonTypeInfo</c> im <see cref="CqrsWireJsonContext"/>.
///
/// Der Compile-Zeit-Drift-Guard (WireSerializerGenerator referenziert <c>Ctx.Default.{Typ}</c>) fängt
/// das meiste schon beim Build; dieser Runtime-Check ist der Backstop gegen Registry↔Context-Drift.
/// </summary>
public static class WireSerializerBootCheck
{
    public static void Verify()
    {
        // (a) Top-Level-Hüllen serialisierbar
        foreach (var t in Anwendung.Wire.WireMessageTypes)
            if (!Anwendung.Wire.CanSerialize(t))
                throw new InvalidOperationException(
                    $"[Wire] Top-Level-Nachricht {t.FullName} ist nicht serialisierbar — Wire-Serializer unvollständig.");

        // (b) STJ-Payloads (Commands/Events) haben eine JsonTypeInfo im Context.
        var stjPayloads = Anwendung.Typen.Commands.Values
            .Concat(Anwendung.Typen.Events.Values);
        foreach (var t in stjPayloads)
            if (Anwendung.WireJson.GetTypeInfo(t) is null)
                throw new InvalidOperationException(
                    $"[Wire] Kein Wire-JsonTypeInfo für Payload-Typ {t.FullName}. " +
                    $"Fehlt eine [JsonSerializable]-Zeile in CqrsWireJsonContext?");

        // (c) Signale sind BEWUSST nicht STJ-backed (uniform (StreamId,Version), self-serialized über
        //     GeneratedWirePoly.WriteSignal/ReadSignal). Backstop hier: jeder Registry-Signaltyp ist im
        //     generierten Signal-Dispatch enthalten (sonst Registry↔Generator-Drift).
        var signalDispatch = Anwendung.WirePoly.SignalTypes.ToHashSet();
        foreach (var t in Anwendung.Typen.Signals.Values)
            if (!signalDispatch.Contains(t))
                throw new InvalidOperationException(
                    $"[Wire] Signal {t.FullName} fehlt im generierten Signal-Dispatch (GeneratedWirePoly). " +
                    $"Registry↔WireSerializerGenerator-Drift?");
    }
}
