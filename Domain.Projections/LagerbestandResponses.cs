using System;
using System.Collections.Generic;
using Abstractions;

namespace Domain.Projections;

public record LagerbestandAntwort(
    Guid Id, string Name, int Anzahl,
    DateTimeOffset LetzteAktualisierung) : IQueryResponse;

public record LagerbestandNichtGefunden(Guid ArtikelId) : IQueryResponse;

public record LagerbestandListe(
    IReadOnlyList<LagerbestandAntwort> Items) : IQueryResponse;