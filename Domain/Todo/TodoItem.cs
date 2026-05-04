using Abstractions;

namespace Domain.Todo;

public partial class TodoItem : IState
{
    public string Titel { get; set; } = string.Empty;
    public string? Beschreibung { get; set; }
    public TodoStatus Status { get; set; }
    public Prioritaet Prioritaet { get; set; }
    public DateTimeOffset? Faelligkeit { get; set; }
    public DateTimeOffset ErstelltAm { get; set; }
    public DateTimeOffset? ErledigtAm { get; set; }
    public List<string> Tags { get; set; } = new();

    public bool IstOffen => Status == TodoStatus.Offen;
    public bool IstErledigt => Status == TodoStatus.Erledigt;
    public bool IstArchiviert => Status == TodoStatus.Archiviert;
}