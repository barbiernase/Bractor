using Abstractions;

namespace Pruefdomaene.Posten;

// ★ SCHEMA-EVOLUTION: das Feld hieß früher `Bestand`, heute `AnfangsBestand` — ohne Upcasting fiele der Wert lautlos auf 0.
//   Die typisierte Wandlung (PostenEroeffnet_V1 → heute) liest der EventUpcastingGenerator aus der IUpcast-Kante.
/// <summary>Ein Posten ist eröffnet; unterschreitet eine Reservierung den <paramref name="Mindestbestand"/>, bestellt die Reaktion
/// <c>NachschubBeiMindestbestand</c> die Fehlmenge nach (0 = kein Nachschub).</summary>
public record PostenEroeffnet(int AnfangsBestand, int Mindestbestand = 0) : IEvent;

/// <summary>Die erste Gestalt von <see cref="PostenEroeffnet"/> (Feld hieß <c>Bestand</c>). Schlichter Record, kein Event mehr.</summary>
public record PostenEroeffnet_V1(int Bestand);

/// <summary>Wandelt die frühere Gestalt in die heutige: Feld <c>Bestand</c> → <c>AnfangsBestand</c>.</summary>
public sealed class PostenEroeffnet_V1_Wandlung : IUpcast<PostenEroeffnet_V1, PostenEroeffnet>
{
    public PostenEroeffnet Wandle(PostenEroeffnet_V1 alt) => new(alt.Bestand);
}

/// <summary>Eine Menge ist reserviert; <paramref name="Fehlmenge"/> sagt, wie weit der verfügbare Bestand danach unter dem Mindestbestand liegt.</summary>
public record PostenReserviert(int Menge, int Fehlmenge = 0) : IEvent;
public record PostenFreigegeben(int Menge) : IEvent;
public record PostenZugebucht(int Menge) : IEvent;

// ── Ablehnungen (transient — lösen im Prozess die Kompensation aus) ──
public record PostenExistiertBereits(Id<Posten> PostenId) : ITransientEvent;
public record PostenNichtGefunden(Id<Posten> PostenId) : ITransientEvent;
public record PostenReichtNicht(Id<Posten> PostenId, int Verfuegbar, int Angefordert) : ITransientEvent;
