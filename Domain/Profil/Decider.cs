using System;
using System.Collections.Generic;
using Abstractions;

namespace Domain.Profil
{
    public partial class Profil
    {
        public partial class Decider : IDecider<Profil>
        {
           

            public IEnumerable<IEvent> Decide(ErstelleProfil cmd)
            {
                if (State.Version > 0) throw new InvalidOperationException("Profile already exists.");
                if (string.IsNullOrWhiteSpace(cmd.Name)) throw new InvalidOperationException("Name cannot be empty.");
                yield return new ProfilErstellt(cmd.AggregateId, cmd.Name);
            }
            public IEnumerable<IEvent> Decide(ÄndereProfilnamen cmd)
            {
                if (State.Version == 0) throw new InvalidOperationException("Profile does not exist.");
                if (State.Name == cmd.NeuerName) yield break;
                yield return new NameGeändert(cmd.NeuerName);
            }
        }
    }
}