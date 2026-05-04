using System;
using Abstractions;

namespace Domain.Lagerartikel;

public record ErstelleLagerartikel(Guid AggregateId, string Name, int InitialeAnzahl) : ICreationCommand;
public record BucheWareneingang(Guid AggregateId, int Anzahl) : ICommand;
public record BucheWarenabgang(Guid AggregateId, int Anzahl) : ICommand;
public record DeaktiviereLagerartikel(Guid AggregateId) : ICommand;
    
public record AendereLagerplatz(Guid AggregateId, Lagerplatz NeuerPlatz) : ICommand;
public record PasseEinkaufspreisAn(Guid AggregateId, Geldbetrag NeuerPreis) : ICommand;
public record OrdneLieferantZu(Guid AggregateId, Guid LieferantId) : ICommand;
public record ReserviereBestand(Guid AggregateId, Guid AuftragsId, int Anzahl) : ICommand;