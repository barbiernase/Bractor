using Abstractions;

namespace Pruefdomaene.Posten;

// ★ SCHEMA-EVOLUTION: das Feld hieß früher `Bestand`, heute `AnfangsBestand` — ohne Upcasting fiele der Wert lautlos auf 0.
//   Die typisierte Wandlung (PostenEroeffnet_V1 → heute) liest der EventUpcastingGenerator aus der IUpcast-Kante.
public record PostenEroeffnet(int AnfangsBestand) : IEvent;

/// <summary>Die erste Gestalt von <see cref="PostenEroeffnet"/> (Feld hieß <c>Bestand</c>). Schlichter Record, kein Event mehr.</summary>
public record PostenEroeffnet_V1(int Bestand);

/// <summary>Wandelt die frühere Gestalt in die heutige: Feld <c>Bestand</c> → <c>AnfangsBestand</c>.</summary>
public sealed class PostenEroeffnet_V1_Wandlung : IUpcast<PostenEroeffnet_V1, PostenEroeffnet>
{
    public PostenEroeffnet Wandle(PostenEroeffnet_V1 alt) => new(alt.Bestand);
}

public record PostenReserviert(int Menge) : IEvent;
public record PostenFreigegeben(int Menge) : IEvent;
public record PostenZugebucht(int Menge) : IEvent;

// ── Ablehnungen (transient — lösen im Prozess die Kompensation aus) ──
public record PostenExistiertBereits(Guid PostenId) : ITransientEvent;
public record PostenNichtGefunden(Guid PostenId) : ITransientEvent;
public record PostenReichtNicht(Guid PostenId, int Verfuegbar, int Angefordert) : ITransientEvent;
