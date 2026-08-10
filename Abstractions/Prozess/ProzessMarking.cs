using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abstractions;

/// <summary>
/// Ein gecachtes Token der Prozess-Faltung: ein gefaltetes Event-Payload plus seine Herkunft
/// (Stream/Version) — die Eingabe der Vorgang-Ableitung und des Join-Matchings. Das prozess-lokale
/// Analogon zum Aggregat-Snapshot, aber auf der Marking-Ebene (P5(b),
/// docs/prozess-marking-cursor-konzept.md §2).
/// </summary>
public sealed class MarkingToken
{
    public IEvent Payload { get; set; } = default!;
    public Guid Stream { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Der VERDICHTETE Ausgang EINES Vorgangs (einer gefeuerten Transition) — die kompakte Darstellung,
/// die den O(N²)-Re-Fold vermeidet (Konzept §4). Statt roher Tokens hält der Cache je aufgelöstem
/// Vorgang genau die DREI Achsen, die der <c>ProzessManager</c> aus dem Voll-Fold liest:
/// <list type="bullet">
///   <item><see cref="ErgebnisDa"/> — irgendein Ziel-Event mit dieser Kausalität liegt vor (auch die
///     interne Inbox-Marke bei Noop/Ablehnung). Steuert „nicht mehr feuern" + Terminal-Erkennung.</item>
///   <item><see cref="WirkungDa"/> + <see cref="Wirkung"/> — ein DOMÄNEN-Event (kein <see cref="IProzessIntern"/>)
///     liegt vor: kompensierbar UND legt ein neues Token in den Fold (schaltet Downstream-Joins scharf).</item>
///   <item><see cref="AbgelehntDa"/> + <see cref="AbgelehntGrund"/> — eine durable <c>KommandoAbgelehnt</c>-Marke
///     mit dieser Kausalität (Treiber-Fold/EM-1): der Manager stempelt daraus <c>SchrittGescheitert</c>.</item>
/// </list>
/// Diese drei Achsen MÜSSEN erhalten bleiben — ein Done-Set allein verlöre Wirkung-vs-Ablehnung, genau
/// die Unterscheidung, an der Kompensation vs. Terminal hängt.
/// </summary>
public sealed class VorgangErgebnis
{
    public Guid Vorgang { get; set; }
    public bool ErgebnisDa { get; set; }
    public bool WirkungDa { get; set; }
    public MarkingToken? Wirkung { get; set; }
    public bool AbgelehntDa { get; set; }
    public string AbgelehntGrund { get; set; } = "abgelehnt";
}

/// <summary>
/// Die verdichtete Faltung (Konzept §2/§4): der aufgelöste Auslöser-Token plus je Vorgang sein
/// <see cref="VorgangErgebnis"/>. Bewusst NICHT „alle Tokens roh" — das Token-Set wird aus den
/// <see cref="Wirkung"/>en rekonstruiert (Auslöser + jede Wirkung = ein Token), sodass der inkrementelle
/// Fixpunkt exakt dieselben Kandidaten wie der Voll-Fold ab 0 bildet.
/// </summary>
public sealed class MarkingKompakt
{
    /// <summary>Der einmalig aufgelöste Auslöser-Token (die Wurzel des Fixpunkts); null bis er gelesen wurde.</summary>
    public MarkingToken? Auslöser { get; set; }

    /// <summary>Je gefeuertem/quittiertem Vorgang der verdichtete Ausgang (die drei Achsen).</summary>
    public List<VorgangErgebnis> Ergebnisse { get; set; } = new();
}

/// <summary>
/// Nicht-autoritativer Cache des gefalteten Prozess-Markings (P5(b)). Ein Dokument pro Korrelation,
/// analog zum Aggregat-Snapshot: der <c>ProzessManager</c> faltet damit pro Weckung nur den TAIL der
/// Ziel-Streams nach (ab <see cref="StreamCursor"/>) statt alles ab 0 → Reads O(N²) → O(N).
///
/// <b>Sicherheit (Konzept §3):</b> der Manager feuert at-least-once und das Ziel dedupliziert über den
/// deterministischen Vorgang; ein falscher/veralteter Cache feuert höchstens eine Transition erneut (die
/// beim Empfänger verpufft), NIE ein falscher Effekt. Fehlt der Cache oder passt <see cref="RegelHash"/>
/// nicht, faltet der Manager sauber ab 0 (Voll-Fold bleibt der Fallback) — „Marking aus dem Log"
/// (Invariante 1) bleibt gewahrt, das Feld ist nur ein Beschleuniger.
/// </summary>
public sealed class ProzessMarking
{
    /// <summary>= Korrelation (die Prozess-Instanz).</summary>
    public Guid Id { get; set; }

    /// <summary>Struktur-Hash der Prozess-Regeln: passt er nicht → Cache verwerfen, Voll-Fold ab 0.</summary>
    public string RegelHash { get; set; } = "";

    /// <summary>Stand des Manager-Entscheidungs-Logs (Beobachtbarkeit; nicht Teil der Fold-Entscheidung).</summary>
    public int LogVersion { get; set; }

    /// <summary>Je bekanntem Ziel-Stream: die zuletzt in den Cache gefaltete Version (Tail-Grenze).</summary>
    public Dictionary<Guid, int> StreamCursor { get; set; } = new();

    /// <summary>Die verdichtete Faltung.</summary>
    public MarkingKompakt Marking { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Best-effort-Store des <see cref="ProzessMarking"/> (Konzept §5): eigene, kurze Session je Zugriff,
/// KEIN Co-Commit mit der Entscheidungs-Transaktion (die Entscheidungen bleiben OCC im Manager-Log).
/// Geht der Cache verloren oder ist er inkonsistent, faltet der Manager wieder ab 0 und der Empfänger
/// dedupliziert — kein Datenverlust, höchstens ein einmaliges Re-Emit. Exakt die Semantik des
/// <see cref="IEmittentenCursor"/>, nur mit verdichtetem Marking statt bloßer Position.
/// </summary>
public interface IProzessMarkingStore
{
    Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct);
    Task SchreibeAsync(ProzessMarking marking, CancellationToken ct);
}
