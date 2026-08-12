namespace Domain.DddPatterns.Spezifikation;

/// <summary>
/// DDD-Baustein SPECIFICATION — eine benannte, wiederverwendbare fachliche Regel als
/// Objekt. Statt eine Bedingung als <c>if</c> im Aggregat, im Query und im UI dreimal
/// zu duplizieren, lebt sie einmal als Spezifikation und wird komponiert.
/// </summary>
public interface ISpezifikation<in T>
{
    bool IstErfuellt(T kandidat);
}

/// <summary>
/// Basisklasse mit den booleschen Kombinatoren (<see cref="Und"/>/<see cref="Oder"/>/
/// <see cref="Nicht"/>). So entstehen aus kleinen Regeln größere, ohne die einzelnen
/// Regeln anzufassen — offen für Erweiterung, geschlossen für Änderung.
/// </summary>
public abstract class Spezifikation<T> : ISpezifikation<T>
{
    public abstract bool IstErfuellt(T kandidat);

    public Spezifikation<T> Und(ISpezifikation<T> andere) => new UndSpezifikation<T>(this, andere);
    public Spezifikation<T> Oder(ISpezifikation<T> andere) => new OderSpezifikation<T>(this, andere);
    public Spezifikation<T> Nicht() => new NichtSpezifikation<T>(this);

    /// <summary>Hebt ein Prädikat zu einer Spezifikation — für Ad-hoc-Regeln ohne eigene Klasse.</summary>
    public static Spezifikation<T> Aus(Func<T, bool> praedikat) => new PraedikatSpezifikation<T>(praedikat);
}

internal sealed class PraedikatSpezifikation<T> : Spezifikation<T>
{
    private readonly Func<T, bool> _praedikat;
    public PraedikatSpezifikation(Func<T, bool> praedikat) => _praedikat = praedikat;
    public override bool IstErfuellt(T kandidat) => _praedikat(kandidat);
}

internal sealed class UndSpezifikation<T> : Spezifikation<T>
{
    private readonly ISpezifikation<T> _links, _rechts;
    public UndSpezifikation(ISpezifikation<T> links, ISpezifikation<T> rechts) { _links = links; _rechts = rechts; }
    public override bool IstErfuellt(T k) => _links.IstErfuellt(k) && _rechts.IstErfuellt(k);
}

internal sealed class OderSpezifikation<T> : Spezifikation<T>
{
    private readonly ISpezifikation<T> _links, _rechts;
    public OderSpezifikation(ISpezifikation<T> links, ISpezifikation<T> rechts) { _links = links; _rechts = rechts; }
    public override bool IstErfuellt(T k) => _links.IstErfuellt(k) || _rechts.IstErfuellt(k);
}

internal sealed class NichtSpezifikation<T> : Spezifikation<T>
{
    private readonly ISpezifikation<T> _innere;
    public NichtSpezifikation(ISpezifikation<T> innere) => _innere = innere;
    public override bool IstErfuellt(T k) => !_innere.IstErfuellt(k);
}
