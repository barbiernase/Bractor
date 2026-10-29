namespace Abstractions;

/// <summary>
/// Die Namen, über die Generatoren und Werkzeuge eine Projektion (<c>IProjektion</c>), einen Reader (<c>IReader</c>) und die Klasse eines Stores
/// erkennen und binden (docs/04 §4.3, docs/10 §10.3). Die EINE Quelle dieser Regeln — Generatoren (per Link eingebunden,
/// netstandard2.0) und Extractor lesen sie von hier, statt sie je für sich zu kennen (wie <see cref="Funktionsvertrag"/>).
/// </summary>
#if VERTRAG_INTERN
internal static class Projektionsvertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Projektionsvertrag
#else
public static class Projektionsvertrag
#endif
{
    private const string A = "Abstractions.";

    /// <summary>Der Marker einer Projektion.</summary>
    public const string IProjektion = A + "IProjektion";

    /// <summary>Der Marker eines Readers.</summary>
    public const string IReader = A + "IReader";

    /// <summary>Die Methode einer Projektion: <c>Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[, CancellationToken])</c>.</summary>
    public const string Auf = "Auf";

    /// <summary>Die Methode eines Readers: <c>Frage(TQuery q, Lese-Fähigkeiten…[, CancellationToken])</c> → Antwort, <c>OneOf&lt;…&gt;</c>, auch als <c>Task</c>.</summary>
    public const string Frage = "Frage";

    /// <summary>Der volle Name der <c>Herkunft</c> (optionaler Parameter eines <see cref="Auf"/>).</summary>
    public const string Herkunft = A + "Herkunft";

    /// <summary>Die ausdrückliche Erlaubnis an einer Projektion, in einen Store der Klasse 3 zu schreiben (R14).</summary>
    public const string MindestensEinmal = A + "MindestensEinmalAttribute";

    /// <summary>Die Assembly-Angabe des Code-Stands einer Projektion (vom Generator in ihrer eigenen Compilation geschrieben).</summary>
    public const string Projektionsstand = A + "ProjektionsstandAttribute";

    // ── Store-Klassen (§2.4): jeder Store implementiert genau eine ──
    public const string Ziel = A + "IProjektionsZiel";
    public const string Atomar = A + "IAtomaresZiel";
    public const string Wiederholungsfest = A + "IWiederholungsfestesZiel";
    public const string NurMindestensEinmal = A + "INurMindestensEinmal";

    /// <summary>Die drei Klassen in Reihenfolge 1, 2, 3.</summary>
    public static readonly string[] Klassen = { Atomar, Wiederholungsfest, NurMindestensEinmal };

    /// <summary>Die generierte Speicher-Identität (UUID) eines Read-Models mit typisierter Id <c>Id&lt;T&gt; Id</c> — für Stores, die einen einfachen
    /// Schlüssel brauchen (ReadModelSchluesselGenerator, Marten-Schema im ProjectionServicesGenerator).</summary>
    public const string Schluessel = "Schluessel";

    /// <summary>Die Kennung einer Projektion — ihr Typname (Code-Fakt): Schlüssel ihres Fortschritts, ihres Stands und ihrer Spur.</summary>
    public static string Kennung(string projektion) => projektion;

    /// <summary>Das Cluster-Kind der Batch-Leser einer Projektion (Identität = Partition).</summary>
    public static string Kind(string projektion) => "batch-" + projektion;

    /// <summary>Der Wirker einer Projektion in der Spur und der Bereich ihrer <c>Herkunft</c>.</summary>
    public static string Wirker(string projektion) => "projektion:" + projektion;
}
