using Abstractions;
using Domain.DddPatterns.Gemeinsam;
using Domain.DddPatterns.Repository;

namespace Domain.DddPatterns.Bestellung;

/// <summary>Lebenszyklus-Status der Bestellung — Teil der Ubiquitous Language.</summary>
public enum Bestellstatus { Offen, Aufgegeben, Storniert }

/// <summary>
/// DDD-Baustein AGGREGATE ROOT — die Konsistenzgrenze der Bestell-Domäne. Sie ist der
/// EINZIGE Einstieg zu ihren Child-Entities (<see cref="Bestellposition"/>): außen kommt
/// niemand an eine Position vorbei an der Wurzel. Die Wurzel erzwingt ALLE Invarianten:
///   • Positionen nur ändern, solange die Bestellung offen ist,
///   • gleiche Position (ArtikelNr) verschmilzt Mengen statt sich zu duplizieren,
///   • die Gesamtsumme überschreitet nie das Kreditlimit,
///   • alle Beträge in derselben Währung,
///   • Aufgeben nur mit mindestens einer Position.
///
/// Zustandsänderungen laufen ausschließlich über DOMAIN EVENTS (event-sourced): eine
/// Command-Methode prüft die Invariante, erzeugt ein Ereignis und WENDET es an. Damit ist
/// die Historie die Wahrheit — und die Rekonstitution (<see cref="AusHistorie"/>) faltet
/// exakt dieselben Ereignisse. Trägt bewusst den Framework-Marker <see cref="IState"/>.
/// </summary>
public sealed class Bestellung : IState, IEreignisGespeist<IDomaenenEreignis>
{
    // Framework-Pflichtfelder (im echten Betrieb vom Actor gesetzt).
    public Guid Id { get; set; }
    public int Version { get; set; }

    public Guid KundeId { get; private set; }
    public Geld Kreditlimit { get; private set; }
    public Bestellstatus Status { get; private set; } = Bestellstatus.Offen;

    private readonly Dictionary<string, Bestellposition> _positionen = new();
    public IReadOnlyCollection<Bestellposition> Positionen => _positionen.Values.ToList();

    // Laufender Saldo: hält Gesamtsumme auf O(1), sonst würde das Kreditlimit-Prüfen
    // beim Hinzufügen jede Position neu aufsummieren (O(N²) über die ganze Bestellung).
    private Geld _gesamtsumme;

    private readonly List<IDomaenenEreignis> _neueEreignisse = new();
    /// <summary>Die seit dem Laden neu erzeugten Ereignisse (das Repository persistiert sie).</summary>
    public IReadOnlyList<IDomaenenEreignis> NeueEreignisse => _neueEreignisse;
    public void EreignisseUebernommen() => _neueEreignisse.Clear();

    private Bestellung() { }

    // ── FACTORY: Erzeugung mit Invariante ──

    /// <summary>Fabrik-Methode: eröffnet eine neue, leere Bestellung für einen Kunden.</summary>
    public static Bestellung Eroeffne(Guid id, Guid kundeId, Geld kreditlimit)
    {
        DomaeneException.Verlange(id != Guid.Empty, "Bestellung braucht eine Id.");
        DomaeneException.Verlange(kundeId != Guid.Empty, "Bestellung braucht einen Kunden.");
        var bestellung = new Bestellung();
        bestellung.Wende(new BestellungEroeffnet(id, kundeId, kreditlimit));
        return bestellung;
    }

    /// <summary>
    /// FACTORY (Rekonstitution): baut den aktuellen Zustand aus der Ereignis-Historie —
    /// ohne die Ereignisse als „neu" zu markieren (sie sind ja schon persistiert). Das ist
    /// der Faden, an dem das <see cref="Repository.IRepository{T}"/> zieht.
    /// </summary>
    public static Bestellung AusHistorie(IEnumerable<IDomaenenEreignis> historie)
    {
        var bestellung = new Bestellung();
        foreach (var ereignis in historie)
            bestellung.Anwenden(ereignis);
        return bestellung;
    }

    // ── Abgeleiteter Zustand ──

    /// <summary>Die Summe aller Zeilensummen (O(1), inkrementell gepflegt) — Verhalten IN der Wurzel.</summary>
    public Geld Gesamtsumme => _gesamtsumme;

    // ── COMMAND-Methoden: Invariante prüfen → Ereignis → anwenden ──

    public void FuegePositionHinzu(string artikelNr, string bezeichnung, int menge, Geld stueckpreis)
    {
        VerlangeOffen();
        DomaeneException.Verlange(menge > 0, $"Menge muss positiv sein: {menge}");
        DomaeneException.Verlange(stueckpreis.Waehrung == Kreditlimit.Waehrung,
            $"Position muss in {Kreditlimit.Waehrung} sein, war {stueckpreis.Waehrung}.");

        if (_positionen.TryGetValue(artikelNr, out var vorhanden))
        {
            var neueMenge = vorhanden.Menge + menge;
            var zuwachs = vorhanden.Stueckpreis.Mal(menge);
            VerlangeKreditlimitHaelt(Gesamtsumme.Plus(zuwachs));
            Wende(new PositionMengeGeaendert(artikelNr, neueMenge));
        }
        else
        {
            VerlangeKreditlimitHaelt(Gesamtsumme.Plus(stueckpreis.Mal(menge)));
            Wende(new PositionHinzugefuegt(artikelNr, bezeichnung, menge, stueckpreis));
        }
    }

    public void AendereMenge(string artikelNr, int neueMenge)
    {
        VerlangeOffen();
        var position = Position(artikelNr);
        DomaeneException.Verlange(neueMenge > 0, $"Menge muss positiv sein: {neueMenge}");

        var ohneAlt = Gesamtsumme.Minus(position.Zeilensumme);
        VerlangeKreditlimitHaelt(ohneAlt.Plus(position.Stueckpreis.Mal(neueMenge)));
        Wende(new PositionMengeGeaendert(artikelNr, neueMenge));
    }

    public void EntfernePosition(string artikelNr)
    {
        VerlangeOffen();
        _ = Position(artikelNr); // wirft, wenn es sie nicht gibt
        Wende(new PositionEntfernt(artikelNr));
    }

    public void GibAuf()
    {
        VerlangeOffen();
        DomaeneException.Verlange(_positionen.Count > 0, "Eine leere Bestellung kann nicht aufgegeben werden.");
        Wende(new BestellungAufgegeben(Id, Gesamtsumme));
    }

    public void Storniere(string grund)
    {
        DomaeneException.Verlange(Status != Bestellstatus.Storniert, "Bestellung ist bereits storniert.");
        Wende(new BestellungStorniert(Id, grund));
    }

    // ── Invarianten-Guards ──

    private void VerlangeOffen() =>
        DomaeneException.Verlange(Status == Bestellstatus.Offen,
            $"Positionen sind nur änderbar, solange die Bestellung offen ist (Status: {Status}).");

    private void VerlangeKreditlimitHaelt(Geld prospektiveSumme) =>
        DomaeneException.Verlange(!prospektiveSumme.GroesserAls(Kreditlimit),
            $"Kreditlimit überschritten: {prospektiveSumme} > {Kreditlimit}.");

    private Bestellposition Position(string artikelNr)
    {
        DomaeneException.Verlange(_positionen.ContainsKey(artikelNr), $"Keine Position mit Artikelnummer {artikelNr}.");
        return _positionen[artikelNr];
    }

    // ── Ereignis-Anwendung (die einzige Stelle, die Zustand mutiert) ──

    private void Wende(IDomaenenEreignis ereignis)
    {
        Anwenden(ereignis);
        _neueEreignisse.Add(ereignis);
    }

    private void Anwenden(IDomaenenEreignis ereignis)
    {
        switch (ereignis)
        {
            case BestellungEroeffnet e:
                Id = e.BestellungId;
                KundeId = e.KundeId;
                Kreditlimit = e.Kreditlimit;
                Status = Bestellstatus.Offen;
                _gesamtsumme = Geld.Null(e.Kreditlimit.Waehrung);
                break;

            case PositionHinzugefuegt e:
                _positionen[e.ArtikelNr] = new Bestellposition(e.ArtikelNr, e.Bezeichnung, e.Menge, e.Stueckpreis);
                _gesamtsumme = _gesamtsumme.Plus(e.Stueckpreis.Mal(e.Menge));
                break;

            case PositionMengeGeaendert e:
                var position = _positionen[e.ArtikelNr];
                _gesamtsumme = _gesamtsumme.Minus(position.Zeilensumme);
                position.SetzeMenge(e.NeueMenge);
                _gesamtsumme = _gesamtsumme.Plus(position.Zeilensumme);
                break;

            case PositionEntfernt e:
                _gesamtsumme = _gesamtsumme.Minus(_positionen[e.ArtikelNr].Zeilensumme);
                _positionen.Remove(e.ArtikelNr);
                break;

            case BestellungAufgegeben:
                Status = Bestellstatus.Aufgegeben;
                break;

            case BestellungStorniert:
                Status = Bestellstatus.Storniert;
                break;
        }

        Version++;
    }
}
