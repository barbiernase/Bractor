using System;
using Abstractions;

namespace Domain.Profil
{
    public record ErstelleProfil(Guid AggregateId, string Name) : ICreationCommand;
    public record ÄndereProfilnamen(Guid AggregateId, string NeuerName) : ICommand;
}