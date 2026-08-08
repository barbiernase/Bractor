namespace Client.Infrastructure.Abstractions;
public interface ISidebarModule : IUiModule
{
    SidebarSide Side       { get; }
    int ExpandedWidth      => 180;

    /// <summary>
    /// Breite des klickbaren Balkens (sichtbar im collapsed UND expanded Zustand).
    /// Default 28px — schmal genug um nicht zu stören, breit genug für vertikalen Text.
    /// </summary>
    int BarWidth           => 28;

    /// <summary>
    /// Text im klickbaren Balken. Default: Title.
    /// Module können überschreiben um z.B. Kurzform oder Kontext-Info zu zeigen.
    /// </summary>
    string BarTitle        => Title;
}
