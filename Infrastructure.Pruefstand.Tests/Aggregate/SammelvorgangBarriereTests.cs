using System;
using Abstractions;
using Cqrs.Testing;
using Domain.Sammelvorgang;
using Xunit;

namespace Infrastructure.Pruefstand.Aggregate;

/// <summary>
/// Die „warte auf ALLE N"-Barriere (Fan-in) als skalares Zähl-Aggregat — store-frei bewiesen.
///
/// Kernpunkt: N ist ein LAUFZEITWERT (Argument von <see cref="StarteSammelvorgang"/>), statisch
/// nicht bekannt. Der Decider vergleicht zur Laufzeit gegen <c>State.Erwartet</c> und feuert den
/// Abschluss erst beim N-ten Teil. Kein <c>UndAlle</c>/Collection nötig — nur zwei Zähler.
/// </summary>
public class SammelvorgangBarriereTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Infrastructure.AggregateHandlerFactory();

    [Fact]
    public void Barriere_feuert_erst_beim_letzten_Teil_N_zur_Laufzeit()
    {
        var id = Guid.NewGuid();
        // Gestartet mit N=3 (Laufzeitwert) + 2 Teile bereits verbucht → der 3. löst die Barriere aus.
        Szenario.Für<Sammelvorgang>(Fabrik, id)
            .Gegeben(new SammelvorgangGestartet(3), new TeilVerbucht(1, 3), new TeilVerbucht(2, 3))
            .Wenn(new MeldeTeilFertig(id))
            .Dann<TeilVerbucht>()
            .Dann<SammelvorgangAbgeschlossen>()
            .UndZustand(s => s.Abgeschlossen && s.Fertig == 3, "beim N-ten Teil ist die Barriere erreicht");
    }

    [Fact]
    public void Vor_dem_letzten_Teil_nur_Fortschritt_ohne_Barriere()
    {
        var id = Guid.NewGuid();
        Szenario.Für<Sammelvorgang>(Fabrik, id)
            .Gegeben(new SammelvorgangGestartet(3), new TeilVerbucht(1, 3))
            .Wenn(new MeldeTeilFertig(id))
            .Dann<TeilVerbucht>()
            .DannKeineAblehnung()
            .UndZustand(s => !s.Abgeschlossen && s.Fertig == 2, "erst 2 von 3 — noch nicht fertig");
    }

    [Fact]
    public void Anderes_N_zeigt_dass_die_Zahl_nicht_statisch_ist()
    {
        var id = Guid.NewGuid();
        // Dieselbe Logik, aber N=1 → schon der erste Teil schließt ab. N steckt allein in den Daten.
        Szenario.Für<Sammelvorgang>(Fabrik, id)
            .Gegeben(new SammelvorgangGestartet(1))
            .Wenn(new MeldeTeilFertig(id))
            .Dann<SammelvorgangAbgeschlossen>()
            .UndZustand(s => s.Abgeschlossen && s.Fertig == 1);
    }

    [Fact]
    public void Spaeter_Teil_nach_Abschluss_wird_abgelehnt()
    {
        var id = Guid.NewGuid();
        Szenario.Für<Sammelvorgang>(Fabrik, id)
            .Gegeben(
                new SammelvorgangGestartet(2),
                new TeilVerbucht(1, 2), new TeilVerbucht(2, 2),
                new SammelvorgangAbgeschlossen())
            .Wenn(new MeldeTeilFertig(id))
            .DannAbgelehnt<SammelvorgangBereitsAbgeschlossen>();
    }

    [Fact]
    public void Leere_Menge_N_gleich_0_ist_sofort_abgeschlossen()
    {
        var id = Guid.NewGuid();
        Szenario.Für<Sammelvorgang>(Fabrik, id)
            .Wenn(new StarteSammelvorgang(id, 0))
            .Dann<SammelvorgangGestartet>()
            .Dann<SammelvorgangAbgeschlossen>()
            .UndZustand(s => s.Abgeschlossen);
    }
}
