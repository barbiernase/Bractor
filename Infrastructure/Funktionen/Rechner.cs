namespace Infrastructure.Funktionen;

/// <summary>
/// Der RECHNER dieses Hosts (docs/04 §4.4, <c>Cluster:Rechner</c>): eine Instanz, die einen Rechner nennt, läuft nur auf ihm — eine Quelle an ihrer
/// Steuerung, ein Modell auf seiner GPU. Ein Schreiber folgt so aus der Konfiguration; fällt der Rechner aus, zieht nichts um, die durablen Eingänge
/// warten. Im Einzelknoten-Betrieb ist der eine Host der Rechner jeder Instanz.
/// </summary>
/// <param name="Name">Der Name dieser Maschine; null = keiner (der Host bedient nur Instanzen ohne Rechner).</param>
/// <param name="Einzelknoten">Besteht der Cluster nur aus diesem Host?</param>
public sealed record EigenerRechner(string? Name, bool Einzelknoten)
{
    public static readonly EigenerRechner Keiner = new(null, Einzelknoten: false);

    /// <summary>Bedient dieser Host die Instanz — nennt sie keinen Rechner oder diesen (im Einzelknoten-Betrieb jede)?</summary>
    public bool Bedient(GraphInstanz instanz) => Einzelknoten || instanz.Rechner is null || instanz.Rechner == Name;

    /// <summary>
    /// Die Pflicht (Boot-Fehler): im Cluster nennt eine Instanz aus der Umgebung, die Zustand oder eine Quelle hat, ihren Rechner — sonst rechneten
    /// mehrere Hosts mit eigenem Gedächtnis bzw. lauschten doppelt. <paramref name="mitZustand"/> sagt, ob eine Instanz Zustand oder Quelle hat.
    /// </summary>
    public void Pruefe(GraphInstanzen instanzen, Func<GraphInstanz, bool> mitZustand)
    {
        if (Einzelknoten || !instanzen.AusUmgebung) return;
        foreach (var i in instanzen.Alle.Where(i => i.Rechner is null && mitZustand(i)))
            throw new Infrastructure.Umgebung.BootFehler($"Instanz {i.Name}: sie hat {(i.Graph.Quelle is not null ? "eine Quelle" : "Zustand")} und nennt keinen Rechner — "
                + "im Cluster läuft sie genau auf einem: \"Rechner\": \"<Name>\" in ihrer Datei, derselbe Name als Cluster:Rechner des Hosts.", "instanz:" + i.Name);
    }
}
