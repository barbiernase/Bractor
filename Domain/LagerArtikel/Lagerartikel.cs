using Abstractions;

namespace Domain.Lagerartikel
{
    public partial class Lagerartikel : IState
    {
        public string Name { get; set; } = string.Empty;
        public int AnzLagernd { get; set; }
        public bool IstAktiv { get; set; }

        // --- NEUE EIGENSCHAFTEN ---
        public Lagerplatz? Platz { get; set; }
        public Geldbetrag? Einkaufspreis { get; set; }
        public Guid? LieferantId { get; set; } // Verweis auf ein anderes Aggregat
        
        // Reservierter Bestand für bestimmte Aufträge
        public Dictionary<Guid, int> Reservierungen { get; } = new();
        public int AnzReserviert => Reservierungen.Values.Sum();
        public int AnzVerfuegbar => AnzLagernd - AnzReserviert;
    }
}