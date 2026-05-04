using System;
using Abstractions;

namespace Domain.Profil
{
    public record ProfilErstellt(Guid AggregateId, string Name) : IEvent;
    public record NameGeändert(string NeuerName) : IEvent;
}