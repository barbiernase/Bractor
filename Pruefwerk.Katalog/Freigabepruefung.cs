using Abstractions;

namespace Pruefwerk.Katalog;

/// <summary>Gibt einen Betrag bis zur Grenze frei, darüber legt sie ihn zur Prüfung vor — eine WAHL.</summary>
public sealed class Freigabepruefung : IFunktion
{
    public const decimal Grenze = 10_000;

    public OneOf<Freigegeben, ZurPruefungVorzulegen> Auf(Pruefe p) =>
        p.Betrag > Grenze ? new ZurPruefungVorzulegen(p.Vorgang) : new Freigegeben(p.Vorgang);
}
