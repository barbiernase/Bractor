namespace Client.Infrastructure.Abstractions;
public interface IUiModule
{
    string Id              { get; }
    string Title           { get; }
    Type   ComponentType   { get; }
    bool   DefaultVisible  => true;
    int    Order           => 0;
    IReadOnlyList<KeyBinding> KeyBindings => [];
}
