using System;
using System.Collections.Generic;
using System.Linq;
using Infrastructure.Mapping;   // GeneratedTypeRegistry

namespace Infrastructure.Serialization;

/// <summary>
/// Boot-Guard der internen Ebene (Multi-Node): stellt beim Cluster-Start sicher, dass <b>jeder</b> Typ,
/// der Node-übergreifend reisen kann, auch einen Serializer-Eintrag hat — sonst Fail-Fast.
///
/// Warum nötig: Single-Node übt Serialisierung nie aus (in-process bleiben rohe CLR-Objekte). Eine
/// fehlende Abdeckung wäre daher <b>still</b> und schlüge erst beim ersten echten Node-Übergang zu.
/// Dieser Check zieht das Versagen an den Boot vor — dieselbe Haltung wie <c>ProzessAzyklizität.PrüfeAlle</c>
/// und <c>GaEinsPruefung</c>.
///
/// Die Domänen-Payloads (offene Menge) kommen aus dem generierten <see cref="GeneratedTypeRegistry"/>,
/// die Framework-Hüllen (geschlossene Menge) aus <see cref="InternalPlaneTypen.FrameworkTypen"/>.
/// </summary>
public static class InternalPlaneCoverage
{
    /// <summary>Wirft <see cref="InvalidOperationException"/>, wenn ein interner Wire-Typ nicht abgedeckt ist.</summary>
    public static void PruefeOderWirf()
    {
        var luecken = FindeLuecken().ToList();
        if (luecken.Count == 0) return;

        throw new InvalidOperationException(
            "Interner Plane-Serializer deckt nicht alle Cross-Node-Typen ab (stille Multi-Node-Lücke). " +
            "Fehlend:\n  - " + string.Join("\n  - ", luecken.Select(t => t.FullName)) +
            "\nAbhilfe: Domänen-Payloads laufen über GeneratedTypeRegistry (Proto-DTO/Regeneration prüfen); " +
            "neue Framework-Wire-Records in InternalPlaneTypen.FrameworkTypen eintragen.");
    }

    /// <summary>Die (hoffentlich leere) Menge nicht abgedeckter Wire-Typen — auch fürs Testen.</summary>
    public static IEnumerable<Type> FindeLuecken()
    {
        var erwartet = GeneratedTypeRegistry.Commands.Values
            .Concat(GeneratedTypeRegistry.Events.Values)
            .Concat(GeneratedTypeRegistry.Signals.Values)
            .Concat(GeneratedTypeRegistry.Triggers.Values)
            .Concat(InternalPlaneTypen.FrameworkTypen)
            .Distinct();

        return erwartet.Where(t => !InternalPlaneTypen.Kennt(t));
    }
}
