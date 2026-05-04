using Abstractions;

namespace Domain.Profil
{
    public partial class Profil
    {
        public partial class Applier : IApplier<Profil>
        {
           
            public void Apply(ProfilErstellt evt)
            {
                State.Id = evt.AggregateId;
                State.Name = evt.Name;
            }
            public void Apply(NameGeändert evt)
            {
                State.Name = evt.NeuerName;
            }
        }
    }
}