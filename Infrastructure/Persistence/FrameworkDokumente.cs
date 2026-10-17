using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>Die Marten-Dokumente des Frameworks — registriert vom Host (<c>AddCqrs</c>) und von den Plan-Tests mit derselben Konfiguration.</summary>
public static class FrameworkDokumente
{
    public static void Registriere(StoreOptions options)
    {
        // Durabler Poll-Backstop-Cursor pro Pull-Pfad der Emittenten: der Poller setzt nach einem Neustart hier wieder auf, statt beim Boot die
        //   ganze Historie zu re-scannen.
        options.Schema.For<PollCursor>().Identity(x => x.Id);

        // Emittenten-Cursor (P4): best-effort Fortschritt eines EMITTIERENDEN Konsumenten (Reaktion / Pipeline-Event) pro Partition — kein
        //   Co-Commit, Verlust heilt der Voll-Fold.
        options.Schema.For<EmittentenCursorDoc>().Identity(x => x.Id);

        // Durabler Offen-Index der Prozesse (§3-Backstop): eine O(offen)-Menge, aus der ein periodischer Scan hängende Prozesse heilt. Der Index
        //   über die Frist trägt die Abfrage der fälligen jede Sekunde (sonst die ganze Tabelle, PF-17; AbfragePlanGarantieTests).
        options.Schema.For<ProzessOffen>().Identity(x => x.Id).Index(x => x.Frist);

        // Dead-Letter-Queue (§5): nicht zustellbare ausgehende Pipeline-Commands, beobachtbar + replay-bar.
        options.Schema.For<DeadLetter>().Identity(x => x.Id).DatabaseSchemaName("dlq");

        // Durabler Fristplan (Deadlines): eine Frist je offener Deadline; der DB-Uhr-getriebene Scheduler feuert die fälligen und entfernt sie.
        options.Schema.For<Frist>().Identity(x => x.Id).Index(x => x.Fällig);

        // Der Prozess-Marking-Cursor (docs/04 §4.5): ein jsonb-Dokument je Korrelation, abgeleiteter Cache des gefalteten Markings.
        options.Schema.For<ProzessMarkingDoc>().Identity(x => x.Id);
    }
}
