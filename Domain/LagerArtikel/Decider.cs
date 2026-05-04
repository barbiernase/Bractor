using System;
using System.Collections.Generic;
using Abstractions;

namespace Domain.Lagerartikel
{
    public partial class Lagerartikel
    {
        public partial class Decider : IDecider<Lagerartikel>
        {
            public IEnumerable<OneOf<LagerartikelErstellt, ArtikelExistiertBereits, EingabeUngueltig>> Decide(
                ErstelleLagerartikel cmd)
            {
                if (this.State.Version > 0)
                {
                    yield return new ArtikelExistiertBereits(cmd.AggregateId);
                    yield break;
                }
                if (string.IsNullOrWhiteSpace(cmd.Name))
                {
                    yield return new EingabeUngueltig("Name", "darf nicht leer sein");
                    yield break;
                }
                if (cmd.InitialeAnzahl < 0)
                {
                    yield return new EingabeUngueltig("InitialeAnzahl", "darf nicht negativ sein");
                    yield break;
                }
                yield return new LagerartikelErstellt(cmd.AggregateId, cmd.Name, cmd.InitialeAnzahl);
            }

            public IEnumerable<OneOf<WareneingangGebucht, ArtikelNichtGefunden, ArtikelInaktiv, EingabeUngueltig>> Decide(
                BucheWareneingang cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (!this.State.IstAktiv)
                {
                    yield return new ArtikelInaktiv(cmd.AggregateId);
                    yield break;
                }
                if (cmd.Anzahl <= 0)
                {
                    yield return new EingabeUngueltig("Anzahl", "muss positiv sein");
                    yield break;
                }
                yield return new WareneingangGebucht(cmd.Anzahl);
            }

            public IEnumerable<OneOf<WarenabgangGebucht, ArtikelNichtGefunden, BestandNichtAusreichend, EingabeUngueltig>> Decide(
                BucheWarenabgang cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (cmd.Anzahl <= 0)
                {
                    yield return new EingabeUngueltig("Anzahl", "muss positiv sein");
                    yield break;
                }
                if (this.State.AnzLagernd < cmd.Anzahl)
                {
                    yield return new BestandNichtAusreichend(cmd.Anzahl, this.State.AnzLagernd);
                    yield break;
                }
                yield return new WarenabgangGebucht(cmd.Anzahl);
            }

            public IEnumerable<OneOf<LagerartikelDeaktiviert, ArtikelNichtGefunden>> Decide(
                DeaktiviereLagerartikel cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (!this.State.IstAktiv) yield break; // Bereits inaktiv → Noop, kein Fehler
                yield return new LagerartikelDeaktiviert();
            }

            public IEnumerable<OneOf<LagerplatzGeaendert, ArtikelNichtGefunden>> Decide(
                AendereLagerplatz cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (this.State.Platz == cmd.NeuerPlatz) yield break; // Keine Änderung → Noop
                yield return new LagerplatzGeaendert(cmd.NeuerPlatz);
            }

            public IEnumerable<OneOf<EinkaufspreisAngepasst, ArtikelNichtGefunden, EingabeUngueltig>> Decide(
                PasseEinkaufspreisAn cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (cmd.NeuerPreis.Wert < 0)
                {
                    yield return new EingabeUngueltig("NeuerPreis", "darf nicht negativ sein");
                    yield break;
                }
                yield return new EinkaufspreisAngepasst(cmd.NeuerPreis);
            }

            public IEnumerable<OneOf<LieferantZugeordnet, ArtikelNichtGefunden>> Decide(
                OrdneLieferantZu cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (this.State.LieferantId == cmd.LieferantId) yield break; // Gleich → Noop
                yield return new LieferantZugeordnet(cmd.LieferantId);
            }

            public IEnumerable<OneOf<BestandReserviert, ArtikelNichtGefunden, VerfuegbarerBestandNichtAusreichend, EingabeUngueltig>> Decide(
                ReserviereBestand cmd)
            {
                if (this.State.Version == 0)
                {
                    yield return new ArtikelNichtGefunden(cmd.AggregateId);
                    yield break;
                }
                if (cmd.Anzahl <= 0)
                {
                    yield return new EingabeUngueltig("Anzahl", "muss positiv sein");
                    yield break;
                }
                if (this.State.AnzVerfuegbar < cmd.Anzahl)
                {
                    yield return new VerfuegbarerBestandNichtAusreichend(cmd.Anzahl, this.State.AnzVerfuegbar);
                    yield break;
                }
                yield return new BestandReserviert(cmd.AuftragsId, cmd.Anzahl);
            }
        }
    }
}